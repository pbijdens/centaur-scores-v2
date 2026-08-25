import 'package:centaur_scores/src/model/api_error.dart';
import 'package:centaur_scores/src/model/scorekeeper_match.dart';
import 'package:centaur_scores/src/model/scorekeeper_participant_options.dart';
import 'package:centaur_scores/src/model/scorekeeper_participant_update.dart';
import 'package:centaur_scores/src/model/scorekeeper_score_conflict.dart';
import 'package:centaur_scores/src/model/scorekeeper_score_update.dart';
import 'package:centaur_scores/src/repository/modelstore.dart';
import 'package:http/http.dart' as http;
import 'package:http/io_client.dart';
import 'dart:convert';
import 'dart:developer';
import 'dart:io';

/// Client for the new public scorekeeper API
/// (`/scorekeeper/{tenantId}/{matchId}/{deviceId}/...`, see
/// PUBLIC-API-DESIGN.md). The base URL (up to and including the device ID)
/// is whatever was scanned/entered during pairing, re-read from
/// [ModelStore] on every call so a re-pair takes effect immediately.
class CentaurScoresAPI {
  static final CentaurScoresAPI _instance = CentaurScoresAPI._internal();

  factory CentaurScoresAPI() {
    return _instance;
  }

  CentaurScoresAPI._internal() {
    log("CentaurScoresAPI was created.");
  }

  // --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- ---

  final ModelStore _store = ModelStore();

  Future<ScorekeeperMatch> getMatchInfo() async {
    final json = await _request('GET', '');
    return ScorekeeperMatch.fromJson(json as Map<String, dynamic>);
  }

  Future<void> putParticipants(List<ScorekeeperParticipantUpdate> participants) async {
    await _request('PUT', '/participants',
        body: participants.map((p) => p.toJson()).toList());
  }

  Future<void> putScores(List<ParticipantScoreUpdates> updates) async {
    await _request('PUT', '/scores',
        body: updates.map((u) => u.toJson()).toList());
  }

  Future<ScorekeeperParticipantOptions> getParticipantOptions() async {
    final json = await _request('GET', '/participant-options');
    return ScorekeeperParticipantOptions.fromJson(json as Map<String, dynamic>);
  }

  Future<DateTime> getTime() async {
    final json = await _request('GET', '/time') as Map<String, dynamic>;
    return DateTime.parse(json['time'] as String);
  }

  // --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- --- ---

  Future<dynamic> _request(String method, String pathSuffix, {dynamic body}) async {
    final baseUrl = await _store.getApiBaseUrl();
    if (baseUrl == null) {
      throw NetworkException('No API base URL configured - device is not paired yet.');
    }
    final uri = Uri.parse('$baseUrl$pathSuffix');
    final headers = <String, String>{'accept': 'application/json'};
    if (body != null) headers['content-type'] = 'application/json';

    http.Response response;
    // Certificate verification is disabled: many target devices (min SDK 23)
    // ship with outdated CA trust stores that can't validate current Let's
    // Encrypt chains, and pinning a specific root would force a redeploy
    // whenever the server's issuing CA changes.
    final rawClient = HttpClient()
      ..badCertificateCallback = (cert, host, port) => true;
    final client = IOClient(rawClient);
    try {
      switch (method) {
        case 'GET':
          response = await client.get(uri, headers: headers);
          break;
        case 'PUT':
          response = await client.put(uri,
              headers: headers, body: body != null ? jsonEncode(body) : null);
          break;
        default:
          throw ArgumentError('Unsupported method $method');
      }
    } catch (error) {
      throw NetworkException('Request to $uri failed: $error');
    } finally {
      client.close();
    }

    if (response.statusCode >= 200 && response.statusCode < 300) {
      final bodyText = const Utf8Decoder().convert(response.bodyBytes);
      if (bodyText.isEmpty) return null;
      return jsonDecode(bodyText);
    }

    throw _parseErrorResponse(response);
  }

  ApiException _parseErrorResponse(http.Response response) {
    final bodyText = const Utf8Decoder().convert(response.bodyBytes);
    if (bodyText.isEmpty) {
      return ApiException(response.statusCode, null, 'HTTP ${response.statusCode}');
    }

    dynamic decoded;
    try {
      decoded = jsonDecode(bodyText);
    } catch (_) {
      return ApiException(response.statusCode, null, bodyText);
    }

    if (decoded is! Map<String, dynamic>) {
      return ApiException(response.statusCode, null, bodyText);
    }

    // Two known shapes: a bare {code, message} (most errors), or a nested
    // {error: {code, message}, conflicts: [...]} (PUT /scores 409). Parse
    // both tolerantly.
    String? code;
    String message = bodyText;
    List<ScoreConflictEntry>? conflicts;

    final errorField = decoded['error'];
    if (errorField is Map<String, dynamic>) {
      code = errorField['code'] as String?;
      message = (errorField['message'] as String?) ?? message;
    } else {
      code = decoded['code'] as String?;
      message = (decoded['message'] as String?) ?? message;
    }

    final conflictsField = decoded['conflicts'];
    if (conflictsField is List) {
      conflicts = conflictsField
          .map((e) => ScoreConflictEntry.fromJson(e as Map<String, dynamic>))
          .toList();
    }

    return ApiException(response.statusCode, code, message, conflicts: conflicts);
  }
}
