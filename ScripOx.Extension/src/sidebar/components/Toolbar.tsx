import React, { useState } from 'react';
import { useStore } from '../store/useStore';
import { sendExtraction } from '../../api/ScripOxClient';
import { GoogleMapsDetector } from '../../content/detectors/GoogleMapsDetector';
import { AutoCrawler } from '../../content/engines/AutoCrawler';
import {
  MousePointer, Table, Send, RotateCw, Sparkles, Wand2,
  Compass, Square, Download, Copy, Check, AlertCircle, RefreshCw
} from 'lucide-react';

export function Toolbar() {
  const {
    theme, mode, setMode, apiStatus, isSending, setIsSending, setSendResult,
    lastSendResult, selectors, records, setRecords, mapping, clearSelection,
    cleanCurrentRecords, isCrawling, setIsCrawling, crawlProgress, setCrawlProgress,
  } = useStore();

  const [showConfirm, setShowConfirm] = useState(false);
  const [showFallback, setShowFallback] = useState(false);
  const [copiedFallback, setCopiedFallback] = useState(false);
  const isMaps = GoogleMapsDetector.isGoogleMapsPage() || location.href.includes('maps');
  const isDark = theme === 'dark';

  const handleStartPick = () => {
    window.dispatchEvent(new CustomEvent('scripox:start-pick'));
  };

  const handleStartTable = () => {
    window.dispatchEvent(new CustomEvent('scripox:start-table'));
  };

  const handleSmartExtract = () => {
    if (isMaps) {
      const leads = GoogleMapsDetector.extractGoogleMapsLeads();
      if (leads.length > 0) {
        setRecords(leads);
        setSendResult({ ok: true, message: `استخراج ${leads.length} سجل بنجاح` });
        return;
      }
    }
    window.dispatchEvent(new CustomEvent('scripox:detect-cards'));
  };

  // ── Auto-Scroll & Deep Crawling ─────────────────────────────
  const toggleAutoCrawl = () => {
    if (isCrawling) {
      AutoCrawler.stop();
      setIsCrawling(false);
      setSendResult({ ok: true, message: `تم إيقاف التمرير. إجمالي السجلات: ${records.length}` });
    } else {
      setIsCrawling(true);
      setCrawlProgress({ scrollCount: 0, leadCount: records.length });
      setSendResult({ ok: true, message: 'بدأ التمرير والزحف التلقائي للصفحات...' });

      AutoCrawler.start(
        (leads, scrollCount) => {
          setRecords(leads);
          setCrawlProgress({ scrollCount, leadCount: leads.length });
        },
        (totalLeads) => {
          setIsCrawling(false);
          setSendResult({ ok: true, message: `اكتمل الزحف: ${totalLeads} سجل` });
        },
        30 // up to 30 scrolls
      );
    }
  };

  const handleCleanData = () => {
    if (records.length === 0) {
      setSendResult({ ok: false, message: 'لا توجد بيانات لتنظيفها' });
      return;
    }
    const before = records.length;
    cleanCurrentRecords();
    const after = useStore.getState().records.length;
    setSendResult({
      ok: true,
      message: `تم تنظيف وتصفية البيانات: ${before} ← ${after} سجل`,
    });
  };

  // ── Send To Desktop with Fallback ───────────────────────────
  const handleSendToScripOx = async () => {
    if (records.length === 0) return;
    setShowConfirm(false);
    setIsSending(true);
    setSendResult(null);

    try {
      await sendExtraction({
        url:      location.href,
        title:    document.title,
        selector: selectors?.bestSelector || 'scripox-smart-lead-feed',
        xpath:    selectors?.xpath || '//div',
        mapping:  mapping || {},
        records,
      });
      setSendResult({ ok: true, message: `تم إرسال ${records.length} سجل للديسكتوب بنجاح ✓` });
      setShowFallback(false);
    } catch (err) {
      // Backend not running on port 8765 -> offer instant Desktop File Export
      setShowFallback(true);
      setSendResult({
        ok: false,
        message: 'برنامج ScripOx Desktop غير مفتوح على المنفذ (8765). يمكنك حفظ الملف للديسكتوب فوراً أدناه.',
      });
    } finally {
      setIsSending(false);
    }
  };

  const handleSaveDesktopFile = () => {
    const payload = {
      scripox_version: '1.0',
      exported_at: new Date().toISOString(),
      url: location.href,
      title: document.title,
      records_count: records.length,
      records: records,
    };
    const content = JSON.stringify(payload, null, 2);
    const blob = new Blob([content], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `scripox_desktop_${Date.now()}.json`;
    a.click();
    URL.revokeObjectURL(url);
    setShowFallback(false);
  };

  const handleCopyDesktopJSON = async () => {
    const payload = {
      url: location.href,
      records,
    };
    await navigator.clipboard.writeText(JSON.stringify(payload, null, 2));
    setCopiedFallback(true);
    setTimeout(() => setCopiedFallback(false), 2000);
  };

  return (
    <div className={`px-3.5 py-2.5 space-y-2.5 border-b transition-colors ${
      isDark ? 'border-slate-800 bg-[#0F172A]' : 'border-slate-200 bg-white'
    }`}>
      {/* Status Bar Pill - Clean, Calm, Subtle */}
      <div className="flex items-center justify-between text-xs">
        <div className={`flex items-center gap-1.5 px-2.5 py-1 rounded-full border text-[11px] font-medium ${
          isDark
            ? 'bg-slate-850 border-slate-700/80 text-slate-300'
            : 'bg-slate-50 border-slate-200 text-slate-600 shadow-2xs'
        }`}>
          <span
            className={`w-2 h-2 rounded-full ${
              apiStatus.connected ? 'bg-emerald-500 shadow-[0_0_6px_#10b981]' : 'bg-slate-400'
            }`}
          />
          <span>{apiStatus.connected ? 'Desktop Linked' : 'Desktop Standby'}</span>
        </div>

        {isMaps && (
          <span className={`text-[10px] px-2 py-0.5 rounded-full font-semibold border ${
            isDark
              ? 'bg-slate-800 text-slate-300 border-slate-700'
              : 'bg-slate-100 text-slate-700 border-slate-200'
          }`}>
            Google Maps
          </span>
        )}
      </div>

      {/* Primary Actions Grid (Calm, Professional) */}
      <div className="grid grid-cols-2 gap-2">
        {/* Smart Extraction */}
        <button
          onClick={handleSmartExtract}
          className={`flex items-center justify-center gap-2 px-3 py-2 rounded-xl text-xs font-semibold border transition-all cursor-pointer shadow-2xs ${
            isDark
              ? 'bg-slate-800 hover:bg-slate-700 text-white border-slate-700'
              : 'bg-slate-900 hover:bg-slate-800 text-white border-slate-900'
          }`}
          title="Smart 1-Click extraction"
        >
          <Sparkles className="w-3.5 h-3.5 text-amber-400" />
          <span>{isMaps ? 'استخراج الخرائط' : 'استخراج ذكي'}</span>
        </button>

        {/* Auto-Scroll & Deep Crawl (سكرولينج وزحف الصفحات) */}
        <button
          onClick={toggleAutoCrawl}
          className={`flex items-center justify-center gap-2 px-3 py-2 rounded-xl text-xs font-semibold border transition-all cursor-pointer shadow-2xs ${
            isCrawling
              ? 'bg-amber-500 text-slate-950 border-amber-600 animate-pulse font-bold'
              : isDark
                ? 'bg-slate-800 hover:bg-slate-700 text-slate-200 border-slate-700'
                : 'bg-white hover:bg-slate-50 text-slate-800 border-slate-200'
          }`}
          title="تمرير تلقائي في الصفحة لجلب دفعات جديدة من البيانات"
        >
          {isCrawling ? (
            <>
              <Square className="w-3 h-3 fill-current" />
              <span>إيقاف الزحف ({crawlProgress.scrollCount})</span>
            </>
          ) : (
            <>
              <Compass className="w-3.5 h-3.5 text-blue-500" />
              <span>زحف وتمرير (Crawl)</span>
            </>
          )}
        </button>

        {/* Manual Pick Element */}
        <button
          onClick={handleStartPick}
          disabled={mode === 'picking'}
          className={`flex items-center justify-center gap-2 px-3 py-1.5 rounded-xl text-xs font-medium border transition-all cursor-pointer disabled:opacity-50 ${
            mode === 'picking'
              ? 'bg-blue-50 border-blue-400 text-blue-600 font-semibold'
              : isDark
                ? 'bg-slate-800/80 border-slate-700 text-slate-300 hover:bg-slate-800'
                : 'bg-white border-slate-200 text-slate-700 hover:bg-slate-50'
          }`}
        >
          <MousePointer className="w-3 h-3 text-blue-500" />
          <span>{mode === 'picking' ? 'جاري التحديد...' : 'تحديد عنصر'}</span>
        </button>

        {/* Clean Data */}
        <button
          onClick={handleCleanData}
          disabled={records.length === 0}
          className={`flex items-center justify-center gap-2 px-3 py-1.5 rounded-xl text-xs font-medium border transition-all cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed ${
            isDark
              ? 'bg-slate-800/80 border-slate-700 text-slate-300 hover:bg-slate-800'
              : 'bg-white border-slate-200 text-slate-700 hover:bg-slate-50'
          }`}
          title="Sanitize phones & deduplicate records"
        >
          <Wand2 className="w-3 h-3 text-emerald-500" />
          <span>تنظيف وتصفية</span>
        </button>
      </div>

      {/* Primary Dispatch to Desktop Button (Always Responsive) */}
      <button
        onClick={handleSendToScripOx}
        disabled={records.length === 0 || isSending}
        className={`w-full py-2 rounded-xl font-semibold text-xs transition-all flex items-center justify-center gap-2 cursor-pointer shadow-xs ${
          records.length === 0
            ? 'opacity-40 cursor-not-allowed bg-slate-100 dark:bg-slate-800 text-slate-400 border border-slate-200 dark:border-slate-700'
            : isDark
              ? 'bg-[#1E293B] hover:bg-slate-700 text-white border border-slate-600'
              : 'bg-[#0F172A] hover:bg-slate-800 text-white border border-slate-900'
        }`}
      >
        {isSending ? (
          <>
            <RotateCw className="w-3.5 h-3.5 animate-spin" />
            <span>جاري الإرسال للديسكتوب...</span>
          </>
        ) : (
          <>
            <Send className="w-3.5 h-3.5 text-emerald-400" />
            <span>إرسال {records.length > 0 ? `(${records.length}) سجل` : ''} إلى ScripOx Desktop</span>
          </>
        )}
      </button>

      {/* Fallback Option if Desktop Bridge isn't currently open */}
      {showFallback && (
        <div className={`p-3 rounded-xl border text-xs space-y-2 transition-all ${
          isDark ? 'bg-slate-900 border-slate-700 text-slate-300' : 'bg-slate-50 border-slate-200 text-slate-700'
        }`}>
          <div className="flex items-start gap-1.5">
            <AlertCircle className="w-4 h-4 text-amber-500 shrink-0 mt-0.5" />
            <p className="text-[11px] leading-relaxed">
              تعذر الاتصال المباشر بمنفذ الديسكتوب (8765). يمكنك حفظ السجلات فوراً كملف واستيراده في برنامج ScripOx Desktop:
            </p>
          </div>
          <div className="flex gap-2">
            <button
              onClick={handleSaveDesktopFile}
              className="flex-1 flex items-center justify-center gap-1.5 py-1.5 bg-[#0F172A] text-white rounded-lg text-xs font-semibold cursor-pointer hover:bg-slate-800 shadow-xs"
            >
              <Download className="w-3 h-3" />
              <span>تحميل ملف ديسكتوب (.json)</span>
            </button>
            <button
              onClick={handleCopyDesktopJSON}
              className={`px-2.5 py-1.5 rounded-lg border text-xs font-medium cursor-pointer transition-colors ${
                isDark ? 'bg-slate-800 border-slate-700 text-slate-200' : 'bg-white border-slate-200 text-slate-700'
              }`}
            >
              {copiedFallback ? <Check className="w-3.5 h-3.5 text-emerald-500" /> : <Copy className="w-3.5 h-3.5" />}
            </button>
          </div>
        </div>
      )}

      {/* Result Indicator Toast */}
      {lastSendResult && (
        <div
          className={`text-xs px-3 py-1.5 rounded-xl border font-medium ${
            lastSendResult.ok
              ? isDark
                ? 'bg-slate-850 border-emerald-900/60 text-emerald-300'
                : 'bg-emerald-50 border-emerald-200 text-emerald-800'
              : isDark
                ? 'bg-slate-850 border-amber-900/60 text-amber-300'
                : 'bg-amber-50 border-amber-200 text-amber-800'
          }`}
        >
          {lastSendResult.message}
        </div>
      )}

      {/* Clear Action */}
      {(selectors || records.length > 0) && (
        <button
          onClick={clearSelection}
          className={`w-full py-0.5 text-[11px] transition-colors rounded-lg cursor-pointer text-center ${
            isDark ? 'text-slate-500 hover:text-slate-300' : 'text-slate-400 hover:text-slate-600'
          }`}
        >
          إعادة ضبط التحديد والسجلات
        </button>
      )}
    </div>
  );
}
