import 'dart:io';

/// Resolves the local public gateway without treating Android's localhost as
/// the development laptop. The public gateway is intentionally fixed to port
/// 80 for local device testing.
class ApiGatewayConfig {
  const ApiGatewayConfig();

  static const gatewayPort = 80;
  static const androidEmulatorHost = '10.247.90.188';
  static const androidUsbReverseHost = '127.0.0.1';

  // Flutter embeds this value at compile time. A physical device must receive
  // the laptop's current LAN address through --dart-define; it must not be
  // committed here because DHCP addresses are environment-specific.
  static const configuredBaseUrl = String.fromEnvironment(
    'BLUEVERSE_API_BASE_URL',
    defaultValue: '',
  );

  List<Uri> get baseUris {
    if (configuredBaseUrl.trim().isNotEmpty) {
      // An explicit value is authoritative. Do not append the emulator or
      // USB candidates, otherwise a stale fallback could hide a bad build
      // configuration and make the override appear to be ignored.
      return [_parseConfiguredBaseUrl(configuredBaseUrl)];
    }

    if (Platform.isAndroid) {
      // 10.0.2.2 reaches the host from the Android emulator. 127.0.0.1
      // reaches the host only when `adb reverse tcp:80 tcp:80` is active.
      // There is intentionally no checked-in laptop LAN address here.
      return [
        _uriForHost(androidEmulatorHost),
        _uriForHost(androidUsbReverseHost),
      ];
    }

    return [_uriForHost('localhost')];
  }

  Uri endpoint(Uri baseUri, String path) {
    if (!path.startsWith('/api/')) {
      throw ArgumentError.value(path, 'path', 'Must be a public /api/ path.');
    }

    return baseUri.replace(path: path);
  }

  String connectionFailureMessage(Iterable<Uri> attemptedUris) {
    final authorities = attemptedUris
        .map((uri) => '${uri.host}:$gatewayPort')
        .join(', ');
    return 'Cannot reach the BLUEVERSE gateway on port $gatewayPort '
        '(tried $authorities). Start Docker Compose, keep the device on the '
        'same network as the laptop, or pass '
        '--dart-define=BLUEVERSE_API_BASE_URL='
        'http://<laptop-lan-ip>:$gatewayPort to flutter run or flutter build.';
  }

  static Uri _parseConfiguredBaseUrl(String value) {
    final uri = Uri.parse(value.trim());
    if (uri.scheme != 'http' || uri.host.isEmpty || uri.port != gatewayPort) {
      throw FormatException(
        'BLUEVERSE_API_BASE_URL must be an http URL on port $gatewayPort.',
      );
    }
    if (uri.userInfo.isNotEmpty ||
        uri.query.isNotEmpty ||
        uri.fragment.isNotEmpty) {
      throw const FormatException(
        'BLUEVERSE_API_BASE_URL must not contain credentials, query data or a fragment.',
      );
    }
    return uri;
  }

  static Uri _uriForHost(String host) => Uri.parse('http://$host:$gatewayPort');
}
