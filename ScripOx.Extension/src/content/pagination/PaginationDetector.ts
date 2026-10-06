// ============================================================
// ScripOx — Pagination Detector
// Detects Next Button, Infinite Scroll, and Load More patterns
// ============================================================

import type { PaginationInfo } from '../../types';

const NEXT_SELECTORS = [
  'a[rel="next"]',
  'a.next', 'a.next-page', 'a[class*="next"]',
  'button.next', 'button[class*="next"]',
  '[aria-label="Next"]', '[aria-label="Next page"]',
  'a:contains("Next")', 'a:contains("›")', 'a:contains("»")',
  '.pagination .next', '.pager .next',
  '[data-page="next"]',
];

const LOAD_MORE_SELECTORS = [
  'button[class*="load-more"]', 'button[class*="loadmore"]',
  'a[class*="load-more"]',
  '[data-action="load-more"]',
  'button:contains("Load More")',
  'button:contains("Show More")',
  'button:contains("See More")',
];

function queryWithText(selectors: string[]): Element | null {
  for (const sel of selectors) {
    // Handle :contains() pseudo-selector manually
    if (sel.includes(':contains(')) {
      const [baseSel, textRaw] = sel.split(':contains(');
      const text = textRaw.replace(/["')]/g, '').toLowerCase();
      const candidates = document.querySelectorAll(baseSel || '*');
      for (const el of Array.from(candidates)) {
        if (el.textContent?.toLowerCase().includes(text)) return el;
      }
    } else {
      try {
        const el = document.querySelector(sel);
        if (el) return el;
      } catch { /* invalid selector */ }
    }
  }
  return null;
}

function detectInfiniteScroll(): boolean {
  // Heuristic: page has no "next" button but uses a scroll-based loader
  const hasObserver =
    typeof IntersectionObserver !== 'undefined' &&
    document.querySelector('[class*="infinite"], [class*="scroll-loader"], [data-infinite]') !== null;
  const hasScrollEvent = !!document.querySelector(
    '[class*="lazy-load"], [class*="infinite-scroll"]'
  );
  return hasObserver || hasScrollEvent;
}

export function detectPagination(): PaginationInfo {
  // 1. Next button
  const next = queryWithText(NEXT_SELECTORS);
  if (next) {
    const { generateSelectors } = require('../engines/SelectorEngine');
    const sel = generateSelectors(next);
    return { type: 'next-button', selector: sel.bestSelector };
  }

  // 2. Load more button
  const loadMore = queryWithText(LOAD_MORE_SELECTORS);
  if (loadMore) {
    const { generateSelectors } = require('../engines/SelectorEngine');
    const sel = generateSelectors(loadMore);
    return { type: 'load-more', selector: sel.bestSelector };
  }

  // 3. Infinite scroll
  if (detectInfiniteScroll()) {
    return { type: 'infinite-scroll' };
  }

  return { type: 'none' };
}

/** Auto-click the next button and resolve when the page changes */
export async function clickNext(selector: string, timeout = 5000): Promise<boolean> {
  return new Promise((resolve) => {
    const btn = document.querySelector(selector) as HTMLElement | null;
    if (!btn) { resolve(false); return; }

    const url = location.href;
    btn.click();

    const interval = setInterval(() => {
      if (location.href !== url) {
        clearInterval(interval);
        resolve(true);
      }
    }, 200);

    setTimeout(() => { clearInterval(interval); resolve(false); }, timeout);
  });
}
