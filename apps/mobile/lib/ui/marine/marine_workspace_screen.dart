import 'package:flutter/material.dart';

import '../../data/services/marine_service.dart';
import '../blueverse_theme.dart';
import 'history_screen.dart';
import 'marine_presentation.dart';
import 'safety_profiles_screen.dart';
import 'suitability_screen.dart';

/// One native shell; navigation remains visible while its workflow scrolls.
class MarineWorkspaceScreen extends StatefulWidget {
  const MarineWorkspaceScreen({
    super.key,
    required this.service,
    required this.initialIndex,
    required this.canManage,
    this.currentUserId,
    this.actions = const [],
  });
  final MarineService service;
  final int initialIndex;
  final bool canManage;
  final String? currentUserId;
  final List<Widget> actions;

  @override
  State<MarineWorkspaceScreen> createState() => _MarineWorkspaceScreenState();
}

class _MarineWorkspaceScreenState extends State<MarineWorkspaceScreen> {
  late int _index = widget.initialIndex;
  @override
  Widget build(BuildContext context) {
    final reduced = MediaQuery.disableAnimationsOf(context);
    final page = switch (_index) {
      0 => SuitabilityScreen(service: widget.service, embedded: true),
      1 => MarineHistoryScreen(service: widget.service, embedded: true),
      _ => SafetyProfilesScreen(
        service: widget.service,
        embedded: true,
        canManage: widget.canManage,
        currentUserId: widget.currentUserId,
      ),
    };
    return Scaffold(
      appBar: AppBar(
        title: const Text('Marine & safety'),
        actions: widget.actions,
      ),
      body: SafeArea(
        top: false,
        child: Column(
          children: [
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
              child: Text(
                'Read the coast, follow the evidence and review activity limits.',
                style: Theme.of(context).textTheme.bodyMedium,
              ),
            ),
            SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Padding(
                padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
                child: Row(
                  children: [
                    for (var i = 0; i < marineLabels.length; i++)
                      Padding(
                        padding: const EdgeInsets.only(right: 8),
                        child: Semantics(
                          selected: _index == i,
                          button: true,
                          child: TextButton(
                            style: TextButton.styleFrom(
                              minimumSize: const Size(48, 48),
                              backgroundColor: _index == i
                                  ? BlueversePalette.coastDeep
                                  : BlueversePalette.coastPearl,
                              foregroundColor: _index == i
                                  ? Colors.white
                                  : BlueversePalette.coastDeep,
                              padding: const EdgeInsets.symmetric(
                                horizontal: 18,
                              ),
                              shape: const StadiumBorder(),
                            ),
                            onPressed: () {
                              if (_index != i) {
                                FocusManager.instance.primaryFocus?.unfocus();
                                setState(() => _index = i);
                              }
                            },
                            child: Text(marineLabels[i]),
                          ),
                        ),
                      ),
                  ],
                ),
              ),
            ),
            Expanded(
              child: AnimatedSwitcher(
                duration: reduced
                    ? Duration.zero
                    : const Duration(milliseconds: 260),
                switchInCurve: Curves.easeOut,
                layoutBuilder: (current, previous) =>
                    current ?? const SizedBox.shrink(),
                transitionBuilder: (child, animation) => FadeTransition(
                  opacity: animation,
                  child: SlideTransition(
                    position: Tween(
                      begin: const Offset(0, .015),
                      end: Offset.zero,
                    ).animate(animation),
                    child: child,
                  ),
                ),
                child: KeyedSubtree(key: ValueKey(_index), child: page),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
