import 'dart:math' as math;

import 'package:flutter/material.dart';

import 'loading_screen_controller.dart';

class BlueverseLoadingScreen extends StatefulWidget {
  const BlueverseLoadingScreen({
    required this.message,
    required this.isExiting,
    required this.exitDuration,
    super.key,
  });

  final LoadingScreenMessage message;
  final bool isExiting;
  final Duration exitDuration;

  @override
  State<BlueverseLoadingScreen> createState() => _BlueverseLoadingScreenState();
}

class _BlueverseLoadingScreenState extends State<BlueverseLoadingScreen>
    with TickerProviderStateMixin {
  late final AnimationController _motion;
  late final AnimationController _wash;

  @override
  void initState() {
    super.initState();
    _motion = AnimationController(
      vsync: this,
      duration: const Duration(milliseconds: 2800),
    );
    _wash = AnimationController(vsync: this, duration: widget.exitDuration);
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    _syncMotion();
  }

  @override
  void didUpdateWidget(covariant BlueverseLoadingScreen oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.isExiting && !oldWidget.isExiting) {
      final reduceMotion =
          MediaQuery.maybeOf(context)?.disableAnimations ?? false;
      _wash.duration = reduceMotion
          ? const Duration(milliseconds: 180)
          : widget.exitDuration;
      _wash.forward(from: 0);
    } else if (!widget.isExiting && oldWidget.isExiting) {
      _wash.reset();
    }
    _syncMotion();
  }

  void _syncMotion() {
    final reduceMotion =
        MediaQuery.maybeOf(context)?.disableAnimations ?? false;
    if (reduceMotion || widget.isExiting) {
      if (_motion.isAnimating) _motion.stop();
      if (reduceMotion) _motion.value = 0.5;
    } else if (!_motion.isAnimating) {
      _motion.repeat();
    }
  }

  @override
  void dispose() {
    _motion.dispose();
    _wash.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final reducedMotion =
        MediaQuery.maybeOf(context)?.disableAnimations ?? false;

    return Positioned.fill(
      child: AnimatedBuilder(
        animation: _wash,
        builder: (context, child) {
          final progress = widget.isExiting ? _wash.value : 0.0;
          Widget revealed = child!;
          if (widget.isExiting && reducedMotion) {
            revealed = Opacity(opacity: 1 - progress, child: revealed);
          } else if (widget.isExiting) {
            revealed = ClipPath(
              key: const ValueKey('blueverse-loading-wave-exit'),
              clipper: _WaveWashClipper(progress: progress),
              child: revealed,
            );
          }

          return IgnorePointer(
            ignoring: widget.isExiting && progress >= 1,
            child: Semantics(
              hidden: widget.isExiting,
              label: '${widget.message.title}. ${widget.message.detail}',
              liveRegion: true,
              child: revealed,
            ),
          );
        },
        child: ColoredBox(
          color: widget.message.isAuthentication
              ? const Color(0xF5F0F7F8)
              : const Color(0xEEF7F6F0),
          child: Center(
            child: SingleChildScrollView(
              padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 32),
              child: AnimatedBuilder(
                animation: _motion,
                builder: (context, _) {
                  final progress = reducedMotion ? 0.5 : _motion.value;
                  final pulse = reducedMotion
                      ? 1.0
                      : 0.96 + (0.04 * math.sin(progress * math.pi * 2));
                  return Column(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Transform.scale(
                        scale: pulse,
                        child: CustomPaint(
                          key: const ValueKey('blueverse-loading-mark'),
                          size: const Size.square(164),
                          painter: _CoastalLoadingMarkPainter(
                            progress: progress,
                            reducedMotion: reducedMotion,
                          ),
                        ),
                      ),
                      if (widget.message.isAuthentication) ...[
                        const SizedBox(height: 18),
                        Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 13,
                            vertical: 9,
                          ),
                          decoration: BoxDecoration(
                            color: Colors.white.withValues(alpha: 0.78),
                            border: Border.all(color: const Color(0xFFC9E0E6)),
                            borderRadius: BorderRadius.circular(99),
                          ),
                          child: const Row(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              Icon(
                                Icons.verified_user_outlined,
                                color: Color(0xFF43858A),
                                size: 16,
                              ),
                              SizedBox(width: 7),
                              Text(
                                'SECURE ACCOUNT TRANSITION',
                                style: TextStyle(
                                  color: Color(0xFF205C79),
                                  fontSize: 10,
                                  fontWeight: FontWeight.w800,
                                  letterSpacing: 1.15,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ],
                      const SizedBox(height: 18),
                      Text(
                        widget.message.title,
                        textAlign: TextAlign.center,
                        style: const TextStyle(
                          color: Color(0xFF205C79),
                          fontSize: 18,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                      const SizedBox(height: 6),
                      ConstrainedBox(
                        constraints: const BoxConstraints(maxWidth: 340),
                        child: Text(
                          widget.message.detail,
                          textAlign: TextAlign.center,
                          style: const TextStyle(
                            color: Color(0xFF5C7481),
                            fontSize: 14,
                            height: 1.45,
                          ),
                        ),
                      ),
                    ],
                  );
                },
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _WaveWashClipper extends CustomClipper<Path> {
  const _WaveWashClipper({required this.progress});

  final double progress;

  @override
  Path getClip(Size size) {
    final normalized = progress.clamp(0.0, 1.0);
    final waveY = size.height * (1 - normalized);
    final amplitude = 24 * math.sin(normalized * math.pi);
    final path = Path()
      ..moveTo(0, 0)
      ..lineTo(size.width, 0);

    const steps = 60;
    for (var step = 0; step <= steps; step++) {
      final x = size.width * (1 - (step / steps));
      final wave = math.sin((x / size.width * math.pi * 3) + 0.4);
      path.lineTo(x, waveY + (wave * amplitude));
    }
    return path..close();
  }

  @override
  bool shouldReclip(_WaveWashClipper oldClipper) =>
      oldClipper.progress != progress;
}

class _CoastalLoadingMarkPainter extends CustomPainter {
  const _CoastalLoadingMarkPainter({
    required this.progress,
    required this.reducedMotion,
  });

  final double progress;
  final bool reducedMotion;

  static const _deep = Color(0xFF205C79);
  static const _blue = Color(0xFF347B9B);
  static const _teal = Color(0xFF43858A);
  static const _glass = Color(0xFFC9E0E6);
  static const _paper = Color(0xFFF7F6F0);

  @override
  void paint(Canvas canvas, Size size) {
    final center = Offset(size.width / 2, size.height / 2);
    final ringRadius = size.shortestSide * 0.45;

    canvas.drawCircle(
      center,
      ringRadius,
      Paint()
        ..color = _glass.withValues(alpha: 0.55)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1,
    );

    canvas.save();
    canvas.rotate(reducedMotion ? 0 : progress * math.pi * 2);
    final ringRect = Rect.fromCircle(center: center, radius: ringRadius - 1);
    final ringShader = const SweepGradient(
      colors: [Colors.transparent, _blue, _teal, _glass, Colors.transparent],
      stops: [0, 0.23, 0.52, 0.78, 1],
    ).createShader(ringRect);
    canvas.drawArc(
      ringRect,
      0,
      math.pi * 2,
      false,
      Paint()
        ..shader = ringShader
        ..style = PaintingStyle.stroke
        ..strokeWidth = 2.4,
    );
    canvas.restore();

    const logoUnits = 44.0;
    final logoScale = (size.shortestSide * 0.52) / logoUnits;
    final logoRadius = 21 * logoScale;
    final clip = Path()
      ..addOval(Rect.fromCircle(center: center, radius: logoRadius - 0.5));

    canvas.drawCircle(center, logoRadius, Paint()..color = _paper);
    canvas.save();
    canvas.clipPath(clip);
    final fillProgress = reducedMotion
        ? 0.5
        : 0.18 +
              (0.72 *
                  ((math.sin(progress * math.pi * 2 - math.pi / 2) + 1) / 2));
    final fillTop = center.dy + logoRadius - (2 * logoRadius * fillProgress);
    final fillBounds = Rect.fromLTRB(
      center.dx - logoRadius,
      fillTop,
      center.dx + logoRadius,
      center.dy + logoRadius,
    );
    final colorAxis = reducedMotion ? 0.3 : progress;
    final gradientDirection = Offset(
      math.cos(colorAxis * math.pi * 2),
      math.sin(colorAxis * math.pi * 2),
    );
    final waterShader = LinearGradient(
      colors: const [_blue, _teal, _glass],
      begin: Alignment(-gradientDirection.dx, -gradientDirection.dy),
      end: Alignment(gradientDirection.dx, gradientDirection.dy),
    ).createShader(fillBounds);
    canvas.drawRect(fillBounds, Paint()..shader = waterShader);
    canvas.restore();

    final unitScale = logoScale;
    final offset = Offset(
      center.dx - 22 * unitScale,
      center.dy - 22 * unitScale,
    );
    Offset point(double x, double y) =>
        Offset(offset.dx + x * unitScale, offset.dy + y * unitScale);

    final wavePath = Path()
      ..moveTo(point(7, 25).dx, point(7, 25).dy)
      ..cubicTo(
        point(12.2, 25).dx,
        point(12.2, 25).dy,
        point(12.2, 20.8).dx,
        point(12.2, 20.8).dy,
        point(17.4, 20.8).dx,
        point(17.4, 20.8).dy,
      )
      ..cubicTo(
        point(22.6, 20.8).dx,
        point(22.6, 20.8).dy,
        point(22.6, 25).dx,
        point(22.6, 25).dy,
        point(27.8, 25).dx,
        point(27.8, 25).dy,
      )
      ..cubicTo(
        point(33, 25).dx,
        point(33, 25).dy,
        point(33, 20.8).dx,
        point(33, 20.8).dy,
        point(38, 20.8).dx,
        point(38, 20.8).dy,
      )
      ..moveTo(point(7, 31).dx, point(7, 31).dy)
      ..cubicTo(
        point(12.2, 31).dx,
        point(12.2, 31).dy,
        point(12.2, 26.8).dx,
        point(12.2, 26.8).dy,
        point(17.4, 26.8).dx,
        point(17.4, 26.8).dy,
      )
      ..cubicTo(
        point(22.6, 26.8).dx,
        point(22.6, 26.8).dy,
        point(22.6, 31).dx,
        point(22.6, 31).dy,
        point(27.8, 31).dx,
        point(27.8, 31).dy,
      )
      ..cubicTo(
        point(33, 31).dx,
        point(33, 31).dy,
        point(33, 26.8).dx,
        point(33, 26.8).dy,
        point(38, 26.8).dx,
        point(38, 26.8).dy,
      );
    canvas.drawPath(
      wavePath,
      Paint()
        ..color = _deep
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1.7 * unitScale
        ..strokeCap = StrokeCap.round,
    );
    canvas.drawCircle(point(28.5, 13), 3 * unitScale, Paint()..color = _teal);
    canvas.drawCircle(
      center,
      logoRadius,
      Paint()
        ..color = _deep
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1.5 * unitScale,
    );
  }

  @override
  bool shouldRepaint(_CoastalLoadingMarkPainter oldDelegate) =>
      oldDelegate.progress != progress ||
      oldDelegate.reducedMotion != reducedMotion;
}
