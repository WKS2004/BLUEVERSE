import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// The small credential boundary used by the Auth API client.
abstract interface class AuthCredentialStore {
  Future<String?> read(String key);
  Future<void> write(String key, String value);
  Future<void> delete(String key);
}

class SecureAuthCredentialStore implements AuthCredentialStore {
  const SecureAuthCredentialStore({
    this.storage = const FlutterSecureStorage(),
  });

  final FlutterSecureStorage storage;

  @override
  Future<String?> read(String key) => storage.read(key: key);

  @override
  Future<void> write(String key, String value) =>
      storage.write(key: key, value: value);

  @override
  Future<void> delete(String key) => storage.delete(key: key);
}
