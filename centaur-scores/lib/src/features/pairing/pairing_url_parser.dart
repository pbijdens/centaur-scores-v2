/// Result of successfully parsing a scanned/entered pairing URL.
class PairingResult {
  final String apiBaseUrl;
  final String? language;

  const PairingResult(this.apiBaseUrl, this.language);
}

class PairingUrlException implements Exception {
  final String reason;

  PairingUrlException(this.reason);

  @override
  String toString() => 'PairingUrlException($reason)';
}

/// Parses a scanned/entered URL of the form
/// `http://host/scores?api=<url-encoded-api-base>&language=NL|EN`.
///
/// [language] resolution matches the reference web client exactly: an
/// explicit case-insensitive "EN" -> `'EN'`; any other present value ->
/// `'NL'`; absent entirely -> `null`, meaning "leave the currently
/// persisted language untouched" (not "default to NL" - that default is
/// applied elsewhere, at first-ever pairing).
PairingResult parsePairingUrl(String raw) {
  final trimmed = raw.trim();
  if (trimmed.isEmpty) {
    throw PairingUrlException('Empty input');
  }

  Uri uri;
  try {
    uri = Uri.parse(trimmed);
  } on FormatException {
    throw PairingUrlException('Not a valid URL');
  }

  final apiParam = uri.queryParameters['api'];
  if (apiParam == null || apiParam.isEmpty) {
    throw PairingUrlException('Missing "api" query parameter');
  }

  final apiUri = Uri.tryParse(apiParam);
  if (apiUri == null || !apiUri.hasScheme || !apiUri.hasAuthority) {
    throw PairingUrlException('"api" parameter is not an absolute URL');
  }

  String? language;
  final languageParam = uri.queryParameters['language'];
  if (languageParam != null && languageParam.isNotEmpty) {
    language = languageParam.toUpperCase() == 'EN' ? 'EN' : 'NL';
  }

  return PairingResult(apiParam, language);
}
