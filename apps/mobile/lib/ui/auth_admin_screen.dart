import 'dart:async';

import 'package:flutter/material.dart';

import 'auth_view_model.dart';
import 'feedback/loading_screen_controller.dart';
import 'account_screens.dart' show AuthAccountSwitcher, AuthAdminNavigationMenu;

enum AuthAdminSection { permissions, roles, users }

class AuthAdminLandingScreen extends StatelessWidget {
  const AuthAdminLandingScreen({required this.viewModel, super.key});

  final AuthViewModel viewModel;

  @override
  Widget build(BuildContext context) {
    final permissions = viewModel.user?.permissions.toSet() ?? const <String>{};
    final section = permissions.contains('auth.permission.read')
        ? AuthAdminSection.permissions
        : permissions.contains('auth.role.read')
        ? AuthAdminSection.roles
        : AuthAdminSection.users;
    return AuthAdminScreen(viewModel: viewModel, section: section);
  }
}

class AuthAdminScreen extends StatefulWidget {
  const AuthAdminScreen({
    required this.viewModel,
    required this.section,
    super.key,
  });

  final AuthViewModel viewModel;
  final AuthAdminSection section;

  @override
  State<AuthAdminScreen> createState() => _AuthAdminScreenState();
}

class _AuthAdminScreenState extends State<AuthAdminScreen> {
  List<Map<String, dynamic>> _permissions = const [];
  List<Map<String, dynamic>> _roles = const [];
  List<Map<String, dynamic>> _users = const [];
  final Map<String, Set<String>> _roleGrants = {};
  final Map<String, Set<String>> _userRoles = {};
  bool _loading = true;
  bool _busy = false;
  String? _error;
  String? _notice;

  String get _title => switch (widget.section) {
    AuthAdminSection.permissions => 'Permissions',
    AuthAdminSection.roles => 'Roles',
    AuthAdminSection.users => 'User accounts',
  };

  String get _requiredReadPermission => switch (widget.section) {
    AuthAdminSection.permissions => 'auth.permission.read',
    AuthAdminSection.roles => 'auth.role.read',
    AuthAdminSection.users => 'auth.user.read',
  };

  Set<String> get _grants => widget.viewModel.user?.permissions.toSet() ?? {};

  bool _can(List<String> requirements) {
    const legacy = {
      'auth.user.create': 'auth.user.manage',
      'auth.user.update': 'auth.user.manage',
      'auth.user.delete': 'auth.user.manage',
      'auth.role.create': 'auth.role.manage',
      'auth.role.update': 'auth.role.manage',
      'auth.role.delete': 'auth.role.manage',
    };
    return requirements.every(
      (code) =>
          _grants.contains(code) ||
          (legacy.containsKey(code) && _grants.contains(legacy[code])),
    );
  }

  @override
  void initState() {
    super.initState();
    if (!_can([_requiredReadPermission])) {
      _loading = false;
      return;
    }
    unawaited(_load());
  }

  Future<void> _load() async {
    final finishLoading = blueverseLoadingScreenController.begin();
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      switch (widget.section) {
        case AuthAdminSection.permissions:
          _permissions = await widget.viewModel.adminPermissions();
        case AuthAdminSection.roles:
          _roles = await widget.viewModel.adminRoles();
          if (_can([
            'auth.role.read',
            'auth.role.update',
            'auth.permission.read',
          ])) {
            _permissions = await widget.viewModel.adminPermissions();
          }
          for (final role in _roles) {
            _roleGrants[role['id'] as String] =
                (role['permissions'] as List<dynamic>).cast<String>().toSet();
          }
        case AuthAdminSection.users:
          _users = await widget.viewModel.adminUsers();
          if (_can(['auth.role.read'])) {
            _roles = await widget.viewModel.adminRoles();
          }
          for (final user in _users) {
            _userRoles[user['id'] as String] = (user['roles'] as List<dynamic>)
                .cast<String>()
                .toSet();
          }
      }
    } on Object catch (error) {
      _error = error.toString();
    } finally {
      if (mounted) setState(() => _loading = false);
      finishLoading();
    }
  }

  Future<void> _perform(Future<void> Function() action, String message) async {
    final finishLoading = blueverseLoadingScreenController.begin();
    setState(() {
      _busy = true;
      _error = null;
      _notice = null;
    });
    try {
      await action();
      if (mounted) setState(() => _notice = message);
      await _load();
      if (mounted) setState(() => _notice = message);
    } on Object catch (error) {
      if (mounted) setState(() => _error = error.toString());
    } finally {
      if (mounted) setState(() => _busy = false);
      finishLoading();
    }
  }

  @override
  Widget build(BuildContext context) {
    final user = widget.viewModel.user;
    final canRead = user != null && _can([_requiredReadPermission]);
    return Scaffold(
      appBar: AppBar(
        title: Text(_title),
        actions: [
          AuthAccountSwitcher(viewModel: widget.viewModel),
          AuthAdminNavigationMenu(viewModel: widget.viewModel),
        ],
      ),
      body: user == null || !canRead
          ? const Center(
              child: Text(
                'Your current account does not have access to this page.',
              ),
            )
          : SafeArea(
              child: _loading
                  ? const SizedBox.shrink()
                  : RefreshIndicator(
                      onRefresh: _load,
                      child: ListView(
                        padding: const EdgeInsets.fromLTRB(16, 16, 16, 28),
                        children: [
                          if (_error != null)
                            _MessageCard(text: _error!, error: true),
                          if (_notice != null) _MessageCard(text: _notice!),
                          if (widget.section == AuthAdminSection.permissions)
                            _buildPermissions(),
                          if (widget.section == AuthAdminSection.roles)
                            _buildRoles(),
                          if (widget.section == AuthAdminSection.users)
                            _buildUsers(),
                        ],
                      ),
                    ),
            ),
    );
  }

  Widget _buildPermissions() => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      const _IntroCard(
        title: 'Permission catalogue',
        detail: 'These application-defined access codes can be granted to or removed from a non-system role on the Roles page.',
      ),
      const SizedBox(height: 12),
      ..._permissions.map(
        (permission) => Card(
          child: ListTile(
            leading: const CircleAvatar(child: Icon(Icons.key_outlined)),
            title: Text(
              permission['code'] as String,
              style: const TextStyle(fontWeight: FontWeight.w700),
            ),
            subtitle: Text(permission['description'] as String),
          ),
        ),
      ),
    ],
  );

  Widget _buildRoles() {
    final canCreate = _can(['auth.role.read', 'auth.role.create']);
    final canUpdate = _can(['auth.role.read', 'auth.role.update']);
    final canDelete = _can(['auth.role.read', 'auth.role.delete']);
    final canAssign = _can([
      'auth.role.read',
      'auth.role.update',
      'auth.permission.read',
    ]);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _IntroCard(
          title: 'Role access',
          detail: 'Each change is checked against your current account grants. System roles stay managed by BLUEVERSE.',
        ),
        if (canCreate) ...[
          const SizedBox(height: 12),
          FilledButton.icon(
            onPressed: _busy ? null : _createRole,
            icon: const Icon(Icons.add),
            label: const Text('Create role'),
          ),
        ],
        if (!canAssign) ...[
          const SizedBox(height: 10),
          const _MessageCard(
            text: 'Changing role permission assignments requires role read and update access plus permission catalogue read access.',
          ),
        ],
        const SizedBox(height: 8),
        ..._roles.map((role) {
          final id = role['id'] as String;
          final system = role['isSystemRole'] as bool;
          final codes = _roleGrants.putIfAbsent(
            id,
            () => (role['permissions'] as List<dynamic>).cast<String>().toSet(),
          );
          return Card(
            clipBehavior: Clip.antiAlias,
            child: ExpansionTile(
              leading: Icon(
                system ? Icons.shield_outlined : Icons.groups_outlined,
              ),
              title: Text(
                role['name'] as String,
                style: const TextStyle(fontWeight: FontWeight.w700),
              ),
              subtitle: Text(
                system
                    ? 'System role · managed by BLUEVERSE'
                    : (role['description'] as String),
              ),
              childrenPadding: const EdgeInsets.fromLTRB(16, 0, 16, 18),
              children: [
                Wrap(
                  spacing: 8,
                  children: [
                    if (canUpdate && !system)
                      OutlinedButton.icon(
                        onPressed: _busy ? null : () => _editRole(role),
                        icon: const Icon(Icons.edit_outlined),
                        label: const Text('Edit details'),
                      ),
                    if (canDelete && !system)
                      TextButton.icon(
                        onPressed: _busy ? null : () => _deleteRole(role),
                        icon: const Icon(Icons.delete_outline),
                        label: const Text('Delete role'),
                      ),
                  ],
                ),
                if (canAssign && !system) ...[
                  const Align(
                    alignment: Alignment.centerLeft,
                    child: Padding(
                      padding: EdgeInsets.only(top: 12, bottom: 4),
                      child: Text(
                        'Permissions',
                        style: TextStyle(fontWeight: FontWeight.w700),
                      ),
                    ),
                  ),
                  ..._permissions.map((permission) {
                    final code = permission['code'] as String;
                    return CheckboxListTile(
                      dense: true,
                      contentPadding: EdgeInsets.zero,
                      value: codes.contains(code),
                      title: Text(
                        code,
                        style: const TextStyle(
                          fontSize: 13,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                      subtitle: Text(permission['description'] as String),
                      onChanged: _busy
                          ? null
                          : (checked) => setState(() {
                              checked == true
                                  ? codes.add(code)
                                  : codes.remove(code);
                            }),
                    );
                  }),
                  Align(
                    alignment: Alignment.centerRight,
                    child: FilledButton.tonalIcon(
                      onPressed: _busy
                          ? null
                          : () => _saveRolePermissions(id, codes),
                      icon: const Icon(Icons.save_outlined),
                      label: const Text('Save permissions'),
                    ),
                  ),
                ],
                if (system)
                  const Padding(
                    padding: EdgeInsets.only(top: 12),
                    child: Text(
                      'System role names, permissions and deletion are deployment-controlled.',
                    ),
                  ),
              ],
            ),
          );
        }),
      ],
    );
  }

  Widget _buildUsers() {
    final canCreate = _can(['auth.user.read', 'auth.user.create']);
    final canUpdate = _can(['auth.user.read', 'auth.user.update']);
    final canDelete = _can(['auth.user.read', 'auth.user.delete']);
    final canAssign = _can([
      'auth.user.read',
      'auth.user.update',
      'auth.role.read',
    ]);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const _IntroCard(
          title: 'User accounts',
          detail: 'Review account details and assigned roles. Account and role changes are verified by the Auth service.',
        ),
        if (canCreate) ...[
          const SizedBox(height: 12),
          FilledButton.icon(
            onPressed: _busy ? null : _createUser,
            icon: const Icon(Icons.person_add_alt_1_outlined),
            label: const Text('Create account'),
          ),
        ],
        if (!canAssign) ...[
          const SizedBox(height: 10),
          const _MessageCard(
            text: 'Changing user role assignments requires user read and update access plus role read access.',
          ),
        ],
        const SizedBox(height: 8),
        ..._users.map((account) {
          final id = account['id'] as String;
          final roles = (account['roles'] as List<dynamic>).cast<String>();
          final selected = _userRoles.putIfAbsent(id, () => roles.toSet());
          final protected = roles.any(
            (name) => _roles.any(
              (role) => role['name'] == name && role['isSystemRole'] == true,
            ),
          );
          final self = id == widget.viewModel.user?.id;
          return Card(
            clipBehavior: Clip.antiAlias,
            child: ExpansionTile(
              leading: const Icon(Icons.account_circle_outlined),
              title: Text(
                account['fullName'] as String,
                style: const TextStyle(fontWeight: FontWeight.w700),
              ),
              subtitle: Text(
                '${account['email']} · ${account['isActive'] == true ? 'Active' : 'Inactive'}',
              ),
              childrenPadding: const EdgeInsets.fromLTRB(16, 0, 16, 18),
              children: [
                Wrap(
                  spacing: 8,
                  children: [
                    if (canUpdate && !self)
                      OutlinedButton.icon(
                        onPressed: _busy ? null : () => _editUser(account),
                        icon: const Icon(Icons.edit_outlined),
                        label: const Text('Edit account'),
                      ),
                    if (canDelete && !self)
                      TextButton.icon(
                        onPressed: _busy || protected
                            ? null
                            : () => _deleteUser(account),
                        icon: const Icon(Icons.delete_outline),
                        label: Text(
                          protected ? 'System account' : 'Delete account',
                        ),
                      ),
                  ],
                ),
                if (canAssign && !self && !protected) ...[
                  const Align(
                    alignment: Alignment.centerLeft,
                    child: Padding(
                      padding: EdgeInsets.only(top: 12, bottom: 4),
                      child: Text(
                        'Assigned roles',
                        style: TextStyle(fontWeight: FontWeight.w700),
                      ),
                    ),
                  ),
                  ..._roles.map((role) {
                    final roleName = role['name'] as String;
                    final system = role['isSystemRole'] == true;
                    final canManageSystem = _can(['auth.role.system.manage']);
                    return CheckboxListTile(
                      dense: true,
                      contentPadding: EdgeInsets.zero,
                      value: selected.contains(roleName),
                      title: Text('$roleName${system ? ' · system' : ''}'),
                      subtitle: Text(role['description'] as String),
                      onChanged: _busy || (system && !canManageSystem)
                          ? null
                          : (checked) => setState(() {
                              checked == true
                                  ? selected.add(roleName)
                                  : selected.remove(roleName);
                            }),
                    );
                  }),
                  Align(
                    alignment: Alignment.centerRight,
                    child: FilledButton.tonalIcon(
                      onPressed: _busy
                          ? null
                          : () => _saveUserRoles(id, selected),
                      icon: const Icon(Icons.save_outlined),
                      label: const Text('Save roles'),
                    ),
                  ),
                ],
                if (protected)
                  const Padding(
                    padding: EdgeInsets.only(top: 12),
                    child: Text(
                      'This account has a system role. System-role removal and account deletion are protected.',
                    ),
                  ),
                if (roles.isEmpty && !canAssign)
                  const Text('No roles assigned.'),
              ],
            ),
          );
        }),
      ],
    );
  }

  Future<void> _createRole() async {
    final input = await _editDialog(title: 'Create role');
    if (input == null) return;
    await _perform(() async {
      await widget.viewModel.adminCreateRole(
        name: input['name']!,
        description: input['description']!,
      );
    }, 'Role created with no permissions assigned.');
  }

  Future<void> _editRole(Map<String, dynamic> role) async {
    final input = await _editDialog(
      title: 'Edit role',
      name: role['name'] as String,
      description: role['description'] as String,
    );
    if (input == null) return;
    await _perform(() async {
      await widget.viewModel.adminUpdateRole(
        id: role['id'] as String,
        name: input['name']!,
        description: input['description']!,
      );
    }, 'Role details saved.');
  }

  Future<Map<String, String>?> _editDialog({
    required String title,
    String name = '',
    String description = '',
  }) async {
    final formKey = GlobalKey<FormState>();
    final nameController = TextEditingController(text: name);
    final descriptionController = TextEditingController(text: description);
    final result = await _showAdminDialog<Map<String, String>>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(title),
        content: Form(
          key: formKey,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextFormField(
                controller: nameController,
                maxLength: 50,
                decoration: const InputDecoration(labelText: 'Name'),
                validator: (value) =>
                    (value ?? '').trim().isEmpty ? 'Enter a role name.' : null,
              ),
              TextFormField(
                controller: descriptionController,
                maxLength: 200,
                decoration: const InputDecoration(labelText: 'Description'),
              ),
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () {
              final value = nameController.text.trim();
              if (!(formKey.currentState?.validate() ?? false)) return;
              Navigator.pop(context, {
                'name': value,
                'description': descriptionController.text.trim(),
              });
            },
            child: const Text('Save'),
          ),
        ],
      ),
    );
    nameController.dispose();
    descriptionController.dispose();
    return result;
  }

  Future<void> _saveRolePermissions(String id, Set<String> codes) =>
      _perform(() async {
        await widget.viewModel.adminSetRolePermissions(
          id: id,
          permissionCodes: codes.toList(),
        );
      }, 'Role permissions saved.');

  Future<void> _deleteRole(Map<String, dynamic> role) async {
    final name = role['name'] as String;
    final confirmed = await _confirm(
      'Delete role?',
      'Delete “$name”? Assigned users will lose permissions from this role.',
    );
    if (!confirmed) return;
    await _perform(
      () => widget.viewModel.adminDeleteRole(role['id'] as String),
      'Role deleted.',
    );
  }

  Future<void> _createUser() async {
    final formKey = GlobalKey<FormState>();
    final name = TextEditingController();
    final email = TextEditingController();
    final password = TextEditingController();
    final selected = <String>{};
    final input = await _showAdminDialog<Map<String, Object?>>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setDialogState) => AlertDialog(
          title: const Text('Create account'),
          content: Form(
            key: formKey,
            child: SizedBox(
              width: 420,
              child: SingleChildScrollView(
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    TextFormField(
                      controller: name,
                      maxLength: 100,
                      decoration: const InputDecoration(labelText: 'Full name'),
                      validator: (value) => (value ?? '').trim().isEmpty
                          ? 'Enter a full name.'
                          : null,
                    ),
                    TextFormField(
                      controller: email,
                      keyboardType: TextInputType.emailAddress,
                      decoration: const InputDecoration(labelText: 'Email'),
                      validator: (value) {
                        final address = value?.trim() ?? '';
                        if (address.isEmpty) return 'Enter an email address.';
                        if (!RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$')
                            .hasMatch(address)) {
                          return 'Enter a valid email address.';
                        }
                        return null;
                      },
                    ),
                    TextFormField(
                      controller: password,
                      obscureText: true,
                      decoration: const InputDecoration(
                        labelText: 'Temporary password (8+ characters)',
                      ),
                      validator: (value) => (value ?? '').length < 8
                          ? 'Use at least 8 characters.'
                          : null,
                    ),
                    if (_can(['auth.role.read'])) ...[
                      const Align(
                        alignment: Alignment.centerLeft,
                        child: Padding(
                          padding: EdgeInsets.only(top: 12),
                          child: Text('Initial roles (optional)'),
                        ),
                      ),
                      ..._roles
                          .where(
                            (role) =>
                                role['isSystemRole'] != true ||
                                _can(['auth.role.system.manage']),
                          )
                          .map((role) {
                            final roleName = role['name'] as String;
                            return CheckboxListTile(
                              value: selected.contains(roleName),
                              title: Text(roleName),
                              onChanged: (checked) => setDialogState(
                                () => checked == true
                                    ? selected.add(roleName)
                                    : selected.remove(roleName),
                              ),
                            );
                          }),
                    ],
                  ],
                ),
              ),
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context),
              child: const Text('Cancel'),
            ),
            FilledButton(
              onPressed: () {
                if (!(formKey.currentState?.validate() ?? false)) return;
                Navigator.pop(context, {
                  'fullName': name.text.trim(),
                  'email': email.text.trim(),
                  'password': password.text,
                  'roles': selected.toList(),
                });
              },
              child: const Text('Create'),
            ),
          ],
        ),
      ),
    );
    name.dispose();
    email.dispose();
    password.dispose();
    if (input == null) return;
    await _perform(() async {
      await widget.viewModel.adminCreateUser(
        email: input['email']! as String,
        password: input['password']! as String,
        fullName: input['fullName']! as String,
        roleNames: input['roles']! as List<String>,
      );
    }, 'Account created.');
  }

  Future<void> _editUser(Map<String, dynamic> account) async {
    final formKey = GlobalKey<FormState>();
    final name = TextEditingController(text: account['fullName'] as String);
    final email = TextEditingController(text: account['email'] as String);
    final password = TextEditingController();
    var active = account['isActive'] as bool;
    final input = await _showAdminDialog<Map<String, Object?>>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setDialogState) => AlertDialog(
          title: const Text('Edit account'),
          content: Form(
            key: formKey,
            child: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  TextFormField(
                    controller: name,
                    maxLength: 100,
                    decoration: const InputDecoration(labelText: 'Full name'),
                    validator: (value) => (value ?? '').trim().isEmpty
                        ? 'Enter a full name.'
                        : null,
                  ),
                  TextFormField(
                    controller: email,
                    keyboardType: TextInputType.emailAddress,
                    decoration: const InputDecoration(labelText: 'Email'),
                    validator: (value) {
                      final address = value?.trim() ?? '';
                      if (address.isEmpty) return 'Enter an email address.';
                      if (!RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$')
                          .hasMatch(address)) {
                        return 'Enter a valid email address.';
                      }
                      return null;
                    },
                  ),
                  TextFormField(
                    controller: password,
                    obscureText: true,
                    decoration: const InputDecoration(
                      labelText: 'New password (optional)',
                    ),
                    validator: (value) =>
                        value != null && value.isNotEmpty && value.length < 8
                        ? 'Use at least 8 characters.'
                        : null,
                  ),
                  SwitchListTile(
                    value: active,
                    title: const Text('Account is active'),
                    onChanged: (value) => setDialogState(() => active = value),
                  ),
                ],
              ),
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context),
              child: const Text('Cancel'),
            ),
            FilledButton(
              onPressed: () {
                if (!(formKey.currentState?.validate() ?? false)) return;
                Navigator.pop(context, {
                  'fullName': name.text.trim(),
                  'email': email.text.trim(),
                  'password': password.text,
                  'active': active,
                });
              },
              child: const Text('Save'),
            ),
          ],
        ),
      ),
    );
    name.dispose();
    email.dispose();
    password.dispose();
    if (input == null) return;
    await _perform(() async {
      await widget.viewModel.adminUpdateUser(
        id: account['id'] as String,
        email: input['email']! as String,
        fullName: input['fullName']! as String,
        isActive: input['active']! as bool,
        newPassword: input['password']! as String,
      );
    }, 'Account updated.');
  }

  Future<void> _saveUserRoles(String id, Set<String> roles) =>
      _perform(() async {
        await widget.viewModel.adminSetUserRoles(
          id: id,
          roleNames: roles.toList(),
        );
      }, 'Role assignments saved.');

  Future<void> _deleteUser(Map<String, dynamic> account) async {
    final fullName = account['fullName'] as String;
    final confirmed = await _confirm(
      'Delete account?',
      'Permanently delete the account for $fullName?',
    );
    if (!confirmed) return;
    await _perform(
      () => widget.viewModel.adminDeleteUser(account['id'] as String),
      'Account deleted.',
    );
  }

  Future<bool> _confirm(String title, String detail) async =>
      await _showAdminDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          title: Text(title),
          content: Text(detail),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(context, false),
              child: const Text('Cancel'),
            ),
            FilledButton(
              onPressed: () => Navigator.pop(context, true),
              child: const Text('Confirm'),
            ),
          ],
        ),
      ) ??
      false;
}

Future<T?> _showAdminDialog<T>({
  required BuildContext context,
  required WidgetBuilder builder,
}) async {
  final navigator = Navigator.of(context, rootNavigator: true);
  final route = DialogRoute<T>(
    context: context,
    builder: builder,
    themes: InheritedTheme.capture(from: context, to: navigator.context),
  );
  final result = await navigator.push<T>(route);
  await route.completed;
  return result;
}

class _IntroCard extends StatelessWidget {
  const _IntroCard({required this.title, required this.detail});
  final String title;
  final String detail;
  @override
  Widget build(BuildContext context) => Card(
    child: Padding(
      padding: const EdgeInsets.all(18),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            title,
            style: Theme.of(context).textTheme.titleLarge
                ?.copyWith(fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: 8),
          Text(detail, style: const TextStyle(height: 1.45)),
        ],
      ),
    ),
  );
}

class _MessageCard extends StatelessWidget {
  const _MessageCard({required this.text, this.error = false});
  final String text;
  final bool error;
  @override
  Widget build(BuildContext context) => Card(
    color: error ? const Color(0xFFFFF0EF) : const Color(0xFFE5EFF0),
    child: Padding(
      padding: const EdgeInsets.all(14),
      child: Text(
        text,
        style: TextStyle(
          color: error
              ? Theme.of(context).colorScheme.error
              : const Color(0xFF205C79),
        ),
      ),
    ),
  );
}
