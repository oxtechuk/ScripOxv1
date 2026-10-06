import React, { useState } from 'react';
import { useStore } from '../store/useStore';
import { Bookmark, Plus, Trash2, Check } from 'lucide-react';

const PRESET_TEMPLATES = [
  { name: 'Google Maps Places',       urlPattern: 'maps.google.com' },
  { name: 'Yellow Pages Directory',    urlPattern: 'yellowpages.com' },
  { name: 'Facebook Business Hub',     urlPattern: 'facebook.com' },
  { name: 'LinkedIn Organizations',    urlPattern: 'linkedin.com' },
  { name: 'Commercial Directory',      urlPattern: '' },
  { name: 'Corporate News Portal',     urlPattern: '' },
  { name: 'Enterprise Job Board',      urlPattern: '' },
];

export function TemplateManager() {
  const { templates, saveTemplate, loadTemplate, deleteTemplate, selectors, theme } = useStore();
  const [newName, setNewName] = useState('');
  const [showSave, setShowSave] = useState(false);
  const [saved, setSaved] = useState<string | null>(null);
  const isDark = theme === 'dark';

  const handleSave = () => {
    if (!newName.trim() || !selectors) return;
    saveTemplate(newName.trim(), location.hostname);
    setNewName('');
    setShowSave(false);
    setSaved(newName.trim());
    setTimeout(() => setSaved(null), 2000);
  };

  const handleLoad = (id: string) => {
    loadTemplate(id);
  };

  return (
    <div className="p-4 space-y-4 font-sans">
      {/* Header */}
      <div className={`flex items-center justify-between border-b pb-2 ${isDark ? 'border-slate-800' : 'border-slate-200'}`}>
        <div>
          <p className={`text-xs font-bold ${isDark ? 'text-slate-300' : 'text-slate-900'}`}>Extraction Templates</p>
          <p className={`text-[11px] ${isDark ? 'text-slate-500' : 'text-slate-500'}`}>Reusable schema definitions across domains</p>
        </div>
        <button
          onClick={() => setShowSave(!showSave)}
          disabled={!selectors}
          className={`px-3 py-1.5 border text-xs font-semibold rounded-xl disabled:opacity-40 disabled:cursor-not-allowed transition-all flex items-center gap-1.5 cursor-pointer shadow-xs ${
            isDark
              ? 'bg-[#1E293B] hover:bg-slate-700 border-slate-700 text-white'
              : 'bg-[#0F172A] hover:bg-slate-800 border-slate-800 text-white'
          }`}
        >
          <Plus className="w-3.5 h-3.5 text-[#CEF22C]" />
          <span>Save Current</span>
        </button>
      </div>

      {/* Save Template Form */}
      {showSave && (
        <div className={`rounded-2xl p-3 border space-y-2 ${
          isDark ? 'bg-slate-900/80 border-slate-800' : 'bg-white border-slate-200 shadow-sm'
        }`}>
          <input
            type="text"
            value={newName}
            onChange={(e) => setNewName(e.target.value)}
            placeholder="Template name (e.g. Dubai Real Estate Cards)"
            className={`w-full rounded-xl px-3 py-2 text-xs outline-none border ${
              isDark
                ? 'bg-slate-950 border-slate-800 text-slate-200 placeholder-slate-600 focus:border-slate-600'
                : 'bg-slate-50 border-slate-200 text-slate-900 placeholder-slate-400 focus:border-slate-300'
            }`}
          />
          <button
            onClick={handleSave}
            disabled={!newName.trim()}
            className="w-full py-2 bg-[#CEF22C] text-[#0F172A] font-bold text-xs rounded-xl hover:bg-[#bde01e] disabled:opacity-40 disabled:cursor-not-allowed cursor-pointer shadow-xs"
          >
            Confirm Save
          </button>
        </div>
      )}

      {/* Preset Schemas */}
      <div>
        <p className={`text-xs font-semibold mb-2 ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>Preset Domain Profiles</p>
        <div className="space-y-1.5">
          {PRESET_TEMPLATES.map((preset) => (
            <div
              key={preset.name}
              className={`flex items-center justify-between p-2.5 rounded-xl border transition-colors ${
                isDark
                  ? 'bg-slate-900/60 border-slate-800/80 hover:border-slate-700'
                  : 'bg-white border-slate-200 hover:border-slate-300 shadow-xs'
              }`}
            >
              <div className="flex items-center gap-2.5">
                <Bookmark className="w-3.5 h-3.5 text-[#CEF22C]" />
                <span className={`text-xs font-semibold ${isDark ? 'text-slate-300' : 'text-slate-800'}`}>{preset.name}</span>
              </div>
              <span className={`text-[10px] font-mono ${isDark ? 'text-slate-500' : 'text-slate-400'}`}>{preset.urlPattern || 'universal'}</span>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
