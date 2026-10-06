import React from 'react';
import { useStore } from '../store/useStore';
import { ChevronRight, ArrowDown, Infinity as InfinityIcon, MinusCircle } from 'lucide-react';

export function PaginationPanel() {
  const { pagination, theme } = useStore();
  const isDark = theme === 'dark';

  const typeLabel: Record<string, string> = {
    'next-button':    'Next Page Button',
    'load-more':      'Load More Button',
    'infinite-scroll': 'Infinite Scroll Stream',
    'none':           'Single Page / No Pagination',
  };

  return (
    <div className="p-4 space-y-4 font-sans">
      <div className={`border-b pb-2 ${isDark ? 'border-slate-800' : 'border-slate-200'}`}>
        <p className={`text-xs font-bold ${isDark ? 'text-slate-300' : 'text-slate-900'}`}>Pagination Automation</p>
        <p className={`text-[11px] ${isDark ? 'text-slate-500' : 'text-slate-500'}`}>Autonomous multi-page extraction detectors</p>
      </div>

      {pagination ? (
        <div className={`rounded-2xl p-4 border space-y-3 ${
          isDark ? 'bg-slate-900/80 border-slate-800' : 'bg-white border-slate-200 shadow-xs'
        }`}>
          <div className="flex items-center gap-3">
            <div className={`w-9 h-9 rounded-xl border flex items-center justify-center ${
              isDark ? 'bg-slate-800 border-slate-700' : 'bg-slate-100 border-slate-200'
            }`}>
              {pagination.type === 'next-button' && <ChevronRight className="w-4 h-4 text-blue-500" />}
              {pagination.type === 'load-more' && <ArrowDown className="w-4 h-4 text-emerald-500" />}
              {pagination.type === 'infinite-scroll' && <InfinityIcon className="w-4 h-4 text-purple-500" />}
              {pagination.type === 'none' && <MinusCircle className="w-4 h-4 text-slate-400" />}
            </div>
            <div>
              <p className={`text-xs font-semibold ${isDark ? 'text-white' : 'text-slate-900'}`}>
                {typeLabel[pagination.type] || pagination.type}
              </p>
              <p className={`text-[11px] font-mono ${isDark ? 'text-slate-500' : 'text-slate-400'}`}>Confidence: {pagination.confidence}%</p>
            </div>
          </div>

          {pagination.selector && (
            <div>
              <p className={`text-[11px] mb-1 ${isDark ? 'text-slate-500' : 'text-slate-500'}`}>Pagination Selector</p>
              <code className={`text-xs font-mono px-2 py-1.5 rounded-xl block border break-all ${
                isDark ? 'text-blue-300 bg-slate-950 border-slate-800' : 'text-blue-700 bg-slate-50 border-slate-200'
              }`}>
                {pagination.selector}
              </code>
            </div>
          )}
        </div>
      ) : (
        <div className={`p-6 text-center text-xs rounded-2xl border ${
          isDark ? 'text-slate-500 bg-slate-900/40 border-slate-800/80' : 'text-slate-500 bg-white border-slate-200 shadow-xs'
        }`}>
          No pagination pattern detected on current document
        </div>
      )}
    </div>
  );
}
