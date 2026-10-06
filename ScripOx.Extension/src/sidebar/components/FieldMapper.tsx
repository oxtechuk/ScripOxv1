import React, { useState } from 'react';
import { useStore } from '../store/useStore';
import { SlidersHorizontal, Plus } from 'lucide-react';

const STANDARD_FIELDS = [
  { key: 'name',        label: 'Company Name',      placeholder: 'h2.title, .company-name' },
  { key: 'phone',       label: 'Telephone',         placeholder: '.phone, a[href^="tel"]' },
  { key: 'phone2',      label: 'Secondary Phone',   placeholder: '.mobile' },
  { key: 'email',       label: 'Email Address',     placeholder: 'a[href^="mailto"]' },
  { key: 'website',     label: 'Official Website',  placeholder: 'a.website' },
  { key: 'address',     label: 'Physical Address',  placeholder: '.address' },
  { key: 'city',        label: 'City / Region',     placeholder: '.city' },
  { key: 'country',     label: 'Country / Market',  placeholder: '.country' },
  { key: 'category',    label: 'Industry Category', placeholder: '.category' },
  { key: 'description', label: 'Company Overview',  placeholder: 'p.desc' },
  { key: 'facebook',    label: 'Facebook Profile',  placeholder: 'a[href*="facebook.com"]' },
  { key: 'instagram',   label: 'Instagram Handle',  placeholder: 'a[href*="instagram.com"]' },
];

export function FieldMapper() {
  const { mapping, updateMappingField, selectors, theme } = useStore();
  const [customField, setCustomField] = useState('');
  const [customSelector, setCustomSelector] = useState('');
  const [customFields, setCustomFields] = useState<{ key: string; label: string }[]>([]);
  const isDark = theme === 'dark';

  const addCustomField = () => {
    if (!customField.trim()) return;
    const key = customField.toLowerCase().replace(/\s+/g, '_');
    setCustomFields((prev) => [...prev, { key, label: customField }]);
    updateMappingField(key, customSelector);
    setCustomField('');
    setCustomSelector('');
  };

  if (!selectors) {
    return (
      <div className="p-8 text-center flex flex-col items-center justify-center">
        <div className={`w-12 h-12 rounded-2xl border flex items-center justify-center mb-3 ${
          isDark ? 'bg-slate-800/80 border-slate-700/80' : 'bg-white border-slate-200 shadow-sm'
        }`}>
          <SlidersHorizontal className={`w-5 h-5 ${isDark ? 'text-slate-400' : 'text-slate-500'}`} />
        </div>
        <p className={`text-xs font-bold ${isDark ? 'text-slate-200' : 'text-slate-800'}`}>No Target Element Selected</p>
        <p className={`text-[11px] mt-1 ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>Pick an element first using the top action bar to map schema fields</p>
      </div>
    );
  }

  const allFields = [...STANDARD_FIELDS, ...customFields];

  return (
    <div className="p-4 space-y-4 font-sans">
      <div className={`border-b pb-2 ${isDark ? 'border-slate-800' : 'border-slate-200'}`}>
        <p className={`text-xs font-bold ${isDark ? 'text-slate-300' : 'text-slate-900'}`}>DOM Field Mapping</p>
        <p className={`text-[11px] ${isDark ? 'text-slate-500' : 'text-slate-500'}`}>Map CSS selectors relative to the selected container</p>
      </div>

      <div className="space-y-3">
        {allFields.map(({ key, label, placeholder }) => (
          <div key={key} className="space-y-1">
            <label className={`text-xs font-semibold block ${isDark ? 'text-slate-400' : 'text-slate-700'}`}>{label}</label>
            <input
              type="text"
              value={mapping[key] || ''}
              onChange={(e) => updateMappingField(key, e.target.value)}
              placeholder={placeholder || key}
              className={`w-full rounded-xl px-3 py-2 text-xs font-mono outline-none border transition-all ${
                isDark
                  ? 'bg-slate-900/90 border-slate-800 text-slate-200 placeholder-slate-600 focus:border-slate-600'
                  : 'bg-white border-slate-200 text-slate-900 placeholder-slate-400 focus:border-slate-400 shadow-xs'
              }`}
            />
          </div>
        ))}
      </div>

      {/* Custom Field Adder */}
      <div className={`rounded-2xl p-3 border space-y-2.5 ${
        isDark ? 'bg-slate-900/60 border-slate-800' : 'bg-white border-slate-200 shadow-xs'
      }`}>
        <p className={`text-xs font-bold ${isDark ? 'text-slate-300' : 'text-slate-900'}`}>Add Schema Field</p>
        <input
          type="text"
          value={customField}
          onChange={(e) => setCustomField(e.target.value)}
          placeholder="Field key (e.g. License_Number)"
          className={`w-full rounded-xl px-3 py-2 text-xs outline-none border ${
            isDark
              ? 'bg-slate-950 border-slate-800 text-slate-200 placeholder-slate-600 focus:border-slate-600'
              : 'bg-slate-50 border-slate-200 text-slate-900 placeholder-slate-400 focus:border-slate-300'
          }`}
        />
        <input
          type="text"
          value={customSelector}
          onChange={(e) => setCustomSelector(e.target.value)}
          placeholder="CSS relative selector"
          className={`w-full rounded-xl px-3 py-2 text-xs font-mono outline-none border ${
            isDark
              ? 'bg-slate-950 border-slate-800 text-slate-200 placeholder-slate-600 focus:border-slate-600'
              : 'bg-slate-50 border-slate-200 text-slate-900 placeholder-slate-400 focus:border-slate-300'
          }`}
        />
        <button
          onClick={addCustomField}
          disabled={!customField.trim()}
          className={`w-full py-2 border text-xs font-bold rounded-xl disabled:opacity-40 disabled:cursor-not-allowed transition-all flex items-center justify-center gap-1.5 cursor-pointer shadow-xs ${
            isDark
              ? 'bg-[#1E293B] hover:bg-slate-700 border-slate-700 text-white'
              : 'bg-[#0F172A] hover:bg-slate-800 border-slate-800 text-white'
          }`}
        >
          <Plus className="w-3.5 h-3.5 text-[#CEF22C]" />
          <span>Add Custom Field</span>
        </button>
      </div>
    </div>
  );
}
