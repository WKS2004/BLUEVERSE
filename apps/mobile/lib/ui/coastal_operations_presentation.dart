import 'package:flutter/material.dart';

import 'blueverse_theme.dart';

/// Shared presentation for the three Coastal Operations workspaces.
/// Domain state, permissions and requests stay in the owning screens.
class CoastalOperationsHero extends StatelessWidget {
  const CoastalOperationsHero({
    required this.assessments,
    this.logs = false,
    this.count,
    super.key,
  });

  final bool assessments, logs;
  final int? count;

  @override
  Widget build(BuildContext context) {
    final label = logs
        ? (assessments ? 'ASSESSMENT LOGS' : 'ALERT LOGS')
        : (assessments ? 'COASTAL ASSESSMENTS' : 'COASTAL ALERTS');
    final title = logs
        ? 'Keep the story of coastal care close.'
        : assessments
        ? 'Look after the places we share.'
        : 'Clear updates. Safer coastal days.';
    final description = logs
        ? 'Follow retained drafts, published records and inactive records through their recorded activity.'
        : assessments
        ? 'Bring observations and evidence together, publish a coastal review and follow its progress.'
        : 'Prepare a clear notice, choose its audience and follow it through publication and resolution.';
    return Card(
      margin: EdgeInsets.zero,
      color: BlueversePalette.coastPearl,
      clipBehavior: Clip.antiAlias,
      shape: RoundedRectangleBorder(
        borderRadius: BorderRadius.circular(26),
        side: const BorderSide(color: BlueversePalette.coastLine),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          SizedBox(
            height: MediaQuery.sizeOf(context).width < 600 ? 180 : 230,
            child: Stack(
              fit: StackFit.expand,
              children: [
                Image.asset(
                  assessments
                      ? 'assets/coastal/onboarding/assessment-hero.png'
                      : 'assets/coastal/onboarding/alerts-hero.png',
                  semanticLabel: assessments
                      ? 'Coastal field workers inspecting a beach access path'
                      : 'A coastal steward guiding visitors toward a safe path',
                  fit: BoxFit.cover,
                  errorBuilder: (_, _, _) => const ColoredBox(
                    color: BlueversePalette.coastGlass,
                    child: Icon(
                      Icons.waves,
                      color: BlueversePalette.coastDeep,
                      size: 72,
                    ),
                  ),
                ),
                Positioned(
                  left: 16,
                  bottom: 16,
                  right: 16,
                  child: Align(
                    alignment: Alignment.centerLeft,
                    child: Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 12,
                        vertical: 7,
                      ),
                      decoration: BoxDecoration(
                        color: BlueversePalette.coastPearl.withValues(
                          alpha: .94,
                        ),
                        borderRadius: BorderRadius.circular(20),
                      ),
                      child: const Text(
                        'BLUEVERSE · COASTAL CARE',
                        style: TextStyle(
                          fontSize: 10,
                          fontWeight: FontWeight.w800,
                          letterSpacing: 1,
                          color: BlueversePalette.coastDeep,
                        ),
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
          Padding(
            padding: const EdgeInsets.all(22),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  label,
                  style: const TextStyle(
                    fontSize: 10,
                    letterSpacing: 1.5,
                    fontWeight: FontWeight.w800,
                    color: BlueversePalette.coastBlue,
                  ),
                ),
                const SizedBox(height: 14),
                Semantics(
                  header: true,
                  child: Text(
                    title,
                    style: Theme.of(context).textTheme.headlineSmall?.copyWith(
                      color: BlueversePalette.coastInk,
                      fontWeight: FontWeight.w700,
                      height: 1.2,
                    ),
                  ),
                ),
                const SizedBox(height: 10),
                Text(
                  description,
                  style: const TextStyle(
                    color: BlueversePalette.coastMuted,
                    height: 1.5,
                  ),
                ),
                const Divider(height: 32, color: BlueversePalette.coastLine),
                Wrap(
                  spacing: 8,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    const Icon(
                      Icons.circle,
                      size: 8,
                      color: BlueversePalette.coastTeal,
                    ),
                    Text(
                      count == null
                          ? 'Your coastal workspace'
                          : '$count ${assessments ? 'assessment' : 'alert'}${count == 1 ? '' : 's'} on this page',
                      style: const TextStyle(
                        color: BlueversePalette.coastDeep,
                        fontSize: 12,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class CoastalOperationsIntroduction extends StatelessWidget {
  const CoastalOperationsIntroduction({
    required this.title,
    required this.description,
    super.key,
  });
  final String title, description;
  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(bottom: 24),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Text(
          'COASTAL OPERATIONS',
          style: TextStyle(
            fontSize: 10,
            letterSpacing: 1.6,
            fontWeight: FontWeight.w800,
            color: BlueversePalette.coastBlue,
          ),
        ),
        const SizedBox(height: 8),
        Semantics(
          header: true,
          child: Text(
            title,
            style: Theme.of(context).textTheme.headlineMedium?.copyWith(
              fontWeight: FontWeight.w700,
              color: BlueversePalette.coastInk,
              height: 1.15,
            ),
          ),
        ),
        const SizedBox(height: 10),
        Text(
          description,
          style: const TextStyle(
            color: BlueversePalette.coastMuted,
            height: 1.5,
          ),
        ),
      ],
    ),
  );
}

/// Keeps section tabs outside the scrolling and animated content.
class CoastalOperationsNavigation extends StatelessWidget {
  const CoastalOperationsNavigation({required this.children, super.key});
  final List<Widget> children;
  @override
  Widget build(BuildContext context) => DecoratedBox(
    decoration: const BoxDecoration(
      border: Border(bottom: BorderSide(color: BlueversePalette.coastLine)),
    ),
    child: SizedBox(
      width: double.infinity,
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
        child: TextButtonTheme(
          data: TextButtonThemeData(
            style: ButtonStyle(
              textStyle: WidgetStatePropertyAll(
                Theme.of(context).textTheme.labelLarge,
              ),
              foregroundColor: WidgetStateProperty.resolveWith(
                (states) => states.contains(WidgetState.disabled)
                    ? BlueversePalette.coastDeep
                    : BlueversePalette.coastMuted,
              ),
              backgroundColor: WidgetStateProperty.resolveWith(
                (states) => states.contains(WidgetState.disabled)
                    ? BlueversePalette.coastSand
                    : Colors.transparent,
              ),
            ),
          ),
          child: Row(
            children: children
                .map(
                  (child) => Semantics(
                    selected: child is TextButton && child.onPressed == null,
                    child: child,
                  ),
                )
                .toList(),
          ),
        ),
      ),
    ),
  );
}

/// Animates only the content, once per route/category entry. Never replays on
/// search or refresh. Platform accessibility can disable motion immediately.
class CoastalOperationsEntrance extends StatefulWidget {
  const CoastalOperationsEntrance({
    required this.forward,
    required this.child,
    super.key,
  });
  final bool forward;
  final Widget child;
  @override
  State<CoastalOperationsEntrance> createState() => _EntranceState();
}

class _EntranceState extends State<CoastalOperationsEntrance>
    with SingleTickerProviderStateMixin {
  late final AnimationController _controller = AnimationController(
    vsync: this,
    duration: const Duration(milliseconds: 420),
  );
  late final Animation<double> _curve = CurvedAnimation(
    parent: _controller,
    curve: Curves.easeOutCubic,
  );
  bool _started = false;
  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    if (MediaQuery.disableAnimationsOf(context)) {
      _controller.value = 1;
      _started = true;
    } else if (!_started) {
      _started = true;
      _controller.forward();
    }
  }

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => ClipRect(
    child: FadeTransition(
      opacity: _curve,
      child: SlideTransition(
        position: Tween<Offset>(
          begin: Offset(widget.forward ? .06 : -.06, 0),
          end: Offset.zero,
        ).animate(_curve),
        child: widget.child,
      ),
    ),
  );
}

class CoastalOperationsFormSection extends StatelessWidget {
  const CoastalOperationsFormSection({required this.title, super.key});
  final String title;
  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(top: 24, bottom: 6),
    child: Semantics(
      header: true,
      child: Text(
        title,
        style: Theme.of(context).textTheme.titleMedium?.copyWith(
          fontWeight: FontWeight.w700,
          color: BlueversePalette.coastDeep,
        ),
      ),
    ),
  );
}
