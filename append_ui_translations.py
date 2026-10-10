import os

translations = {
    'h:/CRM MINI/ScripOx.Desktop/Resources/i18n/en.resx': """
  <data name="title.sources"><value>Data Sources</value></data>
  <data name="search.placeholder"><value>Search...</value></data>
  <data name="btn.create_record"><value>+ New Record</value></data>
  <data name="nav.extension"><value>Browser Extension</value></data>
</root>""",

    'h:/CRM MINI/ScripOx.Desktop/Resources/i18n/de.resx': """
  <data name="title.sources"><value>Datenquellen</value></data>
  <data name="search.placeholder"><value>Suchen...</value></data>
  <data name="btn.create_record"><value>+ Neuer Eintrag</value></data>
  <data name="nav.extension"><value>Browser-Erweiterung</value></data>
</root>""",

    'h:/CRM MINI/ScripOx.Desktop/Resources/i18n/ar.resx': """
  <data name="title.sources"><value>مصادر البيانات</value></data>
  <data name="search.placeholder"><value>بحث...</value></data>
  <data name="btn.create_record"><value>+ سجل جديد</value></data>
  <data name="nav.extension"><value>إضافة المتصفح</value></data>
</root>"""
}

for filepath, content in translations.items():
    with open(filepath, 'r', encoding='utf-8') as f:
        original = f.read()
    
    new_content = original.replace('</root>', content)
    
    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(new_content)
