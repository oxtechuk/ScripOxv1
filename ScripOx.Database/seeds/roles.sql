-- ============================================================
-- ScripOx Seeds: roles.sql
-- ============================================================
USE scripox_db;

INSERT INTO roles (name, name_ar) VALUES
  ('super_admin',   'مدير النظام الكامل'),
  ('admin',         'مدير'),
  ('data_operator', 'مشغّل البيانات'),
  ('reviewer',      'مراجع البيانات'),
  ('sales_user',    'مستخدم المبيعات'),
  ('viewer',        'مشاهد');
