import locale

def fix_file(f):
    enc = locale.getpreferredencoding()
    try:
        with open(f, encoding='utf-8') as file:
            c = file.read()
    except UnicodeDecodeError:
        with open(f, encoding=enc) as file:
            c = file.read()
            
    c = c.replace('<data name=" exports.page_sub\\><value>', '<data name="exports.page_sub"><value>')
    
    with open(f, 'w', encoding='utf-8') as file:
        file.write(c)

for f in ['h:/CRM MINI/ScripOx.Desktop/Resources/i18n/ar.resx', 'h:/CRM MINI/ScripOx.Desktop/Resources/i18n/de.resx', 'h:/CRM MINI/ScripOx.Desktop/Resources/i18n/en.resx']:
    fix_file(f)
