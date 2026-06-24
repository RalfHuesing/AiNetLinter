/**
 * san-timelineview — zentrale Defaults + site.json-Overlay (`options.sanTimeline`).
 * Alle festen Renderer-Werte gehören hierher; Module nutzen `resolveConstants` / `constantsFor(inst)`.
 */
(function (global) {
  'use strict';

  global.SanTimelineView = global.SanTimelineView || {};

  var BASE = {
    GROUP_ROW_HEIGHT: 56,
    STACK_LANE_HEIGHT: 24,
    /** Vertikaler Abstand zwischen gestapelten Termin-Lanes (px); verhindert doppelte Konva-Stroke-Kanten. */
    STACK_LANE_GAP: 4,
    GROUP_ROW_PADDING: 8,
    /** Gesamthöhe Kopf = Major + Minor (vis-Parität: grobes Intervall oben, feines unten). */
    TIME_AXIS_MAJOR_HEIGHT: 24,
    TIME_AXIS_MINOR_HEIGHT: 28,
    TIME_AXIS_HEIGHT: 52,
    TIME_AXIS_MIN_LABEL_PX: 72,
    GROUP_LABEL_WIDTH: 140,
    /** Horizontale Trennlinie zwischen Ressourcen-Zeilen (Zeitbereich + Label). */
    GROUP_ROW_SEPARATOR_STROKE: 'gray-500',
    /** Vertikale Trennlinie zwischen Ressourcen-Spalten im labelLayer. */
    GROUP_COLUMN_SEPARATOR_STROKE: 'gray-300',
    MIN_STAGE_HEIGHT: 120,
    DEFAULT_CAPACITY_NON_WORK: 'gray-300',
    DEFAULT_READONLY_ITEM_FILL: 'red-50',
    DEFAULT_READONLY_ITEM_STROKE: 'red-600',
    /** Standard-Fill/Stroke für Termine ohne className/color (Theme-CSS hat Vorrang). */
    DEFAULT_ITEM_FILL: 'gray-50',
    DEFAULT_ITEM_STROKE: 'gray-400',
    MIN_ZOOM_PX_PER_MS: 1 / (6 * 60 * 60 * 1000),
    /** Maximal reinzoomen: Default 5 s pro Pixel (~66 min sichtbar bei 800 px Breite). */
    MAX_ZOOM_PX_PER_MS: 1 / 5000,
    CULL_BUFFER_RATIO: 0.08,
    /** Anteil der Preset-Spanne links von „jetzt“ (Rest = Zukunft); „Heute“-Linie ~10 % von links. */
    VIEWPORT_PAST_RATIO: 0.1,
    /** Legacy-Name; Trefferzone für Resize-Kanten siehe RESIZE_HANDLE_HIT_WIDTH. */
    RESIZE_HANDLE_WIDTH: 8,
    /** Pointer-Trefferzone links/rechts am Termin (px), unabhängig von der Darstellung. */
    RESIZE_HANDLE_HIT_WIDTH: 18,
    /** Ab dieser Balkenbreite (px) gilt ein selektiertes Item als „kurz“ (Outside-Padding). */
    RESIZE_SHORT_ITEM_THRESHOLD_PX: 60,
    /** Selektiert + kurz: unsichtbare Trefferzone links/rechts außerhalb des Balkens (px). */
    RESIZE_HANDLE_OUTSIDE_PADDING_PX: 10,
    /** Selektiert: verbreiterte Resize-Trefferzone nach innen (px). */
    RESIZE_SELECTED_DOMINANCE_WIDTH_PX: 20,
    /** Markierter Termin (Klick ohne Drag). */
    SELECTED_ITEM_STROKE: 'blue-600',
    SELECTED_ITEM_STROKE_WIDTH: 3,
    SELECTED_ITEM_SHADOW_COLOR: 'rgba(37, 99, 235, 0.28)',
    /** Klick vs. Drag/Pan (px, euklidisch). */
    CLICK_MOVE_THRESHOLD_PX: 5,
    MIN_ITEM_DURATION_MS: 15 * 60 * 1000,
    SNAP_INTERVAL_MS: 15 * 60 * 1000,
    DEFAULT_TIME_LOCALE: 'de-DE',
    /** IANA-Zeitzone für Achsen/Raster (Kalendertagesgrenzen = Mitternacht Ort). */
    DEFAULT_TIME_ZONE: 'Europe/Berlin',
    /** Nicht-Treffer bei aktiver Toolbar-Suche (Termine bleiben sichtbar). */
    SEARCH_DIM_OPACITY: 0.35,
    /** Zeitachsen-Kopf und Label-Spalten-Kopf. */
    TIME_AXIS_HEADER_FILL: 'gray-10',
    TIME_AXIS_MAJOR_BAND_FILL: 'gray-100',
    TIME_AXIS_MAJOR_LABEL_FILL: 'gray-500',
    TIME_AXIS_MAJOR_FONT_SIZE: 13,
    TIME_AXIS_MAJOR_DIVIDER_STROKE: 'gray-300',
    TIME_AXIS_TICK_STROKE: 'gray-300',
    TIME_AXIS_TICK_FONT_SIZE: 13,
    TIME_AXIS_TICK_FILL: 'gray-700',
    TIME_AXIS_BORDER_STROKE: 'gray-400',
    /** Vertikales Raster im Zeitbereich (skalenabhängig). */
    GRID_MINOR_STROKE: 'gray-300',
    GRID_MAJOR_STROKE: 'gray-400',
    GRID_MINOR_MIN_PX: 8,
    /** „Heute“ / aktuelle Zeit — durchgehende Linie. */
    TODAY_LINE_STROKE: 'red-standard',
    TODAY_LINE_STROKE_WIDTH: 2,
    /** Zebra-Hintergrund Ressourcen-Zeilen (gerade/ungerade Index). */
    GROUP_ROW_FILL_EVEN: 'white',
    GROUP_ROW_FILL_ODD: 'gray-10',
    GROUP_LABEL_FONT_SIZE: 14,
    GROUP_LABEL_FILL: 'gray-800',
    /** Eckenradius Termin-Balken (Konva cornerRadius); 0 = eckig/präzise. */
    ITEM_BAR_CORNER_RADIUS: 0,
    DRAG_GHOST_OPACITY: 0.45,
    SHOW_ITEM_LABELS: true,
    ITEM_LABEL_FONT_SIZE: 14,
    ITEM_LABEL_PADDING_X: 5,
    ITEM_LABEL_PADDING_Y: 4,
    ITEM_LABEL_FONT_FAMILY:
      'Inter, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif',
    ITEM_LABEL_FILL: 'gray-900',
    /** Beschriftung bei Toolbar-Suche (Nicht-Treffer). */
    ITEM_LABEL_DIMMED_FILL: 'gray-400',
    ITEM_LABEL_MIN_WIDTH_PX: 24,
  };

  global.SanTimelineView.constants = BASE;

  function shallowCopyConstants() {
    var out = {};
    for (var k in BASE) {
      if (Object.prototype.hasOwnProperty.call(BASE, k)) out[k] = BASE[k];
    }
    return out;
  }

  function readPositiveNumber(value) {
    var n = Number(value);
    return isFinite(n) && n > 0 ? n : null;
  }

  function readNonNegativeNumber(value) {
    var n = Number(value);
    return isFinite(n) && n >= 0 ? n : null;
  }

  function readNonEmptyString(value) {
    if (value == null) return null;
    var s = String(value).trim();
    return s !== '' ? s : null;
  }

  function readBool(value, defaultValue) {
    if (value === true) return true;
    if (value === false) return false;
    return defaultValue;
  }

  function readSection(options, key) {
    var san = options && options.sanTimeline;
    if (!san || typeof san !== 'object') return null;
    var section = san[key];
    return section && typeof section === 'object' ? section : null;
  }

  function labelSourceFromOptions(opts) {
    var nested = opts.sanTimeline && typeof opts.sanTimeline === 'object' ? opts.sanTimeline : {};
    if (nested.labels && typeof nested.labels === 'object') return nested.labels;
    return nested;
  }

  function applyLayoutOverrides(out, layout) {
    if (!layout) return;
    var map = {
      groupRowHeight: 'GROUP_ROW_HEIGHT',
      stackLaneHeight: 'STACK_LANE_HEIGHT',
      stackLaneGap: 'STACK_LANE_GAP',
      groupRowPadding: 'GROUP_ROW_PADDING',
      timeAxisHeight: 'TIME_AXIS_HEIGHT',
      timeAxisMajorHeight: 'TIME_AXIS_MAJOR_HEIGHT',
      timeAxisMinorHeight: 'TIME_AXIS_MINOR_HEIGHT',
      timeAxisMinLabelPx: 'TIME_AXIS_MIN_LABEL_PX',
      groupLabelWidth: 'GROUP_LABEL_WIDTH',
      minStageHeight: 'MIN_STAGE_HEIGHT',
      timeAxisTickFontSize: 'TIME_AXIS_TICK_FONT_SIZE',
      groupLabelFontSize: 'GROUP_LABEL_FONT_SIZE',
    };
    for (var jsonKey in map) {
      if (!Object.prototype.hasOwnProperty.call(map, jsonKey)) continue;
      var n = readPositiveNumber(layout[jsonKey]);
      if (n != null) out[map[jsonKey]] = n;
    }
    var cornerR = readNonNegativeNumber(layout.itemBarCornerRadius);
    if (cornerR != null) out.ITEM_BAR_CORNER_RADIUS = cornerR;
  }

  var RESOLVED_COLOR_CONSTANT_KEYS = [
    'GROUP_ROW_SEPARATOR_STROKE',
    'GROUP_COLUMN_SEPARATOR_STROKE',
    'DEFAULT_CAPACITY_NON_WORK',
    'DEFAULT_READONLY_ITEM_FILL',
    'DEFAULT_READONLY_ITEM_STROKE',
    'DEFAULT_ITEM_FILL',
    'DEFAULT_ITEM_STROKE',
    'SELECTED_ITEM_STROKE',
    'TIME_AXIS_HEADER_FILL',
    'TIME_AXIS_MAJOR_BAND_FILL',
    'TIME_AXIS_MAJOR_LABEL_FILL',
    'TIME_AXIS_MAJOR_DIVIDER_STROKE',
    'TIME_AXIS_TICK_STROKE',
    'TIME_AXIS_TICK_FILL',
    'TIME_AXIS_BORDER_STROKE',
    'GRID_MINOR_STROKE',
    'GRID_MAJOR_STROKE',
    'TODAY_LINE_STROKE',
    'GROUP_ROW_FILL_EVEN',
    'GROUP_ROW_FILL_ODD',
    'GROUP_LABEL_FILL',
    'ITEM_LABEL_FILL',
    'ITEM_LABEL_DIMMED_FILL',
  ];

  function resolveColorSetting(value) {
    var San = global.SanTimelineView;
    if (typeof San.resolveColorToken !== 'function') return readNonEmptyString(value);
    var raw = readNonEmptyString(value);
    if (!raw) return '';
    return San.resolveColorToken(raw) || raw;
  }

  function resolveStoredColorConstants(out) {
    var San = global.SanTimelineView;
    if (typeof San.resolveColorToken !== 'function') return;
    for (var i = 0; i < RESOLVED_COLOR_CONSTANT_KEYS.length; i++) {
      var key = RESOLVED_COLOR_CONSTANT_KEYS[i];
      var raw = out[key];
      if (raw == null || String(raw).trim() === '') continue;
      var s = String(raw).trim();
      if (s.charAt(0) === '#') continue;
      var resolved = San.resolveColorToken(s);
      if (resolved) out[key] = resolved;
    }
  }

  function applyColorOverrides(out, colors, options) {
    if (options && options.capacityDefaultNonWorkBackground != null) {
      var capRoot = resolveColorSetting(options.capacityDefaultNonWorkBackground);
      if (capRoot) out.DEFAULT_CAPACITY_NON_WORK = capRoot;
    }
    if (!colors) return;
    var colorMap = {
      groupRowSeparatorStroke: 'GROUP_ROW_SEPARATOR_STROKE',
      groupColumnSeparatorStroke: 'GROUP_COLUMN_SEPARATOR_STROKE',
      readonlyItemFill: 'DEFAULT_READONLY_ITEM_FILL',
      readonlyItemStroke: 'DEFAULT_READONLY_ITEM_STROKE',
      selectedItemStroke: 'SELECTED_ITEM_STROKE',
      selectedItemShadowColor: 'SELECTED_ITEM_SHADOW_COLOR',
      defaultItemFill: 'DEFAULT_ITEM_FILL',
      defaultItemStroke: 'DEFAULT_ITEM_STROKE',
      timeAxisHeaderFill: 'TIME_AXIS_HEADER_FILL',
      timeAxisTickStroke: 'TIME_AXIS_TICK_STROKE',
      timeAxisTickFill: 'TIME_AXIS_TICK_FILL',
      timeAxisBorderStroke: 'TIME_AXIS_BORDER_STROKE',
      timeAxisMajorBandFill: 'TIME_AXIS_MAJOR_BAND_FILL',
      timeAxisMajorLabelFill: 'TIME_AXIS_MAJOR_LABEL_FILL',
      gridMinorStroke: 'GRID_MINOR_STROKE',
      gridMajorStroke: 'GRID_MAJOR_STROKE',
      todayLineStroke: 'TODAY_LINE_STROKE',
      groupRowFillEven: 'GROUP_ROW_FILL_EVEN',
      groupRowFillOdd: 'GROUP_ROW_FILL_ODD',
      groupLabelFill: 'GROUP_LABEL_FILL',
      itemLabelFill: 'ITEM_LABEL_FILL',
      itemLabelDimmedFill: 'ITEM_LABEL_DIMMED_FILL',
    };
    for (var ck in colorMap) {
      if (!Object.prototype.hasOwnProperty.call(colorMap, ck)) continue;
      var resolved = resolveColorSetting(colors[ck]);
      if (resolved) out[colorMap[ck]] = resolved;
    }
    if (!options || options.capacityDefaultNonWorkBackground == null) {
      var cap = resolveColorSetting(colors.capacityDefaultNonWork);
      if (cap) out.DEFAULT_CAPACITY_NON_WORK = cap;
    }
  }

  function applyLabelOverrides(out, labels, opts) {
    var src = labels || labelSourceFromOptions(opts || {});
    if (!src || typeof src !== 'object') return;
    out.SHOW_ITEM_LABELS = readBool(src.showItemLabels, out.SHOW_ITEM_LABELS);
    var fs = readPositiveNumber(src.itemLabelFontSize);
    if (fs != null) out.ITEM_LABEL_FONT_SIZE = fs;
    var px = readPositiveNumber(src.itemLabelPaddingX);
    if (px != null) out.ITEM_LABEL_PADDING_X = px;
    var py = readPositiveNumber(src.itemLabelPaddingY);
    if (py != null) out.ITEM_LABEL_PADDING_Y = py;
    var ff = readNonEmptyString(src.itemLabelFontFamily);
    if (ff) out.ITEM_LABEL_FONT_FAMILY = ff;
    var fill = readNonEmptyString(src.itemLabelFill);
    if (fill) out.ITEM_LABEL_FILL = fill;
    var dim = readNonEmptyString(src.itemLabelDimmedFill);
    if (dim) out.ITEM_LABEL_DIMMED_FILL = dim;
    var minW = readPositiveNumber(src.itemLabelMinWidthPx);
    if (minW != null) out.ITEM_LABEL_MIN_WIDTH_PX = minW;
  }

  function applyInteractionOverrides(out, interaction) {
    if (!interaction) return;
    var nHit = readPositiveNumber(interaction.resizeHandleHitWidth);
    if (nHit != null) out.RESIZE_HANDLE_HIT_WIDTH = nHit;
    var nShort = readPositiveNumber(interaction.resizeShortItemThresholdPx);
    if (nShort != null) out.RESIZE_SHORT_ITEM_THRESHOLD_PX = nShort;
    var nOutside = readNonNegativeNumber(interaction.resizeHandleOutsidePaddingPx);
    if (nOutside != null) out.RESIZE_HANDLE_OUTSIDE_PADDING_PX = nOutside;
    var nDom = readPositiveNumber(interaction.resizeSelectedDominanceWidthPx);
    if (nDom != null) out.RESIZE_SELECTED_DOMINANCE_WIDTH_PX = nDom;
    var nStroke = readPositiveNumber(interaction.selectedItemStrokeWidth);
    if (nStroke != null) out.SELECTED_ITEM_STROKE_WIDTH = nStroke;
    var nClick = readPositiveNumber(interaction.clickMoveThresholdPx);
    if (nClick != null) out.CLICK_MOVE_THRESHOLD_PX = nClick;
    var minMin = readPositiveNumber(interaction.minItemDurationMinutes);
    if (minMin != null) out.MIN_ITEM_DURATION_MS = Math.round(minMin * 60000);
    var snapMin = readPositiveNumber(interaction.snapIntervalMinutes);
    if (snapMin != null) out.SNAP_INTERVAL_MS = Math.max(60000, Math.round(snapMin * 60000));
    var ghost = readNonNegativeNumber(interaction.dragGhostOpacity);
    if (ghost != null) out.DRAG_GHOST_OPACITY = ghost;
  }

  function applyViewportOverrides(out, viewport) {
    if (!viewport) return;
    var ratio = readNonNegativeNumber(viewport.cullBufferRatio);
    if (ratio != null) out.CULL_BUFFER_RATIO = ratio;
    var dim = readNonNegativeNumber(viewport.searchDimOpacity);
    if (dim != null) out.SEARCH_DIM_OPACITY = dim;
    var minZoom = readPositiveNumber(viewport.minZoomPxPerMs);
    if (minZoom != null) out.MIN_ZOOM_PX_PER_MS = minZoom;
    var maxZoom = readPositiveNumber(viewport.maxZoomPxPerMs);
    if (maxZoom != null) out.MAX_ZOOM_PX_PER_MS = maxZoom;
    var minHours = readPositiveNumber(viewport.minZoomHoursPerPixel);
    if (minHours != null) out.MIN_ZOOM_PX_PER_MS = 1 / (minHours * 3600 * 1000);
    var maxMinutes = readPositiveNumber(viewport.maxZoomMinutesPerPixel);
    if (maxMinutes != null) out.MAX_ZOOM_PX_PER_MS = 1 / (maxMinutes * 60 * 1000);
    var maxSeconds = readPositiveNumber(viewport.maxZoomSecondsPerPixel);
    if (maxSeconds != null) out.MAX_ZOOM_PX_PER_MS = 1 / (maxSeconds * 1000);
  }

  /**
   * Merged Defaults mit <c>options.sanTimeline.{layout,colors,labels,interaction,viewport}</c> und
   * <c>capacityDefaultNonWorkBackground</c> (Bridge).
   * @param {object} [options]
   * @returns {object}
   */
  global.SanTimelineView.resolveConstants = function resolveConstants(options) {
    var out = shallowCopyConstants();
    var opts = options || {};
    applyLayoutOverrides(out, readSection(opts, 'layout'));
    applyColorOverrides(out, readSection(opts, 'colors'), opts);
    applyLabelOverrides(out, readSection(opts, 'labels'), opts);
    applyInteractionOverrides(out, readSection(opts, 'interaction'));
    applyViewportOverrides(out, readSection(opts, 'viewport'));
    var locale = readNonEmptyString(opts.timeLocale);
    if (locale) out.DEFAULT_TIME_LOCALE = locale;
    var tz = readNonEmptyString(opts.timeZone);
    if (tz) out.DEFAULT_TIME_ZONE = tz;
    resolveStoredColorConstants(out);
    return out;
  };

  /** Pro-Instanz-Konstanten (site.json), sonst globale Defaults. */
  global.SanTimelineView.constantsFor = function constantsFor(inst) {
    if (inst && inst.constants) return inst.constants;
    return BASE;
  };
})(typeof globalThis !== 'undefined' ? globalThis : window);
