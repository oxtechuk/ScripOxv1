// ============================================================
// ScripOx — Content Script Entry Point
// Mounts the sidebar as an isolated Shadow DOM
// Coordinates picker, table, card, and pagination detection
// ============================================================

import React from 'react';
import { createRoot } from 'react-dom/client';
import { Sidebar } from '../sidebar/Sidebar';
import { ElementPicker } from './picker/ElementPicker';
import { TableDetector } from './detectors/TableDetector';
import { detectCards, extractCards } from './detectors/CardDetector';
import { detectPagination } from './pagination/PaginationDetector';
import { generateSelectors } from './engines/SelectorEngine';
import tailwindStyles from '../index.css?inline';

import { useStore } from '../sidebar/store/useStore';

// ── Mount sidebar in Shadow DOM ───────────────────────────────

const HOST_ID = 'scripox-extension-host';

function mountSidebar() {
  if (document.getElementById(HOST_ID)) return; // already mounted

  const host = document.createElement('div');
  host.id    = HOST_ID;
  host.style.cssText = 'position:fixed;top:0;left:0;width:100vw;height:100vh;pointer-events:none;z-index:2147483647;';
  document.documentElement.appendChild(host);

  // Shadow DOM isolates our CSS from the page
  const shadow = host.attachShadow({ mode: 'open' });

  // Inject Tailwind-processed styles into shadow root
  const styleEl = document.createElement('style');
  styleEl.textContent = tailwindStyles;
  shadow.appendChild(styleEl);

  const container = document.createElement('div');
  container.id    = 'scripox-root';
  container.style.cssText = 'pointer-events:auto;';
  shadow.appendChild(container);

  const root = createRoot(container);
  root.render(
    <React.StrictMode>
      <Sidebar />
    </React.StrictMode>
  );
}

// ── Late-init: Wait for body to be ready ─────────────────────

function init() {
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', mountSidebar);
  } else {
    mountSidebar();
  }
}

init();

// ── Direct Store Dispatcher ──────────────────────────────────

function dispatchToStore(type: string, payload: any) {
  const store = useStore.getState();
  if (type === 'SET_SELECTION') {
    store.setSelection(null as any, payload.elementInfo, payload.selectors);
  } else if (type === 'SET_RECORDS') {
    store.setRecords(payload);
  } else if (type === 'SET_TABLE') {
    store.setTableInfo(payload);
  } else if (type === 'SET_CARDS') {
    store.setCards(payload);
  } else if (type === 'SET_PAGINATION') {
    store.setPagination(payload);
  }
}

function findRepeatingParentInfo(el: Element) {
  let current: Element = el;
  let bestAncestor: Element = el;

  const getSelector = (element: Element) => {
    if (!element || !element.tagName) return '';
    const tag = element.tagName.toLowerCase();
    if (!element.classList || !element.classList.forEach) return tag;
    
    const classes = Array.from(element.classList)
      .filter(c => typeof c === 'string' && c.trim() !== '' && !c.includes('hover') && !c.includes('active') && !c.includes('focus'))
      .map(c => CSS.escape(c))
      .join('.');
    return classes ? `${tag}.${classes}` : tag;
  };

  while (current && current.parentElement && current.parentElement !== document.body) {
    const parent = current.parentElement;
    const selector = getSelector(parent);
    try {
      const matches = document.querySelectorAll(selector);
      if (matches.length >= 3) {
        bestAncestor = parent;
      } else {
        break;
      }
    } catch {
      break;
    }
    current = parent;
  }

  const parentSelector = getSelector(bestAncestor);

  let relativeSelector = '';
  if (bestAncestor === el) {
    relativeSelector = '';
  } else {
    let path: string[] = [];
    let curr: Element | null = el;
    while (curr && curr !== bestAncestor) {
      path.unshift(getSelector(curr));
      curr = curr.parentElement;
    }
    relativeSelector = path.join(' > ');
  }

  return {
    parentSelector,
    relativeSelector: relativeSelector || getSelector(el)
  };
}

// ── Element Picker ────────────────────────────────────────────

let picker: ElementPicker | null = null;

window.addEventListener('scripox:start-pick', () => {
  picker?.disable();
  useStore.getState().setMode('picking');
  picker = new ElementPicker((el, info) => {
    const repeatInfo = findRepeatingParentInfo(el);
    const selectors = {
      bestSelector: repeatInfo.parentSelector,
      css:          repeatInfo.parentSelector,
      xpath:        generateSelectors(el).xpath,
      domPath:      generateSelectors(el).domPath,
      score:        1.0
    };

    dispatchToStore('SET_SELECTION', { elementInfo: info, selectors });

    // Set default mapping for 'name' and extract
    const newMapping = { name: repeatInfo.relativeSelector };
    useStore.getState().setMapping(newMapping);

    useStore.getState().setMode('idle');
  });
  picker.enable();
});

// ── Table Detection ───────────────────────────────────────────

let tableDetector: TableDetector | null = null;

window.addEventListener('scripox:start-table', () => {
  tableDetector?.disable();
  useStore.getState().setMode('table');
  tableDetector = new TableDetector((tableInfo) => {
    // Convert table rows to records
    const records = tableInfo.rows.map((row) => {
      const rec: Record<string, string> = {};
      tableInfo.headers.forEach((header, idx) => {
        if (tableInfo.selectedColumns.includes(idx)) {
          rec[header || `col_${idx}`] = row[idx] || '';
        }
      });
      return rec;
    });
    dispatchToStore('SET_TABLE', tableInfo);
    dispatchToStore('SET_RECORDS', records);
    useStore.getState().setMode('idle');
  });
  tableDetector.enable();
});

// ── Card Detection ────────────────────────────────────────────

window.addEventListener('scripox:detect-cards', () => {
  useStore.getState().setMode('card');
  const cards = detectCards(3);
  dispatchToStore('SET_CARDS', cards);

  if (cards.length > 0) {
    const records = extractCards(cards[0]);
    dispatchToStore('SET_RECORDS', records);
  }
  useStore.getState().setMode('idle');
});

// ── Pagination Detection ──────────────────────────────────────

window.addEventListener('scripox:detect-pagination', () => {
  const pagination = detectPagination();
  dispatchToStore('SET_PAGINATION', pagination);
});

// ── Message from Background / Popup ──────────────────────────

chrome.runtime.onMessage.addListener((msg) => {
  if (msg.type === 'TOGGLE_PICKER') {
    useStore.getState().setSidebarOpen(true);
    window.dispatchEvent(new CustomEvent('scripox:start-pick'));
  }
  if (msg.type === 'TOGGLE_SIDEBAR') {
    useStore.getState().toggleSidebar();
  }
});

export {};
