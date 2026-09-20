import 'dart:convert';

import 'package:centaur_scores/src/i18n/translations.dart';
import 'package:flutter/material.dart';

import 'signature_pad.dart';

class SignatureCaptureResult {
  final String? archerDataUrl;
  final String? markerDataUrl;

  SignatureCaptureResult({this.archerDataUrl, this.markerDataUrl});
}

/// Full-screen split signature capture: two stacked ink-capture pads (5:1
/// aspect ratio each, so the stack reads as 5:2), one for the archer and one
/// for the marker, per documentation/SIGNING-SCORECARDS.md. Pops with a
/// [SignatureCaptureResult] on confirm (both signatures required), or null
/// on cancel.
class SignatureCaptureDialog extends StatefulWidget {
  final String participantName;

  const SignatureCaptureDialog({super.key, required this.participantName});

  @override
  State<SignatureCaptureDialog> createState() => _SignatureCaptureDialogState();
}

class _SignatureCaptureDialogState extends State<SignatureCaptureDialog> {
  final _archerController = SignaturePadController();
  final _markerController = SignaturePadController();
  String? _error;
  bool _saving = false;

  @override
  void dispose() {
    _archerController.dispose();
    _markerController.dispose();
    super.dispose();
  }

  Future<void> _confirm() async {
    if (_archerController.isEmpty || _markerController.isEmpty) {
      setState(() => _error = t('signatureMissingError'));
      return;
    }
    setState(() {
      _error = null;
      _saving = true;
    });
    final archerBytes = await _archerController.toPngBytes();
    final markerBytes = await _markerController.toPngBytes();
    if (!mounted) return;
    Navigator.of(context).pop(SignatureCaptureResult(
      archerDataUrl: archerBytes != null ? 'data:image/png;base64,${base64Encode(archerBytes)}' : null,
      markerDataUrl: markerBytes != null ? 'data:image/png;base64,${base64Encode(markerBytes)}' : null,
    ));
  }

  @override
  Widget build(BuildContext context) {
    return Dialog.fullscreen(
      child: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(t('signatureCaptureTitle'), style: Theme.of(context).textTheme.titleLarge),
              const SizedBox(height: 4),
              Text(widget.participantName, style: Theme.of(context).textTheme.bodyMedium),
              const SizedBox(height: 8),
              Text(t('signatureCaptureInstructions')),
              const SizedBox(height: 16),
              Expanded(
                child: SingleChildScrollView(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      _padBlock(t('archerSignature'), _archerController),
                      const SizedBox(height: 20),
                      _padBlock(t('markerSignature'), _markerController),
                    ],
                  ),
                ),
              ),
              if (_error != null)
                Padding(
                  padding: const EdgeInsets.only(top: 8),
                  child: Text(_error!, style: const TextStyle(color: Colors.red)),
                ),
              const SizedBox(height: 16),
              Row(
                mainAxisAlignment: MainAxisAlignment.end,
                children: [
                  TextButton(
                    onPressed: _saving ? null : () => Navigator.of(context).pop(),
                    child: Text(t('cancel')),
                  ),
                  const SizedBox(width: 8),
                  ElevatedButton(
                    onPressed: _saving ? null : _confirm,
                    child: Text(t('confirmAction')),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _padBlock(String label, SignaturePadController controller) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Text(label, style: const TextStyle(fontWeight: FontWeight.bold)),
            TextButton(onPressed: controller.clear, child: Text(t('clearSignature'))),
          ],
        ),
        AspectRatio(
          aspectRatio: 5 / 1,
          child: Container(
            decoration: BoxDecoration(border: Border.all(color: Colors.black45)),
            child: SignaturePad(controller: controller),
          ),
        ),
      ],
    );
  }
}
