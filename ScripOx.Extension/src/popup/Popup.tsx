import React, { useEffect, useState } from 'react';
import { createRoot } from 'react-dom/client';
import '../index.css';
import {
  Crosshair,
  PanelRight,
  Download,
  Settings as SettingsIcon,
  Globe,
  Check,
  Sparkles
} from 'lucide-react';
import { translations, type Language } from '../i18n/translations';

interface StatusData {
  connected: boolean;
  version?: string;
}

const LANGUAGES: { code: Language; label: string; flag: string }[] = [
  { code: 'en', label: 'English', flag: '🇬🇧' },
  { code: 'de', label: 'Deutsch', flag: '🇩🇪' },
  { code: 'ar', label: 'العربية', flag: '🇦🇪' },
];

function Popup() {
  const [lang, setLang] = useState<Language>('en');
  const [hasSelectedLanguage, setHasSelectedLanguage] = useState<boolean>(true);
  const [status, setStatus] = useState<StatusData>({ connected: false });
  const [loading, setLoading] = useState(true);
  const [showLangMenu, setShowLangMenu] = useState(false);

  useEffect(() => {
    // 1. Load saved language preference or initialize
    chrome.storage.local.get(['userLanguage', 'hasSelectedLanguage'], (result) => {
      if (result.userLanguage && (result.userLanguage in translations)) {
        setLang(result.userLanguage as Language);
      } else {
        // Default to English as requested
        setLang('en');
      }

      if (result.hasSelectedLanguage === false || result.hasSelectedLanguage === undefined) {
        setHasSelectedLanguage(false);
      } else {
        setHasSelectedLanguage(true);
      }
    });

    // 2. Query Desktop API status
    chrome.runtime.sendMessage({ type: 'GET_API_STATUS' }, (res) => {
      setStatus(res?.payload || { connected: false });
      setLoading(false);
    });
  }, []);

  const handleSelectLanguage = (selectedLang: Language) => {
    setLang(selectedLang);
    setHasSelectedLanguage(true);
    setShowLangMenu(false);
    chrome.storage.local.set({
      userLanguage: selectedLang,
      hasSelectedLanguage: true,
    });
  };

  const t = translations[lang] || translations.en;
  const isRtl = lang === 'ar';

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
      dir={isRtl ? 'rtl' : 'ltr'}
      className="w-84 bg-white text-slate-900 p-5 space-y-4 rounded-2xl shadow-xl border border-slate-100 select-none"
      style={{
        fontFamily: isRtl
          ? "'Cairo', 'Segoe UI', Tahoma, sans-serif"
          : "'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif",
      }}
    >
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2.5">
          <img
            src={chrome.runtime.getURL('assets/main_logo.png')}
            alt="ScripOx Logo"
            className="h-8 object-contain"
          />
          <div>
            <h1 className="text-sm font-bold tracking-tight text-slate-900 leading-tight">
              ScripOx
            </h1>
            <p className="text-[10px] text-slate-400 font-medium leading-none">
              Visual Extractor
            </p>
          </div>
        </div>

        {/* Language Switcher Badge / Button */}
        <div className="relative">
          <button
            type="button"
            onClick={() => setShowLangMenu(!showLangMenu)}
            className="flex items-center gap-1 px-2.5 py-1 rounded-full bg-slate-50 border border-slate-200 text-slate-700 text-xs font-semibold hover:bg-slate-100 transition-colors cursor-pointer"
            title={t.selectLanguage}
          >
            <Globe className="w-3.5 h-3.5 text-blue-600" />
            <span>{LANGUAGES.find((l) => l.code === lang)?.flag}</span>
            <span className="uppercase text-[11px]">{lang}</span>
          </button>

          {/* Language Dropdown Menu */}
          {showLangMenu && (
            <div
              className={`absolute top-full mt-1.5 ${
                isRtl ? 'left-0' : 'right-0'
              } w-36 bg-white rounded-xl shadow-xl border border-slate-100 py-1.5 z-50`}
            >
              <div className="px-3 py-1 text-[10px] font-semibold text-slate-400 uppercase tracking-wider">
                {t.selectLanguage}
              </div>
              {LANGUAGES.map((item) => (
                <button
                  key={item.code}
                  type="button"
                  onClick={() => handleSelectLanguage(item.code)}
                  className={`w-full flex items-center justify-between px-3 py-1.5 text-xs text-left cursor-pointer transition-colors ${
                    lang === item.code
                      ? 'bg-blue-50 text-blue-700 font-semibold'
                      : 'text-slate-700 hover:bg-slate-50'
                  }`}
                >
                  <span className="flex items-center gap-2">
                    <span>{item.flag}</span>
                    <span>{item.label}</span>
                  </span>
                  {lang === item.code && <Check className="w-3.5 h-3.5 text-blue-600" />}
                </button>
              ))}
            </div>
          )}
        </div>
      </div>

      {/* First-time Onboarding Language Card (Shown right after installation) */}
      {!hasSelectedLanguage && (
        <div className="bg-gradient-to-br from-blue-50 to-indigo-50 border border-blue-200/80 rounded-2xl p-3.5 space-y-2.5">
          <div className="flex items-center gap-1.5 text-blue-900 font-semibold text-xs">
            <Sparkles className="w-3.5 h-3.5 text-blue-600 flex-shrink-0" />
            <span>{t.welcomeTitle}</span>
          </div>
          <p className="text-[11px] text-blue-800/80 leading-relaxed">
            {t.welcomeDesc}
          </p>
          <div className="grid grid-cols-3 gap-1.5 pt-1">
            {LANGUAGES.map((item) => (
              <button
                key={item.code}
                type="button"
                onClick={() => handleSelectLanguage(item.code)}
                className={`flex flex-col items-center justify-center p-2 rounded-xl text-xs font-medium border transition-all cursor-pointer ${
                  lang === item.code
                    ? 'bg-blue-600 text-white border-blue-600 shadow-sm'
                    : 'bg-white text-slate-700 border-slate-200 hover:border-blue-300'
                }`}
              >
                <span className="text-base">{item.flag}</span>
                <span className="text-[11px] mt-0.5">{item.label}</span>
              </button>
            ))}
          </div>
        </div>
      )}

      {/* Desktop Connection Status Pill */}
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
            ? t.statusChecking
            : status.connected
            ? `${t.statusConnected} ${status.version ? `(v${status.version})` : ''}`
            : t.statusDisconnected}
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
          <span>{t.startPicker}</span>
        </button>

        <button
          type="button"
          onClick={openSidebar}
          className="w-full flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl
                     bg-slate-900 text-white font-medium text-xs hover:bg-slate-800
                     shadow-sm transition-colors cursor-pointer"
        >
          <PanelRight className="w-4 h-4" />
          <span>{t.openSidebar}</span>
        </button>

        <button
          type="button"
          onClick={() => window.open('https://oxtech.uk', '_blank')}
          className="w-full flex items-center justify-center gap-2 px-4 py-2.5 rounded-xl
                     bg-slate-50 border border-slate-200 text-slate-700 font-medium text-xs hover:bg-slate-100 hover:text-slate-900
                     transition-colors cursor-pointer"
        >
          <Download className="w-4 h-4" />
          <span>{t.downloadDesktop}</span>
        </button>

        <button
          type="button"
          onClick={openOptions}
          className="w-full flex items-center justify-center gap-2 px-4 py-2 rounded-xl
                     bg-white border border-slate-200 text-slate-600 font-medium text-xs
                     hover:bg-slate-50 hover:text-slate-900 transition-colors cursor-pointer"
        >
          <SettingsIcon className="w-4 h-4" />
          <span>{t.settings}</span>
        </button>
      </div>

      {/* Shortcuts */}
      <div className="border-t border-slate-100 pt-3 space-y-1.5">
        <p className="text-[10px] text-slate-400 font-semibold uppercase tracking-wider">
          {t.shortcutsTitle}
        </p>
        <div className="flex justify-between items-center text-xs text-slate-600">
          <span>{t.shortcutPicker}</span>
          <kbd className="bg-slate-100 border border-slate-200 px-1.5 py-0.5 rounded text-[10px] font-mono text-slate-700">
            Alt+Shift+P
          </kbd>
        </div>
        <div className="flex justify-between items-center text-xs text-slate-600">
          <span>{t.shortcutSidebar}</span>
          <kbd className="bg-slate-100 border border-slate-200 px-1.5 py-0.5 rounded text-[10px] font-mono text-slate-700">
            Alt+Shift+S
          </kbd>
        </div>
      </div>

      {/* Footer */}
      <div className="text-center text-[10px] text-slate-400 font-medium">
        {t.footerTagline}
      </div>
    </div>
  );
}

createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <Popup />
  </React.StrictMode>
);
