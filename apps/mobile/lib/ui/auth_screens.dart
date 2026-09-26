import 'package:flutter/material.dart';

import '../data/models/auth_models.dart';
import 'account_screens.dart';
import 'auth_view_model.dart';

class AuthLoginScreen extends StatefulWidget {
  const AuthLoginScreen({required this.viewModel, super.key});

  final AuthViewModel viewModel;

  @override
  State<AuthLoginScreen> createState() => _AuthLoginScreenState();
}

class _AuthLoginScreenState extends State<AuthLoginScreen> {
  final _formKey = GlobalKey<FormState>();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  bool _rememberMe = false;
  bool _obscurePassword = true;
  bool _addingAccount = false;

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
        final showLoginForm = user == null || _addingAccount;
        return Scaffold(
          appBar: AppBar(
            title: Text(showLoginForm ? 'Sign in' : 'Your accounts'),
            actions: [
              if (user != null)
                AuthAccountSwitcher(viewModel: widget.viewModel),
            ],
          ),
          body: Center(
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(24),
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 520),
                child: Card(
                  child: Padding(
                    padding: const EdgeInsets.all(28),
                    child: showLoginForm
                        ? _buildLoginForm(context)
                        : _buildAccount(context, user),
                  ),
                ),
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
      child: Form(
        key: _formKey,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              'Welcome back to BLUEVERSE',
              style: Theme.of(context).textTheme.headlineMedium
                  ?.copyWith(fontWeight: FontWeight.w700),
            ),
            const SizedBox(height: 8),
            const Text(
              'Sign in to continue exploring the people, places and marine life connected to the coast.',
            ),
            const SizedBox(height: 24),
            TextFormField(
              controller: _emailController,
              keyboardType: TextInputType.emailAddress,
              autofillHints: const [AutofillHints.username],
              textInputAction: TextInputAction.next,
              decoration: const InputDecoration(labelText: 'Email address'),
              validator: (value) {
                if (value == null || value.trim().isEmpty) {
                  return 'Enter your email address.';
                }
                return null;
              },
            ),
            const SizedBox(height: 16),
            TextFormField(
              controller: _passwordController,
              obscureText: _obscurePassword,
              autofillHints: const [AutofillHints.password],
              textInputAction: TextInputAction.done,
              onFieldSubmitted: (_) => _submitLogin(),
              decoration: InputDecoration(
                labelText: 'Password',
                suffixIcon: IconButton(
                  tooltip: _obscurePassword ? 'Show password' : 'Hide password',
                  onPressed: () =>
                      setState(() => _obscurePassword = !_obscurePassword),
                  icon: Icon(
                    _obscurePassword
                        ? Icons.visibility_outlined
                        : Icons.visibility_off_outlined,
                  ),
                ),
              ),
              validator: (value) {
                if (value == null || value.isEmpty) {
                  return 'Enter your password.';
                }
                return null;
              },
            ),
            const SizedBox(height: 8),
            CheckboxListTile(
              contentPadding: EdgeInsets.zero,
              value: _rememberMe,
              onChanged: (value) =>
                  setState(() => _rememberMe = value ?? false),
              title: const Text('Keep me signed in for 30 days'),
              controlAffinity: ListTileControlAffinity.leading,
            ),
            if (viewModel.errorMessage != null)
              Padding(
                padding: const EdgeInsets.only(bottom: 12),
                child: Semantics(
                  liveRegion: true,
                  child: Text(
                    viewModel.errorMessage!,
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                  ),
                ),
              ),
            FilledButton(
              onPressed: viewModel.isLoading ? null : _submitLogin,
              child: const Text('Sign in'),
            ),
            const SizedBox(height: 8),
            TextButton(
              onPressed: () => Navigator.pushNamed(context, '/signup'),
              child: const Text('New to BLUEVERSE? Create an account'),
            ),
            if (viewModel.user != null && _addingAccount)
              TextButton.icon(
                onPressed: () => setState(() => _addingAccount = false),
                icon: const Icon(Icons.arrow_back),
                label: const Text('Back to saved accounts'),
              ),
          ],
        ),
      ),
    );
  }

  Future<void> _submitLogin() async {
    if (!(_formKey.currentState?.validate() ?? false)) return;
    final alreadySignedIn = widget.viewModel.user != null;
    await widget.viewModel.signIn(
      email: _emailController.text.trim(),
      password: _passwordController.text,
      rememberMe: _rememberMe,
    );
    if (!mounted || widget.viewModel.errorMessage != null) return;
    if (!alreadySignedIn && widget.viewModel.user != null) {
      Navigator.pushNamedAndRemoveUntil(context, '/dashboard', (_) => false);
      return;
    }
    setState(() => _addingAccount = false);
  }

  Widget _buildAccount(BuildContext context, AuthUser user) {
    final viewModel = widget.viewModel;
    final canAddAccount = viewModel.accounts.length < 5;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(user.fullName, style: Theme.of(context).textTheme.headlineMedium),
        Text(user.email),
        const SizedBox(height: 12),
        OutlinedButton.icon(
          onPressed: () => Navigator.pushNamed(context, '/profile'),
          icon: const Icon(Icons.person_outline),
          label: const Text('Edit profile'),
        ),
        OutlinedButton.icon(
          onPressed: () => Navigator.pushNamed(context, '/dashboard'),
          icon: const Icon(Icons.space_dashboard_outlined),
          label: const Text('Open dashboard'),
        ),
        const SizedBox(height: 24),
        Row(
          children: [
            Expanded(
              child: Text(
                'Accounts on this device (${viewModel.accounts.length}/5)',
                style: Theme.of(context).textTheme.titleLarge,
              ),
            ),
            const Icon(Icons.devices_outlined, color: Color(0xFF347B9B)),
          ],
        ),
        const SizedBox(height: 8),
        ...viewModel.accounts.map((account) {
          final isCurrent = account.id == user.id;
          return Card(
            elevation: 0,
            margin: const EdgeInsets.only(bottom: 8),
            color: isCurrent ? const Color(0xFFE5EFF0) : Colors.white,
            child: ListTile(
              leading: CircleAvatar(
                backgroundColor: Colors.white,
                child: Icon(
                  isCurrent
                      ? Icons.check_circle
                      : Icons.account_circle_outlined,
                  color: const Color(0xFF347B9B),
                ),
              ),
              title: Text(
                account.fullName,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
              subtitle: Text(
                account.email,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
              trailing: isCurrent
                  ? const Chip(label: Text('Current'))
                  : IconButton(
                      tooltip: 'Switch to ${account.fullName}',
                      onPressed: viewModel.isLoading
                          ? null
                          : () => viewModel.switchAccount(account.id),
                      icon: const Icon(Icons.login_outlined),
                    ),
            ),
          );
        }),
        const SizedBox(height: 8),
        FilledButton.tonalIcon(
          onPressed: canAddAccount && !viewModel.isLoading
              ? () => setState(() => _addingAccount = true)
              : null,
          icon: const Icon(Icons.person_add_alt_1_outlined),
          label: Text(
            canAddAccount
                ? 'Sign in to another account'
                : 'Account limit reached',
          ),
        ),
        OutlinedButton.icon(
          onPressed: canAddAccount && !viewModel.isLoading
              ? () => Navigator.pushNamed(context, '/signup')
              : null,
          icon: const Icon(Icons.person_add_outlined),
          label: const Text('Create another account'),
        ),
        const SizedBox(height: 12),
        OutlinedButton(
          onPressed: viewModel.isLoading ? null : viewModel.logoutCurrentDevice,
          child: const Text('Sign out from this device'),
        ),
        if (viewModel.errorMessage != null)
          Text(
            viewModel.errorMessage!,
            style: TextStyle(color: Theme.of(context).colorScheme.error),
          ),
      ],
    );
  }
}
