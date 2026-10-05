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
import 'ui/marine/marine_condition_screen.dart';
import 'ui/marine/marine_permissions_guard.dart';
import 'ui/marine/marine_route_table.dart';
import 'ui/marine/suitability_screen.dart';
import 'ui/marine/history_screen.dart';
import 'ui/marine/safety_profiles_screen.dart';

void main() {
  ErrorWidget.builder = blueverseUnexpectedErrorWidget;
  runApp(const MyApp());
}

class MyApp extends StatefulWidget {
  const MyApp({super.key});

  @override
  State<MyApp> createState() => _MyAppState();
}

class _MyAppState extends State<MyApp> {
  final GlobalKey<NavigatorState> _navigatorKey = GlobalKey<NavigatorState>();
  late final AuthViewModel _authViewModel;
  bool _initialSessionHandled = false;
  String? _lastAuthenticatedUserId;

  @override
  void initState() {
    super.initState();
    final gateway = const ApiGatewayConfig();
    final client = MarineApiClient(client: http.Client(), gateway: gateway);
    final repository = MarineRepository(client: client);
    final service = MarineService(repository: repository);

    _authViewModel = AuthViewModel(
      repository: AuthRepository(apiService: AuthApiService()),
    )..addListener(_handleSessionChange);

    WidgetsBinding.instance.addPostFrameCallback((_) {
      _authViewModel.restore();
      _navigationFor(_authViewModel.user);
    });
  }

  @override
  void dispose() {
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

    if (marineRouteHrefsFor(user).contains(MarineRoutes.conditions)) {
      _replaceNavigationWith(MarineRoutes.conditions);
    } else if (marineRouteHrefsFor(user).contains(MarineRoutes.history)) {
      _replaceNavigationWith(MarineRoutes.history);
    } else {
      _replaceNavigationWith('/dashboard');
    }
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
        '/marine/conditions': (_) => _marineShell(
          route: MarineRoutes.conditions,
          child: SuitabilityScreen(service: _buildMarineService()),
        ),
        '/marine/history': (_) => _marineShell(
          route: MarineRoutes.history,
          child: MarineHistoryScreen(service: _buildMarineService()),
        ),
        '/marine/safety-profiles': (_) => _marineShell(
          route: MarineRoutes.safetyProfiles,
          child: SafetyProfilesScreen(
            service: _buildMarineService(),
            canManage:
                _authViewModel.user?.permissions.contains(
                  'marine.profile.manage',
                ) ==
                true,
            currentUserId: _authViewModel.user?.id,
          ),
        ),
      },
      onUnknownRoute: blueverseUnknownRoute,
    );
  }

  MarineService _buildMarineService() {
    final gateway = const ApiGatewayConfig();
    final client = MarineApiClient(client: http.Client(), gateway: gateway);
    final repository = MarineRepository(client: client);
    return MarineService(repository: repository);
  }

  Widget _marineShell({required String route, required Widget child}) {
    final hrefs = marineRouteHrefsFor(_authViewModel.user);
    final mayRead = hasMarineReadAccess(_authViewModel.user);

    if (!mayRead || !hrefs.contains(route)) {
      return const ColoredBox(
        color: Color(0xFFF7F6F0),
        child: Center(
          child: Text(
            'This coastal service needs permission. Ask an administrator to review your role assignments if you need access.',
            textAlign: TextAlign.center,
          ),
        ),
      );
    }

    return Scaffold(
      appBar: AppBar(title: _appBarTitle(route)),
      body: child,
      bottomNavigationBar: BottomNavigationBar(
        currentIndex: _selectedIndex(route),
        onTap: _onTabSelected,
        type: BottomNavigationBarType.fixed,
        items: const [
          BottomNavigationBarItem(
            icon: Icon(Icons.waves_outlined),
            label: 'Conditions',
          ),
          BottomNavigationBarItem(icon: Icon(Icons.history), label: 'History'),
          BottomNavigationBarItem(
            icon: Icon(Icons.shield_outlined),
            label: 'Safety profiles',
          ),
        ],
      ),
    );
  }

  Widget _appBarTitle(String route) {
    Widget title = const Text('Marine');
    switch (route) {
      case MarineRoutes.conditions:
        title = const Text('Marine conditions');
        break;
      case MarineRoutes.history:
        title = const Text('Condition history');
        break;
      case MarineRoutes.safetyProfiles:
        title = const Text('Safety profiles');
        break;
    }
    return title;
  }

  int _selectedIndex(String route) {
    if (route == MarineRoutes.conditions) return 0;
    if (route == MarineRoutes.history) return 1;
    if (route == MarineRoutes.safetyProfiles) return 2;
    return 0;
  }

  void _onTabSelected(int index) {
    if (index == 0) {
      _replaceNavigationWith(MarineRoutes.conditions);
    } else if (index == 1) {
      _replaceNavigationWith(MarineRoutes.history);
    } else {
      _replaceNavigationWith(MarineRoutes.safetyProfiles);
    }
  }
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
