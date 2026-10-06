import React, { useState } from 'react';
import { useStore } from '../store/useStore';
import type { ExtractedRecord } from '../../types';
import { DataCleaner } from '../../content/engines/DataCleaner';
import { Download, FileSpreadsheet, FileJson, Copy, Check, Target } from 'lucide-react';

function recordsToCSV(records: ExtractedRecord[]): string {
  if (!records.length) return '';
  const fields = [...new Set(records.flatMap((r) => Object.keys(r)))];
  const header = fields.map((f) => `"${f.replace(/"/g, '""')}"`).join(',');
  const rows = records.map((r) =>
    fields.map((f) => {
      const v = String(r[f] ?? '').replace(/"/g, '""');
      return `"${v}"`;
    }).join(',')
  );
  return '\uFEFF' + [header, ...rows].join('\r\n');
}

function recordsToMetaCSV(records: ExtractedRecord[]): string {
  const header = 'email,phone,fn,ln,ct,st,country';
  const rows = records.map((r) => {
    const email = (r['email'] || '').toLowerCase().trim();
    const phone = (r['phone'] || '').replace(/\D/g, '');
    const name = r['name'] || r['title'] || '';
    const parts = name.split(' ');
    const fn = parts[0] || '';
    const ln = parts.slice(1).join(' ') || '';
    const city = r['city'] || r['address'] || '';
    return `"${email}","${phone}","${fn}","${ln}","${city}","","AE"`;
  });
  return '\uFEFF' + [header, ...rows].join('\r\n');
}

function recordsToGoogleCSV(records: ExtractedRecord[]): string {
  const header = 'Email,Phone,First Name,Country';
  const rows = records.map((r) => {
    const email = (r['email'] || '').toLowerCase().trim();
    const phone = (r['phone'] || '').replace(/\D/g, '');
    const name = r['name'] || r['title'] || '';
    const fn = name.split(' ')[0] || '';
    return `"${email}","+${phone}","${fn}","AE"`;
  });
  return '\uFEFF' + [header, ...rows].join('\r\n');
}

function downloadFile(content: string, filename: string, mime: string) {
  const blob = new Blob([content], { type: `${mime};charset=utf-8;` });
  const url  = URL.createObjectURL(blob);
  const a    = document.createElement('a');
  a.href     = url;
  a.download = filename;
  a.click();
  URL.revokeObjectURL(url);
}

export function ExportPanel() {
  const { records, theme } = useStore();
  const [copied, setCopied] = useState(false);
  const isDark = theme === 'dark';

  const cleanList = DataCleaner.cleanAndDeduplicate(records);
  const filename = `scripox_leads_${Date.now()}`;

  const exportCleanCSV = () => {
    downloadFile(recordsToCSV(cleanList), `${filename}_clean.csv`, 'text/csv');
  };

  const exportMetaCSV = () => {
    downloadFile(recordsToMetaCSV(cleanList), `meta_audience_${filename}.csv`, 'text/csv');
  };

  const exportGoogleCSV = () => {
    downloadFile(recordsToGoogleCSV(cleanList), `google_match_${filename}.csv`, 'text/csv');
  };

  const exportJSON = () => {
    downloadFile(JSON.stringify(cleanList, null, 2), `${filename}.json`, 'application/json');
  };

  const copyToClipboard = async () => {
    const text = JSON.stringify(cleanList, null, 2);
    await navigator.clipboard.writeText(text);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  if (records.length === 0) {
    return (
      <div className="p-8 text-center flex flex-col items-center justify-center">
        <div className={`w-12 h-12 rounded-2xl flex items-center justify-center mb-3 border ${
          isDark ? 'bg-slate-800/80 border-slate-700/80' : 'bg-white border-slate-200 shadow-sm'
        }`}>
          <Download className="w-5 h-5 text-slate-400" />
        </div>
        <p className={`text-xs font-bold ${isDark ? 'text-slate-200' : 'text-slate-800'}`}>No Data to Export</p>
        <p className={`text-[11px] mt-1 ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>Extract leads to download formatted Excel & ad audiences</p>
      </div>
    );
  }

  const cardClass = `w-full flex items-center justify-between p-3 rounded-xl border transition-colors cursor-pointer text-left ${
    isDark
      ? 'bg-slate-900/80 border-slate-800 hover:border-slate-700'
      : 'bg-white border-slate-200 hover:border-slate-300 hover:bg-slate-50 shadow-xs'
  }`;

  return (
    <div className="p-4 space-y-3 font-sans">
      <div className={`border-b pb-2 ${isDark ? 'border-slate-800' : 'border-slate-200'}`}>
        <p className={`text-xs font-bold ${isDark ? 'text-slate-200' : 'text-slate-900'}`}>Export &amp; Marketing Audiences</p>
        <p className={`text-[11px] ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>
          Auto-sanitized, cleaned &amp; deduplicated ({cleanList.length} leads)
        </p>
      </div>

      <div className="space-y-2">
        {/* Clean Standard CSV / Excel */}
        <button onClick={exportCleanCSV} className={cardClass}>
          <div className="flex items-center gap-2.5">
            <div className="w-8 h-8 rounded-lg bg-emerald-500/15 border border-emerald-500/30 flex items-center justify-center">
              <FileSpreadsheet className="w-4 h-4 text-emerald-600" />
            </div>
            <div>
              <p className={`text-xs font-semibold ${isDark ? 'text-white' : 'text-slate-900'}`}>Excel / Clean Spreadsheet (CSV)</p>
              <p className={`text-[10px] ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>UTF-8 with BOM — perfect for Arabic &amp; special chars</p>
            </div>
          </div>
          <span className="text-[10px] text-emerald-600 font-mono font-bold">.csv</span>
        </button>

        {/* Meta Ads Audience CSV */}
        <button onClick={exportMetaCSV} className={cardClass}>
          <div className="flex items-center gap-2.5">
            <div className="w-8 h-8 rounded-lg bg-blue-500/15 border border-blue-500/30 flex items-center justify-center">
              <Target className="w-4 h-4 text-blue-600" />
            </div>
            <div>
              <p className={`text-xs font-semibold ${isDark ? 'text-white' : 'text-slate-900'}`}>Meta Ads Custom Audience</p>
              <p className={`text-[10px] ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>Formatted for Facebook &amp; Instagram upload</p>
            </div>
          </div>
          <span className="text-[10px] text-blue-600 font-mono font-bold">Meta</span>
        </button>

        {/* Google Ads Match CSV */}
        <button onClick={exportGoogleCSV} className={cardClass}>
          <div className="flex items-center gap-2.5">
            <div className="w-8 h-8 rounded-lg bg-amber-500/15 border border-amber-500/30 flex items-center justify-center">
              <Target className="w-4 h-4 text-amber-600" />
            </div>
            <div>
              <p className={`text-xs font-semibold ${isDark ? 'text-white' : 'text-slate-900'}`}>Google Customer Match</p>
              <p className={`text-[10px] ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>Formatted for Search &amp; YouTube campaigns</p>
            </div>
          </div>
          <span className="text-[10px] text-amber-600 font-mono font-bold">Google</span>
        </button>

        {/* JSON */}
        <button onClick={exportJSON} className={cardClass}>
          <div className="flex items-center gap-2.5">
            <div className="w-8 h-8 rounded-lg bg-purple-500/15 border border-purple-500/30 flex items-center justify-center">
              <FileJson className="w-4 h-4 text-purple-600" />
            </div>
            <div>
              <p className={`text-xs font-semibold ${isDark ? 'text-white' : 'text-slate-900'}`}>Structured JSON</p>
              <p className={`text-[10px] ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>Standard API objects array</p>
            </div>
          </div>
          <span className="text-[10px] text-purple-600 font-mono font-bold">.json</span>
        </button>

        {/* Copy to Clipboard */}
        <button onClick={copyToClipboard} className={cardClass}>
          <div className="flex items-center gap-2.5">
            <div className={`w-8 h-8 rounded-lg border flex items-center justify-center ${
              isDark ? 'bg-slate-800 border-slate-700' : 'bg-slate-100 border-slate-200'
            }`}>
              {copied ? <Check className="w-4 h-4 text-emerald-500" /> : <Copy className="w-4 h-4 text-slate-500" />}
            </div>
            <div>
              <p className={`text-xs font-semibold ${isDark ? 'text-white' : 'text-slate-900'}`}>{copied ? 'Copied to Clipboard!' : 'Copy to Clipboard'}</p>
              <p className={`text-[10px] ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>Clean JSON text buffer</p>
            </div>
          </div>
        </button>
      </div>
    </div>
  );
}
