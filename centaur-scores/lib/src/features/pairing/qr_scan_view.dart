import 'package:flutter/material.dart';
import 'package:mobile_scanner/mobile_scanner.dart';

import 'package:centaur_scores/src/i18n/translations.dart';
import 'package:centaur_scores/src/repository/repository.dart';
import 'pairing_url_parser.dart';

/// First-run / re-pair gate: scan a QR code (or fall back to typing the URL)
/// to obtain the API base URL + optional language. Not part of
/// [AppNavigator]'s screen set - this is shown directly by [AppShell]
/// whenever `MatchRepository().isConfigured` is false.
class QrScanView extends StatefulWidget {
  const QrScanView({super.key});

  @override
  State<QrScanView> createState() => _QrScanViewState();
}

class _QrScanViewState extends State<QrScanView> {
  bool _manualEntry = false;
  bool _cameraUnavailable = false;
  String? _error;
  MobileScannerController? _controller;
  bool _handledOnce = false;

  @override
  void initState() {
    super.initState();
    _controller = MobileScannerController();
  }

  @override
  void dispose() {
    _controller?.dispose();
    super.dispose();
  }

  Future<void> _handleResult(PairingResult result) async {
    if (_handledOnce) return;
    _handledOnce = true;
    await _controller?.stop();
    await MatchRepository().configure(result.apiBaseUrl, result.language);
    await MatchRepository().fetchMatchInfo();
  }

  void _onDetect(BarcodeCapture capture) {
    if (_handledOnce) return;
    for (final barcode in capture.barcodes) {
      final raw = barcode.rawValue;
      if (raw == null) continue;
      try {
        final result = parsePairingUrl(raw);
        _handleResult(result);
        return;
      } on PairingUrlException {
        // Not a recognized pairing URL (could be an unrelated barcode) -
        // keep scanning rather than hard-failing on it.
        continue;
      }
    }
  }

  void _submitManualUrl(String raw) {
    final trimmed = raw.trim();
    try {
      final result = parsePairingUrl(trimmed);
      setState(() => _error = null);
      _handleResult(result);
      return;
    } on PairingUrlException {
      // Fall through - maybe a bare API base URL was pasted instead of a
      // full "...?api=...&language=..." string.
    }
    final bareUri = Uri.tryParse(trimmed);
    if (bareUri != null &&
        bareUri.hasScheme &&
        bareUri.hasAuthority &&
        !trimmed.contains('api=')) {
      setState(() => _error = null);
      _handleResult(PairingResult(trimmed, null));
      return;
    }
    setState(() => _error = t('invalidUrl'));
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(t('scanQrTitle'))),
      body: _manualEntry || _cameraUnavailable
          ? ManualEntryView(
              errorText: _error,
              onSubmit: _submitManualUrl,
              onBackToScanner: _cameraUnavailable
                  ? null
                  : () => setState(() => _manualEntry = false),
            )
          : _buildScanner(context),
    );
  }

  Widget _buildScanner(BuildContext context) {
    return Stack(
      children: [
        MobileScanner(
          controller: _controller,
          onDetect: _onDetect,
          errorBuilder: (context, error) {
            WidgetsBinding.instance.addPostFrameCallback((_) {
              if (mounted && !_cameraUnavailable) {
                setState(() => _cameraUnavailable = true);
              }
            });
            return Center(child: Text(t('cameraPermissionDenied')));
          },
        ),
        Positioned(
          left: 0,
          right: 0,
          bottom: 24,
          child: Column(children: [
            Container(
              margin: const EdgeInsets.symmetric(horizontal: 24),
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: Colors.black54,
                borderRadius: BorderRadius.circular(8),
              ),
              child: Text(t('scanQrInstructions'),
                  textAlign: TextAlign.center,
                  style: const TextStyle(color: Colors.white)),
            ),
            const SizedBox(height: 12),
            ElevatedButton(
              onPressed: () => setState(() => _manualEntry = true),
              child: Text(t('enterUrlManually')),
            ),
          ]),
        ),
      ],
    );
  }
}

/// Always-available fallback for the QR scanner - a real field-support path
/// (camera malfunction, no camera at all) as well as the way to pair on a
/// camera-less emulator during development, so it's never gated behind a
/// debug flag.
class ManualEntryView extends StatefulWidget {
  final void Function(String) onSubmit;
  final String? errorText;
  final VoidCallback? onBackToScanner;

  const ManualEntryView({
    super.key,
    required this.onSubmit,
    this.errorText,
    this.onBackToScanner,
  });

  @override
  State<ManualEntryView> createState() => _ManualEntryViewState();
}

class _ManualEntryViewState extends State<ManualEntryView> {
  final _controller = TextEditingController();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 480),
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextField(
                controller: _controller,
                minLines: 2,
                maxLines: 4,
                decoration: InputDecoration(
                  labelText: t('manualUrlLabel'),
                  border: const OutlineInputBorder(),
                  errorText: widget.errorText,
                ),
              ),
              const SizedBox(height: 16),
              ElevatedButton(
                onPressed: () => widget.onSubmit(_controller.text),
                child: Text(t('manualUrlSubmit')),
              ),
              if (widget.onBackToScanner != null) ...[
                const SizedBox(height: 8),
                TextButton(
                  onPressed: widget.onBackToScanner,
                  child: Text(t('scanQrTitle')),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
