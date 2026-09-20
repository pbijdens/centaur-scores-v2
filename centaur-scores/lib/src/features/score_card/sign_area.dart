import 'dart:convert';
import 'dart:typed_data';

import 'package:centaur_scores/src/i18n/translations.dart';
import 'package:centaur_scores/src/model/scorekeeper_match.dart';
import 'package:centaur_scores/src/model/scorekeeper_match_participant.dart';
import 'package:centaur_scores/src/repository/repository.dart';
import 'package:centaur_scores/src/style/style_helper.dart';
import 'package:flutter/material.dart';

import 'signature_capture_dialog.dart';

/// Persistent per-participant sign area, rendered below the score entry
/// column (see documentation/SIGNING-SCORECARDS.md). Hidden entirely when
/// the match doesn't require signing; otherwise always present - a "Sign"
/// button (present even for an incomplete card, since matches can be
/// stopped early) until signed, then either the recorded signature images
/// ("signature" mode) or a plain read-only notice ("confirm" mode, which
/// never captures images).
class SignArea extends StatelessWidget {
  final ScorekeeperMatch model;
  final ScorekeeperMatchParticipant participant;

  const SignArea({super.key, required this.model, required this.participant});

  @override
  Widget build(BuildContext context) {
    if (model.signatureMode == 'none') return const SizedBox.shrink();
    final width = StyleHelper.scoreCardColumnWidth(context, model);
    return Container(
      width: width,
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 8),
      child: participant.signed ? _signedContent(context) : _signButton(context),
    );
  }

  Widget _signButton(BuildContext context) {
    return SizedBox(
      width: double.infinity,
      child: ElevatedButton(
        onPressed: () => _onSignPressed(context),
        child: Text(t('sign')),
      ),
    );
  }

  Widget _signedContent(BuildContext context) {
    final archer = participant.archerSignatureDataUrl;
    final marker = participant.markerSignatureDataUrl;
    if (archer == null && marker == null) {
      return Text(
        t('signedReadOnly'),
        textAlign: TextAlign.center,
        style: const TextStyle(fontStyle: FontStyle.italic),
      );
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (archer != null) ...[_signatureThumb(t('archerSignature'), archer), const SizedBox(height: 6)],
        if (marker != null) _signatureThumb(t('markerSignature'), marker),
      ],
    );
  }

  Widget _signatureThumb(String label, String dataUrl) {
    final bytes = _decodeDataUrl(dataUrl);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: const TextStyle(fontSize: 11)),
        if (bytes != null)
          Container(
            decoration: BoxDecoration(border: Border.all(color: Colors.black26)),
            child: AspectRatio(aspectRatio: 5 / 1, child: Image.memory(bytes, fit: BoxFit.contain)),
          ),
      ],
    );
  }

  Uint8List? _decodeDataUrl(String dataUrl) {
    final commaIndex = dataUrl.indexOf(',');
    if (commaIndex < 0) return null;
    try {
      return base64Decode(dataUrl.substring(commaIndex + 1));
    } catch (_) {
      return null;
    }
  }

  Future<void> _onSignPressed(BuildContext context) async {
    if (model.signatureMode == 'signature') {
      final result = await showDialog<SignatureCaptureResult>(
        context: context,
        builder: (context) => SignatureCaptureDialog(participantName: participant.name),
      );
      if (result == null) return;
      MatchRepository().recordSignature(participant.matchParticipantId,
          archerSignatureDataUrl: result.archerDataUrl, markerSignatureDataUrl: result.markerDataUrl);
      return;
    }

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: Text(t('signConfirmTitle')),
        content: Text(t('signConfirmBody')),
        actions: [
          TextButton(onPressed: () => Navigator.of(context).pop(false), child: Text(t('cancel'))),
          ElevatedButton(onPressed: () => Navigator.of(context).pop(true), child: Text(t('confirmAction'))),
        ],
      ),
    );
    if (confirmed == true) {
      MatchRepository().recordSignature(participant.matchParticipantId);
    }
  }
}
