// ============================================================
// ScripOx Visual Extractor — Shared TypeScript Types
// ============================================================

export type ExtractorMode = 'idle' | 'picking' | 'table' | 'card';

export interface ElementInfo {
  tag: string;
  id: string;
  classes: string[];
  textPreview: string;
  rect: DOMRect;
}

export interface SelectorResult {
  css: string;
  xpath: string;
  relativeXpath: string;
  domPath: string;
  bestSelector: string;
  scores: {
    css: number;
    xpath: number;
    relativeXpath: number;
    domPath: number;
  };
}

export interface FieldMapping {
  name?: string;
  phone?: string;
  phone2?: string;
  email?: string;
  website?: string;
  address?: string;
  city?: string;
  country?: string;
  category?: string;
  facebook?: string;
  instagram?: string;
  description?: string;
  [key: string]: string | undefined;
}

export interface ExtractedRecord {
  [field: string]: string | null | undefined;
}

export interface TableInfo {
  selector: string;
  headers: string[];
  rows: string[][];
  selectedColumns: number[];
}

export interface CardInfo {
  containerSelector: string;
  itemSelector: string;
  fields: { label: string; selector: string }[];
  count: number;
}

export interface PaginationInfo {
  type: 'next-button' | 'infinite-scroll' | 'load-more' | 'none';
  selector?: string;
}

export interface ExtractionTemplate {
  id: string;
  name: string;
  url?: string;
  urlPattern?: string;
  selectors: SelectorResult;
  mapping: FieldMapping;
  pagination?: PaginationInfo;
  createdAt: number;
}

export interface BrowserExtractPayload {
  url: string;
  title: string;
  selector: string;
  xpath: string;
  mapping: FieldMapping;
  records: ExtractedRecord[];
}

export interface ApiStatus {
  connected: boolean;
  version?: string;
  checkedAt: number;
}

// ── Chrome messages ──────────────────────────────────────────

export type MessageType =
  | 'CALL_API'
  | 'API_RESULT'
  | 'TOGGLE_SIDEBAR'
  | 'TOGGLE_PICKER'
  | 'GET_API_STATUS'
  | 'API_STATUS_RESULT';

export interface ChromeMessage {
  type: MessageType;
  payload?: unknown;
}
