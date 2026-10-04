import 'dart:async';

import 'package:flutter/material.dart';

import '../../data/models/marine_models.dart';
import '../../data/services/marine_api_client.dart';
import '../../data/services/marine_service.dart';

/// Permission-gated safety-profile surface.
///
/// Reads never require extra grants. Create/update/deactivate require the
/// backend permission [marine.profile.manage] (enforced by the gateway).
class SafetyProfilesScreen extends StatefulWidget {
  const SafetyProfilesScreen({super.key, required this.service});

  final MarineService service;

  @override
  State<SafetyProfilesScreen> createState() => _SafetyProfilesScreenState();
}

class _SafetyProfilesScreenState extends State<SafetyProfilesScreen> {
  List<SafetyProfileDto> _profiles = const [];
  SafetyProfileDto? _selected;
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
      final list = await widget.service.currentProfiles();
      final first = list.isNotEmpty ? list.first : null;
      if (mounted) {
        setState(() {
          _profiles = list;
          _selected = first;
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

  Future<void> _createProfile(String activityId, String activityName) async {
    if (_busy) return;
    final draft = _draftFor(activityId, activityName);
    if (draft == null) return;
    setState(() {
      _busy = true;
      _error = null;
      _notice = null;
    });

    try {
      final created = await widget.service.createProfile(
        activityId: activityId,
        maxWindSpeed: draft.maxWindSpeed,
        maxWaveHeight: draft.maxWaveHeight,
        maxSwellHeight: draft.maxSwellHeight,
        cautionWindSpeed: draft.cautionWindSpeed,
        cautionWaveHeight: draft.cautionWaveHeight,
        cautionSwellHeight: draft.cautionSwellHeight,
      );
      if (mounted) {
        setState(() {
          _profiles = [..._profiles, created];
          _selected = created;
          _busy = false;
          _notice = 'A new active profile version was created for ${created.activityName}.';
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

  SafetyProfileDraft? _draftFor(String activityId, String activityName) {
    final maxWind = _numeric('Maximum wind speed (km/h)', 'maxWind');
    final maxWave = _numeric('Maximum wave height (m)', 'maxWave');
    final maxSwell = _numeric('Maximum swell height (m)', 'maxSwell');
    if (maxWind == null || maxWave == null || maxSwell == null) return null;

    final cautionWind = _numeric('Caution wind speed (km/h, optional)', 'cautionWind');
    final cautionWave = _numeric('Caution wave height (m, optional)', 'cautionWave');
    final cautionSwell = _numeric('Caution swell height (m, optional)', 'cautionSwell');
    if (cautionWind != null && (cautionWind <= 0 || cautionWind > maxWind)) return null;
    if (cautionWave != null && (cautionWave <= 0 || cautionWave > maxWave)) return null;
    if (cautionSwell != null && (cautionSwell <= 0 || cautionSwell > maxSwell)) return null;

    return SafetyProfileDraft(
      maxWindSpeed: maxWind,
      maxWaveHeight: maxWave,
      maxSwellHeight: maxSwell,
      cautionWindSpeed: cautionWind,
      cautionWaveHeight: cautionWave,
      cautionSwellHeight: cautionSwell,
    );
  }

  double? _numeric(String label, String key) {
    final value = _textFields?[key];
    if (value == null) return null;
    final trimmed = value.trim();
    if (trimmed.isEmpty) return null;
    final parsed = double.tryParse(trimmed);
    if (parsed == null || parsed <= 0) {
      _error = 'Enter a positive limit for $label.';
      return null;
    }
    return parsed;
  }

  Map<String, String>? _textFields;

  Future<void> _editProfile() async {
    if (_selected == null || _busy) return;
    final draft = _draftFor(_selected!.activityId, _selected!.activityName);
    if (draft == null) return;

    setState(() {
      _busy = true;
      _error = null;
      _notice = null;
      _textFields = {
        'maxWind': draft.maxWindSpeed.toStringAsFixed(2),
        'maxWave': draft.maxWaveHeight.toStringAsFixed(2),
        'maxSwell': draft.maxSwellHeight.toStringAsFixed(2),
        'cautionWind': draft.cautionWindSpeed?.toStringAsFixed(2) ?? '',
        'cautionWave': draft.cautionWaveHeight?.toStringAsFixed(2) ?? '',
        'cautionSwell': draft.cautionSwellHeight?.toStringAsFixed(2) ?? '',
      };
    });

    try {
      final updated = await widget.service.updateProfile(
        id: _selected!.id,
        maxWindSpeed: draft.maxWindSpeed,
        maxWaveHeight: draft.maxWaveHeight,
        maxSwellHeight: draft.maxSwellHeight,
        cautionWindSpeed: draft.cautionWindSpeed,
        cautionWaveHeight: draft.cautionWaveHeight,
        cautionSwellHeight: draft.cautionSwellHeight,
        isActive: true,
      );
      if (mounted) {
        setState(() {
          _busy = false;
          _profiles = _profiles.map((profile) {
            if (profile.id != updated.id) return profile;
            _selected = updated;
            return updated;
          }).toList();
          _notice = 'The ${updated.activityName} profile was saved as version ${updated.version}.';
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

  Future<void> _deactivateProfile() async {
    if (_selected == null || _busy) return;
    final confirmed = await _confirm('Deactivate profile?');
    if (!confirmed) return;

    setState(() {
      _busy = true;
      _error = null;
      _notice = null;
    });

    try {
      await widget.service.deactivateProfile(_selected!.id);
      if (mounted) {
        setState(() {
          _busy = false;
          _notice = 'The ${_selected!.activityName} profile was deactivated. Assessment history keeps its reference.';
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
      appBar: AppBar(title: const Text('Safety profiles')),
      body: _buildBody(),
    );
  }

  Widget _buildBody() {
    return RefreshIndicator(
      onRefresh: _load,
      child: SingleChildScrollView(
        padding: const EdgeInsets.fromLTRB(16, 16, 16, 28),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (_error != null) _errorCard(),
            if (_notice != null) _noticeCard(),
            if (_selected == null && !_loading && _profiles.isEmpty) _emptyCard(),
            _listCard(),
            if (_selected != null) _detailCard(),
            const _NoteCard(),
          ],
        ),
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

  Widget _emptyCard() => Card(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: const Text(
            'No safety profiles are configured yet. Create the first one for an activity above.',
            style: TextStyle(color: Colors.blueGrey),
          ),
        ),
      );

  Widget _listCard() {
    if (_loading) return const SizedBox.shrink();
    if (_profiles.isEmpty) return const SizedBox.shrink();
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Text('PROFILES', style: TextStyle(color: Colors.blueAccent, fontWeight: FontWeight.w800, letterSpacing: 1.2)),
                Text('${_profiles.length} profiles', style: const TextStyle(fontWeight: FontWeight.w700)),
              ],
            ),
            const SizedBox(height: 8),
            ..._profiles.map((profile) => _profileTile(profile)),
          ],
        ),
      ),
    );
  }

  Widget _profileTile(SafetyProfileDto profile) => Card(
        margin: const EdgeInsets.only(bottom: 10),
        child: ListTile(
          leading: CircleAvatar(
            backgroundColor: _selected?.id == profile.id ? Colors.blueAccent : Colors.transparent,
            child: _selected?.id == profile.id ? const Icon(Icons.radio_button_on, color: Colors.white) : const Icon(Icons.radio_button_off),
          ),
          title: Text(profile.activityName, style: const TextStyle(fontWeight: FontWeight.w700)),
          trailing: _selected?.id == profile.id
              ? Chip(label: Text(_selected!.isActive ? 'ACTIVE' : 'INACTIVE'), backgroundColor: Colors.transparent)
              : (_selected?.activityId != profile.activityId
                  ? const Chip(label: Text(''), backgroundColor: Colors.transparent)
                  : const SizedBox.shrink()),
          onTap: () => setState(() => _selected = profile),
        ),
      );

  Widget _detailCard() {
    final profile = _selected!;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(14),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text(
                  profile.activityName,
                  style: const TextStyle(fontWeight: FontWeight.w700),
                ),
                Text('Version ${profile.version}', style: const TextStyle(fontWeight: FontWeight.w700, color: Colors.blueAccent)),
              ],
            ),
            const SizedBox(height: 8),
            _field('Max wind (km/h)', profile.maxWindSpeed),
            _field('Max wave (m)', profile.maxWaveHeight),
            _field('Max swell (m)', profile.maxSwellHeight),
            _field('Caution wind (km/h)', profile.cautionWindSpeed ?? 0),
            _field('Caution wave (m)', profile.cautionWaveHeight ?? 0),
            _field('Caution swell (m)', profile.cautionSwellHeight ?? 0),
            const SizedBox(height: 12),
            FilledButton.icon(
              onPressed: _busy || _selected!.isActive ? null : _deactivateProfile,
              icon: const Icon(Icons.block),
              label: const Text('Deactivate profile'),
            ),
            const SizedBox(height: 8),
            OutlinedButton.icon(
              onPressed: _busy ? null : _editProfile,
              icon: const Icon(Icons.edit_outlined),
              label: const Text('Save limits'),
            ),
          ],
        ),
      ),
    );
  }

  Widget _field(String label, double value) => Padding(
        padding: const EdgeInsets.only(bottom: 8),
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
                'Safety profiles are configured by the backend with explicit units. Creating a profile supersedes the activity’s previous active row while retaining history.',
                style: TextStyle(height: 1.4, color: Colors.blueGrey),
              ),
            ],
          ),
        ),
      );
}
