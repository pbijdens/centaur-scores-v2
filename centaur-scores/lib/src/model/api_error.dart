import 'scorekeeper_score_conflict.dart';

/// Body shape of the server's non-conflict error responses, e.g.
/// `{"code": "MATCH_NO_LONGER_ACTIVE", "message": "..."}`.
class ApiError {
  final String? code;
  final String? message;

  ApiError({this.code, this.message});
}

/// Thrown by [CentaurScoresAPI] for any non-2xx HTTP response.
///
/// [conflicts] is only populated for a 409 `UPDATE_SCORE_CONFLICT` response
/// from `PUT .../scores`.
class ApiException implements Exception {
  final int status;
  final String? code;
  final String message;
  final List<ScoreConflictEntry>? conflicts;

  ApiException(this.status, this.code, this.message, {this.conflicts});

  @override
  String toString() =>
      'ApiException(status: $status, code: $code, message: $message)';
}

/// Thrown by [CentaurScoresAPI] when the request never reached the server
/// (no response at all - offline, DNS failure, timeout, etc.).
class NetworkException implements Exception {
  final String message;

  NetworkException(this.message);

  @override
  String toString() => 'NetworkException($message)';
}
