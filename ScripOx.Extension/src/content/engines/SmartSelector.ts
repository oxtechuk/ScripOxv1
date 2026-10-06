// ============================================================
// ScripOx — Smart Selector (Algorithmic Repair Engine)
// When a selector breaks, finds the closest matching element
// using DOM similarity, text, attributes and CSS path scoring
// ============================================================

interface RepairCandidate {
  element: Element;
  score: number;
  reason: string;
}

// ── Similarity Helpers ────────────────────────────────────────

function classOverlap(a: Element, b: Element): number {
  const sa = new Set(Array.from(a.classList));
  const sb = new Set(Array.from(b.classList));
  const intersection = [...sa].filter((c) => sb.has(c)).length;
  const union = new Set([...sa, ...sb]).size;
  return union === 0 ? 0 : intersection / union;  // Jaccard coefficient
}

function textSimilarity(a: string, b: string): number {
  const na = a.trim().slice(0, 50).toLowerCase();
  const nb = b.trim().slice(0, 50).toLowerCase();
  if (!na || !nb) return 0;
  if (na === nb) return 1;
  let matches = 0;
  for (let i = 0; i < Math.min(na.length, nb.length); i++) {
    if (na[i] === nb[i]) matches++;
  }
  return matches / Math.max(na.length, nb.length);
}

function attrSimilarity(a: Element, b: Element): number {
  const getAttrs = (el: Element): Record<string, string> => {
    const result: Record<string, string> = {};
    for (const attr of Array.from(el.attributes)) {
      result[attr.name] = attr.value;
    }
    return result;
  };
  const aa = getAttrs(a);
  const ab = getAttrs(b);
  const keys = new Set([...Object.keys(aa), ...Object.keys(ab)]);
  let matches = 0;
  for (const k of keys) {
    if (aa[k] === ab[k]) matches++;
  }
  return keys.size === 0 ? 0 : matches / keys.size;
}

function depthDiff(a: Element, b: Element): number {
  const depth = (el: Element): number => {
    let d = 0;
    let n: Element | null = el;
    while (n.parentElement) { d++; n = n.parentElement; }
    return d;
  };
  return Math.abs(depth(a) - depth(b));
}

// ── Score a candidate against original ───────────────────────

function scoreCandidate(original: Element, candidate: Element): number {
  const tagMatch   = original.tagName === candidate.tagName ? 30 : 0;
  const classSim   = classOverlap(original, candidate) * 40;
  const textSim    = textSimilarity(
    original.textContent || '',
    candidate.textContent || ''
  ) * 20;
  const attrSim    = attrSimilarity(original, candidate) * 15;
  const depthPenalty = depthDiff(original, candidate) * 2;

  return tagMatch + classSim + textSim + attrSim - depthPenalty;
}

// ── Public API ───────────────────────────────────────────────

/**
 * Tries to repair a broken selector by finding the DOM element
 * most similar to `originalElement`.
 *
 * @param selector     - The selector that no longer works
 * @param originalElement - The previously selected element (cached snapshot)
 * @returns The best matching element and an updated selector, or null
 */
export function repairSelector(
  selector: string,
  originalElement: Element
): { element: Element; selector: string; score: number } | null {
  const tag = originalElement.tagName.toLowerCase();
  const candidates = Array.from(document.querySelectorAll(tag));

  if (!candidates.length) return null;

  const scored: RepairCandidate[] = candidates.map((el) => ({
    element: el,
    score:   scoreCandidate(originalElement, el),
    reason:  `tag:${el.tagName} classes:${Array.from(el.classList).slice(0,3).join(' ')}`,
  }));

  scored.sort((a, b) => b.score - a.score);
  const best = scored[0];

  if (best.score < 20) return null; // too dissimilar

  // Generate a fresh selector for the winner
  const { generateSelectors } = require('./SelectorEngine');
  const sel = generateSelectors(best.element);

  return {
    element:  best.element,
    selector: sel.bestSelector,
    score:    best.score,
  };
}
