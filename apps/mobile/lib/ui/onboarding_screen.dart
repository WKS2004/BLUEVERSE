import 'package:flutter/material.dart';

import 'blueverse_theme.dart';

class BlueverseOnboardingScreen extends StatefulWidget {
  const BlueverseOnboardingScreen({super.key});

  @override
  State<BlueverseOnboardingScreen> createState() =>
      _BlueverseOnboardingScreenState();
}

class _BlueverseOnboardingScreenState extends State<BlueverseOnboardingScreen> {
  static const _slides = [
    _OnboardingSlide(
      image: 'assets/coastal/onboarding/coast-hero-daylight.webp',
      eyebrow: 'THE COAST, MORE CONNECTED',
      title: 'Closer to the coast.',
      description: 'Meet the places, people and living waters that make every shoreline worth discovering.',
    ),
    _OnboardingSlide(
      image: 'assets/coastal/onboarding/coastal-livelihood.webp',
      eyebrow: 'MADE OF MANY STORIES',
      title: 'Places made personal.',
      description: 'Find experiences shaped by local knowledge, coastal culture and the people who call this place home.',
    ),
    _OnboardingSlide(
      image: 'assets/coastal/onboarding/mangrove-lagoon.jpg',
      eyebrow: 'CARE THAT TRAVELS WITH YOU',
      title: 'Stay in tune with the sea.',
      description: 'Explore with a deeper understanding of marine life and the changing environments along the shore.',
    ),
    _OnboardingSlide(
      image: 'assets/coastal/onboarding/coastal-walk.jpg',
      eyebrow: 'YOUR COASTAL STORY STARTS HERE',
      title: 'Find your place by the sea.',
      description: 'Join BLUEVERSE to discover coastal experiences and stay connected to the communities around them.',
    ),
  ];

  late final PageController _pageController;
  int _currentPage = 0;

  int get _lastPage => _slides.length - 1;
  bool get _isWelcomePage => _currentPage == _lastPage;

  @override
  void initState() {
    super.initState();
    _pageController = PageController();
  }

  @override
  void dispose() {
    _pageController.dispose();
    super.dispose();
  }

  void _goToPage(int page) {
    if (page < 0 || page > _lastPage) return;
    final reduceMotion = MediaQuery.of(context).disableAnimations;
    if (reduceMotion) {
      _pageController.jumpToPage(page);
      return;
    }
    _pageController.animateToPage(
      page,
      duration: const Duration(milliseconds: 460),
      curve: Curves.easeOutCubic,
    );
  }

  @override
  Widget build(BuildContext context) {
    final viewport = MediaQuery.sizeOf(context);
    final compactHeight = viewport.height < 700;

    return Scaffold(
      backgroundColor: BlueversePalette.coastInk,
      body: Stack(
        fit: StackFit.expand,
        children: [
          PageView.builder(
            controller: _pageController,
            itemCount: _slides.length,
            onPageChanged: (page) => setState(() => _currentPage = page),
            itemBuilder: (context, index) =>
                _OnboardingBackdrop(slide: _slides[index]),
          ),
          SafeArea(
            child: LayoutBuilder(
              builder: (context, constraints) {
                final compactWidth = constraints.maxWidth < 480;
                final slideCopy = Center(
                  child: ConstrainedBox(
                    constraints: const BoxConstraints(maxWidth: 620),
                    child: Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 38),
                      child: _SlideCopy(
                        slide: _slides[_currentPage],
                        compactHeight: compactHeight,
                      ),
                    ),
                  ),
                );
                final bottomNavigation = _BottomNavigation(
                  currentPage: _currentPage,
                  pageCount: _slides.length,
                  showBack: _currentPage > 0,
                  showNext: _currentPage < _lastPage,
                  onBack: () => _goToPage(_currentPage - 1),
                  onNext: () => _goToPage(_currentPage + 1),
                );
                return Padding(
                  padding: const EdgeInsets.fromLTRB(22, 12, 22, 14),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      _TopBar(
                        compactWidth: compactWidth,
                        showSkip: !_isWelcomePage,
                        onSkip: () => _goToPage(_lastPage),
                      ),
                      if (compactHeight)
                        Expanded(
                          child: SingleChildScrollView(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.stretch,
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                const SizedBox(height: 12),
                                slideCopy,
                                const SizedBox(height: 16),
                                if (_isWelcomePage)
                                  _WelcomeActions(compactHeight: true),
                                const SizedBox(height: 12),
                              ],
                            ),
                          ),
                        )
                      else ...[
                        const Spacer(flex: 5),
                        slideCopy,
                        const SizedBox(height: 26),
                        if (_isWelcomePage)
                          _WelcomeActions(compactHeight: false),
                        const Spacer(flex: 2),
                      ],
                      bottomNavigation,
                    ],
                  ),
                );
              },
            ),
          ),
          Positioned(
            left: 8,
            top: viewport.height * 0.46,
            child: _SideArrow(
              icon: Icons.chevron_left_rounded,
              label: 'Previous slide',
              enabled: _currentPage > 0,
              onPressed: () => _goToPage(_currentPage - 1),
            ),
          ),
          Positioned(
            right: 8,
            top: viewport.height * 0.46,
            child: _SideArrow(
              icon: Icons.chevron_right_rounded,
              label: 'Next slide',
              enabled: _currentPage < _lastPage,
              onPressed: () => _goToPage(_currentPage + 1),
            ),
          ),
        ],
      ),
    );
  }
}

class _OnboardingSlide {
  const _OnboardingSlide({
    required this.image,
    required this.eyebrow,
    required this.title,
    required this.description,
  });

  final String image;
  final String eyebrow;
  final String title;
  final String description;
}

class _OnboardingBackdrop extends StatelessWidget {
  const _OnboardingBackdrop({required this.slide});

  final _OnboardingSlide slide;

  @override
  Widget build(BuildContext context) {
    return Stack(
      fit: StackFit.expand,
      children: [
        Image.asset(
          slide.image,
          fit: BoxFit.cover,
          semanticLabel: slide.title,
          errorBuilder: (context, error, stackTrace) => const ColoredBox(
            color: BlueversePalette.coastDeep,
            child: Icon(Icons.waves_rounded, color: Colors.white24, size: 96),
          ),
        ),
        const DecoratedBox(
          decoration: BoxDecoration(
            gradient: LinearGradient(
              begin: Alignment.topCenter,
              end: Alignment.bottomCenter,
              stops: [0, 0.34, 0.68, 1],
              colors: [
                Color(0x70204455),
                Color(0x1A18394C),
                Color(0x8018394C),
                Color(0xF218394C),
              ],
            ),
          ),
        ),
      ],
    );
  }
}

class _TopBar extends StatelessWidget {
  const _TopBar({
    required this.compactWidth,
    required this.showSkip,
    required this.onSkip,
  });

  final bool compactWidth;
  final bool showSkip;
  final VoidCallback onSkip;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Container(
          padding: EdgeInsets.symmetric(
            horizontal: compactWidth ? 3 : 13,
            vertical: 9,
          ),
          decoration: BoxDecoration(
            color: Colors.white.withAlpha(35),
            borderRadius: BorderRadius.circular(30),
            border: Border.all(color: Colors.white.withAlpha(70)),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Icon(Icons.waves_rounded, size: 19, color: Colors.white),
              const SizedBox(width: 6),
              Flexible(
                child: Text(
                  'BLUEVERSE',
                  overflow: TextOverflow.ellipsis,
                  maxLines: 1,
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: 12,
                    fontWeight: FontWeight.w800,
                    letterSpacing: 1.2,
                  ),
                ),
              ),
            ],
          ),
        ),
        if (showSkip && compactWidth)
          TextButton(
            onPressed: onSkip,
            style: TextButton.styleFrom(
              foregroundColor: Colors.white,
              backgroundColor: Colors.white.withAlpha(28),
              minimumSize: const Size(44, 44),
              padding: const EdgeInsets.symmetric(horizontal: 4),
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(24),
                side: BorderSide(color: Colors.white.withAlpha(60)),
              ),
            ),
            child: const Text('Skip'),
          )
        else if (showSkip)
          TextButton.icon(
            onPressed: onSkip,
            style: TextButton.styleFrom(
              foregroundColor: Colors.white,
              backgroundColor: Colors.white.withAlpha(28),
              minimumSize: const Size(44, 44),
              padding: const EdgeInsets.symmetric(horizontal: 14),
              shape: RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(24),
                side: BorderSide(color: Colors.white.withAlpha(60)),
              ),
            ),
            icon: const Icon(Icons.fast_forward_rounded, size: 17),
            label: const Text('Skip'),
          )
        else
          const SizedBox(width: 72, height: 44),
      ],
    );
  }
}

class _SlideCopy extends StatelessWidget {
  const _SlideCopy({required this.slide, required this.compactHeight});

  final _OnboardingSlide slide;
  final bool compactHeight;

  @override
  Widget build(BuildContext context) {
    final textTheme = Theme.of(context).textTheme;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      mainAxisSize: MainAxisSize.min,
      children: [
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 11, vertical: 7),
          decoration: BoxDecoration(
            color: BlueversePalette.coastGlass.withAlpha(52),
            borderRadius: BorderRadius.circular(20),
            border: Border.all(color: Colors.white.withAlpha(72)),
          ),
          child: Text(
            slide.eyebrow,
            style: const TextStyle(
              color: Color(0xFFE4F4F4),
              fontSize: 10,
              fontWeight: FontWeight.w800,
              letterSpacing: 1.35,
            ),
          ),
        ),
        const SizedBox(height: 15),
        Text(
          slide.title,
          style: textTheme.displaySmall?.copyWith(
            color: Colors.white,
            fontSize: compactHeight ? 34 : 39,
            fontWeight: FontWeight.w800,
            height: 1.08,
            letterSpacing: -0.8,
            shadows: const [
              Shadow(
                color: Color(0x66204455),
                blurRadius: 20,
                offset: Offset(0, 3),
              ),
            ],
          ),
        ),
        const SizedBox(height: 13),
        Text(
          slide.description,
          style: textTheme.bodyLarge?.copyWith(
            color: const Color(0xFFE8F2F2),
            fontSize: 15.5,
            height: 1.5,
            fontWeight: FontWeight.w500,
          ),
        ),
      ],
    );
  }
}

class _WelcomeActions extends StatelessWidget {
  const _WelcomeActions({required this.compactHeight});

  final bool compactHeight;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 420),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 38),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              FilledButton.icon(
                onPressed: () => Navigator.of(context).pushNamed('/signup'),
                style: FilledButton.styleFrom(
                  backgroundColor: BlueversePalette.coastGlass,
                  foregroundColor: BlueversePalette.coastInk,
                  minimumSize: Size.fromHeight(compactHeight ? 48 : 54),
                ),
                icon: const Icon(Icons.explore_outlined),
                label: const Text('Create your account'),
              ),
              const SizedBox(height: 10),
              OutlinedButton(
                onPressed: () => Navigator.of(context).pushNamed('/signin'),
                style: OutlinedButton.styleFrom(
                  foregroundColor: Colors.white,
                  side: BorderSide(color: Colors.white.withAlpha(170)),
                  minimumSize: Size.fromHeight(compactHeight ? 46 : 52),
                  backgroundColor: Colors.white.withAlpha(16),
                ),
                child: const Text('I already have an account · Sign in'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _BottomNavigation extends StatelessWidget {
  const _BottomNavigation({
    required this.currentPage,
    required this.pageCount,
    required this.showBack,
    required this.showNext,
    required this.onBack,
    required this.onNext,
  });

  final int currentPage;
  final int pageCount;
  final bool showBack;
  final bool showNext;
  final VoidCallback onBack;
  final VoidCallback onNext;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        if (showBack)
          TextButton.icon(
            onPressed: onBack,
            style: TextButton.styleFrom(
              foregroundColor: Colors.white,
              minimumSize: const Size(82, 46),
              padding: const EdgeInsets.symmetric(horizontal: 8),
            ),
            icon: const Icon(Icons.arrow_back_rounded, size: 18),
            label: const Text('Back'),
          )
        else
          const SizedBox(width: 82, height: 46),
        Expanded(
          child: Semantics(
            liveRegion: true,
            label: 'Slide ${currentPage + 1} of $pageCount',
            child: Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: List.generate(pageCount, (index) {
                final isCurrent = index == currentPage;
                return AnimatedContainer(
                  duration: MediaQuery.of(context).disableAnimations
                      ? Duration.zero
                      : const Duration(milliseconds: 220),
                  curve: Curves.easeOut,
                  width: isCurrent ? 23 : 7,
                  height: 7,
                  margin: const EdgeInsets.symmetric(horizontal: 4),
                  decoration: BoxDecoration(
                    color: isCurrent
                        ? BlueversePalette.coastGlass
                        : Colors.white.withAlpha(125),
                    borderRadius: BorderRadius.circular(8),
                  ),
                );
              }),
            ),
          ),
        ),
        if (showNext)
          TextButton.icon(
            onPressed: onNext,
            style: TextButton.styleFrom(
              foregroundColor: Colors.white,
              minimumSize: const Size(82, 46),
              padding: const EdgeInsets.symmetric(horizontal: 8),
            ),
            iconAlignment: IconAlignment.end,
            icon: const Icon(Icons.arrow_forward_rounded, size: 18),
            label: const Text('Next'),
          )
        else
          const SizedBox(width: 82, height: 46),
      ],
    );
  }
}

class _SideArrow extends StatelessWidget {
  const _SideArrow({
    required this.icon,
    required this.label,
    required this.enabled,
    required this.onPressed,
  });

  final IconData icon;
  final String label;
  final bool enabled;
  final VoidCallback onPressed;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: 44,
      height: 44,
      decoration: BoxDecoration(
        color: Colors.white.withAlpha(enabled ? 34 : 16),
        shape: BoxShape.circle,
        border: Border.all(color: Colors.white.withAlpha(enabled ? 80 : 42)),
      ),
      child: IconButton(
        onPressed: enabled ? onPressed : null,
        tooltip: label,
        padding: EdgeInsets.zero,
        icon: Icon(icon, size: 30),
        color: Colors.white,
        disabledColor: Colors.white54,
      ),
    );
  }
}
