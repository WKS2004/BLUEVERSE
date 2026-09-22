import 'package:flutter_test/flutter_test.dart';

import 'package:mobile/data/services/api_gateway_config.dart';

void main() {
  test('MOB-NETWORK-002 uses the Flutter build-time gateway override', () {
    const configured = String.fromEnvironment('BLUEVERSE_API_BASE_URL');
    final baseUris = const ApiGatewayConfig().baseUris;

    if (configured.trim().isEmpty) {
      expect(baseUris, isNotEmpty);
      return;
    }

    expect(baseUris, [Uri.parse(configured)]);
  });
}
