// ============================================================
// ScripOx — Element Picker
// Hover highlight with blue border + floating tooltip
// ============================================================

import type { ElementInfo } from '../../types';
import { generateSelectors } from '../engines/SelectorEngine';

const HIGHLIGHT_CLASS = 'scripox-highlight';
const TOOLTIP_ID      = 'scripox-tooltip';

const STYLE = `
  .${HIGHLIGHT_CLASS} {
    outline: 2px solid #3b82f6 !important;
    outline-offset: 2px !important;
    cursor: crosshair !important;
    background: rgba(59,130,246,0.08) !important;
    transition: outline 0.1s ease, background 0.1s ease;
  }
  #${TOOLTIP_ID} {
    position: fixed;
    z-index: 2147483647;
    background: rgba(15,23,42,0.95);
    color: #e2e8f0;
    font: 12px/1.5 'Inter', 'Segoe UI', monospace;
    padding: 8px 12px;
    border-radius: 8px;
    border: 1px solid rgba(59,130,246,0.5);
    backdrop-filter: blur(8px);
    pointer-events: none;
    max-width: 280px;
    box-shadow: 0 4px 20px rgba(0,0,0,0.5);
  }
  #${TOOLTIP_ID} .sxt-tag  { color: #60a5fa; font-weight: 600; }
  #${TOOLTIP_ID} .sxt-id   { color: #a78bfa; }
  #${TOOLTIP_ID} .sxt-cls  { color: #34d399; }
  #${TOOLTIP_ID} .sxt-text { color: #94a3b8; font-style: italic; }
`;

export class ElementPicker {
  private enabled = false;
  private hovered: Element | null = null;
  private tooltip: HTMLElement | null = null;
  private styleEl: HTMLElement | null = null;
  private onSelect: (el: Element, info: ElementInfo) => void;

  constructor(onSelect: (el: Element, info: ElementInfo) => void) {
    this.onSelect = onSelect;
  }

  enable() {
    if (this.enabled) return;
    this.enabled = true;
    this.injectStyle();
    this.createTooltip();
    document.addEventListener('mouseover',  this.onHover,  { capture: true, passive: true });
    document.addEventListener('mouseout',   this.onOut,    { capture: true, passive: true });
    document.addEventListener('click',      this.onClick,  { capture: true });
    document.addEventListener('keydown',    this.onKey,    { capture: true });
    document.body.style.setProperty('cursor', 'crosshair', 'important');
  }

  disable() {
    if (!this.enabled) return;
    this.enabled = false;
    this.clearHighlight();
    this.tooltip?.remove();
    this.styleEl?.remove();
    this.tooltip = null;
    this.styleEl = null;
    document.removeEventListener('mouseover',  this.onHover,  { capture: true });
    document.removeEventListener('mouseout',   this.onOut,    { capture: true });
    document.removeEventListener('click',      this.onClick,  { capture: true });
    document.removeEventListener('keydown',    this.onKey,    { capture: true });
    document.body.style.removeProperty('cursor');
  }

  // ── Private ──────────────────────────────────────────────

  private injectStyle() {
    this.styleEl = document.createElement('style');
    this.styleEl.textContent = STYLE;
    (document.head || document.documentElement).appendChild(this.styleEl);
  }

  private createTooltip() {
    this.tooltip = document.createElement('div');
    this.tooltip.id = TOOLTIP_ID;
    document.documentElement.appendChild(this.tooltip);
  }

  private clearHighlight() {
    if (this.hovered) {
      try {
        this.hovered.classList.remove(HIGHLIGHT_CLASS);
      } catch { /* skip */ }
    }
    this.hovered = null;
  }

  private getElementInfo(el: Element): ElementInfo {
    const rect = el.getBoundingClientRect();
    const classList = el.classList ? Array.from(el.classList) : [];
    return {
      tag:         el.tagName.toLowerCase(),
      id:          el.id || '',
      classes:     classList.slice(0, 5) as string[],
      textPreview: (el.textContent || '').trim().slice(0, 60),
      rect,
    };
  }

  private updateTooltip(el: Element, e: MouseEvent) {
    if (!this.tooltip) return;
    const info = this.getElementInfo(el);
    this.tooltip.innerHTML = `
      <span class="sxt-tag">&lt;${info.tag}&gt;</span>
      ${info.id ? `<br><span class="sxt-id">#${info.id}</span>` : ''}
      ${info.classes.length ? `<br><span class="sxt-cls">.${info.classes.join(' .')}</span>` : ''}
      ${info.textPreview ? `<br><span class="sxt-text">"${info.textPreview}"</span>` : ''}
    `;
    const tw = this.tooltip.offsetWidth;
    const th = this.tooltip.offsetHeight;
    let left = e.clientX + 14;
    let top  = e.clientY + 14;
    if (left + tw > window.innerWidth  - 10) left = e.clientX - tw - 14;
    if (top  + th > window.innerHeight - 10) top  = e.clientY - th - 14;
    this.tooltip.style.left = `${left}px`;
    this.tooltip.style.top  = `${top}px`;
  }

  private onHover = (e: Event) => {
    const target = e.target as Element;
    if (!target || target.id === TOOLTIP_ID || !target.classList) return;

    // Ignore element picker hovering on sidebar elements
    if (target.closest && target.closest('#scripox-extension-host')) return;

    this.clearHighlight();
    try {
      target.classList.add(HIGHLIGHT_CLASS);
      this.hovered = target;
      this.updateTooltip(target, e as MouseEvent);
    } catch { /* skip */ }
  };

  private onOut = (_e: Event) => {
    this.clearHighlight();
    if (this.tooltip) this.tooltip.innerHTML = '';
  };

  private onClick = (e: Event) => {
    const target = e.target as Element;
    if (!target || target.id === TOOLTIP_ID || !target.classList) return;
    if (target.closest && target.closest('#scripox-extension-host')) return;

    e.preventDefault();
    e.stopPropagation();

    try {
      const info = this.getElementInfo(target);
      this.onSelect(target, info);
    } catch { /* skip */ }
    this.disable();
  };

  private onKey = (e: KeyboardEvent) => {
    if (e.key === 'Escape') this.disable();
  };
}
