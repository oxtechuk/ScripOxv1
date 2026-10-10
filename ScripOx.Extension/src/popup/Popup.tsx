import React, { useEffect, useState } from 'react';
import { createRoot } from 'react-dom/client';
import '../index.css';
import {
  Crosshair,
  PanelRight,
  Download,
  Settings as SettingsIcon,
  CheckCircle2,
  AlertCircle
} from 'lucide-react';

interface StatusData {
  connected: boolean;
  version?: string;
}

function Popup() {
  const [status, setStatus] = useState<StatusData>({ connected: false });
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    chrome.runtime.sendMessage({ type: 'GET_API_STATUS' }, (res) => {
      setStatus(res?.payload || { connected: false });
      setLoading(false);
    });
  }, []);

  const openSidebar = () => {
    chrome.tabs.query({ active: true, currentWindow: true }, ([tab]) => {
      if (tab?.id) {
        chrome.tabs.sendMessage(tab.id, { type: 'TOGGLE_SIDEBAR' });
        window.close();
      }
    });
  };

  const startPicker = () => {
    chrome.tabs.query({ active: true, currentWindow: true }, ([tab]) => {
      if (tab?.id) {
        chrome.tabs.sendMessage(tab.id, { type: 'TOGGLE_PICKER' });
        window.close();
      }
    });
  };

  const openOptions = () => {
    chrome.runtime.openOptionsPage();
  };

  return (
    <div
      className="w-80 bg-white text-slate-900 p-5 space-y-4 rounded-2xl shadow-xl border border-slate-100"
      style={{ fontFamily: "'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif" }}
    >
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <img
            src={chrome.runtime.getURL('assets/main_logo.png')}
            alt="ScripOx Logo"
            className="h-8 object-contain"
          />
        </div>

        {/* Market Badge */}
        <span className="text-[10px] font-semibold px-2 py-0.5 rounded-full bg-slate-100 border border-slate-200 text-slate-600">
          DE • UAE
        </span>
      </div>

      {/* Connection Status Pill */}
      <div
        className={`flex items-center gap-2 px-3 py-2 rounded-xl text-xs font-medium border ${
          status.connected
            ? 'bg-emerald-50 border-emerald-200 text-emerald-800'
            : 'bg-slate-50 border-slate-200 text-slate-700'
        }`}
      >
        <span
          className={`w-2 h-2 rounded-full flex-shrink-0 ${
            status.connected
              ? 'bg-emerald-500 shadow-[0_0_8px_#10b981] animate-pulse'
              : 'bg-amber-500'
          }`}
        />
        <span className="truncate">
          {loading
            ? 'Checking Desktop Link…'
            : status.connected
            ? `Desktop Connected ${status.version ? `(v${status.version})` : ''}`
            : 'Desktop Standby — Open ScripOx'}
        </span>
      </div>

      {/* Action Buttons */}
      <div className="space-y-2 pt-1">
        <button
          type="button"
          onClick={startPicker}
          className="w-full flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl
                     bg-blue-600 text-white font-medium text-xs hover:bg-blue-700
                     shadow-sm transition-colors cursor-pointer"
        >
          <Crosshair className="w-4 h-4" />
          <span>Start Visual Picker</span>
        </button>

        <button
          type="button"
          onClick={openSidebar}
          className="w-full flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl
                     bg-slate-900 text-white font-medium text-xs hover:bg-slate-800
                     shadow-sm transition-colors cursor-pointer"
        >
          <PanelRight className="w-4 h-4" />
          <span>Open Data Sidebar</span>
        </button>

        <button
          type="button"
          onClick={() => window.open('http://oxtech.uk/p/scripox', '_blank')}
          className="w-full flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl
                     bg-slate-50 border border-slate-200 text-slate-700 font-medium text-xs hover:bg-slate-100 hover:text-slate-900
                     transition-colors cursor-pointer"
        >
          <Download className="w-4 h-4" />
          <span>تحميل نسخة الديسكتوب</span>
        </button>

        <button
          type="button"
          onClick={openOptions}
          className="w-full flex items-center justify-center gap-2 px-4 py-2 rounded-xl
                     bg-white border border-slate-200 text-slate-600 font-medium text-xs
                     hover:bg-slate-50 hover:text-slate-900 transition-colors cursor-pointer"
        >
          <SettingsIcon className="w-4 h-4" />
          <span>Extension Settings</span>
        </button>
      </div>

      {/* Shortcuts */}
      <div className="border-t border-slate-100 pt-3 space-y-1.5">
        <p className="text-[10px] text-slate-400 font-semibold uppercase tracking-wider">
          Quick Shortcuts
        </p>
        <div className="flex justify-between items-center text-xs text-slate-600">
          <span>Element Picker</span>
          <kbd className="bg-slate-100 border border-slate-200 px-1.5 py-0.5 rounded text-[10px] font-mono text-slate-700">
            Alt+Shift+P
          </kbd>
        </div>
        <div className="flex justify-between items-center text-xs text-slate-600">
          <span>Toggle Sidebar</span>
          <kbd className="bg-slate-100 border border-slate-200 px-1.5 py-0.5 rounded text-[10px] font-mono text-slate-700">
            Alt+Shift+S
          </kbd>
        </div>
      </div>

      {/* Footer */}
      <div className="text-center text-[10px] text-slate-400 font-medium">
        Ox Tech UK • Engineered for German &amp; UAE Enterprises
      </div>
    </div>
  );
}

createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <Popup />
  </React.StrictMode>
);
