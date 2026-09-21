import 'package:flutter/material.dart';

import '../data/models/auth_models.dart';
import 'auth_view_model.dart';

class AuthLoginScreen extends StatefulWidget {
  const AuthLoginScreen({required this.viewModel, super.key});

  final AuthViewModel viewModel;

  @override
  State<AuthLoginScreen> createState() => _AuthLoginScreenState();
}

class _AuthLoginScreenState extends State<AuthLoginScreen> {
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  bool _rememberMe = false;

  @override
  void dispose() {
    _emailController.dispose();
    _passwordController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: widget.viewModel,
      builder: (context, _) {
        final user = widget.viewModel.user;
        return Scaffold(
          appBar: AppBar(title: const Text('BLUEVERSE Auth')),
          body: Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 460),
              child: Padding(
                padding: const EdgeInsets.all(24),
                child: user == null
                    ? _buildLoginForm(context)
                    : _buildAccount(context, user),
              ),
            ),
          ),
        );
      },
    );
  }

  Widget _buildLoginForm(BuildContext context) {
    final viewModel = widget.viewModel;
    return AutofillGroup(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text('Sign in', style: Theme.of(context).textTheme.headlineMedium),
          const SizedBox(height: 8),
          const Text(
            'Your device identifier and session keys are stored in secure platform storage.',
          ),
          const SizedBox(height: 24),
          TextField(
            controller: _emailController,
            keyboardType: TextInputType.emailAddress,
            autofillHints: const [AutofillHints.email],
            decoration: const InputDecoration(labelText: 'Email'),
          ),
          TextField(
            controller: _passwordController,
            obscureText: true,
            autofillHints: const [AutofillHints.password],
            decoration: const InputDecoration(labelText: 'Password'),
          ),
          CheckboxListTile(
            contentPadding: EdgeInsets.zero,
            value: _rememberMe,
            onChanged: (value) => setState(() => _rememberMe = value ?? false),
            title: const Text('Remember me for 30 days'),
          ),
          if (viewModel.errorMessage != null)
            Padding(
              padding: const EdgeInsets.only(bottom: 12),
              child: Text(
                viewModel.errorMessage!,
                style: TextStyle(color: Theme.of(context).colorScheme.error),
              ),
            ),
          FilledButton(
            onPressed: viewModel.isLoading
                ? null
                : () => viewModel.signIn(
                    email: _emailController.text.trim(),
                    password: _passwordController.text,
                    rememberMe: _rememberMe,
                  ),
            child: Text(viewModel.isLoading ? 'Signing in…' : 'Sign in'),
          ),
        ],
      ),
    );
  }

  Widget _buildAccount(BuildContext context, AuthUser user) {
    final viewModel = widget.viewModel;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(user.fullName, style: Theme.of(context).textTheme.headlineMedium),
        Text(user.email),
        const SizedBox(height: 24),
        Text(
          'Active sessions (${viewModel.sessions.length}/5)',
          style: Theme.of(context).textTheme.titleLarge,
        ),
        const SizedBox(height: 8),
        if (viewModel.sessions.isEmpty)
          const Text('No active sessions were returned.'),
        ...viewModel.sessions.map(_sessionTile),
        const SizedBox(height: 16),
        OutlinedButton(
          onPressed: viewModel.isLoading ? null : viewModel.logoutCurrentDevice,
          child: const Text('Log out this device'),
        ),
        FilledButton(
          onPressed: viewModel.isLoading ? null : viewModel.logoutAllDevices,
          child: const Text('Log out everywhere'),
        ),
        if (viewModel.errorMessage != null)
          Text(
            viewModel.errorMessage!,
            style: TextStyle(color: Theme.of(context).colorScheme.error),
          ),
      ],
    );
  }

  Widget _sessionTile(AuthSession session) {
    return ListTile(
      contentPadding: EdgeInsets.zero,
      title: Text(session.isCurrent ? 'This device' : 'Signed-in device'),
      subtitle: Text(
        '${session.rememberMe ? '30-day' : '1-day'} session · expires ${session.expiresAt.toLocal()}',
      ),
      trailing: session.isCurrent ? const Chip(label: Text('Current')) : null,
    );
  }
}
