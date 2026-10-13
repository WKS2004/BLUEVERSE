"""AI-NET-001: only the private Safety Operations agent may reach Gemini through the proxy."""

from __future__ import annotations

import os
import socket
import unittest
from unittest.mock import patch
from urllib.error import URLError

from services.agents.gemini_egress_proxy import (
    GEMINI_HOST,
    GEMINI_PORT,
    MAX_HEADER_BYTES,
    ProxyRequestError,
    _read_connect_request,
    connect_to_gemini,
    parse_connect_request,
)
from services.agents.safety_operations.gemini import (
    GEMINI_API_HOST,
    GEMINI_EGRESS_PROXY_URL,
    GeminiConfig,
    GeminiConfigurationError,
    UrllibTransport,
    _RequiredGeminiProxyHandler,
)


def connect_request(
    authority: str = f"{GEMINI_HOST}:{GEMINI_PORT}",
    host: str | None = None,
    headers: tuple[str, ...] = (),
) -> bytes:
    host_header = f"Host: {host if host is not None else authority}\r\n"
    return (
        f"CONNECT {authority} HTTP/1.1\r\n{host_header}{''.join(item + chr(13) + chr(10) for item in headers)}\r\n"
    ).encode("ascii")


class _Request:
    type = "https"
    host = GEMINI_API_HOST

    def __init__(self) -> None:
        self.proxy: tuple[str, str] | None = None

    def set_proxy(self, host: str, proxy_type: str) -> None:
        self.proxy = (host, proxy_type)


class GeminiProxyBoundaryTests(unittest.TestCase):
    def test_ai_net_001_valid_connect_targets_only_gemini_tls(self) -> None:
        self.assertEqual((GEMINI_HOST, 443), parse_connect_request(connect_request()))

    def test_ai_net_001_rejects_non_connect_other_hosts_and_non_tls_ports(self) -> None:
        cases = (
            (b"GET https://generativelanguage.googleapis.com/ HTTP/1.1\r\nHost: generativelanguage.googleapis.com\r\n\r\n", 405),
            (connect_request("attacker.example:443"), 403),
            (connect_request(f"{GEMINI_HOST}:80"), 403),
            (connect_request(host="attacker.example:443"), 403),
        )
        for request, status in cases:
            with self.subTest(request=request[:80]), self.assertRaises(ProxyRequestError) as raised:
                parse_connect_request(request)
            self.assertEqual(status, raised.exception.status)

    def test_ai_net_001_rejects_duplicate_or_credential_proxy_headers(self) -> None:
        duplicate_host = (
            f"CONNECT {GEMINI_HOST}:443 HTTP/1.1\r\n"
            f"Host: {GEMINI_HOST}:443\r\nHost: {GEMINI_HOST}:443\r\n\r\n"
        ).encode("ascii")
        for request in (
            duplicate_host,
            connect_request(headers=("Proxy-Authorization: Basic c2VjcmV0",)),
            connect_request(headers=("X-Forwarded-Host: attacker.example",)),
        ):
            with self.subTest(request=request[:100]), self.assertRaises(ProxyRequestError):
                parse_connect_request(request)

    def test_ai_net_001_rejects_oversized_connect_headers(self) -> None:
        request = b"CONNECT " + (b"a" * MAX_HEADER_BYTES) + b"\r\n"
        with self.assertRaises(ProxyRequestError) as raised:
            parse_connect_request(request)
        self.assertEqual(431, raised.exception.status)

    def test_ai_net_001_rejects_tls_bytes_before_connect_is_established(self) -> None:
        class BufferedClient:
            def __init__(self, payload: bytes) -> None:
                self.payload = payload

            def recv(self, _maximum: int) -> bytes:
                payload, self.payload = self.payload, b""
                return payload

        client = BufferedClient(connect_request() + b"\x16\x03\x01")
        with self.assertRaises(ProxyRequestError) as raised:
            _read_connect_request(client)  # type: ignore[arg-type]
        self.assertEqual(400, raised.exception.status)

    def test_ai_net_001_dns_private_addresses_never_open_sockets(self) -> None:
        created: list[object] = []

        def resolver(host: str, port: int, *, type: int):
            self.assertEqual(GEMINI_HOST, host)
            self.assertEqual(443, port)
            self.assertEqual(socket.SOCK_STREAM, type)
            return [
                (socket.AF_INET, socket.SOCK_STREAM, 6, "", ("127.0.0.1", 443)),
                (socket.AF_INET, socket.SOCK_STREAM, 6, "", ("10.0.0.4", 443)),
                (socket.AF_INET, socket.SOCK_STREAM, 6, "", ("169.254.1.2", 443)),
            ]

        def socket_factory(*_args):
            created.append(object())
            raise AssertionError("private DNS results must be rejected before socket creation")

        with self.assertRaises(OSError):
            connect_to_gemini(resolver=resolver, socket_factory=socket_factory)
        self.assertEqual([], created)

    def test_ai_net_001_public_dns_result_is_pinned_for_connect(self) -> None:
        addresses = [
            (socket.AF_INET, socket.SOCK_STREAM, 6, "", ("10.0.0.4", 443)),
            (socket.AF_INET, socket.SOCK_STREAM, 6, "", ("8.8.8.8", 443)),
        ]

        class FakeSocket:
            def __init__(self, *_args) -> None:
                self.timeout = None
                self.connected_to = None
                self.closed = False

            def settimeout(self, value: float) -> None:
                self.timeout = value

            def connect(self, address: tuple[str, int]) -> None:
                self.connected_to = address

            def close(self) -> None:
                self.closed = True

        created: list[FakeSocket] = []

        def socket_factory(*args) -> FakeSocket:
            result = FakeSocket(*args)
            created.append(result)
            return result

        upstream = connect_to_gemini(
            resolver=lambda *_args, **_kwargs: addresses,
            socket_factory=socket_factory,
            timeout=2.0,
        )

        self.assertIs(created[0], upstream)
        self.assertEqual(("8.8.8.8", 443), upstream.connected_to)
        self.assertEqual(2.0, upstream.timeout)
        self.assertEqual(1, len(created))

    def test_ai_net_001_transport_ignores_no_proxy_and_forces_internal_route(self) -> None:
        request = _Request()
        handler = _RequiredGeminiProxyHandler(GEMINI_EGRESS_PROXY_URL)
        with patch.dict(os.environ, {"NO_PROXY": GEMINI_API_HOST, "no_proxy": GEMINI_API_HOST}):
            handler.proxy_open(request, GEMINI_EGRESS_PROXY_URL, "https")

        self.assertEqual(("gemini-egress-proxy:3128", "http"), request.proxy)

    def test_ai_net_001_rejects_proxy_override_and_non_gemini_url(self) -> None:
        with self.assertRaises(GeminiConfigurationError):
            _RequiredGeminiProxyHandler("http://attacker.example:3128")
        with self.assertRaises(GeminiConfigurationError):
            UrllibTransport("http://attacker.example:3128")
        with self.assertRaises(URLError):
            UrllibTransport().post("https://attacker.example/v1/interactions", {}, b"{}", 1.0)

    def test_ai_net_001_environment_rejects_proxy_override(self) -> None:
        with self.assertRaises(GeminiConfigurationError):
            GeminiConfig.from_environment(
                {
                    "GEMINI_API_KEY": "synthetic-key",
                    "GEMINI_MODEL": "gemini-test-model",
                    "GEMINI_DATA_HANDLING_APPROVED": "true",
                    "GEMINI_EGRESS_PROXY_URL": "http://attacker.example:3128",
                }
            )


if __name__ == "__main__":
    unittest.main()
