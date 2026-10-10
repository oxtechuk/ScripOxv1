import os

# Dictionary of file paths and their respective translations to append
translations = {
    'h:/CRM MINI/ScripOx.Desktop/Resources/i18n/en.resx': """
  <data name="exports.page_title"><value>Step-by-step Advertising Data Export and Matching Wizard</value></data>
  <data name="exports.page_sub"><value>Step-by-step data export and matching wizard with official platform templates.</value></data>
  <data name="exports.wizard_title"><value>Export &amp; Filtering Wizard</value></data>
  <data name="exports.wizard_sub"><value>Select Group, Category, Priority, and Export Type</value></data>
  
  <data name="exports.step1"><value>Group / Source:</value></data>
  <data name="exports.step2"><value>Category / Sector:</value></data>
  <data name="exports.step3"><value>Lead Priority:</value></data>
  <data name="exports.step4"><value>Export Type &amp; Target Template:</value></data>
  
  <data name="exports.filter_all_groups"><value>All (All Groups and Sources)</value></data>
  <data name="exports.filter_all_categories"><value>All (All Categories and Sectors)</value></data>
  <data name="exports.filter_all_priorities"><value>All (All Priorities)</value></data>
  <data name="exports.filter_all_statuses"><value>All (All Statuses)</value></data>

  <data name="exports.priority_high"><value>★★★ High Priority</value></data>
  <data name="exports.priority_medium"><value>★★ Medium Priority</value></data>
  <data name="exports.priority_low"><value>★ Low Priority</value></data>
  
  <data name="exports.chk_contact"><value>Exclude records without contact info (phone/email)</value></data>
  <data name="exports.lbl_status"><value>Status Filter:</value></data>
  
  <data name="exports.btn_copy_csv"><value>📋 Copy displayed data to clipboard (CSV)</value></data>
  <data name="exports.backup_title"><value>Database Backup</value></data>
  <data name="exports.backup_desc"><value>Create a full backup archive of the ScripOx SQLite database.</value></data>
  <data name="exports.btn_backup"><value>Take Backup (.db)</value></data>
  
  <data name="exports.refresh"><value>🔄 Refresh</value></data>
  <data name="exports.copy_csv"><value>📋 Copy CSV</value></data>
  <data name="exports.export_now"><value>⬇ Export Now</value></data>
  
  <data name="exports.records_count"><value>Matched Records: </value></data>
  <data name="exports.ltv"><value>  •  Estimated Total Value (LTV): </value></data>

  <data name="exports.meta_title"><value>Live Preview: Meta Custom Audience</value></data>
  <data name="exports.meta_sub"><value>Value-Based Audience format 100% compatible with official Meta template</value></data>
  <data name="exports.meta_spec"><value>19 columns perfectly matched with Meta Custom Audiences specs</value></data>
  <data name="exports.meta_btn"><value>⬇ Export Meta Ads CSV</value></data>
  <data name="exports.meta_combo"><value>🎯 Meta Ads — Value-Based Audience (19 cols)</value></data>

  <data name="exports.google_title"><value>Live Preview: Google Ads Customer Match</value></data>
  <data name="exports.google_sub"><value>Official Customer Match template for Google Ads (Search, YouTube &amp; Display)</value></data>
  <data name="exports.google_spec"><value>International E.164 phone formatting and hashed email support</value></data>
  <data name="exports.google_btn"><value>⬇ Export Google Ads Customer Match</value></data>
  <data name="exports.google_combo"><value>🔍 Google Ads — Customer Match List (4 cols)</value></data>

  <data name="exports.mail_title"><value>Live Preview: Email Marketing List</value></data>
  <data name="exports.mail_sub"><value>Cleaned contact list ready for direct import to Mailchimp and Brevo</value></data>
  <data name="exports.mail_spec"><value>8 columns including name, company, sector, and status details</value></data>
  <data name="exports.mail_btn"><value>⬇ Export Email Marketing CSV</value></data>
  <data name="exports.mail_combo"><value>📧 Email Marketing — Mailchimp &amp; Brevo (8 cols)</value></data>

  <data name="exports.excel_title"><value>Live Preview: Business Dossier (.xlsx)</value></data>
  <data name="exports.excel_sub"><value>Official multi-sheet Excel file containing company cards and KPI summaries</value></data>
  <data name="exports.excel_spec"><value>GoBD compliant export with native localization support</value></data>
  <data name="exports.excel_btn"><value>⬇ Generate Full Excel Dossier (.xlsx)</value></data>
  <data name="exports.excel_combo"><value>📊 Excel Business Dossier — Official Report (.xlsx)</value></data>

  <data name="exports.contact"><value>contacts</value></data>
  <data name="exports.subscriber"><value>subscribers</value></data>
  <data name="exports.company_count"><value>companies</value></data>
</root>""",

    'h:/CRM MINI/ScripOx.Desktop/Resources/i18n/de.resx': """
  <data name="exports.page_title"><value>Schritt-für-Schritt Export- &amp; Abgleich-Assistent</value></data>
  <data name="exports.page_sub"><value>Schritt-für-Schritt Datenexport- und Abgleich-Assistent mit offiziellen Plattformvorlagen.</value></data>
  <data name="exports.wizard_title"><value>Export- &amp; Filter-Assistent</value></data>
  <data name="exports.wizard_sub"><value>Wählen Sie Gruppe, Kategorie, Priorität und Exporttyp</value></data>
  
  <data name="exports.step1"><value>Gruppe / Quelle:</value></data>
  <data name="exports.step2"><value>Kategorie / Sektor:</value></data>
  <data name="exports.step3"><value>Priorität:</value></data>
  <data name="exports.step4"><value>Exporttyp &amp; Zielvorlage:</value></data>
  
  <data name="exports.filter_all_groups"><value>Alle (Alle Gruppen und Quellen)</value></data>
  <data name="exports.filter_all_categories"><value>Alle (Alle Kategorien und Sektoren)</value></data>
  <data name="exports.filter_all_priorities"><value>Alle (Alle Prioritäten)</value></data>
  <data name="exports.filter_all_statuses"><value>Alle (Alle Status)</value></data>

  <data name="exports.priority_high"><value>★★★ Hohe Priorität</value></data>
  <data name="exports.priority_medium"><value>★★ Mittlere Priorität</value></data>
  <data name="exports.priority_low"><value>★ Niedrige Priorität</value></data>
  
  <data name="exports.chk_contact"><value>Datensätze ohne Kontaktinfos (Telefon/E-Mail) ausschließen</value></data>
  <data name="exports.lbl_status"><value>Statusfilter:</value></data>
  
  <data name="exports.btn_copy_csv"><value>📋 Angezeigte Daten in Zwischenablage kopieren (CSV)</value></data>
  <data name="exports.backup_title"><value>Datenbank-Backup</value></data>
  <data name="exports.backup_desc"><value>Erstellen Sie ein vollständiges Backup-Archiv der ScripOx SQLite-Datenbank.</value></data>
  <data name="exports.btn_backup"><value>Backup erstellen (.db)</value></data>
  
  <data name="exports.refresh"><value>🔄 Aktualisieren</value></data>
  <data name="exports.copy_csv"><value>📋 CSV Kopieren</value></data>
  <data name="exports.export_now"><value>⬇ Jetzt exportieren</value></data>
  
  <data name="exports.records_count"><value>Gefundene Datensätze: </value></data>
  <data name="exports.ltv"><value>  •  Geschätzter Gesamtwert (LTV): </value></data>

  <data name="exports.meta_title"><value>Live-Vorschau: Meta Custom Audience</value></data>
  <data name="exports.meta_sub"><value>Value-Based Audience Format, 100% kompatibel mit der offiziellen Meta-Vorlage</value></data>
  <data name="exports.meta_spec"><value>19 Spalten, exakt abgestimmt auf die Meta Custom Audiences Spezifikationen</value></data>
  <data name="exports.meta_btn"><value>⬇ Meta Ads CSV exportieren</value></data>
  <data name="exports.meta_combo"><value>🎯 Meta Ads — Value-Based Audience (19 Spalten)</value></data>

  <data name="exports.google_title"><value>Live-Vorschau: Google Ads Customer Match</value></data>
  <data name="exports.google_sub"><value>Offizielle Vorlage für den Kundenabgleich bei Google Ads</value></data>
  <data name="exports.google_spec"><value>Internationale E.164-Telefonformatierung und Unterstützung für gehashte E-Mails</value></data>
  <data name="exports.google_btn"><value>⬇ Google Ads Customer Match exportieren</value></data>
  <data name="exports.google_combo"><value>🔍 Google Ads — Customer Match List (4 Spalten)</value></data>

  <data name="exports.mail_title"><value>Live-Vorschau: E-Mail-Marketing-Liste</value></data>
  <data name="exports.mail_sub"><value>Bereinigte Kontaktliste, bereit für den Direktimport zu Mailchimp und Brevo</value></data>
  <data name="exports.mail_spec"><value>8 Spalten mit Details zu Name, Unternehmen, Branche und Status</value></data>
  <data name="exports.mail_btn"><value>⬇ E-Mail-Marketing CSV exportieren</value></data>
  <data name="exports.mail_combo"><value>📧 E-Mail Marketing — Mailchimp &amp; Brevo (8 Spalten)</value></data>

  <data name="exports.excel_title"><value>Live-Vorschau: Business Dossier (.xlsx)</value></data>
  <data name="exports.excel_sub"><value>Offizielle mehrblättrige Excel-Datei mit Unternehmenskarten und KPI-Übersichten</value></data>
  <data name="exports.excel_spec"><value>GoBD-konformer Export mit nativer Lokalisierungsunterstützung</value></data>
  <data name="exports.excel_btn"><value>⬇ Vollständiges Excel-Dossier erstellen (.xlsx)</value></data>
  <data name="exports.excel_combo"><value>📊 Excel Business Dossier — Offizieller Bericht (.xlsx)</value></data>

  <data name="exports.contact"><value>Kontakte</value></data>
  <data name="exports.subscriber"><value>Abonnenten</value></data>
  <data name="exports.company_count"><value>Unternehmen</value></data>
</root>""",

    'h:/CRM MINI/ScripOx.Desktop/Resources/i18n/ar.resx': """
  <data name="exports.page_title"><value>معالج تصدير ومطابقة البيانات الإعلانية خطوة بخطوة</value></data>
  <data name="exports.page_sub"><value>معالج تصدير ومطابقة البيانات الإعلانية خطوة بخطوة مع قوالب المنصات الرسمية.</value></data>
  <data name="exports.wizard_title"><value>معالج الفرز والتصدير (Wizard)</value></data>
  <data name="exports.wizard_sub"><value>اختر المجموعة ثم الفئة ثم الأولوية ثم نوع التصدير</value></data>
  
  <data name="exports.step1"><value>المجموعة / المصدر (Group / Source):</value></data>
  <data name="exports.step2"><value>الفئة / التصنيف (Category / Sector):</value></data>
  <data name="exports.step3"><value>درجة الأولوية (Lead Priority):</value></data>
  <data name="exports.step4"><value>نوع التصدير والقالب المستهدف (Target Template):</value></data>
  
  <data name="exports.filter_all_groups"><value>الكل (جميع المجموعات والمصادر)</value></data>
  <data name="exports.filter_all_categories"><value>الكل (جميع الفئات والتصنيفات)</value></data>
  <data name="exports.filter_all_priorities"><value>الكل (جميع درجات الأولوية)</value></data>
  <data name="exports.filter_all_statuses"><value>الكل (جميع الحالات)</value></data>

  <data name="exports.priority_high"><value>★★★ أولوية عالية</value></data>
  <data name="exports.priority_medium"><value>★★ أولوية متوسطة</value></data>
  <data name="exports.priority_low"><value>★ أولوية منخفضة</value></data>
  
  <data name="exports.chk_contact"><value>استبعاد السجلات بدون وسيلة اتصال (هاتف أو إيميل)</value></data>
  <data name="exports.lbl_status"><value>فلترة الحالة:</value></data>
  
  <data name="exports.btn_copy_csv"><value>📋 نسخ البيانات المعروضة إلى الحافظة (CSV)</value></data>
  <data name="exports.backup_title"><value>نسخة احتياطية لقاعدة البيانات</value></data>
  <data name="exports.backup_desc"><value>إنشاء أرشيف احتياطي كامل لقاعدة بيانات ScripOx SQLite.</value></data>
  <data name="exports.btn_backup"><value>أخذ نسخة احتياطية (.db)</value></data>
  
  <data name="exports.refresh"><value>🔄 تحديث</value></data>
  <data name="exports.copy_csv"><value>📋 نسخ CSV</value></data>
  <data name="exports.export_now"><value>⬇ تصدير الآن</value></data>
  
  <data name="exports.records_count"><value>عدد السجلات المطابقة: </value></data>
  <data name="exports.ltv"><value>  •  إجمالي القيمة التقديرية (LTV): </value></data>

  <data name="exports.meta_title"><value>معاينة حية لملف جمهور ميتا (Meta Custom Audience)</value></data>
  <data name="exports.meta_sub"><value>صيغة الجمهور المخصص المبني على القيمة (Value-Based Audience) متطابقة 100% مع تمبلت ميتا الرسمي</value></data>
  <data name="exports.meta_spec"><value>19 أعمدة متطابقة تماماً مع مواصفات Meta Custom Audiences</value></data>
  <data name="exports.meta_btn"><value>⬇ تصدير ملف ميتا Meta Ads CSV</value></data>
  <data name="exports.meta_combo"><value>🎯 Meta Ads — Value-Based Audience (19 عمود ميتا)</value></data>

  <data name="exports.google_title"><value>معاينة حية لقالب Google Ads Customer Match</value></data>
  <data name="exports.google_sub"><value>قالب مطابقة العملاء الرسمي لمنصة Google Ads (Search, YouTube &amp; Display Campaigns)</value></data>
  <data name="exports.google_spec"><value>تنسيق أرقام الهواتف الدولي E.164 وترميز البريد الإلكتروني المعتمد</value></data>
  <data name="exports.google_btn"><value>⬇ تصدير Google Ads Customer Match</value></data>
  <data name="exports.google_combo"><value>🔍 Google Ads — Customer Match List (4 أعمدة)</value></data>

  <data name="exports.mail_title"><value>معاينة حية لقائمة البريد التسويقي (Mailchimp / Brevo)</value></data>
  <data name="exports.mail_sub"><value>قائمة جهات الاتصال المفروزة والجاهزة للاستيراد المباشر في Mailchimp و Brevo و CRM</value></data>
  <data name="exports.mail_spec"><value>8 أعمدة تشمل تفاصيل الاسم والشركة والتصنيف وحالة العميل</value></data>
  <data name="exports.mail_btn"><value>⬇ تصدير قائمة Email Marketing CSV</value></data>
  <data name="exports.mail_combo"><value>📧 Email Marketing — Mailchimp &amp; Brevo (8 أعمدة)</value></data>

  <data name="exports.excel_title"><value>معاينة تقرير إكسل التنفيذي الشامل (Business Dossier)</value></data>
  <data name="exports.excel_sub"><value>ملف إكسل رسمي متعدد الصفحات يتضمن بطاقات الشركات وملخص المؤشرات</value></data>
  <data name="exports.excel_spec"><value>تصدير متوافق مع معايير التدقيق GoBD واللغة العربية/الإنجليزية</value></data>
  <data name="exports.excel_btn"><value>⬇ توليد ملف الإكسل الكامل (.xlsx)</value></data>
  <data name="exports.excel_combo"><value>📊 Excel Business Dossier — تقرير إكسل رسمي (.xlsx)</value></data>

  <data name="exports.contact"><value>جهة اتصال</value></data>
  <data name="exports.subscriber"><value>مشترك</value></data>
  <data name="exports.company_count"><value>شركة</value></data>
</root>"""
}

for filepath, content in translations.items():
    with open(filepath, 'r', encoding='utf-8') as f:
        original = f.read()
    
    # Replace the </root> at the end with the new content
    new_content = original.replace('</root>', content)
    
    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(new_content)
