import 'package:flutter_test/flutter_test.dart';
import 'package:mobile/data/services/api_gateway_config.dart';

void main() {
  // marine-conditions / marine-condition-history / marine-safety-profile-management:
  // Android Emulator host access and the documented USB reverse tunnel.
  test('M2-MOB-NET-001 uses portable Android gateway defaults', () {
    expect(ApiGatewayConfig.androidEmulatorHost, '10.0.2.2');
    expect(ApiGatewayConfig.androidUsbReverseHost, '127.0.0.1');
    expect(ApiGatewayConfig.gatewayPort, 80);
    const config = ApiGatewayConfig();
    for (final host in [
      ApiGatewayConfig.androidEmulatorHost,
      ApiGatewayConfig.androidUsbReverseHost,
    ]) {
      final endpoint = config.endpoint(
        Uri.parse('http://$host:80'),
        '/api/marine/current',
      );
      expect(endpoint.scheme, 'http');
      expect(endpoint.host, host);
      expect(endpoint.port, 80);
      expect(endpoint.path, '/api/marine/current');
    }
  });
}
