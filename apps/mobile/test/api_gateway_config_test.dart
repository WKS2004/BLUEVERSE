import 'package:flutter_test/flutter_test.dart';

import 'package:mobile/data/services/api_gateway_config.dart';

void main() {
  group('MOB-NETWORK-001', () {
    test('builds public auth endpoints on port 80', () {
      const config = ApiGatewayConfig();
      final endpoint = config.endpoint(
        Uri.parse('http://172.28.20.242:80'),
        '/api/auth/login',
      );

      expect(endpoint.toString(), 'http://172.28.20.242/api/auth/login');
      expect(endpoint.port, 80);
    });

    test('rejects non-public endpoint paths', () {
      const config = ApiGatewayConfig();

      expect(
        () => config.endpoint(Uri.parse('http://localhost:80'), '/login'),
        throwsArgumentError,
      );
    });

    test('reports a stable port-80 connectivity message', () {
      const config = ApiGatewayConfig();
      final message = config.connectionFailureMessage([
        Uri.parse('http://10.0.2.2:80'),
        Uri.parse('http://172.28.20.242:80'),
      ]);

      expect(message, contains('port 80'));
      expect(message, contains('10.0.2.2:80'));
      expect(message, contains('172.28.20.242:80'));
      expect(message, isNot(contains('50872')));
    });
  });
}
