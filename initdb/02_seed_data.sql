INSERT INTO system_roles (code, name) VALUES
  ('client',           'Клиент'),
  ('manager',          'Руководитель'),
  ('resource_owner',   'Владелец ресурса'),
  ('security_officer', 'Сотрудник ИБ'),
  ('agent',            'Инженер'),
  ('admin',            'Администратор');

INSERT INTO agent_groups (code, name) VALUES
  ('helpdesk',      'Первая линия'),
  ('network_team',  'Сетевые инженеры'),
  ('security_team', 'Команда ИБ'),
  ('sysadmins',     'Системные администраторы');

INSERT INTO ticket_types (code, name) VALUES
  ('incident',        'Инцидент'),
  ('service_request', 'Запрос на обслуживание'),
  ('change',          'Изменение'),
  ('release',         'Релиз'),
  ('access',          'Запрос на доступ');

INSERT INTO ticket_statuses (code, name) VALUES
  ('new',         'Новая'),
  ('in_progress', 'В работе'),
  ('on_approval', 'На согласовании'),
  ('resolved',    'Решена'),
  ('closed',      'Закрыта');

INSERT INTO sla_policies (name, schedule_type, reaction_time_minutes, resolution_time_minutes) VALUES
  ('24x7 Критичный',  '24x7', 15,  240),
  ('8x5 Стандартный', '8x5',  60,  480),
  ('8x5 Низкий',      '8x5',  240, 1440);

INSERT INTO calendar_exceptions (exception_date, is_work_day, description) VALUES
  ('2026-01-01', false, 'Новый год'),
  ('2026-01-07', false, 'Рождество'),
  ('2026-03-08', false, '8 марта'),
  ('2026-05-01', false, 'Праздник весны и труда'),
  ('2026-05-09', false, 'День Победы'),
  ('2026-06-13', true,  'Рабочая суббота (перенос)');

INSERT INTO users (object_sid, username, email, full_name, department, is_active) VALUES
  ('S-1-5-21-1001', 'admin',     'admin@itsm.local',     'Администратор Системы', 'IT',           true),
  ('S-1-5-21-1002', 'i.ivanov',  'i.ivanov@itsm.local',  'Иван Иванов',           'IT',           true),
  ('S-1-5-21-1003', 'p.petrov',  'p.petrov@itsm.local',  'Пётр Петров',           'IT',           true),
  ('S-1-5-21-1004', 's.sidorov', 's.sidorov@itsm.local', 'Сидор Сидоров',         'Бухгалтерия',  true),
  ('S-1-5-21-1005', 'a.anna',    'a.anna@itsm.local',    'Анна Аннова',           'Бухгалтерия',  true),
  ('S-1-5-21-1006', 'm.maria',   'm.maria@itsm.local',   'Мария Марьина',         'Отдел продаж', true);

UPDATE users SET manager_id = (SELECT id FROM users WHERE username = 'i.ivanov')
  WHERE username IN ('p.petrov', 's.sidorov', 'a.anna', 'm.maria');

UPDATE users SET manager_id = (SELECT id FROM users WHERE username = 'admin')
  WHERE username = 'i.ivanov';

INSERT INTO user_role_mappings (user_id, role_id)
SELECT u.id, r.id
FROM users u, system_roles r
WHERE (u.username = 'admin'    AND r.code = 'admin')
   OR (u.username = 'i.ivanov' AND r.code = 'manager')
   OR (u.username = 'p.petrov' AND r.code = 'agent')
   OR (u.username = 's.sidorov' AND r.code = 'client')
   OR (u.username = 'a.anna'   AND r.code = 'client')
   OR (u.username = 'm.maria'  AND r.code = 'client');

INSERT INTO service_catalog (name, description, is_active) VALUES
  ('IT-поддержка',              'Базовые услуги техподдержки',   true),
  ('Доступы и учётные записи',  'Выдача доступов к системам',    true),
  ('Оборудование',              'Выдача и обслуживание техники', true);

INSERT INTO service_catalog (parent_id, name, description, is_active)
SELECT id, 'Сброс пароля', 'Восстановление доступа к учётной записи', true
FROM service_catalog WHERE name = 'Доступы и учётные записи';

INSERT INTO service_catalog (parent_id, name, description, is_active)
SELECT id, 'Выдача ноутбука', 'Новый ноутбук для сотрудника', true
FROM service_catalog WHERE name = 'Оборудование';

INSERT INTO service_catalog (parent_id, name, description, is_active)
SELECT id, 'Настройка рабочего места', 'Установка ПО, настройка', true
FROM service_catalog WHERE name = 'IT-поддержка';

INSERT INTO assets (inventory_number, name, category, serial_number, status, assigned_user_id)
SELECT 'INV-0001', 'Ноутбук Dell Latitude 5540', 'Hardware', 'DL5540-001', 'In Use', u.id
FROM users u WHERE u.username = 'i.ivanov';

INSERT INTO assets (inventory_number, name, category, serial_number, status, assigned_user_id)
SELECT 'INV-0002', 'Ноутбук Lenovo ThinkPad', 'Hardware', 'LT-002', 'In Use', u.id
FROM users u WHERE u.username = 'p.petrov';

INSERT INTO assets (inventory_number, name, category, serial_number, status, license_expiration_date)
VALUES ('INV-0003', 'Microsoft Office 365', 'Software License', 'MS-OFF-001', 'In Use', '2026-12-31');

INSERT INTO assets (inventory_number, name, category, serial_number, status)
VALUES ('INV-0004', 'Монитор Dell U2723', 'Hardware', 'MON-004', 'In Use');

INSERT INTO asset_history_logs (asset_id, user_id, action_type, old_status, new_status, comment)
SELECT
  (SELECT id FROM assets WHERE inventory_number = 'INV-0002'),
  (SELECT id FROM users WHERE username = 'admin'),
  'StatusChange', 'Purchased', 'In Use',
  'Выдан Петрову П. при приёме на работу';

INSERT INTO tickets (title, description, type_id, status_id, priority, service_id, creator_id, assignee_id, assignee_group_id, sla_policy_id)
SELECT
  'Не включается ноутбук',
  'После обновления Windows ноутбук не загружается',
  (SELECT id FROM ticket_types    WHERE code = 'incident'),
  (SELECT id FROM ticket_statuses WHERE code = 'in_progress'),
  'High',
  (SELECT id FROM service_catalog WHERE name = 'Настройка рабочего места'),
  (SELECT id FROM users WHERE username = 'i.ivanov'),
  (SELECT id FROM users WHERE username = 'p.petrov'),
  (SELECT id FROM agent_groups WHERE code = 'helpdesk'),
  (SELECT id FROM sla_policies WHERE name = '8x5 Стандартный');

INSERT INTO tickets (title, description, type_id, status_id, priority, service_id, creator_id, sla_policy_id)
SELECT
  'Забыл пароль от корпоративной почты',
  'Не могу войти в почту, нужен сброс пароля',
  (SELECT id FROM ticket_types    WHERE code = 'access'),
  (SELECT id FROM ticket_statuses WHERE code = 'new'),
  'Medium',
  (SELECT id FROM service_catalog WHERE name = 'Сброс пароля'),
  (SELECT id FROM users WHERE username = 's.sidorov'),
  (SELECT id FROM sla_policies WHERE name = '24x7 Критичный');

INSERT INTO tickets (title, description, type_id, status_id, priority, service_id, creator_id, sla_policy_id)
SELECT
  'Заявка на новый ноутбук',
  'Для нового сотрудника требуется ноутбук',
  (SELECT id FROM ticket_types    WHERE code = 'service_request'),
  (SELECT id FROM ticket_statuses WHERE code = 'on_approval'),
  'Low',
  (SELECT id FROM service_catalog WHERE name = 'Выдача ноутбука'),
  (SELECT id FROM users WHERE username = 'm.maria'),
  (SELECT id FROM sla_policies WHERE name = '8x5 Низкий');

INSERT INTO ticket_messages (ticket_id, author_id, message_body, is_internal)
SELECT
  (SELECT id FROM tickets WHERE title = 'Не включается ноутбук'),
  (SELECT id FROM users   WHERE username = 'i.ivanov'),
  'Добрый день! Когда сможете посмотреть?',
  false;

INSERT INTO ticket_messages (ticket_id, author_id, message_body, is_internal)
SELECT
  (SELECT id FROM tickets WHERE title = 'Не включается ноутбук'),
  (SELECT id FROM users   WHERE username = 'p.petrov'),
  'Внутренняя заметка: похоже на проблему с загрузчиком, нужен образ для восстановления',
  true;

INSERT INTO ticket_approvals (ticket_id, approver_id, step_number, status)
SELECT
  (SELECT id FROM tickets WHERE title = 'Заявка на новый ноутбук'),
  (SELECT id FROM users   WHERE username = 'i.ivanov'),
  1,
  'Pending';

INSERT INTO ticket_audit_logs (ticket_id, user_id, field_name, old_value, new_value)
SELECT
  (SELECT id FROM tickets WHERE title = 'Не включается ноутбук'),
  (SELECT id FROM users   WHERE username = 'p.petrov'),
  'status_id', 'new', 'in_progress';

INSERT INTO knowledge_base_articles (title, content, service_id, is_published)
SELECT
  'Как сбросить пароль самостоятельно',
  '<p>Зайдите в портал самообслуживания → «Забыли пароль» → следуйте инструкции.</p>',
  (SELECT id FROM service_catalog WHERE name = 'Сброс пароля'),
  true;

INSERT INTO ticket_asset_mappings (ticket_id, asset_id)
SELECT
  (SELECT id FROM tickets WHERE title = 'Не включается ноутбук'),
  (SELECT id FROM assets  WHERE inventory_number = 'INV-0001');