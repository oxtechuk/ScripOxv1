# دليل إزالة تحذيرات Windows Defender و SmartScreen لبرنامج ScripOx CRM

هذا الدليل يوضح لك بالخطوات العملية كيفية جعل برنامج **ScripOx** موثوقاً 100% على أي جهاز ويندوز دون أن تظهر رسالة "برنامج ضار" أو "Windows protected your PC".

---

## 1. التعديلات التقنية التي تم تطبيقها بالفعل داخل المشروع

تم تنفيذ الخطوات التالية لمنع إطلاق إنذارات الفيروسات الكاذبة (False Positives):

1. **إزالة سكريبت PowerShell الخفي من ملف التثبيت ([installer.iss](file:///c:/wamp64/www/ScripOxv1/installer.iss))**:
   - كان ملف التثبيت ينفذ أمراً خفياً عبر PowerShell مع `ExecutionPolicy Bypass` لتحميل مكونات في مجلد Temp، وهو السلوك رقم #1 الذي يرصده Windows Defender كنشاط تروجان. تم حذفه بالكامل، وأصبح التطبيق يعتمد على المعالجة النظيفة داخل C#.
2. **تحويل PyInstaller إلى وضع المجلد الآمن (`--onedir`) ([build_backend.py](file:///c:/wamp64/www/ScripOxv1/ScripOx.Extractor/build_backend.py))**:
   - وضع `--onefile` القديم كان يفك ضغط ملفات البايثون في `AppData\Local\Temp` عند كل تشغيل (سلوك الفيروسات Dropper). تم تحويله إلى `--onedir` ليعمل في مكانه الأصلي بثبات وسرعة ودون إثارة الشبهات.
3. **إضافة بيانات الهوية والـ Metadata الكاملة ([file_version_info.txt](file:///c:/wamp64/www/ScripOxv1/ScripOx.Extractor/file_version_info.txt))**:
   - تم ربط اسم الشركة `Ox Tech`، واسم المنتج `ScripOx CRM`، ورقم الإصدار `1.1.3.0`، والأيقونة الرسمية بملف الـ Backend والـ Installer.
4. **سكريبت التوقيع الرقمي المعتمد ([sign_release.ps1](file:///c:/wamp64/www/ScripOxv1/sign_release.ps1))**:
   - سكريبت يقوم بتوقيع ملفات البرنامج و الـ Installer تلقائياً بختم SHA-256 و Timestamp معتمد أثناء الـ Build.

---

## 2. الخطوة الذهبية: رفع الملف لمايكروسوفت (False Positive Whitelist)

تتيح مايكروسوفت للمطورين خدمة مجانية لفحص ملف الـ Exe في معاملها وإصدار استثناء رسمي (Whitelist) لـ **Windows Defender** خلال 2-6 ساعات:

### الخطوات:
1. افتح بوابة المطورين الرسمية من مايكروسوفت:
   👉 **[Microsoft Security Intelligence - File Submission](https://www.microsoft.com/en-us/wdsi/filesubmission)**
2. سجّل الدخول بحساب Microsoft (أو أنشئ حساباً مجانياً).
3. اختر نوع الرفع: **Software Developer**.
4. ارفع ملف التثبيت: `dist\installer\ScripOx-Setup-v1.1.3.exe` (أو ارفعه كملف مضغوط Zip إذا كان حجمه كبيراً).
5. في حقول البيانات، املأ التالي:
   - **Company name**: `Ox Tech`
   - **Product name**: `ScripOx CRM`
   - **File was detected as**: اكتب اسم الإنذار (مثال: `Trojan:Win32/Wacatac.B!ml` أو `Unrecognized application`).
   - **Comments**: انسخ النص التالي بالإنجليزية:
     ```text
     Hello Microsoft Security Team,
     This is a false positive detection for our legitimate business application "ScripOx CRM", developed by Ox Tech (https://oxtech.uk).
     The application is a customer relationship and market intelligence platform built with .NET 8 WPF and Python FastAPI.
     It contains no malicious payloads or unwanted software.
     Please analyze and add this build to the Microsoft Defender whitelist.
     Thank you.
     ```
6. اضغط **Submit**.
7. ستستلم رقم تذكرة (Submission ID)، وخلال بضع ساعات سيتم فحص الملف وتحديث قاعدة بيانات تعريفات Windows Defender عبر السحابة لجميع أجهزة ويندوز تلقائياً.

---

## 3. حل نافذة SmartScreen الزرقاء ("Windows protected your PC")

نافذة SmartScreen لا تعني وجود فيروس، بل تعني: **"هذا الملف لم يقم بتحميله عدد كبير من الأشخاص بعد" (Lack of Reputation)**.

### الحل الفوري مع العملاء:
أخبر العميل بالضغط على:
1. **More info** (مزيد من المعلومات).
2. ثم زر **Run anyway** (تشغيل على أية حال).
*(البرنامج سيعمل فوراً ولن يسأله مرة ثانية)*.

### الحل التلقائي عبر ملف Whitelist المرفق:
قم بإرسال ملف [Whitelist_And_Install.bat](file:///c:/wamp64/www/ScripOxv1/Whitelist_And_Install.bat) بجانب ملف التثبيت. بمجرد أن يفتحه العميل بنقرة واحدة، يقوم فوراً بإزالة حظر التحميل (Zone.Identifier) من ملف الـ Setup.

### الحل الجذري التجاري (عند التوسع):
شراء شهادة توقيع كود معتمدة (**Code Signing Certificate**) مثل:
- **Certum Standard Code Signing** (أقل تكلفة وتبدأ من حوالي $65-$80 سنوياً).
- عند توقيع البرنامج بها عبر `sign_release.ps1`، يختفي اسم "Unknown Publisher" نهائياً ويظهر اسم شركتك `Ox Tech` بشكل موثق.

---

## 4. كيفية بناء وتوقيع النسخة الجديدة الآن

كل ما عليك فعله لبناء النسخة المحسنة هو تشغيل الأمر التالي في PowerShell:
```powershell
.\build_release.ps1 -Version "1.1.3"
```
سيقوم السكريبت تلقائياً بـ:
1. تجهيز الكروم إكستنشن.
2. بناء تطبيق C# WPF بنجاح.
3. تجميع بايثون بوضع `--onedir` النظيف الآمن.
4. بناء الـ Installer النظيف بدون أوامر PowerShell خفية.
5. توقيع جميع الملفات التنفيذية وحفظ الشهادة العامة في مجلد `certificates/`.
6. تجهيز ملف الـ ZIP وملفات التحقق SHA256.
