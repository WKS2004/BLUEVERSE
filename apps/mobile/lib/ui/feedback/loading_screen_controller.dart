import 'dart:async';

import 'package:flutter/foundation.dart';

enum LoadingScreenMode { coastal, authentication }

@immutable
class LoadingScreenMessage {
  const LoadingScreenMessage({
    required this.mode,
    required this.title,
    required this.detail,
    this.contextPriority = 0,
    this.minimumVisibleDuration = Duration.zero,
  });

  const LoadingScreenMessage.coastal()
    : mode = LoadingScreenMode.coastal,
      title = 'A moment by the water',
      detail = 'Loading BLUEVERSE',
      contextPriority = 0,
      minimumVisibleDuration = Duration.zero;

  const LoadingScreenMessage.restoreSession()
    : mode = LoadingScreenMode.authentication,
      title = 'Restoring your account',
      detail = 'Checking your BLUEVERSE session securely.',
      contextPriority = 0,
      minimumVisibleDuration = Duration.zero;

  const LoadingScreenMessage.signIn()
    : mode = LoadingScreenMode.authentication,
      title = 'Signing you in',
      detail = 'Verifying your details and opening your account.',
      contextPriority = 3,
      minimumVisibleDuration = const Duration(milliseconds: 850);

  const LoadingScreenMessage.registration()
    : mode = LoadingScreenMode.authentication,
      title = 'Preparing your account',
      detail = 'Setting up your BLUEVERSE profile securely.',
      contextPriority = 3,
      minimumVisibleDuration = const Duration(milliseconds: 850);

  const LoadingScreenMessage.switchAccount()
    : mode = LoadingScreenMode.authentication,
      title = 'Switching account',
      detail = 'Restoring the selected account on this device.',
      contextPriority = 2,
      minimumVisibleDuration = const Duration(milliseconds: 850);

  const LoadingScreenMessage.profile()
    : mode = LoadingScreenMode.authentication,
      title = 'Saving your profile',
      detail = 'Keeping your account details up to date.',
      contextPriority = 1,
      minimumVisibleDuration = Duration.zero;

  const LoadingScreenMessage.sessions()
    : mode = LoadingScreenMode.authentication,
      title = 'Checking your sessions',
      detail = 'Loading the devices connected to your account.',
      contextPriority = 1,
      minimumVisibleDuration = Duration.zero;

  const LoadingScreenMessage.password()
    : mode = LoadingScreenMode.authentication,
      title = 'Securing your account',
      detail = 'Updating your sign-in credentials.',
      contextPriority = 3,
      minimumVisibleDuration = const Duration(milliseconds: 850);

  const LoadingScreenMessage.signOut()
    : mode = LoadingScreenMode.authentication,
      title = 'Signing you out',
      detail = 'Closing this account’s session on this device.',
      contextPriority = 3,
      minimumVisibleDuration = const Duration(milliseconds: 850);

  const LoadingScreenMessage.endSession()
    : mode = LoadingScreenMode.authentication,
      title = 'Ending the session',
      detail = 'Applying your session security request.',
      contextPriority = 3,
      minimumVisibleDuration = const Duration(milliseconds: 850);

  const LoadingScreenMessage.deleteAccount()
    : mode = LoadingScreenMode.authentication,
      title = 'Removing your account',
      detail = 'Finishing your confirmed account request securely.',
      contextPriority = 3,
      minimumVisibleDuration = const Duration(milliseconds: 850);

  final LoadingScreenMode mode;
  final String title;
  final String detail;
  final int contextPriority;
  final Duration minimumVisibleDuration;

  bool get isAuthentication => mode == LoadingScreenMode.authentication;
}

class LoadingScreenController extends ChangeNotifier {
  LoadingScreenController({
    this.minimumVisibleDuration = const Duration(milliseconds: 160),
    this.slowLoadThreshold = const Duration(seconds: 2),
    this.exitAnimationDuration = const Duration(milliseconds: 850),
  });

  final Duration minimumVisibleDuration;
  final Duration slowLoadThreshold;
  final Duration exitAnimationDuration;

  final Map<int, LoadingScreenMessage> _tasks = {};
  int _nextTaskId = 0;
  Timer? _settleTimer;
  Timer? _slowTimer;
  Timer? _exitTimer;
  Timer? _authMinimumTimer;
  bool _isVisible = false;
  bool _isExiting = false;
  bool _hadSlowLoad = false;
  bool _authMinimumElapsed = true;
  LoadingScreenMessage _message = const LoadingScreenMessage.coastal();

  LoadingScreenMessage _latestMessage() {
    var selectedMessage = _tasks.values.first;
    for (final message in _tasks.values.skip(1)) {
      if (message.contextPriority >= selectedMessage.contextPriority) {
        selectedMessage = message;
      }
    }

    if (_isVisible &&
        _message.isAuthentication &&
        _message.contextPriority > selectedMessage.contextPriority) {
      return _message;
    }
    return selectedMessage;
  }

  bool get isVisible => _isVisible;
  bool get isExiting => _isExiting;
  bool get hasBeenSlow => _hadSlowLoad;
  LoadingScreenMessage get message => _message;

  VoidCallback begin({
    LoadingScreenMessage message = const LoadingScreenMessage.coastal(),
  }) {
    final wasIdle = _tasks.isEmpty;
    _settleTimer?.cancel();
    _settleTimer = null;
    _exitTimer?.cancel();
    _exitTimer = null;

    if (!_isVisible) {
      _isVisible = true;
      _hadSlowLoad = false;
      _startSlowTimer();
    } else if (wasIdle && !_hadSlowLoad) {
      _startSlowTimer();
    }

    _isExiting = false;
    final taskId = ++_nextTaskId;
    _tasks[taskId] = message;
    _message = _latestMessage();
    if (message.minimumVisibleDuration > Duration.zero) {
      _authMinimumTimer?.cancel();
      _authMinimumElapsed = false;
      _authMinimumTimer = Timer(message.minimumVisibleDuration, () {
        _authMinimumTimer = null;
        _authMinimumElapsed = true;
        if (_tasks.isEmpty && _settleTimer == null) _finishWhenReady();
      });
    }
    notifyListeners();

    var finished = false;
    return () {
      if (finished) return;
      finished = true;
      _tasks.remove(taskId);

      if (_tasks.isNotEmpty) {
        _message = _latestMessage();
        notifyListeners();
        return;
      }

      _slowTimer?.cancel();
      _slowTimer = null;
      _settleTimer = Timer(minimumVisibleDuration, () {
        _settleTimer = null;
        _finishWhenReady();
      });
      notifyListeners();
    };
  }

  Future<T> track<T>(
    Future<T> Function() action, {
    LoadingScreenMessage message = const LoadingScreenMessage.coastal(),
  }) async {
    final finish = begin(message: message);
    try {
      return await action();
    } finally {
      finish();
    }
  }

  void _startSlowTimer() {
    _slowTimer?.cancel();
    _slowTimer = Timer(slowLoadThreshold, () {
      _slowTimer = null;
      if (_tasks.isNotEmpty) _hadSlowLoad = true;
    });
  }

  void _finishWhenReady() {
    if (_tasks.isNotEmpty || !_authMinimumElapsed) return;

    if (_hadSlowLoad) {
      _isExiting = true;
      notifyListeners();
      _exitTimer = Timer(exitAnimationDuration, _hide);
    } else {
      _hide();
    }
  }

  void _hide() {
    _exitTimer?.cancel();
    _exitTimer = null;
    _slowTimer?.cancel();
    _slowTimer = null;
    _authMinimumTimer?.cancel();
    _authMinimumTimer = null;
    _isVisible = false;
    _isExiting = false;
    _hadSlowLoad = false;
    _authMinimumElapsed = true;
    _message = const LoadingScreenMessage.coastal();
    notifyListeners();
  }

  @override
  void dispose() {
    _settleTimer?.cancel();
    _slowTimer?.cancel();
    _exitTimer?.cancel();
    _authMinimumTimer?.cancel();
    super.dispose();
  }
}

final blueverseLoadingScreenController = LoadingScreenController();
