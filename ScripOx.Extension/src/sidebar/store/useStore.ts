// ============================================================
// ScripOx — Zustand Store
// Central state for the sidebar React app
// ============================================================

import { create } from 'zustand';
import { persist, createJSONStorage } from 'zustand/middleware';
import type {
  ExtractorMode, ElementInfo, SelectorResult,
  FieldMapping, ExtractedRecord, TableInfo,
  CardInfo, PaginationInfo, ExtractionTemplate, ApiStatus,
} from '../../types';
import { DataCleaner } from '../../content/engines/DataCleaner';

interface ScripOxState {
  // Docking & Position (Left / Right / Float)
  dockPosition: 'right' | 'left' | 'float';
  setDockPosition: (pos: 'right' | 'left' | 'float') => void;
  panelPosition: { x: number; y: number };
  setPanelPosition: (pos: { x: number; y: number }) => void;

  // Clean records action
  cleanCurrentRecords: () => void;
  // Mode
  mode: ExtractorMode;
  setMode: (mode: ExtractorMode) => void;

  // Selected element
  selectedElement: Element | null;
  elementInfo: ElementInfo | null;
  selectors: SelectorResult | null;
  setSelection: (el: Element, info: ElementInfo, sel: SelectorResult) => void;
  clearSelection: () => void;

  // Table
  tableInfo: TableInfo | null;
  setTableInfo: (info: TableInfo | null) => void;

  // Cards
  cards: CardInfo[];
  selectedCard: CardInfo | null;
  setCards: (cards: CardInfo[]) => void;
  selectCard: (card: CardInfo) => void;

  // Field Mapping
  mapping: FieldMapping;
  setMapping: (mapping: FieldMapping) => void;
  updateMappingField: (field: string, selector: string) => void;

  // Theme (Light matching Desktop / Dark)
  theme: 'light' | 'dark';
  setTheme: (theme: 'light' | 'dark') => void;
  toggleTheme: () => void;

  // Column Visibility
  hiddenColumns: string[];
  toggleColumnVisibility: (col: string) => void;
  setHiddenColumns: (cols: string[]) => void;

  // Row Selection (Index-based)
  selectedRowIndices: number[];
  setSelectedRowIndices: (indices: number[]) => void;
  toggleRowSelection: (index: number) => void;
  selectAllRows: () => void;
  clearRowSelection: () => void;
  deleteSelectedRecords: () => void;

  // Records (live preview data)
  records: ExtractedRecord[];
  setRecords: (records: ExtractedRecord[]) => void;
  updateRecord: (index: number, field: string, value: string) => void;
  removeRecord: (index: number) => void;
  clearRecords: () => void;

  // Pagination
  pagination: PaginationInfo | null;
  setPagination: (info: PaginationInfo | null) => void;

  // Templates
  templates: ExtractionTemplate[];
  saveTemplate: (name: string, urlPattern?: string) => void;
  loadTemplate: (id: string) => void;
  deleteTemplate: (id: string) => void;

  // API Status
  apiStatus: ApiStatus;
  setApiStatus: (status: ApiStatus) => void;

  // Sidebar visibility
  sidebarOpen: boolean;
  setSidebarOpen: (open: boolean) => void;
  toggleSidebar: () => void;

  // Sending state
  isSending: boolean;
  lastSendResult: { ok: boolean; message: string } | null;
  setIsSending: (sending: boolean) => void;
  setSendResult: (result: { ok: boolean; message: string } | null) => void;

  // Auto-Scroll & Deep Crawl state
  isCrawling: boolean;
  crawlProgress: { scrollCount: number; leadCount: number };
  setIsCrawling: (crawling: boolean) => void;
  setCrawlProgress: (progress: { scrollCount: number; leadCount: number }) => void;
}

const extensionStorage = {
  getItem: (name: string): Promise<string | null> => {
    return new Promise((resolve) => {
      if (typeof chrome !== 'undefined' && chrome.storage && chrome.storage.local) {
        try {
          chrome.storage.local.get(name, (res) => {
            if (typeof chrome !== 'undefined' && chrome.runtime?.lastError) {
              resolve(null);
            } else {
              resolve(res ? res[name] ?? null : null);
            }
          });
        } catch {
          resolve(null);
        }
      } else {
        try {
          resolve(localStorage.getItem(name));
        } catch {
          resolve(null);
        }
      }
    });
  },
  setItem: (name: string, value: string): Promise<void> => {
    return new Promise((resolve) => {
      if (typeof chrome !== 'undefined' && chrome.storage && chrome.storage.local) {
        try {
          chrome.storage.local.set({ [name]: value }, () => {
            resolve();
          });
        } catch {
          resolve();
        }
      } else {
        try {
          localStorage.setItem(name, value);
        } catch {
          // Ignore
        }
        resolve();
      }
    });
  },
  removeItem: (name: string): Promise<void> => {
    return new Promise((resolve) => {
      if (typeof chrome !== 'undefined' && chrome.storage && chrome.storage.local) {
        try {
          chrome.storage.local.remove(name, () => {
            resolve();
          });
        } catch {
          resolve();
        }
      } else {
        try {
          localStorage.removeItem(name);
        } catch {
          // Ignore
        }
        resolve();
      }
    });
  }
};

function extractMappedRecords(
  bestSelector: string | undefined,
  mapping: FieldMapping
): ExtractedRecord[] {
  if (!bestSelector || !mapping || Object.keys(mapping).length === 0) return [];

  let items: Element[] = [];
  try {
    // Check if parent selector is an XPath
    if (bestSelector.startsWith('/') || bestSelector.startsWith('//') || bestSelector.includes('::') || bestSelector.includes('[')) {
      const xpath = bestSelector.startsWith('.') ? bestSelector : bestSelector.startsWith('/') ? bestSelector : `//${bestSelector}`;
      const result = document.evaluate(xpath, document, null, XPathResult.ORDERED_NODE_SNAPSHOT_TYPE, null);
      for (let i = 0; i < result.snapshotLength; i++) {
        const node = result.snapshotItem(i);
        if (node && node.nodeType === Node.ELEMENT_NODE) {
          items.push(node as Element);
        }
      }
    } else {
      items = Array.from(document.querySelectorAll(bestSelector));
    }
  } catch (err) {
    console.warn('Failed to query bestSelector:', bestSelector, err);
    return [];
  }

  if (!items.length) return [];

  return items.map((item) => {
    const record: ExtractedRecord = {};
    for (const [field, selector] of Object.entries(mapping)) {
      if (!selector || !selector.trim()) continue;
      try {
        let el: Element | null = null;
        // Check if child selector is an XPath
        if (selector.startsWith('/') || selector.startsWith('//') || selector.startsWith('.') || selector.includes('::') || selector.includes('[')) {
          const xpath = selector.startsWith('.') ? selector : selector.startsWith('/') ? `.${selector}` : `.//${selector}`;
          const res = document.evaluate(xpath, item, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null);
          el = res.singleNodeValue as Element;
        } else {
          el = item.matches(selector) ? item : item.querySelector(selector);
        }

        if (el) {
          let val = '';
          if (el.tagName === 'A') {
            const href = (el as HTMLAnchorElement).href;
            if (href.startsWith('mailto:')) val = href.replace('mailto:', '');
            else if (href.startsWith('tel:')) val = href.replace('tel:', '');
            else val = href;
          } else if (el.tagName === 'IMG') {
            val = (el as HTMLImageElement).src;
          } else {
            val = el.textContent?.trim() || '';
          }
          record[field] = val;
        } else {
          record[field] = '';
        }
      } catch {
        record[field] = '';
      }
    }

    // Try to extract latitude and longitude from links inside the item (e.g. Google Maps coords)
    try {
      const links = Array.from(item.querySelectorAll('a'));
      for (const link of links) {
        const match = link.href.match(/@(-?\d+\.\d+),(-?\d+\.\d+)/);
        if (match) {
          record['latitude'] = match[1];
          record['longitude'] = match[2];
          break;
        }
      }
    } catch { /* skip */ }

    return record;
  });

  return DataCleaner.cleanAndDeduplicate(records);
}

function getActiveItemSelector(state: any): string | undefined {
  if (state.selectors?.bestSelector) return state.selectors.bestSelector;
  if (state.selectedCard?.itemSelector) return state.selectedCard.itemSelector;
  if (state.cards && state.cards[0]?.itemSelector) return state.cards[0].itemSelector;
  if (state.tableInfo?.selector) return state.tableInfo.selector;
  return undefined;
}

export const useStore = create<ScripOxState>()(
  persist(
    (set, get) => ({
      // ── Docking & Position ────────────────────────────────
      dockPosition: 'right',
      setDockPosition: (dockPosition) => set({ dockPosition }),
      panelPosition: { x: 40, y: 40 },
      setPanelPosition: (panelPosition) => set({ panelPosition }),
      cleanCurrentRecords: () => set((s) => ({
        records: DataCleaner.cleanAndDeduplicate(s.records)
      })),

      // ── Mode ─────────────────────────────────────────────
      mode: 'idle',
      setMode: (mode) => set({ mode }),

      // ── Selection ─────────────────────────────────────────
      selectedElement: null,
      elementInfo: null,
      selectors: null,
      setSelection: (el, info, sel) => set({
        selectedElement: el,
        elementInfo: info,
        selectors: sel,
        mode: 'idle',
      }),
      clearSelection: () => set({
        selectedElement: null, elementInfo: null, selectors: null,
        records: [], mapping: {},
      }),

      // ── Table ─────────────────────────────────────────────
      tableInfo: null,
      setTableInfo: (info) => set({ tableInfo: info }),

      // ── Cards ─────────────────────────────────────────────
      cards: [],
      selectedCard: null,
      setCards: (cards) => set({ cards }),
      selectCard: (card) => set({ selectedCard: card }),

      // ── Field Mapping ────────────────────────────────────
      mapping: {},
      setMapping: (mapping) => set((s) => {
        const itemSelector = getActiveItemSelector(s);
        const records = extractMappedRecords(itemSelector, mapping);
        return { mapping, records };
      }),
      updateMappingField: (field, selector) => set((s) => {
        const mapping = { ...s.mapping, [field]: selector };
        const itemSelector = getActiveItemSelector(s);
        const records = extractMappedRecords(itemSelector, mapping);
        return { mapping, records };
      }),

      // ── Theme ─────────────────────────────────────────────
      theme: 'light',
      setTheme: (theme) => set({ theme }),
      toggleTheme: () => set((s) => ({ theme: s.theme === 'light' ? 'dark' : 'light' })),

      // ── Column Visibility ─────────────────────────────────
      hiddenColumns: [],
      toggleColumnVisibility: (col) => set((s) => ({
        hiddenColumns: s.hiddenColumns.includes(col)
          ? s.hiddenColumns.filter((c) => c !== col)
          : [...s.hiddenColumns, col]
      })),
      setHiddenColumns: (hiddenColumns) => set({ hiddenColumns }),

      // ── Row Selection ─────────────────────────────────────
      selectedRowIndices: [],
      setSelectedRowIndices: (selectedRowIndices) => set({ selectedRowIndices }),
      toggleRowSelection: (index) => set((s) => ({
        selectedRowIndices: s.selectedRowIndices.includes(index)
          ? s.selectedRowIndices.filter((i) => i !== index)
          : [...s.selectedRowIndices, index]
      })),
      selectAllRows: () => set((s) => ({
        selectedRowIndices: s.records.map((_, i) => i)
      })),
      clearRowSelection: () => set({ selectedRowIndices: [] }),
      deleteSelectedRecords: () => set((s) => {
        const remaining = s.records.filter((_, i) => !s.selectedRowIndices.includes(i));
        return { records: remaining, selectedRowIndices: [] };
      }),

      // ── Records ──────────────────────────────────────────
      records: [],
      setRecords: (records) => set({ records, selectedRowIndices: [] }),
      updateRecord: (index, field, value) =>
        set((s) => {
          const records = [...s.records];
          records[index] = { ...records[index], [field]: value };
          return { records };
        }),
      removeRecord: (index) =>
        set((s) => ({
          records: s.records.filter((_, i) => i !== index),
          selectedRowIndices: s.selectedRowIndices
            .filter((i) => i !== index)
            .map((i) => (i > index ? i - 1 : i)),
        })),
      clearRecords: () => set({ records: [], selectedRowIndices: [], hiddenColumns: [] }),

      // ── Pagination ────────────────────────────────────────
      pagination: null,
      setPagination: (info) => set({ pagination: info }),

      // ── Templates ────────────────────────────────────────
      templates: [],
      saveTemplate: (name, urlPattern) => {
        const s = get();
        const tpl: ExtractionTemplate = {
          id:          crypto.randomUUID(),
          name,
          url:         location?.href,
          urlPattern,
          selectors:   s.selectors!,
          mapping:     s.mapping,
          pagination:  s.pagination ?? undefined,
          createdAt:   Date.now(),
        };
        set((st) => ({ templates: [tpl, ...st.templates] }));
      },
      loadTemplate: (id) => {
        const tpl = get().templates.find((t) => t.id === id);
        if (tpl) {
          const records = extractMappedRecords(tpl.selectors.bestSelector, tpl.mapping);
          set({ selectors: tpl.selectors, mapping: tpl.mapping, records, selectedRowIndices: [] });
        }
      },
      deleteTemplate: (id) =>
        set((s) => ({ templates: s.templates.filter((t) => t.id !== id) })),

      // ── API Status ────────────────────────────────────────
      apiStatus: { connected: false, checkedAt: 0 },
      setApiStatus: (status) => set({ apiStatus: status }),

      // ── Sidebar ───────────────────────────────────────────
      sidebarOpen: true,
      setSidebarOpen: (open) => set({ sidebarOpen: open }),
      toggleSidebar: () => set((s) => ({ sidebarOpen: !s.sidebarOpen })),

      // ── Sending ───────────────────────────────────────────
      isSending: false,
      lastSendResult: null,
      setIsSending: (sending) => set({ isSending: sending }),
      setSendResult: (result) => set({ lastSendResult: result }),

      // ── Crawling ──────────────────────────────────────────
      isCrawling: false,
      crawlProgress: { scrollCount: 0, leadCount: 0 },
      setIsCrawling: (isCrawling) => set({ isCrawling }),
      setCrawlProgress: (crawlProgress) => set({ crawlProgress }),
    }),
    {
      name: 'scripox-extractor',
      storage: createJSONStorage(() => extensionStorage as any),
      // Don't persist DOM references
      partialize: (s) => ({
        theme:        s.theme,
        templates:    s.templates,
        mapping:      s.mapping,
        apiStatus:    s.apiStatus,
        dockPosition: s.dockPosition,
        hiddenColumns: s.hiddenColumns,
      }),
    }
  )
);
