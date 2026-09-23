class AuthUser {
  const AuthUser({
    required this.id,
    required this.email,
    required this.fullName,
    required this.isActive,
    required this.createdAt,
    required this.roles,
    required this.permissions,
  });

  final String id;
  final String email;
  final String fullName;
  final bool isActive;
  final DateTime createdAt;
  final List<String> roles;
  final List<String> permissions;

  factory AuthUser.fromJson(Map<String, dynamic> json) {
    return AuthUser(
      id: _requiredString(json, 'id'),
      email: _requiredString(json, 'email'),
      fullName: _requiredString(json, 'fullName'),
      isActive: _requiredBool(json, 'isActive'),
      createdAt: DateTime.parse(_requiredString(json, 'createdAt')),
      roles: _stringList(json['roles']),
      permissions: _stringList(json['permissions']),
    );
  }
}

class AuthResponse {
  const AuthResponse({
    required this.token,
    required this.expiresAt,
    required this.deviceId,
    required this.deviceKey,
    required this.refreshToken,
    required this.sessionExpiresAt,
    required this.rememberMe,
    required this.user,
  });

  final String token;
  final DateTime expiresAt;
  final String deviceId;
  final String? deviceKey;
  final String refreshToken;
  final DateTime sessionExpiresAt;
  final bool rememberMe;
  final AuthUser user;

  factory AuthResponse.fromJson(Map<String, dynamic> json) {
    return AuthResponse(
      token: _requiredString(json, 'token'),
      expiresAt: DateTime.parse(_requiredString(json, 'expiresAt')),
      deviceId: _requiredString(json, 'deviceId'),
      deviceKey: json['deviceKey'] as String?,
      refreshToken: _requiredString(json, 'refreshToken'),
      sessionExpiresAt: DateTime.parse(
        _requiredString(json, 'sessionExpiresAt'),
      ),
      rememberMe: _requiredBool(json, 'rememberMe'),
      user: AuthUser.fromJson(_requiredMap(json, 'user')),
    );
  }
}

class AuthSession {
  const AuthSession({
    required this.id,
    required this.deviceId,
    required this.createdAt,
    required this.lastSeenAt,
    required this.expiresAt,
    required this.rememberMe,
    required this.isCurrent,
  });

  final String id;
  final String deviceId;
  final DateTime createdAt;
  final DateTime lastSeenAt;
  final DateTime expiresAt;
  final bool rememberMe;
  final bool isCurrent;

  factory AuthSession.fromJson(Map<String, dynamic> json) {
    return AuthSession(
      id: _requiredString(json, 'id'),
      deviceId: _requiredString(json, 'deviceId'),
      createdAt: DateTime.parse(_requiredString(json, 'createdAt')),
      lastSeenAt: DateTime.parse(_requiredString(json, 'lastSeenAt')),
      expiresAt: DateTime.parse(_requiredString(json, 'expiresAt')),
      rememberMe: _requiredBool(json, 'rememberMe'),
      isCurrent: _requiredBool(json, 'isCurrent'),
    );
  }
}

String _requiredString(Map<String, dynamic> json, String key) {
  final value = json[key];
  if (value is! String || value.isEmpty) {
    throw FormatException('Missing or invalid $key.');
  }
  return value;
}

bool _requiredBool(Map<String, dynamic> json, String key) {
  final value = json[key];
  if (value is! bool) {
    throw FormatException('Missing or invalid $key.');
  }
  return value;
}

Map<String, dynamic> _requiredMap(Map<String, dynamic> json, String key) {
  final value = json[key];
  if (value is! Map) {
    throw FormatException('Missing or invalid $key.');
  }
  return Map<String, dynamic>.from(value);
}

List<String> _stringList(Object? value) {
  if (value is! List) {
    return const [];
  }
  return value.whereType<String>().toList(growable: false);
}
