import 'dart:async';

import 'package:flutter/material.dart';

import '../data/models/auth_models.dart';
import 'auth_view_model.dart';

const _coastInk = Color(0xFF18394C);
const _coastDeep = Color(0xFF205C79);
const _coastBlue = Color(0xFF347B9B);
const _coastTeal = Color(0xFF43858A);
const _coastSage = Color(0xFFE5EFF0);
const _coastLine = Color(0xFFD6E2E7);
const _maximumSessions = 5;

Future<String?> _requestPasswordVerification(
  BuildContext context, {
  required String title,
  required String message,
}) => showDialog<String>(
  context: context,
  builder: (_) => _PasswordVerificationDialog(title: title, message: message),
);

class _PasswordVerificationDialog extends StatefulWidget {
  const _PasswordVerificationDialog({
    required this.title,
    required this.message,
  });

  final String title;
  final String message;

  @override
  State<_PasswordVerificationDialog> createState() =>
      _PasswordVerificationDialogState();
}

class _PasswordVerificationDialogState
    extends State<_PasswordVerificationDialog> {
  final _passwordController = TextEditingController();

  @override
  void dispose() {
    _passwordController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: Text(widget.title),
    content: Column(
      mainAxisSize: MainAxisSize.min,
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(widget.message),
        const SizedBox(height: 14),
        TextField(
          controller: _passwordController,
          obscureText: true,
          autofocus: true,
          autofillHints: const [AutofillHints.password],
          textInputAction: TextInputAction.done,
          onSubmitted: (_) => Navigator.pop(context, _passwordController.text),
          decoration: const InputDecoration(labelText: 'Current password'),
        ),
      ],
    ),
    actions: [
      TextButton(
        onPressed: () => Navigator.pop(context),
        child: const Text('Cancel'),
      ),
      FilledButton(
        onPressed: () => Navigator.pop(context, _passwordController.text),
        child: const Text('Verify'),
      ),
    ],
  );
}

class AuthAccountSwitcher extends StatelessWidget {
  const AuthAccountSwitcher({required this.viewModel, super.key});

  final AuthViewModel viewModel;

  @override
  Widget build(BuildContext context) {
    return PopupMenuButton<String>(
      tooltip: 'Switch BLUEVERSE account',
      icon: const Icon(Icons.switch_account_outlined),
      onSelected: (value) {
        if (value == 'add-login') {
          Navigator.pushNamed(context, '/signin');
        } else if (value == 'add-register') {
          Navigator.pushNamed(context, '/signup');
        } else if (value.startsWith('switch:')) {
          final userId = value.substring('switch:'.length);
          if (userId != viewModel.user?.id) {
            viewModel.switchAccount(userId);
          }
        }
      },
      itemBuilder: (context) => [
        ...viewModel.accounts.map(
          (account) => PopupMenuItem<String>(
            value: 'switch:${account.id}',
            child: Row(
              children: [
                Icon(
                  account.id == viewModel.user?.id
                      ? Icons.check_circle
                      : Icons.account_circle_outlined,
                  color: _coastBlue,
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(account.fullName, overflow: TextOverflow.ellipsis),
                      Text(
                        account.email,
                        style: Theme.of(context).textTheme.bodySmall,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
        const PopupMenuDivider(),
        PopupMenuItem<String>(
          value: 'add-login',
          enabled: viewModel.accounts.length < 5,
          child: Text(
            viewModel.accounts.length < 5
                ? 'Sign in to another account'
                : 'Account limit reached (5)',
          ),
        ),
        PopupMenuItem<String>(
          value: 'add-register',
          enabled: viewModel.accounts.length < 5,
          child: const Text('Create another account'),
        ),
      ],
    );
  }
}

class AuthAdminNavigationMenu extends StatelessWidget {
  const AuthAdminNavigationMenu({required this.viewModel, super.key});

  final AuthViewModel viewModel;

  @override
  Widget build(BuildContext context) {
    final grants = viewModel.user?.permissions.toSet() ?? const <String>{};
    final entries = <(String, String, IconData)>[
      if (grants.contains('auth.permission.read'))
        ('/admin/permissions', 'Permissions', Icons.key_outlined),
      if (grants.contains('auth.role.read'))
        ('/admin/roles', 'Roles', Icons.groups_outlined),
      if (grants.contains('auth.user.read'))
        ('/admin/users', 'User accounts', Icons.manage_accounts_outlined),
    ];
    if (entries.isEmpty) return const SizedBox.shrink();
    return PopupMenuButton<String>(
      tooltip: 'Administration',
      icon: const Icon(Icons.admin_panel_settings_outlined),
      onSelected: (path) => Navigator.pushNamed(context, path),
      itemBuilder: (context) => entries
          .map(
            (entry) => PopupMenuItem<String>(
              value: entry.$1,
              child: Row(
                children: [
                  Icon(entry.$3),
                  const SizedBox(width: 10),
                  Text(entry.$2),
                ],
              ),
            ),
          )
          .toList(),
    );
  }
}

class AuthProfileScreen extends StatefulWidget {
  const AuthProfileScreen({required this.viewModel, super.key});

  final AuthViewModel viewModel;

  @override
  State<AuthProfileScreen> createState() => _AuthProfileScreenState();
}

class _AuthProfileScreenState extends State<AuthProfileScreen> {
  final _formKey = GlobalKey<FormState>();
  final _nameController = TextEditingController();
  String? _loadedUserId;
  bool _isSaving = false;
  bool _saved = false;
  bool _saveFailed = false;

  @override
  void initState() {
    super.initState();
    widget.viewModel.addListener(_syncProfileName);
    _syncProfileName();
    if (widget.viewModel.user == null && !widget.viewModel.isLoading) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted &&
            widget.viewModel.user == null &&
            !widget.viewModel.isLoading) {
          unawaited(widget.viewModel.restore());
        }
      });
    }
  }

  @override
  void dispose() {
    widget.viewModel.removeListener(_syncProfileName);
    _nameController.dispose();
    super.dispose();
  }

  void _syncProfileName() {
    final user = widget.viewModel.user;
    if (user != null && user.id != _loadedUserId) {
      _loadedUserId = user.id;
      _nameController.text = user.fullName;
    }
  }

  Future<void> _saveProfile() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    setState(() {
      _isSaving = true;
      _saved = false;
      _saveFailed = false;
    });
    final updated = await widget.viewModel.updateProfile(
      fullName: _nameController.text.trim(),
    );
    if (!mounted) return;
    setState(() {
      _isSaving = false;
      _saved = updated;
      _saveFailed = !updated;
    });
  }

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: widget.viewModel,
      builder: (context, _) {
        final user = widget.viewModel.user;
        return Scaffold(
          appBar: AppBar(
            title: const Text('Your profile'),
            actions: [
              AuthAccountSwitcher(viewModel: widget.viewModel),
              AuthAdminNavigationMenu(viewModel: widget.viewModel),
              IconButton(
                tooltip: 'Open dashboard',
                onPressed: () => Navigator.pushNamed(context, '/dashboard'),
                icon: const Icon(Icons.space_dashboard_outlined),
              ),
            ],
          ),
          body: SafeArea(
            child: user == null
                ? _AccountAccessState(viewModel: widget.viewModel)
                : SingleChildScrollView(
                    padding: const EdgeInsets.fromLTRB(20, 20, 20, 32),
                    child: Center(
                      child: ConstrainedBox(
                        constraints: const BoxConstraints(maxWidth: 760),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            Text(
                              'Your profile, in your hands.',
                              style: Theme.of(context).textTheme.headlineMedium
                                  ?.copyWith(
                                    color: _coastInk,
                                    fontWeight: FontWeight.w700,
                                  ),
                            ),
                            const SizedBox(height: 8),
                            const Text(
                              'Keep the details connected to your account current. Email and account history are managed securely by BLUEVERSE.',
                              style: TextStyle(height: 1.5, color: _coastInk),
                            ),
                            const SizedBox(height: 24),
                            Card(
                              color: Colors.white,
                              elevation: 0,
                              shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(24),
                                side: const BorderSide(color: _coastLine),
                              ),
                              child: Padding(
                                padding: const EdgeInsets.all(22),
                                child: Form(
                                  key: _formKey,
                                  child: Column(
                                    crossAxisAlignment:
                                        CrossAxisAlignment.stretch,
                                    children: [
                                      Row(
                                        children: [
                                          CircleAvatar(
                                            backgroundColor: _coastSage,
                                            foregroundColor: _coastDeep,
                                            child: Text(
                                              user.fullName
                                                  .trim()
                                                  .substring(0, 1)
                                                  .toUpperCase(),
                                              style: const TextStyle(
                                                fontWeight: FontWeight.w700,
                                              ),
                                            ),
                                          ),
                                          const SizedBox(width: 12),
                                          Expanded(
                                            child: Text(
                                              'Personal details',
                                              style: Theme.of(context)
                                                  .textTheme
                                                  .titleLarge
                                                  ?.copyWith(
                                                    color: _coastInk,
                                                    fontWeight: FontWeight.w700,
                                                  ),
                                            ),
                                          ),
                                        ],
                                      ),
                                      const SizedBox(height: 22),
                                      TextFormField(
                                        controller: _nameController,
                                        maxLength: 100,
                                        textCapitalization:
                                            TextCapitalization.words,
                                        textInputAction: TextInputAction.done,
                                        decoration: const InputDecoration(
                                          labelText: 'Full name',
                                          helperText: 'The name shown with your BLUEVERSE account.',
                                        ),
                                        validator: (value) {
                                          final name = value?.trim() ?? '';
                                          if (name.isEmpty) {
                                            return 'Enter your name.';
                                          }
                                          if (name.length > 100) {
                                            return 'Use 100 characters or fewer.';
                                          }
                                          return null;
                                        },
                                        onFieldSubmitted: (_) =>
                                            unawaited(_saveProfile()),
                                      ),
                                      const SizedBox(height: 12),
                                      TextFormField(
                                        initialValue: user.email,
                                        readOnly: true,
                                        decoration: const InputDecoration(
                                          labelText: 'Email address',
                                          helperText: 'Email changes are not available from this profile yet.',
                                        ),
                                      ),
                                      if (_saved) ...[
                                        const SizedBox(height: 12),
                                        const _ProfileMessage(
                                          text:
                                              'Your profile has been updated.',
                                          success: true,
                                        ),
                                      ],
                                      if (_saveFailed) ...[
                                        const SizedBox(height: 12),
                                        const _ProfileMessage(
                                          text: 'We couldn’t save your changes just now. Check your connection and try again.',
                                          success: false,
                                        ),
                                      ],
                                      const SizedBox(height: 18),
                                      FilledButton(
                                        onPressed: _isSaving
                                            ? null
                                            : _saveProfile,
                                        child: const Text('Save changes'),
                                      ),
                                    ],
                                  ),
                                ),
                              ),
                            ),
                            const SizedBox(height: 16),
                            _AssignedRolesCard(roles: user.roles),
                            const SizedBox(height: 16),
                            _ProfileSummaryCard(user: user),
                            const SizedBox(height: 16),
                            _ProfilePasswordCard(viewModel: widget.viewModel),
                            const SizedBox(height: 16),
                            _ProfileSessionsCard(viewModel: widget.viewModel),
                            const SizedBox(height: 16),
                            _ProfileDeleteAccountCard(
                              viewModel: widget.viewModel,
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
          ),
        );
      },
    );
  }
}

class _ProfilePasswordCard extends StatefulWidget {
  const _ProfilePasswordCard({required this.viewModel});

  final AuthViewModel viewModel;

  @override
  State<_ProfilePasswordCard> createState() => _ProfilePasswordCardState();
}

class _ProfilePasswordCardState extends State<_ProfilePasswordCard> {
  final _formKey = GlobalKey<FormState>();
  final _currentPassword = TextEditingController();
  final _newPassword = TextEditingController();
  final _confirmPassword = TextEditingController();
  bool _obscurePasswords = true;

  @override
  void dispose() {
    _currentPassword.dispose();
    _newPassword.dispose();
    _confirmPassword.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    await widget.viewModel.changePassword(
      currentPassword: _currentPassword.text,
      newPassword: _newPassword.text,
    );
    if (!mounted) return;
    if (widget.viewModel.errorMessage == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Password changed. Sign in again to continue.'),
        ),
      );
      _currentPassword.clear();
      _newPassword.clear();
      _confirmPassword.clear();
    }
  }

  @override
  Widget build(BuildContext context) {
    return Card(
      color: Colors.white,
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(24),
        side: const BorderSide(color: _coastLine),
      ),
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                'Change password',
                style: Theme.of(context).textTheme.titleLarge
                    ?.copyWith(color: _coastInk, fontWeight: FontWeight.w700),
              ),
              const SizedBox(height: 6),
              const Text(
                'For your security, you’ll sign in again after updating it.',
                style: TextStyle(color: _coastInk, height: 1.4),
              ),
              const SizedBox(height: 16),
              TextFormField(
                controller: _currentPassword,
                obscureText: _obscurePasswords,
                autofillHints: const [AutofillHints.password],
                decoration: InputDecoration(
                  labelText: 'Current password',
                  suffixIcon: IconButton(
                    tooltip: _obscurePasswords
                        ? 'Show passwords'
                        : 'Hide passwords',
                    onPressed: () =>
                        setState(() => _obscurePasswords = !_obscurePasswords),
                    icon: Icon(
                      _obscurePasswords
                          ? Icons.visibility_outlined
                          : Icons.visibility_off_outlined,
                    ),
                  ),
                ),
                validator: (value) => value == null || value.isEmpty
                    ? 'Enter your current password.'
                    : null,
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _newPassword,
                obscureText: _obscurePasswords,
                autofillHints: const [AutofillHints.newPassword],
                decoration: const InputDecoration(
                  labelText: 'New password',
                  helperText: 'Use at least 8 characters.',
                ),
                validator: (value) => (value ?? '').length < 8
                    ? 'Use at least 8 characters.'
                    : null,
              ),
              const SizedBox(height: 12),
              TextFormField(
                controller: _confirmPassword,
                obscureText: _obscurePasswords,
                textInputAction: TextInputAction.done,
                onFieldSubmitted: (_) => _submit(),
                decoration: const InputDecoration(
                  labelText: 'Confirm new password',
                ),
                validator: (value) => value != _newPassword.text
                    ? 'Your new passwords do not match.'
                    : null,
              ),
              if (widget.viewModel.errorMessage != null) ...[
                const SizedBox(height: 12),
                _ProfileMessage(
                  text: widget.viewModel.errorMessage!,
                  success: false,
                ),
              ],
              const SizedBox(height: 16),
              FilledButton(
                onPressed: widget.viewModel.isLoading ? null : _submit,
                child: const Text('Update password'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _ProfileSessionsCard extends StatelessWidget {
  const _ProfileSessionsCard({required this.viewModel});

  final AuthViewModel viewModel;

  Future<void> _endSession(BuildContext context, AuthSession session) async {
    String? password;
    if (!session.isCurrent) {
      password = await _requestPasswordVerification(
        context,
        title: 'End this device session?',
        message: 'Enter your current password to confirm this session change.',
      );
      if (password == null) return;
    }
    await viewModel.endSession(session, currentPassword: password ?? '');
  }

  Future<void> _signOutEverywhere(BuildContext context) async {
    final password = await _requestPasswordVerification(
      context,
      title: 'Sign out this account everywhere?',
      message: 'Enter your current password to end every active session for this account.',
    );
    if (password == null) return;
    await viewModel.logoutAllDevices(currentPassword: password);
  }

  @override
  Widget build(BuildContext context) {
    return Card(
      color: Colors.white,
      elevation: 0,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(24),
        side: const BorderSide(color: _coastLine),
      ),
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    'Login sessions',
                    style: Theme.of(context).textTheme.titleLarge?.copyWith(
                      color: _coastInk,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
                Text(
                  '${viewModel.sessions.length} / $_maximumSessions',
                  style: const TextStyle(
                    color: _coastDeep,
                    fontWeight: FontWeight.w800,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 6),
            const Text(
              'Review signed-in devices and end sessions you no longer use.',
              style: TextStyle(color: _coastInk, height: 1.4),
            ),
            if (viewModel.isLoading && viewModel.sessions.isEmpty)
              const SizedBox.shrink()
            else if (viewModel.sessions.isEmpty) ...[
              const SizedBox(height: 14),
              const Text('No login sessions are available right now.'),
              Align(
                alignment: Alignment.centerLeft,
                child: TextButton.icon(
                  onPressed: viewModel.isLoading
                      ? null
                      : viewModel.reloadSessions,
                  icon: const Icon(Icons.refresh),
                  label: const Text('Try again'),
                ),
              ),
            ] else ...[
              const SizedBox(height: 12),
              ...viewModel.sessions.map(
                (session) => _ProfileSessionTile(
                  session: session,
                  isLoading: viewModel.isLoading,
                  onEnd: () => _endSession(context, session),
                ),
              ),
            ],
            const SizedBox(height: 12),
            OutlinedButton.icon(
              onPressed: viewModel.isLoading
                  ? null
                  : () => _signOutEverywhere(context),
              icon: const Icon(Icons.devices_outlined),
              label: const Text('Sign out this account everywhere'),
            ),
            if (viewModel.errorMessage != null) ...[
              const SizedBox(height: 8),
              _ProfileMessage(text: viewModel.errorMessage!, success: false),
            ],
          ],
        ),
      ),
    );
  }
}

class _ProfileSessionTile extends StatelessWidget {
  const _ProfileSessionTile({
    required this.session,
    required this.isLoading,
    required this.onEnd,
  });

  final AuthSession session;
  final bool isLoading;
  final Future<void> Function() onEnd;

  @override
  Widget build(BuildContext context) {
    return Card(
      color: const Color(0x59E5EFF0),
      elevation: 0,
      margin: const EdgeInsets.only(bottom: 8),
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(18)),
      child: ListTile(
        leading: CircleAvatar(
          backgroundColor: Colors.white,
          foregroundColor: _coastBlue,
          child: Icon(
            session.isCurrent ? Icons.phone_android : Icons.devices_outlined,
          ),
        ),
        title: Text(session.isCurrent ? 'This device' : 'Signed-in device'),
        subtitle: Text(
          '${session.rememberMe ? '30-day' : '1-day'} sign-in · last active ${session.lastSeenAt.toLocal()}',
        ),
        trailing: session.isCurrent
            ? TextButton(
                onPressed: isLoading ? null : () => onEnd(),
                child: const Text('Sign out'),
              )
            : TextButton(
                onPressed: isLoading ? null : () => onEnd(),
                child: const Text('End'),
              ),
      ),
    );
  }
}

class _ProfileDeleteAccountCard extends StatelessWidget {
  const _ProfileDeleteAccountCard({required this.viewModel});

  final AuthViewModel viewModel;

  Future<void> _confirmDelete(BuildContext context) async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        backgroundColor: const Color(0xFFFFF7F6),
        icon: const Icon(Icons.warning_amber_rounded, color: Color(0xFF991B1B)),
        title: const Text('Delete this account permanently?'),
        content: const Text(
          'This cannot be undone. Cancel to keep your account, or confirm to delete it and end its sessions.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            style: FilledButton.styleFrom(
              backgroundColor: const Color(0xFF991B1B),
              foregroundColor: Colors.white,
            ),
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Yes, delete account'),
          ),
        ],
      ),
    );
    if (confirmed != true || !context.mounted) return;
    final deleted = await viewModel.deleteAccount();
    if (deleted && context.mounted && viewModel.user != null) {
      Navigator.pushNamedAndRemoveUntil(
        context,
        '/dashboard',
        (_) => false,
      );
    }
  }

  @override
  Widget build(BuildContext context) => Card(
    color: const Color(0xFFFFF7F6),
    elevation: 0,
    shape: RoundedRectangleBorder(
      borderRadius: BorderRadius.circular(24),
      side: const BorderSide(color: Color(0xFFFECACA)),
    ),
    child: Padding(
      padding: const EdgeInsets.all(20),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            'Delete your account',
            style: Theme.of(context).textTheme.titleLarge?.copyWith(
              color: const Color(0xFF7F1D1D),
              fontWeight: FontWeight.w700,
            ),
          ),
          const SizedBox(height: 6),
          const Text(
            'This permanently removes your BLUEVERSE account and its sessions. System-managed accounts cannot delete themselves.',
            style: TextStyle(color: Color(0xFF7F1D1D), height: 1.45),
          ),
          if (viewModel.errorMessage != null) ...[
            const SizedBox(height: 12),
            _ProfileMessage(text: viewModel.errorMessage!, success: false),
          ],
          const SizedBox(height: 14),
          Align(
            alignment: Alignment.centerLeft,
            child: FilledButton.icon(
              style: FilledButton.styleFrom(
                backgroundColor: const Color(0xFF991B1B),
                foregroundColor: Colors.white,
              ),
              onPressed: viewModel.isLoading
                  ? null
                  : () => _confirmDelete(context),
              icon: const Icon(Icons.delete_outline),
              label: const Text('Delete account'),
            ),
          ),
        ],
      ),
    ),
  );
}

class _AssignedRolesCard extends StatelessWidget {
  const _AssignedRolesCard({required this.roles});

  final List<String> roles;

  @override
  Widget build(BuildContext context) => Card(
    color: Colors.white,
    elevation: 0,
    shape: RoundedRectangleBorder(
      borderRadius: BorderRadius.circular(24),
      side: const BorderSide(color: _coastLine),
    ),
    child: Padding(
      padding: const EdgeInsets.all(22),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Roles allocated to you',
            style: Theme.of(context).textTheme.titleLarge
                ?.copyWith(color: _coastInk, fontWeight: FontWeight.w700),
          ),
          const SizedBox(height: 12),
          if (roles.isEmpty)
            const Text(
              'No roles have been allocated to this account yet.',
              style: TextStyle(color: _coastInk, height: 1.45),
            )
          else
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: roles
                  .map(
                    (role) =>
                        Chip(label: Text(role), backgroundColor: _coastSage),
                  )
                  .toList(),
            ),
        ],
      ),
    ),
  );
}

class _ProfileSummaryCard extends StatelessWidget {
  const _ProfileSummaryCard({required this.user});

  final AuthUser user;

  @override
  Widget build(BuildContext context) {
    return Card(
      color: _coastSage,
      elevation: 0,
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(24)),
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const Text(
              'ACCOUNT SINCE',
              style: TextStyle(
                color: _coastBlue,
                fontSize: 11,
                fontWeight: FontWeight.w800,
                letterSpacing: 1.4,
              ),
            ),
            const SizedBox(height: 8),
            Text(
              _monthAndYear(user.createdAt),
              style: Theme.of(context).textTheme.titleLarge
                  ?.copyWith(color: _coastInk, fontWeight: FontWeight.w700),
            ),
            const SizedBox(height: 6),
            const Text(
              'Your BLUEVERSE account keeps coastal places and people in view.',
              style: TextStyle(color: _coastInk, height: 1.45),
            ),
          ],
        ),
      ),
    );
  }
}

class _ProfileMessage extends StatelessWidget {
  const _ProfileMessage({required this.text, required this.success});

  final String text;
  final bool success;

  @override
  Widget build(BuildContext context) {
    final foreground = success ? _coastDeep : Colors.red.shade900;
    final background = success ? _coastSage : Colors.red.shade50;
    return Semantics(
      liveRegion: true,
      child: Container(
        decoration: BoxDecoration(
          color: background,
          borderRadius: BorderRadius.circular(16),
        ),
        padding: const EdgeInsets.all(12),
        child: Text(text, style: TextStyle(color: foreground, height: 1.4)),
      ),
    );
  }
}

class AuthDashboardScreen extends StatefulWidget {
  const AuthDashboardScreen({required this.viewModel, super.key});

  final AuthViewModel viewModel;

  @override
  State<AuthDashboardScreen> createState() => _AuthDashboardScreenState();
}

class _AuthDashboardScreenState extends State<AuthDashboardScreen> {
  @override
  void initState() {
    super.initState();
    if (widget.viewModel.user == null && !widget.viewModel.isLoading) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted &&
            widget.viewModel.user == null &&
            !widget.viewModel.isLoading) {
          unawaited(widget.viewModel.restore());
        }
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: widget.viewModel,
      builder: (context, _) {
        final user = widget.viewModel.user;
        return Scaffold(
          appBar: AppBar(
            title: const Text('Your BLUEVERSE'),
            actions: [
              AuthAccountSwitcher(viewModel: widget.viewModel),
              AuthAdminNavigationMenu(viewModel: widget.viewModel),
              if (user != null)
                IconButton(
                  tooltip: 'Edit your profile',
                  onPressed: () => Navigator.pushNamed(context, '/profile'),
                  icon: const Icon(Icons.person_outline),
                ),
            ],
          ),
          body: SafeArea(
            child: widget.viewModel.isLoading && user == null
                ? const SizedBox.shrink()
                : user == null
                ? _AccountAccessState(viewModel: widget.viewModel)
                : _buildDashboard(context, user),
          ),
        );
      },
    );
  }

  Widget _buildDashboard(BuildContext context, AuthUser user) {
    return SingleChildScrollView(
      padding: const EdgeInsets.fromLTRB(20, 18, 20, 32),
      child: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 820),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Container(
                decoration: BoxDecoration(
                  color: _coastDeep,
                  borderRadius: BorderRadius.circular(24),
                ),
                padding: const EdgeInsets.all(22),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      'A SHARED COASTAL VIEW',
                      style: TextStyle(
                        color: Color(0xFFC9E0E6),
                        fontSize: 11,
                        fontWeight: FontWeight.w800,
                        letterSpacing: 1.3,
                      ),
                    ),
                    const SizedBox(height: 8),
                    Text(
                      'Welcome, ${user.fullName.split(' ').first}.',
                      style: Theme.of(context).textTheme.headlineSmall
                          ?.copyWith(
                            color: Colors.white,
                            fontWeight: FontWeight.w700,
                          ),
                    ),
                    const SizedBox(height: 8),
                    const Text(
                      'Your account details today, with room for coastal experiences as they become available.',
                      style: TextStyle(color: Colors.white70, height: 1.5),
                    ),
                    const SizedBox(height: 18),
                    FilledButton.tonalIcon(
                      onPressed: () => Navigator.pushNamed(context, '/profile'),
                      icon: const Icon(Icons.edit_outlined),
                      label: const Text('Edit your profile'),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 16),
              const SizedBox(height: 28),
              Text(
                'What’s ahead',
                style: Theme.of(context).textTheme.titleLarge
                    ?.copyWith(color: _coastInk, fontWeight: FontWeight.w700),
              ),
              const SizedBox(height: 4),
              const Text(
                'BLUEVERSE’s coastal services are taking shape. Their updates will appear here when they are ready.',
                style: TextStyle(color: _coastInk, height: 1.45),
              ),
              const SizedBox(height: 12),
              const _CoastalFocusTile(
                icon: Icons.explore_outlined,
                title: 'Coastal discovery',
                description: 'Places and experiences with context from local communities and marine life.',
              ),
              const _CoastalFocusTile(
                icon: Icons.waves_outlined,
                title: 'Marine awareness',
                description: 'Clearer context for changing conditions and considered days by the water.',
              ),
              const _CoastalFocusTile(
                icon: Icons.eco_outlined,
                title: 'Shared stewardship',
                description: 'A connected view for people visiting, working along and caring for the shore.',
              ),
              const SizedBox(height: 12),
              OutlinedButton.icon(
                onPressed: () => Navigator.pushNamed(context, '/profile'),
                icon: const Icon(Icons.person_outline),
                label: const Text('Manage your profile'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _CoastalFocusTile extends StatelessWidget {
  const _CoastalFocusTile({
    required this.icon,
    required this.title,
    required this.description,
  });

  final IconData icon;
  final String title;
  final String description;

  @override
  Widget build(BuildContext context) {
    return Card(
      color: Colors.white,
      elevation: 0,
      margin: const EdgeInsets.only(bottom: 10),
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(22),
        side: const BorderSide(color: _coastLine),
      ),
      child: ListTile(
        contentPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
        leading: CircleAvatar(
          backgroundColor: _coastSage,
          foregroundColor: _coastBlue,
          child: Icon(icon),
        ),
        title: Text(title, style: const TextStyle(fontWeight: FontWeight.w700)),
        subtitle: Padding(
          padding: const EdgeInsets.only(top: 4),
          child: Text(description, style: const TextStyle(height: 1.4)),
        ),
        trailing: const Text(
          'SOON',
          style: TextStyle(
            color: _coastTeal,
            fontSize: 10,
            fontWeight: FontWeight.w800,
            letterSpacing: 1.1,
          ),
        ),
      ),
    );
  }
}

class _AccountAccessState extends StatelessWidget {
  const _AccountAccessState({required this.viewModel});

  final AuthViewModel viewModel;

  @override
  Widget build(BuildContext context) {
    if (viewModel.isLoading) {
      return const SizedBox.shrink();
    }
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 480),
          child: Card(
            color: Colors.white,
            elevation: 0,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(24),
              side: const BorderSide(color: _coastLine),
            ),
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text(
                    'Sign in to see your BLUEVERSE.',
                    style: Theme.of(context).textTheme.titleLarge?.copyWith(
                      color: _coastInk,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  const SizedBox(height: 8),
                  const Text(
                    'Your profile and account summary will appear here after you sign in.',
                    style: TextStyle(color: _coastInk, height: 1.45),
                  ),
                  const SizedBox(height: 18),
                  FilledButton(
                    onPressed: () => Navigator.pushNamed(context, '/signin'),
                    child: const Text('Sign in'),
                  ),
                  TextButton(
                    onPressed: () => Navigator.pushNamed(context, '/signup'),
                    child: const Text('Create account'),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

String _monthAndYear(DateTime date) {
  const months = [
    'January',
    'February',
    'March',
    'April',
    'May',
    'June',
    'July',
    'August',
    'September',
    'October',
    'November',
    'December',
  ];
  final localDate = date.toLocal();
  return '${months[localDate.month - 1]} ${localDate.year}';
}
