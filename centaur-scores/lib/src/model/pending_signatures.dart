/// A queued, not-yet-synced scorecard signature. Unlike [ScoreEdit], there is
/// no conflict-detection "old" value to track - signing has no conflict
/// resolution per documentation/SIGNING-SCORECARDS.md, so this is just the
/// payload waiting to be POSTed.
class PendingSignature {
  String? archerSignatureDataUrl;
  String? markerSignatureDataUrl;

  PendingSignature({this.archerSignatureDataUrl, this.markerSignatureDataUrl});

  factory PendingSignature.fromJson(Map<String, dynamic> json) => PendingSignature(
        archerSignatureDataUrl: json['archerSignatureDataUrl'] as String?,
        markerSignatureDataUrl: json['markerSignatureDataUrl'] as String?,
      );

  Map<String, dynamic> toJson() => {
        'archerSignatureDataUrl': archerSignatureDataUrl,
        'markerSignatureDataUrl': markerSignatureDataUrl,
      };
}

/// Locally queued, not-yet-synced signatures, keyed by matchParticipantId.
class PendingSignatures {
  final Map<String, PendingSignature> byParticipant;

  PendingSignatures({Map<String, PendingSignature>? byParticipant})
      : byParticipant = byParticipant ?? {};

  factory PendingSignatures.fromJson(Map<String, dynamic> json) {
    final result = <String, PendingSignature>{};
    for (final entry in json.entries) {
      result[entry.key] = PendingSignature.fromJson(entry.value as Map<String, dynamic>);
    }
    return PendingSignatures(byParticipant: result);
  }

  Map<String, dynamic> toJson() {
    final result = <String, dynamic>{};
    for (final entry in byParticipant.entries) {
      result[entry.key] = entry.value.toJson();
    }
    return result;
  }

  bool get isEmpty => byParticipant.isEmpty;
  bool get isNotEmpty => byParticipant.isNotEmpty;
}
