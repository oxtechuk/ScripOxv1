// ============================================================
// ScripOx — CSS Selector Engine
// Generates CSS, XPath, Relative XPath, DOMPath + confidence scores
// ============================================================

import type { SelectorResult } from '../../types';
import { getAbsoluteXPath, getRelativeXPath, scoreXPath } from './XPathEngine';

// ── CSS Selector Generation ──────────────────────────────────

function getCssSelector(el: Element): string {
  // Strategy 1: unique ID
  if (el.id) return `#${CSS.escape(el.id)}`;

  const parts: string[] = [];
  let node: Element | null = el;

  while (node && node !== document.documentElement) {
    let selector = node.tagName.toLowerCase();

    // Add classes (filter utility/JS classes)
    const classes = Array.from(node.classList)
      .filter((c) => !/^(js-|is-|has-|ng-|v-|active|selected|hover|focus)/.test(c))
      .slice(0, 3);

    if (classes.length) {
      selector += '.' + classes.map((c) => CSS.escape(c)).join('.');
    }

    // Add :nth-of-type if sibling disambiguates
    const parent = node.parentElement;
    if (parent) {
      const siblings = Array.from(parent.children).filter(
        (s) => s.tagName === node!.tagName
      );
      if (siblings.length > 1) {
        const idx = siblings.indexOf(node) + 1;
        selector += `:nth-of-type(${idx})`;
      }
    }

    parts.unshift(selector);

    // Stop early if unique
    try {
      if (document.querySelectorAll(parts.join(' > ')).length === 1) break;
    } catch {
      // invalid selector midway — continue
    }

    node = node.parentElement;
  }

  return parts.join(' > ');
}

function getDomPath(el: Element): string {
  const parts: string[] = [];
  let node: Element | null = el;
  while (node && node !== document.documentElement) {
    const parent = node.parentElement;
    if (!parent) break;
    const idx = Array.from(parent.children).indexOf(node);
    parts.unshift(`${node.tagName.toLowerCase()}:nth-child(${idx + 1})`);
    node = parent;
  }
  return parts.join(' > ');
}

function scoreCss(selector: string): number {
  let score = 100;
  if (selector.startsWith('#')) return 99; // ID → almost perfect
  score -= (selector.match(/nth/g) || []).length * 10;  // nth = fragile
  score -= selector.split('>').length * 3;               // depth cost
  score += selector.includes('.') ? 10 : 0;             // class = good
  return Math.max(0, Math.min(100, score));
}

// ── Public API ───────────────────────────────────────────────

export function generateSelectors(el: Element): SelectorResult {
  const css          = getCssSelector(el);
  const xpath        = getAbsoluteXPath(el);
  const relativeXpath = getRelativeXPath(el);
  const domPath      = getDomPath(el);

  const scores = {
    css:          scoreCss(css),
    xpath:        scoreXPath(xpath),
    relativeXpath: scoreXPath(relativeXpath),
    domPath:      Math.max(0, 70 - domPath.split('>').length * 5),
  };

  const best = Object.entries(scores).reduce((a, b) => (b[1] > a[1] ? b : a));

  const bestSelector =
    best[0] === 'css'          ? css          :
    best[0] === 'relativeXpath' ? relativeXpath :
    best[0] === 'xpath'        ? xpath         : domPath;

  return { css, xpath, relativeXpath, domPath, bestSelector, scores };
}

/** Test if a selector still matches the expected element */
export function testSelector(selector: string, original?: Element): boolean {
  try {
    const matches = document.querySelectorAll(selector);
    if (!matches.length) return false;
    if (original) return Array.from(matches).includes(original);
    return true;
  } catch {
    return false;
  }
}
