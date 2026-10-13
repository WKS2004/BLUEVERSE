"""Restricted outbound CONNECT proxy for the private Safety Operations agent."""

from __future__ import annotations

import ipaddress
import select
import socket
import socketserver
import threading
import time


GEMINI_HOST = "generativelanguage.googleapis.com"
GEMINI_PORT = 443
LISTEN_HOST = "0.0.0.0"
LISTEN_PORT = 3128
MAX_HEADER_BYTES = 8 * 1024
MAX_CONCURRENT_CLIENTS = 32
CLIENT_HEADER_TIMEOUT_SECONDS = 5
UPSTREAM_CONNECT_TIMEOUT_SECONDS = 5
TUNNEL_IDLE_TIMEOUT_SECONDS = 15
MAX_TUNNEL_LIFETIME_SECONDS = 120
MAX_TUNNEL_BYTES = 32 * 1024 * 1024
_ALLOWED_CONNECT_HEADERS = frozenset({"host", "connection", "proxy-connection", "user-agent", "accept-encoding"})


class ProxyRequestError(ValueError):
    def __init__(self, status: int) -> None:
        super().__init__("CONNECT request rejected")
        self.status = status


def parse_connect_request(header: bytes) -> tuple[str, int]:
    """Validate a single host-only CONNECT request without accepting credentials."""
    if len(header) > MAX_HEADER_BYTES:
        raise ProxyRequestError(431)
    if not header.endswith(b"\r\n\r\n"):
        raise ProxyRequestError(400)

    try:
        lines = header[:-4].decode("iso-8859-1").split("\r\n")
    except UnicodeDecodeError:
        raise ProxyRequestError(400) from None
    if not lines or not lines[0]:
        raise ProxyRequestError(400)

    request_line = lines[0].split(" ")
    if len(request_line) != 3 or any(not part for part in request_line):
        raise ProxyRequestError(400)
    method, authority, http_version = request_line
    if method != "CONNECT":
        raise ProxyRequestError(405)
    if http_version not in ("HTTP/1.0", "HTTP/1.1"):
        raise ProxyRequestError(400)

    host_headers: list[str] = []
    seen_headers: set[str] = set()
    for line in lines[1:]:
        if not line or line[0] in " \t" or ":" not in line:
            raise ProxyRequestError(400)
        name, value = line.split(":", 1)
        normalized_name = name.casefold()
        value = value.strip()
        if not name or any(char.isspace() or ord(char) < 33 or ord(char) > 126 for char in name):
            raise ProxyRequestError(400)
        if normalized_name not in _ALLOWED_CONNECT_HEADERS or normalized_name in seen_headers:
            raise ProxyRequestError(400)
        if any(ord(char) < 32 and char != "\t" for char in value):
            raise ProxyRequestError(400)
        seen_headers.add(normalized_name)
        if normalized_name == "host":
            host_headers.append(value)

    expected_authority = f"{GEMINI_HOST}:{GEMINI_PORT}"
    if authority.casefold() != expected_authority or len(host_headers) != 1:
        raise ProxyRequestError(403)
    if host_headers[0].casefold() != expected_authority:
        raise ProxyRequestError(403)
    return GEMINI_HOST, GEMINI_PORT


def _read_connect_request(client: socket.socket) -> tuple[str, int]:
    received = bytearray()
    while True:
        chunk = client.recv(min(2048, MAX_HEADER_BYTES + 1 - len(received)))
        if not chunk:
            raise ProxyRequestError(400)
        received.extend(chunk)
        header_end = received.find(b"\r\n\r\n")
        if header_end >= 0:
            if header_end + 4 != len(received):
                # A client must wait for 200 Connection Established before sending TLS.
                raise ProxyRequestError(400)
            return parse_connect_request(bytes(received))
        if len(received) > MAX_HEADER_BYTES:
            raise ProxyRequestError(431)


def connect_to_gemini(
    *,
    resolver=socket.getaddrinfo,
    socket_factory=socket.socket,
    timeout: float = UPSTREAM_CONNECT_TIMEOUT_SECONDS,
) -> socket.socket:
    """Connect to a public DNS result for the fixed provider, pinning the resolved IP."""
    try:
        candidates = resolver(GEMINI_HOST, GEMINI_PORT, type=socket.SOCK_STREAM)
    except OSError:
        raise OSError("Gemini provider resolution failed") from None

    for family, socktype, protocol, _canonical_name, address in candidates:
        ip_text = address[0]
        if "%" in ip_text:
            continue
        try:
            ip = ipaddress.ip_address(ip_text)
        except ValueError:
            continue
        if not ip.is_global:
            continue

        upstream = socket_factory(family, socktype, protocol)
        try:
            upstream.settimeout(timeout)
            upstream.connect(address)
            return upstream
        except OSError:
            upstream.close()

    raise OSError("Gemini provider has no reachable public address") from None


def relay_tunnel(client: socket.socket, upstream: socket.socket) -> None:
    """Relay bounded TLS bytes; the proxy never reads or logs decrypted model data."""
    started = time.monotonic()
    transferred = 0
    endpoints = {client: upstream, upstream: client}

    while True:
        lifetime_left = MAX_TUNNEL_LIFETIME_SECONDS - (time.monotonic() - started)
        if lifetime_left <= 0:
            return
        readable, _, _ = select.select(
            list(endpoints),
            [],
            [],
            min(TUNNEL_IDLE_TIMEOUT_SECONDS, lifetime_left),
        )
        if not readable:
            return
        for source in readable:
            try:
                payload = source.recv(64 * 1024)
                if not payload:
                    return
                transferred += len(payload)
                if transferred > MAX_TUNNEL_BYTES:
                    return
                endpoints[source].sendall(payload)
            except OSError:
                return


def _send_status(client: socket.socket, status: int) -> None:
    reasons = {
        400: "Bad Request",
        403: "Forbidden",
        405: "Method Not Allowed",
        408: "Request Timeout",
        431: "Request Header Fields Too Large",
        502: "Bad Gateway",
        503: "Service Unavailable",
    }
    reason = reasons.get(status, "Bad Request")
    try:
        client.sendall(
            f"HTTP/1.1 {status} {reason}\r\nConnection: close\r\nContent-Length: 0\r\n\r\n".encode("ascii")
        )
    except OSError:
        pass


class _GeminiConnectHandler(socketserver.BaseRequestHandler):
    def handle(self) -> None:
        client = self.request
        client.settimeout(CLIENT_HEADER_TIMEOUT_SECONDS)
        try:
            _read_connect_request(client)
        except ProxyRequestError as exc:
            _send_status(client, exc.status)
            return
        except socket.timeout:
            _send_status(client, 408)
            return
        except OSError:
            _send_status(client, 400)
            return

        try:
            upstream = connect_to_gemini()
        except OSError:
            _send_status(client, 502)
            return

        try:
            client.sendall(b"HTTP/1.1 200 Connection Established\r\n\r\n")
            client.settimeout(TUNNEL_IDLE_TIMEOUT_SECONDS)
            upstream.settimeout(TUNNEL_IDLE_TIMEOUT_SECONDS)
            relay_tunnel(client, upstream)
        except OSError:
            return
        finally:
            upstream.close()


class _BoundedThreadingTCPServer(socketserver.ThreadingMixIn, socketserver.TCPServer):
    allow_reuse_address = True
    daemon_threads = True
    request_queue_size = 64

    def __init__(self, address: tuple[str, int], handler: type[socketserver.BaseRequestHandler]) -> None:
        self._client_slots = threading.BoundedSemaphore(MAX_CONCURRENT_CLIENTS)
        super().__init__(address, handler)

    def process_request(self, request: socket.socket, client_address: tuple[str, int]) -> None:
        if not self._client_slots.acquire(blocking=False):
            _send_status(request, 503)
            request.close()
            return
        try:
            super().process_request(request, client_address)
        except BaseException:
            self._client_slots.release()
            raise

    def process_request_thread(self, request: socket.socket, client_address: tuple[str, int]) -> None:
        try:
            super().process_request_thread(request, client_address)
        finally:
            self._client_slots.release()

    def handle_error(self, request: socket.socket, client_address: tuple[str, int]) -> None:
        # Do not log request metadata, tunnel bytes, or exception details.
        return


def main() -> None:
    with _BoundedThreadingTCPServer((LISTEN_HOST, LISTEN_PORT), _GeminiConnectHandler) as server:
        server.serve_forever(poll_interval=0.5)


if __name__ == "__main__":
    main()
