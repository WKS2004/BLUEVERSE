import 'package:flutter/material.dart';

import 'experience_view_model.dart';

import '../feedback/blueverse_loading_screen.dart';
import '../feedback/loading_screen_controller.dart';

class ExperienceLoading extends StatelessWidget {
  const ExperienceLoading({super.key});

  @override
  Widget build(BuildContext context) => Stack(
    children: const [
      BlueverseLoadingScreen(
        message: LoadingScreenMessage.coastal(),
        isExiting: false,
        exitDuration: Duration.zero,
      ),
    ],
  );
}

class ExperienceNotice extends StatelessWidget {
  const ExperienceNotice(
    this.message, {
    super.key,
    this.onRetry,
    this.isError = false,
  });
  final String message;
  final VoidCallback? onRetry;
  final bool isError;

  @override
  Widget build(BuildContext context) => Semantics(
    liveRegion: true,
    child: Card(
      color: isError ? Theme.of(context).colorScheme.errorContainer : null,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(message),
            if (onRetry != null)
              TextButton.icon(
                onPressed: onRetry,
                icon: const Icon(Icons.refresh),
                label: const Text('Try again'),
              ),
          ],
        ),
      ),
    ),
  );
}

class ExperienceHero extends StatelessWidget {
  const ExperienceHero({
    super.key,
    required this.title,
    required this.detail,
    this.image = 'assets/coastal/onboarding/coast-hero-daylight.webp',
  });
  final String title;
  final String detail;
  final String image;

  @override
  Widget build(BuildContext context) => Card(
    clipBehavior: Clip.antiAlias,
    margin: const EdgeInsets.only(bottom: 20),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Image.asset(
          image,
          height: 160,
          fit: BoxFit.cover,
          excludeFromSemantics: true,
        ),
        Padding(
          padding: const EdgeInsets.all(20),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(title, style: Theme.of(context).textTheme.headlineSmall),
              const SizedBox(height: 8),
              Text(detail, style: Theme.of(context).textTheme.bodyMedium),
            ],
          ),
        ),
      ],
    ),
  );
}

/// Keep the native map and scroll position alive while neighbouring tabs move.
class ExperienceTabBody extends StatefulWidget {
  const ExperienceTabBody({super.key, required this.child});
  final Widget child;
  @override
  State<ExperienceTabBody> createState() => _ExperienceTabBodyState();
}

class _ExperienceTabBodyState extends State<ExperienceTabBody>
    with AutomaticKeepAliveClientMixin {
  @override
  bool get wantKeepAlive => true;
  @override
  Widget build(BuildContext context) {
    super.build(context);
    return widget.child;
  }
}

/// Text fields remain alive through a dialog's closing animation. Release their
/// controllers when the dialog widget unmounts, rather than when pop resolves.
class ExperienceFormLifecycle extends StatefulWidget {
  const ExperienceFormLifecycle({
    super.key,
    required this.controllers,
    required this.viewModel,
    required this.child,
  });
  final List<TextEditingController> controllers;
  final ExperienceViewModel viewModel;
  final Widget child;
  @override
  State<ExperienceFormLifecycle> createState() =>
      _ExperienceFormLifecycleState();
}

class _ExperienceFormLifecycleState extends State<ExperienceFormLifecycle> {
  @override
  void dispose() {
    for (final controller in widget.controllers) {
      controller.dispose();
    }
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: widget.viewModel,
    builder: (context, _) => PopScope(
      canPop: !widget.viewModel.isPerformingCatalogueAction,
      child: widget.child,
    ),
  );
}
