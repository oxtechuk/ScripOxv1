-- ============================================================
-- ScripOx Seeds: statuses.sql
-- ============================================================
USE scripox_db;

INSERT INTO company_statuses (name, name_ar, color, is_final, sort_order) VALUES
  ('New',        'جديد',            '#6C757D', 0, 1),
  ('In Review',  'قيد المراجعة',    '#FFC107', 0, 2),
  ('Verified',   'تم التحقق',       '#198754', 0, 3),
  ('Contacted',  'تم التواصل',      '#0D6EFD', 0, 4),
  ('Interested', 'مهتم',            '#6F42C1', 0, 5),
  ('Duplicate',  'مكرر',            '#DC3545', 1, 6),
  ('Rejected',   'مرفوض',           '#ADB5BD', 1, 7),
  ('Closed',     'مغلق',            '#343A40', 1, 8);
