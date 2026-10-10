# متطلبات نظام التراخيص (License API Requirements) لمطور Laravel

مرحباً،
نحتاج إلى بناء نظام تفعيل تراخيص (License Activation System) في مشروع Laravel الخاص بموقع `oxtech.uk`. هذا النظام سيتخاطب مباشرة مع برنامج Desktop (مبني بـ C# WPF) للتحقق من التراخيص.

إليك التفاصيل التقنية الدقيقة المطلوبة:

---

## 1. تصميم قاعدة البيانات (Database Schema)

نحتاج إلى جدولين أساسيين:

### الجدول الأول: `licenses` (التراخيص)
يخزن بيانات المفاتيح المُصدرة للعملاء.
```php
Schema::create('licenses', function (Blueprint $table) {
    $table->id();
    $table->foreignId('user_id')->nullable()->constrained(); // اختياري لربط الرخصة بحساب عميل
    $table->string('license_key')->unique(); // المفتاح نفسه، مثال: XXXX-XXXX-XXXX-XXXX
    $table->integer('max_devices')->default(1); // أقصى عدد أجهزة مسموح بها للمفتاح
    $table->enum('status', ['active', 'revoked', 'expired'])->default('active'); // حالة المفتاح
    $table->dateTime('expires_at')->nullable(); // تاريخ الانتهاء
    $table->timestamps();
});
```

### الجدول الثاني: `license_devices` (الأجهزة المفعلة)
يخزن بيانات الأجهزة التي قامت بتفعيل الرخصة لضمان عدم مشاركة المفتاح لأكثر من الحد المسموح.
```php
Schema::create('license_devices', function (Blueprint $table) {
    $table->id();
    $table->foreignId('license_id')->constrained()->cascadeOnDelete();
    $table->string('hardware_id'); // المعرف الفريد للجهاز (يُرسل من برنامج الديسكتوب)
    $table->dateTime('last_checked_at')->nullable(); // آخر مرة تم التحقق فيها من الجهاز
    $table->timestamps();
    
    // منع تكرار نفس الجهاز لنفس الرخصة
    $table->unique(['license_id', 'hardware_id']);
});
```

---

## 2. الواجهات البرمجية (API Endpoints)

يجب إنشاء مسارين (Routes) تحت النطاق `http://oxtech.uk/api/license/...`
يرجى التأكد من أن هذه المسارات تستقبل وتعيد البيانات بصيغة `JSON`.

### أ. واجهة التفعيل (Activation Endpoint)
- **الرابط:** `POST http://oxtech.uk/api/license/activate`
- **البيانات المستلمة (Request Body):**
  ```json
  {
      "license_key": "XXXX-XXXX-XXXX-XXXX",
      "hardware_id": "ABCDEF1234567890"
  }
  ```
- **المنطق المطلوب (Logic):**
  1. ابحث عن `license_key` في جدول التراخيص.
  2. تأكد أن حالته `active` ولم يتجاوز تاريخ الانتهاء `expires_at`.
  3. إذا كان الجهاز (`hardware_id`) مسجلاً بالفعل لهذه الرخصة، قم بتحديث `last_checked_at` وأرجع حالة النجاح.
  4. إذا لم يكن مسجلاً، تحقق من أن عدد الأجهزة المسجلة لهذه الرخصة أقل من `max_devices`.
     - إذا كان أقل: قم بتسجيل الجهاز الجديد في `license_devices` وأرجع نجاح.
     - إذا تجاوز الحد: أرجع خطأ "تم تجاوز الحد الأقصى للأجهزة".

- **الردود المتوقعة (Response Format):**
  - **حالة النجاح (200 OK):**
    ```json
    {
        "success": true,
        "message": "License activated successfully.",
        "data": {
            "expires_at": "2027-10-10T00:00:00Z",
            "is_active": true
        }
    }
    ```
  - **حالة الفشل (400 Bad Request):**
    ```json
    {
        "success": false,
        "message": "Invalid or expired license key." // أو رسالة الخطأ المناسبة
    }
    ```

### ب. واجهة التحقق الدوري (Verification Endpoint)
يتم استدعاء هذه الواجهة في الخلفية (Background) من البرنامج للتحقق مما إذا كان قد تم حظر المفتاح من الإدارة.
- **الرابط:** `POST http://oxtech.uk/api/license/verify`
- **البيانات المستلمة (Request Body):**
  ```json
  {
      "license_key": "XXXX-XXXX-XXXX-XXXX",
      "hardware_id": "ABCDEF1234567890"
  }
  ```
- **المنطق المطلوب (Logic):**
  1. تأكد من وجود الرخصة وصلاحيتها (`active`).
  2. تأكد من أن `hardware_id` مسجل فعلاً في `license_devices` لهذه الرخصة.
  3. قم بتحديث `last_checked_at` للجهاز.

- **الردود المتوقعة:** (نفس الردود في واجهة التفعيل).

---

## 3. حماية الـ API (Security Notes)
- يُفضل استخدام `Rate Limiting` (مثلاً 10 طلبات في الدقيقة) على مسارات الـ API لمنع هجمات التخمين (Brute-force).
- يمكنك إضافة `Header` سري بسيط (API Key) في برنامج الـ Desktop وإرساله مع كل طلب لضمان أن الطلبات تأتي من البرنامج فقط.

الرجاء إخباري عند الانتهاء من رفع هذه الـ APIs حتى أقوم بربطها وتجربتها من طرف برنامج الديسكتوب.
