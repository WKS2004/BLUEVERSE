import 'package:flutter/material.dart';

import '../data/models/auth_models.dart';
import 'blueverse_theme.dart';

bool plannerGranted(AuthUser? user, String permission) =>
    user?.permissions.contains(permission) == true;

class PlannerNavigationMenu extends StatelessWidget {
  const PlannerNavigationMenu({super.key, this.user});
  final AuthUser? user;
  @override
  Widget build(BuildContext context) => PopupMenuButton<String>(
    tooltip: 'Coastal planner',
    icon: const Icon(Icons.explore_outlined),
    onSelected: (path) => Navigator.pushNamed(context, path),
    itemBuilder: (_) => [
      const PopupMenuItem(value: '/planner', child: Text('The planner')),
      if (plannerGranted(user, 'planner.recommendations.create'))
        const PopupMenuItem(
          value: '/planner/plan',
          child: Text('Create a plan'),
        ),
      if (plannerGranted(user, 'planner.itineraries.manage'))
        const PopupMenuItem(
          value: '/planner/saved',
          child: Text('Plan history'),
        ),
    ],
  );
}

class PlannerAccess extends StatelessWidget {
  const PlannerAccess({
    super.key,
    required this.user,
    required this.route,
    this.arguments,
  });
  final AuthUser? user;
  final String route;
  final Object? arguments;
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(
      title: const Text('Coastal planner'),
      actions: [PlannerNavigationMenu(user: user)],
    ),
    body: Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(
              Icons.waves,
              size: 48,
              color: BlueversePalette.coastTeal,
            ),
            const SizedBox(height: 20),
            Text(
              user == null
                  ? 'Sign in to plan with the coast.'
                  : 'Coastal planning access',
              style: Theme.of(context).textTheme.headlineSmall,
            ),
            const SizedBox(height: 12),
            Text(
              user == null
                  ? 'Keep your plans in one place and return whenever you need them.'
                  : 'Ask an administrator for the planning access your account needs.',
            ),
            const SizedBox(height: 20),
            if (user == null)
              FilledButton(
                onPressed: () => Navigator.pushNamed(
                  context,
                  '/signin',
                  arguments: {'returnTo': route, 'returnArguments': arguments},
                ),
                child: const Text('Sign in and continue'),
              ),
            TextButton(
              onPressed: () => Navigator.pushNamed(context, '/planner'),
              child: const Text('Explore the planner'),
            ),
          ],
        ),
      ),
    ),
  );
}

class PlannerSectionHero extends StatelessWidget {
  const PlannerSectionHero({
    super.key,
    required this.history,
    required this.user,
  });
  final bool history;
  final AuthUser? user;
  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      Wrap(
        spacing: 10,
        runSpacing: 8,
        children: [
          if (plannerGranted(user, 'planner.recommendations.create'))
            _tab(context, false, 'Create a plan', '/planner/plan'),
          if (plannerGranted(user, 'planner.itineraries.manage'))
            _tab(context, true, 'Plan history', '/planner/saved'),
        ],
      ),
      const Divider(height: 32),
      const Text(
        'YOUR COASTAL PLANNER',
        style: TextStyle(
          fontSize: 10,
          fontWeight: FontWeight.w800,
          letterSpacing: 1.8,
          color: BlueversePalette.coastBlue,
        ),
      ),
      const SizedBox(height: 10),
      Text(
        history ? 'Your plan history.' : 'Create your coastal plan.',
        style: Theme.of(context).textTheme.headlineMedium
            ?.copyWith(fontWeight: FontWeight.w700),
      ),
      const SizedBox(height: 10),
      Text(
        history
            ? 'Revisit saved drafts, confirmed plans and days by the coast.'
            : 'Choose your coast and shape a day at your own pace.',
      ),
      const SizedBox(height: 24),
      Card(
        margin: EdgeInsets.zero,
        clipBehavior: Clip.antiAlias,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Image.asset(
              history
                  ? 'assets/coastal/planner/plan-memories.webp'
                  : 'assets/coastal/planner/trip-preparation.webp',
              height: 200,
              width: double.infinity,
              fit: BoxFit.cover,
              semanticLabel: history
                  ? 'A coastal travel journal'
                  : 'A day bag and map ready for the coast',
            ),
            Padding(
              padding: const EdgeInsets.all(24),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    history ? 'GOOD DAYS, KEPT CLOSE' : 'A DAY WORTH PLANNING',
                    style: const TextStyle(
                      fontSize: 10,
                      letterSpacing: 1.5,
                      color: BlueversePalette.coastTeal,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  const SizedBox(height: 12),
                  Text(
                    history
                        ? 'Your coast. Your plans. Your memories.'
                        : 'Make room for a great coastal day.',
                    style: Theme.of(context).textTheme.headlineSmall,
                  ),
                  const SizedBox(height: 12),
                  Text(
                    history
                        ? 'Open a plan to shape its details or check current conditions before you go.'
                        : 'Explore current suggestions, save a private draft and arrange the details before confirming.',
                  ),
                  const Divider(height: 32),
                  Text(
                    history
                        ? 'Private drafts · Confirmed plans · Past coastal days'
                        : 'Your interests · Current conditions · Your own pace',
                    style: const TextStyle(
                      fontSize: 12,
                      color: BlueversePalette.coastBlue,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
      const SizedBox(height: 24),
    ],
  );
  Widget _tab(
    BuildContext context,
    bool targetHistory,
    String label,
    String route,
  ) => Semantics(
    selected: history == targetHistory,
    child: history == targetHistory
        ? FilledButton(onPressed: () {}, child: Text(label))
        : OutlinedButton(
            onPressed: () => Navigator.pushReplacementNamed(context, route),
            child: Text(label),
          ),
  );
}
