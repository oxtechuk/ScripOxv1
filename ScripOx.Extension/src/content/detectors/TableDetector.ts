// ============================================================
// ScripOx — Table Detector
// Detects HTML tables and extracts rows/columns for preview
// ============================================================

import type { TableInfo } from '../../types';
import { generateSelectors } from '../engines/SelectorEngine';

const TABLE_HIGHLIGHT = 'scripox-table-highlight';

const TABLE_STYLE = `
  .${TABLE_HIGHLIGHT} {
    outline: 2px dashed #f59e0b !important;
    outline-offset: 3px !important;
    background: rgba(245,158,11,0.06) !important;
    cursor: pointer !important;
  }
`;

export class TableDetector {
  private styleEl: HTMLElement | null = null;
  private tables: Element[] = [];
  private onClick: (info: TableInfo) => void;

  constructor(onClick: (info: TableInfo) => void) {
    this.onClick = onClick;
  }

  /** Find all tables on the page and highlight them */
  enable() {
    this.injectStyle();
    // Query both native tables and div elements behaving as grids or tables
    const selector = 'table, [role="grid"], [role="table"], .table, .grid-table, .data-table';
    this.tables = Array.from(document.querySelectorAll(selector));
    this.tables.forEach((t) => {
      t.classList.add(TABLE_HIGHLIGHT);
      t.addEventListener('click', this.handleClick, { capture: true, once: true });
    });
  }

  disable() {
    this.tables.forEach((t) => {
      t.classList.remove(TABLE_HIGHLIGHT);
      t.removeEventListener('click', this.handleClick, { capture: true });
    });
    this.styleEl?.remove();
    this.styleEl = null;
    this.tables = [];
  }

  /** Extract table data to JSON */
  static extractTable(table: Element, selectedColumns?: number[]): TableInfo {
    const sel = generateSelectors(table);

    // Traditional native HTML table
    if (table.tagName === 'TABLE') {
      const rows = Array.from(table.querySelectorAll('tr'));
      const headers: string[] = [];
      const headerRow = rows.find((r) => r.querySelector('th'));
      if (headerRow) {
        Array.from(headerRow.querySelectorAll('th, td')).forEach((cell) => {
          headers.push((cell.textContent || '').trim());
        });
      }

      const dataRows: string[][] = [];
      rows
        .filter((r) => !r.querySelector('th') || r !== headerRow)
        .forEach((row) => {
          const cells = Array.from(row.querySelectorAll('td, th'));
          const rowData = cells.map((c) => (c.textContent || '').trim());
          if (rowData.some((c) => c)) dataRows.push(rowData);
        });

      return {
        selector:       sel.css,
        headers,
        rows:           dataRows,
        selectedColumns: selectedColumns ?? headers.map((_, i) => i),
      };
    }

    // Modern DIV-based table / CSS Grid / Flexbox list
    let rows = Array.from(table.querySelectorAll('[role="row"], .row, .tr, [class*="-row"]'));
    if (rows.length === 0) {
      rows = Array.from(table.children);
    }

    const dataRows: string[][] = [];
    let headers: string[] = [];

    rows.forEach((row, rowIndex) => {
      let cells = Array.from(row.querySelectorAll('[role="gridcell"], [role="cell"], .cell, .col, .td, [class*="-cell"], [class*="-col"]'));
      if (cells.length === 0) {
        cells = Array.from(row.children);
      }

      const rowData = cells.map((c) => (c.textContent || '').trim());
      if (rowData.some((c) => c)) {
        const isHeader = rowIndex === 0 && (row.querySelector('.header, .th, [role="columnheader"]') || rows.length > 1);
        if (isHeader) {
          headers = rowData.map((h, i) => h || `Column ${i + 1}`);
        } else {
          dataRows.push(rowData);
        }
      }
    });

    if (headers.length === 0 && dataRows.length > 0) {
      headers = dataRows[0].map((_, i) => `Column ${i + 1}`);
    }

    return {
      selector:       sel.css,
      headers,
      rows:           dataRows,
      selectedColumns: selectedColumns ?? headers.map((_, i) => i),
    };
  }

  private injectStyle() {
    this.styleEl = document.createElement('style');
    this.styleEl.textContent = TABLE_STYLE;
    (document.head || document.documentElement).appendChild(this.styleEl);
  }

  private handleClick = (e: Event) => {
    e.preventDefault();
    e.stopPropagation();
    const table = e.currentTarget as Element;
    const info = TableDetector.extractTable(table);
    this.onClick(info);
    this.disable();
  };
}
