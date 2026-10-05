import 'package:flutter/material.dart';

import '../../data/models/marine_models.dart';

/// Collects a complete profile version, including the cited basis for each
/// factor. It returns a draft only after all values pass client-side checks;
/// the backend remains authoritative.
class MarineSafetyProfileEditor extends StatefulWidget {
  const MarineSafetyProfileEditor({
    super.key,
    required this.activityName,
    this.profile,
  });

  final String activityName;
  final SafetyProfileDto? profile;

  @override
  State<MarineSafetyProfileEditor> createState() =>
      _MarineSafetyProfileEditorState();
}

class _MarineSafetyProfileEditorState extends State<MarineSafetyProfileEditor> {
  final _formKey = GlobalKey<FormState>();
  final Map<String, TextEditingController> _controllers = {};
  String? _error;

  @override
  void initState() {
    super.initState();
    final profile = widget.profile;
    final values = <String, String>{
      'maxWind': profile?.maxWindSpeed.toString() ?? '',
      'maxWave': profile?.maxWaveHeight.toString() ?? '',
      'maxSwell': profile?.maxSwellHeight.toString() ?? '',
      'cautionWind': profile?.cautionWindSpeed?.toString() ?? '',
      'cautionWave': profile?.cautionWaveHeight?.toString() ?? '',
      'cautionSwell': profile?.cautionSwellHeight?.toString() ?? '',
      'windSource': profile?.windCriteriaSource ?? '',
      'windRationale': profile?.windCriteriaRationale ?? '',
      'waveSource': profile?.waveCriteriaSource ?? '',
      'waveRationale': profile?.waveCriteriaRationale ?? '',
      'swellSource': profile?.swellCriteriaSource ?? '',
      'swellRationale': profile?.swellCriteriaRationale ?? '',
    };
    for (final entry in values.entries) {
      _controllers[entry.key] = TextEditingController(text: entry.value);
    }
  }

  @override
  void dispose() {
    for (final controller in _controllers.values) {
      controller.dispose();
    }
    super.dispose();
  }

  String? _limitError(
    String? value, {
    required bool optional,
    required double maximum,
  }) {
    final raw = value?.trim() ?? '';
    if (raw.isEmpty && optional) return null;
    final parsed = double.tryParse(raw);
    if (parsed == null ||
        !parsed.isFinite ||
        parsed < 0.01 ||
        parsed > maximum) {
      return 'Enter a value from 0.01 to ${maximum.toStringAsFixed(0)}.';
    }
    return null;
  }

  Widget _limitField(
    String key,
    String label,
    double maximum, {
    bool optional = false,
  }) => TextFormField(
    controller: _controllers[key],
    keyboardType: const TextInputType.numberWithOptions(decimal: true),
    decoration: InputDecoration(
      labelText: label,
      border: const OutlineInputBorder(),
    ),
    validator: (value) =>
        _limitError(value, optional: optional, maximum: maximum),
  );

  Widget _evidenceField(
    String key,
    String label, {
    required int minLength,
    required int maxLength,
    bool multiline = false,
  }) => TextFormField(
    controller: _controllers[key],
    maxLength: maxLength,
    minLines: multiline ? 2 : 1,
    maxLines: multiline ? 4 : 2,
    decoration: InputDecoration(
      labelText: label,
      border: const OutlineInputBorder(),
    ),
    validator: (value) {
      final length = value?.trim().length ?? 0;
      if (length < minLength || length > maxLength)
        return 'Use $minLength to $maxLength characters.';
      return null;
    },
  );

  void _submit() {
    if (!_formKey.currentState!.validate()) return;
    final maxWind = double.parse(_controllers['maxWind']!.text.trim());
    final maxWave = double.parse(_controllers['maxWave']!.text.trim());
    final maxSwell = double.parse(_controllers['maxSwell']!.text.trim());
    double? optional(String key) => _controllers[key]!.text.trim().isEmpty
        ? null
        : double.parse(_controllers[key]!.text.trim());
    final cautionWind = optional('cautionWind');
    final cautionWave = optional('cautionWave');
    final cautionSwell = optional('cautionSwell');
    if ((cautionWind != null && cautionWind >= maxWind) ||
        (cautionWave != null && cautionWave >= maxWave) ||
        (cautionSwell != null && cautionSwell >= maxSwell)) {
      setState(
        () => _error = 'Each caution value must be below its maximum limit.',
      );
      return;
    }
    Navigator.pop(
      context,
      SafetyProfileDraft(
        maxWindSpeed: maxWind,
        maxWaveHeight: maxWave,
        maxSwellHeight: maxSwell,
        cautionWindSpeed: cautionWind,
        cautionWaveHeight: cautionWave,
        cautionSwellHeight: cautionSwell,
        windCriteriaSource: _controllers['windSource']!.text.trim(),
        windCriteriaRationale: _controllers['windRationale']!.text.trim(),
        waveCriteriaSource: _controllers['waveSource']!.text.trim(),
        waveCriteriaRationale: _controllers['waveRationale']!.text.trim(),
        swellCriteriaSource: _controllers['swellSource']!.text.trim(),
        swellCriteriaRationale: _controllers['swellRationale']!.text.trim(),
      ),
    );
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: Text(
      '${widget.profile == null ? 'New' : 'New version'} · ${widget.activityName}',
    ),
    content: SizedBox(
      width: 560,
      child: Form(
        key: _formKey,
        child: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              _limitField('maxWind', 'Maximum wind (km/h)', 1000),
              _limitField('maxWave', 'Maximum wave (m)', 50),
              _limitField('maxSwell', 'Maximum swell (m)', 50),
              _limitField(
                'cautionWind',
                'Caution wind (km/h, optional)',
                1000,
                optional: true,
              ),
              _limitField(
                'cautionWave',
                'Caution wave (m, optional)',
                50,
                optional: true,
              ),
              _limitField(
                'cautionSwell',
                'Caution swell (m, optional)',
                50,
                optional: true,
              ),
              const SizedBox(height: 12),
              _evidenceField(
                'windSource',
                'Wind source reference',
                minLength: 3,
                maxLength: 512,
              ),
              _evidenceField(
                'windRationale',
                'Wind rationale',
                minLength: 10,
                maxLength: 2000,
                multiline: true,
              ),
              _evidenceField(
                'waveSource',
                'Wave source reference',
                minLength: 3,
                maxLength: 512,
              ),
              _evidenceField(
                'waveRationale',
                'Wave rationale',
                minLength: 10,
                maxLength: 2000,
                multiline: true,
              ),
              _evidenceField(
                'swellSource',
                'Swell source reference',
                minLength: 3,
                maxLength: 512,
              ),
              _evidenceField(
                'swellRationale',
                'Swell rationale',
                minLength: 10,
                maxLength: 2000,
                multiline: true,
              ),
              if (_error != null)
                Align(
                  alignment: Alignment.centerLeft,
                  child: Text(
                    _error!,
                    style: const TextStyle(color: Colors.redAccent),
                  ),
                ),
            ],
          ),
        ),
      ),
    ),
    actions: [
      TextButton(
        onPressed: () => Navigator.pop(context),
        child: const Text('Cancel'),
      ),
      FilledButton(onPressed: _submit, child: const Text('Save draft')),
    ],
  );
}
