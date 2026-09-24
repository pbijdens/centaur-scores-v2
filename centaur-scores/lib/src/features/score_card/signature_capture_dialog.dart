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
/// aspect ratio each, so the stack reads as 5:2, scaled to fit the screen
/// without scrolling), one for the archer and one
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
    // Nothing in here may scroll: a scroll view would compete with the pads
    // for vertical drags, so signing would scroll the page instead of (or as
    // well as) drawing. Instead the pads are sized to whatever height is
    // left, which matters most in landscape.
    return Dialog.fullscreen(
      child: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('${t('signatureCaptureTitle')} - ${widget.participantName}',
                            style: Theme.of(context).textTheme.titleLarge),
                        Text(t('signatureCaptureInstructions')),
                      ],
                    ),
                  ),
                  const SizedBox(width: 8),
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
              if (_error != null)
                Padding(
                  padding: const EdgeInsets.only(top: 8),
                  child: Text(_error!, style: const TextStyle(color: Colors.red)),
                ),
              const SizedBox(height: 8),
              Expanded(
                child: LayoutBuilder(builder: (context, constraints) {
                  // Largest 5:1 pad size where both pads plus their label
                  // rows fit without overflowing.
                  final byHeight = (constraints.maxHeight - 2 * _labelRowHeight - _padGap) / 2;
                  final byWidth = constraints.maxWidth / 5;
                  final padHeight = (byHeight < byWidth ? byHeight : byWidth).clamp(0.0, double.infinity);
                  final padWidth = padHeight * 5;
                  return Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      _padBlock(t('archerSignature'), _archerController, padWidth, padHeight),
                      const SizedBox(height: _padGap),
                      _padBlock(t('markerSignature'), _markerController, padWidth, padHeight),
                    ],
                  );
                }),
              ),
            ],
          ),
        ),
      ),
    );
  }

  static const double _labelRowHeight = 40;
  static const double _padGap = 12;

  Widget _padBlock(String label, SignaturePadController controller, double width, double height) {
    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        SizedBox(
          width: width,
          height: _labelRowHeight,
          child: Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(label, style: const TextStyle(fontWeight: FontWeight.bold)),
              TextButton(onPressed: controller.clear, child: Text(t('clearSignature'))),
            ],
          ),
        ),
        Container(
          width: width,
          height: height,
          decoration: BoxDecoration(border: Border.all(color: Colors.black45)),
          child: SignaturePad(controller: controller),
        ),
      ],
    );
  }
}
