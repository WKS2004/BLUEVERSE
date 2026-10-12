// Only these registered personal pages may be used as a post-login return.
// Never accept an external URL or an arbitrary route from navigation arguments.
(String, Object?) plannerLoginReturn(Object? arguments) {
  if (arguments is! Map ||
      !['/planner/plan', '/planner/saved'].contains(arguments['returnTo'])) {
    return ('/dashboard', null);
  }
  final original = arguments['returnArguments'];
  final safe = original is Map
      ? <String, String>{
          for (final key in ['destinationId', 'activityId', 'itineraryId'])
            if (original[key] is String) key: original[key] as String,
        }
      : null;
  return (arguments['returnTo'] as String, safe);
}
