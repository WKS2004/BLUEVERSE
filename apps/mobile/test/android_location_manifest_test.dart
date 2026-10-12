import 'dart:io';

import 'package:flutter_test/flutter_test.dart';

void main() {
  // experience-discovery: the user revised the requirement to precise,
  // opt-in foreground location. Background tracking remains excluded.
  test('MOB-EXP-033 Android map permits precise foreground location only', () {
    final manifest = File('android/app/src/main/AndroidManifest.xml')
        .readAsStringSync();
    final permissionTags = RegExp(r'<uses-permission\b[^>]*>')
        .allMatches(manifest)
        .map((match) => match.group(0)!)
        .toList();
    String permission(String name) => permissionTags.singleWhere(
      (tag) => tag.contains('android:name="$name"'),
    );

    expect(
      manifest,
      contains('xmlns:tools="http://schemas.android.com/tools"'),
    );
    expect(
      permission('android.permission.ACCESS_COARSE_LOCATION'),
      isNot(contains('tools:node="remove"')),
    );
    expect(
      permission('android.permission.INTERNET'),
      isNot(contains('tools:node="remove"')),
    );
    expect(
      permission('android.permission.ACCESS_FINE_LOCATION'),
      isNot(contains('tools:node="remove"')),
    );
    expect(
      permission('android.permission.ACCESS_BACKGROUND_LOCATION'),
      contains('tools:node="remove"'),
    );
    expect(manifest, contains('android:label="BLUEVERSE"'));
  });
}
