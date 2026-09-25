import 'package:flutter/material.dart';

import 'auth_view_model.dart';

class AuthRegistrationScreen extends StatefulWidget {
  const AuthRegistrationScreen({required this.viewModel, super.key});

  final AuthViewModel viewModel;

  @override
  State<AuthRegistrationScreen> createState() => _AuthRegistrationScreenState();
}

class _AuthRegistrationScreenState extends State<AuthRegistrationScreen> {
  final _formKey = GlobalKey<FormState>();
  final _fullNameController = TextEditingController();
  final _emailController = TextEditingController();
  final _passwordController = TextEditingController();
  final _confirmPasswordController = TextEditingController();
  bool _rememberMe = false;
  bool _obscurePassword = true;

  @override
  void dispose() {
    _fullNameController.dispose();
    _emailController.dispose();
    _passwordController.dispose();
    _confirmPasswordController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    return Scaffold(
      appBar: AppBar(title: const Text('Create account')),
      body: ListenableBuilder(
        listenable: widget.viewModel,
        builder: (context, _) {
          return Center(
            child: SingleChildScrollView(
              padding: const EdgeInsets.all(24),
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 520),
                child: Card(
                  child: Padding(
                    padding: const EdgeInsets.all(28),
                    child: AutofillGroup(
                      child: Form(
                        key: _formKey,
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            Text(
                              'A place in the BLUEVERSE',
                              style: Theme.of(context).textTheme.labelLarge
                                  ?.copyWith(
                                    color: colors.primary,
                                    fontWeight: FontWeight.w800,
                                    letterSpacing: 1.2,
                                  ),
                            ),
                            const SizedBox(height: 10),
                            Text(
                              'Come closer to the coast.',
                              style: Theme.of(context).textTheme.headlineMedium
                                  ?.copyWith(fontWeight: FontWeight.w700),
                            ),
                            const SizedBox(height: 8),
                            Text(
                              'Create an account to discover coastal experiences with a clearer view of the sea, its people and the living world around them.',
                              style: Theme.of(context).textTheme.bodyMedium,
                            ),
                            const SizedBox(height: 24),
                            TextFormField(
                              controller: _fullNameController,
                              autofillHints: const [AutofillHints.name],
                              maxLength: 100,
                              textCapitalization: TextCapitalization.words,
                              textInputAction: TextInputAction.next,
                              decoration: const InputDecoration(
                                labelText: 'Full name',
                                counterText: '',
                              ),
                              validator: (value) {
                                if (value == null || value.trim().isEmpty) {
                                  return 'Enter your name.';
                                }
                                return null;
                              },
                            ),
                            const SizedBox(height: 14),
                            TextFormField(
                              controller: _emailController,
                              autofillHints: const [AutofillHints.email],
                              keyboardType: TextInputType.emailAddress,
                              textInputAction: TextInputAction.next,
                              decoration: const InputDecoration(
                                labelText: 'Email address',
                              ),
                              validator: (value) {
                                final email = value?.trim() ?? '';
                                if (email.isEmpty) return 'Enter your email.';
                                if (!RegExp(r'^[^@\s]+@[^@\s]+\.[^@\s]+$')
                                    .hasMatch(email)) {
                                  return 'Enter a valid email address.';
                                }
                                return null;
                              },
                            ),
                            const SizedBox(height: 14),
                            TextFormField(
                              controller: _passwordController,
                              autofillHints: const [AutofillHints.newPassword],
                              obscureText: _obscurePassword,
                              textInputAction: TextInputAction.next,
                              decoration: InputDecoration(
                                labelText: 'Password',
                                helperText: 'Use at least 8 characters.',
                                suffixIcon: IconButton(
                                  tooltip: _obscurePassword
                                      ? 'Show password'
                                      : 'Hide password',
                                  onPressed: () => setState(
                                    () => _obscurePassword = !_obscurePassword,
                                  ),
                                  icon: Icon(
                                    _obscurePassword
                                        ? Icons.visibility_outlined
                                        : Icons.visibility_off_outlined,
                                  ),
                                ),
                              ),
                              validator: (value) {
                                if ((value ?? '').length < 8) {
                                  return 'Use at least 8 characters.';
                                }
                                return null;
                              },
                            ),
                            const SizedBox(height: 14),
                            TextFormField(
                              controller: _confirmPasswordController,
                              autofillHints: const [AutofillHints.newPassword],
                              obscureText: _obscurePassword,
                              textInputAction: TextInputAction.done,
                              onFieldSubmitted: (_) => _submit(),
                              decoration: const InputDecoration(
                                labelText: 'Confirm password',
                              ),
                              validator: (value) {
                                if (value == null || value.isEmpty) {
                                  return 'Confirm your password.';
                                }
                                if (value != _passwordController.text) {
                                  return 'Your passwords do not match.';
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
                              title: const Text(
                                'Keep me signed in for 30 days',
                              ),
                              controlAffinity: ListTileControlAffinity.leading,
                            ),
                            if (widget.viewModel.accounts.length >= 5)
                              const Padding(
                                padding: EdgeInsets.only(top: 8),
                                child: Text(
                                  'This device already has five signed-in accounts. Remove one from the account menu before adding another.',
                                  style: TextStyle(
                                    color: Color(0xFF205C79),
                                    height: 1.4,
                                  ),
                                ),
                              ),
                            if (widget.viewModel.errorMessage != null) ...[
                              const SizedBox(height: 8),
                              Semantics(
                                liveRegion: true,
                                child: Text(
                                  widget.viewModel.errorMessage!,
                                  style: TextStyle(color: colors.error),
                                ),
                              ),
                            ],
                            const SizedBox(height: 16),
                            FilledButton(
                              onPressed:
                                  widget.viewModel.isLoading ||
                                      widget.viewModel.accounts.length >= 5
                                  ? null
                                  : _submit,
                              child: const Text('Create account'),
                            ),
                            const SizedBox(height: 8),
                            TextButton(
                              onPressed: () => Navigator.pop(context),
                              child: const Text(
                                'Already have an account? Sign in',
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
                ),
              ),
            ),
          );
        },
      ),
    );
  }

  Future<void> _submit() async {
    if (widget.viewModel.accounts.length >= 5) return;
    if (!(_formKey.currentState?.validate() ?? false)) return;

    final created = await widget.viewModel.createAccount(
      fullName: _fullNameController.text.trim(),
      email: _emailController.text.trim(),
      password: _passwordController.text,
      rememberMe: _rememberMe,
    );
    if (created && mounted) {
      Navigator.pushReplacementNamed(context, '/profile');
    }
  }
}
