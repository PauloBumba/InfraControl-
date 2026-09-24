-- F2-T003: seed fictício de inventário (APENAS dados de documentação RFC 5737).
-- IPs em 192.0.2.0/24 (TEST-NET-1), 198.51.100.0/24 (TEST-NET-2), 203.0.113.0/24 (TEST-NET-3).
-- NUNCA usar IPs, MACs, nomes ou senhas reais da rede do hospital neste arquivo.
-- Uso: psql -U infracontrol -d infracontrol -f docs/database/seed-fictitious-devices.sql

BEGIN;

INSERT INTO locations (id, name, description, "CreateDateTime", "UpdateDateTime")
VALUES (
  '00000000-0000-0000-0000-000000000101',
  'Data Center (seed)',
  'Local fictício para desenvolvimento',
  NOW(), NOW()
)
ON CONFLICT (id) DO NOTHING;

INSERT INTO racks (id, location_id, name, description, "CreateDateTime", "UpdateDateTime")
VALUES (
  '00000000-0000-0000-0000-000000000201',
  '00000000-0000-0000-0000-000000000101',
  'R01 (seed)',
  'Rack fictício',
  NOW(), NOW()
)
ON CONFLICT (id) DO NOTHING;

INSERT INTO devices (
  id, location_id, rack_id, type, name, ip, mac, serial, model, vendor,
  vlan_id, description, "CreateDateTime", "UpdateDateTime"
) VALUES
  (
    '00000000-0000-0000-0000-000000000301',
    '00000000-0000-0000-0000-000000000101',
    '00000000-0000-0000-0000-000000000201',
    'Switch',
    'sw-seed-01',
    '192.0.2.10',
    '02:00:00:00:00:01',
    'SEED-SN-001',
    'Switch modelo fictício',
    'Fabricante Fictício',
    NULL,
    'Switch de exemplo (dados fictícios)',
    NOW(), NOW()
  ),
  (
    '00000000-0000-0000-0000-000000000302',
    '00000000-0000-0000-0000-000000000101',
    '00000000-0000-0000-0000-000000000201',
    'Printer',
    'prn-seed-01',
    '198.51.100.20',
    '02:00:00:00:00:02',
    'SEED-SN-002',
    'Impressora fictícia',
    'Fabricante Fictício',
    NULL,
    'Impressora de exemplo (dados fictícios)',
    NOW(), NOW()
  ),
  (
    '00000000-0000-0000-0000-000000000303',
    '00000000-0000-0000-0000-000000000101',
    NULL,
    'AccessPoint',
    'ap-seed-01',
    '203.0.113.30',
    '02:00:00:00:00:03',
    'SEED-SN-003',
    'AP fictício',
    'Fabricante Fictício',
    NULL,
    'Access Point de exemplo (dados fictícios)',
    NOW(), NOW()
  )
ON CONFLICT (id) DO NOTHING;

COMMIT;
