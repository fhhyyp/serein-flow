<script setup lang="ts">
type TemplateItem<T> = T extends readonly (infer Item)[] ? Item : T extends Record<string, infer Value> ? Value : T extends number ? number : never;
import { computed, nextTick, onBeforeUnmount, ref, type Component } from 'vue';
import { ChevronDown, ChevronUp, Grip, Move, PanelBottom, PanelLeft, PanelRight, PanelTop, Plus, X } from 'lucide-vue-next';
import { t } from '../../i18n';
import { dockSplitZoneAtPoint } from '../../flow/dockableSplit';
import type { DockablePanelGroupState, DockPosition, WorkspacePanelId, WorkspacePanelTab, WorkspaceSize, } from '../../flow/dockableWorkspace';
interface GroupOption {
  id: string;
  label: string;
}
interface MoveEndEvent {
  panelId: WorkspacePanelId;
  targetGroupId?: string;
  dockTarget?: DockPosition;
  clientX: number;
  clientY: number;
  moved: boolean;
  interaction: 'move' | 'resize';
}
interface DragPreviewEvent {
  dock?: DockPosition;
  targetGroupId?: string;
  clientX?: number;
  clientY?: number;
}
const props = withDefaults(defineProps<{
  group: DockablePanelGroupState;
  tabs: WorkspacePanelTab[];
  workspaceSize: WorkspaceSize;
  groupOptions?: GroupOption[];
  canvasGroup?: boolean;
  dockedOffset?: number;
  dockedSize?: number;
  layoutPriority?: 'full-row' | 'center-column';
  canvasInsets?: {
    top: number;
    right: number;
    bottom: number;
    left: number;
  };
  dropTargeted?: boolean;
  splitTargets?: boolean;
  dragIndividualTabs?: boolean;
}>(), {
  groupOptions: () => [],
  canvasGroup: false,
  dockedOffset: 0,
  layoutPriority: 'center-column',
  canvasInsets: () => ({ top: 0, right: 0, bottom: 0, left: 0 }),
  dropTargeted: false,
  splitTargets: false,
  dragIndividualTabs: false,
});
const emit = defineEmits<{
  'update:group': [
    patch: Partial<DockablePanelGroupState>
  ];
  activate: [
    groupId: string,
    panelId: WorkspacePanelId
  ];
  'move-end': [
    event: MoveEndEvent
  ];
  'drag-preview': [
    event?: DragPreviewEvent
  ];
  dock: [
    panelId: WorkspacePanelId,
    dock: DockPosition
  ];
  combine: [
    panelId: WorkspacePanelId,
    targetGroupId: string
  ];
  detach: [
    panelId: WorkspacePanelId
  ];
  close: [
    panelId: WorkspacePanelId
  ];
}>();
const root = ref<HTMLElement>();
const menuButton = ref<HTMLButtonElement>();
const menu = ref<HTMLElement>();
const menuOpen = ref(false);
const menuStyle = ref<Record<string, string>>({});
const resizing = ref(false);
const moving = ref(false);
const activeTab = computed(() => {
  const evaluatedValue: WorkspacePanelTab = props.tabs.find((tab) => tab.id === props.group.activePanelId) ?? props.tabs[0];
  return evaluatedValue;
});
const currentDock = computed(() => props.group.dock);
const canClose = computed(() => activeTab.value?.closable !== false);
const groupStyle = computed<Record<string, string | number>>(() => {
  const group = props.group;
  const minResult: number = Math.min(group.width, props.workspaceSize.width);
  const width = Math.max(240, minResult);
  const minResult1: number = Math.min(group.height, props.workspaceSize.height);
  const height = Math.max(160, minResult1);
  const effectiveHeight = group.collapsed ? 76 : height;
  const fullRowPriority = props.layoutPriority === 'full-row';
  if (group.dock === 'fill') {
    return {
      top: `${props.canvasInsets.top}px`,
      right: `${props.canvasInsets.right}px`,
      bottom: `${props.canvasInsets.bottom}px`,
      left: `${props.canvasInsets.left}px`,
      zIndex: group.zIndex,
    };
  }
  if (group.dock === 'left' || group.dock === 'right') {
    const effectiveWidth = group.collapsed ? 76 : width;
    return {
      top: fullRowPriority ? `${props.canvasInsets.top}px` : '0px',
      bottom: fullRowPriority ? `${props.canvasInsets.bottom}px` : '0px',
      left: group.dock === 'left' ? `${props.dockedOffset}px` : 'auto',
      right: group.dock === 'right' ? `${props.dockedOffset}px` : 'auto',
      width: `${props.dockedSize ?? effectiveWidth}px`,
      height: 'auto',
      zIndex: group.zIndex,
    };
  }
  if (group.dock === 'top' || group.dock === 'bottom') {
    return {
      top: group.dock === 'top' ? `${props.dockedOffset}px` : 'auto',
      right: '0px',
      bottom: group.dock === 'bottom' ? `${props.dockedOffset}px` : 'auto',
      left: '0px',
      width: 'auto',
      height: `${props.dockedSize ?? (group.collapsed ? 76 : height)}px`,
      zIndex: group.zIndex,
    };
  }
  const style: Record<string, string | number> = {
    left: `${group.x}px`,
    top: `${group.y}px`,
    width: `${width}px`,
    height: `${effectiveHeight}px`,
    zIndex: group.zIndex,
  };
  return style;
});
const dockOptions: Array<{
  value: DockPosition;
  labelKey: string;
  icon: Component;
}> = [
  { value: 'free', labelKey: 'panel.freePosition', icon: Move },
  { value: 'left', labelKey: 'panel.dockLeft', icon: PanelLeft },
  { value: 'right', labelKey: 'panel.dockRight', icon: PanelRight },
  { value: 'top', labelKey: 'panel.dockTop', icon: PanelTop },
  { value: 'bottom', labelKey: 'panel.dockBottom', icon: PanelBottom },
];
function workspaceRect(): DOMRect | undefined {
  const getBoundingClientRectResult: DOMRect | undefined = root.value?.parentElement?.getBoundingClientRect();
  return getBoundingClientRectResult;
}
function edgeDockAtPoint(clientX: number, clientY: number): DockPosition | undefined {
  const bounds = workspaceRect();
  if (!bounds) {
    return undefined;
  }
  const distances: Array<{
    dock: DockPosition;
    distance: number;
  }> = [
    { dock: 'left', distance: clientX - bounds.left },
    { dock: 'right', distance: bounds.right - clientX },
    { dock: 'top', distance: clientY - bounds.top },
    { dock: 'bottom', distance: bounds.bottom - clientY },
  ];
  const nearest = distances.sort((a, b) => a.distance - b.distance)[0];
  return nearest && nearest.distance <= 52 ? nearest.dock : undefined;
}
function panelRect(): DOMRect | undefined {
  const getBoundingClientRectResult1: DOMRect | undefined = root.value?.getBoundingClientRect();
  return getBoundingClientRectResult1;
}
function targetGroupAtPoint(clientX: number, clientY: number): string | undefined {
  if (typeof document === 'undefined') {
    return undefined;
  }
  if (props.splitTargets && props.tabs.length > 1) {
    const own = panelRect();
    if (own && clientX >= own.left && clientX <= own.right && clientY >= own.top && clientY <= own.bottom) {
      const chromeElement = root.value?.querySelector<HTMLElement>('.dock-panel__chrome');
      const chrome = chromeElement?.getBoundingClientRect();
      if (chrome && clientY <= chrome.bottom) {
        return undefined;
      }
      const evaluatedValue1: string | undefined = dockSplitZoneAtPoint(own, clientX, clientY) === 'center' ? undefined : props.group.id;
      return evaluatedValue1;
    }
  }
  const querySelectorAllResult: Iterable<HTMLElement> | ArrayLike<HTMLElement> = document.querySelectorAll<HTMLElement>('[data-dock-group]');
  const candidates = Array.from(querySelectorAllResult);
  const resolveFindResult = () => {
    const filterResult = candidates
      .filter((element) => {
      const evaluatedValue2: boolean = element.getAttribute('data-dock-group') !== props.group.id;
      return evaluatedValue2;
    });
    const sortResult = filterResult.sort((a, b) => {
      const evaluatedValue3: number = Number(b.style.zIndex || 0) - Number(a.style.zIndex || 0);
      return evaluatedValue3;
    });
    const findResult = sortResult.find((element) => {
      const bounds = element.getBoundingClientRect();
      const inside = clientX >= bounds.left && clientX <= bounds.right && clientY >= bounds.top && clientY <= bounds.bottom;
      if (!inside) {
        return false;
      }
      if (props.splitTargets || !element.classList.contains('dock-panel--canvas')) {
        return true;
      }
      const chromeElement = element.querySelector<HTMLElement>('.dock-panel__chrome');
      const chrome = chromeElement?.getBoundingClientRect();
      const booleanResult: boolean = Boolean(chrome && clientX >= chrome.left && clientX <= chrome.right
        && clientY >= chrome.top && clientY <= chrome.bottom);
      return booleanResult;
    });
    return findResult;
  };
  const targetGroup = resolveFindResult();
  const evaluatedValue4: string | undefined = targetGroup?.getAttribute('data-dock-group') ?? undefined;
  return evaluatedValue4;
}
function beginMove(event: PointerEvent, panelId = activeTab.value?.id): void {
  if (event.button !== 0 || !panelId) {
    return;
  }
  const workspace = workspaceRect();
  const panel = panelRect();
  if (!workspace || !panel) {
    return;
  }
  const individualTabDrag = props.dragIndividualTabs && props.tabs.length > 1;
  event.preventDefault();
  menuOpen.value = false;
  // A tab drag leaves its group in place until the drop target is known.
  moving.value = !individualTabDrag;
  emit('activate', props.group.id, panelId);
  const startX = panel.left - workspace.left;
  const startY = panel.top - workspace.top;
  const startPointerX = event.clientX;
  const startPointerY = event.clientY;
  let moved = false;
  const onMove = (moveEvent: PointerEvent) => {
    const deltaX = moveEvent.clientX - startPointerX;
    const deltaY = moveEvent.clientY - startPointerY;
    if (Math.abs(deltaX) > 3 || Math.abs(deltaY) > 3) {
      moved = true;
    }
    if (!moved) {
      return;
    }
    const width = panel.width;
    const height = panel.height;
    if (individualTabDrag) {
      const evaluatedValue5: DragPreviewEvent | undefined = {
        dock: edgeDockAtPoint(moveEvent.clientX, moveEvent.clientY),
        targetGroupId: targetGroupAtPoint(moveEvent.clientX, moveEvent.clientY),
        clientX: moveEvent.clientX,
        clientY: moveEvent.clientY,
      };
      emit('drag-preview', evaluatedValue5);
      return;
    }
    if (props.group.dock !== 'free') {
      emit('update:group', { dock: 'free', x: startX, y: startY, width, height });
    }
    const resolveMinResult = () => {
      const maxResult9: number = Math.max(0, startX + deltaX);
      const maxResult10: number = Math.max(0, props.workspaceSize.width - width);
      const minResult2: number | undefined = Math.min(maxResult9, maxResult10);
      return minResult2;
    };
    const resolveMinResult1 = () => {
      const maxResult11: number = Math.max(0, startY + deltaY);
      const maxResult12: number = Math.max(0, props.workspaceSize.height - height);
      const minResult3: number | undefined = Math.min(maxResult11, maxResult12);
      return minResult3;
    };
    const evaluatedValue6: Partial<DockablePanelGroupState> = {
      dock: 'free',
      x: resolveMinResult(),
      y: resolveMinResult1(),
    };
    emit('update:group', evaluatedValue6);
    const evaluatedValue7: DragPreviewEvent | undefined = {
      dock: edgeDockAtPoint(moveEvent.clientX, moveEvent.clientY),
      targetGroupId: targetGroupAtPoint(moveEvent.clientX, moveEvent.clientY),
      clientX: moveEvent.clientX,
      clientY: moveEvent.clientY,
    };
    emit('drag-preview', evaluatedValue7);
  };
  const onUp = (upEvent?: PointerEvent) => {
    moving.value = false;
    window.removeEventListener('pointermove', onMove);
    window.removeEventListener('pointerup', onUp);
    if (stopInteraction === onUp) {
      stopInteraction = undefined;
    }
    if (!upEvent) {
      return;
    }
    const evaluatedValue8: DragPreviewEvent | undefined = {
      dock: moved ? edgeDockAtPoint(upEvent.clientX, upEvent.clientY) : undefined,
      targetGroupId: moved ? targetGroupAtPoint(upEvent.clientX, upEvent.clientY) : undefined,
      clientX: upEvent.clientX,
      clientY: upEvent.clientY,
    };
    emit('drag-preview', evaluatedValue8);
    const evaluatedValue9: MoveEndEvent = {
      panelId,
      targetGroupId: moved ? targetGroupAtPoint(upEvent.clientX, upEvent.clientY) : undefined,
      dockTarget: moved ? edgeDockAtPoint(upEvent.clientX, upEvent.clientY) : undefined,
      clientX: upEvent.clientX,
      clientY: upEvent.clientY,
      moved,
      interaction: 'move',
    };
    emit('move-end', evaluatedValue9);
    emit('drag-preview');
  };
  stopInteraction?.();
  stopInteraction = onUp;
  window.addEventListener('pointermove', onMove);
  window.addEventListener('pointerup', onUp);
}
function beginResize(event: PointerEvent, direction: string): void {
  if (event.button !== 0 || props.group.dock === 'fill') {
    return;
  }
  const workspace = workspaceRect();
  const panel = panelRect();
  if (!workspace || !panel) {
    return;
  }
  event.preventDefault();
  event.stopPropagation();
  menuOpen.value = false;
  resizing.value = true;
  const startPointerX = event.clientX;
  const startPointerY = event.clientY;
  const startX = panel.left - workspace.left;
  const startY = panel.top - workspace.top;
  const startWidth = panel.width;
  const startHeight = panel.height;
  const startDock = props.group.dock;
  const edgeDocked = startDock === 'left' || startDock === 'right' || startDock === 'top' || startDock === 'bottom';
  const minimumWidth = 240;
  const minimumHeight = 160;
  if (!edgeDocked) {
    emit('update:group', { dock: 'free', x: startX, y: startY, width: startWidth, height: startHeight });
  }
  const onMove = (moveEvent: PointerEvent) => {
    const deltaX = moveEvent.clientX - startPointerX;
    const deltaY = moveEvent.clientY - startPointerY;
    const isWest = direction.includes('w');
    const isNorth = direction.includes('n');
    const isEast = direction.includes('e');
    const isSouth = direction.includes('s');
    let nextX = startX;
    let nextY = startY;
    let nextWidth = startWidth;
    let nextHeight = startHeight;
    if (edgeDocked) {
      if (startDock === 'left') {
        if (isEast) {
          nextWidth = startWidth + deltaX;
        }
        if (isWest) {
          nextWidth = startWidth - deltaX;
        }
      }
      else if (startDock === 'right') {
        if (isWest) {
          nextWidth = startWidth - deltaX;
        }
        if (isEast) {
          nextWidth = startWidth + deltaX;
        }
      }
      else if (startDock === 'top') {
        if (isSouth) {
          nextHeight = startHeight + deltaY;
        }
        if (isNorth) {
          nextHeight = startHeight - deltaY;
        }
      }
      else if (startDock === 'bottom') {
        if (isNorth) {
          nextHeight = startHeight - deltaY;
        }
        if (isSouth) {
          nextHeight = startHeight + deltaY;
        }
      }
    }
    else {
      if (isEast) {
        nextWidth = startWidth + deltaX;
      }
      if (isSouth) {
        nextHeight = startHeight + deltaY;
      }
      if (isWest) {
        nextWidth = startWidth - deltaX;
        nextX = startX + deltaX;
      }
      if (isNorth) {
        nextHeight = startHeight - deltaY;
        nextY = startY + deltaY;
      }
    }
    const maxResult4: number = Math.max(minimumWidth, nextWidth);
    nextWidth = Math.min(maxResult4, props.workspaceSize.width);
    const maxResult5: number = Math.max(minimumHeight, nextHeight);
    nextHeight = Math.min(maxResult5, props.workspaceSize.height);
    if (!edgeDocked) {
      const maxResult: number = Math.max(0, nextX);
      const maxResult1: number = Math.max(0, props.workspaceSize.width - nextWidth);
      nextX = Math.min(maxResult, maxResult1);
      const maxResult2: number = Math.max(0, nextY);
      const maxResult3: number = Math.max(0, props.workspaceSize.height - nextHeight);
      nextY = Math.min(maxResult2, maxResult3);
    }
    emit('update:group', { dock: edgeDocked ? startDock : 'free', x: nextX, y: nextY, width: nextWidth, height: nextHeight });
  };
  const onUp = (upEvent?: PointerEvent) => {
    resizing.value = false;
    window.removeEventListener('pointermove', onMove);
    window.removeEventListener('pointerup', onUp);
    if (stopInteraction === onUp) {
      stopInteraction = undefined;
    }
    if (!upEvent) {
      return;
    }
    emit('move-end', {
      panelId: activeTab.value?.id ?? 'canvas',
      dockTarget: edgeDocked ? startDock : undefined,
      clientX: upEvent.clientX,
      clientY: upEvent.clientY,
      moved: true,
      interaction: 'resize',
    });
  };
  stopInteraction?.();
  stopInteraction = onUp;
  window.addEventListener('pointermove', onMove);
  window.addEventListener('pointerup', onUp);
}
let stopInteraction: ((event?: PointerEvent) => void) | undefined;
function selectTab(panelId: WorkspacePanelId): void {
  emit('activate', props.group.id, panelId);
}
function chooseDock(dock: DockPosition): void {
  if (!activeTab.value) {
    return;
  }
  menuOpen.value = false;
  emit('dock', activeTab.value.id, dock);
}
function combineInto(groupId: string): void {
  if (!activeTab.value) {
    return;
  }
  menuOpen.value = false;
  emit('combine', activeTab.value.id, groupId);
}
function detach(): void {
  if (!activeTab.value) {
    return;
  }
  menuOpen.value = false;
  emit('detach', activeTab.value.id);
}
function closeActive(): void {
  if (!activeTab.value || !canClose.value) {
    return;
  }
  menuOpen.value = false;
  emit('close', activeTab.value.id);
}
function toggleMenu(): void {
  menuOpen.value = !menuOpen.value;
  if (menuOpen.value) {
    focusGroup();
    void nextTick(updateMenuPosition);
  }
}
function updateMenuPosition(): void {
  if (!menuOpen.value || !menuButton.value || typeof window === 'undefined') {
    return;
  }
  const anchor = menuButton.value.getBoundingClientRect();
  const evaluatedValue10: number = menu.value?.getBoundingClientRect().width ?? 208;
  const maxResult6: number = Math.max(190, evaluatedValue10);
  const menuWidth = Math.min(240, maxResult6);
  const viewportPadding = 8;
  const maxResult7: number = Math.max(viewportPadding, anchor.right - menuWidth);
  const maxResult8: number = Math.max(viewportPadding, window.innerWidth - menuWidth - viewportPadding);
  const left = Math.min(maxResult7, maxResult8);
  const menuHeight = menu.value?.getBoundingClientRect().height ?? 0;
  const opensBelow = anchor.bottom + 6 + menuHeight <= window.innerHeight - viewportPadding || anchor.top < 160;
  const top = opensBelow
    ? anchor.bottom + 6
    : Math.max(viewportPadding, anchor.top - menuHeight - 6);
  menuStyle.value = {
    left: `${left}px`,
    top: `${top}px`,
    maxHeight: `${Math.max(160, window.innerHeight - viewportPadding * 2)}px`,
  };
}
function handleViewportChange(): void {
  if (menuOpen.value) {
    updateMenuPosition();
  }
}
function toggleCollapsed(): void {
  emit('update:group', { collapsed: !props.group.collapsed });
}
function focusGroup(): void {
  emit('activate', props.group.id, activeTab.value?.id ?? 'canvas');
}
if (typeof window !== 'undefined') {
  window.addEventListener('resize', handleViewportChange);
  window.addEventListener('scroll', handleViewportChange, true);
}
onBeforeUnmount(() => {
  stopInteraction?.();
  if (typeof window !== 'undefined') {
    window.removeEventListener('resize', handleViewportChange);
    window.removeEventListener('scroll', handleViewportChange, true);
  }
});
const readSectionClass = computed(() => ({ 'dock-panel--dragging': moving.value, 'dock-panel--resizing': resizing.value, 'dock-panel--collapsed': props.group.collapsed, 'dock-panel--canvas': props.canvasGroup, 'dock-panel--docked': props.group.dock !== 'free' && props.group.dock !== 'fill', 'dock-panel--menu-open': menuOpen.value, 'dock-panel--drop-targeted': props.dropTargeted }));
const readSpanIf = computed(() => (props.group.dock !== 'free' && props.group.dock !== 'fill'));
const readTemplateText = computed(() => {
  const evaluatedValue11: string = (t(`panel.dock.${props.group.dock}`));
  return evaluatedValue11;
});
function readButtonItems() {
  return (dockOptions);
}
function readButtonClass(option: TemplateItem<ReturnType<typeof readButtonItems>>) {
  return ({ active: currentDock.value === option.value });
}
const readButtonIf = computed(() => (props.tabs.length > 1));
function readButtonItems2() {
  return (props.groupOptions);
}
const readNavIf = computed(() => (props.tabs.length > 1));
function readButtonItems3() {
  return (props.tabs);
}
function readButtonAriaSelected(tab: TemplateItem<ReturnType<typeof readButtonItems3>>) {
  return (tab.id === activeTab.value?.id);
}
function readButtonClass2(tab: TemplateItem<ReturnType<typeof readButtonItems3>>) {
  return ({ active: tab.id === activeTab.value?.id });
}
const readDivIf = computed(() => (props.group.dock !== 'fill' && !props.group.collapsed));
function readSpanItems() {
  return (['n', 'ne', 'e', 'se', 's', 'sw', 'w', 'nw']);
}
function readSpanClass(direction: TemplateItem<ReturnType<typeof readSpanItems>>) {
  return (`dock-panel__resize-handle--${direction}`);
}
</script>

<template>
  <section
    ref="root"
    class="dock-panel"
    :class="readSectionClass"
    :style="groupStyle"
    :data-dock-group="props.group.id"
    @pointerdown="focusGroup"
  >
    <header class="dock-panel__chrome">
      <div
        class="dock-panel__titlebar"
        @pointerdown="beginMove($event)"
      >
        <Grip
          class="dock-panel__grip"
          :size="14"
          aria-hidden="true"
         />
        <component
          :is="activeTab?.icon"
          :size="15"
          aria-hidden="true"
         />
        <strong>{{ activeTab?.label }}</strong>
        <span
          v-if="readSpanIf"
          class="dock-panel__dock-state"
        >{{ readTemplateText }}</span>
      </div>
      <div
        class="dock-panel__actions"
        @pointerdown.stop
      >
        <button
          v-if="props.group.collapsed"
          class="dock-panel__button"
          type="button"
          :title="t('panel.expand')"
          :aria-label="t('panel.expand')"
          @click="toggleCollapsed"
        >
          <ChevronDown :size="15" />
        </button>
        <button
          v-else
          class="dock-panel__button"
          type="button"
          :title="t('panel.collapse')"
          :aria-label="t('panel.collapse')"
          @click="toggleCollapsed"
        >
          <ChevronUp :size="15" />
        </button>
        <button
          ref="menuButton"
          class="dock-panel__button"
          type="button"
          :title="t('panel.layout')"
          :aria-label="t('panel.layout')"
          :aria-expanded="menuOpen"
          @click="toggleMenu"
        >
          <Plus :size="15" />
        </button>
        <Teleport to="body">
          <div
            v-if="menuOpen"
            ref="menu"
            class="dock-panel__menu"
            :style="menuStyle"
            role="menu"
            @pointerdown.stop
          >
            <span>{{ t('panel.layout') }}</span>
            <button
              v-for="option in readButtonItems()"
              :key="option.value"
              type="button"
              role="menuitem"
              :class="readButtonClass(option)"
              @click="chooseDock(option.value)"
            >
              <component
                :is="option.icon"
                :size="14"
               />
              {{ t(option.labelKey) }}
            </button>
            <button
              v-if="readButtonIf"
              type="button"
              role="menuitem"
              @click="detach"
            >
              <Move :size="14" />
              {{ t('panel.detach') }}
            </button>
            <template v-if="props.groupOptions.length">
              <span>{{ t('panel.combineInto') }}</span>
              <button
                v-for="option in readButtonItems2()"
                :key="option.id"
                type="button"
                role="menuitem"
                @click="combineInto(option.id)"
              >
                <Plus :size="14" />
                {{ option.label }}
              </button>
            </template>
            <button
              v-if="canClose"
              class="dock-panel__menu-danger"
              type="button"
              role="menuitem"
              @click="closeActive"
            >
              <X :size="14" />
              {{ t('panel.close') }}
            </button>
          </div>
        </Teleport>
        <button
          v-if="canClose"
          class="dock-panel__button dock-panel__button--close"
          type="button"
          :title="t('panel.close')"
          :aria-label="t('panel.close')"
          @click="closeActive"
        >
          <X :size="15" />
        </button>
      </div>
      <nav
        v-if="readNavIf"
        class="dock-panel__tabs"
        role="tablist"
        :aria-label="t('panel.tabs')"
      >
        <button
          v-for="tab in readButtonItems3()"
          :key="tab.id"
          type="button"
          role="tab"
          :aria-selected="readButtonAriaSelected(tab)"
          :class="readButtonClass2(tab)"
          @pointerdown.stop="beginMove($event, tab.id)"
          @click="selectTab(tab.id)"
        >
          <component
            :is="tab.icon"
            :size="13"
           />
          <span>{{ tab.label }}</span>
        </button>
      </nav>
    </header>
    <div
      v-if="!props.group.collapsed"
      class="dock-panel__body"
    >
      <slot :panel-id="activeTab?.id"></slot>
    </div>
    <div
      v-if="readDivIf"
      class="dock-panel__resize-grips"
      aria-hidden="true"
    >
      <span
        v-for="direction in readSpanItems()"
        :key="direction"
        class="dock-panel__resize-handle"
        :class="readSpanClass(direction)"
        @pointerdown="beginResize($event, direction)"
      ></span>
    </div>
  </section>
</template>
