// ============================================================
// ScripOx — FastAPI Client
// All HTTP calls go through the background service worker
// to avoid CORS restrictions from content scripts
// ============================================================

import type { BrowserExtractPayload, ApiStatus } from '../types';

function sendToBackground<T>(message: object): Promise<T> {
  return new Promise((resolve, reject) => {
    chrome.runtime.sendMessage(message, (response) => {
      if (chrome.runtime.lastError) {
        reject(new Error(chrome.runtime.lastError.message));
        return;
      }
      if (response?.payload?.ok === false) {
        reject(new Error(response.payload.error));
      } else {
        resolve(response?.payload?.data ?? response?.payload);
      }
    });
  });
}

// ── Public API methods ────────────────────────────────────────

/** Check if the ScripOx backend is running */
export async function getApiStatus(): Promise<ApiStatus> {
  return sendToBackground({ type: 'GET_API_STATUS' });
}

/** Send extracted records to ScripOx Desktop */
export async function sendExtraction(payload: BrowserExtractPayload): Promise<unknown> {
  return sendToBackground({
    type: 'CALL_API',
    payload: {
      method: 'POST',
      path: '/api/connectors/browser-extract',
      body: payload,
    },
  });
}

/** List all extraction jobs */
export async function listJobs(page = 1): Promise<unknown> {
  return sendToBackground({
    type: 'CALL_API',
    payload: { method: 'GET', path: `/api/jobs?page=${page}&page_size=10` },
  });
}

/** Get health status */
export async function getHealth(): Promise<unknown> {
  return sendToBackground({
    type: 'CALL_API',
    payload: { method: 'GET', path: '/api/health' },
  });
}
