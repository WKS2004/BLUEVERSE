import 'package:flutter/material.dart';

class PlannerPageMotion extends PageTransitionsBuilder {
  const PlannerPageMotion();
  @override
  Widget buildTransitions<T>(
    PageRoute<T> route,
    BuildContext context,
    Animation<double> animation,
    Animation<double> secondaryAnimation,
    Widget child,
  ) {
    if (route.settings.name != '/planner/plan' &&
        route.settings.name != '/planner/saved') {
      return const FadeUpwardsPageTransitionsBuilder().buildTransitions(
        route,
        context,
        animation,
        secondaryAnimation,
        child,
      );
    }
    if (MediaQuery.disableAnimationsOf(context)) return child;
    final curved = CurvedAnimation(
      parent: animation,
      curve: Curves.easeOutCubic,
    );
    return FadeTransition(
      opacity: curved,
      child: SlideTransition(
        position: Tween<Offset>(
          begin: Offset(
            route.settings.name == '/planner/saved' ? .03 : -.03,
            0,
          ),
          end: Offset.zero,
        ).animate(curved),
        child: child,
      ),
    );
  }
}
