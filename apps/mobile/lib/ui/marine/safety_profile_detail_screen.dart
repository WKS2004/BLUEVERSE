import 'dart:async';

import 'package:flutter/material.dart';

import '../../data/models/marine_models.dart';
import '../../data/services/marine_api_client.dart';
import '../../data/services/marine_service.dart';

class SafetyProfileDetailScreen extends StatefulWidget {
  const SafetyProfileDetailScreen({super.key, required this.service});

  final MarineService service;

  @override
  State<SafetyProfileDetailScreen> createState() => _SafetyProfileDetailScreenState();
}

class _SafetyProfileDetailScreenState extends State<SafetyProfileDetailScreen> {
  SafetyProfileDto? _profile;
  bool _loading = true;
  bool _busy = false;
  String? _error;
  String? _notice;

  @override
  void initState() {
    super.initState();
    _load();
  }

  Future<void> _load() async {
    if (!mounted) return;
    setState(() {
      _loading = true;
      _error = null;
      _notice = null;
    });

    try {
      final profiles = await widget.service.currentProfiles();
      final first = profiles.isNotEmpty ? profiles.first : null;
      if (mounted) {
        setState(() {
          _profile = first;
          _loading = false;
        });
      }
    } on MarineApiException catch (error) {
      if (mounted) setState(() {
        _error = error.message;
        _loading = false;
      });
    } catch (error) {
      if (mounted) setState(() {
        _error = 'The marine service could not be reached. Check the gateway address and try again.';
        _loading = false;
      });
    }
  }

  Future<void> _save() async {
    if (_profile == null || _busy) return;

    final maxWind = _numeric('Maximum wind speed (km/h)');
    final maxWave = _numeric('Maximum wave height (m)');
    final maxSwell = _numeric('Maximum swell height (m)');
    final cautionWind = _numeric('Caution wind speed (km/h, optional)');
    final cautionWave = _numeric('Caution wave height (m, optional)');
    final cautionSwell = _numeric('Caution swell height (m, optional)');

    if (maxWind == null || maxWave == null || maxSwell == null) return;
    if (cautionWind != null && (cautionWind <= 0 || cautionWind > maxWind)) {
      _error = 'Caution wind must be positive and below the maximum wind speed.';
      return;
    }
    if (cautionWave != null && (cautionWave <= 0 || cautionWave > maxWave)) {
      _error = 'Caution wave must be positive and below the maximum wave height.';
      return;
    }
    if (cautionSwell != null && (cautionSwell <= 0 || cautionSwell > maxSwell)) {
      _error = 'Caution swell must be positive and below the maximum swell height.';
      return;
    }

    setState(() {
      _busy = true;
      _error = null;
      _notice = null;
    });

    try {
      final updated = await widget.service.updateProfile(
        id: _profile!.id,
        maxWindSpeed: maxWind,
        maxWaveHeight: maxWave,
        maxSwellHeight: maxSwell,
        cautionWindSpeed: cautionWind,
        cautionWaveHeight: cautionWave,
        cautionSwellHeight: cautionSwell,
        isActive: true,
      );
      if (mounted) {
        setState(() {
          _busy = false;
          _profile = updated;
          _notice = 'The ${updated.activityName} profile was saved as version ${updated.version}.';
        });
      }
    } on MarineApiException catch (error) {
      if (mounted) setState(() {
        _busy = false;
        _error = error.message;
      });
    } catch (error) {
      if (mounted) setState(() {
        _busy = false;
        _error = 'The marine service could not be reached. Check the gateway address and try again.';
      });
    }
  }

  Future<void> _deactivate() async {
    if (_profile == null || _busy) return;
    final confirmed = await _confirm('Deactivate this safety profile? Active assessments keep their history.');
    if (!confirmed) return;

    setState(() {
      _busy = true;
      _error = null;
      _notice = null;
    });

    try {
      await widget.service.deactivateProfile(_profile!.id);
      if (mounted) {
        setState(() {
          _busy = false;
          _notice = 'The ${_profile!.activityName} profile was deactivated. Assessment history keeps its reference.';
          _load();
        });
      }
    } on MarineApiException catch (error) {
      if (mounted) setState(() {
        _busy = false;
        _error = error.message;
      });
    } catch (error) {
      if (mounted) setState(() {
        _busy = false;
        _error = 'The marine service could not be reached. Check the gateway address and try again.';
      });
    }
  }

  double? _numeric(String label) {
    final value = _textFields?[label];
    if (value == null) return null;
    final trimmed = value.trim();
    if (trimmed.isEmpty) return null;
    final parsed = double.tryParse(trimmed);
    if (parsed == null || parsed <= 0) {
      _error = 'Enter a positive $label.';
      return null;
    }
    return parsed;
  }

  Map<String, String>? _textFields;

  Future<bool> _confirm(String message) async {
    final result = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Deactivate profile?'),
        content: Text(message),
        actions: [
          TextButton(onPressed: () => Navigator.pop(context, false), child: const Text('Cancel')),
          FilledButton(onPressed: () => Navigator.pop(context, true), child: const Text('Deactivate')),
        ],
      ),
    );
    return result ?? false;
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Safety profile details')),
      body: _buildBody(),
    );
  }

  Widget _buildBody() {
    if (_loading) return const Center(child: CircularProgressIndicator());
    if (_profile == null) {
      return Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.error_outline, color: Colors.redAccent, size: 42),
            const SizedBox(height: 12),
            Text(_error ?? 'No profile is configured yet.', style: const TextStyle(color: Colors.redAccent)),
            const SizedBox(height: 12),
            FilledButton(onPressed: _load, child: const Text('Try again')),
          ],
        ),
      );
    }

    return SingleChildScrollView(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 28),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (_error != null) _errorCard(),
          if (_notice != null) _noticeCard(),
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text(_profile!.activityName, style: const TextStyle(fontSize: 20, fontWeight: FontWeight.w700)),
                      Text('Version ${_profile!.version}', style: const TextStyle(fontWeight: FontWeight.w700, color: Colors.blueAccent)),
                    ],
                  ),
                  const SizedBox(height: 12),
                  _field('Maximum wind speed', _profile!.maxWindSpeed),
                  _field('Maximum wave height', _profile!.maxWaveHeight),
                  _field('Maximum swell height', _profile!.maxSwellHeight),
                  _field('Caution wind speed', _profile!.cautionWindSpeed ?? 0),
                  _field('Caution wave height', _profile!.cautionWaveHeight ?? 0),
                  _field('Caution swell height', _profile!.cautionSwellHeight ?? 0),
                  const SizedBox(height: 16),
                  OutlinedButton.icon(
                    onPressed: _busy ? null : _save,
                    icon: const Icon(Icons.save_outlined),
                    label: const Text('Save limits'),
                  ),
                  const SizedBox(height: 8),
                  FilledButton.icon(
                    onPressed: _busy ? null : _deactivate,
                    icon: const Icon(Icons.block),
                    label: const Text('Deactivate'),
                  ),
                ],
              ),
            ),
          ),
          const _NoteCard(),
        ],
      ),
    );
  }

  Widget _errorCard() => Card(
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Row(
            children: [
              const Icon(Icons.error_outline, color: Colors.redAccent),
              const SizedBox(width: 12),
              Expanded(
                child: Text(
                  _error!,
                  style: const TextStyle(color: Colors.redAccent),
                ),
              ),
            ],
          ),
        ),
      );

  Widget _noticeCard() => Card(
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Row(
            children: [
              const Icon(Icons.info_outline, color: Colors.blueAccent),
              const SizedBox(width: 12),
              Expanded(
                child: Text(
                  _notice!,
                  style: const TextStyle(color: Colors.blueAccent),
                ),
              ),
            ],
          ),
        ),
      );

  Widget _field(String label, double value) => Padding(
        padding: const EdgeInsets.only(bottom: 10),
        child: Row(
          children: [
            const SizedBox(width: 8),
            Text('$label: ', style: const TextStyle(fontWeight: FontWeight.w600, color: Colors.blueGrey)),
            Expanded(child: Text(value.toStringAsFixed(2), style: const TextStyle(color: Colors.blueGrey))),
          ],
        ),
      );
}

class _NoteCard extends StatelessWidget {
  const _NoteCard();

  @override
  Widget build(BuildContext context) => Card(
        child: Padding(
          padding: const EdgeInsets.all(14),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const Text(
                'Server-managed limits',
                style: TextStyle(fontWeight: FontWeight.w800, color: Colors.blueAccent),
              ),
              const SizedBox(height: 6),
              const Text(
                'Edit only the server-owned safety profile. Values are saved in the backend and directly affect deterministic suitability results.',
                style: TextStyle(height: 1.4, color: Colors.blueGrey),
              ),
            ],
          ),
        ),
      );
}
