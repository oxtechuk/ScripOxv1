import React, { useState } from 'react';
import { useStore } from '../store/useStore';
import { MousePointer, Copy, Check } from 'lucide-react';

interface ScoreBarProps {
  label: string;
  score: number;
  selector: string;
  active: boolean;
  onClick: () => void;
  isDark: boolean;
}

function ScoreBar({ label, score, selector, active, onClick, isDark }: ScoreBarProps) {
  const [copied, setCopied] = useState(false);
  const copy = (e: React.MouseEvent) => {
    e.stopPropagation();
    navigator.clipboard.writeText(selector);
    setCopied(true);
    setTimeout(() => setCopied(false), 1500);
  };
  const color = score >= 80 ? 'bg-emerald-500' : score >= 50 ? 'bg-amber-500' : 'bg-red-500';

  return (
    <div
      onClick={onClick}
      className={`group p-3 rounded-2xl border cursor-pointer transition-all duration-150 ${
        active
          ? 'border-blue-500 bg-blue-500/10 shadow-sm'
          : isDark
            ? 'border-slate-800 bg-slate-900/60 hover:border-slate-700'
            : 'border-slate-200 bg-white hover:border-slate-300 shadow-xs'
      }`}
    >
      <div className="flex items-center justify-between mb-1.5">
        <span className={`text-xs font-semibold ${isDark ? 'text-slate-300' : 'text-slate-800'}`}>{label}</span>
        <div className="flex items-center gap-2">
          <span className={`text-[11px] font-bold font-mono ${score >= 80 ? 'text-emerald-500' : score >= 50 ? 'text-amber-500' : 'text-red-500'}`}>
            {score}%
          </span>
          <button
            onClick={copy}
            className={`transition-all text-xs p-1 rounded cursor-pointer ${
              isDark ? 'text-slate-400 hover:text-white bg-slate-800 hover:bg-slate-700' : 'text-slate-500 hover:text-slate-900 bg-slate-100 hover:bg-slate-200'
            }`}
            title="Copy selector"
          >
            {copied ? <Check className="w-3 h-3 text-emerald-500" /> : <Copy className="w-3 h-3" />}
          </button>
        </div>
      </div>
      {/* Score bar */}
      <div className={`h-1 rounded-full mb-2 overflow-hidden ${isDark ? 'bg-slate-800' : 'bg-slate-100'}`}>
        <div
          className={`h-full ${color} rounded-full transition-all duration-500`}
          style={{ width: `${score}%` }}
        />
      </div>
      {/* Selector text */}
      <code className={`text-xs break-all font-mono leading-relaxed block ${isDark ? 'text-slate-400' : 'text-slate-600'}`}>
        {selector}
      </code>
    </div>
  );
}

export function SelectorPanel() {
  const { selectors, theme } = useStore();
  const [activeType, setActiveType] = useState<string>('bestSelector');
  const isDark = theme === 'dark';

  if (!selectors) {
    return (
      <div className="p-8 text-center flex flex-col items-center justify-center space-y-2">
        <div className={`w-12 h-12 rounded-2xl border flex items-center justify-center mb-1 ${
          isDark ? 'bg-slate-800/80 border-slate-700/80' : 'bg-white border-slate-200 shadow-sm'
        }`}>
          <MousePointer className={`w-5 h-5 ${isDark ? 'text-slate-400' : 'text-slate-500'}`} />
        </div>
        <p className={`text-xs font-bold ${isDark ? 'text-slate-200' : 'text-slate-800'}`}>Ready to Capture Element</p>
        <p className={`text-[11px] ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>Click Pick Element above or use shortcut</p>
        <div className="pt-2">
          <kbd className={`border px-2 py-1 rounded-lg text-[10px] font-mono ${
            isDark ? 'bg-slate-900 border-slate-800 text-slate-400' : 'bg-slate-100 border-slate-200 text-slate-600'
          }`}>
            Alt+Shift+P
          </kbd>
        </div>
      </div>
    );
  }

  const items = [
    { type: 'css',           label: 'CSS Unique Selector',    selector: selectors.css,           score: selectors.scores.css },
    { type: 'xpath',         label: 'Full XPath',             selector: selectors.xpath,         score: selectors.scores.xpath },
    { type: 'relativeXpath', label: 'Relative XPath Anchor',  selector: selectors.relativeXpath, score: selectors.scores.relativeXpath },
    { type: 'domPath',       label: 'Hierarchical DOM Path',  selector: selectors.domPath,       score: selectors.scores.domPath },
  ];

  return (
    <div className="p-4 space-y-3 font-sans">
      <div className={`border-b pb-2 ${isDark ? 'border-slate-800' : 'border-slate-200'}`}>
        <p className={`text-xs font-bold ${isDark ? 'text-slate-300' : 'text-slate-900'}`}>Generated Selector Candidates</p>
        <p className={`text-[11px] ${isDark ? 'text-slate-500' : 'text-slate-500'}`}>Heuristically ranked by DOM robustness &amp; reliability</p>
      </div>

      <div className="space-y-2">
        {items.map((item) => (
          <ScoreBar
            key={item.type}
            label={item.label}
            score={item.score}
            selector={item.selector}
            active={activeType === item.type}
            onClick={() => setActiveType(item.type)}
            isDark={isDark}
          />
        ))}
      </div>
    </div>
  );
}
