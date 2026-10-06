import React, { useState, useMemo } from 'react';
import { useStore } from '../store/useStore';
import { sendExtraction } from '../../api/ScripOxClient';
import { AutoCrawler } from '../../content/engines/AutoCrawler';
import type { ExtractedRecord } from '../../types';
import {
  Table, Search, X, Wand2, Download, Send, Trash2,
  CheckSquare, Square, Eye, EyeOff, Check, Copy,
  RotateCw, AlertCircle, Layers, Compass
} from 'lucide-react';

function exportRecordsToExcelCSV(records: ExtractedRecord[], filename = 'scripox_leads') {
  if (!records.length) return;
  const fields = [...new Set(records.flatMap((r) => Object.keys(r)))];
  const header = fields.map((f) => `"${f.replace(/"/g, '""')}"`).join(',');
  const rows = records.map((r) =>
    fields.map((f) => {
      const v = String(r[f] ?? '').replace(/"/g, '""');
      return `"${v}"`;
    }).join(',')
  );
  // Prepend UTF-8 BOM (\uFEFF) so Excel displays Arabic & Unicode correctly
  const csvContent = '\uFEFF' + [header, ...rows].join('\r\n');
  const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = `${filename}_${Date.now()}.csv`;
  a.click();
  URL.revokeObjectURL(url);
}

export function DataPreview() {
  const {
    theme, records, setRecords, updateRecord, removeRecord, clearRecords,
    selectors, cleanCurrentRecords, apiStatus,
    hiddenColumns, toggleColumnVisibility, setHiddenColumns,
    selectedRowIndices, toggleRowSelection, selectAllRows,
    clearRowSelection, deleteSelectedRecords,
    isCrawling, setIsCrawling, crawlProgress, setCrawlProgress,
  } = useStore();

  const isDark = theme === 'dark';
  const [searchTerm, setSearchTerm] = useState('');
  const [showColPicker, setShowColPicker] = useState(false);
  const [showClearConfirm, setShowClearConfirm] = useState(false);
  const [copiedCell, setCopiedCell] = useState<string | null>(null);
  const [actionNotice, setActionNotice] = useState<string | null>(null);
  const [isSendingToDesktop, setIsSendingToDesktop] = useState(false);
  const [desktopFallbackNotice, setDesktopFallbackNotice] = useState(false);

  // All detected fields/columns across all records
  const allFields = useMemo(() => {
    return [...new Set(records.flatMap((r) => Object.keys(r)))];
  }, [records]);

  // Visible columns (all columns excluding hiddenColumns)
  const visibleFields = useMemo(() => {
    return allFields.filter((col) => !hiddenColumns.includes(col));
  }, [allFields, hiddenColumns]);

  // Filtered records based on live search
  const filteredRecordsWithIndex = useMemo(() => {
    if (!searchTerm.trim()) {
      return records.map((record, origIndex) => ({ record, origIndex }));
    }
    const query = searchTerm.toLowerCase();
    return records
      .map((record, origIndex) => ({ record, origIndex }))
      .filter(({ record }) => {
        return Object.values(record).some((val) =>
          String(val ?? '').toLowerCase().includes(query)
        );
      });
  }, [records, searchTerm]);

  const handleCopyCell = (val: string, key: string) => {
    if (!val) return;
    navigator.clipboard.writeText(val);
    setCopiedCell(key);
    setTimeout(() => setCopiedCell(null), 1500);
  };

  const notify = (msg: string) => {
    setActionNotice(msg);
    setTimeout(() => setActionNotice(null), 3500);
  };

  const handleClean = () => {
    const before = records.length;
    cleanCurrentRecords();
    const after = useStore.getState().records.length;
    notify(`تم تنظيف وتصفية البيانات: ${before} ← ${after} سجل`);
  };

  const handleExportAll = () => {
    const dataToExport = filteredRecordsWithIndex.map((item) => item.record);
    exportRecordsToExcelCSV(dataToExport, 'scripox_leads');
    notify(`تم تصدير ${dataToExport.length} سجل إلى ملف Excel`);
  };

  const handleExportSelected = () => {
    const dataToExport = records.filter((_, idx) => selectedRowIndices.includes(idx));
    exportRecordsToExcelCSV(dataToExport, 'scripox_selected_leads');
    notify(`تم تصدير ${dataToExport.length} سجل محدد إلى Excel`);
  };

  // ── Auto-Scroll & Deep Crawl ────────────────────────────────
  const toggleAutoCrawl = () => {
    if (isCrawling) {
      AutoCrawler.stop();
      setIsCrawling(false);
      notify(`تم إيقاف الزحف. إجمالي السجلات: ${records.length}`);
    } else {
      setIsCrawling(true);
      setCrawlProgress({ scrollCount: 0, leadCount: records.length });
      notify('بدأ التمرير والزحف التلقائي للصفحات...');

      AutoCrawler.start(
        (leads, scrollCount) => {
          setRecords(leads);
          setCrawlProgress({ scrollCount, leadCount: leads.length });
        },
        (totalLeads) => {
          setIsCrawling(false);
          notify(`اكتمل الزحف: ${totalLeads} سجل`);
        },
        30
      );
    }
  };

  // ── Send to Desktop with Intelligent Fallback ───────────────
  const handleSendToDesktop = async (onlySelected = false) => {
    const rowsToSend = onlySelected
      ? records.filter((_, idx) => selectedRowIndices.includes(idx))
      : records;

    if (rowsToSend.length === 0) return;
    setIsSendingToDesktop(true);

    try {
      await sendExtraction({
        url: location.href,
        title: document.title,
        selector: selectors?.bestSelector || 'scripox-grid-view',
        xpath: selectors?.xpath || '//div',
        mapping: {},
        records: rowsToSend,
      });
      notify(`تم إرسال ${rowsToSend.length} سجل إلى ScripOx Desktop بنجاح ✓`);
      setDesktopFallbackNotice(false);
    } catch (err) {
      // Backend not running on port 8765 -> directly trigger desktop JSON export
      setDesktopFallbackNotice(true);
      notify('الديسكتوب غير متصل على المنفذ (8765). تم تجهيز خيار حفظ الملف.');
    } finally {
      setIsSendingToDesktop(false);
    }
  };

  const handleDownloadDesktopFile = () => {
    const payload = {
      scripox_version: '1.0',
      exported_at: new Date().toISOString(),
      url: location.href,
      title: document.title,
      records: records,
    };
    const blob = new Blob([JSON.stringify(payload, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `scripox_desktop_${Date.now()}.json`;
    a.click();
    URL.revokeObjectURL(url);
    setDesktopFallbackNotice(false);
    notify('تم تنزيل ملف الديسكتوب (.json) بنجاح');
  };

  if (!selectors && records.length === 0) {
    return (
      <div className="p-8 text-center flex flex-col items-center justify-center font-sans" dir="ltr">
        <div className={`w-12 h-12 rounded-2xl flex items-center justify-center mb-3 border ${
          isDark ? 'bg-slate-800/80 border-slate-700/80 text-slate-400' : 'bg-white border-slate-200 text-slate-600 shadow-2xs'
        }`}>
          <Table className="w-5 h-5 text-slate-400" />
        </div>
        <p className={`text-xs font-semibold ${isDark ? 'text-slate-200' : 'text-slate-800'}`}>Excel Live Data Grid</p>
        <p className={`text-[11px] mt-1 ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>
          Extracted records stream directly here in an interactive spreadsheet.
        </p>
      </div>
    );
  }

  if (records.length === 0) {
    return (
      <div className="p-8 text-center flex flex-col items-center justify-center font-sans" dir="ltr">
        <div className={`w-12 h-12 rounded-2xl flex items-center justify-center mb-3 border ${
          isDark ? 'bg-slate-800/80 border-slate-700/80 text-slate-400' : 'bg-white border-slate-200 text-slate-600 shadow-2xs'
        }`}>
          <Table className="w-5 h-5 text-slate-400" />
        </div>
        <p className={`text-xs font-semibold ${isDark ? 'text-slate-200' : 'text-slate-800'}`}>No Records Extracted</p>
        <p className={`text-[11px] mt-1 ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>
          Click Smart Auto-Scrape above or pick elements to extract leads.
        </p>
      </div>
    );
  }

  const isAllSelected = records.length > 0 && selectedRowIndices.length === records.length;
  const isPartiallySelected = selectedRowIndices.length > 0 && selectedRowIndices.length < records.length;

  return (
    <div className="p-3 space-y-2.5 font-sans" dir="ltr" style={{ direction: 'ltr', textAlign: 'left' }}>
      {/* ── Top Bar: Search, Columns, Calm Actions ── */}
      <div className="space-y-2">
        {/* Search & Columns row */}
        <div className="flex items-center gap-1.5">
          {/* Live Search Input */}
          <div className="relative flex-1">
            <Search className="w-3.5 h-3.5 absolute left-2.5 top-1/2 -translate-y-1/2 text-slate-400" />
            <input
              type="text"
              placeholder="Search table..."
              value={searchTerm}
              onChange={(e) => setSearchTerm(e.target.value)}
              className={`w-full pl-8 pr-7 py-1 text-xs rounded-xl border outline-none transition-all ${
                isDark
                  ? 'bg-slate-900 border-slate-700 text-slate-100 placeholder-slate-500 focus:border-slate-500'
                  : 'bg-white border-slate-200 text-slate-900 placeholder-slate-400 focus:border-slate-400 shadow-2xs'
              }`}
            />
            {searchTerm && (
              <button
                onClick={() => setSearchTerm('')}
                className="absolute right-2 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600 cursor-pointer"
              >
                <X className="w-3 h-3" />
              </button>
            )}
          </div>

          {/* Columns Visibility Selector Dropdown Button */}
          <div className="relative">
            <button
              onClick={() => setShowColPicker(!showColPicker)}
              className={`flex items-center gap-1.5 px-2.5 py-1 rounded-xl text-xs font-medium border cursor-pointer transition-all shadow-2xs ${
                hiddenColumns.length > 0
                  ? 'bg-slate-100 text-slate-800 border-slate-300'
                  : isDark
                    ? 'bg-slate-900 border-slate-700 text-slate-300 hover:bg-slate-800'
                    : 'bg-white border-slate-200 text-slate-700 hover:bg-slate-50'
              }`}
              title="Hide / Show columns"
            >
              <Layers className="w-3 h-3 text-slate-400" />
              <span>Cols ({visibleFields.length}/{allFields.length})</span>
            </button>

            {/* Column Visibility Popover */}
            {showColPicker && (
              <div className={`absolute right-0 top-8 z-50 w-52 p-2 rounded-2xl border shadow-xl ${
                isDark
                  ? 'bg-slate-900 border-slate-700 text-slate-200'
                  : 'bg-white border-slate-200 text-slate-800'
              }`}>
                <div className="flex items-center justify-between pb-1.5 mb-1.5 border-b border-slate-200 dark:border-slate-800 text-[11px] font-semibold">
                  <span>Columns Visibility</span>
                  <div className="flex gap-2">
                    <button
                      onClick={() => setHiddenColumns([])}
                      className="text-blue-500 hover:underline cursor-pointer text-[10px]"
                    >
                      Show All
                    </button>
                    <button
                      onClick={() => setShowColPicker(false)}
                      className="text-slate-400 hover:text-slate-600 cursor-pointer"
                    >
                      <X className="w-3 h-3" />
                    </button>
                  </div>
                </div>
                <div className="max-h-48 overflow-y-auto space-y-0.5 custom-scrollbar">
                  {allFields.map((field) => {
                    const isVisible = !hiddenColumns.includes(field);
                    return (
                      <button
                        key={field}
                        onClick={() => toggleColumnVisibility(field)}
                        className={`w-full flex items-center justify-between px-2 py-1 rounded-lg text-xs cursor-pointer text-left transition-colors ${
                          isVisible
                            ? isDark ? 'bg-slate-800 text-white' : 'bg-slate-100 text-slate-900'
                            : 'text-slate-400 hover:bg-slate-50 line-through opacity-60'
                        }`}
                      >
                        <span className="truncate max-w-[130px] font-mono text-[11px]">{field}</span>
                        {isVisible ? <Eye className="w-3 h-3 text-slate-500" /> : <EyeOff className="w-3 h-3 text-slate-400" />}
                      </button>
                    );
                  })}
                </div>
              </div>
            )}
          </div>
        </div>

        {/* Calm Action Bar (Export Excel, Desktop, Crawl, Clean, Clear Table) */}
        <div className="flex items-center justify-between gap-1.5 flex-wrap">
          <div className="flex items-center gap-1.5">
            {/* Direct Export to Excel */}
            <button
              onClick={handleExportAll}
              className={`flex items-center gap-1.5 px-2.5 py-1 rounded-xl text-xs font-semibold border cursor-pointer transition-all shadow-2xs ${
                isDark
                  ? 'bg-slate-800 border-slate-700 text-slate-200 hover:bg-slate-750'
                  : 'bg-white border-slate-200 text-slate-700 hover:bg-slate-50'
              }`}
              title="Download Excel file (.csv with UTF-8 BOM)"
            >
              <Download className="w-3 h-3 text-emerald-500" />
              <span>Export Excel</span>
            </button>

            {/* Send to Desktop Core (Always Responsive) */}
            <button
              onClick={() => handleSendToDesktop(false)}
              disabled={isSendingToDesktop}
              className={`flex items-center gap-1.5 px-2.5 py-1 rounded-xl text-xs font-semibold border transition-all shadow-2xs cursor-pointer ${
                isDark
                  ? 'bg-slate-800 border-slate-700 text-slate-200 hover:bg-slate-700'
                  : 'bg-slate-900 border-slate-900 text-white hover:bg-slate-800'
              }`}
              title="Send to ScripOx Desktop application"
            >
              {isSendingToDesktop ? <RotateCw className="w-3 h-3 animate-spin" /> : <Send className="w-3 h-3 text-emerald-400" />}
              <span>To Desktop</span>
            </button>

            {/* Auto-Scroll & Deep Crawl (سكرولينج وزحف) */}
            <button
              onClick={toggleAutoCrawl}
              className={`flex items-center gap-1 px-2.5 py-1 rounded-xl text-xs font-semibold border cursor-pointer transition-all shadow-2xs ${
                isCrawling
                  ? 'bg-amber-500 text-slate-950 border-amber-600 font-bold'
                  : isDark
                    ? 'bg-slate-800 border-slate-700 text-slate-200 hover:bg-slate-750'
                    : 'bg-white border-slate-200 text-slate-700 hover:bg-slate-50'
              }`}
              title="Auto-scroll pages to load continuous records"
            >
              <Compass className="w-3 h-3 text-blue-500" />
              <span>{isCrawling ? `Stop (${crawlProgress.scrollCount})` : 'Crawl'}</span>
            </button>

            {/* Clean Data */}
            <button
              onClick={handleClean}
              className={`flex items-center gap-1 px-2 py-1 rounded-xl text-xs font-medium border cursor-pointer transition-all ${
                isDark
                  ? 'bg-slate-900 border-slate-800 text-slate-400 hover:bg-slate-800'
                  : 'bg-white border-slate-200 text-slate-600 hover:bg-slate-50'
              }`}
              title="Sanitize phones & deduplicate records"
            >
              <Wand2 className="w-3 h-3 text-slate-400" />
              <span>Clean</span>
            </button>
          </div>

          {/* Delete / Clear Active Table */}
          <div>
            {!showClearConfirm ? (
              <button
                onClick={() => setShowClearConfirm(true)}
                className={`flex items-center gap-1 px-2 py-1 rounded-xl text-xs font-medium transition-all cursor-pointer ${
                  isDark ? 'text-slate-400 hover:text-red-400' : 'text-slate-500 hover:text-red-600'
                }`}
                title="Clear current table"
              >
                <Trash2 className="w-3 h-3" />
                <span>حذف الجدول</span>
              </button>
            ) : (
              <div className="flex items-center gap-1 bg-slate-100 dark:bg-slate-800 p-0.5 px-1.5 rounded-xl border border-slate-200 dark:border-slate-700">
                <span className="text-[10px] text-slate-600 dark:text-slate-300 font-medium">حذف؟</span>
                <button
                  onClick={() => {
                    clearRecords();
                    setShowClearConfirm(false);
                    notify('تم مسح الجدول بنجاح');
                  }}
                  className="px-1.5 py-0.5 bg-red-600 text-white rounded text-[10px] font-bold cursor-pointer"
                >
                  نعم
                </button>
                <button
                  onClick={() => setShowClearConfirm(false)}
                  className="px-1 text-slate-500 text-[10px] cursor-pointer"
                >
                  إلغاء
                </button>
              </div>
            )}
          </div>
        </div>
      </div>

      {/* Action Notice Toast */}
      {actionNotice && (
        <div className={`px-2.5 py-1.5 rounded-xl text-xs font-medium border flex items-center justify-between ${
          isDark
            ? 'bg-slate-850 border-slate-700 text-slate-300'
            : 'bg-slate-100 border-slate-200 text-slate-800'
        }`}>
          <span>{actionNotice}</span>
          <button onClick={() => setActionNotice(null)} className="cursor-pointer text-slate-400">
            <X className="w-3 h-3" />
          </button>
        </div>
      )}

      {/* Desktop Fallback Card if Desktop bridge isn't running */}
      {desktopFallbackNotice && (
        <div className={`p-2.5 rounded-xl border text-xs space-y-1.5 ${
          isDark ? 'bg-slate-900 border-slate-700 text-slate-300' : 'bg-slate-50 border-slate-200 text-slate-700'
        }`}>
          <div className="flex items-center gap-1.5">
            <AlertCircle className="w-3.5 h-3.5 text-amber-500 shrink-0" />
            <span className="text-[11px] font-semibold">برنامج الديسكتوب غير متصل حالياً (port 8765)</span>
          </div>
          <div className="flex gap-1.5">
            <button
              onClick={handleDownloadDesktopFile}
              className="flex-1 py-1 px-2 bg-[#0F172A] text-white rounded-lg text-xs font-semibold cursor-pointer hover:bg-slate-800 flex items-center justify-center gap-1"
            >
              <Download className="w-3 h-3" />
              <span>تنزيل ملف الديسكتوب (.json)</span>
            </button>
            <button
              onClick={() => setDesktopFallbackNotice(false)}
              className="px-2 py-1 rounded-lg border text-xs text-slate-500 cursor-pointer"
            >
              إغلاق
            </button>
          </div>
        </div>
      )}

      {/* ── Floating Bulk Action Bar (When rows are selected) ── */}
      {selectedRowIndices.length > 0 && (
        <div className={`flex items-center justify-between px-3 py-1.5 rounded-xl border shadow-sm transition-all ${
          isDark
            ? 'bg-slate-900 border-slate-700 text-slate-200'
            : 'bg-white border-slate-300 text-slate-800'
        }`}>
          <div className="flex items-center gap-2 text-xs font-semibold">
            <span className="w-2 h-2 rounded-full bg-blue-500" />
            <span>{selectedRowIndices.length} rows selected</span>
          </div>

          <div className="flex items-center gap-1.5">
            <button
              onClick={handleExportSelected}
              className="flex items-center gap-1 px-2 py-0.5 rounded-lg text-xs font-medium border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-700 dark:text-slate-300 hover:bg-slate-50 cursor-pointer shadow-2xs"
            >
              <Download className="w-3 h-3 text-emerald-500" />
              <span>Excel</span>
            </button>

            <button
              onClick={() => handleSendToDesktop(true)}
              className="flex items-center gap-1 px-2 py-0.5 rounded-lg text-xs font-medium bg-slate-900 text-white hover:bg-slate-800 cursor-pointer shadow-2xs"
            >
              <Send className="w-3 h-3 text-emerald-400" />
              <span>Desktop</span>
            </button>

            <button
              onClick={deleteSelectedRecords}
              className="flex items-center gap-1 px-2 py-0.5 rounded-lg text-xs font-medium text-red-600 hover:bg-red-50 dark:hover:bg-red-950/40 cursor-pointer"
            >
              <Trash2 className="w-3 h-3" />
              <span>حذف</span>
            </button>

            <button
              onClick={clearRowSelection}
              className="p-1 rounded cursor-pointer text-slate-400 hover:text-slate-600"
            >
              <X className="w-3 h-3" />
            </button>
          </div>
        </div>
      )}

      {/* ── Excel-like Spreadsheet Data Grid ── */}
      <div className={`rounded-xl border overflow-hidden shadow-2xs ${
        isDark ? 'border-slate-800 bg-slate-950/40' : 'border-slate-200 bg-white'
      }`}>
        <div className="max-h-[52vh] overflow-x-auto overflow-y-auto custom-scrollbar">
          <table className="w-full text-left text-xs border-collapse select-text">
            {/* Table Header (Excel style - strictly left-to-right aligned) */}
            <thead className={`sticky top-0 z-20 border-b select-none ${
              isDark ? 'bg-slate-900 border-slate-800 text-slate-300' : 'bg-slate-100 border-slate-200 text-slate-700'
            }`}>
              <tr>
                {/* Select All Checkbox */}
                <th className="w-8 px-2 py-2 text-center border-r border-slate-200 dark:border-slate-800">
                  <button
                    onClick={() => {
                      if (isAllSelected) clearRowSelection();
                      else selectAllRows();
                    }}
                    className="cursor-pointer text-slate-400 hover:text-slate-600 flex items-center justify-center mx-auto"
                  >
                    {isAllSelected ? (
                      <CheckSquare className="w-3.5 h-3.5 text-blue-600" />
                    ) : isPartiallySelected ? (
                      <div className="w-3.5 h-3.5 border-2 border-blue-600 rounded-xs flex items-center justify-center">
                        <div className="w-2 h-1 bg-blue-600" />
                      </div>
                    ) : (
                      <Square className="w-3.5 h-3.5 text-slate-400" />
                    )}
                  </button>
                </th>

                {/* Row Number Column */}
                <th className="w-10 px-2 py-2 text-center font-mono text-[10px] text-slate-400 border-r border-slate-200 dark:border-slate-800">
                  #
                </th>

                {/* Dynamic Data Columns */}
                {visibleFields.map((field) => (
                  <th
                    key={field}
                    className="px-3 py-2 text-xs font-semibold tracking-wide border-r border-slate-200 dark:border-slate-800 truncate max-w-[180px]"
                  >
                    <div className="flex items-center justify-between gap-1.5">
                      <span className="truncate">{field}</span>
                      <button
                        onClick={() => toggleColumnVisibility(field)}
                        className="opacity-40 hover:opacity-100 text-slate-400 hover:text-red-500 cursor-pointer p-0.5"
                        title={`Hide column '${field}'`}
                      >
                        <EyeOff className="w-3 h-3" />
                      </button>
                    </div>
                  </th>
                ))}

                {/* Actions column */}
                <th className="w-8 px-2 py-2 text-center text-[10px] text-slate-400">
                  ✕
                </th>
              </tr>
            </thead>

            {/* Table Body (Excel rows - Calm, Subtle Highlights) */}
            <tbody>
              {filteredRecordsWithIndex.map(({ record, origIndex }, rowIdx) => {
                const isSelected = selectedRowIndices.includes(origIndex);
                return (
                  <tr
                    key={origIndex}
                    className={`border-b transition-colors ${
                      isSelected
                        ? isDark
                          ? 'bg-slate-800/80 border-slate-700'
                          : 'bg-slate-100 border-slate-300'
                        : isDark
                          ? rowIdx % 2 === 0 ? 'bg-slate-900/30 hover:bg-slate-850' : 'bg-slate-950/40 hover:bg-slate-850'
                          : rowIdx % 2 === 0 ? 'bg-white hover:bg-slate-50' : 'bg-slate-50/50 hover:bg-slate-100/70'
                    } ${isDark ? 'border-slate-800/60' : 'border-slate-200/80'}`}
                  >
                    {/* Checkbox */}
                    <td className="px-2 py-1.5 text-center border-r border-slate-200 dark:border-slate-800/80">
                      <button
                        onClick={() => toggleRowSelection(origIndex)}
                        className="cursor-pointer flex items-center justify-center mx-auto"
                      >
                        {isSelected ? (
                          <CheckSquare className="w-3.5 h-3.5 text-blue-600" />
                        ) : (
                          <Square className="w-3.5 h-3.5 text-slate-400 hover:text-slate-600" />
                        )}
                      </button>
                    </td>

                    {/* Row Index */}
                    <td className="px-2 py-1.5 text-center font-mono text-[10px] text-slate-400 border-r border-slate-200 dark:border-slate-800/80">
                      {origIndex + 1}
                    </td>

                    {/* Data Cells */}
                    {visibleFields.map((field) => {
                      const cellValue = String(record[field] ?? '');
                      const cellKey = `${origIndex}-${field}`;
                      const isCopied = copiedCell === cellKey;

                      return (
                        <td
                          key={field}
                          onClick={() => handleCopyCell(cellValue, cellKey)}
                          className={`px-3 py-1.5 text-xs font-mono border-r border-slate-200 dark:border-slate-800/80 max-w-[200px] truncate cursor-pointer transition-colors group relative ${
                            isDark ? 'text-slate-300 hover:bg-slate-800/50' : 'text-slate-800 hover:bg-slate-100'
                          }`}
                          title={`Click to copy: ${cellValue || '(Empty)'}`}
                        >
                          <div className="flex items-center justify-between gap-1">
                            <span className="truncate">{cellValue || <span className="opacity-30 italic">null</span>}</span>
                            {cellValue && (
                              <span className="opacity-0 group-hover:opacity-100 transition-opacity">
                                {isCopied ? <Check className="w-3 h-3 text-emerald-500" /> : <Copy className="w-3 h-3 text-slate-400" />}
                              </span>
                            )}
                          </div>
                        </td>
                      );
                    })}

                    {/* Delete Individual Row */}
                    <td className="px-2 py-1.5 text-center">
                      <button
                        onClick={() => removeRecord(origIndex)}
                        className="text-slate-400 hover:text-red-500 p-0.5 rounded transition-colors cursor-pointer"
                        title="Delete this row"
                      >
                        <Trash2 className="w-3 h-3" />
                      </button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>

        {/* ── Table Footer Status Bar (Excel-style) ── */}
        <div className={`px-3 py-1.5 border-t flex items-center justify-between text-[11px] ${
          isDark ? 'bg-slate-900/90 border-slate-800 text-slate-400' : 'bg-slate-50 border-slate-200 text-slate-600'
        }`}>
          <div className="flex items-center gap-3 font-mono">
            <span>
              <strong>{filteredRecordsWithIndex.length}</strong> / <strong>{records.length}</strong> rows
            </span>
            <span>•</span>
            <span>
              <strong>{visibleFields.length}</strong> / <strong>{allFields.length}</strong> cols
            </span>
            {searchTerm && (
              <span className="text-amber-500 font-sans font-semibold">(Filtered)</span>
            )}
          </div>

          <div className="flex items-center gap-2">
            {selectedRowIndices.length > 0 && (
              <span className="text-blue-600 font-semibold">
                {selectedRowIndices.length} Selected
              </span>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
