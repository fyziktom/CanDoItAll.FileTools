const states = new WeakMap();

const wheelModes = Object.freeze({
    disabled: 0,
    zoom: 1,
    controlKey: 2,
});

const zoomEpsilon = 0.0005;
const overflowTolerance = 0.5;
const maximumPlaneSize = 100000;
const lengthUnits = Object.freeze({
    px: 1,
    pt: 96 / 72,
    pc: 16,
    in: 96,
    cm: 96 / 2.54,
    mm: 96 / 25.4,
    q: 96 / 101.6,
    em: 16,
    rem: 16,
    ex: 8,
});

const interactiveSelector = [
    'a',
    'button',
    'input',
    'select',
    'textarea',
    '[contenteditable="true"]',
    '[data-cdi-ft-zoom-pan-interactive]',
].join(',');

export function initialize(viewport, content, options) {
    if (!viewport || !content) {
        throw new Error('FileZoomPanFrame requires both viewport and content elements.');
    }

    destroy(viewport);
    const state = {
        viewport,
        content,
        options: normalizeOptions(options),
        natural: null,
        fit: 1,
        zoom: 1,
        x: 0,
        y: 0,
        overflowX: false,
        overflowY: false,
        pointers: new Map(),
        lastPointer: null,
        pinch: null,
        cleanup: [],
        resizeObserver: null,
        mutationObserver: null,
        measureQueued: false,
    };

    states.set(viewport, state);
    attachListeners(state);
    observe(state);
    measure(state);
}

export function configure(viewport, options) {
    const state = states.get(viewport);
    if (!state) {
        return;
    }

    state.options = normalizeOptions(options);
    state.zoom = clamp(state.zoom, state.options.minimumZoom, state.options.maximumZoom);
    if (!state.options.enabled) {
        resetView(state);
    }

    applyTransform(state);
}

export function zoomBy(viewport, factor) {
    const state = states.get(viewport);
    if (!state?.options.enabled) {
        return false;
    }

    return zoomAt(state, Number(factor), viewportCenter(state));
}

export function reset(viewport) {
    const state = states.get(viewport);
    if (!state) {
        return;
    }

    resetView(state);
    applyTransform(state);
}

export function describe(viewport) {
    const state = states.get(viewport);
    if (!state) {
        return null;
    }

    const plane = planeSize(state);
    return {
        zoom: state.zoom,
        fit: state.fit,
        scale: state.fit * state.zoom,
        x: state.x,
        y: state.y,
        naturalWidth: state.natural?.width ?? null,
        naturalHeight: state.natural?.height ?? null,
        planeWidth: plane.width,
        planeHeight: plane.height,
        viewportWidth: state.viewport.clientWidth,
        viewportHeight: state.viewport.clientHeight,
        overflowX: state.overflowX,
        overflowY: state.overflowY,
    };
}

export function destroy(viewport) {
    const state = states.get(viewport);
    if (!state) {
        return;
    }

    for (const cleanup of state.cleanup) {
        cleanup();
    }

    state.resizeObserver?.disconnect();
    state.mutationObserver?.disconnect();
    state.content.style.removeProperty('transform');
    state.content.style.removeProperty('inline-size');
    state.content.style.removeProperty('block-size');
    for (const name of ['data-cdi-ft-zoom', 'data-cdi-ft-scale', 'data-cdi-ft-pan-x', 'data-cdi-ft-pan-y',
        'data-cdi-ft-zoomed', 'data-cdi-ft-overflow', 'data-cdi-ft-panning', 'data-cdi-ft-natural']) {
        viewport.removeAttribute(name);
    }

    states.delete(viewport);
}

function attachListeners(state) {
    const { viewport, content } = state;
    const onWheel = (event) => handleWheel(state, event);
    const onPointerDown = (event) => handlePointerDown(state, event);
    const onPointerMove = (event) => handlePointerMove(state, event);
    const onPointerUp = (event) => finishPointer(state, event);
    const onKeyDown = (event) => handleKeyDown(state, event);
    const onDoubleClick = (event) => handleDoubleClick(state, event);
    const onDragStart = (event) => {
        if (state.options.enabled) {
            event.preventDefault();
        }
    };
    const onLoad = () => scheduleMeasure(state);

    viewport.addEventListener('wheel', onWheel, { passive: false });
    viewport.addEventListener('pointerdown', onPointerDown);
    viewport.addEventListener('pointermove', onPointerMove);
    viewport.addEventListener('pointerup', onPointerUp);
    viewport.addEventListener('pointercancel', onPointerUp);
    viewport.addEventListener('keydown', onKeyDown);
    viewport.addEventListener('dblclick', onDoubleClick);
    viewport.addEventListener('dragstart', onDragStart);
    content.addEventListener('load', onLoad, true);

    state.cleanup.push(() => {
        viewport.removeEventListener('wheel', onWheel);
        viewport.removeEventListener('pointerdown', onPointerDown);
        viewport.removeEventListener('pointermove', onPointerMove);
        viewport.removeEventListener('pointerup', onPointerUp);
        viewport.removeEventListener('pointercancel', onPointerUp);
        viewport.removeEventListener('keydown', onKeyDown);
        viewport.removeEventListener('dblclick', onDoubleClick);
        viewport.removeEventListener('dragstart', onDragStart);
        content.removeEventListener('load', onLoad, true);
    });
}

function observe(state) {
    if (typeof ResizeObserver === 'function') {
        state.resizeObserver = new ResizeObserver(() => scheduleMeasure(state));
        state.resizeObserver.observe(state.viewport);
    }

    if (typeof MutationObserver === 'function') {
        state.mutationObserver = new MutationObserver(() => scheduleMeasure(state));
        state.mutationObserver.observe(state.content, {
            childList: true,
            subtree: true,
            attributes: true,
            attributeFilter: ['viewBox', 'width', 'height', 'src', 'data-cdi-ft-content-width', 'data-cdi-ft-content-height'],
        });
    }
}

function scheduleMeasure(state) {
    if (state.measureQueued) {
        return;
    }

    // A microtask coalesces observer bursts but, unlike requestAnimationFrame, still runs while the
    // document is hidden, so content that loads in a background tab is fitted before it is shown.
    state.measureQueued = true;
    queueMicrotask(() => {
        state.measureQueued = false;
        if (states.get(state.viewport) === state) {
            measure(state);
        }
    });
}

function measure(state) {
    const natural = resolveNaturalSize(state);
    const changed = !sameSize(natural, state.natural);
    state.natural = natural;
    applyPlaneSize(state);
    updateFit(state);
    if (changed) {
        resetView(state);
    }

    applyTransform(state);
}

function resolveNaturalSize(state) {
    const child = state.content.firstElementChild;
    if (!child) {
        return null;
    }

    return sizeOf(child)
        ?? sizeOf(child.querySelector?.('[data-cdi-ft-zoom-pan-content]'))
        ?? readSvgSize(child.querySelector?.('svg[viewBox], svg[width][height]'));
}

function sizeOf(element) {
    if (!element) {
        return null;
    }

    const declared = readDeclaredSize(element);
    if (declared) {
        return declared;
    }

    if (element instanceof HTMLImageElement) {
        return element.complete && element.naturalWidth > 0 && element.naturalHeight > 0
            ? validSize(element.naturalWidth, element.naturalHeight)
            : null;
    }

    if (element instanceof SVGSVGElement) {
        return readSvgSize(element);
    }

    return null;
}

function readDeclaredSize(element) {
    const width = Number(element.getAttribute('data-cdi-ft-content-width'));
    const height = Number(element.getAttribute('data-cdi-ft-content-height'));
    return validSize(width, height);
}

function readSvgSize(svg) {
    if (!svg) {
        return null;
    }

    const width = lengthToPixels(svg.getAttribute('width'));
    const height = lengthToPixels(svg.getAttribute('height'));
    if (width && height) {
        return validSize(width, height);
    }

    const viewBox = parseViewBox(svg.getAttribute('viewBox'));
    if (!viewBox) {
        return null;
    }

    if (width) {
        return validSize(width, width * viewBox.height / viewBox.width);
    }

    if (height) {
        return validSize(height * viewBox.width / viewBox.height, height);
    }

    return validSize(viewBox.width, viewBox.height);
}

function parseViewBox(value) {
    if (!value) {
        return null;
    }

    const parts = value.trim().split(/[\s,]+/).map(Number);
    if (parts.length !== 4 || parts.some((part) => !Number.isFinite(part))) {
        return null;
    }

    return parts[2] > 0 && parts[3] > 0 ? { width: parts[2], height: parts[3] } : null;
}

function lengthToPixels(value) {
    if (value === null || value === undefined) {
        return null;
    }

    const match = String(value).trim().match(/^([+-]?(?:\d+\.?\d*|\.\d+)(?:e[+-]?\d+)?)\s*([a-z%]*)$/i);
    if (!match) {
        return null;
    }

    const unit = match[2].toLowerCase();
    if (unit === '%') {
        return null;
    }

    const factor = unit === '' ? 1 : lengthUnits[unit];
    if (factor === undefined) {
        return null;
    }

    const pixels = Number(match[1]) * factor;
    return Number.isFinite(pixels) && pixels > 0 ? pixels : null;
}

function validSize(width, height) {
    if (!Number.isFinite(width) || !Number.isFinite(height) || width <= 0 || height <= 0) {
        return null;
    }

    if (width > maximumPlaneSize || height > maximumPlaneSize) {
        return null;
    }

    return { width, height };
}

function sameSize(first, second) {
    if (!first || !second) {
        return first === second;
    }

    return Math.abs(first.width - second.width) < 0.01 && Math.abs(first.height - second.height) < 0.01;
}

function applyPlaneSize(state) {
    const { content, natural, viewport } = state;
    if (natural) {
        content.style.inlineSize = `${natural.width}px`;
        content.style.blockSize = `${natural.height}px`;
        viewport.setAttribute('data-cdi-ft-natural', `${roundTo(natural.width, 2)}x${roundTo(natural.height, 2)}`);
    } else {
        content.style.removeProperty('inline-size');
        content.style.removeProperty('block-size');
        viewport.removeAttribute('data-cdi-ft-natural');
    }
}

function planeSize(state) {
    if (state.natural) {
        return state.natural;
    }

    return {
        width: state.content.offsetWidth,
        height: state.content.offsetHeight,
    };
}

function updateFit(state) {
    const viewportWidth = state.viewport.clientWidth;
    const viewportHeight = state.viewport.clientHeight;
    if (!state.natural || viewportWidth <= 0 || viewportHeight <= 0) {
        state.fit = 1;
        return;
    }

    const fit = Math.min(viewportWidth / state.natural.width, viewportHeight / state.natural.height);
    state.fit = Number.isFinite(fit) && fit > 0 ? fit : 1;
}

function applyTransform(state) {
    const { viewport, content } = state;
    const viewportWidth = viewport.clientWidth;
    const viewportHeight = viewport.clientHeight;
    const plane = planeSize(state);
    const scale = state.fit * state.zoom;
    const scaledWidth = plane.width * scale;
    const scaledHeight = plane.height * scale;
    state.x = clampOffset(state.x, viewportWidth, scaledWidth);
    state.y = clampOffset(state.y, viewportHeight, scaledHeight);
    state.overflowX = scaledWidth > viewportWidth + overflowTolerance;
    state.overflowY = scaledHeight > viewportHeight + overflowTolerance;

    content.style.transform = `translate3d(${roundTo(state.x, 2)}px, ${roundTo(state.y, 2)}px, 0) scale(${roundTo(scale, 5)})`;
    viewport.setAttribute('data-cdi-ft-zoom', String(roundTo(state.zoom, 4)));
    viewport.setAttribute('data-cdi-ft-scale', String(roundTo(scale, 5)));
    viewport.setAttribute('data-cdi-ft-pan-x', String(roundTo(state.x, 2)));
    viewport.setAttribute('data-cdi-ft-pan-y', String(roundTo(state.y, 2)));
    viewport.setAttribute('data-cdi-ft-zoomed', String(Math.abs(state.zoom - 1) > zoomEpsilon));
    viewport.setAttribute('data-cdi-ft-overflow', overflowLabel(state));
    updateReadout(state);
}

function overflowLabel(state) {
    if (state.overflowX && state.overflowY) {
        return 'xy';
    }

    if (state.overflowX) {
        return 'x';
    }

    return state.overflowY ? 'y' : 'none';
}

function updateReadout(state) {
    const readout = state.viewport.parentElement?.querySelector('[data-cdi-ft-zoom-readout]');
    if (readout) {
        readout.textContent = `${Math.round(state.zoom * 100)}%`;
    }
}

function resetView(state) {
    state.zoom = 1;
    state.x = 0;
    state.y = 0;
    state.pointers.clear();
    state.lastPointer = null;
    state.pinch = null;
    setPanning(state, false);
}

function handleWheel(state, event) {
    const { options } = state;
    if (!options.enabled || options.wheelMode === wheelModes.disabled) {
        return;
    }

    const delta = normalizeWheelDelta(state, event);
    const zoomIntent = options.wheelMode === wheelModes.zoom || event.ctrlKey || event.metaKey;
    if (zoomIntent && delta.y !== 0) {
        const magnitude = clamp(Math.abs(delta.y) / 100, 0.1, 1);
        const base = delta.y < 0 ? options.zoomFactor : 1 / options.zoomFactor;
        const changed = zoomAt(state, Math.pow(base, magnitude), clientPoint(state, event.clientX, event.clientY));
        if (changed || event.ctrlKey || event.metaKey) {
            event.preventDefault();
        }

        return;
    }

    const panX = event.shiftKey && delta.x === 0 ? delta.y : delta.x;
    const panY = event.shiftKey && delta.x === 0 ? 0 : delta.y;
    if (panBy(state, -panX, -panY)) {
        event.preventDefault();
    }
}

function normalizeWheelDelta(state, event) {
    let factor = 1;
    if (event.deltaMode === 1) {
        factor = 16;
    } else if (event.deltaMode === 2) {
        factor = Math.max(state.viewport.clientHeight, 1);
    }

    return {
        x: event.deltaX * factor,
        y: event.deltaY * factor,
    };
}

function handlePointerDown(state, event) {
    if (!state.options.enabled || isInteractiveEvent(state, event)) {
        return;
    }

    if (event.pointerType === 'mouse' && event.button !== 0) {
        return;
    }

    const point = { x: event.clientX, y: event.clientY };
    state.pointers.set(event.pointerId, point);
    try {
        state.viewport.setPointerCapture?.(event.pointerId);
    } catch {
        // Pointer capture is a progressive enhancement.
    }

    if (state.pointers.size === 1) {
        state.lastPointer = point;
        state.pinch = null;
    } else if (state.pointers.size === 2) {
        const [first, second] = [...state.pointers.values()];
        state.pinch = { distance: distance(first, second), midpoint: midpoint(first, second) };
    }

    if (canPan(state) || state.pointers.size > 1) {
        event.preventDefault();
        setPanning(state, true);
        focusViewport(state);
    }
}

function handlePointerMove(state, event) {
    if (!state.pointers.has(event.pointerId)) {
        return;
    }

    const point = { x: event.clientX, y: event.clientY };
    state.pointers.set(event.pointerId, point);
    if (state.pointers.size >= 2 && state.pinch) {
        const [first, second] = [...state.pointers.values()];
        const nextDistance = distance(first, second);
        const nextMidpoint = midpoint(first, second);
        zoomAt(
            state,
            nextDistance / Math.max(state.pinch.distance, 1),
            clientPoint(state, state.pinch.midpoint.x, state.pinch.midpoint.y));
        state.x += nextMidpoint.x - state.pinch.midpoint.x;
        state.y += nextMidpoint.y - state.pinch.midpoint.y;
        applyTransform(state);
        state.pinch = { distance: nextDistance, midpoint: nextMidpoint };
        event.preventDefault();
        setPanning(state, true);
        return;
    }

    if (!state.lastPointer) {
        state.lastPointer = point;
        return;
    }

    const deltaX = point.x - state.lastPointer.x;
    const deltaY = point.y - state.lastPointer.y;
    state.lastPointer = point;
    if (panBy(state, deltaX, deltaY)) {
        event.preventDefault();
        setPanning(state, true);
    }
}

function finishPointer(state, event) {
    if (!state.pointers.delete(event.pointerId)) {
        return;
    }

    try {
        if (state.viewport.hasPointerCapture?.(event.pointerId)) {
            state.viewport.releasePointerCapture(event.pointerId);
        }
    } catch {
        // Pointer capture is a progressive enhancement.
    }

    const remaining = [...state.pointers.values()];
    state.lastPointer = remaining[0] ?? null;
    state.pinch = null;
    if (remaining.length === 0) {
        setPanning(state, false);
    }
}

function handleKeyDown(state, event) {
    if (!state.options.enabled || isInteractiveEvent(state, event)) {
        return;
    }

    let changed = false;
    switch (event.key) {
        case '+':
        case '=':
            changed = zoomAt(state, state.options.zoomFactor, viewportCenter(state));
            break;
        case '-':
        case '_':
            changed = zoomAt(state, 1 / state.options.zoomFactor, viewportCenter(state));
            break;
        case '0':
            changed = Math.abs(state.zoom - 1) > zoomEpsilon || state.overflowX || state.overflowY;
            resetView(state);
            applyTransform(state);
            break;
        case 'ArrowLeft':
            changed = panBy(state, state.options.keyboardPanStep, 0);
            break;
        case 'ArrowRight':
            changed = panBy(state, -state.options.keyboardPanStep, 0);
            break;
        case 'ArrowUp':
            changed = panBy(state, 0, state.options.keyboardPanStep);
            break;
        case 'ArrowDown':
            changed = panBy(state, 0, -state.options.keyboardPanStep);
            break;
        case 'Home':
            changed = panBy(state, Number.POSITIVE_INFINITY, Number.POSITIVE_INFINITY);
            break;
        case 'End':
            changed = panBy(state, Number.NEGATIVE_INFINITY, Number.NEGATIVE_INFINITY);
            break;
        default:
            return;
    }

    if (changed) {
        event.preventDefault();
    }
}

function handleDoubleClick(state, event) {
    if (!state.options.enabled || isInteractiveEvent(state, event)) {
        return;
    }

    event.preventDefault();
    if (Math.abs(state.zoom - 1) > zoomEpsilon) {
        resetView(state);
        applyTransform(state);
        return;
    }

    zoomAt(state, 2, clientPoint(state, event.clientX, event.clientY));
}

function zoomAt(state, factor, origin) {
    if (!Number.isFinite(factor) || factor <= 0) {
        return false;
    }

    const nextZoom = clamp(state.zoom * factor, state.options.minimumZoom, state.options.maximumZoom);
    if (Math.abs(nextZoom - state.zoom) < zoomEpsilon) {
        return false;
    }

    const ratio = nextZoom / state.zoom;
    state.x = origin.x - (origin.x - state.x) * ratio;
    state.y = origin.y - (origin.y - state.y) * ratio;
    state.zoom = nextZoom;
    applyTransform(state);
    return true;
}

function panBy(state, deltaX, deltaY) {
    const previousX = state.x;
    const previousY = state.y;
    state.x += deltaX;
    state.y += deltaY;
    applyTransform(state);
    return Math.abs(previousX - state.x) > 0.01 || Math.abs(previousY - state.y) > 0.01;
}

function canPan(state) {
    return state.overflowX || state.overflowY;
}

function setPanning(state, panning) {
    if (panning) {
        state.viewport.setAttribute('data-cdi-ft-panning', 'true');
    } else {
        state.viewport.removeAttribute('data-cdi-ft-panning');
    }
}

function focusViewport(state) {
    if (document.activeElement !== state.viewport && typeof state.viewport.focus === 'function') {
        try {
            state.viewport.focus({ preventScroll: true });
        } catch {
            // Focus is a progressive enhancement.
        }
    }
}

function normalizeOptions(options) {
    return {
        enabled: options?.enabled !== false,
        suppressContentInteraction: options?.suppressContentInteraction === true,
        wheelMode: Number(options?.wheelMode ?? wheelModes.zoom),
        minimumZoom: Number(options?.minimumZoom ?? 0.25),
        maximumZoom: Number(options?.maximumZoom ?? 8),
        zoomFactor: Number(options?.zoomFactor ?? 1.2),
        keyboardPanStep: Number(options?.keyboardPanStep ?? 48),
    };
}

function isInteractiveEvent(state, event) {
    if (state.options.suppressContentInteraction) {
        return false;
    }

    const path = typeof event.composedPath === 'function' ? event.composedPath() : [];
    for (const target of path) {
        if (target === state.viewport) {
            return false;
        }

        if (target instanceof Element && target.matches(interactiveSelector)) {
            return true;
        }
    }

    return false;
}

function clientPoint(state, clientX, clientY) {
    const bounds = state.viewport.getBoundingClientRect();
    return {
        x: clientX - bounds.left,
        y: clientY - bounds.top,
    };
}

function viewportCenter(state) {
    return {
        x: state.viewport.clientWidth / 2,
        y: state.viewport.clientHeight / 2,
    };
}

function clampOffset(value, viewportSize, scaledSize) {
    if (scaledSize <= viewportSize) {
        return (viewportSize - scaledSize) / 2;
    }

    return clamp(value, viewportSize - scaledSize, 0);
}

function clamp(value, minimum, maximum) {
    return Math.min(maximum, Math.max(minimum, value));
}

function roundTo(value, digits) {
    const factor = Math.pow(10, digits);
    return Math.round(value * factor) / factor;
}

function midpoint(first, second) {
    return {
        x: (first.x + second.x) / 2,
        y: (first.y + second.y) / 2,
    };
}

function distance(first, second) {
    return Math.hypot(second.x - first.x, second.y - first.y);
}
