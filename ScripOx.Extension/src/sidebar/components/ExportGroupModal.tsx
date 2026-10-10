import React, { useState, useEffect } from 'react';
import { Folder, Tag, X, Send, AlertCircle, RotateCw } from 'lucide-react';

interface ExportGroupModalProps {
  isOpen: boolean;
  onClose: () => void;
  onConfirm: (groupName: string) => void;
  recordCount: number;
  isDark: boolean;
  isSending?: boolean;
}

const PRESET_GROUPS = [
  'VIP Prospect',
  'مطاعم وكافيهات',
  'شركات تسويق ودعاية',
  'عقارات وتطوير',
  'أطباء ومستشفيات',
  'تجارة ومستوردين',
  'خدمات وأعمال',
];

export function ExportGroupModal({
  isOpen,
  onClose,
  onConfirm,
  recordCount,
  isDark,
  isSending = false,
}: ExportGroupModalProps) {
  const [groupName, setGroupName] = useState('');
  const [error, setError] = useState('');

  useEffect(() => {
    if (isOpen) {
      setGroupName('');
      setError('');
    }
  }, [isOpen]);

  if (!isOpen) return null;

  const handleSubmit = (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    const trimmed = groupName.trim();
    if (!trimmed) {
      setError('⚠️ يجب كتابة أو اختيار اسم المجموعة قبل التصدير للبرنامج.');
      return;
    }
    setError('');
    onConfirm(trimmed);
  };

  return (
    <div className="fixed inset-0 z-50 bg-black/65 backdrop-blur-xs flex items-center justify-center p-3 font-sans" dir="rtl">
      <div
        className={`w-full max-w-sm rounded-2xl border shadow-2xl p-4 transition-all animate-in fade-in zoom-in-95 ${
          isDark
            ? 'bg-[#0F172A] border-slate-700 text-slate-100'
            : 'bg-white border-slate-200 text-slate-900'
        }`}
      >
        {/* Header */}
        <div className="flex items-center justify-between pb-3 border-b border-slate-200 dark:border-slate-800">
          <div className="flex items-center gap-2">
            <div className="w-8 h-8 rounded-xl bg-lime-400/20 text-lime-600 dark:text-lime-400 flex items-center justify-center">
              <Folder className="w-4 h-4" />
            </div>
            <div>
              <h3 className="text-xs font-bold leading-tight">تصدير للبرنامج وتحديد المجموعة</h3>
              <p className="text-[10px] text-slate-500 dark:text-slate-400">
                تنظيم ({recordCount}) شركة مستخرجة تحت تصنيف محدد
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            disabled={isSending}
            className="p-1 rounded-lg text-slate-400 hover:text-slate-600 dark:hover:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800 cursor-pointer"
          >
            <X className="w-4 h-4" />
          </button>
        </div>

        {/* Form Body */}
        <form onSubmit={handleSubmit} className="pt-3 space-y-3">
          <div>
            <label className="block text-[11px] font-semibold mb-1 text-slate-700 dark:text-slate-300">
              اسم المجموعة / التصنيف <span className="text-rose-500 font-bold">*</span>
            </label>
            <div className="relative">
              <input
                type="text"
                autoFocus
                value={groupName}
                onChange={(e) => {
                  setGroupName(e.target.value);
                  if (error) setError('');
                }}
                placeholder="اكتب اسم المجموعة (مثال: مطاعم دبي، عقارات، تسويق)..."
                disabled={isSending}
                className={`w-full px-3 py-2 text-xs rounded-xl border outline-none transition-all ${
                  error
                    ? 'border-rose-500 bg-rose-50/20 text-rose-600 dark:text-rose-400'
                    : isDark
                      ? 'bg-slate-900 border-slate-700 text-white focus:border-lime-400'
                      : 'bg-slate-50 border-slate-200 text-slate-900 focus:border-slate-800'
                }`}
              />
            </div>
            {error && (
              <p className="text-[10px] text-rose-500 mt-1 font-semibold flex items-center gap-1">
                <AlertCircle className="w-3 h-3 shrink-0" />
                <span>{error}</span>
              </p>
            )}
          </div>

          {/* Quick Suggestions */}
          <div>
            <div className="flex items-center gap-1 text-[10px] font-medium text-slate-500 dark:text-slate-400 mb-1.5">
              <Tag className="w-3 h-3" />
              <span>أو اختر من المجموعات الشائعة:</span>
            </div>
            <div className="flex flex-wrap gap-1.5">
              {PRESET_GROUPS.map((preset) => (
                <button
                  type="button"
                  key={preset}
                  onClick={() => {
                    setGroupName(preset);
                    setError('');
                  }}
                  disabled={isSending}
                  className={`text-[10px] px-2 py-0.5 rounded-lg border transition-all cursor-pointer font-medium ${
                    groupName === preset
                      ? 'bg-lime-400 text-slate-950 border-lime-500 font-bold'
                      : isDark
                        ? 'bg-slate-800 border-slate-700 text-slate-300 hover:bg-slate-700'
                        : 'bg-slate-100 border-slate-200 text-slate-700 hover:bg-slate-200'
                  }`}
                >
                  {preset}
                </button>
              ))}
            </div>
          </div>

          {/* Buttons */}
          <div className="pt-2 flex items-center gap-2">
            <button
              type="submit"
              disabled={isSending}
              className={`flex-1 py-2 px-3 rounded-xl text-xs font-bold transition-all flex items-center justify-center gap-1.5 cursor-pointer shadow-sm ${
                isDark
                  ? 'bg-lime-400 text-slate-950 hover:bg-lime-300'
                  : 'bg-slate-900 text-white hover:bg-slate-800'
              }`}
            >
              {isSending ? (
                <>
                  <RotateCw className="w-3.5 h-3.5 animate-spin" />
                  <span>جاري التصدير للبرنامج...</span>
                </>
              ) : (
                <>
                  <Send className="w-3.5 h-3.5" />
                  <span>تصدير وحفظ للمجموعة الآن ✓</span>
                </>
              )}
            </button>
            <button
              type="button"
              onClick={onClose}
              disabled={isSending}
              className={`py-2 px-3 rounded-xl text-xs font-semibold border cursor-pointer transition-colors ${
                isDark
                  ? 'bg-slate-800 border-slate-700 text-slate-300 hover:bg-slate-700'
                  : 'bg-white border-slate-200 text-slate-600 hover:bg-slate-100'
              }`}
            >
              إلغاء
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
