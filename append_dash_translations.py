import os

translations = {
    'h:/CRM MINI/ScripOx.Desktop/Resources/i18n/en.resx': """
  <data name="title.dashboard"><value>Dashboard</value></data>
  <data name="title.jobs"><value>Extraction Jobs</value></data>
  <data name="title.raw_records"><value>Raw Records</value></data>
  <data name="title.companies"><value>Companies</value></data>
  <data name="title.map"><value>Map Explorer</value></data>
  <data name="title.exports"><value>Exports</value></data>
  <data name="title.settings"><value>Settings</value></data>
  
  <data name="nav.dashboard"><value>Dashboard</value></data>
  <data name="nav.companies"><value>Companies</value></data>
  <data name="nav.sources"><value>Sources</value></data>
  <data name="nav.jobs"><value>Jobs</value></data>
  <data name="nav.raw_records"><value>Raw Records</value></data>
  <data name="nav.map"><value>Map Explorer</value></data>
  <data name="nav.exports"><value>Exports</value></data>
  <data name="nav.settings"><value>Settings</value></data>
  
  <data name="search.suggestions_live"><value>Live Matches</value></data>
  <data name="search.matches"><value>Matches</value></data>
  <data name="search.no_matches"><value>No Matches</value></data>
  
  <data name="auth.logout"><value>Sign Out</value></data>
  <data name="role.super_admin"><value>Super Admin</value></data>
  <data name="role.admin"><value>Admin</value></data>

  <data name="dash.title"><value>Dashboard Overview</value></data>
  <data name="dash.subtitle"><value>Real-time extraction intelligence and portfolio metrics</value></data>
  <data name="dash.view_all"><value>Live Overview</value></data>
  <data name="dash.total_companies"><value>TOTAL COMPANIES</value></data>
  <data name="dash.extraction_jobs"><value>EXTRACTION JOBS</value></data>
  <data name="dash.raw_staging"><value>RAW STAGING</value></data>
  <data name="dash.data_sources"><value>DATA SOURCES</value></data>
  <data name="dash.portfolio_status"><value>Portfolio by Status</value></data>
  <data name="dash.recent_activities"><value>Recent Company Activities</value></data>
  <data name="dash.showing_latest"><value>Showing latest verified entities</value></data>
</root>""",

    'h:/CRM MINI/ScripOx.Desktop/Resources/i18n/de.resx': """
  <data name="title.dashboard"><value>Dashboard</value></data>
  <data name="title.jobs"><value>Extraktionsjobs</value></data>
  <data name="title.raw_records"><value>Rohdaten</value></data>
  <data name="title.companies"><value>Unternehmen</value></data>
  <data name="title.map"><value>Karten-Explorer</value></data>
  <data name="title.exports"><value>Exporte</value></data>
  <data name="title.settings"><value>Einstellungen</value></data>
  
  <data name="nav.dashboard"><value>Dashboard</value></data>
  <data name="nav.companies"><value>Unternehmen</value></data>
  <data name="nav.sources"><value>Quellen</value></data>
  <data name="nav.jobs"><value>Jobs</value></data>
  <data name="nav.raw_records"><value>Rohdaten</value></data>
  <data name="nav.map"><value>Karten-Explorer</value></data>
  <data name="nav.exports"><value>Exporte</value></data>
  <data name="nav.settings"><value>Einstellungen</value></data>
  
  <data name="search.suggestions_live"><value>Live-Ergebnisse</value></data>
  <data name="search.matches"><value>Treffer</value></data>
  <data name="search.no_matches"><value>Keine Treffer</value></data>
  
  <data name="auth.logout"><value>Abmelden</value></data>
  <data name="role.super_admin"><value>Super-Admin</value></data>
  <data name="role.admin"><value>Admin</value></data>

  <data name="dash.title"><value>Dashboard Übersicht</value></data>
  <data name="dash.subtitle"><value>Echtzeit-Extraktionsintelligenz und Portfolio-Metriken</value></data>
  <data name="dash.view_all"><value>Live-Übersicht</value></data>
  <data name="dash.total_companies"><value>UNTERNEHMEN GESAMT</value></data>
  <data name="dash.extraction_jobs"><value>EXTRAKTIONSJOBS</value></data>
  <data name="dash.raw_staging"><value>ROHDATEN</value></data>
  <data name="dash.data_sources"><value>DATENQUELLEN</value></data>
  <data name="dash.portfolio_status"><value>Portfolio nach Status</value></data>
  <data name="dash.recent_activities"><value>Aktuelle Unternehmensaktivitäten</value></data>
  <data name="dash.showing_latest"><value>Zeige neueste verifizierte Einträge</value></data>
</root>""",

    'h:/CRM MINI/ScripOx.Desktop/Resources/i18n/ar.resx': """
  <data name="title.dashboard"><value>لوحة التحكم</value></data>
  <data name="title.jobs"><value>مهام الاستخراج</value></data>
  <data name="title.raw_records"><value>السجلات الخام</value></data>
  <data name="title.companies"><value>الشركات</value></data>
  <data name="title.map"><value>مستكشف الخريطة</value></data>
  <data name="title.exports"><value>التصدير</value></data>
  <data name="title.settings"><value>الإعدادات</value></data>
  
  <data name="nav.dashboard"><value>لوحة التحكم</value></data>
  <data name="nav.companies"><value>الشركات</value></data>
  <data name="nav.sources"><value>المصادر</value></data>
  <data name="nav.jobs"><value>المهام</value></data>
  <data name="nav.raw_records"><value>السجلات الخام</value></data>
  <data name="nav.map"><value>مستكشف الخريطة</value></data>
  <data name="nav.exports"><value>التصدير</value></data>
  <data name="nav.settings"><value>الإعدادات</value></data>
  
  <data name="search.suggestions_live"><value>نتائج فورية</value></data>
  <data name="search.matches"><value>مطابقات</value></data>
  <data name="search.no_matches"><value>لا توجد نتائج</value></data>
  
  <data name="auth.logout"><value>تسجيل الخروج</value></data>
  <data name="role.super_admin"><value>مدير النظام</value></data>
  <data name="role.admin"><value>مدير</value></data>

  <data name="dash.title"><value>حركة العملاء والشركات</value></data>
  <data name="dash.subtitle"><value>ذكاء استخراج وتحليل بيانات الشركات والعملاء لحظياً</value></data>
  <data name="dash.view_all"><value>نظرة عامة حية</value></data>
  <data name="dash.total_companies"><value>إجمالي الشركات</value></data>
  <data name="dash.extraction_jobs"><value>مهام الاستخراج</value></data>
  <data name="dash.raw_staging"><value>السجلات الخام</value></data>
  <data name="dash.data_sources"><value>مصادر البيانات</value></data>
  <data name="dash.portfolio_status"><value>العملاء حسب الحالة</value></data>
  <data name="dash.recent_activities"><value>أحدث الأنشطة</value></data>
  <data name="dash.showing_latest"><value>عرض أحدث السجلات المعتمدة</value></data>
</root>"""
}

for filepath, content in translations.items():
    with open(filepath, 'r', encoding='utf-8') as f:
        original = f.read()
    
    new_content = original.replace('</root>', content)
    
    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(new_content)
