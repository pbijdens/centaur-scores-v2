import 'package:flutter/material.dart';

import 'package:centaur_scores/src/i18n/translations.dart';
import 'package:centaur_scores/src/model/participant_category_value.dart';
import 'package:centaur_scores/src/model/scorekeeper_match.dart';

/// Shared federation-number/name/per-category form, used by both the
/// "Add unlisted participant" sub-form and the Edit Participant screen.
/// Save is only enabled once a name and every category are set.
class ParticipantForm extends StatefulWidget {
  final ScorekeeperMatch match;
  final String? initialFederationNumber;
  final String? initialName;
  final Map<String, String> initialCategoryValues; // categoryId -> value name

  final void Function(String? federationNumber, String name,
      List<ParticipantCategoryValue> categories) onSave;
  final VoidCallback onCancel;

  const ParticipantForm({
    super.key,
    required this.match,
    this.initialFederationNumber,
    this.initialName,
    this.initialCategoryValues = const {},
    required this.onSave,
    required this.onCancel,
  });

  @override
  State<ParticipantForm> createState() => _ParticipantFormState();
}

class _ParticipantFormState extends State<ParticipantForm> {
  late final TextEditingController _federationController;
  late final TextEditingController _nameController;
  late final Map<String, String?> _selectedValues;

  @override
  void initState() {
    super.initState();
    _federationController = TextEditingController(text: widget.initialFederationNumber ?? '');
    _nameController = TextEditingController(text: widget.initialName ?? '');
    _selectedValues = {
      for (final category in widget.match.categories)
        category.id: widget.initialCategoryValues[category.id],
    };
    _nameController.addListener(() => setState(() {}));
  }

  @override
  void dispose() {
    _federationController.dispose();
    _nameController.dispose();
    super.dispose();
  }

  bool get _isValid =>
      _nameController.text.trim().isNotEmpty &&
      _selectedValues.values.every((v) => v != null);

  void _submit() {
    final categories = widget.match.categories
        .map((category) => ParticipantCategoryValue.create(
            category.id, category.name, _selectedValues[category.id]))
        .toList();
    widget.onSave(
        _federationController.text.trim().isEmpty ? null : _federationController.text.trim(),
        _nameController.text.trim(),
        categories);
  }

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TextField(
            controller: _federationController,
            decoration: InputDecoration(labelText: t('federationNumber')),
          ),
          const SizedBox(height: 12),
          TextField(
            controller: _nameController,
            decoration: InputDecoration(labelText: t('fullName')),
          ),
          const SizedBox(height: 12),
          ...widget.match.categories.map((category) => Padding(
                padding: const EdgeInsets.only(bottom: 12),
                child: DropdownButtonFormField<String>(
                  value: _selectedValues[category.id],
                  decoration: InputDecoration(labelText: category.name),
                  hint: Text(t('categoryRequired')),
                  items: category.values
                      .map((value) => DropdownMenuItem(
                            value: value.name,
                            child: Text(value.name),
                          ))
                      .toList(),
                  onChanged: (value) => setState(() => _selectedValues[category.id] = value),
                ),
              )),
          const SizedBox(height: 12),
          Row(
            mainAxisAlignment: MainAxisAlignment.end,
            children: [
              TextButton(onPressed: widget.onCancel, child: Text(t('cancel'))),
              const SizedBox(width: 8),
              ElevatedButton(
                onPressed: _isValid ? _submit : null,
                child: Text(t('save')),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

