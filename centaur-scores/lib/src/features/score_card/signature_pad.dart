import 'dart:typed_data';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';

/// A single hand-drawn ink-capture pad. Hand-rolled (no signature-pad
/// package - see documentation/SIGNING-SCORECARDS.md's decision to keep this
/// app's minimal-dependency posture) using raw pointer events and a
/// [CustomPainter], with a white background so the exported PNG matches the
/// "black and white low resolution drawing" storage convention.
class SignaturePadController extends ChangeNotifier {
  final List<List<Offset>> strokes = [];
  List<Offset>? _currentStroke;
  final GlobalKey repaintKey = GlobalKey();

  bool get isEmpty => strokes.isEmpty;

  void startStroke(Offset point) {
    _currentStroke = [point];
    strokes.add(_currentStroke!);
    notifyListeners();
  }

  void extendStroke(Offset point) {
    _currentStroke?.add(point);
    notifyListeners();
  }

  void endStroke() {
    _currentStroke = null;
  }

  void clear() {
    strokes.clear();
    _currentStroke = null;
    notifyListeners();
  }

  /// Renders the pad's current strokes (via the [RepaintBoundary] it's
  /// wrapped in) to a PNG, or null if nothing has been drawn yet.
  Future<Uint8List?> toPngBytes() async {
    if (strokes.isEmpty) return null;
    final boundary =
        repaintKey.currentContext?.findRenderObject() as RenderRepaintBoundary?;
    if (boundary == null) return null;
    final image = await boundary.toImage(pixelRatio: 2.0);
    final byteData = await image.toByteData(format: ui.ImageByteFormat.png);
    return byteData?.buffer.asUint8List();
  }
}

class SignaturePad extends StatefulWidget {
  final SignaturePadController controller;

  const SignaturePad({super.key, required this.controller});

  @override
  State<SignaturePad> createState() => _SignaturePadState();
}

class _SignaturePadState extends State<SignaturePad> {
  @override
  void initState() {
    super.initState();
    widget.controller.addListener(_onControllerChanged);
  }

  @override
  void dispose() {
    widget.controller.removeListener(_onControllerChanged);
    super.dispose();
  }

  void _onControllerChanged() => setState(() {});

  @override
  Widget build(BuildContext context) {
    return RepaintBoundary(
      key: widget.controller.repaintKey,
      child: Container(
        color: Colors.white,
        child: Listener(
          onPointerDown: (event) => widget.controller.startStroke(event.localPosition),
          onPointerMove: (event) => widget.controller.extendStroke(event.localPosition),
          onPointerUp: (_) => widget.controller.endStroke(),
          onPointerCancel: (_) => widget.controller.endStroke(),
          child: CustomPaint(
            painter: _SignaturePainter(widget.controller.strokes),
            size: Size.infinite,
          ),
        ),
      ),
    );
  }
}

class _SignaturePainter extends CustomPainter {
  final List<List<Offset>> strokes;

  _SignaturePainter(this.strokes);

  @override
  void paint(Canvas canvas, Size size) {
    // A pointer that starts inside the pad keeps delivering move events
    // after it leaves the pad's bounds - clip so ink never spills outside.
    canvas.clipRect(Offset.zero & size);
    final linePaint = Paint()
      ..color = Colors.black
      ..strokeWidth = 2.5
      ..strokeCap = StrokeCap.round
      ..strokeJoin = StrokeJoin.round
      ..style = PaintingStyle.stroke;
    final dotPaint = Paint()
      ..color = Colors.black
      ..style = PaintingStyle.fill;

    for (final stroke in strokes) {
      if (stroke.length < 2) {
        if (stroke.isNotEmpty) canvas.drawCircle(stroke.first, linePaint.strokeWidth / 2, dotPaint);
        continue;
      }
      final path = Path()..moveTo(stroke.first.dx, stroke.first.dy);
      for (final point in stroke.skip(1)) {
        path.lineTo(point.dx, point.dy);
      }
      canvas.drawPath(path, linePaint);
    }
  }

  @override
  bool shouldRepaint(covariant _SignaturePainter oldDelegate) => true;
}
