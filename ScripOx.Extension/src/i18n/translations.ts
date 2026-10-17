export type Language = 'en' | 'de' | 'ar';

export interface Translations {
  appName: string;
  selectLanguage: string;
  chooseLanguagePrompt: string;
  welcomeTitle: string;
  welcomeDesc: string;
  statusChecking: string;
  statusConnected: string;
  statusDisconnected: string;
  startPicker: string;
  openSidebar: string;
  downloadDesktop: string;
  settings: string;
  shortcutsTitle: string;
  shortcutPicker: string;
  shortcutSidebar: string;
  footerTagline: string;
  save: string;
}

export const translations: Record<Language, Translations> = {
  en: {
    appName: 'ScripOx',
    selectLanguage: 'Language',
    chooseLanguagePrompt: 'Choose your preferred language',
    welcomeTitle: 'Welcome to ScripOx',
    welcomeDesc: 'Select your preferred language to get started with visual data extraction.',
    statusChecking: 'Checking Desktop Link…',
    statusConnected: 'Desktop Connected',
    statusDisconnected: 'Desktop Standby — Open ScripOx',
    startPicker: 'Start Visual Picker',
    openSidebar: 'Open Data Sidebar',
    downloadDesktop: 'Download Desktop App',
    settings: 'Extension Settings',
    shortcutsTitle: 'Quick Shortcuts',
    shortcutPicker: 'Element Picker',
    shortcutSidebar: 'Toggle Sidebar',
    footerTagline: 'Ox Tech UK • Engineered for Enterprise Intelligence',
    save: 'Confirm & Continue',
  },
  de: {
    appName: 'ScripOx',
    selectLanguage: 'Sprache',
    chooseLanguagePrompt: 'Wählen Sie Ihre bevorzugte Sprache',
    welcomeTitle: 'Willkommen bei ScripOx',
    welcomeDesc: 'Wählen Sie Ihre bevorzugte Sprache für die visuelle Datenextraktion.',
    statusChecking: 'Desktop-Verbindung prüfen…',
    statusConnected: 'Desktop verbunden',
    statusDisconnected: 'Desktop Standby — ScripOx öffnen',
    startPicker: 'Visuelle Auswahl starten',
    openSidebar: 'Daten-Seitenleiste öffnen',
    downloadDesktop: 'Desktop-App herunterladen',
    settings: 'Erweiterungseinstellungen',
    shortcutsTitle: 'Tastenkombinationen',
    shortcutPicker: 'Element-Auswahl',
    shortcutSidebar: 'Seitenleiste umschalten',
    footerTagline: 'Ox Tech UK • Entwickelt für Enterprise Intelligence',
    save: 'Bestätigen & Weiter',
  },
  ar: {
    appName: 'ScripOx',
    selectLanguage: 'اللغة',
    chooseLanguagePrompt: 'اختر لغتك المفضلة',
    welcomeTitle: 'مرحباً بك في ScripOx',
    welcomeDesc: 'اختر لغتك المفضلة لبدء استخراج البيانات وتحديد العناصر بصرياً.',
    statusChecking: 'جارٍ التحقق من الاتصال بالديسكتوب…',
    statusConnected: 'متصل ببرنامج الديسكتوب',
    statusDisconnected: 'الديسكتوب في وضع الاستعداد — افتح ScripOx',
    startPicker: 'بدء أداة التحديد البصري',
    openSidebar: 'فتح شريط البيانات الجانبي',
    downloadDesktop: 'تحميل تطبيق الديسكتوب',
    settings: 'إعدادات الإضافة',
    shortcutsTitle: 'الاختصارات السريعة',
    shortcutPicker: 'محدد العناصر',
    shortcutSidebar: 'إظهار/إخفاء الشريط الجانبي',
    footerTagline: 'Ox Tech UK • مصمم لذكاء وتحليل بيانات الأعمال',
    save: 'تأكيد ومتابعة',
  },
};
