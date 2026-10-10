import React, { useEffect, useState } from 'react';
import { createRoot } from 'react-dom/client';
import '../index.css';
import {
  Globe,
  Palette,
  Layers,
  Check,
  RotateCcw,
  Sun,
  Moon,
  ExternalLink,
  CheckCircle2,
  AlertCircle,
  Sliders,
  Radio
} from 'lucide-react';

interface Settings {
  apiUrl: string;
  autoDetectPagination: boolean;
  highlightColor: string;
  minCardRepeat: number;
}

const DEFAULT_SETTINGS: Settings = {
  apiUrl: 'http://127.0.0.1:8765',
  autoDetectPagination: true,
  highlightColor: '#2563eb',
  minCardRepeat: 3,
};

const PRESET_COLORS = [
  { label: 'Blue', hex: '#2563eb' },
  { label: 'Sky', hex: '#0284c7' },
  { label: 'Emerald', hex: '#059669' },
  { label: 'Indigo', hex: '#4f46e5' },
  { label: 'Amber', hex: '#d97706' },
  { label: 'Rose', hex: '#e11d48' },
];

function Options() {
  const [settings, setSettings] = useState<Settings>(DEFAULT_SETTINGS);
  const [saved, setSaved] = useState(false);
  const [isDark, setIsDark] = useState<boolean>(() => {
    return localStorage.getItem('scripox_theme') !== 'light';
  });
  const [pingStatus, setPingStatus] = useState<'idle' | 'checking' | 'connected' | 'failed'>('idle');

  useEffect(() => {
    chrome.storage.local.get('settings', (res) => {
      if (res.settings) {
        setSettings({ ...DEFAULT_SETTINGS, ...res.settings });
      }
    });
  }, []);

  const toggleTheme = () => {
    setIsDark((prev) => {
      const next = !prev;
      localStorage.setItem('scripox_theme', next ? 'dark' : 'light');
      return next;
    });
  };

  const update = <K extends keyof Settings>(key: K, value: Settings[K]) => {
    setSettings((prev) => ({ ...prev, [key]: value }));
  };

  const save = () => {
    chrome.storage.local.set({ settings }, () => {
      setSaved(true);
      setTimeout(() => setSaved(false), 2500);
    });
  };

  const resetDefaults = () => {
    setSettings(DEFAULT_SETTINGS);
  };

  const testConnection = async () => {
    setPingStatus('checking');
    try {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), 2000);
      const res = await fetch(`${settings.apiUrl.replace(/\/+$/, '')}/status`, {
        signal: controller.signal,
      });
      clearTimeout(timeoutId);
      if (res.ok) {
        setPingStatus('connected');
      } else {
        setPingStatus('failed');
      }
    } catch {
      setPingStatus('failed');
    }
    setTimeout(() => setPingStatus('idle'), 3500);
  };

  return (
    <div
      className={`min-h-screen transition-colors duration-200 ${
        isDark ? 'bg-slate-950 text-slate-100' : 'bg-slate-50 text-slate-900'
      } py-10 px-4 sm:px-6`}
      style={{ fontFamily: "'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif" }}
    >
      <div className="max-w-2xl mx-auto space-y-6">
        {/* Header */}
        <header
          className={`rounded-2xl border p-6 flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4 transition-colors ${
            isDark ? 'bg-slate-900/80 border-slate-800' : 'bg-white border-slate-200 shadow-sm'
          }`}
        >
          <div className="flex items-center gap-3.5">
            <div className={`w-11 h-11 rounded-xl flex items-center justify-center border ${
              isDark ? 'bg-slate-800 border-slate-700/60' : 'bg-slate-100 border-slate-200'
            }`}>
              <Sliders className={`w-5 h-5 ${isDark ? 'text-blue-400' : 'text-blue-600'}`} />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h1 className="text-lg font-bold tracking-tight">ScripOx Settings</h1>
                <span className={`text-[11px] font-semibold px-2 py-0.5 rounded-full border ${
                  isDark ? 'bg-slate-800 text-slate-400 border-slate-700' : 'bg-slate-100 text-slate-600 border-slate-200'
                }`}>
                  v1.0.0
                </span>
              </div>
              <p className={`text-xs mt-0.5 ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>
                Visual Extractor configuration and desktop bridge
              </p>
            </div>
          </div>

          <div className="flex items-center gap-2 self-end sm:self-auto">
            <button
              type="button"
              onClick={resetDefaults}
              className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium border transition-colors cursor-pointer ${
                isDark
                  ? 'border-slate-800 bg-slate-900 text-slate-400 hover:text-slate-200 hover:bg-slate-800'
                  : 'border-slate-200 bg-white text-slate-600 hover:text-slate-900 hover:bg-slate-100'
              }`}
              title="Reset all settings to defaults"
            >
              <RotateCcw className="w-3.5 h-3.5" />
              <span>Defaults</span>
            </button>

            <button
              type="button"
              onClick={toggleTheme}
              className={`p-2 rounded-lg border transition-colors cursor-pointer ${
                isDark
                  ? 'border-slate-800 bg-slate-900 text-slate-400 hover:text-slate-200 hover:bg-slate-800'
                  : 'border-slate-200 bg-white text-slate-600 hover:text-slate-900 hover:bg-slate-100'
              }`}
              title={isDark ? 'Switch to light mode' : 'Switch to dark mode'}
            >
              {isDark ? <Sun className="w-4 h-4" /> : <Moon className="w-4 h-4" />}
            </button>
          </div>
        </header>

        {/* Section 1: Desktop Link & API */}
        <section
          className={`rounded-2xl border p-6 space-y-4 transition-colors ${
            isDark ? 'bg-slate-900/80 border-slate-800' : 'bg-white border-slate-200 shadow-sm'
          }`}
        >
          <div className="flex items-center gap-2.5 pb-2 border-b border-slate-200/10">
            <Globe className={`w-4 h-4 ${isDark ? 'text-blue-400' : 'text-blue-600'}`} />
            <div>
              <h2 className="text-sm font-semibold">Desktop Service API</h2>
              <p className={`text-xs ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>
                Local endpoint used to communicate with the ScripOx Desktop engine
              </p>
            </div>
          </div>

          <div className="space-y-2">
            <div className="flex gap-2">
              <input
                type="url"
                value={settings.apiUrl}
                onChange={(e) => update('apiUrl', e.target.value)}
                placeholder="http://127.0.0.1:8765"
                className={`flex-1 rounded-xl px-3.5 py-2 text-xs font-mono border transition-colors outline-none ${
                  isDark
                    ? 'bg-slate-950 border-slate-800 text-slate-100 focus:border-blue-500'
                    : 'bg-slate-50 border-slate-200 text-slate-800 focus:border-blue-600'
                }`}
              />
              <button
                type="button"
                onClick={testConnection}
                disabled={pingStatus === 'checking'}
                className={`px-3.5 py-2 rounded-xl text-xs font-medium border flex items-center gap-1.5 transition-colors cursor-pointer ${
                  isDark
                    ? 'bg-slate-800 border-slate-700 text-slate-200 hover:bg-slate-700'
                    : 'bg-slate-100 border-slate-200 text-slate-700 hover:bg-slate-200'
                }`}
              >
                <Radio className={`w-3.5 h-3.5 ${pingStatus === 'checking' ? 'animate-pulse text-blue-500' : ''}`} />
                <span>{pingStatus === 'checking' ? 'Testing…' : 'Test Ping'}</span>
              </button>
            </div>

            {pingStatus === 'connected' && (
              <div className="flex items-center gap-1.5 text-xs text-emerald-500 pt-1 font-medium">
                <CheckCircle2 className="w-3.5 h-3.5" />
                <span>Desktop service responded successfully!</span>
              </div>
            )}
            {pingStatus === 'failed' && (
              <div className="flex items-center gap-1.5 text-xs text-amber-500 pt-1 font-medium">
                <AlertCircle className="w-3.5 h-3.5" />
                <span>Could not reach service at this address. Make sure ScripOx Desktop is open.</span>
              </div>
            )}

            <p className={`text-[11px] ${isDark ? 'text-slate-500' : 'text-slate-400'}`}>
              Default port is <code className="font-mono text-xs">http://127.0.0.1:8765</code>
            </p>
          </div>
        </section>

        {/* Section 2: Visual Picker Appearance */}
        <section
          className={`rounded-2xl border p-6 space-y-4 transition-colors ${
            isDark ? 'bg-slate-900/80 border-slate-800' : 'bg-white border-slate-200 shadow-sm'
          }`}
        >
          <div className="flex items-center gap-2.5 pb-2 border-b border-slate-200/10">
            <Palette className={`w-4 h-4 ${isDark ? 'text-blue-400' : 'text-blue-600'}`} />
            <div>
              <h2 className="text-sm font-semibold">Element Highlight Color</h2>
              <p className={`text-xs ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>
                Accent border color applied when hovering elements on web pages
              </p>
            </div>
          </div>

          <div className="space-y-3">
            {/* Swatches & Custom Picker */}
            <div className="flex flex-wrap items-center gap-2">
              {PRESET_COLORS.map((preset) => {
                const active = settings.highlightColor.toLowerCase() === preset.hex.toLowerCase();
                return (
                  <button
                    key={preset.hex}
                    type="button"
                    onClick={() => update('highlightColor', preset.hex)}
                    className={`flex items-center gap-1.5 px-3 py-1.5 rounded-lg text-xs font-medium border transition-all cursor-pointer ${
                      active
                        ? isDark
                          ? 'border-blue-500 bg-blue-500/10 text-white'
                          : 'border-blue-600 bg-blue-50 text-blue-700'
                        : isDark
                        ? 'border-slate-800 bg-slate-950 text-slate-300 hover:border-slate-700'
                        : 'border-slate-200 bg-slate-50 text-slate-700 hover:border-slate-300'
                    }`}
                  >
                    <span
                      className="w-3 h-3 rounded-full flex-shrink-0"
                      style={{ backgroundColor: preset.hex }}
                    />
                    <span>{preset.label}</span>
                    {active && <Check className="w-3 h-3 ml-0.5" />}
                  </button>
                );
              })}

              {/* Native color picker */}
              <div className="flex items-center gap-2 ml-auto">
                <input
                  type="color"
                  value={settings.highlightColor}
                  onChange={(e) => update('highlightColor', e.target.value)}
                  className="w-7 h-7 rounded-lg border-0 bg-transparent cursor-pointer p-0"
                  title="Pick custom color"
                />
                <span className={`text-xs font-mono font-medium ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>
                  {settings.highlightColor}
                </span>
              </div>
            </div>

            {/* Live Preview Box */}
            <div
              className="p-3 rounded-xl border flex items-center justify-between text-xs transition-colors"
              style={{
                borderColor: `${settings.highlightColor}40`,
                backgroundColor: `${settings.highlightColor}10`,
              }}
            >
              <div className="flex items-center gap-2">
                <span
                  className="w-2.5 h-2.5 rounded-full"
                  style={{ backgroundColor: settings.highlightColor }}
                />
                <span className="font-medium" style={{ color: settings.highlightColor }}>
                  Interactive Highlight Preview
                </span>
              </div>
              <span className={`text-[11px] opacity-75 ${isDark ? 'text-slate-300' : 'text-slate-700'}`}>
                Active Outline
              </span>
            </div>
          </div>
        </section>

        {/* Section 3: Detection & Behavior */}
        <section
          className={`rounded-2xl border p-6 space-y-5 transition-colors ${
            isDark ? 'bg-slate-900/80 border-slate-800' : 'bg-white border-slate-200 shadow-sm'
          }`}
        >
          <div className="flex items-center gap-2.5 pb-2 border-b border-slate-200/10">
            <Layers className={`w-4 h-4 ${isDark ? 'text-blue-400' : 'text-blue-600'}`} />
            <div>
              <h2 className="text-sm font-semibold">Extraction Behavior</h2>
              <p className={`text-xs ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>
                Card clustering and pagination detection heuristics
              </p>
            </div>
          </div>

          {/* Min Card Repeat Stepper */}
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-3">
            <div>
              <label className="text-xs font-medium block">
                Minimum Repeating Elements
              </label>
              <p className={`text-[11px] mt-0.5 ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>
                Minimum matching elements required to detect a repeating list or card group (2 – 20)
              </p>
            </div>

            <div className="flex items-center gap-2">
              <button
                type="button"
                onClick={() => update('minCardRepeat', Math.max(2, settings.minCardRepeat - 1))}
                className={`w-8 h-8 rounded-lg border text-sm font-semibold flex items-center justify-center transition-colors cursor-pointer ${
                  isDark
                    ? 'border-slate-800 bg-slate-950 text-slate-300 hover:bg-slate-800'
                    : 'border-slate-200 bg-slate-50 text-slate-700 hover:bg-slate-100'
                }`}
              >
                -
              </button>
              <input
                type="number"
                min={2}
                max={20}
                value={settings.minCardRepeat}
                onChange={(e) => update('minCardRepeat', Math.max(2, Math.min(20, Number(e.target.value) || 2)))}
                className={`w-14 h-8 rounded-lg text-center text-xs font-semibold border outline-none ${
                  isDark
                    ? 'bg-slate-950 border-slate-800 text-slate-100 focus:border-blue-500'
                    : 'bg-slate-50 border-slate-200 text-slate-900 focus:border-blue-600'
                }`}
              />
              <button
                type="button"
                onClick={() => update('minCardRepeat', Math.min(20, settings.minCardRepeat + 1))}
                className={`w-8 h-8 rounded-lg border text-sm font-semibold flex items-center justify-center transition-colors cursor-pointer ${
                  isDark
                    ? 'border-slate-800 bg-slate-950 text-slate-300 hover:bg-slate-800'
                    : 'border-slate-200 bg-slate-50 text-slate-700 hover:bg-slate-100'
                }`}
              >
                +
              </button>
            </div>
          </div>

          <div className="h-px bg-slate-200/10" />

          {/* Auto detect pagination switch */}
          <div className="flex items-center justify-between gap-4">
            <div>
              <p className="text-xs font-medium">Auto-detect Pagination</p>
              <p className={`text-[11px] mt-0.5 ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>
                Automatically scan for Next Page links and Load More buttons
              </p>
            </div>

            <button
              type="button"
              role="switch"
              aria-checked={settings.autoDetectPagination}
              onClick={() => update('autoDetectPagination', !settings.autoDetectPagination)}
              className={`w-11 h-6 rounded-full transition-colors relative cursor-pointer flex-shrink-0 ${
                settings.autoDetectPagination
                  ? 'bg-blue-600'
                  : isDark
                  ? 'bg-slate-800'
                  : 'bg-slate-300'
              }`}
            >
              <span
                className={`absolute top-1 left-1 w-4 h-4 bg-white rounded-full shadow-sm transition-transform duration-200 ${
                  settings.autoDetectPagination ? 'translate-x-5' : 'translate-x-0'
                }`}
              />
            </button>
          </div>
        </section>

        {/* Save button & feedback */}
        <div className="space-y-3 pt-1">
          <button
            type="button"
            onClick={save}
            className={`w-full py-2.5 px-4 rounded-xl text-xs font-semibold flex items-center justify-center gap-2 transition-all cursor-pointer shadow-sm ${
              saved
                ? 'bg-emerald-600 hover:bg-emerald-500 text-white'
                : 'bg-blue-600 hover:bg-blue-500 text-white'
            }`}
          >
            {saved ? (
              <>
                <Check className="w-4 h-4" />
                <span>Saved Successfully</span>
              </>
            ) : (
              <span>Save Settings</span>
            )}
          </button>

          <footer className="text-center">
            <p className={`text-[11px] ${isDark ? 'text-slate-600' : 'text-slate-400'}`}>
              ScripOx Visual Extractor • Ox Tech UK
            </p>
          </footer>
        </div>
      </div>
    </div>
  );
}

createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <Options />
  </React.StrictMode>
);
