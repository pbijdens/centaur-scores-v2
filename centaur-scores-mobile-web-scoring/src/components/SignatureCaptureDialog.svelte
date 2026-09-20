<script lang="ts">
  import { t } from '../lib/i18n';

  interface Props {
    participantName: string;
    onConfirm: (archerDataUrl: string, markerDataUrl: string) => void;
    onCancel: () => void;
  }

  let { participantName, onConfirm, onCancel }: Props = $props();

  let archerCanvasEl = $state<HTMLCanvasElement | null>(null);
  let markerCanvasEl = $state<HTMLCanvasElement | null>(null);
  let archerHasContent = $state(false);
  let markerHasContent = $state(false);
  let error = $state('');

  // Plain (non-reactive) per-pad drawing state - only ever read/written
  // synchronously inside this pad's own pointer handlers, so it doesn't need
  // to trigger re-renders the way archer/markerHasContent does.
  type PadState = { drawing: boolean; lastX: number; lastY: number };
  const archerState: PadState = { drawing: false, lastX: 0, lastY: 0 };
  const markerState: PadState = { drawing: false, lastX: 0, lastY: 0 };

  function setupCanvas(canvas: HTMLCanvasElement) {
    const rect = canvas.getBoundingClientRect();
    const dpr = window.devicePixelRatio || 1;
    canvas.width = Math.max(1, Math.round(rect.width * dpr));
    canvas.height = Math.max(1, Math.round(rect.height * dpr));
    const ctx = canvas.getContext('2d');
    if (!ctx) return;
    ctx.scale(dpr, dpr);
    ctx.fillStyle = '#fff';
    ctx.fillRect(0, 0, rect.width, rect.height);
    ctx.strokeStyle = '#000';
    ctx.lineWidth = 2.5;
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';
  }

  $effect(() => {
    if (archerCanvasEl) setupCanvas(archerCanvasEl);
  });
  $effect(() => {
    if (markerCanvasEl) setupCanvas(markerCanvasEl);
  });

  function pointerPos(canvas: HTMLCanvasElement, event: PointerEvent) {
    const rect = canvas.getBoundingClientRect();
    return { x: event.clientX - rect.left, y: event.clientY - rect.top };
  }

  function makeHandlers(
    getCanvas: () => HTMLCanvasElement | null,
    state: PadState,
    markHasContent: (value: boolean) => void,
  ) {
    return {
      onPointerDown: (event: PointerEvent) => {
        const canvas = getCanvas();
        const ctx = canvas?.getContext('2d');
        if (!canvas || !ctx) return;
        event.preventDefault();
        canvas.setPointerCapture(event.pointerId);
        state.drawing = true;
        const { x, y } = pointerPos(canvas, event);
        state.lastX = x;
        state.lastY = y;
        // A single tap should still leave a visible dot.
        ctx.beginPath();
        ctx.arc(x, y, ctx.lineWidth / 2, 0, Math.PI * 2);
        ctx.fillStyle = '#000';
        ctx.fill();
        markHasContent(true);
      },
      onPointerMove: (event: PointerEvent) => {
        const canvas = getCanvas();
        const ctx = canvas?.getContext('2d');
        if (!canvas || !ctx || !state.drawing) return;
        event.preventDefault();
        const { x, y } = pointerPos(canvas, event);
        ctx.beginPath();
        ctx.moveTo(state.lastX, state.lastY);
        ctx.lineTo(x, y);
        ctx.stroke();
        state.lastX = x;
        state.lastY = y;
      },
      onPointerUp: () => {
        state.drawing = false;
      },
    };
  }

  const archerHandlers = makeHandlers(
    () => archerCanvasEl,
    archerState,
    (v) => (archerHasContent = v),
  );
  const markerHandlers = makeHandlers(
    () => markerCanvasEl,
    markerState,
    (v) => (markerHasContent = v),
  );

  function clearPad(canvas: HTMLCanvasElement | null, markHasContent: (value: boolean) => void) {
    if (!canvas) return;
    setupCanvas(canvas);
    markHasContent(false);
  }

  function confirm() {
    if (!archerHasContent || !markerHasContent || !archerCanvasEl || !markerCanvasEl) {
      error = $t('signatureMissingError');
      return;
    }
    error = '';
    onConfirm(archerCanvasEl.toDataURL('image/png'), markerCanvasEl.toDataURL('image/png'));
  }
</script>

<div class="overlay" role="alertdialog" aria-modal="true">
  <div class="dialog">
    <h2>{$t('signatureCaptureTitle')}</h2>
    <p class="participant-name">{participantName}</p>
    <p class="instructions">{$t('signatureCaptureInstructions')}</p>

    <div class="pad-block">
      <div class="pad-header">
        <span>{$t('archerSignature')}</span>
        <button type="button" class="text-button" onclick={() => clearPad(archerCanvasEl, (v) => (archerHasContent = v))}>
          {$t('clearSignature')}
        </button>
      </div>
      <div class="pad-frame">
        <canvas
          bind:this={archerCanvasEl}
          onpointerdown={archerHandlers.onPointerDown}
          onpointermove={archerHandlers.onPointerMove}
          onpointerup={archerHandlers.onPointerUp}
          onpointercancel={archerHandlers.onPointerUp}
        ></canvas>
      </div>
    </div>

    <div class="pad-block">
      <div class="pad-header">
        <span>{$t('markerSignature')}</span>
        <button type="button" class="text-button" onclick={() => clearPad(markerCanvasEl, (v) => (markerHasContent = v))}>
          {$t('clearSignature')}
        </button>
      </div>
      <div class="pad-frame">
        <canvas
          bind:this={markerCanvasEl}
          onpointerdown={markerHandlers.onPointerDown}
          onpointermove={markerHandlers.onPointerMove}
          onpointerup={markerHandlers.onPointerUp}
          onpointercancel={markerHandlers.onPointerUp}
        ></canvas>
      </div>
    </div>

    {#if error}<p class="error">{error}</p>{/if}

    <div class="choices">
      <button type="button" class="button secondary" onclick={onCancel}>{$t('cancel')}</button>
      <button type="button" class="button" onclick={confirm}>{$t('confirmAction')}</button>
    </div>
  </div>
</div>

<style lang="scss">
  @use '../styles/variables' as v;

  .overlay {
    position: fixed;
    inset: 0;
    background: rgba(0, 0, 0, 0.5);
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 100;
    padding: 1rem;
  }

  .dialog {
    width: 100%;
    max-width: 40rem;
    max-height: 92vh;
    overflow-y: auto;
    background: v.$color-surface;
    border-radius: v.$radius;
    padding: 1.25rem;
  }

  .participant-name {
    font-weight: 700;
    margin: 0 0 0.25rem;
  }

  .instructions {
    color: v.$color-text-muted;
    margin: 0 0 1rem;
  }

  .pad-block {
    margin-bottom: 1rem;
  }

  .pad-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    margin-bottom: 0.4rem;
    font-weight: 600;
  }

  .text-button {
    background: none;
    border: none;
    color: v.$color-primary;
    font-weight: 600;
    padding: 0.3rem 0.5rem;
  }

  .pad-frame {
    aspect-ratio: 5 / 1;
    border: 2px solid v.$color-border;
    border-radius: v.$radius;
    overflow: hidden;
    background: #fff;
  }

  .pad-frame canvas {
    width: 100%;
    height: 100%;
    display: block;
    touch-action: none;
  }

  .error {
    color: v.$color-danger;
    margin: 0.5rem 0 0;
  }

  .choices {
    display: flex;
    gap: 0.6rem;
    margin-top: 1rem;
  }

  .choices .button {
    flex: 1;
  }
</style>
