import 'package:flutter_test/flutter_test.dart';
import 'package:centaur_scores/src/features/pairing/pairing_url_parser.dart';

void main() {
  group('parsePairingUrl', () {
    test('parses the reference example URL', () {
      const url =
          'http://localhost:5173/scores?api=http%3A%2F%2F127.0.0.1%3A5080%2Fscorekeeper%2F47dd598e-f72c-4220-98d5-0a7282f1e794%2Ff22d95d1-7b79-48a2-a30b-ab2d45ce2997%2Fa7213111-d89b-4859-8d2c-22e0323a3fe9&language=NL';
      final result = parsePairingUrl(url);
      expect(result.apiBaseUrl,
          'http://127.0.0.1:5080/scorekeeper/47dd598e-f72c-4220-98d5-0a7282f1e794/f22d95d1-7b79-48a2-a30b-ab2d45ce2997/a7213111-d89b-4859-8d2c-22e0323a3fe9');
      expect(result.language, 'NL');
    });

    test('recognizes EN case-insensitively', () {
      final result = parsePairingUrl('http://x/?api=http%3A%2F%2Fa.b%2Fc&language=en');
      expect(result.language, 'EN');
    });

    test('any other non-empty language value maps to NL', () {
      final result = parsePairingUrl('http://x/?api=http%3A%2F%2Fa.b%2Fc&language=FR');
      expect(result.language, 'NL');
    });

    test('missing language leaves it null (do not touch persisted language)', () {
      final result = parsePairingUrl('http://x/?api=http%3A%2F%2Fa.b%2Fc');
      expect(result.language, isNull);
    });

    test('missing api parameter throws', () {
      expect(() => parsePairingUrl('http://x/?language=NL'),
          throwsA(isA<PairingUrlException>()));
    });

    test('non-URL api value throws', () {
      expect(() => parsePairingUrl('http://x/?api=not-a-url'),
          throwsA(isA<PairingUrlException>()));
    });

    test('garbage input throws', () {
      expect(() => parsePairingUrl(''), throwsA(isA<PairingUrlException>()));
    });
  });
}
