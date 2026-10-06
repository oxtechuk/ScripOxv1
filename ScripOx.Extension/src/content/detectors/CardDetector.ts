// ============================================================
// ScripOx — Card / Repeating Structure Detector
// Finds repeating elements (products, companies, cards, etc.)
// ============================================================

import type { CardInfo } from '../../types';
import { generateSelectors } from '../engines/SelectorEngine';
import { DataCleaner } from '../engines/DataCleaner';

interface GroupCandidate {
  parentSelector: string;
  itemSelector:   string;
  count:          number;
  score:          number;
  sample:         Element;
}

/** Check structural similarity between two elements (same tag, similar classes) */
function areSimilar(a: Element, b: Element): boolean {
  if (a.tagName !== b.tagName) return false;
  const ca = Array.from(a.classList);
  const cb = Array.from(b.classList);
  const shared = ca.filter((c) => cb.includes(c)).length;
  return shared >= Math.min(ca.length, cb.length) * 0.5;
}

/** Score a group of repeating children — higher = better card candidate */
function scoreGroup(items: Element[]): number {
  if (items.length < 3) return 0;
  let score = Math.min(items.length * 5, 50);

  // Prefer groups with rich content (links, images, spans)
  const sample = items[0];
  if (sample.querySelector('a'))   score += 15;
  if (sample.querySelector('img')) score += 10;
  if (sample.querySelectorAll('span, p').length > 1) score += 10;

  // Penalise very generic containers
  const tag = sample.tagName.toLowerCase();
  if (['li', 'tr', 'article', 'div'].includes(tag)) score += 5;

  return score;
}

/** Extract field selectors from a sample card element */
function extractFields(card: Element): { label: string; selector: string }[] {
  const fields: { label: string; selector: string }[] = [];
  const fieldCandidates: [string, string][] = [
    ['Name / Title',  'h1,h2,h3,h4,[class*="title"],[class*="name"]'],
    ['Phone',         '[class*="phone"],[class*="tel"],a[href^="tel"]'],
    ['Email',         '[class*="email"],a[href^="mailto"]'],
    ['Website',       'a[href^="http"]:not([href*="facebook"]):not([href*="instagram"])'],
    ['Address',       '[class*="address"],[class*="location"],[itemprop="address"]'],
    ['Category',      '[class*="category"],[class*="tag"],[class*="type"]'],
    ['Description',   'p,[class*="desc"],[class*="summary"]'],
    ['Image',         'img'],
  ];

  for (const [label, sel] of fieldCandidates) {
    try {
      const found = card.querySelector(sel);
      if (found) {
        const rel = generateSelectors(found);
        // Make selector relative to item
        const relSel = rel.css.replace(/^.*?>\s*/, '');
        fields.push({ label, selector: relSel });
      }
    } catch { /* skip invalid selectors */ }
  }
  return fields;
}

// ── Public API ───────────────────────────────────────────────

/**
 * Scans the entire DOM for repeating card-like structures.
 * Returns the best candidates sorted by score.
 */
export function detectCards(minRepeat = 3): CardInfo[] {
  const candidates: GroupCandidate[] = [];

  // Walk all elements that have at least minRepeat children with same tag
  const parents = document.querySelectorAll('*');
  for (const parent of Array.from(parents)) {
    const children = Array.from(parent.children);
    if (children.length < minRepeat) continue;

    // Group by tag
    const tagGroups: Record<string, Element[]> = {};
    for (const child of children) {
      const key = child.tagName;
      tagGroups[key] = tagGroups[key] || [];
      tagGroups[key].push(child);
    }

    for (const [, group] of Object.entries(tagGroups)) {
      if (group.length < minRepeat) continue;

      // Check structural similarity
      const similar = group.filter((el, i) =>
        i === 0 || areSimilar(group[0], el)
      );
      if (similar.length < minRepeat) continue;

      const parentSel = generateSelectors(parent);
      const itemSel   = generateSelectors(group[0]);

      candidates.push({
        parentSelector: parentSel.css,
        itemSelector:   group[0].tagName.toLowerCase(),
        count:          similar.length,
        score:          scoreGroup(similar),
        sample:         group[0],
      });
    }
  }

  // Sort by score, deduplicate parent selectors
  const seen = new Set<string>();
  return candidates
    .sort((a, b) => b.score - a.score)
    .filter((c) => {
      if (seen.has(c.parentSelector)) return false;
      seen.add(c.parentSelector);
      return true;
    })
    .slice(0, 5)
    .map((c) => ({
      containerSelector: c.parentSelector,
      itemSelector:      c.itemSelector,
      fields:            extractFields(c.sample),
      count:             c.count,
    }));
}

/** Extract records from a detected card group */
export function extractCards(info: CardInfo): Record<string, string>[] {
  try {
    const container = document.querySelector(info.containerSelector);
    if (!container) return [];

    const items = container.querySelectorAll(info.itemSelector);
    const rawList: Record<string, string>[] = [];

    for (const item of Array.from(items)) {
      const record: Record<string, string> = {};
      for (const field of info.fields) {
        try {
          const el = item.querySelector(field.selector);
          if (el) {
            let val = '';
            if (el.tagName === 'A') {
              const href = (el as HTMLAnchorElement).href;
              if (href.startsWith('tel:')) val = href.replace('tel:', '');
              else if (href.startsWith('mailto:')) val = href.replace('mailto:', '');
              else val = href;
            } else if (el.tagName === 'IMG') {
              val = (el as HTMLImageElement).src;
            } else {
              val = el.textContent || '';
            }
            record[field.label] = val;
          }
        } catch { /* skip */ }
      }
      if (Object.keys(record).length > 0) {
        rawList.push(record);
      }
    }

    return DataCleaner.cleanAndDeduplicate(rawList);
  } catch (err) {
    console.warn("Failed to extract cards:", err);
    return [];
  }
}
