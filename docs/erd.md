# ScripOx — ERD (Entity Relationship Diagram)
**Ox Tech — oxtech.uk**

---

## ERD نصي كامل بالعلاقات

```
roles (1) ──────────────── (N) users
  id PK                         id PK
  name                          username
  name_ar                       email
                                password_hash
                                role_id FK → roles.id
                                language
                                is_active

users (1) ──────────────── (N) extraction_jobs
                               id PK
                               source_id FK → sources.id
                               triggered_by FK → users.id
                               status
                               params_json
                               total_found / total_saved
                               progress_pct
                               started_at / ended_at

sources (1) ─────────────── (N) extraction_jobs
  id PK
  name
  type (web/api/csv/manual)
  connector
  config_json
  is_active

sources (1) ─────────────── (N) field_mappings
                               id PK
                               source_id FK → sources.id
                               source_field
                               target_field
                               transform

extraction_jobs (1) ─────── (N) raw_records
                               id PK BIGINT
                               job_id FK → extraction_jobs.id
                               raw_json
                               status (pending/approved/rejected/duplicate)
                               reviewed_by FK → users.id

raw_records (1) ──────────── (0..1) companies
                               id PK BIGINT
                               name, category, phone, email…
                               x (longitude), y (latitude)
                               location POINT SRID 4326  ← Spatial
                               source_id FK → sources.id
                               raw_record_id FK → raw_records.id
                               status_id FK → company_statuses.id
                               assigned_to FK → users.id
                               priority, is_verified

company_statuses (1) ──────── (N) companies
  id PK
  name / name_ar
  color (#HEX)
  is_final
  sort_order

companies (1) ─────────────── (N) company_links
                               id PK
                               company_id FK → companies.id
                               platform
                               url

companies (N) ─────────────── (M) tags
  via company_tag junction:
    company_id FK → companies.id
    tag_id FK → tags.id

tags:
  id PK
  name (unique)
  color

companies (1) ─────────────── (N) company_notes
                               id PK
                               company_id FK → companies.id
                               user_id FK → users.id
                               content
                               is_pinned

companies (1) ─────────────── (N) company_comments
                               id PK
                               company_id FK → companies.id
                               user_id FK → users.id
                               content
                               parent_id FK → company_comments.id  ← Nested replies

users (1) ──────────────────── (N) export_jobs
                               id PK
                               created_by FK → users.id
                               format (xlsx/csv)
                               filters_json
                               file_path / status

(any entity) ───────────────── (N) audit_logs
                               id PK BIGINT
                               user_id FK → users.id
                               entity (table name)
                               entity_id
                               action (create/update/delete/login/export…)
                               old_json / new_json
                               ip_address

translations:
  id PK
  lang (en/ar)
  key_name  (unique per lang)
  value
```

---

## الفهارس (Indexes)

| الجدول | الفهرس | النوع | السبب |
|--------|--------|-------|-------|
| companies | location | SPATIAL | البحث الجغرافي |
| companies | city, status_id | COMPOSITE | فلترة شائعة |
| companies | name, address | FULLTEXT | البحث النصي |
| extraction_jobs | status | INDEX | فلترة الجوبز |
| raw_records | job_id, status | INDEX | مراجعة السجلات |
| audit_logs | entity, entity_id | INDEX | تتبع التعديلات |
| translations | lang, key_name | UNIQUE | منع التكرار |

---

## ملاحظات الـ Spatial

```sql
-- تعريف العمود
location POINT NOT NULL SRID 4326

-- الإدراج
ST_GeomFromText('POINT(lng lat)', 4326)

-- البحث داخل نطاق (دائرة 5 كيلومتر)
SELECT * FROM companies
WHERE ST_Distance_Sphere(
  location,
  ST_GeomFromText('POINT(-0.1278 51.5074)', 4326)
) < 5000;
```
