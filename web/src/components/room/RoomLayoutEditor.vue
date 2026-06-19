<template>
  <section class="layout-editor">
    <header class="layout-editor__toolbar">
      <div class="layout-editor__title">
        <span class="layout-editor__eyebrow">Schematic layout</span>
        <h2>Room plan</h2>
      </div>

      <div class="layout-editor__actions">
        <div class="layout-editor__mode" aria-label="Layout mode">
          <Button
            label="View"
            icon="pi pi-eye"
            :severity="mode === 'view' ? 'primary' : 'secondary'"
            :variant="mode === 'view' ? undefined : 'text'"
            @click="setMode('view')"
          />
          <Button
            v-if="!isReadOnly"
            label="Edit"
            icon="pi pi-pencil"
            :severity="mode === 'edit' ? 'primary' : 'secondary'"
            :variant="mode === 'edit' ? undefined : 'text'"
            @click="setMode('edit')"
          />
        </div>
        <Tag v-if="isReadOnly" severity="secondary" value="Read only" />
        <Tag v-if="mode === 'edit' || isDirty" :severity="isDirty ? 'warn' : 'success'" :value="isDirty ? 'Unsaved' : 'Saved'" />
        <Button v-if="mode === 'view'" label="Refresh data" icon="pi pi-refresh" severity="secondary" variant="text" :loading="isTelemetryLoading" @click="() => loadTelemetry()" />
        <Button v-if="mode === 'edit'" label="Reload" icon="pi pi-refresh" severity="secondary" variant="text" :disabled="isSaving" @click="reloadLayout" />
        <Button v-if="mode === 'edit'" label="Save" icon="pi pi-save" :loading="isSaving" :disabled="!isDirty || hasPlacementErrors" @click="saveLayout" />
      </div>
    </header>

    <Message v-if="errorMessage" severity="error" variant="simple">{{ errorMessage }}</Message>
    <Message v-if="mode === 'edit' && placementWarning" severity="warn" variant="simple">{{ placementWarning }}</Message>
    <Message v-if="mode === 'view' && telemetryError" severity="warn" variant="simple">{{ telemetryError }}</Message>

    <div class="layout-editor__body" :class="{ 'layout-editor__body--view': mode === 'view' }">
      <aside v-if="mode === 'edit'" class="layout-panel layout-panel--left">
        <section class="layout-panel__section">
          <div class="layout-panel__heading">
            <span>Dimensions</span>
            <small>{{ layout.width }} x {{ layout.height }} {{ layout.unit }}</small>
          </div>
          <div class="layout-dimensions">
            <label>
              <span>Width</span>
              <InputNumber
                :model-value="layout.width"
                :min="1"
                :max="1000"
                :max-fraction-digits="1"
                suffix=" m"
                fluid
                @update:model-value="setLayoutNumber('width', $event)"
              />
            </label>
            <label>
              <span>Height</span>
              <InputNumber
                :model-value="layout.height"
                :min="1"
                :max="1000"
                :max-fraction-digits="1"
                suffix=" m"
                fluid
                @update:model-value="setLayoutNumber('height', $event)"
              />
            </label>
          </div>
        </section>

        <section class="layout-panel__section">
          <div class="layout-panel__heading">
            <span>Geometry</span>
            <small>{{ getGeometryOption(layout.geometry.type).label }} / {{ geometryPoints.length }} pts</small>
          </div>
          <label>
            <span>Room shape</span>
            <Select
              :model-value="layout.geometry.type"
              :options="geometryOptions"
              option-label="label"
              option-value="value"
              fluid
              :disabled="mode !== 'edit'"
              @update:model-value="setGeometryType($event)"
            >
              <template #value="{ value }">
                <div class="layout-select-value">
                  <span class="material-symbols-outlined">{{ getGeometryOption(value).symbol }}</span>
                  <span>{{ getGeometryOption(value).label }}</span>
                </div>
              </template>
              <template #option="{ option }">
                <div class="layout-select-option">
                  <span class="material-symbols-outlined">{{ option.symbol }}</span>
                  <span>
                    <strong>{{ option.label }}</strong>
                    <small>{{ option.description }}</small>
                  </span>
                </div>
              </template>
            </Select>
          </label>

          <div class="layout-vertices">
            <div class="layout-vertices__header">
              <span>#</span>
              <span>X</span>
              <span>Y</span>
            </div>
            <div class="layout-vertices__rows">
              <div
                v-for="(point, index) in geometryPoints"
                :key="`geometry-point-${index}`"
                class="layout-vertex-row"
              >
                <span class="layout-vertex-row__index">{{ index + 1 }}</span>
                <InputNumber
                  :model-value="point.x"
                  :min="0"
                  :max="layout.width"
                  :max-fraction-digits="2"
                  :disabled="!canEditCustomGeometry"
                  fluid
                  @update:model-value="setGeometryPoint(index, 'x', $event)"
                />
                <InputNumber
                  :model-value="point.y"
                  :min="0"
                  :max="layout.height"
                  :max-fraction-digits="2"
                  :disabled="!canEditCustomGeometry"
                  fluid
                  @update:model-value="setGeometryPoint(index, 'y', $event)"
                />
              </div>
            </div>
            <div class="layout-geometry-actions">
              <Button label="Add vertex" icon="pi pi-plus" severity="secondary" variant="text" :disabled="!canEditCustomGeometry" @click="addGeometryPoint" />
              <Button label="Remove" icon="pi pi-minus" severity="secondary" variant="text" :disabled="!canEditCustomGeometry || geometryPoints.length <= 3" @click="removeGeometryPoint" />
            </div>
          </div>
        </section>

        <section class="layout-panel__section">
          <div class="layout-panel__heading">
            <span>Add element</span>
            <small>{{ layout.items.length }} placed</small>
          </div>
          <div class="layout-tool-grid">
            <button
              v-for="itemType in itemTypes"
              :key="itemType.value"
              type="button"
              class="layout-tool app-clickable"
              :disabled="mode !== 'edit'"
              @click="addLayoutItem(itemType.value)"
            >
              <span class="material-symbols-outlined">{{ itemType.symbol }}</span>
              <span>{{ itemType.label }}</span>
            </button>
          </div>
        </section>

        <section class="layout-panel__section">
          <div class="layout-panel__heading">
            <span>Plan actions</span>
          </div>
          <div class="layout-stack">
            <Button label="Clear items" icon="pi pi-trash" severity="danger" variant="text" :disabled="mode !== 'edit' || layout.items.length === 0" @click="clearItems" />
            <Button label="Restore saved" icon="pi pi-history" severity="secondary" variant="text" :disabled="!isDirty" @click="restoreSavedLayout" />
          </div>
        </section>
      </aside>

      <main class="layout-canvas-shell">
        <div v-if="isLoading" class="layout-loading">
          <Skeleton width="70%" height="70%" />
        </div>

        <div v-else class="layout-canvas">
          <div class="layout-ruler layout-ruler--top">
            <span>0 {{ layout.unit }}</span>
            <span>{{ layout.width }} {{ layout.unit }}</span>
          </div>
          <div class="layout-canvas__row">
            <div class="layout-ruler layout-ruler--side">
              <span>0 {{ layout.unit }}</span>
              <span>{{ layout.height }} {{ layout.unit }}</span>
            </div>
            <div class="layout-board-wrap">
              <div
                ref="boardRef"
                class="layout-board"
                :class="{
                  'layout-board--edit': mode === 'edit',
                  'layout-board--map-visible': mode === 'view' && hasMapOverlay
                }"
                :style="boardStyle"
                @pointerdown="clearSelection"
              >
                <svg
                  class="layout-board__grid"
                  :viewBox="`0 0 ${layout.width} ${layout.height}`"
                  preserveAspectRatio="none"
                  aria-hidden="true"
                >
                  <defs>
                    <clipPath :id="gridClipId">
                      <polygon :points="geometrySvgPoints" />
                    </clipPath>
                  </defs>
                  <g :clip-path="`url(#${gridClipId})`">
                    <line
                      v-for="line in boardGridLines"
                      :key="line.id"
                      class="layout-board__grid-line"
                      :class="{ 'layout-board__grid-line--major': line.major }"
                      :x1="line.x1"
                      :y1="line.y1"
                      :x2="line.x2"
                      :y2="line.y2"
                    />
                  </g>
                </svg>
                <svg
                  class="layout-board__shape"
                  :viewBox="`0 0 ${layout.width} ${layout.height}`"
                  preserveAspectRatio="none"
                  aria-hidden="true"
                >
                  <polygon class="layout-board__shape-fill" :points="geometrySvgPoints" />
                  <polyline class="layout-board__shape-line" :points="`${geometrySvgPoints} ${geometryPoints[0]?.x || 0},${geometryPoints[0]?.y || 0}`" />
                  <circle
                    v-for="(point, index) in geometryPoints"
                    :key="`geometry-handle-${index}`"
                    class="layout-board__vertex"
                    :class="{ 'layout-board__vertex--editable': canEditCustomGeometry }"
                    :cx="point.x"
                    :cy="point.y"
                    :r="vertexRadius"
                    @pointerdown.stop="onVertexPointerDown($event, index)"
                  />
                </svg>
                <svg
                  v-if="mode === 'view' && hasMapOverlay"
                  class="layout-board__map-field"
                  :viewBox="`0 0 ${layout.width} ${layout.height}`"
                  preserveAspectRatio="none"
                  aria-hidden="true"
                >
                  <defs>
                    <clipPath :id="roomClipId">
                      <polygon :points="geometrySvgPoints" />
                    </clipPath>
                    <radialGradient
                      v-for="zone in mapGradientZones"
                      :id="zone.gradientId"
                      :key="zone.gradientId"
                      gradientUnits="userSpaceOnUse"
                      :cx="zone.x"
                      :cy="zone.y"
                      :r="zone.radius"
                    >
                      <stop offset="0%" :stop-color="zone.color" :stop-opacity="zone.centerOpacity" />
                      <stop offset="56%" :stop-color="zone.color" :stop-opacity="zone.midOpacity" />
                      <stop offset="100%" :stop-color="zone.color" stop-opacity="0" />
                    </radialGradient>
                  </defs>
                  <g :clip-path="`url(#${roomClipId})`">
                    <rect
                      v-if="mapGradientBase"
                      class="layout-board__map-gradient-base"
                      x="0"
                      y="0"
                      :width="layout.width"
                      :height="layout.height"
                      :style="{ fill: mapGradientBase.color, opacity: mapGradientBase.opacity }"
                    />
                    <circle
                      v-for="zone in mapGradientZones"
                      :key="zone.id"
                      class="layout-board__map-gradient-zone"
                      :cx="zone.x"
                      :cy="zone.y"
                      :r="zone.radius"
                      :fill="`url(#${zone.gradientId})`"
                    />
                    <g class="layout-board__map-cells">
                      <rect
                        v-for="cell in mapCells"
                        :key="cell.id"
                        class="layout-board__map-cell"
                        :x="cell.x"
                        :y="cell.y"
                        :width="cell.width"
                        :height="cell.height"
                        :style="{ fill: cell.color, opacity: cell.opacity }"
                      />
                    </g>
                    <path
                      v-for="overlay in ventInfluenceOverlays"
                      :key="overlay.id"
                      class="layout-board__vent-influence"
                      :d="overlay.path"
                      :style="{
                        fill: overlay.color,
                        fillOpacity: overlay.fillOpacity,
                        stroke: overlay.color,
                        opacity: overlay.opacity,
                        '--layout-influence-stroke-opacity': overlay.strokeOpacity
                      }"
                    />
                    <circle
                      v-for="overlay in sensorInfluenceOverlays"
                      :key="overlay.id"
                      class="layout-board__sensor-influence"
                      :cx="overlay.x"
                      :cy="overlay.y"
                      :r="overlay.radius"
                      :style="{
                        fill: overlay.color,
                        fillOpacity: overlay.fillOpacity,
                        stroke: overlay.color,
                        opacity: overlay.opacity,
                        '--layout-influence-stroke-opacity': overlay.strokeOpacity
                      }"
                    />
                    <path
                      v-for="stream in airflowStreamlines"
                      :key="stream.id"
                      class="layout-board__airflow-stream"
                      :d="stream.path"
                      pathLength="1"
                      :style="{
                        '--layout-airflow-opacity': stream.opacity,
                        strokeWidth: stream.strokeWidth,
                        '--layout-airflow-delay': stream.delay,
                        '--layout-airflow-duration': stream.duration,
                        '--layout-airflow-dasharray': stream.dashArray
                      }"
                    />
                  </g>
                </svg>
                <component
                  :is="mode === 'edit' ? 'button' : 'div'"
                  v-for="item in layout.items"
                  :key="item.id"
                  :type="mode === 'edit' ? 'button' : undefined"
                  class="layout-item"
                  :class="[
                    `layout-item--${getItemType(item.type).tone}`,
                    {
                      'layout-item--selected': mode === 'edit' && item.id === selectedId,
                      'layout-item--editable': mode === 'edit',
                      'layout-item--invalid': hasItemPlacementError(item),
                      'layout-item--has-telemetry': mode === 'view' && hasItemTelemetry(item)
                    }
                  ]"
                  :aria-label="getItemAriaLabel(item)"
                  :role="mode === 'view' ? 'img' : undefined"
                  :tabindex="mode === 'view' && hasItemTelemetry(item) ? 0 : undefined"
                  :title="getItemPlacementTitle(item)"
                  :style="[getItemStyle(item), getItemTelemetryStyle(item)]"
                  @click.stop="selectLayoutItem(item)"
                  @pointerdown.stop="onItemPointerDown($event, item)"
                >
                  <span class="layout-item__content">
                    <span class="material-symbols-outlined">{{ getItemType(item.type).symbol }}</span>
                    <span class="layout-item__label">{{ getItemMapLabel(item) }}</span>
                  </span>
                  <span v-if="mode === 'edit' && isDirectionalItem(item.type)" class="layout-item__direction" aria-hidden="true">
                    <span class="layout-item__direction-head" />
                  </span>
                  <span
                    v-if="mode === 'view' && getItemHoverMetrics(item).length"
                    class="layout-item__metric-popover"
                    aria-hidden="true"
                  >
                    <span class="layout-item__metric-title">{{ getItemDisplayName(item) }}</span>
                    <span class="layout-item__metric-list">
                      <span
                        v-for="metric in getItemHoverMetrics(item)"
                        :key="metric.key"
                        class="layout-item__metric-chip"
                        :class="`layout-item__metric-chip--${metric.tone}`"
                      >
                        <span class="material-symbols-outlined">{{ metric.icon }}</span>
                        <strong>{{ metric.shortValue }}</strong>
                        <small>{{ metric.label }}</small>
                      </span>
                    </span>
                  </span>
                  <span v-if="canTransformItem(item)" class="layout-item__rotate-arm" aria-hidden="true" />
                  <span
                    v-if="canTransformItem(item)"
                    class="layout-item__rotate-handle"
                    title="Rotate"
                    aria-hidden="true"
                    @pointerdown.stop.prevent="onRotatePointerDown($event, item)"
                  >
                    <span class="material-symbols-outlined">rotate_right</span>
                  </span>
                  <template v-if="canTransformItem(item)">
                    <span
                      v-for="handle in resizeHandles"
                      :key="handle"
                      class="layout-item__resize-handle"
                      :class="`layout-item__resize-handle--${handle}`"
                      :title="`Resize ${handle.toUpperCase()}`"
                      aria-hidden="true"
                      @pointerdown.stop.prevent="onResizePointerDown($event, item, handle)"
                    />
                  </template>
                </component>
                <svg
                  v-if="mode === 'view' && ventDirectionCues.length"
                  class="layout-board__airflow-cues"
                  :viewBox="`0 0 ${layout.width} ${layout.height}`"
                  preserveAspectRatio="none"
                  aria-hidden="true"
                >
                  <defs>
                    <clipPath :id="airflowCueClipId">
                      <polygon :points="geometrySvgPoints" />
                    </clipPath>
                    <marker
                      :id="airflowCueMarkerId"
                      markerWidth="7"
                      markerHeight="7"
                      refX="6"
                      refY="3.5"
                      orient="auto"
                      markerUnits="strokeWidth"
                    >
                      <path d="M0,0 L7,3.5 L0,7 Z" class="layout-board__airflow-cue-marker" />
                    </marker>
                  </defs>
                  <g :clip-path="`url(#${airflowCueClipId})`">
                    <g
                      v-for="cue in ventDirectionCues"
                      :key="cue.id"
                      class="layout-board__airflow-cue"
                      :style="{ opacity: cue.opacity }"
                    >
                      <line
                        class="layout-board__airflow-cue-stem"
                        :x1="cue.x1"
                        :y1="cue.y1"
                        :x2="cue.x2"
                        :y2="cue.y2"
                        pathLength="1"
                        :marker-end="`url(#${airflowCueMarkerId})`"
                      />
                    </g>
                  </g>
                </svg>
              </div>
            </div>
          </div>
          <div v-if="mode === 'view'" class="layout-view-legend" aria-label="Room layout legend">
            <span class="layout-view-legend__metric">{{ layout.width }} x {{ layout.height }} {{ layout.unit }}</span>
            <span class="layout-view-legend__metric">{{ getGeometryOption(layout.geometry.type).label }}</span>
            <span v-for="itemType in viewLegendItemTypes" :key="itemType.value">
              <i :class="`layout-legend__dot layout-legend__dot--${itemType.tone}`" />
              {{ itemType.label }}
            </span>
          </div>
          <section v-if="mode === 'view'" class="layout-telemetry-panel" aria-label="Room telemetry values">
            <div class="layout-telemetry-panel__header">
              <div class="layout-telemetry-panel__title">
                <span>Live values</span>
                <small v-if="hasTelemetryLoaded">{{ sensors.length }} sensors / {{ devices.length }} vents</small>
              </div>
              <label class="layout-map-control">
                <span>Map</span>
                <Select
                  :model-value="activeMapLayer"
                  :options="mapLayerOptions"
                  option-label="label"
                  option-value="value"
                  class="layout-map-control__select"
                  @update:model-value="setActiveMapLayer($event)"
                >
                  <template #value="{ value }">
                    <span class="layout-map-select-value">
                      <span class="material-symbols-outlined">{{ getMapLayerOption(value).icon }}</span>
                      <span>{{ getMapLayerOption(value).label }}</span>
                    </span>
                  </template>
                  <template #option="{ option }">
                    <span class="layout-map-select-option">
                      <span class="material-symbols-outlined">{{ option.icon }}</span>
                      <span>
                        <strong>{{ option.label }}</strong>
                        <small>{{ option.description }}</small>
                      </span>
                    </span>
                  </template>
                </Select>
              </label>
            </div>

            <div v-if="boardMetrics.length" class="layout-telemetry-summary">
              <span
                v-for="metric in boardMetrics"
                :key="metric.key"
                class="layout-telemetry-card"
                :class="`layout-telemetry-card--${metric.tone}`"
              >
                <span class="material-symbols-outlined">{{ metric.icon }}</span>
                <strong>{{ metric.shortValue }}</strong>
                <small>{{ metric.label }}</small>
              </span>
            </div>

            <span v-if="hasTelemetryLoaded && !boardMetrics.length" class="layout-telemetry-panel__empty">
              No live values for placed sensors or ventilation devices.
            </span>
          </section>
        </div>
      </main>

      <aside v-if="mode === 'edit'" class="layout-panel layout-panel--inspector">
        <section v-if="selectedItem" class="layout-panel__section">
          <div class="layout-panel__heading">
            <span>Inspector</span>
            <small>{{ getItemType(selectedItem.type).label }}</small>
          </div>

          <div class="layout-stack">
            <Message v-if="selectedItemPlacementError" severity="warn" variant="simple">{{ selectedItemPlacementError }}</Message>

            <label>
              <span>Label</span>
              <InputText
                :model-value="selectedItem.label || ''"
                placeholder="Optional label"
                fluid
                @update:model-value="setSelectedText($event)"
              />
            </label>

            <div v-if="selectedBoundAsset" class="layout-bound-asset">
              <span>Linked asset</span>
              <strong>{{ selectedBoundAsset.label }}</strong>
              <small>{{ selectedBoundAsset.detail }}</small>
            </div>

            <label>
              <span>Type</span>
              <Select
                :model-value="selectedItem.type"
                :options="itemTypes"
                option-label="label"
                option-value="value"
                fluid
                :disabled="isSelectedRequiredAsset"
                @update:model-value="setSelectedType($event)"
              >
                <template #value="{ value }">
                  <div class="layout-select-value">
                    <span class="material-symbols-outlined">{{ getItemType(value).symbol }}</span>
                    <span>{{ getItemType(value).label }}</span>
                  </div>
                </template>
                <template #option="{ option }">
                  <div class="layout-select-option">
                    <span class="material-symbols-outlined">{{ option.symbol }}</span>
                    <span>
                      <strong>{{ option.label }}</strong>
                      <small>{{ option.description }}</small>
                    </span>
                  </div>
                </template>
              </Select>
            </label>

            <div class="layout-inspector-grid">
              <label>
                <span>X</span>
                <InputNumber :model-value="selectedItem.x" :min="0" :max="layout.width" :max-fraction-digits="2" fluid @update:model-value="setSelectedNumber('x', $event)" />
              </label>
              <label>
                <span>Y</span>
                <InputNumber :model-value="selectedItem.y" :min="0" :max="layout.height" :max-fraction-digits="2" fluid @update:model-value="setSelectedNumber('y', $event)" />
              </label>
              <label>
                <span>W</span>
                <InputNumber :model-value="selectedItem.width" :min="0.1" :max="layout.width" :max-fraction-digits="2" fluid @update:model-value="setSelectedNumber('width', $event)" />
              </label>
              <label>
                <span>H</span>
                <InputNumber :model-value="selectedItem.height" :min="0.1" :max="layout.height" :max-fraction-digits="2" fluid @update:model-value="setSelectedNumber('height', $event)" />
              </label>
            </div>

            <label>
              <span>Rotation</span>
              <InputNumber :model-value="selectedItem.rotation" :min="-360" :max="360" suffix=" deg" fluid @update:model-value="setSelectedNumber('rotation', $event)" />
            </label>

            <div class="layout-inspector-actions">
              <Button label="Duplicate" icon="pi pi-copy" severity="secondary" variant="text" :disabled="mode !== 'edit' || isSelectedRequiredAsset" @click="duplicateSelected" />
              <Button label="Remove" icon="pi pi-trash" severity="danger" variant="text" :disabled="mode !== 'edit' || isSelectedRequiredAsset" @click="removeSelected" />
            </div>
          </div>
        </section>

        <section v-else class="layout-panel__section layout-empty-inspector">
          <span class="material-symbols-outlined">ads_click</span>
          <strong>Select an element</strong>
          <p>Click an item on the plan to edit its label, coordinates, size, and rotation.</p>
        </section>

        <section class="layout-panel__section">
          <div class="layout-panel__heading">
            <span>Legend</span>
          </div>
          <div class="layout-legend">
            <span v-for="itemType in itemTypes" :key="itemType.value">
              <i :class="`layout-legend__dot layout-legend__dot--${itemType.tone}`" />
              {{ itemType.label }}
            </span>
          </div>
        </section>
      </aside>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed, inject, onMounted, onUnmounted, ref, watch, type ComputedRef } from "vue";
import { useRoute } from "vue-router";
import Button from "primevue/button";
import InputNumber from "primevue/inputnumber";
import InputText from "primevue/inputtext";
import Message from "primevue/message";
import Select from "primevue/select";
import Skeleton from "primevue/skeleton";
import Tag from "primevue/tag";
import { useToast } from "primevue/usetoast";
import { getRoomDevices, getRoomLayout, getRoomSensors, updateRoomLayout } from "@/services/apiService";
import type {
  RoomLayout,
  RoomLayoutGeometry,
  RoomLayoutGeometryType,
  RoomLayoutItem,
  RoomLayoutItemType,
  RoomLayoutPoint,
} from "@/types/room";
import {
  PARAMETER_ICONS,
  PARAMETER_LABELS,
  type Device,
  type Parameter,
  type Sensor,
} from "@/types/sensor";

type EditorMode = "view" | "edit";
type ResizeHandle = "nw" | "ne" | "sw" | "se";
type RoomAssetKind = "sensor" | "vent";
type TelemetryTone = "cool" | "normal" | "warm" | "hot" | "humid" | "co2" | "vent";
type RoomMapLayer = "off" | "temperature" | "humidity" | "co2" | "device_speed";
type LayoutItemOption = {
  value: RoomLayoutItemType;
  label: string;
  description: string;
  symbol: string;
  tone: string;
  width: number;
  height: number;
};

type GeometryOption = {
  value: RoomLayoutGeometryType;
  label: string;
  description: string;
  symbol: string;
};
type MapLayerOption = {
  value: RoomMapLayer;
  label: string;
  description: string;
  icon: string;
  source: "none" | RoomAssetKind;
};
type TelemetryEntity = {
  id: number;
  serial_number: string;
};
type RoomAssetSummary = {
  label: string;
  detail: string;
};
type LayoutMetric = {
  key: string;
  label: string;
  value: string;
  shortValue: string;
  icon: string;
  tone: TelemetryTone;
};
type MapCell = {
  id: string;
  x: number;
  y: number;
  width: number;
  height: number;
  color: string;
  opacity: string;
};
type MapCellDraft = Omit<MapCell, "color" | "opacity"> & {
  field: FieldValue;
};
type MapColorContext = {
  min: number;
  max: number;
  mean: number;
  spread: number;
};
type SensorInfluenceOverlay = {
  id: string;
  x: number;
  y: number;
  radius: number;
  color: string;
  fillOpacity: string;
  opacity: string;
  strokeOpacity: string;
};
type VentInfluenceOverlay = {
  id: string;
  path: string;
  color: string;
  fillOpacity: string;
  opacity: string;
  strokeOpacity: string;
};
type BoardGridLine = {
  id: string;
  x1: number;
  y1: number;
  x2: number;
  y2: number;
  major: boolean;
};
type AirflowVector = {
  id: string;
  x1: number;
  y1: number;
  x2: number;
  y2: number;
  opacity: string;
};
type VentDirectionCue = AirflowVector;
type AirflowStreamline = {
  id: string;
  path: string;
  opacity: string;
  delay: string;
  duration: string;
  dashArray: string;
  strokeWidth: string;
};
type MapGradientBase = {
  color: string;
  opacity: string;
};
type MapGradientZone = {
  id: string;
  gradientId: string;
  x: number;
  y: number;
  radius: number;
  color: string;
  centerOpacity: string;
  midOpacity: string;
};
type MapGradientDraft = {
  id: string;
  point: FieldPoint;
  field: FieldValue;
  radius: number;
};
type FieldPoint = {
  x: number;
  y: number;
};
type BoundaryProjection = {
  point: FieldPoint;
  distance: number;
};
type MapSample = {
  id: string;
  item: RoomLayoutItem;
  point: FieldPoint;
  value: number;
};
type FieldValue = {
  value: number;
  confidence: number;
  airflow: number;
};
type SensorFieldBase = {
  value: number;
  confidence: number;
};
type VentConditioning = {
  value: number;
  intensity: number;
  confidence: number;
};
type PaginatedData<T> = {
  data?: T[];
};

const itemTypes: LayoutItemOption[] = [
  { value: "sensor", label: "Sensor", description: "Telemetry node or sensor body", symbol: "sensors", tone: "sensor", width: 0.55, height: 0.55 },
  { value: "vent", label: "Ventilation", description: "Air inlet, exhaust, HVAC element", symbol: "mode_fan", tone: "vent", width: 0.75, height: 0.75 },
  { value: "door", label: "Door", description: "Entry or service door", symbol: "door_open", tone: "access", width: 0.9, height: 0.24 },
  { value: "window", label: "Window", description: "Window or transparent opening", symbol: "window", tone: "access", width: 1.2, height: 0.22 },
  { value: "desk", label: "Desk", description: "Operator or residential furniture", symbol: "desk", tone: "furniture", width: 1.2, height: 0.65 },
  { value: "equipment", label: "Equipment", description: "Machine, rack, production unit", symbol: "precision_manufacturing", tone: "equipment", width: 1.05, height: 0.85 },
  { value: "zone", label: "Zone", description: "Operational or controlled area", symbol: "crop_square", tone: "zone", width: 1.6, height: 1.1 },
  { value: "obstacle", label: "Obstacle", description: "Column, wall block, fixed obstruction", symbol: "block", tone: "obstacle", width: 0.8, height: 0.8 },
];

const geometryOptions: GeometryOption[] = [
  { value: "rectangle", label: "Rectangle", description: "Regular room or open office bay", symbol: "crop_square" },
  { value: "l_shape", label: "L-shape", description: "Room with a recessed technical zone", symbol: "polyline" },
  { value: "t_shape", label: "T-shape", description: "Cross-zone or combined working area", symbol: "join_inner" },
  { value: "custom", label: "Custom polygon", description: "Manual contour by editable vertices", symbol: "conversion_path" },
];
const mapLayerOptions: MapLayerOption[] = [
  { value: "temperature", label: "Heat", description: "Temperature field from room sensors", icon: PARAMETER_ICONS.temperature, source: "sensor" },
  { value: "humidity", label: "Humidity", description: "Humidity field from room sensors", icon: PARAMETER_ICONS.humidity, source: "sensor" },
  { value: "co2", label: "CO₂", description: "CO₂ concentration field from room sensors", icon: PARAMETER_ICONS.co2, source: "sensor" },
  { value: "device_speed", label: "Ventilation", description: "Fan speed field from ventilation devices", icon: "mode_fan", source: "vent" },
  { value: "off", label: "Off", description: "No map overlay", icon: "layers_clear", source: "none" },
];
const resizeHandles: ResizeHandle[] = ["nw", "ne", "sw", "se"];

const route = useRoute();
const toast = useToast();
const envId = Number(route.params.envId);
const roomId = Number(route.params.roomId);
const layoutLoadTimeoutMs = 5000;
const telemetryRefreshMs = 30000;
const mapGridColumns = 96;
const mapGradientColumns = 24;
const roomClipId = `layout-room-clip-${roomId}`;
const gridClipId = `layout-room-grid-clip-${roomId}`;
const airflowCueMarkerId = `layout-airflow-cue-marker-${roomId}`;
const airflowCueClipId = `layout-airflow-cue-clip-${roomId}`;
const boardRef = ref<HTMLElement | null>(null);
const layout = ref<RoomLayout>(createDefaultLayout());
const savedLayout = ref<RoomLayout>(createDefaultLayout());
const sensors = ref<Sensor[]>([]);
const devices = ref<Device[]>([]);
const selectedId = ref<string | null>(null);
const mode = ref<EditorMode>("view");
const isLoading = ref(true);
const isSaving = ref(false);
const isTelemetryLoading = ref(false);
const hasLoaded = ref(false);
const hasTelemetryLoaded = ref(false);
const errorMessage = ref("");
const telemetryError = ref("");
const activeMapLayer = ref<RoomMapLayer>("temperature");
const injectedReadOnly = inject<ComputedRef<boolean>>("roomReadOnly", computed(() => false));
const isReadOnly = computed(() => injectedReadOnly.value);
let telemetryInterval: ReturnType<typeof setInterval> | undefined;

const activeDrag = ref<{
  id: string;
  offsetX: number;
  offsetY: number;
} | null>(null);
const activeResize = ref<{
  id: string;
  handle: ResizeHandle;
  startX: number;
  startY: number;
  startWidth: number;
  startHeight: number;
} | null>(null);
const activeRotate = ref<{
  id: string;
  centerX: number;
  centerY: number;
  startAngle: number;
  startRotation: number;
} | null>(null);
const activeVertexIndex = ref<number | null>(null);

const boardStyle = computed(() => ({
  "--layout-ratio": `${layout.value.width} / ${layout.value.height}`,
}));

const selectedItem = computed(() => layout.value.items.find((item) => item.id === selectedId.value) ?? null);
const selectedBoundAsset = computed(() => (selectedItem.value ? getBoundAssetSummary(selectedItem.value) : null));
const isSelectedRequiredAsset = computed(() => Boolean(selectedBoundAsset.value));
const isDirty = computed(() => JSON.stringify(layout.value) !== JSON.stringify(savedLayout.value));
const geometryPoints = computed(() => layout.value.geometry.points);
const roomCentroid = computed<FieldPoint>(() => calculateRoomCentroid(geometryPoints.value));
const geometrySvgPoints = computed(() => geometryPoints.value.map((point) => `${point.x},${point.y}`).join(" "));
const canEditCustomGeometry = computed(() => mode.value === "edit" && layout.value.geometry.type === "custom");
const vertexRadius = computed(() => Math.max(0.07, Math.min(layout.value.width, layout.value.height) * 0.018));
const viewLegendItemTypes = computed(() => {
  const usedTypes = new Set(layout.value.items.map((item) => getItemType(item.type).value));
  return itemTypes.filter((itemType) => usedTypes.has(itemType.value));
});
const sensorLayoutItems = computed(() => layout.value.items.filter((item) => getItemType(item.type).value === "sensor"));
const ventLayoutItems = computed(() => layout.value.items.filter((item) => getItemType(item.type).value === "vent"));
const boardGridLines = computed<BoardGridLine[]>(() => {
  const step = getBoardGridStep();
  const majorStep = step * 2;
  const lines: BoardGridLine[] = [];

  for (let x = step; x < layout.value.width; x += step) {
    const value = roundMapCoordinate(x);
    lines.push({
      id: `grid-x-${value}`,
      x1: value,
      y1: 0,
      x2: value,
      y2: layout.value.height,
      major: isGridMajorLine(value, majorStep),
    });
  }

  for (let y = step; y < layout.value.height; y += step) {
    const value = roundMapCoordinate(y);
    lines.push({
      id: `grid-y-${value}`,
      x1: 0,
      y1: value,
      x2: layout.value.width,
      y2: value,
      major: isGridMajorLine(value, majorStep),
    });
  }

  return lines;
});
const mapCells = computed<MapCell[]>(() => {
  const layer = activeMapLayer.value;
  if (layer === "off" || layer !== "device_speed") return [];

  const columns = mapGridColumns;
  const rows = clamp(Math.round((columns * layout.value.height) / layout.value.width), 32, 72);
  const cellWidth = layout.value.width / columns;
  const cellHeight = layout.value.height / rows;
  const cellOverlap = Math.max(cellWidth, cellHeight) * 0.1;
  const samples = getMapSamples(layer);
  if (layer !== "device_speed" && !samples.length) return [];

  const drafts: MapCellDraft[] = [];
  for (let row = 0; row < rows; row += 1) {
    for (let column = 0; column < columns; column += 1) {
      const point = {
        x: (column + 0.5) * cellWidth,
        y: (row + 0.5) * cellHeight,
      };

      if (!isPointInsidePolygon(point, geometryPoints.value)) continue;

      const field = layer === "device_speed"
        ? getVentilationFieldValue(point)
        : getSensorFieldValue(point, layer, samples);

      if (!field) continue;

      drafts.push({
        id: `${layer}-${row}-${column}`,
        x: roundMapCoordinate(column * cellWidth - cellOverlap / 2),
        y: roundMapCoordinate(row * cellHeight - cellOverlap / 2),
        width: roundMapCoordinate(cellWidth + cellOverlap),
        height: roundMapCoordinate(cellHeight + cellOverlap),
        field,
      });
    }
  }

  const colorContext = createMapColorContext(layer, drafts.map((draft) => draft.field.value));
  return drafts.map((draft) => ({
    id: draft.id,
    x: draft.x,
    y: draft.y,
    width: draft.width,
    height: draft.height,
    color: getMapCellColor(layer, draft.field.value, colorContext),
    opacity: String(round(getMapCellOpacity(layer, draft.field))),
  }));
});
const mapGradientSamples = computed<MapSample[]>(() => {
  const layer = activeMapLayer.value;
  if (layer === "off" || layer === "device_speed") return [];
  return getMapSamples(layer);
});
const mapGradientFieldDrafts = computed<MapGradientDraft[]>(() => {
  const layer = activeMapLayer.value;
  const samples = mapGradientSamples.value;
  if (!samples.length) return [];

  const columns = mapGradientColumns;
  const rows = clamp(Math.round((columns * layout.value.height) / layout.value.width), 12, 24);
  const cellWidth = layout.value.width / columns;
  const cellHeight = layout.value.height / rows;
  const radius = clamp(Math.max(cellWidth, cellHeight) * 2.25, 0.55, getRoomDiagonal() * 0.24);
  const drafts: MapGradientDraft[] = [];

  for (let row = 0; row < rows; row += 1) {
    for (let column = 0; column < columns; column += 1) {
      const point = {
        x: (column + 0.5) * cellWidth,
        y: (row + 0.5) * cellHeight,
      };

      if (!isPointInsidePolygon(point, geometryPoints.value)) continue;

      const field = getSensorFieldValue(point, layer, samples);
      if (!field) continue;

      drafts.push({
        id: `${layer}-${row}-${column}`,
        point,
        field,
        radius,
      });
    }
  }

  return drafts;
});
const mapGradientBase = computed<MapGradientBase | null>(() => {
  const layer = activeMapLayer.value;
  const drafts = mapGradientFieldDrafts.value;
  if (!drafts.length) return null;

  const values = drafts.map((draft) => draft.field.value);
  const context = createMapColorContext(layer, values);
  const background = getLayerBackgroundValue(layer, values);

  return {
    color: getMapCellColor(layer, background, context),
    opacity: String(round(layer === "temperature" ? 0.2 : 0.16)),
  };
});
const mapGradientZones = computed<MapGradientZone[]>(() => {
  const layer = activeMapLayer.value;
  const drafts = mapGradientFieldDrafts.value;
  if (!drafts.length) return [];

  const values = drafts.map((draft) => draft.field.value);
  const context = createMapColorContext(layer, values);
  const background = getLayerBackgroundValue(layer, values);
  const valueRange = Math.max(Math.abs((context?.max ?? Math.max(...values)) - (context?.min ?? Math.min(...values))), getLayerMinimumContrastRange(layer));

  return drafts.map((draft) => {
    const deviation = clamp(Math.abs(draft.field.value - background) / valueRange, 0.22, 1);
    const airflowBoost = clamp(draft.field.airflow * 0.26, 0, 0.26);
    return {
      id: `map-gradient-${draft.id}`,
      gradientId: `layout-map-gradient-${roomId}-${draft.id}`,
      x: roundMapCoordinate(draft.point.x),
      y: roundMapCoordinate(draft.point.y),
      radius: round(draft.radius),
      color: getMapCellColor(layer, draft.field.value, context),
      centerOpacity: String(round(0.16 + deviation * 0.26 + airflowBoost)),
      midOpacity: String(round(0.07 + deviation * 0.11 + airflowBoost * 0.55)),
    };
  });
});
const sensorInfluenceOverlays = computed<SensorInfluenceOverlay[]>(() => {
  const layer = activeMapLayer.value;
  if (layer === "off" || layer === "device_speed") return [];

  const samples = getMapSamples(layer);
  if (!samples.length) return [];

  return [];
});
const ventInfluenceOverlays = computed<VentInfluenceOverlay[]>(() => {
  const layer = activeMapLayer.value;
  if (layer === "off") return [];

  return ventLayoutItems.value
    .map((item) => {
      const device = getDeviceForItem(item);
      if (!device || device.fan_speed === null || device.fan_speed === undefined || Number.isNaN(Number(device.fan_speed))) {
        return null;
      }

      const speed = clamp(Number(device.fan_speed), 0, 100);
      if (speed <= 0) return null;

      const speedRatio = getVentSimulationSpeedRatio(speed);
      const outlet = getVentOutletPoint(item);
      const direction = getVentAirflowDirection(item);
      const reach = getVentVisualReach(speed);
      const baseHalfWidth = clamp(Math.min(item.width, item.height) * 0.3, 0.08, 0.24);
      const endHalfWidth = clamp(0.34 + speedRatio * 0.5, 0.34, Math.min(layout.value.width, layout.value.height) * 0.32);

      return {
        id: `vent-impact-${layer}-${item.id}`,
        path: createVentPlumePath(outlet, direction, reach, baseHalfWidth, endHalfWidth),
        color: getVentInfluenceColor(layer),
        fillOpacity: String(round(0.16 + speedRatio * 0.18)),
        opacity: String(round(0.44 + speedRatio * 0.28)),
        strokeOpacity: String(round(0.08 + speedRatio * 0.08)),
      };
    })
    .filter((overlay): overlay is VentInfluenceOverlay => overlay !== null);
});
const airflowStreamlines = computed<AirflowStreamline[]>(() => {
  if (activeMapLayer.value === "off") return [];

  return ventLayoutItems.value.flatMap((item) => {
    const device = getDeviceForItem(item);
    if (!device || device.fan_speed === null || device.fan_speed === undefined || Number.isNaN(Number(device.fan_speed))) {
      return [];
    }

    const speed = clamp(Number(device.fan_speed), 0, 100);
    if (speed <= 0) return [];

    const speedRatio = getVentSimulationSpeedRatio(speed);
    const outlet = getVentOutletPoint(item);
    const direction = getVentAirflowDirection(item);
    const reach = getVentVisualReach(speed) * (0.58 + speedRatio * 0.12);
    const count = speedRatio > 0.72 ? 6 : 5;
    const spread = clamp(Math.min(item.width, item.height) * (0.16 + speedRatio * 0.16), 0.08, 0.34);
    const streamRatios = [0, -0.46, 0.38, -0.18, 0.62, -0.68];
    const streams: AirflowStreamline[] = [];

    for (let index = 0; index < count; index += 1) {
      const ratio = streamRatios[index] ?? 0;
      const progress = (index + 0.45) / (count + 0.55);
      const startDistance = reach * (0.14 + progress * 0.54);
      const segmentLength = reach * (0.18 + speedRatio * 0.045) * (index % 2 === 0 ? 1.08 : 0.92);
      const lateralStart = ratio * spread * (0.42 + progress * 0.52);
      const lateralEnd = ratio * spread * (0.62 + progress * 0.78);
      const curve = ((index % 2 === 0 ? 1 : -1) * 0.018 + ratio * 0.024) * (0.48 + speedRatio);
      const path = createVentStreamlinePath(
        outlet,
        direction,
        startDistance,
        segmentLength,
        lateralStart,
        lateralEnd,
        curve,
      );

      streams.push({
        id: `airflow-stream-${item.id}-${index}`,
        path,
        opacity: String(round(0.42 + speedRatio * 0.2 - Math.abs(ratio) * 0.07)),
        delay: `${round(index * -0.24)}s`,
        duration: `${round(1.15 - speedRatio * 0.18 + index * 0.06)}s`,
        dashArray: `${round(0.72 + speedRatio * 0.08)} ${round(0.26 + Math.abs(ratio) * 0.12)}`,
        strokeWidth: `${round(0.04 + speedRatio * 0.014)}rem`,
      });
    }

    return streams;
  });
});
const hasMapOverlay = computed(() => activeMapLayer.value !== "off" && (
  mapCells.value.length > 0 ||
  mapGradientZones.value.length > 0 ||
  ventInfluenceOverlays.value.length > 0 ||
  airflowStreamlines.value.length > 0
));
const ventDirectionCues = computed<VentDirectionCue[]>(() => {
  if (activeMapLayer.value === "off") return [];

  return ventLayoutItems.value
    .map((item) => {
      const device = getDeviceForItem(item);
      if (!device || device.fan_speed === null || device.fan_speed === undefined || Number.isNaN(Number(device.fan_speed))) {
        return null;
      }

      const speed = clamp(Number(device.fan_speed), 0, 100);
      if (speed <= 0) return null;

      const direction = getVentAirflowDirection(item);
      const outlet = getVentOutletPoint(item);
      const cueSize = Math.min(item.width, item.height);
      const start = offsetPoint(outlet, direction, cueSize * 0.08);
      const end = offsetPoint(outlet, direction, cueSize * 0.7);

      return {
        id: `vent-direction-${item.id}`,
        x1: roundMapCoordinate(start.x),
        y1: roundMapCoordinate(start.y),
        x2: roundMapCoordinate(end.x),
        y2: roundMapCoordinate(end.y),
        opacity: String(round(0.68 + getVentSimulationSpeedRatio(speed) * 0.22)),
      };
    })
    .filter((cue): cue is VentDirectionCue => cue !== null);
});
const boardMetrics = computed<LayoutMetric[]>(() => {
  const metrics: LayoutMetric[] = [];
  const temperatures = getParameterValues("temperature");
  const humidities = getParameterValues("humidity");
  const co2Values = getParameterValues("co2");
  const fanSpeeds = devices.value
    .map((device) => device.fan_speed)
    .filter((value): value is number => value !== null && value !== undefined && !Number.isNaN(Number(value)));

  if (temperatures.length) {
    const value = average(temperatures);
    metrics.push(createMetric("temperature", "Avg temp", value, "°C", PARAMETER_ICONS.temperature, getTemperatureTone(value)));
  }

  if (co2Values.length) {
    const value = Math.max(...co2Values);
    metrics.push(createMetric("co2", "Peak CO₂", value, "ppm", PARAMETER_ICONS.co2, getCo2Tone(value)));
  }

  if (humidities.length) {
    const value = average(humidities);
    metrics.push(createMetric("humidity", "Avg humidity", value, "%", PARAMETER_ICONS.humidity, "humid"));
  }

  if (fanSpeeds.length) {
    const value = average(fanSpeeds);
    metrics.push(createMetric("device_speed", "Avg fan", value, "%", "mode_fan", "vent"));
  }

  return metrics;
});
const roomBoundPlacementErrors = computed(() => getLayoutPlacementErrors(layout.value).map((item) => getItemDisplayName(item)));
const hasPlacementErrors = computed(() => roomBoundPlacementErrors.value.length > 0);
const placementWarning = computed(() => {
  if (!hasPlacementErrors.value) return "";
  return `Sensors and ventilation must stay inside the room contour: ${roomBoundPlacementErrors.value.join(", ")}.`;
});
const selectedItemPlacementError = computed(() => {
  if (!selectedItem.value || !hasItemPlacementError(selectedItem.value)) return "";
  return `${getItemDisplayName(selectedItem.value)} must be fully inside the room contour.`;
});

watch(
  () => [layout.value.width, layout.value.height],
  () => {
    if (!hasLoaded.value) return;
    normalizeCurrentLayout();
  }
);

watch(isReadOnly, (readOnly) => {
  if (readOnly) setMode("view");
});

onMounted(() => {
  void loadLayout();
  void loadTelemetry();
  telemetryInterval = setInterval(() => void loadTelemetry({ silent: true }), telemetryRefreshMs);
});
onUnmounted(() => {
  removePointerListeners();
  if (telemetryInterval) clearInterval(telemetryInterval);
});

function createDefaultLayout(): RoomLayout {
  return {
    width: 6,
    height: 4,
    unit: "m",
    geometry: createGeometry("rectangle", 6, 4),
    items: [],
  };
}

function cloneLayout(value: RoomLayout): RoomLayout {
  return JSON.parse(JSON.stringify(value));
}

function clamp(value: number, min: number, max: number) {
  return Math.min(Math.max(value, min), max);
}

function round(value: number) {
  return Math.round(value * 100) / 100;
}

function roundMapCoordinate(value: number) {
  return Math.round(value * 10000) / 10000;
}

function normalizeAngle(value: number) {
  let result = value;
  while (result > 360) result -= 360;
  while (result < -360) result += 360;
  return round(result);
}

function getAngle(centerX: number, centerY: number, point: RoomLayoutPoint) {
  return (Math.atan2(point.y - centerY, point.x - centerX) * 180) / Math.PI;
}

function getItemType(type: string): LayoutItemOption {
  return itemTypes.find((itemType) => itemType.value === type) ?? itemTypes[0];
}

function getGeometryOption(type: string): GeometryOption {
  return geometryOptions.find((option) => option.value === type) ?? geometryOptions[0];
}

function getMapLayerOption(value: string | null | undefined): MapLayerOption {
  return mapLayerOptions.find((option) => option.value === value) ?? mapLayerOptions[0];
}

function setActiveMapLayer(value: string) {
  activeMapLayer.value = getMapLayerOption(value).value;
}

function getItemCenter(item: RoomLayoutItem): FieldPoint {
  return {
    x: item.x + item.width / 2,
    y: item.y + item.height / 2,
  };
}

function getItemDirection(item: RoomLayoutItem) {
  const radians = ((Number(item.rotation) || 0) * Math.PI) / 180;
  return {
    x: Math.cos(radians),
    y: Math.sin(radians),
  };
}

function getVentAirflowDirection(item: RoomLayoutItem): FieldPoint {
  const targetDirection = getVentRoomFacingDirection(item);
  return getAlignedItemSideDirection(item, targetDirection) ?? getItemDirection(item);
}

function getVentOutletPoint(item: RoomLayoutItem): FieldPoint {
  const center = getItemCenter(item);
  const direction = getVentAirflowDirection(item);
  const halfExtent = getItemHalfExtentAlongDirection(item, direction);
  return {
    x: center.x + direction.x * halfExtent,
    y: center.y + direction.y * halfExtent,
  };
}

function getVentRoomFacingDirection(item: RoomLayoutItem): FieldPoint {
  const center = getItemCenter(item);
  const boundary = getClosestRoomBoundaryPoint(center);
  const centroidDirection = normalizeVector({
    x: roomCentroid.value.x - center.x,
    y: roomCentroid.value.y - center.y,
  });

  if (boundary) {
    const roomScale = Math.min(layout.value.width, layout.value.height);
    const wallInfluenceDistance = Math.max(roomScale * 0.34, Math.max(item.width, item.height) * 1.8);
    const awayFromWall = normalizeVector({
      x: center.x - boundary.point.x,
      y: center.y - boundary.point.y,
    });

    if (boundary.distance <= wallInfluenceDistance && !isZeroVector(awayFromWall)) {
      return awayFromWall;
    }
  }

  return isZeroVector(centroidDirection) ? getItemDirection(item) : centroidDirection;
}

function getAlignedItemSideDirection(item: RoomLayoutItem, targetDirection: FieldPoint): FieldPoint | null {
  const sides = getItemSideDirections(item);
  if (!sides.length || isZeroVector(targetDirection)) return null;

  return sides.reduce((best, side) => {
    const score = side.x * targetDirection.x + side.y * targetDirection.y;
    return score > best.score ? { direction: side, score } : best;
  }, { direction: sides[0], score: Number.NEGATIVE_INFINITY }).direction;
}

function getItemSideDirections(item: RoomLayoutItem): FieldPoint[] {
  const xAxis = normalizeVector(getItemDirection(item));
  const yAxis = normalizeVector(getNormal(xAxis));

  return [
    xAxis,
    { x: -xAxis.x, y: -xAxis.y },
    yAxis,
    { x: -yAxis.x, y: -yAxis.y },
  ];
}

function getItemHalfExtentAlongDirection(item: RoomLayoutItem, direction: FieldPoint) {
  const xAxis = getItemDirection(item);
  const yAxis = getNormal(xAxis);
  const projectedWidth = Math.abs(direction.x * xAxis.x + direction.y * xAxis.y) * (item.width / 2);
  const projectedHeight = Math.abs(direction.x * yAxis.x + direction.y * yAxis.y) * (item.height / 2);
  return projectedWidth + projectedHeight;
}

function getClosestRoomBoundaryPoint(point: FieldPoint): BoundaryProjection | null {
  const points = geometryPoints.value;
  if (points.length < 2) return null;

  let closest: BoundaryProjection | null = null;
  for (let index = 0; index < points.length; index += 1) {
    const start = points[index];
    const end = points[(index + 1) % points.length];
    const projected = getClosestPointOnSegment(point, start, end);
    const distance = Math.hypot(point.x - projected.x, point.y - projected.y);
    if (!closest || distance < closest.distance) {
      closest = { point: projected, distance };
    }
  }

  return closest;
}

function getClosestPointOnSegment(point: FieldPoint, start: FieldPoint, end: FieldPoint): FieldPoint {
  const deltaX = end.x - start.x;
  const deltaY = end.y - start.y;
  const lengthSquared = deltaX ** 2 + deltaY ** 2;
  if (lengthSquared <= 0.0001) return { x: start.x, y: start.y };

  const ratio = clamp(((point.x - start.x) * deltaX + (point.y - start.y) * deltaY) / lengthSquared, 0, 1);
  return {
    x: start.x + deltaX * ratio,
    y: start.y + deltaY * ratio,
  };
}

function normalizeVector(vector: FieldPoint): FieldPoint {
  const length = Math.hypot(vector.x, vector.y);
  if (length <= 0.0001) {
    return { x: 0, y: 0 };
  }

  return {
    x: vector.x / length,
    y: vector.y / length,
  };
}

function isZeroVector(vector: FieldPoint) {
  return Math.abs(vector.x) <= 0.0001 && Math.abs(vector.y) <= 0.0001;
}

function calculateRoomCentroid(points: RoomLayoutPoint[]): FieldPoint {
  if (points.length < 3) {
    return {
      x: layout.value.width / 2,
      y: layout.value.height / 2,
    };
  }

  let doubleArea = 0;
  let centroidX = 0;
  let centroidY = 0;

  for (let index = 0; index < points.length; index += 1) {
    const current = points[index];
    const next = points[(index + 1) % points.length];
    const cross = current.x * next.y - next.x * current.y;
    doubleArea += cross;
    centroidX += (current.x + next.x) * cross;
    centroidY += (current.y + next.y) * cross;
  }

  if (Math.abs(doubleArea) <= 0.0001) {
    return {
      x: layout.value.width / 2,
      y: layout.value.height / 2,
    };
  }

  return {
    x: centroidX / (3 * doubleArea),
    y: centroidY / (3 * doubleArea),
  };
}

function getRoomDiagonal() {
  return Math.hypot(layout.value.width, layout.value.height);
}

function getVentSimulationSpeedRatio(speed: number) {
  const speedRatio = clamp(speed / 100, 0, 1);
  if (speedRatio <= 0) return 0;
  return clamp(0.32 + Math.sqrt(speedRatio) * 0.68, 0, 1);
}

function getVentReach(speed: number) {
  const speedRatio = getVentSimulationSpeedRatio(speed);
  const roomSize = Math.min(layout.value.width, layout.value.height);
  return clamp(roomSize * 0.45 + getRoomDiagonal() * speedRatio * 0.55, roomSize * 0.35, getRoomDiagonal());
}

function getVentVisualReach(speed: number) {
  const speedRatio = getVentSimulationSpeedRatio(speed);
  const roomSize = Math.min(layout.value.width, layout.value.height);
  return clamp(getVentReach(speed) * (0.42 + speedRatio * 0.16), roomSize * 0.38, getRoomDiagonal() * 0.48);
}

function getBoardGridStep() {
  const target = Math.max(layout.value.width, layout.value.height) / 12;
  return [0.25, 0.5, 1, 2, 5, 10, 20, 50].find((step) => step >= target) ?? 100;
}

function isGridMajorLine(value: number, majorStep: number) {
  const ratio = value / majorStep;
  return Math.abs(ratio - Math.round(ratio)) < 0.0001;
}

function getNormal(direction: FieldPoint): FieldPoint {
  return {
    x: -direction.y,
    y: direction.x,
  };
}

function offsetPoint(point: FieldPoint, vector: FieldPoint, distance: number): FieldPoint {
  return {
    x: point.x + vector.x * distance,
    y: point.y + vector.y * distance,
  };
}

function formatSvgPoint(point: FieldPoint) {
  return `${roundMapCoordinate(point.x)} ${roundMapCoordinate(point.y)}`;
}

function createVentPlumePath(
  outlet: FieldPoint,
  direction: FieldPoint,
  reach: number,
  startHalfWidth: number,
  endHalfWidth: number,
) {
  const normal = getNormal(direction);
  const end = offsetPoint(outlet, direction, reach);
  const control = offsetPoint(outlet, direction, reach * 0.52);
  const startLeft = offsetPoint(outlet, normal, startHalfWidth);
  const startRight = offsetPoint(outlet, normal, -startHalfWidth);
  const controlLeft = offsetPoint(control, normal, endHalfWidth * 0.7);
  const controlRight = offsetPoint(control, normal, -endHalfWidth * 0.7);
  const endLeft = offsetPoint(end, normal, endHalfWidth);
  const endRight = offsetPoint(end, normal, -endHalfWidth);

  return [
    `M ${formatSvgPoint(startLeft)}`,
    `Q ${formatSvgPoint(controlLeft)} ${formatSvgPoint(endLeft)}`,
    `L ${formatSvgPoint(endRight)}`,
    `Q ${formatSvgPoint(controlRight)} ${formatSvgPoint(startRight)}`,
    "Z",
  ].join(" ");
}

function createVentStreamlinePath(
  outlet: FieldPoint,
  direction: FieldPoint,
  startDistance: number,
  segmentLength: number,
  startOffset: number,
  endOffset: number,
  curveOffset: number,
) {
  const normal = getNormal(direction);
  const endDistance = startDistance + segmentLength;
  const start = offsetPoint(offsetPoint(outlet, direction, startDistance), normal, startOffset);
  const end = offsetPoint(offsetPoint(outlet, direction, endDistance), normal, endOffset);
  const control = offsetPoint(
    offsetPoint(outlet, direction, startDistance + segmentLength * 0.52),
    normal,
    (startOffset + endOffset) * 0.58 + curveOffset,
  );

  return `M ${formatSvgPoint(start)} Q ${formatSvgPoint(control)} ${formatSvgPoint(end)}`;
}

function getMapSamples(layer: RoomMapLayer): MapSample[] {
  if (layer === "off" || layer === "device_speed") return [];

  return sensorLayoutItems.value
    .map((item) => {
      const parameter = getSensorParameter(getSensorForItem(item), layer);
      if (!parameter || parameter.value === null || parameter.value === undefined || Number.isNaN(Number(parameter.value))) {
        return null;
      }

      return {
        id: `${layer}-${item.id}`,
        item,
        point: getItemCenter(item),
        value: Number(parameter.value),
      };
    })
    .filter((sample): sample is MapSample => sample !== null);
}

function getSensorFieldValue(point: FieldPoint, layer: RoomMapLayer, samples: MapSample[]): FieldValue | null {
  const airflow = getAirflowAtPoint(point);
  const baseField = interpolateSensorFieldBase(point, layer, samples, airflow);
  if (!baseField) return null;

  const values = samples.map((sample) => sample.value);
  const spatialValue = applySpatialUncertainty(layer, baseField.value, values, baseField.confidence);
  const advectedValue = applyAirflowAdvection(point, layer, spatialValue, samples, airflow);
  const conditionedField = applyVentConditioning(point, layer, advectedValue, values);
  const eddy = getAirflowEddy(layer, point, airflow, values);

  return {
    value: conditionedField.value + eddy,
    confidence: clamp(Math.max(baseField.confidence, conditionedField.confidence), 0.03, 1),
    airflow: Math.max(airflow.intensity, conditionedField.intensity),
  };
}

function interpolateSensorFieldBase(
  point: FieldPoint,
  layer: RoomMapLayer,
  samples: MapSample[],
  airflow: { x: number; y: number; intensity: number },
): SensorFieldBase | null {
  let weightedValue = 0;
  let weightSum = 0;

  for (const sample of samples) {
    const distance = Math.hypot(point.x - sample.point.x, point.y - sample.point.y);
    const baseWeight = 1 / (distance ** 2.25 + 0.08);
    const alignment = getFlowAlignment(sample.point, point, airflow);
    const obstruction = getObstructionFactor(sample.point, point);
    const downwindBoost = Math.max(0, alignment) * airflow.intensity * 1.15;
    const upwindPenalty = Math.max(0, -alignment) * airflow.intensity * 0.55;
    const weight = baseWeight * obstruction * (1 + downwindBoost) * (1 - upwindPenalty);

    weightedValue += sample.value * weight;
    weightSum += weight;
  }

  if (weightSum <= 0) return null;

  return {
    value: weightedValue / weightSum,
    confidence: clamp(weightSum / (weightSum + 0.72), 0.03, 1),
  };
}

function applyAirflowAdvection(
  point: FieldPoint,
  layer: RoomMapLayer,
  value: number,
  samples: MapSample[],
  airflow: { x: number; y: number; intensity: number },
) {
  if (airflow.intensity < 0.025 || samples.length === 0) return value;

  const traceDistance = clamp(getRoomDiagonal() * airflow.intensity * 0.2, 0.08, Math.min(layout.value.width, layout.value.height) * 0.4);
  const upstreamPoint = getBacktracedAirflowPoint(point, airflow, traceDistance);
  const upstreamAirflow = getAirflowAtPoint(upstreamPoint);
  const upstreamField = interpolateSensorFieldBase(upstreamPoint, layer, samples, upstreamAirflow);
  if (!upstreamField) return value;

  const mix = clamp(airflow.intensity * getLayerAdvectionRatio(layer), 0, 0.72);
  return value + (upstreamField.value - value) * mix;
}

function getBacktracedAirflowPoint(
  point: FieldPoint,
  airflow: { x: number; y: number; intensity: number },
  distance: number,
) {
  for (const ratio of [1, 0.75, 0.5, 0.25]) {
    const candidate = {
      x: point.x - airflow.x * distance * ratio,
      y: point.y - airflow.y * distance * ratio,
    };

    if (isPointInsidePolygon(candidate, geometryPoints.value)) {
      return candidate;
    }
  }

  return point;
}

function applyVentConditioning(point: FieldPoint, layer: RoomMapLayer, value: number, values: number[]) {
  const conditioning = getVentConditioningAtPoint(point, layer, values);
  if (!conditioning) {
    return {
      value,
      confidence: 0,
      intensity: 0,
    };
  }

  const mix = clamp(conditioning.intensity * getLayerVentMixingRatio(layer), 0, 0.94);
  return {
    value: value + (conditioning.value - value) * mix,
    confidence: conditioning.confidence,
    intensity: conditioning.intensity,
  };
}

function getVentConditioningAtPoint(point: FieldPoint, layer: RoomMapLayer, values: number[]): VentConditioning | null {
  if (!values.length) return null;

  let weightedValue = 0;
  let weightSum = 0;

  for (const item of ventLayoutItems.value) {
    const device = getDeviceForItem(item);
    if (!device || device.fan_speed === null || device.fan_speed === undefined || Number.isNaN(Number(device.fan_speed))) {
      continue;
    }

    const speed = clamp(Number(device.fan_speed), 0, 100);
    if (speed <= 0) continue;

    const contribution = getVentAirflowContribution(point, item, speed);
    if (contribution < 0.012) continue;

    const supplyValue = getVentSupplyValue(layer, values, speed);
    const speedRatio = getVentSimulationSpeedRatio(speed);
    const weight = contribution * (0.72 + speedRatio * 0.48);
    weightedValue += supplyValue * weight;
    weightSum += weight;
  }

  if (weightSum <= 0) return null;

  return {
    value: weightedValue / weightSum,
    intensity: clamp(weightSum, 0, 1),
    confidence: clamp(weightSum / (weightSum + 0.42), 0.04, 1),
  };
}

function getVentSupplyValue(layer: RoomMapLayer, values: number[], speed: number) {
  const mean = average(values);
  const minimum = Math.min(...values);
  const speedRatio = getVentSimulationSpeedRatio(speed);

  if (layer === "temperature") {
    const coolingDelta = 2.2 + speedRatio * 3.4;
    return clamp(Math.min(mean - coolingDelta, 21.2 - speedRatio * 1.8), 16, 24);
  }

  if (layer === "co2") {
    const outdoorCo2 = 420;
    const dilutionRatio = 0.04 + (1 - speedRatio) * 0.08;
    return clamp(outdoorCo2 + Math.max(0, Math.min(mean, minimum) - outdoorCo2) * dilutionRatio, 400, Math.max(430, mean));
  }

  if (layer === "humidity") {
    const comfortHumidity = 42;
    return clamp(mean + (comfortHumidity - mean) * (0.42 + speedRatio * 0.22) - speedRatio * 5.2, 28, 62);
  }

  return mean;
}

function getLayerAdvectionRatio(layer: RoomMapLayer) {
  if (layer === "temperature") return 0.68;
  if (layer === "co2") return 0.74;
  if (layer === "humidity") return 0.56;
  return 0.36;
}

function getLayerVentMixingRatio(layer: RoomMapLayer) {
  if (layer === "temperature") return 1.08;
  if (layer === "co2") return 1.14;
  if (layer === "humidity") return 0.82;
  return 0.5;
}

function getAirflowEddy(layer: RoomMapLayer, point: FieldPoint, airflow: { intensity: number }, values: number[]) {
  if (!values.length || airflow.intensity < 0.04) return 0;

  const wave = Math.sin(point.x * 3.7 + point.y * 1.9 + roomId) * Math.cos(point.x * 1.4 - point.y * 4.1);
  return wave * getLayerEddyAmplitude(layer, values) * clamp(airflow.intensity, 0, 1);
}

function getLayerEddyAmplitude(layer: RoomMapLayer, values: number[]) {
  const spread = Math.max(Math.max(...values) - Math.min(...values), getLayerMinimumContrastRange(layer));
  if (layer === "temperature") return Math.min(0.18, spread * 0.06);
  if (layer === "co2") return Math.min(28, spread * 0.05);
  if (layer === "humidity") return Math.min(1.6, spread * 0.05);
  return spread * 0.04;
}

function getVentilationFieldValue(point: FieldPoint): FieldValue | null {
  const airflow = getAirflowAtPoint(point);
  if (airflow.intensity < 0.025) return null;

  return {
    value: airflow.intensity * 100,
    confidence: airflow.intensity,
    airflow: airflow.intensity,
  };
}

function getAirflowAtPoint(point: FieldPoint) {
  let intensity = 0;
  let vectorX = 0;
  let vectorY = 0;

  for (const item of ventLayoutItems.value) {
    const device = getDeviceForItem(item);
    if (!device || device.fan_speed === null || device.fan_speed === undefined || Number.isNaN(Number(device.fan_speed))) {
      continue;
    }

    const speed = clamp(Number(device.fan_speed), 0, 100);
    if (speed <= 0) continue;

    const contribution = getVentAirflowContribution(point, item, speed);
    const direction = getVentAirflowDirection(item);
    intensity += contribution;
    vectorX += direction.x * contribution;
    vectorY += direction.y * contribution;
  }

  const magnitude = Math.hypot(vectorX, vectorY);
  return {
    intensity: clamp(intensity, 0, 1),
    x: magnitude > 0 ? vectorX / magnitude : 0,
    y: magnitude > 0 ? vectorY / magnitude : 0,
  };
}

function getVentAirflowContribution(point: FieldPoint, item: RoomLayoutItem, speed: number) {
  const speedRatio = getVentSimulationSpeedRatio(speed);
  const source = getVentOutletPoint(item);
  const direction = getVentAirflowDirection(item);
  const deltaX = point.x - source.x;
  const deltaY = point.y - source.y;
  const forward = deltaX * direction.x + deltaY * direction.y;
  const lateral = Math.abs(deltaX * -direction.y + deltaY * direction.x);
  const distance = Math.hypot(deltaX, deltaY);
  const reach = getVentReach(speed);
  const localRecirculation = Math.exp(-((distance / (0.38 + speedRatio * 0.34)) ** 2)) * 0.34;
  let plume = 0;

  if (forward > -0.08) {
    const downstream = Math.max(0, forward);
    const spread = 0.34 + downstream * 0.38 + speedRatio * 0.3;
    const longitudinalDecay = Math.exp(-((downstream / reach) ** 2) * 0.82);
    const lateralDecay = Math.exp(-((lateral / spread) ** 2) * 1.08);
    plume = longitudinalDecay * lateralDecay;
  }

  return clamp((speedRatio ** 0.66) * (plume + localRecirculation) * getObstructionFactor(source, point), 0, 1);
}

function getFlowAlignment(from: FieldPoint, to: FieldPoint, airflow: { x: number; y: number; intensity: number }) {
  if (airflow.intensity <= 0) return 0;

  const vectorX = to.x - from.x;
  const vectorY = to.y - from.y;
  const length = Math.hypot(vectorX, vectorY);
  if (length <= 0) return 0;

  return (vectorX / length) * airflow.x + (vectorY / length) * airflow.y;
}

function applySpatialUncertainty(layer: RoomMapLayer, value: number, values: number[], confidence: number) {
  if (!values.length) return value;

  const background = getLayerBackgroundValue(layer, values);
  const uncertainty = 1 - clamp(confidence, 0, 1);
  return value + (background - value) * uncertainty * 0.9;
}

function getLayerBackgroundValue(layer: RoomMapLayer, values: number[]) {
  const mean = average(values);
  const minimum = Math.min(...values);

  if (layer === "temperature") {
    const excessHeat = Math.max(0, mean - 22);
    return clamp(mean - 0.35 - excessHeat * 0.34, 17, mean);
  }

  if (layer === "co2") {
    return clamp(mean - Math.max(0, mean - 500) * 0.22, 420, mean);
  }

  if (layer === "humidity") {
    return clamp(mean + (45 - mean) * 0.24, 25, 85);
  }

  return minimum;
}

function getObstructionFactor(start: FieldPoint, end: FieldPoint) {
  return layout.value.items.reduce((factor, item) => {
    const type = getItemType(item.type).value;
    if (type !== "obstacle" && type !== "equipment") return factor;
    if (!segmentPassesThroughItem(item, start, end)) return factor;
    return factor * (type === "obstacle" ? 0.45 : 0.72);
  }, 1);
}

function segmentPassesThroughItem(item: RoomLayoutItem, start: FieldPoint, end: FieldPoint) {
  const probes = 7;
  for (let index = 1; index < probes; index += 1) {
    const ratio = index / probes;
    const point = {
      x: start.x + (end.x - start.x) * ratio,
      y: start.y + (end.y - start.y) * ratio,
    };

    if (isPointInItemBounds(point, item)) return true;
  }

  return false;
}

function isPointInItemBounds(point: FieldPoint, item: RoomLayoutItem) {
  const margin = 0.04;
  return point.x >= item.x - margin
    && point.x <= item.x + item.width + margin
    && point.y >= item.y - margin
    && point.y <= item.y + item.height + margin;
}

function createMapColorContext(layer: RoomMapLayer, values: number[]): MapColorContext | null {
  if (!values.length) return null;

  let min = Math.min(...values);
  let max = Math.max(...values);
  const mean = average(values);
  let spread = max - min;
  const minimumContrastRange = getLayerMinimumContrastRange(layer);

  if (spread < minimumContrastRange) {
    const padding = (minimumContrastRange - spread) / 2;
    min -= padding;
    max += padding;
    spread = max - min;
  }

  if (layer === "temperature") {
    const padding = Math.max(0.2, spread * 0.07);
    if (min >= 24) {
      return {
        min: min - padding,
        max: max + padding,
        mean,
        spread: spread + padding * 2,
      };
    }

    return {
      min: min - padding,
      max: max + padding,
      mean,
      spread: spread + padding * 2,
    };
  }

  if (layer === "humidity") {
    const padding = Math.max(1, spread * 0.08);
    return {
      min: min - padding,
      max: max + padding,
      mean,
      spread: spread + padding * 2,
    };
  }

  if (layer === "co2") {
    const padding = Math.max(45, spread * 0.08);
    return {
      min: Math.max(380, min - padding),
      max: max + padding,
      mean,
      spread: spread + padding * 2,
    };
  }

  const padding = Math.max(5, spread * 0.08);
  return {
    min: Math.max(0, min - padding),
    max: Math.min(100, max + padding),
    mean,
    spread: spread + padding * 2,
  };
}

function getLayerMinimumContrastRange(layer: RoomMapLayer) {
  if (layer === "temperature") return 1.1;
  if (layer === "humidity") return 7;
  if (layer === "co2") return 240;
  if (layer === "device_speed") return 24;
  return 1;
}

function getMapCellColor(layer: RoomMapLayer, value: number, context: MapColorContext | null = null) {
  if (layer === "temperature") {
    const min = context?.min ?? 16;
    const max = context?.max ?? 30;
    if (context && min >= 23.5) {
      const middle = min + (max - min) * 0.52;
      return interpolateColor(value, [
        [min, [254, 243, 199]],
        [middle, [251, 191, 36]],
        [max, [248, 113, 113]],
      ]);
    }

    if (context && max <= 24.5) {
      const middle = min + (max - min) * 0.52;
      return interpolateColor(value, [
        [min, [224, 242, 254]],
        [middle, [209, 250, 229]],
        [max, [253, 230, 138]],
      ]);
    }

    const comfort = clamp(context?.mean ?? 21.5, min + 0.15, max - 0.3);
    const warm = clamp(comfort + (max - comfort) * 0.46, comfort + 0.2, max - 0.12);
    return interpolateColor(value, [
      [min, [224, 242, 254]],
      [comfort, [209, 250, 229]],
      [warm, [253, 230, 138]],
      [max, [248, 113, 113]],
    ]);
  }

  if (layer === "humidity") {
    const min = context?.min ?? 20;
    const max = context?.max ?? 90;
    const middle = clamp(context?.mean ?? 45, min + 0.2, max - 0.2);
    return interpolateColor(value, [
      [min, [254, 243, 199]],
      [middle, [209, 250, 229]],
      [middle + (max - middle) * 0.55, [191, 219, 254]],
      [max, [165, 180, 252]],
    ]);
  }

  if (layer === "co2") {
    const min = context?.min ?? 420;
    const max = context?.max ?? 2200;
    const middle = clamp(context?.mean ?? 900, min + 10, max - 10);
    return interpolateColor(value, [
      [min, [220, 252, 231]],
      [middle, [253, 230, 138]],
      [middle + (max - middle) * 0.48, [254, 202, 202]],
      [max, [221, 214, 254]],
    ]);
  }

  return interpolateColor(value, [
    [0, [240, 253, 250]],
    [40, [204, 251, 241]],
    [75, [153, 246, 228]],
    [100, [94, 234, 212]],
  ]);
}

function interpolateColor(value: number, stops: Array<[number, [number, number, number]]>) {
  const first = stops[0];
  const last = stops[stops.length - 1];
  if (value <= first[0]) return formatRgb(first[1]);
  if (value >= last[0]) return formatRgb(last[1]);

  for (let index = 1; index < stops.length; index += 1) {
    const previous = stops[index - 1];
    const current = stops[index];
    if (value > current[0]) continue;

    const ratio = clamp((value - previous[0]) / (current[0] - previous[0]), 0, 1);
    return formatRgb([
      Math.round(previous[1][0] + (current[1][0] - previous[1][0]) * ratio),
      Math.round(previous[1][1] + (current[1][1] - previous[1][1]) * ratio),
      Math.round(previous[1][2] + (current[1][2] - previous[1][2]) * ratio),
    ]);
  }

  return formatRgb(last[1]);
}

function formatRgb(color: [number, number, number]) {
  return `rgb(${color[0]}, ${color[1]}, ${color[2]})`;
}

function getMapCellOpacity(layer: RoomMapLayer, field: FieldValue) {
  if (layer === "device_speed") {
    return clamp(0.12 + field.confidence * 0.3, 0.1, 0.42);
  }

  return clamp(0.14 + field.confidence * 0.16 + getMapSeverity(layer, field.value) * 0.12 + field.airflow * 0.06, 0.14, 0.48);
}

function getVentInfluenceColor(layer: RoomMapLayer) {
  if (layer === "temperature") return "rgb(14, 165, 233)";
  if (layer === "co2") return "rgb(20, 184, 166)";
  if (layer === "humidity") return "rgb(37, 99, 235)";
  return "rgb(13, 148, 136)";
}

function getMapSeverity(layer: RoomMapLayer, value: number) {
  if (layer === "temperature") return clamp(Math.abs(value - 21.5) / 8, 0, 1);
  if (layer === "humidity") return clamp(Math.abs(value - 45) / 45, 0, 1);
  if (layer === "co2") return clamp((value - 500) / 1100, 0, 1);
  if (layer === "device_speed") return clamp(value / 100, 0, 1);
  return 0.35;
}

function getItemDisplayName(item: RoomLayoutItem) {
  const type = getItemType(item.type).value;
  if (type === "sensor") {
    const sensor = getSensorForItem(item);
    const sensorId = sensor?.id ?? getBoundEntityId(item, "sensor");
    return sensorId ? `Sensor #${sensorId}` : item.label || "Sensor";
  }

  if (type === "vent") {
    const device = getDeviceForItem(item);
    const deviceId = device?.id ?? getBoundEntityId(item, "vent");
    return deviceId ? `Vent #${deviceId}` : item.label || "Ventilation";
  }

  return item.label || getItemType(item.type).label;
}

function getItemMapLabel(item: RoomLayoutItem) {
  const type = getItemType(item.type).value;
  if (type === "sensor") {
    const sensorId = getSensorForItem(item)?.id ?? getBoundEntityId(item, "sensor");
    return sensorId ? `S#${sensorId}` : "Sensor";
  }

  if (type === "vent") {
    const deviceId = getDeviceForItem(item)?.id ?? getBoundEntityId(item, "vent");
    return deviceId ? `V#${deviceId}` : "Vent";
  }

  return item.label || getItemType(item.type).label;
}

function getItemAriaLabel(item: RoomLayoutItem) {
  const name = getItemDisplayName(item);
  const type = getItemType(item.type).label;
  const rotation = normalizeAngle(Number(item.rotation) || 0);
  const direction = isDirectionalItem(item.type) ? ", airflow follows the room-facing side" : "";
  const position = `x ${round(item.x)} ${layout.value.unit}, y ${round(item.y)} ${layout.value.unit}`;
  const telemetry = getItemTelemetrySummary(item);
  const telemetryText = telemetry ? `, ${telemetry}` : "";
  const base = `${name}, ${type}, ${position}, rotation ${rotation} degrees${direction}${telemetryText}`;
  return mode.value === "edit" ? `Edit ${base}` : base;
}

function setMode(nextMode: EditorMode) {
  if (nextMode === "edit" && isReadOnly.value) return;

  mode.value = nextMode;

  if (nextMode === "view") {
    selectedId.value = null;
    activeDrag.value = null;
    activeResize.value = null;
    activeRotate.value = null;
    activeVertexIndex.value = null;
    removePointerListeners();
  } else {
    selectedId.value = selectedId.value ?? layout.value.items[0]?.id ?? null;
  }
}

function selectLayoutItem(item: RoomLayoutItem) {
  if (mode.value !== "edit") return;
  selectedId.value = item.id;
}

function canTransformItem(item: RoomLayoutItem) {
  return mode.value === "edit" && item.id === selectedId.value;
}

function isRoomBoundItem(type: string) {
  return type === "sensor" || type === "vent";
}

function isDirectionalItem(type: string) {
  return getItemType(type).value === "vent";
}

function getItemPlacementTitle(item: RoomLayoutItem) {
  const telemetry = getItemTelemetrySummary(item);
  const suffix = telemetry ? `\n${telemetry}` : "";
  if (!hasItemPlacementError(item)) return `${getItemDisplayName(item)}${suffix}`;
  return `${getItemDisplayName(item)} must be fully inside the room contour.${suffix}`;
}

function getSortedEntities<T extends TelemetryEntity>(entities: T[]) {
  return [...entities].sort((first, second) => first.id - second.id);
}

function toPositiveInteger(value: unknown) {
  const numeric = Number(value);
  return Number.isInteger(numeric) && numeric > 0 ? numeric : null;
}

function getBoundEntityId(item: RoomLayoutItem, kind: RoomAssetKind) {
  return toPositiveInteger(kind === "sensor" ? item.sensor_id : item.device_id);
}

function itemMatchesEntityText(item: RoomLayoutItem, entity: TelemetryEntity) {
  const text = `${item.id} ${item.label || ""} ${item.serial_number || ""}`.toLowerCase();
  const serial = entity.serial_number?.toLowerCase();
  if (serial && text.includes(serial)) return true;

  const explicitIds = (text.match(/\d+/g) ?? []).map(Number);
  return explicitIds.includes(entity.id);
}

function resolveEntityForItem<T extends TelemetryEntity>(
  item: RoomLayoutItem,
  sameTypeItems: RoomLayoutItem[],
  entities: T[],
  kind: RoomAssetKind,
) {
  const sorted = getSortedEntities(entities);
  if (!sorted.length) return null;

  const boundId = getBoundEntityId(item, kind);
  if (boundId) {
    return sorted.find((entity) => entity.id === boundId) ?? null;
  }

  const textMatch = sorted.find((entity) => itemMatchesEntityText(item, entity));
  if (textMatch) return textMatch;

  const itemIndex = sameTypeItems.findIndex((candidate) => candidate.id === item.id);
  return itemIndex >= 0 ? sorted[itemIndex] ?? null : null;
}

function getSensorForItem(item: RoomLayoutItem) {
  return resolveEntityForItem(item, sensorLayoutItems.value, sensors.value, "sensor");
}

function getDeviceForItem(item: RoomLayoutItem) {
  return resolveEntityForItem(item, ventLayoutItems.value, devices.value, "vent");
}

function getBoundAssetSummary(item: RoomLayoutItem): RoomAssetSummary | null {
  const type = getItemType(item.type).value;

  if (type === "sensor") {
    const sensor = getSensorForItem(item);
    const sensorId = sensor?.id ?? getBoundEntityId(item, "sensor");
    if (!sensorId) return null;

    return {
      label: `Sensor #${sensorId}`,
      detail: sensor?.serial_number ? `Serial ${sensor.serial_number}` : "Telemetry sensor",
    };
  }

  if (type === "vent") {
    const device = getDeviceForItem(item);
    const deviceId = device?.id ?? getBoundEntityId(item, "vent");
    if (!deviceId) return null;

    return {
      label: `Vent #${deviceId}`,
      detail: device?.serial_number ? `Serial ${device.serial_number}` : "Ventilation device",
    };
  }

  return null;
}

function getSensorParameter(sensor: Sensor | null, name: string): Parameter | null {
  return sensor?.parameters?.find((parameter) => (
    parameter.name === name
    && parameter.value !== null
    && parameter.value !== undefined
    && !Number.isNaN(Number(parameter.value))
  )) ?? null;
}

function getParameterValues(name: string) {
  return sensors.value
    .flatMap((sensor) => sensor.parameters ?? [])
    .filter((parameter) => parameter.name === name && parameter.value !== null && parameter.value !== undefined)
    .map((parameter) => Number(parameter.value))
    .filter((value) => !Number.isNaN(value));
}

function average(values: number[]) {
  return values.reduce((sum, value) => sum + value, 0) / values.length;
}

function getMetricFractionDigits(key: string) {
  return key === "co2" || key === "pressure" || key === "device_speed" ? 0 : 1;
}

function formatMetricNumber(value: number, key: string) {
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: getMetricFractionDigits(key) }).format(value);
}

function formatMetricValue(value: number, unit: string | undefined, key: string) {
  const formatted = formatMetricNumber(value, key);
  if (!unit) return formatted;
  return unit === "%" || unit.startsWith("°") ? `${formatted}${unit}` : `${formatted} ${unit}`;
}

function getTemperatureTone(value: number): TelemetryTone {
  if (value < 19) return "cool";
  if (value >= 28) return "hot";
  if (value >= 24) return "warm";
  return "normal";
}

function getCo2Tone(value: number): TelemetryTone {
  if (value >= 1200) return "hot";
  if (value >= 900) return "warm";
  return "co2";
}

function getHumidityTone(value: number): TelemetryTone {
  if (value < 35) return "cool";
  if (value >= 75) return "hot";
  if (value >= 60) return "humid";
  return "normal";
}

function getMetricTone(key: string, value: number): TelemetryTone {
  if (key === "temperature") return getTemperatureTone(value);
  if (key === "co2") return getCo2Tone(value);
  if (key === "humidity") return getHumidityTone(value);
  if (key === "device_speed") return "vent";
  return "normal";
}

function createMetric(
  key: string,
  label: string,
  value: number,
  unit: string,
  icon: string,
  tone: TelemetryTone,
): LayoutMetric {
  const metricValue = formatMetricValue(value, unit, key);
  return {
    key,
    label,
    value: metricValue,
    shortValue: metricValue,
    icon,
    tone,
  };
}

function createParameterMetric(parameter: Parameter): LayoutMetric {
  return createMetric(
    parameter.name,
    PARAMETER_LABELS[parameter.name] ?? parameter.name,
    Number(parameter.value),
    parameter.unit,
    PARAMETER_ICONS[parameter.name] ?? "monitoring",
    getMetricTone(parameter.name, Number(parameter.value)),
  );
}

function createDeviceMetric(device: Device | null): LayoutMetric | null {
  if (!device || device.fan_speed === null || device.fan_speed === undefined || Number.isNaN(Number(device.fan_speed))) {
    return null;
  }

  return createMetric("device_speed", "Fan speed", Number(device.fan_speed), "%", "mode_fan", "vent");
}

function getItemPrimaryMetric(item: RoomLayoutItem): LayoutMetric | null {
  const type = getItemType(item.type).value;
  if (type === "sensor") {
    const sensor = getSensorForItem(item);
    const parameter = getSensorParameter(sensor, "temperature")
      ?? getSensorParameter(sensor, "co2")
      ?? getSensorParameter(sensor, "humidity")
      ?? sensor?.parameters?.find((candidate) => candidate.value !== null && candidate.value !== undefined);
    return parameter ? createParameterMetric(parameter) : null;
  }

  if (type === "vent") {
    return createDeviceMetric(getDeviceForItem(item));
  }

  return null;
}

function getItemSecondaryMetrics(item: RoomLayoutItem): LayoutMetric[] {
  if (getItemType(item.type).value !== "sensor") return [];

  const sensor = getSensorForItem(item);
  const primaryKey = getItemPrimaryMetric(item)?.key;
  const preferred = ["co2", "humidity", "pressure", "temperature"];
  return preferred
    .filter((name) => name !== primaryKey)
    .map((name) => getSensorParameter(sensor, name))
    .filter((parameter): parameter is Parameter => parameter !== null)
    .map(createParameterMetric);
}

function getItemHoverMetrics(item: RoomLayoutItem): LayoutMetric[] {
  return [getItemPrimaryMetric(item), ...getItemSecondaryMetrics(item)]
    .filter((metric): metric is LayoutMetric => metric !== null);
}

function getItemTelemetrySummary(item: RoomLayoutItem) {
  return getItemHoverMetrics(item).map((metric) => `${metric.label} ${metric.value}`).join(", ");
}

function hasItemTelemetry(item: RoomLayoutItem) {
  return getItemHoverMetrics(item).length > 0;
}

function getItemTelemetryStyle(item: RoomLayoutItem) {
  const metric = getItemPrimaryMetric(item);
  if (!metric) return {};

  return {
    "--layout-telemetry-color": `var(--layout-telemetry-${metric.tone})`,
  };
}

function hasItemPlacementError(item: RoomLayoutItem) {
  return isRoomBoundItem(getItemType(item.type).value) && !isItemInsideRoom(item, layout.value);
}

function getLayoutPlacementErrors(value: RoomLayout) {
  return value.items.filter((item) => isRoomBoundItem(getItemType(item.type).value) && !isItemInsideRoom(item, value));
}

function isPointOnSegment(point: RoomLayoutPoint, start: RoomLayoutPoint, end: RoomLayoutPoint) {
  const cross = (point.y - start.y) * (end.x - start.x) - (point.x - start.x) * (end.y - start.y);
  if (Math.abs(cross) > 0.000001) return false;

  const dot = (point.x - start.x) * (end.x - start.x) + (point.y - start.y) * (end.y - start.y);
  if (dot < -0.000001) return false;

  const squaredLength = (end.x - start.x) ** 2 + (end.y - start.y) ** 2;
  return dot <= squaredLength + 0.000001;
}

function isPointInsidePolygon(point: RoomLayoutPoint, polygon: RoomLayoutPoint[]) {
  if (polygon.length < 3) return false;

  let inside = false;
  for (let index = 0, previousIndex = polygon.length - 1; index < polygon.length; previousIndex = index++) {
    const current = polygon[index];
    const previous = polygon[previousIndex];

    if (isPointOnSegment(point, previous, current)) return true;

    const intersects = current.y > point.y !== previous.y > point.y
      && point.x <= ((previous.x - current.x) * (point.y - current.y)) / (previous.y - current.y) + current.x + 0.000001;

    if (intersects) inside = !inside;
  }

  return inside;
}

function getItemProbePoints(item: RoomLayoutItem): RoomLayoutPoint[] {
  const left = item.x;
  const right = item.x + item.width;
  const top = item.y;
  const bottom = item.y + item.height;
  const center = { x: (left + right) / 2, y: (top + bottom) / 2 };
  const points = [
    { x: left, y: top },
    { x: center.x, y: top },
    { x: right, y: top },
    { x: right, y: center.y },
    { x: right, y: bottom },
    { x: center.x, y: bottom },
    { x: left, y: bottom },
    { x: left, y: center.y },
    center,
  ];

  const rotation = ((Number(item.rotation) || 0) * Math.PI) / 180;
  if (Math.abs(rotation) < 0.000001) return points;

  const cos = Math.cos(rotation);
  const sin = Math.sin(rotation);
  return points.map((point) => {
    const x = point.x - center.x;
    const y = point.y - center.y;
    return {
      x: center.x + x * cos - y * sin,
      y: center.y + x * sin + y * cos,
    };
  });
}

function isItemInsideRoom(item: RoomLayoutItem, value: RoomLayout) {
  const polygon = value.geometry.points;
  if (polygon.length < 3) return false;

  return getItemProbePoints(item).every((point) => (
    point.x >= -0.000001
    && point.x <= value.width + 0.000001
    && point.y >= -0.000001
    && point.y <= value.height + 0.000001
    && isPointInsidePolygon(point, polygon)
  ));
}

function doItemsOverlap(first: RoomLayoutItem, second: RoomLayoutItem) {
  const gap = 0.08;
  return first.x < second.x + second.width + gap
    && first.x + first.width + gap > second.x
    && first.y < second.y + second.height + gap
    && first.y + first.height + gap > second.y;
}

function hasLayoutCollision(item: RoomLayoutItem, existingItems = layout.value.items) {
  return existingItems.some((candidate) => candidate.id !== item.id && doItemsOverlap(item, candidate));
}

function findOpenPlacement(item: RoomLayoutItem) {
  const normalized = normalizeItem(item);
  const type = getItemType(normalized.type).value;
  const maxX = Math.max(0, layout.value.width - normalized.width);
  const maxY = Math.max(0, layout.value.height - normalized.height);
  const stepsX = Math.max(1, Math.min(32, Math.ceil(layout.value.width * 4)));
  const stepsY = Math.max(1, Math.min(32, Math.ceil(layout.value.height * 4)));
  const phases = isRoomBoundItem(type) ? [true] : [true, false];

  for (const shouldStayInsideRoom of phases) {
    for (let yIndex = 0; yIndex <= stepsY; yIndex += 1) {
      for (let xIndex = 0; xIndex <= stepsX; xIndex += 1) {
        const candidate = {
          ...normalized,
          x: round((maxX * xIndex) / stepsX),
          y: round((maxY * yIndex) / stepsY),
        };

        if (shouldStayInsideRoom && !isItemInsideRoom(candidate, layout.value)) continue;
        if (!hasLayoutCollision(candidate)) return candidate;
      }
    }
  }

  return keepRoomBoundItemInsideRoom(normalized);
}

function findNearestValidPlacement(item: RoomLayoutItem, value: RoomLayout) {
  if (!isRoomBoundItem(getItemType(item.type).value) || isItemInsideRoom(item, value)) return item;

  const maxX = Math.max(0, value.width - item.width);
  const maxY = Math.max(0, value.height - item.height);
  const stepsX = Math.max(1, Math.min(64, Math.ceil(value.width * 10)));
  const stepsY = Math.max(1, Math.min(64, Math.ceil(value.height * 10)));
  let best: RoomLayoutItem | null = null;
  let bestScore = Number.POSITIVE_INFINITY;

  for (let xIndex = 0; xIndex <= stepsX; xIndex += 1) {
    const x = round((maxX * xIndex) / stepsX);
    for (let yIndex = 0; yIndex <= stepsY; yIndex += 1) {
      const y = round((maxY * yIndex) / stepsY);
      const candidate = { ...item, x, y };
      if (!isItemInsideRoom(candidate, value)) continue;

      const score = (candidate.x - item.x) ** 2 + (candidate.y - item.y) ** 2;
      if (score < bestScore) {
        best = candidate;
        bestScore = score;
      }
    }
  }

  return best ?? item;
}

function keepRoomBoundItemInsideRoom(item: RoomLayoutItem, value: RoomLayout = layout.value) {
  const normalized = normalizeItem(item, value.width, value.height);
  return findNearestValidPlacement(normalized, value);
}

function keepRoomBoundItemsInsideRoom() {
  layout.value.items = layout.value.items.map((item) => keepRoomBoundItemInsideRoom(item));
}

function createItemId(prefix: string) {
  const fallback = `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 10)}`;
  const uuid = globalThis.crypto?.randomUUID?.() ?? fallback;
  return `${prefix}-${uuid}`;
}

function createGeometry(type: RoomLayoutGeometryType, width: number, height: number, points?: RoomLayoutPoint[]): RoomLayoutGeometry {
  if (type === "custom") {
    return {
      type,
      points: normalizeGeometryPoints(points?.length ? points : presetGeometryPoints("rectangle", width, height), width, height),
    };
  }

  return {
    type,
    points: presetGeometryPoints(type, width, height),
  };
}

function presetGeometryPoints(type: RoomLayoutGeometryType, width: number, height: number): RoomLayoutPoint[] {
  if (type === "l_shape") {
    return [
      { x: 0, y: 0 },
      { x: width, y: 0 },
      { x: width, y: round(height * 0.58) },
      { x: round(width * 0.58), y: round(height * 0.58) },
      { x: round(width * 0.58), y: height },
      { x: 0, y: height },
    ];
  }

  if (type === "t_shape") {
    return [
      { x: 0, y: 0 },
      { x: width, y: 0 },
      { x: width, y: round(height * 0.34) },
      { x: round(width * 0.65), y: round(height * 0.34) },
      { x: round(width * 0.65), y: height },
      { x: round(width * 0.35), y: height },
      { x: round(width * 0.35), y: round(height * 0.34) },
      { x: 0, y: round(height * 0.34) },
    ];
  }

  return [
    { x: 0, y: 0 },
    { x: width, y: 0 },
    { x: width, y: height },
    { x: 0, y: height },
  ];
}

function normalizeGeometryPoints(points: RoomLayoutPoint[], width: number, height: number): RoomLayoutPoint[] {
  const source = points.length >= 3 ? points : presetGeometryPoints("rectangle", width, height);

  return source.map((point) => ({
    x: round(clamp(Number(point.x) || 0, 0, width)),
    y: round(clamp(Number(point.y) || 0, 0, height)),
  }));
}

function normalizeGeometry(value: RoomLayoutGeometry | undefined, width: number, height: number): RoomLayoutGeometry {
  const type = getGeometryOption(value?.type || "rectangle").value;

  if (type === "custom") {
    return createGeometry("custom", width, height, value?.points);
  }

  return createGeometry(type, width, height);
}

function normalizeLayout(value: RoomLayout): RoomLayout {
  const width = clamp(Number(value.width) || 6, 1, 1000);
  const height = clamp(Number(value.height) || 4, 1, 1000);
  const normalized = {
    width: round(width),
    height: round(height),
    unit: value.unit || "m",
    geometry: normalizeGeometry(value.geometry, width, height),
    items: (value.items || []).map((item) => normalizeItem(item, width, height)),
  };

  normalized.items = normalized.items.map((item) => keepRoomBoundItemInsideRoom(item, normalized));
  return normalized;
}

function normalizeItem(item: RoomLayoutItem, roomWidth = layout.value.width, roomHeight = layout.value.height): RoomLayoutItem {
  const type = getItemType(item.type).value;
  const itemWidth = clamp(Number(item.width) || getItemType(type).width, 0.1, roomWidth);
  const itemHeight = clamp(Number(item.height) || getItemType(type).height, 0.1, roomHeight);
  const normalized: RoomLayoutItem = {
    id: item.id || createItemId(type),
    type,
    label: item.label?.trim() || null,
    x: round(clamp(Number(item.x) || 0, 0, Math.max(0, roomWidth - itemWidth))),
    y: round(clamp(Number(item.y) || 0, 0, Math.max(0, roomHeight - itemHeight))),
    width: round(itemWidth),
    height: round(itemHeight),
    rotation: round(clamp(Number(item.rotation) || 0, -360, 360)),
  };

  if (type === "sensor") {
    normalized.sensor_id = toPositiveInteger(item.sensor_id);
    normalized.serial_number = item.serial_number?.trim() || null;
  }

  if (type === "vent") {
    normalized.device_id = toPositiveInteger(item.device_id);
    normalized.serial_number = item.serial_number?.trim() || null;
  }

  return normalized;
}

async function withTimeout<T>(promise: Promise<T>, timeoutMs: number): Promise<T> {
  let timeoutId: ReturnType<typeof setTimeout> | undefined;

  return Promise.race([
    promise.finally(() => {
      if (timeoutId) clearTimeout(timeoutId);
    }),
    new Promise<T>((_, reject) => {
      timeoutId = setTimeout(() => reject(new Error("Layout request timed out")), timeoutMs);
    }),
  ]);
}

function extractPaginatedData<T>(value: PaginatedData<T> | T[] | null | undefined) {
  if (Array.isArray(value)) return value;
  return value?.data ?? [];
}

function normalizeCurrentLayout() {
  layout.value.geometry = normalizeGeometry(layout.value.geometry, layout.value.width, layout.value.height);
  keepRoomBoundItemsInsideRoom();
}

function isRequiredAssetItem(item: RoomLayoutItem) {
  const type = getItemType(item.type).value;
  return (type === "sensor" && Boolean(getBoundEntityId(item, "sensor")))
    || (type === "vent" && Boolean(getBoundEntityId(item, "vent")));
}

function shouldReplaceGenericAssetLabel(label: string | null | undefined, kind: RoomAssetKind) {
  const text = label?.trim().toLowerCase();
  if (!text) return true;

  if (kind === "sensor") {
    return /^s#?\d+$/.test(text) || /^sensor\s*#?\d+$/.test(text);
  }

  return /^v#?\d+$/.test(text) || /^vent\s*#?\d+$/.test(text) || /^ventilation\s*#?\d+$/.test(text);
}

function bindItemToSensor(item: RoomLayoutItem, sensor: Sensor) {
  item.type = "sensor";
  item.sensor_id = sensor.id;
  item.serial_number = sensor.serial_number || null;
  delete item.device_id;

  if (shouldReplaceGenericAssetLabel(item.label, "sensor")) {
    item.label = `Sensor #${sensor.id}`;
  }

  return item;
}

function bindItemToDevice(item: RoomLayoutItem, device: Device) {
  item.type = "vent";
  item.device_id = device.id;
  item.serial_number = device.serial_number || null;
  delete item.sensor_id;

  if (shouldReplaceGenericAssetLabel(item.label, "vent")) {
    item.label = `Vent #${device.id}`;
  }

  return item;
}

function createStableAssetItemId(kind: RoomAssetKind, entityId: number) {
  const base = `${kind}-${entityId}`;
  if (!layout.value.items.some((item) => item.id === base)) return base;
  return createItemId(kind);
}

function createBoundSensorItem(sensor: Sensor): RoomLayoutItem {
  const option = getItemType("sensor");
  const index = layout.value.items.filter((item) => getItemType(item.type).value === "sensor").length + 1;
  const width = Math.min(option.width, layout.value.width);
  const height = Math.min(option.height, layout.value.height);

  return bindItemToSensor(normalizeItem({
    id: createStableAssetItemId("sensor", sensor.id),
    type: "sensor",
    label: `Sensor #${sensor.id}`,
    x: round(clamp(0.45 + index * 0.25, 0, Math.max(0, layout.value.width - width))),
    y: round(clamp(0.45 + index * 0.2, 0, Math.max(0, layout.value.height - height))),
    width,
    height,
    rotation: 0,
  }), sensor);
}

function createBoundDeviceItem(device: Device): RoomLayoutItem {
  const option = getItemType("vent");
  const index = layout.value.items.filter((item) => getItemType(item.type).value === "vent").length + 1;
  const width = Math.min(option.width, layout.value.width);
  const height = Math.min(option.height, layout.value.height);

  return bindItemToDevice(normalizeItem({
    id: createStableAssetItemId("vent", device.id),
    type: "vent",
    label: `Vent #${device.id}`,
    x: round(clamp(layout.value.width - width - 0.45 - index * 0.25, 0, Math.max(0, layout.value.width - width))),
    y: round(clamp(0.45 + index * 0.2, 0, Math.max(0, layout.value.height - height))),
    width,
    height,
    rotation: 0,
  }), device);
}

function findAssetItemForEntity<T extends TelemetryEntity>(
  kind: RoomAssetKind,
  entity: T,
  entities: T[],
  usedItemIds: Set<string>,
) {
  const entityIds = new Set(entities.map((candidate) => candidate.id));
  const candidates = layout.value.items.filter((item) => (
    getItemType(item.type).value === kind && !usedItemIds.has(item.id)
  ));

  const exact = candidates.find((item) => getBoundEntityId(item, kind) === entity.id);
  if (exact) return exact;

  const textMatch = candidates.find((item) => {
    const boundId = getBoundEntityId(item, kind);
    return (!boundId || !entityIds.has(boundId)) && itemMatchesEntityText(item, entity);
  });
  if (textMatch) return textMatch;

  const unbound = candidates.filter((item) => !getBoundEntityId(item, kind));
  const entityIndex = getSortedEntities(entities).findIndex((candidate) => candidate.id === entity.id);
  return entityIndex >= 0 ? unbound[entityIndex] ?? null : null;
}

function syncRequiredRoomAssets() {
  if (!hasLoaded.value || !hasTelemetryLoaded.value) return;

  layout.value.items = layout.value.items.map((item) => normalizeItem(item));

  const usedItemIds = new Set<string>();
  const sensorIds = new Set(sensors.value.map((sensor) => sensor.id));
  const deviceIds = new Set(devices.value.map((device) => device.id));

  for (const sensor of getSortedEntities(sensors.value)) {
    const item = findAssetItemForEntity("sensor", sensor, sensors.value, usedItemIds);

    if (item) {
      bindItemToSensor(item, sensor);
      Object.assign(item, keepRoomBoundItemInsideRoom(item));
      usedItemIds.add(item.id);
      continue;
    }

    const placed = findOpenPlacement(createBoundSensorItem(sensor));
    layout.value.items.push(placed);
    usedItemIds.add(placed.id);
  }

  for (const device of getSortedEntities(devices.value)) {
    const item = findAssetItemForEntity("vent", device, devices.value, usedItemIds);

    if (item) {
      bindItemToDevice(item, device);
      Object.assign(item, keepRoomBoundItemInsideRoom(item));
      usedItemIds.add(item.id);
      continue;
    }

    const placed = findOpenPlacement(createBoundDeviceItem(device));
    layout.value.items.push(placed);
    usedItemIds.add(placed.id);
  }

  layout.value.items = layout.value.items.filter((item) => {
    const type = getItemType(item.type).value;
    if (type === "sensor") {
      const sensorId = getBoundEntityId(item, "sensor");
      return Boolean(sensorId && sensorIds.has(sensorId) && usedItemIds.has(item.id));
    }

    if (type === "vent") {
      const deviceId = getBoundEntityId(item, "vent");
      return Boolean(deviceId && deviceIds.has(deviceId) && usedItemIds.has(item.id));
    }

    return true;
  });

  if (selectedId.value && !layout.value.items.some((item) => item.id === selectedId.value)) {
    selectedId.value = layout.value.items[0]?.id ?? null;
  }
}

function getFirstUnplacedSensor(excludeItemId?: string) {
  const placedSensorIds = new Set(layout.value.items
    .filter((item) => item.id !== excludeItemId && getItemType(item.type).value === "sensor")
    .map((item) => getBoundEntityId(item, "sensor"))
    .filter((sensorId): sensorId is number => sensorId !== null));

  return getSortedEntities(sensors.value).find((sensor) => !placedSensorIds.has(sensor.id)) ?? null;
}

function getFirstUnplacedDevice(excludeItemId?: string) {
  const placedDeviceIds = new Set(layout.value.items
    .filter((item) => item.id !== excludeItemId && getItemType(item.type).value === "vent")
    .map((item) => getBoundEntityId(item, "vent"))
    .filter((deviceId): deviceId is number => deviceId !== null));

  return getSortedEntities(devices.value).find((device) => !placedDeviceIds.has(device.id)) ?? null;
}

async function loadTelemetry(options: { silent?: boolean } = {}) {
  if (!options.silent) {
    isTelemetryLoading.value = true;
    telemetryError.value = "";
  }

  try {
    const [sensorResponse, deviceResponse] = await Promise.all([
      getRoomSensors(roomId, 0, 1000),
      getRoomDevices(roomId, 0, 1000),
    ]);

    sensors.value = extractPaginatedData(sensorResponse as PaginatedData<Sensor>);
    devices.value = extractPaginatedData(deviceResponse as PaginatedData<Device>);
    hasTelemetryLoaded.value = true;
    syncRequiredRoomAssets();
  } catch (error) {
    console.error("Failed to load layout telemetry:", error);
    telemetryError.value = "Unable to load live values for the room plan.";
  } finally {
    if (!options.silent) {
      isTelemetryLoading.value = false;
    }
  }
}

async function loadLayout() {
  isLoading.value = true;
  errorMessage.value = "";

  try {
    const result = normalizeLayout(await withTimeout(getRoomLayout(envId, roomId), layoutLoadTimeoutMs));
    layout.value = cloneLayout(result);
    savedLayout.value = cloneLayout(result);
    selectedId.value = result.items[0]?.id ?? null;
    hasLoaded.value = true;
    syncRequiredRoomAssets();
  } catch (error) {
    const fallback = createDefaultLayout();
    errorMessage.value = "Unable to load room layout. Showing an empty draft.";
    layout.value = fallback;
    savedLayout.value = cloneLayout(fallback);
    selectedId.value = null;
    hasLoaded.value = true;
    syncRequiredRoomAssets();
  } finally {
    isLoading.value = false;
  }
}

async function reloadLayout() {
  hasLoaded.value = false;
  await loadLayout();
}

async function saveLayout() {
  if (isReadOnly.value) return;

  if (!hasTelemetryLoaded.value) {
    errorMessage.value = "Room sensors and ventilation devices are still loading. Wait for live assets before saving.";
    return;
  }

  syncRequiredRoomAssets();

  const hasUnboundAsset = layout.value.items.some((item) => {
    const type = getItemType(item.type).value;
    return (type === "sensor" && !getBoundEntityId(item, "sensor"))
      || (type === "vent" && !getBoundEntityId(item, "vent"));
  });

  if (hasUnboundAsset) {
    errorMessage.value = "Room sensors and ventilation devices are still loading. Wait for live assets before saving.";
    return;
  }

  if (hasPlacementErrors.value) {
    errorMessage.value = "Sensors and ventilation must be fully inside the room contour.";
    return;
  }

  isSaving.value = true;
  errorMessage.value = "";

  try {
    const normalized = normalizeLayout(layout.value);
    await updateRoomLayout(envId, roomId, normalized);
    layout.value = cloneLayout(normalized);
    savedLayout.value = cloneLayout(normalized);
    toast.add({ severity: "success", summary: "Layout saved", detail: "Room plan was updated.", life: 2500 });
  } catch (error) {
    errorMessage.value = "Unable to save room layout.";
    toast.add({ severity: "error", summary: "Save failed", detail: "Check the layout values and try again.", life: 3500 });
  } finally {
    isSaving.value = false;
  }
}

function restoreSavedLayout() {
  layout.value = cloneLayout(savedLayout.value);
  syncRequiredRoomAssets();
  selectedId.value = layout.value.items[0]?.id ?? null;
}

function setLayoutNumber(key: "width" | "height", value: number | null) {
  layout.value[key] = round(clamp(Number(value) || (key === "width" ? 6 : 4), 1, 1000));
  normalizeCurrentLayout();
}

function setGeometryType(type: RoomLayoutGeometryType) {
  const currentPoints = layout.value.geometry.points;
  layout.value.geometry = createGeometry(type, layout.value.width, layout.value.height, currentPoints);
  keepRoomBoundItemsInsideRoom();
}

function setGeometryPoint(index: number, key: "x" | "y", value: number | null) {
  if (!canEditCustomGeometry.value) return;
  const point = layout.value.geometry.points[index];
  if (!point) return;

  point[key] = round(clamp(Number(value) || 0, 0, key === "x" ? layout.value.width : layout.value.height));
  keepRoomBoundItemsInsideRoom();
}

function addGeometryPoint() {
  if (!canEditCustomGeometry.value) return;

  const points = layout.value.geometry.points;
  const last = points.at(-1) ?? { x: 0, y: layout.value.height };
  const first = points[0] ?? { x: 0, y: 0 };

  points.push({
    x: round((last.x + first.x) / 2),
    y: round((last.y + first.y) / 2),
  });
  keepRoomBoundItemsInsideRoom();
}

function removeGeometryPoint() {
  if (!canEditCustomGeometry.value || layout.value.geometry.points.length <= 3) return;
  layout.value.geometry.points.pop();
  keepRoomBoundItemsInsideRoom();
}

function setSelectedText(value: string | undefined) {
  if (!selectedItem.value) return;
  selectedItem.value.label = value?.trim() || null;
}

function setSelectedType(value: RoomLayoutItemType) {
  if (!selectedItem.value) return;
  if (isRequiredAssetItem(selectedItem.value)) return;

  const option = getItemType(value);

  if (option.value === "sensor") {
    const sensor = getFirstUnplacedSensor(selectedItem.value.id);
    if (!sensor) {
      toast.add({ severity: "info", summary: "All sensors placed", detail: "Every room sensor is already on the plan.", life: 2500 });
      return;
    }

    selectedItem.value.width = Math.min(option.width, layout.value.width);
    selectedItem.value.height = Math.min(option.height, layout.value.height);
    bindItemToSensor(selectedItem.value, sensor);
    selectedItem.value.label = `Sensor #${sensor.id}`;
    Object.assign(selectedItem.value, keepRoomBoundItemInsideRoom(selectedItem.value));
    return;
  }

  if (option.value === "vent") {
    const device = getFirstUnplacedDevice(selectedItem.value.id);
    if (!device) {
      toast.add({ severity: "info", summary: "All ventilation placed", detail: "Every room ventilation device is already on the plan.", life: 2500 });
      return;
    }

    selectedItem.value.width = Math.min(option.width, layout.value.width);
    selectedItem.value.height = Math.min(option.height, layout.value.height);
    bindItemToDevice(selectedItem.value, device);
    selectedItem.value.label = `Vent #${device.id}`;
    Object.assign(selectedItem.value, keepRoomBoundItemInsideRoom(selectedItem.value));
    return;
  }

  selectedItem.value.type = option.value;
  delete selectedItem.value.sensor_id;
  delete selectedItem.value.device_id;
  delete selectedItem.value.serial_number;
  selectedItem.value.width = Math.min(selectedItem.value.width, layout.value.width) || option.width;
  selectedItem.value.height = Math.min(selectedItem.value.height, layout.value.height) || option.height;
  Object.assign(selectedItem.value, keepRoomBoundItemInsideRoom(selectedItem.value));
}

function setSelectedNumber(key: "x" | "y" | "width" | "height" | "rotation", value: number | null) {
  if (!selectedItem.value) return;

  const item = selectedItem.value;
  const numeric = Number(value) || 0;

  if (key === "width") {
    item.width = round(clamp(numeric, 0.1, layout.value.width));
    item.x = round(clamp(item.x, 0, Math.max(0, layout.value.width - item.width)));
    Object.assign(item, keepRoomBoundItemInsideRoom(item));
    return;
  }

  if (key === "height") {
    item.height = round(clamp(numeric, 0.1, layout.value.height));
    item.y = round(clamp(item.y, 0, Math.max(0, layout.value.height - item.height)));
    Object.assign(item, keepRoomBoundItemInsideRoom(item));
    return;
  }

  if (key === "x") {
    item.x = round(clamp(numeric, 0, Math.max(0, layout.value.width - item.width)));
    Object.assign(item, keepRoomBoundItemInsideRoom(item));
    return;
  }

  if (key === "y") {
    item.y = round(clamp(numeric, 0, Math.max(0, layout.value.height - item.height)));
    Object.assign(item, keepRoomBoundItemInsideRoom(item));
    return;
  }

  item.rotation = round(clamp(numeric, -360, 360));
  Object.assign(item, keepRoomBoundItemInsideRoom(item));
}

function applyTransformedItem(item: RoomLayoutItem, candidate: RoomLayoutItem) {
  const normalized = normalizeItem(candidate);
  const type = getItemType(normalized.type).value;
  const next = isRoomBoundItem(type) ? keepRoomBoundItemInsideRoom(normalized) : normalized;

  if (isRoomBoundItem(type) && !isItemInsideRoom(next, layout.value)) return false;

  Object.assign(item, next);
  return true;
}

function getResizedItem(item: RoomLayoutItem, point: RoomLayoutPoint, resizeState: NonNullable<typeof activeResize.value>) {
  const minimumSize = 0.1;
  const right = resizeState.startX + resizeState.startWidth;
  const bottom = resizeState.startY + resizeState.startHeight;
  let x = resizeState.startX;
  let y = resizeState.startY;
  let width = resizeState.startWidth;
  let height = resizeState.startHeight;

  if (resizeState.handle.includes("w")) {
    x = clamp(point.x, 0, right - minimumSize);
    width = right - x;
  } else {
    width = clamp(point.x - resizeState.startX, minimumSize, layout.value.width - resizeState.startX);
  }

  if (resizeState.handle.includes("n")) {
    y = clamp(point.y, 0, bottom - minimumSize);
    height = bottom - y;
  } else {
    height = clamp(point.y - resizeState.startY, minimumSize, layout.value.height - resizeState.startY);
  }

  return {
    ...item,
    x: round(x),
    y: round(y),
    width: round(width),
    height: round(height),
  };
}

function getRotatedItem(item: RoomLayoutItem, point: RoomLayoutPoint, rotateState: NonNullable<typeof activeRotate.value>, shouldSnap: boolean) {
  const angle = getAngle(rotateState.centerX, rotateState.centerY, point);
  let rotation = rotateState.startRotation + angle - rotateState.startAngle;
  if (shouldSnap) rotation = Math.round(rotation / 15) * 15;

  return {
    ...item,
    rotation: normalizeAngle(rotation),
  };
}

function addLayoutItem(type: RoomLayoutItemType) {
  if (mode.value !== "edit") return;

  const option = getItemType(type);

  if (option.value === "sensor") {
    const sensor = getFirstUnplacedSensor();
    if (!sensor) {
      toast.add({ severity: "info", summary: "All sensors placed", detail: "Every room sensor is already on the plan.", life: 2500 });
      return;
    }

    const item = findOpenPlacement(createBoundSensorItem(sensor));
    layout.value.items.push(item);
    selectedId.value = item.id;
    return;
  }

  if (option.value === "vent") {
    const device = getFirstUnplacedDevice();
    if (!device) {
      toast.add({ severity: "info", summary: "All ventilation placed", detail: "Every room ventilation device is already on the plan.", life: 2500 });
      return;
    }

    const item = findOpenPlacement(createBoundDeviceItem(device));
    layout.value.items.push(item);
    selectedId.value = item.id;
    return;
  }

  const index = layout.value.items.filter((item) => item.type === type).length + 1;
  const width = Math.min(option.width, layout.value.width);
  const height = Math.min(option.height, layout.value.height);
  const item: RoomLayoutItem = {
    id: createItemId(type),
    type,
    label: `${option.label} ${index}`,
    x: round(clamp(0.5 + index * 0.28, 0, Math.max(0, layout.value.width - width))),
    y: round(clamp(0.5 + index * 0.22, 0, Math.max(0, layout.value.height - height))),
    width,
    height,
    rotation: 0,
  };

  layout.value.items.push(findOpenPlacement(item));
  selectedId.value = item.id;
}

function duplicateSelected() {
  if (!selectedItem.value || mode.value !== "edit") return;
  if (isRequiredAssetItem(selectedItem.value)) return;

  const source = selectedItem.value;
  const item = normalizeItem({
    ...source,
    id: createItemId(source.type),
    label: source.label ? `${source.label} copy` : `${getItemType(source.type).label} copy`,
    x: source.x + 0.25,
    y: source.y + 0.25,
  });

  layout.value.items.push(findOpenPlacement(item));
  selectedId.value = item.id;
}

function removeSelected() {
  if (!selectedId.value || mode.value !== "edit") return;
  if (selectedItem.value && isRequiredAssetItem(selectedItem.value)) return;

  layout.value.items = layout.value.items.filter((item) => item.id !== selectedId.value);
  selectedId.value = layout.value.items[0]?.id ?? null;
}

function clearItems() {
  layout.value.items = layout.value.items.filter((item) => isRequiredAssetItem(item));
  syncRequiredRoomAssets();
  selectedId.value = layout.value.items[0]?.id ?? null;
}

function clearSelection(event: PointerEvent) {
  if (mode.value !== "edit") return;
  if (event.target !== event.currentTarget) return;
  selectedId.value = null;
}

function getItemStyle(item: RoomLayoutItem) {
  return {
    left: `${(item.x / layout.value.width) * 100}%`,
    top: `${(item.y / layout.value.height) * 100}%`,
    width: `${(item.width / layout.value.width) * 100}%`,
    height: `${(item.height / layout.value.height) * 100}%`,
    transform: `rotate(${item.rotation}deg)`,
    "--layout-item-rotation": `${item.rotation}deg`,
  };
}

function boardPointToUnits(event: PointerEvent) {
  const board = boardRef.value;
  if (!board) return { x: 0, y: 0 };

  const rect = board.getBoundingClientRect();
  const x = ((event.clientX - rect.left) / rect.width) * layout.value.width;
  const y = ((event.clientY - rect.top) / rect.height) * layout.value.height;

  return {
    x: clamp(x, 0, layout.value.width),
    y: clamp(y, 0, layout.value.height),
  };
}

function onVertexPointerDown(event: PointerEvent, index: number) {
  if (!canEditCustomGeometry.value) return;

  activeVertexIndex.value = index;
  (event.currentTarget as SVGCircleElement).setPointerCapture?.(event.pointerId);
  window.addEventListener("pointermove", onPointerMove);
  window.addEventListener("pointerup", onPointerUp, { once: true });
}

function onItemPointerDown(event: PointerEvent, item: RoomLayoutItem) {
  if (mode.value !== "edit") return;
  selectedId.value = item.id;

  const point = boardPointToUnits(event);
  activeDrag.value = {
    id: item.id,
    offsetX: point.x - item.x,
    offsetY: point.y - item.y,
  };

  (event.currentTarget as HTMLElement).setPointerCapture?.(event.pointerId);
  window.addEventListener("pointermove", onPointerMove);
  window.addEventListener("pointerup", onPointerUp, { once: true });
}

function onResizePointerDown(event: PointerEvent, item: RoomLayoutItem, handle: ResizeHandle) {
  selectedId.value = item.id;
  if (mode.value !== "edit") return;

  activeResize.value = {
    id: item.id,
    handle,
    startX: item.x,
    startY: item.y,
    startWidth: item.width,
    startHeight: item.height,
  };

  (event.currentTarget as HTMLElement).setPointerCapture?.(event.pointerId);
  window.addEventListener("pointermove", onPointerMove);
  window.addEventListener("pointerup", onPointerUp, { once: true });
}

function onRotatePointerDown(event: PointerEvent, item: RoomLayoutItem) {
  selectedId.value = item.id;
  if (mode.value !== "edit") return;

  const centerX = item.x + item.width / 2;
  const centerY = item.y + item.height / 2;
  activeRotate.value = {
    id: item.id,
    centerX,
    centerY,
    startAngle: getAngle(centerX, centerY, boardPointToUnits(event)),
    startRotation: item.rotation,
  };

  (event.currentTarget as HTMLElement).setPointerCapture?.(event.pointerId);
  window.addEventListener("pointermove", onPointerMove);
  window.addEventListener("pointerup", onPointerUp, { once: true });
}

function onPointerMove(event: PointerEvent) {
  if (activeVertexIndex.value !== null) {
    const point = layout.value.geometry.points[activeVertexIndex.value];
    if (!point) return;

    const next = boardPointToUnits(event);
    point.x = round(next.x);
    point.y = round(next.y);
    keepRoomBoundItemsInsideRoom();
    return;
  }

  if (activeRotate.value) {
    const item = layout.value.items.find((candidate) => candidate.id === activeRotate.value?.id);
    if (!item) return;

    applyTransformedItem(item, getRotatedItem(item, boardPointToUnits(event), activeRotate.value, event.shiftKey));
    return;
  }

  if (activeResize.value) {
    const item = layout.value.items.find((candidate) => candidate.id === activeResize.value?.id);
    if (!item) return;

    applyTransformedItem(item, getResizedItem(item, boardPointToUnits(event), activeResize.value));
    return;
  }

  if (!activeDrag.value) return;
  const item = layout.value.items.find((candidate) => candidate.id === activeDrag.value?.id);
  if (!item) return;

  const point = boardPointToUnits(event);
  const candidate = {
    ...item,
    x: round(clamp(point.x - activeDrag.value.offsetX, 0, Math.max(0, layout.value.width - item.width))),
    y: round(clamp(point.y - activeDrag.value.offsetY, 0, Math.max(0, layout.value.height - item.height))),
  };

  if (isRoomBoundItem(getItemType(item.type).value) && !isItemInsideRoom(candidate, layout.value)) return;

  item.x = candidate.x;
  item.y = candidate.y;
}

function onPointerUp() {
  activeDrag.value = null;
  activeResize.value = null;
  activeRotate.value = null;
  activeVertexIndex.value = null;
  removePointerListeners();
}

function removePointerListeners() {
  window.removeEventListener("pointermove", onPointerMove);
  window.removeEventListener("pointerup", onPointerUp);
}
</script>

<style scoped>
.layout-editor {
  display: flex;
  flex: 1;
  flex-direction: column;
  gap: 10px;
  min-height: 0;
  min-width: 0;
  width: 100%;
  --layout-telemetry-cool: #0284c7;
  --layout-telemetry-normal: #059669;
  --layout-telemetry-warm: #d97706;
  --layout-telemetry-hot: #dc2626;
  --layout-telemetry-humid: #2563eb;
  --layout-telemetry-co2: #4f46e5;
  --layout-telemetry-vent: #0f766e;
}

.layout-editor__toolbar {
  align-items: center;
  background: var(--app-surface);
  border: 1px solid var(--app-border);
  border-radius: var(--app-radius);
  display: flex;
  flex: 0 0 auto;
  gap: 12px;
  justify-content: space-between;
  padding: 10px 12px;
}

.layout-editor__title {
  min-width: 0;
}

.layout-editor__eyebrow,
.layout-panel__heading span,
.layout-panel label > span {
  color: var(--app-muted);
  font-family: var(--app-mono);
  font-size: 0.68rem;
  font-weight: 700;
  letter-spacing: 0;
  line-height: 1rem;
  text-transform: uppercase;
}

.layout-editor h2 {
  color: var(--app-text-strong);
  font-size: 1rem;
  font-weight: 800;
  line-height: 1.25rem;
  margin: 0;
}

.layout-editor__actions,
.layout-editor__mode {
  align-items: center;
  display: flex;
  gap: 6px;
}

.layout-editor__mode {
  background: var(--app-surface-soft);
  border: 1px solid var(--app-border);
  border-radius: var(--app-radius);
  padding: 3px;
}

.layout-editor__mode :deep(.p-button) {
  min-height: 30px;
}

.layout-editor__body {
  align-items: stretch;
  display: grid;
  flex: 1;
  gap: 10px;
  grid-template-columns: 240px minmax(360px, 1fr) 284px;
  min-height: min(640px, calc(100vh - 170px));
  min-width: 0;
}

.layout-editor__body.layout-editor__body--view {
  grid-template-columns: minmax(0, 1fr);
  min-height: 0;
}

.layout-panel,
.layout-canvas-shell {
  background: var(--app-surface);
  border: 1px solid var(--app-border);
  border-radius: var(--app-radius);
  min-height: 0;
  min-width: 0;
}

.layout-panel {
  display: flex;
  flex-direction: column;
  gap: 1px;
  overflow: hidden auto;
}

.layout-panel__section {
  border-bottom: 1px solid var(--app-border);
  display: flex;
  flex-direction: column;
  gap: 10px;
  padding: 12px;
}

.layout-panel__section:last-child {
  border-bottom: 0;
}

.layout-panel__heading {
  align-items: center;
  display: flex;
  justify-content: space-between;
  min-width: 0;
}

.layout-panel__heading small {
  color: var(--app-muted);
  font-family: var(--app-mono);
  font-size: 0.7rem;
  text-align: right;
}

.layout-panel label {
  display: flex;
  flex-direction: column;
  gap: 5px;
  min-width: 0;
}

.layout-dimensions,
.layout-stack {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.layout-vertices {
  border: 1px solid var(--app-border);
  border-radius: 5px;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

.layout-vertices__header,
.layout-vertex-row {
  align-items: center;
  display: grid;
  gap: 6px;
  grid-template-columns: 28px minmax(0, 1fr) minmax(0, 1fr);
}

.layout-vertices__header {
  background: var(--app-surface-soft);
  border-bottom: 1px solid var(--app-border);
  color: var(--app-muted);
  font-family: var(--app-mono);
  font-size: 0.68rem;
  font-weight: 700;
  min-height: 30px;
  padding: 5px 8px;
  text-transform: uppercase;
}

.layout-vertices__rows {
  max-height: 184px;
  overflow: auto;
}

.layout-vertex-row {
  border-bottom: 1px solid var(--app-border);
  padding: 5px 6px;
}

.layout-vertex-row:last-child {
  border-bottom: 0;
}

.layout-vertex-row__index {
  align-items: center;
  background: var(--app-surface-soft);
  border: 1px solid var(--app-border);
  border-radius: 4px;
  color: var(--app-muted);
  display: inline-flex;
  font-family: var(--app-mono);
  font-size: 0.68rem;
  font-weight: 700;
  height: 28px;
  justify-content: center;
}

.layout-vertices :deep(.p-inputnumber-input) {
  font-family: var(--app-mono);
  font-size: 0.72rem;
  height: 28px;
  padding-inline: 6px;
}

.layout-geometry-actions {
  display: grid;
  gap: 6px;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  padding: 6px;
}

.layout-tool-grid {
  display: grid;
  gap: 6px;
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.layout-tool {
  align-items: center;
  background: var(--app-surface-soft);
  border: 1px solid var(--app-border);
  border-radius: 5px;
  color: var(--app-text-strong);
  display: flex;
  flex-direction: column;
  gap: 5px;
  justify-content: center;
  min-height: 68px;
  padding: 8px 4px;
}

.layout-tool:disabled {
  cursor: not-allowed;
  opacity: 0.48;
}

.layout-tool span:last-child {
  font-size: 0.72rem;
  font-weight: 680;
  line-height: 1rem;
}

.layout-canvas-shell {
  display: flex;
  flex-direction: column;
  min-height: 420px;
  overflow: hidden;
}

.layout-editor__body--view .layout-canvas-shell {
  min-height: 0;
}

.layout-loading {
  display: grid;
  flex: 1;
  min-height: 0;
  place-items: center;
}

.layout-canvas {
  display: flex;
  flex: 1;
  flex-direction: column;
  min-height: 0;
  min-width: 0;
  padding: 10px;
}

.layout-editor__body--view .layout-canvas {
  padding: 10px;
}

.layout-map-select-value,
.layout-map-select-option {
  align-items: center;
  display: flex;
  gap: 8px;
  min-width: 0;
}

.layout-map-select-value .material-symbols-outlined,
.layout-map-select-option .material-symbols-outlined {
  color: var(--app-muted);
  font-size: 1rem;
}

.layout-map-select-option strong,
.layout-map-select-option small {
  display: block;
}

.layout-map-select-option small {
  color: var(--app-muted);
  font-size: 0.72rem;
}

.layout-canvas__row {
  display: flex;
  flex: 1;
  gap: 8px;
  min-height: 0;
  min-width: 0;
}

.layout-ruler {
  color: var(--app-muted);
  font-family: var(--app-mono);
  font-size: 0.68rem;
  font-weight: 650;
  line-height: 1rem;
}

.layout-ruler--top {
  display: flex;
  justify-content: space-between;
  padding-left: 42px;
  padding-right: 6px;
}

.layout-ruler--side {
  align-items: center;
  display: flex;
  flex: 0 0 34px;
  flex-direction: column;
  justify-content: space-between;
  padding: 6px 0;
  writing-mode: vertical-rl;
}

.layout-board-wrap {
  align-items: center;
  background: var(--app-board-bg);
  border: 1px solid var(--app-border);
  border-radius: 5px;
  display: flex;
  flex: 1;
  justify-content: center;
  min-height: 0;
  min-width: 0;
  overflow: auto;
  padding: 18px;
}

.layout-editor__body--view .layout-board-wrap {
  overflow: hidden;
  padding: 16px;
}

.layout-board {
  aspect-ratio: var(--layout-ratio);
  background: var(--app-board-surface);
  border: 2px solid var(--app-board-border);
  box-shadow: inset 0 0 0 1px rgb(255 255 255 / 0.8);
  flex: 0 1 auto;
  height: 100%;
  max-height: 100%;
  max-width: 100%;
  min-height: 260px;
  min-width: 320px;
  position: relative;
  width: auto;
}

.layout-board--edit {
  cursor: crosshair;
}

.layout-board__grid {
  display: block;
  height: 100%;
  inset: 0;
  opacity: 0.85;
  pointer-events: none;
  position: absolute;
  width: 100%;
  z-index: 0;
}

.layout-board--map-visible .layout-board__grid {
  opacity: 0.26;
}

.layout-board__grid-line {
  fill: none;
  opacity: 0.48;
  shape-rendering: geometricPrecision;
  stroke: rgb(52 66 75 / 0.18);
  stroke-width: 0.006rem;
  vector-effect: non-scaling-stroke;
}

.layout-board__grid-line--major {
  opacity: 0.62;
  stroke: rgb(52 66 75 / 0.24);
  stroke-width: 0.01rem;
}

.layout-board__shape {
  display: block;
  height: 100%;
  inset: 0;
  pointer-events: none;
  position: absolute;
  width: 100%;
  z-index: 1;
}

.layout-board__shape-fill {
  fill: rgb(255 255 255 / 0.94);
  filter: drop-shadow(0 1px 0 rgb(15 23 42 / 0.18));
  stroke: none;
}

.layout-board__shape-line {
  fill: none;
  stroke: var(--app-board-line);
  stroke-linejoin: round;
  stroke-width: 2px;
  vector-effect: non-scaling-stroke;
}

.layout-board__vertex {
  fill: var(--app-surface);
  opacity: 0;
  pointer-events: none;
  stroke: var(--app-primary);
  stroke-width: 2px;
  vector-effect: non-scaling-stroke;
}

.layout-board__vertex--editable {
  cursor: grab;
  opacity: 1;
  pointer-events: auto;
}

.layout-board__map-field {
  display: block;
  height: 100%;
  inset: 0;
  filter: saturate(0.9) contrast(0.98);
  mix-blend-mode: normal;
  opacity: 0.84;
  pointer-events: none;
  position: absolute;
  width: 100%;
  z-index: 1;
}

.layout-board__map-cells {
  filter: blur(3px);
}

.layout-board__map-gradient-base,
.layout-board__map-gradient-zone {
  shape-rendering: geometricPrecision;
  stroke: none;
}

.layout-board__map-gradient-zone {
  mix-blend-mode: multiply;
}

.layout-board__map-cell {
  shape-rendering: auto;
  stroke: none;
}

.layout-board__sensor-influence,
.layout-board__vent-influence {
  mix-blend-mode: normal;
  stroke-linecap: round;
  stroke-opacity: var(--layout-influence-stroke-opacity, 0.68);
  stroke-width: 0.045rem;
  vector-effect: non-scaling-stroke;
}

.layout-board__sensor-influence {
  stroke-dasharray: 0.12 0.12;
}

.layout-board__vent-influence {
  stroke-dasharray: none;
}

.layout-board__airflow-stream {
  animation: layout-airflow-stream-drift var(--layout-airflow-duration, 1.4s) ease-in-out infinite;
  animation-delay: var(--layout-airflow-delay, 0s);
  fill: none;
  filter: drop-shadow(0 0 2px rgb(14 116 144 / 0.28));
  opacity: 0;
  stroke: rgb(8 145 178 / 0.72);
  stroke-dasharray: var(--layout-airflow-dasharray, 0.5 0.5);
  stroke-dashoffset: 0.42;
  stroke-linecap: round;
  vector-effect: non-scaling-stroke;
}

.layout-board__airflow-cues {
  display: block;
  height: 100%;
  inset: 0;
  pointer-events: none;
  position: absolute;
  width: 100%;
  z-index: 5;
}

.layout-board__airflow-cue-stem {
  animation: layout-airflow-drift 1.05s linear infinite;
  fill: none;
  stroke: rgb(8 109 119 / 0.96);
  stroke-dasharray: 0.2 0.12;
  stroke-dashoffset: 0;
  stroke-linecap: round;
  vector-effect: non-scaling-stroke;
  filter: drop-shadow(0 1px 2px rgb(15 23 42 / 0.22));
  stroke-width: 0.072rem;
}

.layout-board__airflow-cue-marker {
  fill: rgb(13 116 109 / 0.94);
}

@keyframes layout-airflow-drift {
  to {
    stroke-dashoffset: -1;
  }
}

@keyframes layout-airflow-stream-drift {
  0% {
    opacity: 0;
    stroke-dashoffset: 0.48;
  }

  18%,
  72% {
    opacity: var(--layout-airflow-opacity, 0.48);
  }

  100% {
    opacity: 0;
    stroke-dashoffset: -0.32;
  }
}

@media (prefers-reduced-motion: reduce) {
  .layout-board__airflow-stream,
  .layout-board__airflow-cue-stem {
    animation: none;
    stroke-dashoffset: 0;
  }
}

.layout-item {
  align-items: center;
  background: var(--app-item-surface);
  border: 1px solid var(--app-border-strong);
  border-radius: 4px;
  color: var(--app-text-strong);
  cursor: default;
  display: flex;
  flex-direction: column;
  gap: 2px;
  justify-content: center;
  min-height: 24px;
  min-width: 24px;
  overflow: hidden;
  padding: 3px;
  position: absolute;
  transform-origin: center;
  transition:
    border-color 140ms var(--app-ease-out),
    box-shadow 140ms var(--app-ease-out),
    opacity 140ms var(--app-ease-out);
}

.layout-item {
  z-index: 2;
}

.layout-item--editable {
  cursor: grab;
}

.layout-item--editable:active {
  cursor: grabbing;
}

.layout-item--selected {
  border-color: var(--app-primary);
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--app-primary) 20%, transparent);
  z-index: 3;
}

.layout-item--selected.layout-item--editable {
  overflow: visible;
}

.layout-item--invalid {
  border-color: var(--app-danger);
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--app-danger) 18%, transparent);
}

.layout-item--has-telemetry {
  border-color: color-mix(in srgb, var(--layout-telemetry-color, currentColor) 34%, var(--app-border-strong));
  box-shadow: 0 0 0 1px color-mix(in srgb, var(--layout-telemetry-color, currentColor) 10%, transparent);
}

.layout-item__resize-handle,
.layout-item__rotate-handle {
  align-items: center;
  background: var(--app-primary);
  border: 2px solid var(--app-on-primary);
  box-shadow: 0 3px 10px rgb(15 23 42 / 0.22);
  display: inline-flex;
  justify-content: center;
  pointer-events: auto;
  position: absolute;
  z-index: 5;
}

.layout-item__resize-handle {
  border-radius: 2px;
  height: 10px;
  width: 10px;
}

.layout-item__resize-handle--nw {
  cursor: nwse-resize;
  left: 0;
  top: 0;
  transform: translate(-50%, -50%);
}

.layout-item__resize-handle--ne {
  cursor: nesw-resize;
  right: 0;
  top: 0;
  transform: translate(50%, -50%);
}

.layout-item__resize-handle--sw {
  bottom: 0;
  cursor: nesw-resize;
  left: 0;
  transform: translate(-50%, 50%);
}

.layout-item__resize-handle--se {
  bottom: 0;
  cursor: nwse-resize;
  right: 0;
  transform: translate(50%, 50%);
}

.layout-item__rotate-arm {
  border-left: 1px solid var(--app-primary);
  height: 22px;
  left: 50%;
  pointer-events: none;
  position: absolute;
  top: -22px;
  transform: translateX(-50%);
  z-index: 4;
}

.layout-item__rotate-handle {
  border-radius: 999px;
  color: var(--app-on-primary);
  cursor: grab;
  height: 22px;
  left: 50%;
  top: -34px;
  transform: translateX(-50%);
  width: 22px;
}

.layout-item__rotate-handle:active {
  cursor: grabbing;
}

.layout-item__rotate-handle .material-symbols-outlined {
  font-size: 0.88rem;
  line-height: 1;
}

.layout-item :deep(.material-symbols-outlined) {
  font-size: 1.05rem;
}

.layout-item__rotate-handle :deep(.material-symbols-outlined) {
  font-size: 0.88rem;
}

.layout-item__content {
  align-items: center;
  display: flex;
  flex-direction: column;
  gap: 2px;
  max-width: 100%;
  min-width: 0;
  position: relative;
  transform: rotate(calc(var(--layout-item-rotation, 0deg) * -1));
  z-index: 2;
}

.layout-item__content > .material-symbols-outlined,
.layout-item__label {
  position: relative;
  z-index: 2;
}

.layout-item__label {
  font-family: var(--app-mono);
  font-size: 0.58rem;
  font-weight: 700;
  line-height: 0.75rem;
  max-width: 100%;
  overflow: hidden;
  text-overflow: ellipsis;
  text-transform: uppercase;
  white-space: nowrap;
}

.layout-item__direction {
  height: 14px;
  left: calc(100% + 3px);
  pointer-events: none;
  position: absolute;
  top: 50%;
  transform: translateY(-50%);
  width: 14px;
  z-index: 3;
}

.layout-item__direction-head {
  border-bottom: 5px solid transparent;
  border-left: 9px solid currentColor;
  border-top: 5px solid transparent;
  filter: drop-shadow(0 1px 1px rgb(15 23 42 / 0.18));
  height: 0;
  opacity: 0.78;
  position: absolute;
  right: 0;
  top: 50%;
  transform: translateY(-50%);
  width: 0;
}

.layout-item__metric-popover {
  background: rgb(255 255 255 / 0.96);
  border: 1px solid var(--app-border);
  border-radius: 6px;
  box-shadow: 0 12px 28px rgb(15 23 42 / 0.14);
  color: var(--app-text-strong);
  display: grid;
  gap: 6px;
  left: 50%;
  min-width: 172px;
  opacity: 0;
  padding: 8px;
  pointer-events: none;
  position: absolute;
  top: calc(100% + 8px);
  transform: translateX(-50%) rotate(calc(var(--layout-item-rotation, 0deg) * -1));
  transform-origin: top center;
  transition:
    opacity 120ms var(--app-ease-out),
    visibility 120ms var(--app-ease-out);
  visibility: hidden;
  z-index: 20;
}

.layout-item:hover .layout-item__metric-popover,
.layout-item:focus-visible .layout-item__metric-popover {
  opacity: 1;
  visibility: visible;
}

.layout-item__metric-title {
  color: var(--app-muted);
  font-family: var(--app-mono);
  font-size: 0.62rem;
  font-weight: 780;
  line-height: 0.82rem;
  overflow: hidden;
  text-overflow: ellipsis;
  text-transform: uppercase;
  white-space: nowrap;
}

.layout-item__metric-list {
  display: grid;
  gap: 5px;
}

.layout-item__metric-chip {
  align-items: center;
  background: var(--app-surface);
  border: 1px solid var(--app-border);
  border-left: 2px solid color-mix(in srgb, var(--layout-telemetry-color, var(--app-border-strong)) 58%, var(--app-border));
  border-radius: 5px;
  display: grid;
  gap: 1px 6px;
  grid-template-columns: auto minmax(0, 1fr);
  min-height: 30px;
  padding: 4px 6px;
}

.layout-item__metric-chip .material-symbols-outlined {
  color: var(--layout-telemetry-color, var(--app-muted));
  font-size: 0.95rem;
  grid-row: 1 / span 2;
  opacity: 0.82;
}

.layout-item__metric-chip strong {
  font-family: var(--app-mono);
  font-size: 0.72rem;
  font-weight: 820;
  line-height: 0.9rem;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.layout-item__metric-chip small {
  color: var(--app-muted);
  font-size: 0.55rem;
  font-weight: 760;
  line-height: 0.72rem;
  overflow: hidden;
  text-overflow: ellipsis;
  text-transform: uppercase;
  white-space: nowrap;
}

.layout-item--sensor {
  background: var(--app-tone-sensor-bg);
  color: var(--app-tone-sensor-text);
  overflow: visible;
}

.layout-item--vent {
  background: var(--app-tone-vent-bg);
  color: var(--app-tone-vent-text);
  overflow: visible;
}

.layout-item--access {
  background: var(--app-tone-access-bg);
  color: var(--app-tone-access-text);
}

.layout-item--furniture {
  background: var(--app-tone-furniture-bg);
  color: var(--app-tone-furniture-text);
}

.layout-item--equipment {
  background: var(--app-tone-equipment-bg);
  color: var(--app-tone-equipment-text);
}

.layout-item--zone {
  background: var(--app-tone-zone-bg);
  border-style: dashed;
  color: var(--app-tone-zone-text);
}

.layout-item--obstacle {
  background: var(--app-tone-obstacle-bg);
  color: var(--app-tone-obstacle-text);
}

.layout-bound-asset {
  background: var(--app-surface-soft);
  border: 1px solid var(--app-border);
  border-radius: 5px;
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding: 8px;
}

.layout-bound-asset span {
  color: var(--app-muted);
  font-family: var(--app-mono);
  font-size: 0.66rem;
  font-weight: 760;
  line-height: 0.9rem;
  text-transform: uppercase;
}

.layout-bound-asset strong {
  color: var(--app-text-strong);
  font-size: 0.86rem;
  line-height: 1.1rem;
}

.layout-bound-asset small {
  color: var(--app-muted);
  font-family: var(--app-mono);
  font-size: 0.68rem;
  line-height: 0.9rem;
  overflow-wrap: anywhere;
}

.layout-inspector-grid {
  display: grid;
  gap: 8px;
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.layout-inspector-actions {
  display: grid;
  gap: 8px;
  grid-template-columns: repeat(2, minmax(0, 1fr));
}

.layout-empty-inspector {
  align-items: center;
  color: var(--app-muted);
  min-height: 180px;
  justify-content: center;
  text-align: center;
}

.layout-empty-inspector strong {
  color: var(--app-text-strong);
}

.layout-empty-inspector p {
  font-size: 0.82rem;
  line-height: 1.25rem;
  margin: 0;
}

.layout-legend {
  display: flex;
  flex-direction: column;
  gap: 7px;
}

.layout-view-legend {
  align-items: center;
  border-top: 1px solid var(--app-border);
  display: flex;
  flex: 0 0 auto;
  flex-wrap: wrap;
  gap: 7px 12px;
  padding: 10px 12px 0;
}

.layout-view-legend span {
  align-items: center;
  color: var(--app-text-strong);
  display: inline-flex;
  font-size: 0.78rem;
  gap: 7px;
}

.layout-view-legend__metric {
  color: var(--app-muted) !important;
  font-family: var(--app-mono);
  font-size: 0.68rem !important;
  font-weight: 700;
  text-transform: uppercase;
}

.layout-telemetry-panel {
  border-top: 1px solid var(--app-border);
  display: flex;
  flex: 0 0 auto;
  flex-direction: column;
  gap: 10px;
  padding: 12px;
}

.layout-telemetry-panel__header {
  align-items: center;
  display: flex;
  gap: 12px;
  justify-content: space-between;
  min-width: 0;
}

.layout-telemetry-panel__title {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}

.layout-telemetry-panel__title span,
.layout-map-control > span {
  color: var(--app-muted);
  font-family: var(--app-mono);
  font-size: 0.68rem;
  font-weight: 760;
  line-height: 1rem;
  text-transform: uppercase;
}

.layout-telemetry-panel__title small,
.layout-telemetry-panel__empty {
  color: var(--app-muted);
  font-size: 0.78rem;
}

.layout-map-control {
  align-items: center;
  display: flex;
  flex: 0 0 auto;
  gap: 8px;
}

.layout-map-control__select {
  min-width: 168px;
}

.layout-telemetry-summary {
  align-items: stretch;
  background: var(--app-surface);
  border: 1px solid var(--app-border);
  border-radius: 6px;
  display: flex;
  flex-wrap: wrap;
  gap: 7px;
  padding: 8px;
}

.layout-telemetry-card {
  align-items: center;
  background: var(--app-surface-soft);
  border: 1px solid var(--app-border);
  border-left: 2px solid color-mix(in srgb, var(--layout-telemetry-color, var(--app-border-strong)) 58%, var(--app-border));
  border-radius: 5px;
  color: var(--app-text-strong);
  display: grid;
  gap: 1px 7px;
  grid-template-columns: auto auto;
  min-height: 38px;
  padding: 5px 8px;
}

.layout-telemetry-card .material-symbols-outlined {
  color: var(--layout-telemetry-color, var(--app-muted));
  font-size: 1rem;
  grid-row: 1 / span 2;
  opacity: 0.82;
}

.layout-telemetry-card strong {
  font-family: var(--app-mono);
  font-weight: 820;
}

.layout-telemetry-card strong {
  font-size: 0.78rem;
  line-height: 0.95rem;
}

.layout-telemetry-card small {
  color: var(--app-muted);
  font-weight: 720;
  text-transform: uppercase;
}

.layout-telemetry-card small {
  font-size: 0.6rem;
  line-height: 0.72rem;
}

.layout-telemetry-card--cool,
.layout-item__metric-chip--cool { --layout-telemetry-color: var(--layout-telemetry-cool); }
.layout-telemetry-card--normal,
.layout-item__metric-chip--normal { --layout-telemetry-color: var(--layout-telemetry-normal); }
.layout-telemetry-card--warm,
.layout-item__metric-chip--warm { --layout-telemetry-color: var(--layout-telemetry-warm); }
.layout-telemetry-card--hot,
.layout-item__metric-chip--hot { --layout-telemetry-color: var(--layout-telemetry-hot); }
.layout-telemetry-card--humid,
.layout-item__metric-chip--humid { --layout-telemetry-color: var(--layout-telemetry-humid); }
.layout-telemetry-card--co2,
.layout-item__metric-chip--co2 { --layout-telemetry-color: var(--layout-telemetry-co2); }
.layout-telemetry-card--vent,
.layout-item__metric-chip--vent { --layout-telemetry-color: var(--layout-telemetry-vent); }

.layout-legend span {
  align-items: center;
  color: var(--app-text-strong);
  display: flex;
  font-size: 0.8rem;
  gap: 8px;
}

.layout-legend__dot {
  border: 1px solid var(--app-border-strong);
  border-radius: 2px;
  height: 10px;
  width: 10px;
}

.layout-legend__dot--sensor { background: var(--app-tone-sensor-bg); }
.layout-legend__dot--vent { background: var(--app-tone-vent-bg); }
.layout-legend__dot--access { background: var(--app-tone-access-bg); }
.layout-legend__dot--furniture { background: var(--app-tone-furniture-bg); }
.layout-legend__dot--equipment { background: var(--app-tone-equipment-bg); }
.layout-legend__dot--zone { background: var(--app-tone-zone-bg); }
.layout-legend__dot--obstacle { background: var(--app-tone-obstacle-bg); }

.layout-select-value,
.layout-select-option {
  align-items: center;
  display: flex;
  gap: 8px;
  min-width: 0;
}

.layout-select-option strong,
.layout-select-option small {
  display: block;
}

.layout-select-option small {
  color: var(--app-muted);
  font-size: 0.75rem;
}

@media (max-width: 1180px) {
  .layout-editor__body {
    grid-template-columns: 220px minmax(340px, 1fr);
  }

  .layout-panel--inspector {
    grid-column: 1 / -1;
    min-height: 240px;
  }
}

@media (max-width: 860px) {
  .layout-editor__toolbar,
  .layout-editor__actions {
    align-items: stretch;
    flex-direction: column;
  }

  .layout-editor__actions,
  .layout-editor__mode {
    width: 100%;
  }

  .layout-editor__actions :deep(.p-button),
  .layout-editor__mode :deep(.p-button) {
    flex: 1 1 0;
    justify-content: center;
  }

  .layout-telemetry-panel__header,
  .layout-map-control {
    align-items: stretch;
    flex-direction: column;
  }

  .layout-map-control {
    gap: 4px;
  }

  .layout-map-control__select {
    min-width: 0;
    width: 100%;
  }

  .layout-editor__body {
    grid-template-columns: minmax(0, 1fr);
  }

  .layout-board {
    min-width: 260px;
  }
}
</style>
