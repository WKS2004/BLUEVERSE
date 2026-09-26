import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:mobile/data/repositories/auth_repository.dart';
import 'package:mobile/data/services/auth_api_service.dart';
import 'package:mobile/data/services/auth_credential_store.dart';
import 'package:mobile/main.dart';
import 'package:mobile/ui/auth_view_model.dart';

class MemoryCredentialStore implements AuthCredentialStore {
  final values = <String, String>{};

  @override
  Future<String?> read(String key) async => values[key];

  @override
  Future<void> write(String key, String value) async {
    values[key] = value;
  }

  @override
  Future<void> delete(String key) async {
    values.remove(key);
  }
}

void main() {
  testWidgets(
    'MOB-LAUNCH-001 signed-out launch shows onboarding after restore',
    (tester) async {
      final client = MockClient(
        (_) async =>
            http.Response('{"status":401,"detail":"No active account."}', 401),
      );
      addTearDown(client.close);
      final viewModel = AuthViewModel(
        repository: AuthRepository(
          apiService: AuthApiService(
            client: client,
            storage: MemoryCredentialStore(),
          ),
        ),
      );
      addTearDown(viewModel.dispose);

      await tester.pumpWidget(
        MaterialApp(home: MobileLaunchPage(viewModel: viewModel)),
      );
      expect(find.text('BLUEVERSE'), findsOneWidget);
      expect(find.text('Closer to the coast.'), findsNothing);

      await viewModel.restore();
      await tester.pumpAndSettle();

      expect(find.text('BLUEVERSE'), findsOneWidget);
      expect(find.text('Closer to the coast.'), findsOneWidget);
      expect(find.text('Next'), findsOneWidget);
      expect(find.text('Skip'), findsOneWidget);
      expect(find.text('I already have an account · Sign in'), findsNothing);
    },
  );
}
