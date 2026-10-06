// ============================================================
// ScripOx — Shadow DOM + iFrame Helper
// Traverses shadow roots and cross-origin iframes safely
// ============================================================

/**
 * Query a CSS selector across the regular DOM AND all shadow roots
 * (recursively). Returns the first match.
 */
export function queryDeep(selector: string, root: Document | Element | ShadowRoot = document): Element | null {
  // Try in current root
  const found = (root as Element).querySelector?.(selector) || null;
  if (found) return found;

  // Search in shadow roots
  const allElements = (root as Element).querySelectorAll?.('*') || [];
  for (const el of Array.from(allElements)) {
    if (el.shadowRoot) {
      const shadow = queryDeep(selector, el.shadowRoot);
      if (shadow) return shadow;
    }
  }
  return null;
}

/**
 * Query all matches across the regular DOM AND all shadow roots.
 */
export function queryAllDeep(selector: string, root: Document | Element | ShadowRoot = document): Element[] {
  const results: Element[] = [];

  const search = (searchRoot: Document | Element | ShadowRoot) => {
    try {
      const matches = (searchRoot as Element).querySelectorAll?.(selector) || [];
      results.push(...Array.from(matches));
    } catch { /* invalid selector in this context */ }

    const allEls = (searchRoot as Element).querySelectorAll?.('*') || [];
    for (const el of Array.from(allEls)) {
      if (el.shadowRoot) search(el.shadowRoot);
    }
  };

  search(root);
  return results;
}

/**
 * Get all shadow roots in the document (recursively).
 */
export function getAllShadowRoots(root: Document | Element = document): ShadowRoot[] {
  const roots: ShadowRoot[] = [];
  const allEls = root.querySelectorAll('*');
  for (const el of Array.from(allEls)) {
    if (el.shadowRoot) {
      roots.push(el.shadowRoot);
      roots.push(...getAllShadowRoots(el.shadowRoot as unknown as Element));
    }
  }
  return roots;
}

/**
 * Attach a MutationObserver that watches for lazy-loaded elements.
 * Calls `callback` with newly added elements matching `selector`.
 */
export function watchLazyLoaded(
  selector: string,
  callback: (el: Element) => void,
  root: Element = document.body
): MutationObserver {
  const observer = new MutationObserver((mutations) => {
    for (const mutation of mutations) {
      for (const node of Array.from(mutation.addedNodes)) {
        if (!(node instanceof Element)) continue;
        if (node.matches?.(selector)) callback(node);
        node.querySelectorAll?.(selector).forEach((el) => callback(el));
      }
    }
  });
  observer.observe(root, { childList: true, subtree: true });
  return observer;
}
