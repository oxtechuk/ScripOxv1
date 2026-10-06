// ============================================================
// ScripOx — Background Service Worker
// Bridges content scripts ↔ FastAPI (avoids CORS restrictions)
// ============================================================

import type { ChromeMessage, ApiStatus } from '../types';

const API_BASE = 'http://127.0.0.1:8765';

let cachedStatus: ApiStatus = { connected: false, checkedAt: 0 };

// ── Health check ─────────────────────────────────────────────
async function checkHealth(): Promise<ApiStatus> {
  // Cache for 10 seconds
  if (Date.now() - cachedStatus.checkedAt < 10_000) return cachedStatus;
  try {
    const res = await fetch(`${API_BASE}/api/health`, { signal: AbortSignal.timeout(3000) });
    const data = res.ok ? await res.json() : null;
    cachedStatus = { connected: res.ok, version: data?.version, checkedAt: Date.now() };
  } catch {
    cachedStatus = { connected: false, checkedAt: Date.now() };
  }
  return cachedStatus;
}

// ── API proxy ────────────────────────────────────────────────
async function callApi(method: string, path: string, body?: unknown): Promise<unknown> {
  const opts: RequestInit = {
    method,
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    signal: AbortSignal.timeout(15_000),
  };
  if (body) opts.body = JSON.stringify(body);
  const res = await fetch(`${API_BASE}${path}`, opts);
  if (!res.ok) {
    const err = await res.text();
    throw new Error(`API ${res.status}: ${err}`);
  }
  return res.status === 204 ? null : res.json();
}

// ── Message handler ──────────────────────────────────────────
chrome.runtime.onMessage.addListener(
  (msg: ChromeMessage, _sender, sendResponse) => {
    if (msg.type === 'GET_API_STATUS') {
      checkHealth().then((status) => sendResponse({ type: 'API_STATUS_RESULT', payload: status }));
      return true; // keep channel open
    }

    if (msg.type === 'CALL_API') {
      const { method, path, body } = msg.payload as { method: string; path: string; body?: unknown };
      callApi(method, path, body)
        .then((data) => sendResponse({ type: 'API_RESULT', payload: { ok: true, data } }))
        .catch((err) => sendResponse({ type: 'API_RESULT', payload: { ok: false, error: String(err) } }));
      return true;
    }

    if (msg.type === 'TOGGLE_SIDEBAR') {
      chrome.tabs.query({ active: true, currentWindow: true }, ([tab]) => {
        if (tab?.id) {
          chrome.tabs.sendMessage(tab.id, { type: 'TOGGLE_SIDEBAR' });
        }
      });
    }

    if (msg.type === 'TOGGLE_PICKER') {
      chrome.tabs.query({ active: true, currentWindow: true }, ([tab]) => {
        if (tab?.id) {
          chrome.tabs.sendMessage(tab.id, { type: 'TOGGLE_PICKER' });
        }
      });
    }
  }
);

// ── Keyboard shortcut commands ────────────────────────────────
chrome.commands.onCommand.addListener((command) => {
  chrome.tabs.query({ active: true, currentWindow: true }, ([tab]) => {
    if (!tab?.id) return;
    if (command === 'toggle-picker') {
      chrome.tabs.sendMessage(tab.id, { type: 'TOGGLE_PICKER' });
    } else if (command === 'toggle-sidebar') {
      chrome.tabs.sendMessage(tab.id, { type: 'TOGGLE_SIDEBAR' });
    }
  });
});

// ── Startup health check ──────────────────────────────────────
chrome.runtime.onStartup.addListener(() => checkHealth());
checkHealth();

export {};
