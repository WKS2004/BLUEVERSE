import 'package:flutter/material.dart';

import 'data/models/auth_models.dart';
import 'data/repositories/auth_repository.dart';
import 'data/services/auth_api_service.dart';

import 'package:http/http.dart' as http;

import 'data/repositories/marine_repository.dart';
import 'data/services/marine_service.dart';
import 'data/services/marine_api_client.dart';
import 'data/services/api_gateway_config.dart';
import 'ui/account_screens.dart';
import 'ui/onboarding_screen.dart';
import 'ui/auth_admin_screen.dart';
import 'ui/auth_registration_screen.dart';
import 'ui/auth_screens.dart';
import 'ui/auth_view_model.dart';
import 'ui/blueverse_theme.dart';
import 'ui/feedback/blueverse_error_screen.dart';
import 'ui/feedback/blueverse_loading_screen.dart';
import 'ui/feedback/loading_screen_controller.dart';
import 'ui/marine/marine_auth_navigation.dart';
import 'ui/marine/marine_permissions_guard.dart';
import 'ui/marine/marine_route_table.dart';
import 'ui/marine/marine_workspace_screen.dart';

void main() {
  ErrorWidget.builder = blueverseUnexpectedErrorWidget;
  runApp(const MyApp());
}

class MyApp extends StatefulWidget {
  const MyApp({super.key, this.authRepository, this.marineService});

  final AuthRepository? authRepository;
  final MarineService? marineService;

  @override
  State<MyApp> createState() => _MyAppState();
}

class _MyAppState extends State<MyApp> {
  final GlobalKey<NavigatorState> _navigatorKey = GlobalKey<NavigatorState>();
  late final AuthViewModel _authViewModel;
  bool _initialSessionHandled = false;
  String? _lastAuthenticatedUserId;
  http.Client? _marineHttpClient;
  late final MarineService _marineService;

  @override
  void initState() {
    super.initState();
    _authViewModel = AuthViewModel(
      repository:
          widget.authRepository ?? AuthRepository(apiService: AuthApiService()),
    )..addListener(_handleSessionChange);
    _marineService = _buildMarineService();

    WidgetsBinding.instance.addPostFrameCallback((_) {
      _authViewModel.restore();
      _navigationFor(_authViewModel.user);
    });
  }

  @override
  void dispose() {
    _marineHttpClient?.close();
    _authViewModel
      ..removeListener(_handleSessionChange)
      ..dispose();
    super.dispose();
  }

  void _handleSessionChange() {
    if (!_authViewModel.hasRestoredSession) return;

    final activeUserId = _authViewModel.user?.id;
    if (!_initialSessionHandled) {
      _initialSessionHandled = true;
      _lastAuthenticatedUserId = activeUserId;
      _navigationFor(_authViewModel.user);
      return;
    }

    final wasAuthenticated = _lastAuthenticatedUserId != null;
    _lastAuthenticatedUserId = activeUserId;
    if (wasAuthenticated && activeUserId == null) {
      _replaceNavigationWith('/');
    }
  }

  void _navigationFor(AuthUser? user) {
    if (user == null) return;

    _replaceNavigationWith('/dashboard');
  }

  void _replaceNavigationWith(String routeName) {
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _navigatorKey.currentState?.pushNamedAndRemoveUntil(
        routeName,
        (_) => false,
      );
    });
  }

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'BLUEVERSE',
      navigatorKey: _navigatorKey,
      theme: BlueverseTheme.light,
      builder: (context, child) => ListenableBuilder(
        listenable: blueverseLoadingScreenController,
        builder: (context, _) => Stack(
          fit: StackFit.expand,
          children: [
            child ?? const SizedBox.shrink(),
            if (blueverseLoadingScreenController.isVisible)
              BlueverseLoadingScreen(
                message: blueverseLoadingScreenController.message,
                isExiting: blueverseLoadingScreenController.isExiting,
                exitDuration:
                    blueverseLoadingScreenController.exitAnimationDuration,
              ),
          ],
        ),
      ),
      routes: {
        '/': (_) => MobileLaunchPage(viewModel: _authViewModel),
        '/signin': (_) => AuthLoginScreen(viewModel: _authViewModel),
        '/signup': (_) => AuthRegistrationScreen(viewModel: _authViewModel),
        '/profile': (_) => AuthProfileScreen(viewModel: _authViewModel),
        '/dashboard': (_) => AuthDashboardScreen(viewModel: _authViewModel),
        '/admin': (_) => AuthAdminLandingScreen(viewModel: _authViewModel),
        '/admin/permissions': (_) => AuthAdminScreen(
          viewModel: _authViewModel,
          section: AuthAdminSection.permissions,
        ),
        '/admin/roles': (_) => AuthAdminScreen(
          viewModel: _authViewModel,
          section: AuthAdminSection.roles,
        ),
        '/admin/users': (_) => AuthAdminScreen(
          viewModel: _authViewModel,
          section: AuthAdminSection.users,
        ),
        '/404': (_) => const BlueverseErrorScreen.notFound(),
        '/500': (_) => const BlueverseErrorScreen.server(),
        '/marine/conditions': (_) =>
            _marineShell(route: MarineRoutes.conditions),
        '/marine/history': (_) => _marineShell(route: MarineRoutes.history),
        '/marine/safety-profiles': (_) =>
            _marineShell(route: MarineRoutes.safetyProfiles),
      },
      onUnknownRoute: blueverseUnknownRoute,
    );
  }

  MarineService _buildMarineService() {
    final injected = widget.marineService;
    if (injected != null) return injected;
    final gateway = const ApiGatewayConfig();
    _marineHttpClient = http.Client();
    final client = MarineApiClient(
      client: _marineHttpClient!,
      gateway: gateway,
      authenticatedHeaders:
          _authViewModel.repository.apiService.authenticatedHeaders,
      beginRequest: (method) =>
          method == 'GET' ? () {} : blueverseLoadingScreenController.begin(),
    );
    final repository = MarineRepository(client: client);
    return MarineService(repository: repository);
  }

  Widget _marineShell({required String route}) => ListenableBuilder(
    listenable: _authViewModel,
    builder: (context, _) {
      final user = _authViewModel.user;
      if (!hasMarineReadAccess(user) ||
          !marineRouteHrefsFor(user).contains(route)) {
        return Scaffold(
          appBar: AppBar(title: const Text('Marine & safety')),
          body: Center(
            child: Padding(
              padding: const EdgeInsets.all(24),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Text(
                    'This coastal service needs permission. Ask an administrator to review your role assignments if you need access.',
                    textAlign: TextAlign.center,
                  ),
                  TextButton(
                    onPressed: () => Navigator.pushNamed(context, '/dashboard'),
                    child: const Text('Return to dashboard'),
                  ),
                ],
              ),
            ),
          ),
        );
      }
      return MarineWorkspaceScreen(
        key: ValueKey('${user!.id}:${user.permissions.join(',')}'),
        service: _marineService,
        initialIndex: route == MarineRoutes.history
            ? 1
            : route == MarineRoutes.safetyProfiles
            ? 2
            : 0,
        canManage: user.permissions.contains('marine.profile.manage'),
        currentUserId: user.id,
        actions: [
          IconButton(
            tooltip: 'Dashboard',
            icon: const Icon(Icons.home_outlined),
            onPressed: () => Navigator.pushNamed(context, '/dashboard'),
          ),
          IconButton(
            tooltip: 'Profile',
            icon: const Icon(Icons.person_outline),
            onPressed: () => Navigator.pushNamed(context, '/profile'),
          ),
          AuthAdminNavigationMenu(viewModel: _authViewModel),
          AuthAccountSwitcher(viewModel: _authViewModel),
        ],
      );
    },
  );
}

class MobileLaunchPage extends StatelessWidget {
  const MobileLaunchPage({super.key, required this.viewModel});

  final AuthViewModel viewModel;

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: viewModel,
      builder: (context, _) {
        if (!viewModel.hasRestoredSession) {
          return Scaffold(
            backgroundColor: BlueversePalette.coastPaper,
            body: Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Icon(
                    Icons.waves_rounded,
                    color: BlueversePalette.coastDeep,
                    size: 42,
                  ),
                  const SizedBox(height: 14),
                  Text(
                    'BLUEVERSE',
                    style: Theme.of(context).textTheme.titleMedium?.copyWith(
                      color: BlueversePalette.coastDeep,
                      fontWeight: FontWeight.w800,
                      letterSpacing: 1.8,
                    ),
                  ),
                ],
              ),
            ),
          );
        }
        return const BlueverseOnboardingScreen();
      },
    );
  }
}
