import React, { useState } from 'react';
import { useStore } from '../store/useStore';
import { Table as TableIcon, Trash2, Download, Check, ArrowRight } from 'lucide-react';

export function TablePicker() {
  const { tableInfo, setTableInfo, setRecords, theme } = useStore();
  const [selectedCols, setSelectedCols] = useState<number[]>([]);
  const isDark = theme === 'dark';

  if (!tableInfo) {
    return (
      <div className="p-8 text-center flex flex-col items-center justify-center">
        <div className={`w-12 h-12 rounded-2xl flex items-center justify-center mb-3 border ${
          isDark ? 'bg-slate-800/80 border-slate-700/80' : 'bg-white border-slate-200 shadow-sm'
        }`}>
          <TableIcon className={`w-5 h-5 ${isDark ? 'text-slate-400' : 'text-slate-500'}`} />
        </div>
        <p className={`text-xs font-bold ${isDark ? 'text-slate-200' : 'text-slate-800'}`}>No Table Selected</p>
        <p className={`text-[11px] mt-1 ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>
          Click <strong className="text-amber-500">Pick Table</strong> on the top bar and click any HTML table on the web page.
        </p>
      </div>
    );
  }

  const toggleCol = (idx: number) => {
    setSelectedCols((prev) =>
      prev.includes(idx) ? prev.filter((i) => i !== idx) : [...prev, idx]
    );
  };

  const selectAll = () => setSelectedCols(tableInfo.headers.map((_, i) => i));
  const selectNone = () => setSelectedCols([]);

  const visibleCols = selectedCols.length > 0 ? selectedCols : tableInfo.headers.map((_, i) => i);

  // Convert table rows to ExtractedRecord objects
  const handleImportToGrid = () => {
    const headers = tableInfo.headers;
    const records = tableInfo.rows.map((row) => {
      const obj: Record<string, string> = {};
      visibleCols.forEach((colIdx) => {
        const key = headers[colIdx] || `Col_${colIdx + 1}`;
        obj[key] = row[colIdx] ?? '';
      });
      return obj;
    });
    setRecords(records);
  };

  const handleExportCSV = () => {
    const headers = visibleCols.map((c) => `"${(tableInfo.headers[c] || `Col_${c + 1}`).replace(/"/g, '""')}"`).join(',');
    const rows = tableInfo.rows.map((row) =>
      visibleCols.map((c) => `"${(row[c] ?? '').replace(/"/g, '""')}"`).join(',')
    );
    const content = '\uFEFF' + [headers, ...rows].join('\r\n');
    const blob = new Blob([content], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `scripox_table_${Date.now()}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  };

  return (
    <div className="p-4 space-y-4">
      {/* Header Info & Actions */}
      <div className={`rounded-2xl p-3.5 border ${
        isDark ? 'bg-slate-900/80 border-slate-800' : 'bg-white border-slate-200 shadow-xs'
      }`}>
        <div className="flex items-center justify-between mb-2">
          <p className="text-[11px] text-slate-500 font-medium">Target Table Selector</p>
          <button
            onClick={() => setTableInfo(null)}
            className="flex items-center gap-1 text-[11px] text-red-500 hover:text-red-600 font-semibold cursor-pointer"
            title="Remove/Deselect table"
          >
            <Trash2 className="w-3 h-3" />
            <span>حذف هذا الجدول</span>
          </button>
        </div>
        <code className="text-xs text-amber-500 font-mono break-all block mb-3">
          {tableInfo.selector}
        </code>

        <div className="flex gap-2">
          <button
            onClick={handleImportToGrid}
            className="flex-1 flex items-center justify-center gap-1.5 py-1.5 bg-[#CEF22C] text-[#0F172A] font-bold text-xs rounded-xl hover:bg-[#bde01e] transition-colors cursor-pointer shadow-xs"
          >
            <span>فتح في جدول Excel</span>
            <ArrowRight className="w-3.5 h-3.5" />
          </button>
          <button
            onClick={handleExportCSV}
            className={`flex items-center gap-1.5 px-3 py-1.5 rounded-xl text-xs font-semibold border cursor-pointer transition-colors ${
              isDark
                ? 'bg-slate-800 border-slate-700 text-slate-200 hover:bg-slate-700'
                : 'bg-slate-100 border-slate-200 text-slate-700 hover:bg-slate-200'
            }`}
          >
            <Download className="w-3.5 h-3.5 text-emerald-500" />
            <span>تصدير Excel</span>
          </button>
        </div>
      </div>

      {/* Column selector */}
      <div>
        <div className="flex items-center justify-between mb-2">
          <p className={`text-xs font-semibold ${isDark ? 'text-slate-300' : 'text-slate-700'}`}>
            Columns Detected ({tableInfo.headers.length})
          </p>
          <div className="flex gap-2">
            <button onClick={selectAll} className="text-xs text-blue-500 hover:underline cursor-pointer">All</button>
            <span className="text-slate-400">|</span>
            <button onClick={selectNone} className="text-xs text-slate-400 hover:underline cursor-pointer">None</button>
          </div>
        </div>
        <div className="flex flex-wrap gap-1.5">
          {tableInfo.headers.map((header, idx) => (
            <button
              key={idx}
              onClick={() => toggleCol(idx)}
              className={`px-2.5 py-1 rounded-xl text-xs font-medium transition-all duration-150 cursor-pointer ${
                (selectedCols.length === 0 || selectedCols.includes(idx))
                  ? 'bg-amber-500/20 border border-amber-500/40 text-amber-600 font-semibold'
                  : isDark
                    ? 'bg-slate-900 border border-slate-800 text-slate-500'
                    : 'bg-white border border-slate-200 text-slate-400'
              }`}
            >
              {header || `Col ${idx + 1}`}
            </button>
          ))}
        </div>
      </div>

      {/* Rows Preview */}
      <div>
        <p className={`text-xs font-semibold mb-2 ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>
          Preview ({tableInfo.rows.length} rows total)
        </p>
        <div className={`overflow-x-auto rounded-xl border ${
          isDark ? 'border-slate-800 bg-slate-900/60' : 'border-slate-200 bg-white'
        }`}>
          <table className="w-full text-xs">
            <thead>
              <tr className={`border-b ${isDark ? 'border-slate-800 bg-slate-950/80 text-slate-400' : 'border-slate-200 bg-slate-100 text-slate-600'}`}>
                {visibleCols.map((colIdx) => (
                  <th key={colIdx} className="text-left px-3 py-2 font-medium">
                    {tableInfo.headers[colIdx] || `Col ${colIdx + 1}`}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {tableInfo.rows.slice(0, 10).map((row, rowIdx) => (
                <tr key={rowIdx} className={`border-b ${isDark ? 'border-slate-800/40 hover:bg-slate-800/30 text-slate-300' : 'border-slate-200/60 hover:bg-slate-50 text-slate-800'}`}>
                  {visibleCols.map((colIdx) => (
                    <td key={colIdx} className="px-3 py-2 font-mono text-[11px] truncate max-w-[140px]">
                      {row[colIdx] ?? ''}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
