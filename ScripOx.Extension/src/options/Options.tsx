import React, { useEffect, useState } from 'react';
import { createRoot } from 'react-dom/client';
import '../index.css';

interface Settings {
  apiUrl: string;
  autoDetectPagination: boolean;
  highlightColor: string;
  minCardRepeat: number;
}

const DEFAULT: Settings = {
  apiUrl: 'http://127.0.0.1:8765',
  autoDetectPagination: true,
  highlightColor: '#3b82f6',
  minCardRepeat: 3,
};

function Options() {
  const [settings, setSettings] = useState<Settings>(DEFAULT);
  const [saved, setSaved] = useState(false);

  useEffect(() => {
    chrome.storage.local.get('settings', (res) => {
      if (res.settings) setSettings({ ...DEFAULT, ...res.settings });
    });
  }, []);

  const save = () => {
    chrome.storage.local.set({ settings }, () => {
      setSaved(true);
      setTimeout(() => setSaved(false), 2000);
    });
  };

  const update = <K extends keyof Settings>(key: K, value: Settings[K]) => {
    setSettings((s) => ({ ...s, [key]: value }));
  };

  return (
    <div
      className="min-h-screen bg-slate-900 text-slate-100 p-8"
      style={{ fontFamily: "'Inter','Segoe UI',sans-serif" }}
    >
      <div className="max-w-xl mx-auto space-y-8">
        {/* Header */}
        <div className="flex items-center gap-4">
          <span className="text-4xl">🦂</span>
          <div>
            <h1 className="text-2xl font-bold bg-gradient-to-r from-blue-400 to-violet-400
                            bg-clip-text text-transparent">
              ScripOx Settings
            </h1>
            <p className="text-slate-500 text-sm">Visual Extractor Configuration</p>
          </div>
        </div>

        {/* Settings form */}
        <div className="bg-slate-800/50 rounded-2xl border border-slate-700/50 p-6 space-y-6">

          {/* API URL */}
          <div className="space-y-2">
            <label className="text-sm font-semibold text-slate-300">ScripOx API URL</label>
            <input
              type="url"
              value={settings.apiUrl}
              onChange={(e) => update('apiUrl', e.target.value)}
              className="w-full bg-slate-900 border border-slate-700 rounded-xl px-4 py-2.5
                         text-sm text-slate-200 placeholder-slate-600
                         focus:outline-none focus:border-blue-500/60 transition-colors"
              placeholder="http://127.0.0.1:8765"
            />
            <p className="text-xs text-slate-600">Default: http://127.0.0.1:8765</p>
          </div>

          {/* Highlight Color */}
          <div className="space-y-2">
            <label className="text-sm font-semibold text-slate-300">Element Highlight Color</label>
            <div className="flex items-center gap-3">
              <input
                type="color"
                value={settings.highlightColor}
                onChange={(e) => update('highlightColor', e.target.value)}
                className="w-12 h-10 rounded-lg border border-slate-700 bg-slate-900 cursor-pointer"
              />
              <span className="text-sm text-slate-400 font-mono">{settings.highlightColor}</span>
            </div>
          </div>

          {/* Min card repeat */}
          <div className="space-y-2">
            <label className="text-sm font-semibold text-slate-300">
              Minimum Repeating Elements for Card Detection
            </label>
            <input
              type="number"
              min={2}
              max={20}
              value={settings.minCardRepeat}
              onChange={(e) => update('minCardRepeat', Number(e.target.value))}
              className="w-24 bg-slate-900 border border-slate-700 rounded-xl px-3 py-2
                         text-sm text-slate-200
                         focus:outline-none focus:border-blue-500/60 transition-colors"
            />
            <p className="text-xs text-slate-600">
              Minimum number of similar elements to consider a repeating card group (default: 3)
            </p>
          </div>

          {/* Auto detect pagination */}
          <div className="flex items-center justify-between">
            <div>
              <p className="text-sm font-semibold text-slate-300">Auto-detect Pagination</p>
              <p className="text-xs text-slate-600">Automatically scan for next/load-more buttons</p>
            </div>
            <button
              onClick={() => update('autoDetectPagination', !settings.autoDetectPagination)}
              className={`w-12 h-6 rounded-full transition-all duration-300
                          ${settings.autoDetectPagination ? 'bg-blue-600' : 'bg-slate-700'}
                          relative`}
            >
              <span
                className={`absolute top-1 w-4 h-4 bg-white rounded-full shadow
                             transition-transform duration-300
                             ${settings.autoDetectPagination ? 'translate-x-7' : 'translate-x-1'}`}
              />
            </button>
          </div>
        </div>

        {/* Save button */}
        <button
          onClick={save}
          className="w-full py-3 rounded-xl font-semibold
                     bg-gradient-to-r from-blue-600 to-violet-600
                     hover:from-blue-500 hover:to-violet-500
                     text-white shadow-lg shadow-blue-500/20
                     transition-all duration-200"
        >
          {saved ? '✓ Saved!' : 'Save Settings'}
        </button>

        <p className="text-center text-xs text-slate-700">
          ScripOx Visual Extractor v1.0.0 — Ox Tech UK
        </p>
      </div>
    </div>
  );
}

createRoot(document.getElementById('root')!).render(
  <React.StrictMode><Options /></React.StrictMode>
);
