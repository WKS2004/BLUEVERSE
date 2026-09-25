import 'dart:math' as math;

import 'package:flutter/material.dart';

enum BlueverseErrorKind { notFound, server }

class BlueverseErrorScreen extends StatelessWidget {
  const BlueverseErrorScreen({required this.kind, super.key});

  const BlueverseErrorScreen.notFound({super.key})
    : kind = BlueverseErrorKind.notFound;

  const BlueverseErrorScreen.server({super.key})
    : kind = BlueverseErrorKind.server;

  final BlueverseErrorKind kind;

  bool get _isNotFound => kind == BlueverseErrorKind.notFound;

  @override
  Widget build(BuildContext context) {
    final code = _isNotFound ? '404' : '500';
    final title = _isNotFound
        ? 'This cove isn’t on our chart.'
        : 'A current interrupted the journey.';
    final description = _isNotFound
        ? 'We couldn’t find the page you were looking for. The coast is still here, so let’s find another way in.'
        : 'Something went wrong while bringing this part of BLUEVERSE to shore. Your account and saved details are safe.';

    return Scaffold(
      backgroundColor: const Color(0xFFF7F6F0),
      body: SafeArea(
        child: LayoutBuilder(
          builder: (context, constraints) => SingleChildScrollView(
            padding: const EdgeInsets.fromLTRB(20, 18, 20, 24),
            child: Center(
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 840),
                child: ConstrainedBox(
                  constraints: BoxConstraints(
                    minHeight: math
                        .max(0.0, constraints.maxHeight - 42)
                        .toDouble(),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      Row(
                        children: [
                          Container(
                            width: 42,
                            height: 42,
                            decoration: const BoxDecoration(
                              color: Color(0xFFE5EFF0),
                              shape: BoxShape.circle,
                            ),
                            child: const Icon(
                              Icons.waves_outlined,
                              color: Color(0xFF205C79),
                            ),
                          ),
                          const SizedBox(width: 10),
                          const Text(
                            'BLUEVERSE',
                            style: TextStyle(
                              color: Color(0xFF205C79),
                              fontSize: 15,
                              fontWeight: FontWeight.w800,
                              letterSpacing: -0.4,
                            ),
                          ),
                          const Spacer(),
                          const Text(
                            'OUR COAST, MORE CONNECTED',
                            style: TextStyle(
                              color: Color(0xFF5C7481),
                              fontSize: 9,
                              fontWeight: FontWeight.w800,
                              letterSpacing: 1.1,
                            ),
                          ),
                        ],
                      ),
                      const SizedBox(height: 26),
                      Container(
                        clipBehavior: Clip.antiAlias,
                        decoration: BoxDecoration(
                          borderRadius: BorderRadius.circular(30),
                          gradient: const LinearGradient(
                            begin: Alignment.topLeft,
                            end: Alignment.bottomRight,
                            colors: [Color(0xFFEAF2F0), Color(0xFFD7E8E9)],
                          ),
                          boxShadow: const [
                            BoxShadow(
                              color: Color(0x1D18394C),
                              blurRadius: 36,
                              offset: Offset(0, 18),
                            ),
                          ],
                        ),
                        child: Column(
                          children: [
                            Padding(
                              padding: const EdgeInsets.fromLTRB(20, 28, 20, 0),
                              child: Column(
                                children: [
                                  Container(
                                    width: 56,
                                    height: 56,
                                    decoration: const BoxDecoration(
                                      color: Colors.white,
                                      shape: BoxShape.circle,
                                    ),
                                    child: Icon(
                                      _isNotFound
                                          ? Icons.explore_off_outlined
                                          : Icons.sync_problem_outlined,
                                      color: const Color(0xFF347B9B),
                                      size: 28,
                                    ),
                                  ),
                                  const SizedBox(height: 12),
                                  Text(
                                    _isNotFound
                                        ? 'UNCHARTED WATERS'
                                        : 'A TEMPORARY PAUSE',
                                    style: const TextStyle(
                                      color: Color(0xFF347B9B),
                                      fontSize: 10,
                                      fontWeight: FontWeight.w800,
                                      letterSpacing: 1.6,
                                    ),
                                  ),
                                  const SizedBox(height: 9),
                                  Text(
                                    code,
                                    style: const TextStyle(
                                      color: Color(0xFF205C79),
                                      fontSize: 56,
                                      fontWeight: FontWeight.w700,
                                      height: 1.0,
                                      letterSpacing: -3.5,
                                    ),
                                  ),
                                  const SizedBox(height: 7),
                                  Text(
                                    title,
                                    textAlign: TextAlign.center,
                                    style: Theme.of(context)
                                        .textTheme
                                        .headlineSmall
                                        ?.copyWith(
                                          color: const Color(0xFF18394C),
                                          fontWeight: FontWeight.w700,
                                          letterSpacing: -0.7,
                                        ),
                                  ),
                                  const SizedBox(height: 9),
                                  ConstrainedBox(
                                    constraints: const BoxConstraints(
                                      maxWidth: 580,
                                    ),
                                    child: Text(
                                      description,
                                      textAlign: TextAlign.center,
                                      style: const TextStyle(
                                        color: Color(0xFF5C7481),
                                        height: 1.55,
                                      ),
                                    ),
                                  ),
                                  const SizedBox(height: 20),
                                  Wrap(
                                    alignment: WrapAlignment.center,
                                    spacing: 10,
                                    runSpacing: 10,
                                    children: [
                                      FilledButton.icon(
                                        onPressed: () => Navigator.of(context)
                                            .pushNamedAndRemoveUntil(
                                              '/',
                                              (route) => false,
                                            ),
                                        icon: const Icon(Icons.waves_outlined),
                                        label: const Text('Back to the coast'),
                                      ),
                                      if (!_isNotFound)
                                        OutlinedButton.icon(
                                          onPressed: () {
                                            final navigator = Navigator.of(
                                              context,
                                            );
                                            if (navigator.canPop()) {
                                              navigator.pop();
                                            } else {
                                              navigator.pushNamedAndRemoveUntil(
                                                '/',
                                                (route) => false,
                                              );
                                            }
                                          },
                                          icon: const Icon(Icons.refresh),
                                          label: const Text('Try again'),
                                        ),
                                    ],
                                  ),
                                  const SizedBox(height: 16),
                                ],
                              ),
                            ),
                            SizedBox(
                              height: 125,
                              width: double.infinity,
                              child: CustomPaint(
                                painter: _ErrorWavePainter(),
                                child: const Padding(
                                  padding: EdgeInsets.only(bottom: 15),
                                  child: Align(
                                    alignment: Alignment.bottomCenter,
                                    child: Text(
                                      'BLUEVERSE · COASTAL DISCOVERY',
                                      style: TextStyle(
                                        color: Color(0xFFEAF5F5),
                                        fontSize: 9,
                                        fontWeight: FontWeight.w800,
                                        letterSpacing: 1.2,
                                      ),
                                    ),
                                  ),
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(height: 18),
                      Text(
                        _isNotFound
                            ? 'Sometimes the best discoveries start with a change of course.'
                            : 'We’ll be ready when the tide settles.',
                        textAlign: TextAlign.center,
                        style: const TextStyle(
                          color: Color(0xFF5C7481),
                          fontSize: 12,
                          fontWeight: FontWeight.w600,
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _ErrorWavePainter extends CustomPainter {
  @override
  void paint(Canvas canvas, Size size) {
    Path wave(double top, double amplitude, double phase) {
      final path = Path()..moveTo(0, top);
      const segments = 42;
      for (var index = 0; index <= segments; index++) {
        final x = size.width * index / segments;
        final y =
            top + math.sin((x / size.width * math.pi * 2) + phase) * amplitude;
        path.lineTo(x, y);
      }
      return path
        ..lineTo(size.width, size.height)
        ..lineTo(0, size.height)
        ..close();
    }

    canvas.drawPath(
      wave(25, 10, 0.5),
      Paint()..color = const Color(0xFF69A9A9).withValues(alpha: 0.58),
    );
    canvas.drawPath(
      wave(43, 9, 2.1),
      Paint()..color = const Color(0xFF347B9B).withValues(alpha: 0.76),
    );
    canvas.drawPath(wave(61, 8, 4.0), Paint()..color = const Color(0xFF205C79));
  }

  @override
  bool shouldRepaint(covariant _ErrorWavePainter oldDelegate) => false;
}

Route<void> blueverseUnknownRoute(RouteSettings settings) =>
    MaterialPageRoute<void>(
      settings: settings,
      builder: (_) => const BlueverseErrorScreen.notFound(),
    );

Widget blueverseUnexpectedErrorWidget(FlutterErrorDetails _) =>
    const BlueverseErrorScreen.server();
