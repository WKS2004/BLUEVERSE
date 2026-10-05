import 'dart:async';

import 'package:flutter/material.dart';

import '../../data/models/marine_models.dart';
import '../../data/services/marine_api_client.dart';
import '../../data/services/marine_service.dart';
import 'marine_safety_profile_editor.dart';

/// Permission-gated safety-profile surface.
///
/// Reads never require extra grants. Create/update/deactivate require the
/// backend permission [marine.profile.manage] (enforced by the gateway).
class SafetyProfilesScreen extends StatefulWidget {
  const SafetyProfilesScreen({
    super.key,
    required this.service,
    required this.canManage,
    this.currentUserId,
  });

  final MarineService service;
  final bool canManage;
  final String? currentUserId;

  @override
  State<SafetyProfilesScreen> createState() => _SafetyProfilesScreenState();
}

class _SafetyProfilesScreenState extends State<SafetyProfilesScreen> {
  List<SafetyProfileDto> _profiles = const [];
  SafetyProfileDto? _selected;
  String _createActivityId = '';
  bool _loading = true;
  bool _busy = false;
  String? _error;
  String? _notice;

  @override
  void initState() {
    super.initState();
    final activities = widget.service.referenceActivities;
    if (activities.isNotEmpty)
      _createActivityId = activities.first['id'] as String;
    _load();
  }

  Future<void> _load({String? selectProfileId}) async {
    if (!mounted) return;
    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final list = await widget.service.currentProfiles();
      SafetyProfileDto? selected;
      for (final item in list) {
        if (item.id == selectProfileId ||
            (selectProfileId == null && item.id == _selected?.id)) {
          selected = item;
          break;
        }
      }
      selected ??= list.isNotEmpty ? list.first : null;
      if (mounted) {
        setState(() {
          _profiles = list;
          _selected = selected;
          _loading = false;
        });
      }
    } on MarineApiException catch (error) {
      if (mounted)
        setState(() {
          _error = error.message;
          _loading = false;
        });
    } catch (error) {
      if (mounted)
        setState(() {
          _error = 'The marine service could not be reached. Check the gateway address and try again.';
          _loading = false;
        });
    }
  }

  Future<void> _createProfile(String activityId, String activityName) async {
    if (_busy || !widget.canManage) return;
    final draft = await _requestDraft(activityName: activityName);
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
        windCriteriaSource: draft.windCriteriaSource,
        windCriteriaRationale: draft.windCriteriaRationale,
        waveCriteriaSource: draft.waveCriteriaSource,
        waveCriteriaRationale: draft.waveCriteriaRationale,
        swellCriteriaSource: draft.swellCriteriaSource,
        swellCriteriaRationale: draft.swellCriteriaRationale,
        cautionWindSpeed: draft.cautionWindSpeed,
        cautionWaveHeight: draft.cautionWaveHeight,
        cautionSwellHeight: draft.cautionSwellHeight,
      );
      if (mounted) {
        setState(() {
          _profiles = [..._profiles, created];
          _selected = created;
          _busy = false;
          _notice =
              'Version ${created.version} for ${created.activityName} is saved and awaiting a different manager’s review.';
        });
        await _load(selectProfileId: created.id);
      }
    } on MarineApiException catch (error) {
      if (mounted)
        setState(() {
          _busy = false;
          _error = error.message;
        });
    } catch (error) {
      if (mounted)
        setState(() {
          _busy = false;
          _error = 'The marine service could not be reached. Check the gateway address and try again.';
        });
    }
  }

  Future<void> _editProfile() async {
    final profile = _selected;
    if (profile == null || _busy || !widget.canManage) return;
    final draft = await _requestDraft(
      activityName: profile.activityName,
      profile: profile,
    );
    if (draft == null) return;

    setState(() {
      _busy = true;
      _error = null;
      _notice = null;
    });

    try {
      final updated = await widget.service.updateProfile(
        id: profile.id,
        maxWindSpeed: draft.maxWindSpeed,
        maxWaveHeight: draft.maxWaveHeight,
        maxSwellHeight: draft.maxSwellHeight,
        windCriteriaSource: draft.windCriteriaSource,
        windCriteriaRationale: draft.windCriteriaRationale,
        waveCriteriaSource: draft.waveCriteriaSource,
        waveCriteriaRationale: draft.waveCriteriaRationale,
        swellCriteriaSource: draft.swellCriteriaSource,
        swellCriteriaRationale: draft.swellCriteriaRationale,
        cautionWindSpeed: draft.cautionWindSpeed,
        cautionWaveHeight: draft.cautionWaveHeight,
        cautionSwellHeight: draft.cautionSwellHeight,
      );
      if (mounted) {
        setState(() {
          _busy = false;
          _notice =
              'Version ${updated.version} for ${updated.activityName} is saved and awaiting review.';
        });
        await _load(selectProfileId: updated.id);
      }
    } on MarineApiException catch (error) {
      if (mounted)
        setState(() {
          _busy = false;
          _error = error.message;
        });
    } catch (error) {
      if (mounted)
        setState(() {
          _busy = false;
          _error = 'The marine service could not be reached. Check the gateway address and try again.';
        });
    }
  }

  Future<void> _deactivateProfile() async {
    if (_selected == null || _busy || !widget.canManage) return;
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
          _notice =
              'The ${_selected!.activityName} profile was deactivated. Assessment history keeps its reference.';
          _load();
        });
      }
    } on MarineApiException catch (error) {
      if (mounted)
        setState(() {
          _busy = false;
          _error = error.message;
        });
    } catch (error) {
      if (mounted)
        setState(() {
          _busy = false;
          _error = 'The marine service could not be reached. Check the gateway address and try again.';
        });
    }
  }

  Future<void> _reviewProfile() async {
    final profile = _selected;
    if (profile == null ||
        _busy ||
        !widget.canManage ||
        profile.reviewedAt != null)
      return;
    if (profile.createdByUserId == widget.currentUserId) {
      setState(
        () => _error =
            'A different authorized manager must review this profile version.',
      );
      return;
    }
    setState(() {
      _busy = true;
      _error = null;
      _notice = null;
    });
    try {
      final approved = await widget.service.reviewProfile(profile.id);
      if (mounted) {
        setState(() {
          _busy = false;
          _notice =
              'Version ${approved.version} for ${approved.activityName} is approved and effective.';
        });
        await _load(selectProfileId: approved.id);
      }
    } on MarineApiException catch (error) {
      if (mounted)
        setState(() {
          _busy = false;
          _error = error.message;
        });
    } catch (error) {
      if (mounted)
        setState(() {
          _busy = false;
          _error = 'The marine service could not be reached. Check the gateway address and try again.';
        });
    }
  }

  Future<SafetyProfileDraft?> _requestDraft({
    required String activityName,
    SafetyProfileDto? profile,
  }) => showDialog<SafetyProfileDraft>(
    context: context,
    builder: (context) =>
        MarineSafetyProfileEditor(activityName: activityName, profile: profile),
  );

  Future<bool> _confirm(String message) async {
    final result = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Deactivate profile?'),
        content: Text(message),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Deactivate'),
          ),
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
            if (_selected == null && !_loading && _profiles.isEmpty)
              _emptyCard(),
            if (widget.canManage) _createCard(),
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

  Widget _createCard() => Card(
    child: Padding(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const Text(
            'Create a safety profile draft',
            style: TextStyle(
              fontWeight: FontWeight.w800,
              color: Colors.blueAccent,
            ),
          ),
          const SizedBox(height: 8),
          DropdownButtonFormField<String>(
            value: _createActivityId.isEmpty ? null : _createActivityId,
            decoration: const InputDecoration(labelText: 'Activity'),
            items: widget.service.referenceActivities
                .map(
                  (activity) => DropdownMenuItem<String>(
                    value: activity['id'] as String,
                    child: Text(activity['name'] as String),
                  ),
                )
                .toList(),
            onChanged: _busy
                ? null
                : (value) => setState(() => _createActivityId = value ?? ''),
          ),
          const SizedBox(height: 12),
          OutlinedButton.icon(
            onPressed: _busy || _createActivityId.isEmpty
                ? null
                : () {
                    final activity = widget.service.referenceActivities
                        .firstWhere((item) => item['id'] == _createActivityId);
                    _createProfile(
                      _createActivityId,
                      activity['name'] as String,
                    );
                  },
            icon: const Icon(Icons.add),
            label: const Text('Create draft'),
          ),
          const SizedBox(height: 6),
          const Text(
            'Every limit needs a source and rationale. Another authorized manager must approve a draft before assessments can use it.',
            style: TextStyle(color: Colors.blueGrey, height: 1.35),
          ),
        ],
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
                const Text(
                  'PROFILES',
                  style: TextStyle(
                    color: Colors.blueAccent,
                    fontWeight: FontWeight.w800,
                    letterSpacing: 1.2,
                  ),
                ),
                Text(
                  '${_profiles.length} profiles',
                  style: const TextStyle(fontWeight: FontWeight.w700),
                ),
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
        backgroundColor: _selected?.id == profile.id
            ? Colors.blueAccent
            : Colors.transparent,
        child: _selected?.id == profile.id
            ? const Icon(Icons.radio_button_on, color: Colors.white)
            : const Icon(Icons.radio_button_off),
      ),
      title: Text(
        profile.activityName,
        style: const TextStyle(fontWeight: FontWeight.w700),
      ),
      trailing: Chip(
        label: Text(
          profile.reviewedAt == null
              ? 'REVIEW REQUIRED'
              : profile.isActive
              ? 'ACTIVE'
              : 'SUPERSEDED',
        ),
        backgroundColor: Colors.transparent,
      ),
      onTap: () => setState(() => _selected = profile),
    ),
  );

  Widget _detailCard() {
    final profile = _selected!;
    final hasCriteria = [
      profile.windCriteriaSource,
      profile.windCriteriaRationale,
      profile.waveCriteriaSource,
      profile.waveCriteriaRationale,
      profile.swellCriteriaSource,
      profile.swellCriteriaRationale,
    ].every((value) => value?.trim().isNotEmpty == true);
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
                Text(
                  'Version ${profile.version}',
                  style: const TextStyle(
                    fontWeight: FontWeight.w700,
                    color: Colors.blueAccent,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 8),
            _field('Max wind (km/h)', profile.maxWindSpeed),
            _field('Max wave (m)', profile.maxWaveHeight),
            _field('Max swell (m)', profile.maxSwellHeight),
            _field('Caution wind (km/h)', profile.cautionWindSpeed ?? 0),
            _field('Caution wave (m)', profile.cautionWaveHeight ?? 0),
            _field('Caution swell (m)', profile.cautionSwellHeight ?? 0),
            const Divider(height: 24),
            _textField('Wind source', profile.windCriteriaSource),
            _textField('Wind rationale', profile.windCriteriaRationale),
            _textField('Wave source', profile.waveCriteriaSource),
            _textField('Wave rationale', profile.waveCriteriaRationale),
            _textField('Swell source', profile.swellCriteriaSource),
            _textField('Swell rationale', profile.swellCriteriaRationale),
            _textField(
              'Review',
              profile.reviewedAt?.toIso8601String() ?? 'Pending review',
            ),
            const SizedBox(height: 12),
            if (widget.canManage) ...[
              if (profile.reviewedAt == null)
                if (!hasCriteria)
                  const Padding(
                    padding: EdgeInsets.only(bottom: 8),
                    child: Text(
                      'Add a source and rationale for every factor by saving a new version before review.',
                      style: TextStyle(color: Colors.blueGrey),
                    ),
                  )
                else
                  FilledButton.icon(
                    onPressed:
                        _busy || profile.createdByUserId == widget.currentUserId
                        ? null
                        : _reviewProfile,
                    icon: const Icon(Icons.fact_check_outlined),
                    label: const Text('Review and activate'),
                  ),
              if (profile.reviewedAt == null &&
                  profile.createdByUserId == widget.currentUserId)
                const Padding(
                  padding: EdgeInsets.only(top: 6, bottom: 8),
                  child: Text(
                    'A different authorized manager must review this version.',
                    style: TextStyle(color: Colors.blueGrey),
                  ),
                ),
              OutlinedButton.icon(
                onPressed: _busy ? null : _editProfile,
                icon: const Icon(Icons.edit_outlined),
                label: const Text('Create a new version'),
              ),
              if (profile.isActive) ...[
                const SizedBox(height: 8),
                FilledButton.icon(
                  onPressed: _busy ? null : _deactivateProfile,
                  icon: const Icon(Icons.block),
                  label: const Text('Deactivate profile'),
                ),
              ],
            ],
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
        Text(
          '$label: ',
          style: const TextStyle(
            fontWeight: FontWeight.w600,
            color: Colors.blueGrey,
          ),
        ),
        Expanded(
          child: Text(
            value.toStringAsFixed(2),
            style: const TextStyle(color: Colors.blueGrey),
          ),
        ),
      ],
    ),
  );

  Widget _textField(String label, String? value) => Padding(
    padding: const EdgeInsets.only(bottom: 8),
    child: Text(
      '$label: ${value?.trim().isNotEmpty == true ? value : 'Not recorded'}',
      style: const TextStyle(color: Colors.blueGrey),
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
            style: TextStyle(
              fontWeight: FontWeight.w800,
              color: Colors.blueAccent,
            ),
          ),
          const SizedBox(height: 6),
          const Text(
            'Safety profiles are versioned with source and rationale for every factor. A different authorized manager must approve each draft before it can support an assessment; older versions and assessment history are retained.',
            style: TextStyle(height: 1.4, color: Colors.blueGrey),
          ),
        ],
      ),
    ),
  );
}
