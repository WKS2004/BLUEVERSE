import 'package:flutter/material.dart';

import 'data/repositories/auth_repository.dart';
import 'data/repositories/experience_repository.dart';
import 'data/services/auth_api_service.dart';
import 'data/services/experience_api_service.dart';
import 'ui/account_screens.dart';
import 'ui/auth_admin_screen.dart';
import 'ui/auth_registration_screen.dart';
import 'ui/auth_screens.dart';
import 'ui/auth_view_model.dart';
import 'ui/blueverse_theme.dart';
import 'ui/experiences/catalogue_management_screen.dart';
import 'ui/experiences/destination_detail_screen.dart';
import 'ui/experiences/experience_view_model.dart';
import 'ui/experiences/experiences_discovery_screen.dart';
import 'ui/experiences/favourites_screen.dart';
import 'ui/experiences/offering_detail_screen.dart';
import 'ui/feedback/blueverse_error_screen.dart';
import 'ui/feedback/blueverse_loading_screen.dart';
import 'ui/feedback/loading_screen_controller.dart';
import 'ui/onboarding_screen.dart';

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
  late final ExperienceViewModel _experienceViewModel;
  bool _initialSessionHandled = false;
  String? _lastAuthenticatedUserId;

  @override
  void initState() {
    super.initState();
    _authViewModel = AuthViewModel(
      repository: AuthRepository(apiService: AuthApiService()),
    )..addListener(_handleSessionChange);
    _experienceViewModel = ExperienceViewModel(
      repository: ExperienceRepository(apiService: ExperienceApiService()),
    );
    WidgetsBinding.instance.addPostFrameCallback((_) {
      _authViewModel.restore();
    });
  }

  @override
  void dispose() {
    _authViewModel
      ..removeListener(_handleSessionChange)
      ..dispose();
    _experienceViewModel.dispose();
    super.dispose();
  }

  void _handleSessionChange() {
    if (!_authViewModel.hasRestoredSession) return;

    final activeUserId = _authViewModel.user?.id;
    if (!_initialSessionHandled) {
      _initialSessionHandled = true;
      _lastAuthenticatedUserId = activeUserId;
      if (activeUserId != null) _replaceNavigationWith('/dashboard');
      return;
    }

    final wasAuthenticated = _lastAuthenticatedUserId != null;
    _lastAuthenticatedUserId = activeUserId;
    if (wasAuthenticated && activeUserId == null) {
      _replaceNavigationWith('/');
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
      home: MobileLaunchPage(viewModel: _authViewModel),
      routes: {
        '/signin': (_) => AuthLoginScreen(viewModel: _authViewModel),
        '/signup': (_) => AuthRegistrationScreen(viewModel: _authViewModel),
        '/profile': (_) => AuthProfileScreen(viewModel: _authViewModel),
        '/dashboard': (_) => AuthDashboardScreen(
              viewModel: _authViewModel,
              experienceViewModel: _experienceViewModel,
            ),
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
        '/experiences': (_) => ExperiencesDiscoveryScreen(viewModel: _experienceViewModel),
        '/experiences/destinations/:id': (_) => ExperiencesDiscoveryScreen(viewModel: _experienceViewModel),
        '/experiences/offerings/:id': (_) => ExperiencesDiscoveryScreen(viewModel: _experienceViewModel),
        '/experiences/favourites': (_) => FavouritesScreen(viewModel: _experienceViewModel),
        '/experiences/manage': (_) => CatalogueManagementScreen(
          viewModel: _experienceViewModel,
          authViewModel: _authViewModel,
        ),
        '/404': (_) => const BlueverseErrorScreen.notFound(),
        '/500': (_) => const BlueverseErrorScreen.server(),
      },
      onGenerateRoute: (settings) {
        final uri = Uri.tryParse(settings.name ?? '');
        if (uri != null) {
          final segments = uri.pathSegments;
          if (segments.length == 3 && segments[0] == 'experiences' && segments[1] == 'destinations') {
            return MaterialPageRoute(
              settings: settings,
              builder: (_) => DestinationDetailScreen(
                destinationId: segments[2],
                viewModel: _experienceViewModel,
              ),
            );
          }
          if (segments.length == 3 && segments[0] == 'experiences' && segments[1] == 'offerings') {
            return MaterialPageRoute(
              settings: settings,
              builder: (_) => OfferingDetailScreen(
                offeringId: segments[2],
                viewModel: _experienceViewModel,
              ),
            );
          }
        }
        return null;
      },
      onUnknownRoute: blueverseUnknownRoute,
    );
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
