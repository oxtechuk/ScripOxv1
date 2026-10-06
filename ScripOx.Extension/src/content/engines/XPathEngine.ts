// ============================================================
// ScripOx — XPath Engine
// Generates absolute, relative and optimised XPath expressions
// ============================================================

/** Returns the absolute XPath for a given element */
export function getAbsoluteXPath(el: Element): string {
  const parts: string[] = [];
  let node: Element | null = el;
  while (node && node.nodeType === Node.ELEMENT_NODE) {
    let idx = 1;
    let sibling = node.previousElementSibling;
    while (sibling) {
      if (sibling.tagName === node.tagName) idx++;
      sibling = sibling.previousElementSibling;
    }
    parts.unshift(`${node.tagName.toLowerCase()}[${idx}]`);
    node = node.parentElement;
  }
  return `/${parts.join('/')}`;
}

/** Returns a relative XPath that targets the element by its most unique attributes */
export function getRelativeXPath(el: Element): string {
  const tag = el.tagName.toLowerCase();

  // Prefer id
  if (el.id) return `//${tag}[@id='${el.id}']`;

  // Build class filter
  const classes = Array.from(el.classList)
    .filter((c) => !/^(js-|is-|has-|ng-|v-)/.test(c)) // skip JS-only classes
    .slice(0, 2);

  if (classes.length) {
    const classExpr = classes.map((c) => `contains(@class,'${c}')`).join(' and ');
    return `//${tag}[${classExpr}]`;
  }

  // Fall back to text content
  const text = el.textContent?.trim().slice(0, 30);
  if (text) return `//${tag}[contains(text(),'${text}')]`;

  return getAbsoluteXPath(el);
}

/**
 * Score the XPath: higher = better (shorter, uses id/class, etc.)
 */
export function scoreXPath(xpath: string): number {
  let score = 100;
  score -= (xpath.match(/\[/g) || []).length * 5;   // predicates cost
  score += xpath.includes('@id') ? 30 : 0;
  score += xpath.includes('@class') ? 15 : 0;
  score -= xpath.split('/').length * 2;              // depth cost
  return Math.max(0, Math.min(100, score));
}
