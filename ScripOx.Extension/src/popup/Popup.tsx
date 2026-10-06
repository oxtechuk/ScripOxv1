import React, { useEffect, useState } from 'react';
import { createRoot } from 'react-dom/client';
import '../index.css';

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
      className="w-80 bg-[#FAFAFA] text-[#0F172A] p-5 space-y-4 rounded-2xl shadow-xl border border-slate-200"
      style={{ fontFamily: "'Inter', -apple-system, sans-serif" }}
    >
      {/* Header (Hudson 8 & CRMEdge Style) */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2.5">
          <div className="w-8 h-8 rounded-lg bg-[#0F172A] flex items-center justify-center relative">
            <span className="text-white font-bold text-sm">S</span>
            <span className="w-2.5 h-2.5 rounded-full bg-[#CEF22C] absolute top-1 right-1" />
          </div>
          <div>
            <h1 className="font-bold text-sm tracking-tight text-[#0F172A]">
              ScripOx Visual Hub
            </h1>
            <p className="text-[11px] text-slate-500 font-medium">Enterprise Data Extractor</p>
          </div>
        </div>

        {/* Market Badge */}
        <span className="text-[10px] font-bold px-2 py-0.5 rounded-full bg-slate-100 border border-slate-200 text-slate-600">
          DE • UAE
        </span>
      </div>

      {/* Connection Status Pill (CRMEdge Style) */}
      <div
        className={`flex items-center gap-2 px-3 py-2 rounded-xl text-xs font-medium border ${
          status.connected
            ? 'bg-emerald-50 border-emerald-200 text-emerald-800'
            : 'bg-amber-50 border-amber-200 text-amber-800'
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

      {/* Action Buttons (CRMEdge Style) */}
      <div className="space-y-2 pt-1">
        <button
          onClick={startPicker}
          className="w-full flex items-center justify-center gap-2 px-4 py-2.5 rounded-full
                     bg-[#CEF22C] text-[#0F172A] font-bold text-xs hover:bg-[#bde01e]
                     shadow-sm transition-all duration-150 cursor-pointer"
        >
          <span>🎯</span> Start Visual Picker
        </button>

        <button
          onClick={openSidebar}
          className="w-full flex items-center justify-center gap-2 px-4 py-2.5 rounded-full
                     bg-[#0F172A] text-white font-semibold text-xs hover:bg-slate-800
                     shadow-sm transition-all duration-150 cursor-pointer"
        >
          <span>📋</span> Open Data Sidebar
        </button>

        <button
          onClick={openOptions}
          className="w-full flex items-center justify-center gap-2 px-4 py-2 rounded-full
                     bg-white border border-slate-200 text-slate-600 font-medium text-xs
                     hover:bg-slate-50 hover:text-slate-900 transition-all duration-150 cursor-pointer"
        >
          <span>⚙️</span> Extension Settings
        </button>
      </div>

      {/* Shortcuts */}
      <div className="border-t border-slate-200 pt-3 space-y-1.5">
        <p className="text-[10px] text-slate-500 font-bold uppercase tracking-wider">
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
