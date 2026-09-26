import 'package:flutter/material.dart';

abstract final class BlueversePalette {
  static const coastPaper = Color(0xFFF7F6F0);
  static const coastSand = Color(0xFFE9EFF0);
  static const coastPearl = Color(0xFFFBFDFD);
  static const coastInk = Color(0xFF18394C);
  static const coastDeep = Color(0xFF205C79);
  static const coastBlue = Color(0xFF347B9B);
  static const coastTeal = Color(0xFF43858A);
  static const coastGlass = Color(0xFFC9E0E6);
  static const coastSage = Color(0xFFE5EFF0);
  static const coastMuted = Color(0xFF5C7481);
  static const coastLine = Color(0xFFD6E2E7);
}

abstract final class BlueverseTheme {
  static ThemeData get light {
    final colors =
        ColorScheme.fromSeed(
          seedColor: BlueversePalette.coastDeep,
          brightness: Brightness.light,
        ).copyWith(
          primary: BlueversePalette.coastDeep,
          onPrimary: Colors.white,
          secondary: BlueversePalette.coastTeal,
          onSecondary: Colors.white,
          surface: BlueversePalette.coastPearl,
          onSurface: BlueversePalette.coastInk,
          onSurfaceVariant: BlueversePalette.coastMuted,
          outline: BlueversePalette.coastLine,
          primaryContainer: BlueversePalette.coastGlass,
          onPrimaryContainer: BlueversePalette.coastInk,
          secondaryContainer: BlueversePalette.coastSage,
          onSecondaryContainer: BlueversePalette.coastInk,
        );
    final baseText = ThemeData.light().textTheme.apply(
      bodyColor: BlueversePalette.coastInk,
      displayColor: BlueversePalette.coastInk,
      fontFamily: 'Manrope',
    );

    return ThemeData(
      useMaterial3: true,
      colorScheme: colors,
      scaffoldBackgroundColor: BlueversePalette.coastPaper,
      textTheme: baseText.copyWith(
        displayLarge: baseText.displayLarge?.copyWith(fontFamily: 'Sora'),
        displayMedium: baseText.displayMedium?.copyWith(fontFamily: 'Sora'),
        displaySmall: baseText.displaySmall?.copyWith(fontFamily: 'Sora'),
        headlineLarge: baseText.headlineLarge?.copyWith(fontFamily: 'Sora'),
        headlineMedium: baseText.headlineMedium?.copyWith(fontFamily: 'Sora'),
        headlineSmall: baseText.headlineSmall?.copyWith(fontFamily: 'Sora'),
      ),
      appBarTheme: const AppBarTheme(
        backgroundColor: BlueversePalette.coastPaper,
        foregroundColor: BlueversePalette.coastDeep,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
      ),
      cardTheme: CardThemeData(
        color: BlueversePalette.coastPearl,
        elevation: 0,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(24),
          side: const BorderSide(color: BlueversePalette.coastLine),
        ),
      ),
      inputDecorationTheme: const InputDecorationTheme(
        filled: true,
        fillColor: BlueversePalette.coastPearl,
        contentPadding: EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.all(Radius.circular(16)),
          borderSide: BorderSide(color: BlueversePalette.coastLine),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.all(Radius.circular(16)),
          borderSide: BorderSide(color: BlueversePalette.coastLine),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.all(Radius.circular(16)),
          borderSide: BorderSide(color: BlueversePalette.coastBlue, width: 1.5),
        ),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: BlueversePalette.coastDeep,
          foregroundColor: Colors.white,
          minimumSize: const Size.fromHeight(50),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(28),
          ),
          textStyle: const TextStyle(fontWeight: FontWeight.w700),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: BlueversePalette.coastDeep,
          minimumSize: const Size.fromHeight(50),
          side: const BorderSide(color: BlueversePalette.coastLine),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(28),
          ),
          textStyle: const TextStyle(fontWeight: FontWeight.w700),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          foregroundColor: BlueversePalette.coastBlue,
          textStyle: const TextStyle(fontWeight: FontWeight.w700),
        ),
      ),
      snackBarTheme: const SnackBarThemeData(
        backgroundColor: BlueversePalette.coastInk,
        contentTextStyle: TextStyle(color: Colors.white),
        behavior: SnackBarBehavior.floating,
      ),
    );
  }
}
