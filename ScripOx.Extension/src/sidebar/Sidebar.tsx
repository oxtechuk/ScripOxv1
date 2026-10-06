import React, { useEffect, useRef, useState } from 'react';
import { useStore } from './store/useStore';
import { Toolbar } from './components/Toolbar';
import { SelectorPanel } from './components/SelectorPanel';
import { FieldMapper } from './components/FieldMapper';
import { DataPreview } from './components/DataPreview';
import { TablePicker } from './components/TablePicker';
import { PaginationPanel } from './components/PaginationPanel';
import { TemplateManager } from './components/TemplateManager';
import { ExportPanel } from './components/ExportPanel';
import { getApiStatus } from '../api/ScripOxClient';
import {
  X, ChevronLeft, ChevronRight, PanelLeft, PanelRight,
  Move, GripVertical, Sun, Moon
} from 'lucide-react';

type SidebarTab = 'selector' | 'fields' | 'data' | 'table' | 'pagination' | 'templates' | 'export';

export function Sidebar() {
  const {
    sidebarOpen, toggleSidebar, setApiStatus, selectors, tableInfo,
    dockPosition, setDockPosition, panelPosition, setPanelPosition,
    theme, toggleTheme, records,
  } = useStore();

  const isDark = theme === 'dark';
  const [tab, setTab] = useState<SidebarTab>('data');
  const [isDragging, setIsDragging] = useState(false);
  const dragStartRef = useRef<{ mouseX: number; mouseY: number; startX: number; startY: number }>({
    mouseX: 0, mouseY: 0, startX: 40, startY: 40
  });
  const sidebarRef = useRef<HTMLDivElement>(null);

  // Poll API status every 15s
  useEffect(() => {
    const check = async () => {
      const status = await getApiStatus().catch(() => ({ connected: false, checkedAt: Date.now() }));
      setApiStatus(status as any);
    };
    check();
    const id = setInterval(check, 15_000);
    return () => clearInterval(id);
  }, []);

  // Auto-switch tabs only when a table is explicitly picked
  useEffect(() => {
    if (tableInfo) {
      setTab('table');
    }
  }, [tableInfo]);

  // ── Drag & Drop Event Handlers ──────────────────────────────
  const handlePointerDown = (e: React.PointerEvent) => {
    // Only drag from header handle
    const target = e.target as HTMLElement;
    if (target.closest('button') || target.closest('input')) return;

    // If currently docked, transition to floating mode at current position
    let initialX = panelPosition.x;
    let initialY = panelPosition.y;

    if (dockPosition === 'right' && sidebarRef.current) {
      const rect = sidebarRef.current.getBoundingClientRect();
      initialX = Math.max(20, rect.left);
      initialY = Math.max(20, rect.top);
      setDockPosition('float');
      setPanelPosition({ x: initialX, y: initialY });
    } else if (dockPosition === 'left' && sidebarRef.current) {
      const rect = sidebarRef.current.getBoundingClientRect();
      initialX = Math.max(20, rect.left);
      initialY = Math.max(20, rect.top);
      setDockPosition('float');
      setPanelPosition({ x: initialX, y: initialY });
    }

    dragStartRef.current = {
      mouseX: e.clientX,
      mouseY: e.clientY,
      startX: initialX,
      startY: initialY,
    };
    setIsDragging(true);
    e.currentTarget.setPointerCapture(e.pointerId);
  };

  const handlePointerMove = (e: React.PointerEvent) => {
    if (!isDragging) return;
    const deltaX = e.clientX - dragStartRef.current.mouseX;
    const deltaY = e.clientY - dragStartRef.current.mouseY;

    const newX = Math.max(10, Math.min(window.innerWidth - 450, dragStartRef.current.startX + deltaX));
    const newY = Math.max(10, Math.min(window.innerHeight - 200, dragStartRef.current.startY + deltaY));

    setPanelPosition({ x: newX, y: newY });
  };

  const handlePointerUp = (e: React.PointerEvent) => {
    if (isDragging) {
      setIsDragging(false);
      try {
        e.currentTarget.releasePointerCapture(e.pointerId);
      } catch { /* ignore */ }
    }
  };

  // Excel Grid is #1 First Tab
  const tabs: { id: SidebarTab; label: string; count?: number }[] = [
    { id: 'data',       label: 'Excel Grid', count: records.length },
    { id: 'selector',   label: 'Selectors' },
    { id: 'fields',     label: 'Fields' },
    { id: 'table',      label: 'Table' },
    { id: 'pagination', label: 'Pages' },
    { id: 'templates',  label: 'Templates' },
    { id: 'export',     label: 'Export' },
  ];

  // Dynamic style and classes based on docking mode & theme
  // Set explicit direction: ltr to isolate from host page RTL direction
  let panelStyle: React.CSSProperties = {
    fontFamily: "'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif",
    direction: 'ltr',
    textAlign: 'left',
  };
  let panelClasses = isDark
    ? 'bg-[#0F172A]/98 backdrop-blur-2xl shadow-2xl flex flex-col text-slate-100 font-sans z-[2147483646] transition-all '
    : 'bg-[#FFFFFF]/98 backdrop-blur-2xl shadow-2xl flex flex-col text-slate-800 font-sans z-[2147483646] transition-all ';

  if (dockPosition === 'left') {
    panelClasses += `fixed left-0 top-0 h-screen w-[440px] border-r ${
      isDark ? 'border-slate-800' : 'border-slate-200'
    } ${sidebarOpen ? 'translate-x-0' : '-translate-x-full'}`;
  } else if (dockPosition === 'right') {
    panelClasses += `fixed right-0 top-0 h-screen w-[440px] border-l ${
      isDark ? 'border-slate-800' : 'border-slate-200'
    } ${sidebarOpen ? 'translate-x-0' : 'translate-x-full'}`;
  } else {
    // Floating draggable mode
    panelStyle = {
      ...panelStyle,
      position: 'fixed',
      left: `${panelPosition.x}px`,
      top: `${panelPosition.y}px`,
      width: '450px',
      maxHeight: '92vh',
      height: '86vh',
    };
    panelClasses += `rounded-2xl border ${
      isDark
        ? 'border-slate-700/80 shadow-[0_25px_60px_-15px_rgba(0,0,0,0.8)]'
        : 'border-slate-200 shadow-[0_20px_50px_rgba(0,0,0,0.12)]'
    } overflow-hidden ${
      sidebarOpen ? 'opacity-100 scale-100' : 'opacity-0 scale-95 pointer-events-none'
    }`;
  }

  return (
    <>
      {/* Collapsed Float Pill Button (Left or Right Edge) */}
      <button
        onClick={toggleSidebar}
        className={`fixed top-1/2 -translate-y-1/2 z-[2147483646]
                   px-3 py-3.5 shadow-2xl flex items-center gap-2
                   cursor-pointer transition-all duration-300 transform ${
                     isDark
                       ? 'bg-[#0F172A] hover:bg-slate-800 text-white border-slate-700'
                       : 'bg-white hover:bg-slate-50 text-slate-900 border-slate-200'
                   }
                   ${dockPosition === 'left' ? 'left-0 rounded-r-2xl border-r border-y' : 'right-0 rounded-l-2xl border-l border-y'}
                   ${sidebarOpen ? (dockPosition === 'left' ? '-translate-x-full opacity-0 pointer-events-none' : 'translate-x-full opacity-0 pointer-events-none') : 'translate-x-0 opacity-100'}`}
        title="Open ScripOx Hub"
      >
        {dockPosition === 'left' && <ChevronRight className="w-3.5 h-3.5 text-slate-400" />}
        <span className="w-2 h-2 rounded-full bg-[#CEF22C]" />
        <span className="text-xs font-semibold tracking-wide">ScripOx</span>
        {dockPosition !== 'left' && <ChevronLeft className="w-3.5 h-3.5 text-slate-400" />}
      </button>

      {/* Main Panel */}
      <div
        ref={sidebarRef}
        style={panelStyle}
        className={panelClasses}
        dir="ltr"
      >
        {/* Header with Drag Handle, Theme Toggle & Dock Switchers */}
        <div
          onPointerDown={handlePointerDown}
          onPointerMove={handlePointerMove}
          onPointerUp={handlePointerUp}
          className={`flex items-center justify-between px-4 py-2.5 border-b cursor-grab active:cursor-grabbing select-none transition-colors ${
            isDark
              ? 'border-slate-800/80 bg-[#0A101D]/70'
              : 'border-slate-200 bg-white'
          }`}
        >
          {/* Logo & Drag indicator */}
          <div className="flex items-center gap-2">
            <GripVertical className={`w-3.5 h-3.5 transition-colors ${isDark ? 'text-slate-500 hover:text-slate-300' : 'text-slate-400 hover:text-slate-600'}`} />
            <div className="w-6 h-6 rounded-lg bg-[#0F172A] border border-slate-700 flex items-center justify-center relative shadow-xs">
              <span className="text-white font-bold text-xs">S</span>
              <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 absolute top-0.5 right-0.5" />
            </div>
            <div>
              <span className={`font-bold text-xs tracking-tight block ${isDark ? 'text-white' : 'text-slate-900'}`}>
                ScripOx Hub
              </span>
              <span className={`text-[10px] font-medium ${isDark ? 'text-slate-400' : 'text-slate-500'}`}>Smart Lead Extractor</span>
            </div>
          </div>

          {/* Controls: Theme Switcher + Quick Dock + Close */}
          <div className="flex items-center gap-0.5">
            {/* Theme Toggle (Light / Dark) */}
            <button
              onClick={toggleTheme}
              className={`p-1.5 rounded-lg transition-colors cursor-pointer ${
                isDark
                  ? 'text-amber-400 hover:text-amber-300 hover:bg-slate-800'
                  : 'text-slate-500 hover:text-slate-900 hover:bg-slate-100'
              }`}
              title={isDark ? 'Switch to Light Theme' : 'Switch to Dark Theme'}
            >
              {isDark ? <Sun className="w-3.5 h-3.5" /> : <Moon className="w-3.5 h-3.5" />}
            </button>

            {/* Dock Left Button */}
            <button
              onClick={() => setDockPosition('left')}
              className={`p-1.5 rounded-lg transition-colors cursor-pointer ${
                dockPosition === 'left'
                  ? isDark ? 'bg-slate-800 text-white font-bold' : 'bg-slate-200 text-slate-900 font-bold'
                  : isDark ? 'text-slate-400 hover:text-white hover:bg-slate-800' : 'text-slate-500 hover:text-slate-900 hover:bg-slate-100'
              }`}
              title="Dock Left"
            >
              <PanelLeft className="w-3.5 h-3.5" />
            </button>

            {/* Float / Drag Button */}
            <button
              onClick={() => {
                if (dockPosition !== 'float') {
                  setDockPosition('float');
                  setPanelPosition({ x: 80, y: 50 });
                }
              }}
              className={`p-1.5 rounded-lg transition-colors cursor-pointer ${
                dockPosition === 'float'
                  ? isDark ? 'bg-slate-800 text-white font-bold' : 'bg-slate-200 text-slate-900 font-bold'
                  : isDark ? 'text-slate-400 hover:text-white hover:bg-slate-800' : 'text-slate-500 hover:text-slate-900 hover:bg-slate-100'
              }`}
              title="Free Floating / Drag"
            >
              <Move className="w-3.5 h-3.5" />
            </button>

            {/* Dock Right Button */}
            <button
              onClick={() => setDockPosition('right')}
              className={`p-1.5 rounded-lg transition-colors cursor-pointer ${
                dockPosition === 'right'
                  ? isDark ? 'bg-slate-800 text-white font-bold' : 'bg-slate-200 text-slate-900 font-bold'
                  : isDark ? 'text-slate-400 hover:text-white hover:bg-slate-800' : 'text-slate-500 hover:text-slate-900 hover:bg-slate-100'
              }`}
              title="Dock Right"
            >
              <PanelRight className="w-3.5 h-3.5" />
            </button>

            {/* Close Button */}
            <button
              onClick={toggleSidebar}
              className={`transition-colors p-1.5 rounded-lg cursor-pointer ml-1 ${
                isDark ? 'text-slate-400 hover:text-white hover:bg-slate-800' : 'text-slate-500 hover:text-slate-900 hover:bg-slate-100'
              }`}
              title="Minimize sidebar"
            >
              <X className="w-4 h-4" />
            </button>
          </div>
        </div>

        {/* Toolbar (Smart Scraper, Clean Data, Pick, Dispatch) */}
        <Toolbar />

        {/* Segmented Control Tabs (Calm, Professional) */}
        <div className={`px-2.5 py-1.5 border-b transition-colors ${
          isDark
            ? 'border-slate-800 bg-[#0F172A]'
            : 'border-slate-200 bg-slate-50'
        }`}>
          <div className={`flex gap-1 overflow-x-auto scrollbar-hide p-0.5 rounded-xl border ${
            isDark
              ? 'bg-slate-900 border-slate-800'
              : 'bg-slate-200/60 border-slate-300/60'
          }`}>
            {tabs.map((t) => (
              <button
                key={t.id}
                onClick={() => setTab(t.id)}
                className={`whitespace-nowrap px-2.5 py-1 rounded-lg text-xs font-medium
                            transition-all duration-150 cursor-pointer flex items-center gap-1.5
                            ${tab === t.id
                              ? isDark
                                ? 'bg-slate-800 text-white shadow-xs border border-slate-700'
                                : 'bg-white text-slate-900 shadow-xs border border-slate-200 font-semibold'
                              : isDark
                                ? 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/40'
                                : 'text-slate-600 hover:text-slate-900 hover:bg-white/40'
                            }`}
              >
                <span>{t.label}</span>
                {typeof t.count === 'number' && t.count > 0 && (
                  <span className={`text-[10px] px-1.5 py-0.2 rounded-full font-semibold ${
                    tab === t.id
                      ? isDark ? 'bg-slate-700 text-slate-100' : 'bg-slate-900 text-white'
                      : isDark ? 'bg-slate-800 text-slate-400' : 'bg-slate-200 text-slate-700'
                  }`}>
                    {t.count}
                  </span>
                )}
              </button>
            ))}
          </div>
        </div>

        {/* Tab Content */}
        <div className={`flex-1 overflow-y-auto custom-scrollbar transition-colors ${
          isDark ? 'bg-[#0B1323]/50 text-slate-100' : 'bg-[#F8FAFC] text-slate-900'
        }`}>
          {tab === 'data'       && <DataPreview />}
          {tab === 'selector'   && <SelectorPanel />}
          {tab === 'fields'     && <FieldMapper />}
          {tab === 'table'      && <TablePicker />}
          {tab === 'pagination' && <PaginationPanel />}
          {tab === 'templates'  && <TemplateManager />}
          {tab === 'export'     && <ExportPanel />}
        </div>
      </div>
    </>
  );
}
