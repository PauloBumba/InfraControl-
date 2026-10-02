-- Seed de demonstração: alimenta o banco dev com volume realista de dados
-- (>5 linhas por entidade principal) para validar as telas cheias.
-- APENAS dados fictícios: IPs em RFC 5737 (192.0.2.0/24, 198.51.100.0/24,
-- 203.0.113.0/24), MACs locais 02:00:00:* e nomes de exemplo.
-- NUNCA usar IPs, MACs, nomes ou senhas reais da rede do hospital aqui.
-- Idempotente (ON CONFLICT DO NOTHING) — rodar de novo não duplica.
-- Uso: docker compose exec -T postgres psql -U infracontrol -d infracontrol \
--        < docs/database/seed-demo-data.sql

BEGIN;

-- ============ LOCALIZAÇÕES (6) ============
INSERT INTO locations (id, name, description, "CreateDateTime", "UpdateDateTime") VALUES
  ('d0000000-0000-0000-0000-000000000101', 'Data Center Principal', 'Sala técnica principal (demo)',  NOW() - INTERVAL '120 days', NOW()),
  ('d0000000-0000-0000-0000-000000000102', 'Data Center BKP',      'Backup do DC (demo)',            NOW() - INTERVAL '110 days', NOW()),
  ('d0000000-0000-0000-0000-000000000103', 'Escritório Matriz',     '4º andar (demo)',                NOW() - INTERVAL '100 days', NOW()),
  ('d0000000-0000-0000-0000-000000000104', 'Filial Centro',         'Loja e escritório (demo)',       NOW() - INTERVAL '90 days',  NOW()),
  ('d0000000-0000-0000-0000-000000000105', 'Filial Norte',          'Unidade norte (demo)',           NOW() - INTERVAL '80 days',  NOW()),
  ('d0000000-0000-0000-0000-000000000106', 'Hospital Bloco A',      'Rede do bloco A (demo)',         NOW() - INTERVAL '70 days',  NOW())
ON CONFLICT (id) DO NOTHING;

-- ============ RACKS (6) ============
INSERT INTO racks (id, location_id, name, description, "CreateDateTime", "UpdateDateTime") VALUES
  ('d0000000-0000-0000-0000-000000000111', 'd0000000-0000-0000-0000-000000000101', 'Rack A1', 'Core e borda (demo)',   NOW() - INTERVAL '119 days', NOW()),
  ('d0000000-0000-0000-0000-000000000112', 'd0000000-0000-0000-0000-000000000101', 'Rack A2', 'Servidores (demo)',      NOW() - INTERVAL '118 days', NOW()),
  ('d0000000-0000-0000-0000-000000000113', 'd0000000-0000-0000-0000-000000000103', 'Rack M1', 'Andar 4 (demo)',         NOW() - INTERVAL '99 days',  NOW()),
  ('d0000000-0000-0000-0000-000000000114', 'd0000000-0000-0000-0000-000000000104', 'Rack C1', 'Filial Centro (demo)',   NOW() - INTERVAL '89 days',  NOW()),
  ('d0000000-0000-0000-0000-000000000115', 'd0000000-0000-0000-0000-000000000102', 'Rack B1', 'DC backup (demo)',       NOW() - INTERVAL '109 days', NOW()),
  ('d0000000-0000-0000-0000-000000000116', 'd0000000-0000-0000-0000-000000000106', 'Rack H1', 'Hospital bloco A (demo)', NOW() - INTERVAL '69 days', NOW())
ON CONFLICT (id) DO NOTHING;

-- ============ VLANS (8) ============
INSERT INTO vlans (id, name, vlan_number, description, "CreateDateTime", "UpdateDateTime") VALUES
  ('d0000000-0000-0000-0000-000000000121', 'Gerenciamento', 10, 'Equipamentos de rede (demo)',     NOW() - INTERVAL '120 days', NOW()),
  ('d0000000-0000-0000-0000-000000000122', 'Estações',      20, 'PCs e impressoras (demo)',        NOW() - INTERVAL '120 days', NOW()),
  ('d0000000-0000-0000-0000-000000000123', 'Servidores',    30, 'Servidores (demo)',               NOW() - INTERVAL '120 days', NOW()),
  ('d0000000-0000-0000-0000-000000000124', 'Voz',           40, 'Telefonia IP (demo)',             NOW() - INTERVAL '115 days', NOW()),
  ('d0000000-0000-0000-0000-000000000125', 'WiFi Corporativo', 50, 'SSIDs internos (demo)',         NOW() - INTERVAL '115 days', NOW()),
  ('d0000000-0000-0000-0000-000000000126', 'Visitantes',    60, 'Convidados (demo)',               NOW() - INTERVAL '115 days', NOW()),
  ('d0000000-0000-0000-0000-000000000127', 'Impressoras',   70, 'Parque de impressão (demo)',      NOW() - INTERVAL '112 days', NOW()),
  ('d0000000-0000-0000-0000-000000000128', 'CFTV',          80, 'Câmeras e gravação (demo)',       NOW() - INTERVAL '112 days', NOW())
ON CONFLICT (id) DO NOTHING;

-- ============ DISPOSITIVOS (20) ============
INSERT INTO devices (
  id, location_id, rack_id, type, name, ip, mac, serial, model, vendor,
  vlan_id, description, monitoring_checks, profile_is_custom, "CreateDateTime", "UpdateDateTime"
) VALUES
  ('d0000000-0000-0000-0000-000000000131', 'd0000000-0000-0000-0000-000000000101', 'd0000000-0000-0000-0000-000000000111', 'Switch',       'sw-core-01',      '192.0.2.10',  '02:00:00:00:01:01', 'DEMO-SW-001', 'Catalyst 9300-48', 'Cisco',           'd0000000-0000-0000-0000-000000000121', 'Switch core principal (demo)',         'Ping,SnmpSystem', false, NOW() - INTERVAL '120 days', NOW()),
  ('d0000000-0000-0000-0000-000000000132', 'd0000000-0000-0000-0000-000000000101', 'd0000000-0000-0000-0000-000000000111', 'Switch',       'sw-core-02',      '192.0.2.11',  '02:00:00:00:01:02', 'DEMO-SW-002', 'Catalyst 9300-48', 'Cisco',           'd0000000-0000-0000-0000-000000000121', 'Switch core redundante (demo)',        'Ping,SnmpSystem', false, NOW() - INTERVAL '120 days', NOW()),
  ('d0000000-0000-0000-0000-000000000133', 'd0000000-0000-0000-0000-000000000103', 'd0000000-0000-0000-0000-000000000113', 'Switch',       'sw-piso-01',      '198.51.100.10', '02:00:00:00:01:03', 'DEMO-SW-003', 'UniFi USW-24',    'Ubiquiti',        'd0000000-0000-0000-0000-000000000121', 'Acesso do 4º andar (demo)',           'Ping,SnmpSystem', false, NOW() - INTERVAL '95 days', NOW()),
  ('d0000000-0000-0000-0000-000000000134', 'd0000000-0000-0000-0000-000000000105', NULL,                                'Switch',       'sw-filial-norte', '203.0.113.10', '02:00:00:00:01:04', 'DEMO-SW-004', 'CCR 1009',        'MikroTik',        'd0000000-0000-0000-0000-000000000121', 'Switch da filial norte (demo)',       'Ping,SnmpSystem', false, NOW() - INTERVAL '75 days', NOW()),
  ('d0000000-0000-0000-0000-000000000135', 'd0000000-0000-0000-0000-000000000102', 'd0000000-0000-0000-0000-000000000115', 'Switch',       'sw-dc-bkp-01',    '192.0.2.12',  '02:00:00:00:01:05', 'DEMO-SW-005', 'Catalyst 9200-24', 'Cisco',           'd0000000-0000-0000-0000-000000000121', 'Core do DC backup (demo)',            'Ping,SnmpSystem', false, NOW() - INTERVAL '105 days', NOW()),
  ('d0000000-0000-0000-0000-000000000136', 'd0000000-0000-0000-0000-000000000106', 'd0000000-0000-0000-0000-000000000116', 'Switch',       'sw-hospital-a',   '203.0.113.11', '02:00:00:00:01:06', 'DEMO-SW-006', 'UniFi USW-Pro-48','Ubiquiti',        'd0000000-0000-0000-0000-000000000121', 'Rede do hospital bloco A (demo)',     'Ping,SnmpSystem', false, NOW() - INTERVAL '65 days', NOW()),
  ('d0000000-0000-0000-0000-000000000137', 'd0000000-0000-0000-0000-000000000101', 'd0000000-0000-0000-0000-000000000111', 'Router',       'rtr-edge-01',     '192.0.2.1',   '02:00:00:00:01:07', 'DEMO-RT-001',  'ASR 1001-X',      'Cisco',           'd0000000-0000-0000-0000-000000000121', 'Roteador de borda (demo)',            'Ping,SnmpSystem', false, NOW() - INTERVAL '120 days', NOW()),
  ('d0000000-0000-0000-0000-000000000138', 'd0000000-0000-0000-0000-000000000104', 'd0000000-0000-0000-0000-000000000114', 'Router',       'rtr-branch-01',   '198.51.100.1', '02:00:00:00:01:08', 'DEMO-RT-002',  'hEX S',           'MikroTik',        'd0000000-0000-0000-0000-000000000121', 'Borda da filial centro (demo)',       'Ping,SnmpSystem', false, NOW() - INTERVAL '85 days', NOW()),
  ('d0000000-0000-0000-0000-000000000139', 'd0000000-0000-0000-0000-000000000101', 'd0000000-0000-0000-0000-000000000111', 'Firewall',     'fw-perimetro-01', '192.0.2.2',   '02:00:00:00:01:09', 'DEMO-FW-001',  'FortiGate 100F',   'Fortinet',        'd0000000-0000-0000-0000-000000000121', 'Firewall perítrico (demo)',           'Ping,SnmpSystem', false, NOW() - INTERVAL '120 days', NOW()),
  ('d0000000-0000-0000-0000-000000000140', 'd0000000-0000-0000-0000-000000000101', NULL,                                'Firewall',     'fw-dmz-01',       '192.0.2.3',   '02:00:00:00:01:10', 'DEMO-FW-002',  'FortiGate 40F',    'Fortinet',        'd0000000-0000-0000-0000-000000000121', 'Firewall da DMZ (demo)',              'Ping,SnmpSystem', false, NOW() - INTERVAL '100 days', NOW()),
  ('d0000000-0000-0000-0000-000000000141', 'd0000000-0000-0000-0000-000000000101', 'd0000000-0000-0000-0000-000000000112', 'Server',       'srv-ad-01',       '192.0.2.100', '02:00:00:00:01:11', 'DEMO-SV-001',  'PowerEdge R650',   'Dell',            'd0000000-0000-0000-0000-000000000123', 'Controlador de domínio (demo)',       'Ping,SnmpSystem', false, NOW() - INTERVAL '90 days', NOW()),
  ('d0000000-0000-0000-0000-000000000142', 'd0000000-0000-0000-0000-000000000101', 'd0000000-0000-0000-0000-000000000112', 'Server',       'srv-bd-01',       '192.0.2.101', '02:00:00:00:01:12', 'DEMO-SV-002',  'PowerEdge R740',   'Dell',            'd0000000-0000-0000-0000-000000000123', 'Banco de dados principal (demo)',     'Ping,SnmpSystem', false, NOW() - INTERVAL '90 days', NOW()),
  ('d0000000-0000-0000-0000-000000000143', 'd0000000-0000-0000-0000-000000000101', 'd0000000-0000-0000-0000-000000000112', 'Server',       'srv-app-01',      '192.0.2.102', '02:00:00:00:01:13', 'DEMO-SV-003',  'ProLiant DL380',   'HPE',             'd0000000-0000-0000-0000-000000000123', 'Aplicações internas (demo)',          'Ping,SnmpSystem', false, NOW() - INTERVAL '88 days', NOW()),
  ('d0000000-0000-0000-0000-000000000144', 'd0000000-0000-0000-0000-000000000102', 'd0000000-0000-0000-0000-000000000115', 'Server',       'srv-file-01',     '192.0.2.103', '02:00:00:00:01:14', 'DEMO-SV-004',  'ProLiant DL360',   'HPE',             'd0000000-0000-0000-0000-000000000123', 'Arquivos e backup (demo)',            'Ping,SnmpSystem', false, NOW() - INTERVAL '80 days', NOW()),
  ('d0000000-0000-0000-0000-000000000145', 'd0000000-0000-0000-0000-000000000103', 'd0000000-0000-0000-0000-000000000113', 'AccessPoint',  'ap-sala-reuniao', '198.51.100.50', '02:00:00:00:01:15', 'DEMO-AP-001', 'U6 Enterprise',    'Ubiquiti',        'd0000000-0000-0000-0000-000000000125', 'Sala de reunião 4º andar (demo)',     'Ping,SnmpSystem', false, NOW() - INTERVAL '70 days', NOW()),
  ('d0000000-0000-0000-0000-000000000146', 'd0000000-0000-0000-0000-000000000103', 'd0000000-0000-0000-0000-000000000113', 'AccessPoint',  'ap-recepcao',     '198.51.100.51', '02:00:00:00:01:16', 'DEMO-AP-002', 'U6 Lite',          'Ubiquiti',        'd0000000-0000-0000-0000-000000000125', 'Recepção (demo)',                     'Ping,SnmpSystem', false, NOW() - INTERVAL '70 days', NOW()),
  ('d0000000-0000-0000-0000-000000000147', 'd0000000-0000-0000-0000-000000000103', 'd0000000-0000-0000-0000-000000000113', 'Printer',      'prn-ti-01',       '198.51.100.20', '02:00:00:00:01:17', 'DEMO-PR-001',  'LaserJet M507',    'HP',              'd0000000-0000-0000-0000-000000000127', 'Impressora do setor de TI (demo)',    'Ping', false,           NOW() - INTERVAL '60 days', NOW()),
  ('d0000000-0000-0000-0000-000000000148', 'd0000000-0000-0000-0000-000000000106', 'd0000000-0000-0000-0000-000000000116', 'Camera',       'cam-entrada-01',  '203.0.113.40', '02:00:00:00:01:18', 'DEMO-CM-001',  'IPC-HDW3849H',    'Dahua',           'd0000000-0000-0000-0000-000000000128', 'Câmera da entrada (demo)',            'Ping', false,           NOW() - INTERVAL '55 days', NOW()),
  ('d0000000-0000-0000-0000-000000000149', 'd0000000-0000-0000-0000-000000000101', 'd0000000-0000-0000-0000-000000000111', 'UPS',          'ups-rack-a1',     '192.0.2.200', '02:00:00:00:01:19', 'DEMO-UP-001',  'Smart-UPS 3000',   'APC',             'd0000000-0000-0000-0000-000000000121', 'No-break do rack A1 (demo)',          'Ping,SnmpSystem', false, NOW() - INTERVAL '50 days', NOW()),
  ('d0000000-0000-0000-0000-000000000150', 'd0000000-0000-0000-0000-000000000103', 'd0000000-0000-0000-0000-000000000113', 'Phone',        'phone-ti-01',     '198.51.100.60', '02:00:00:00:01:20', 'DEMO-PH-001',  'IP Phone 8841',    'Cisco',           'd0000000-0000-0000-0000-000000000124', 'Telefone do TI (demo)',               'Ping', false,           NOW() - INTERVAL '45 days', NOW())
ON CONFLICT (id) DO NOTHING;

-- ============ INTERFACES (40 — 2 por device) ============
INSERT INTO network_interfaces (id, device_id, name, description, "CreateDateTime", "UpdateDateTime")
SELECT
  ('d0000000-0000-0000-0000-' || lpad(to_hex(65536 + (200 + row_number() OVER (ORDER BY d.id) + CASE WHEN s.n = 1 THEN 0 ELSE 40 END)), 12, '0'))::uuid,
  d.id,
  CASE WHEN d.type IN ('Server','Printer','Camera','Phone') THEN 'eth' || s.n ELSE 'Gi0/' || s.n END,
  'Interface ' || s.n || ' (demo)',
  NOW() - INTERVAL '60 days', NOW()
FROM devices d
CROSS JOIN (SELECT 1 AS n UNION ALL SELECT 2) s
WHERE d.id::text LIKE 'd0000000-%'
ON CONFLICT (id) DO NOTHING;

-- ============ IPs de interface (40) ============
INSERT INTO ip_addresses (id, interface_id, ip, "CreateDateTime", "UpdateDateTime")
SELECT
  ('d0000000-0000-0000-0000-' || lpad(to_hex(65536 + (300 + row_number() OVER (ORDER BY i.id) + CASE WHEN i.name IN ('Gi0/2','eth2') THEN 40 ELSE 0 END)), 12, '0'))::uuid,
  i.id,
  CASE
    WHEN i.name IN ('Gi0/2','eth2') THEN '198.51.100.' || (120 + (row_number() OVER (ORDER BY i.id)))
    ELSE '192.0.2.' || (120 + (row_number() OVER (ORDER BY i.id)))
  END,
  NOW() - INTERVAL '60 days', NOW()
FROM network_interfaces i
WHERE i.id::text LIKE 'd0000000-%'
ON CONFLICT (id) DO NOTHING;

-- ip_addresses.ip é UNIQUE: se algum IP repetir (só em re-execução parcial),
-- remove as duplicatas mais antigas mantendo a primeira.
DELETE FROM ip_addresses a USING ip_addresses b
WHERE a.ip = b.ip AND a."CreateDateTime" > b."CreateDateTime" AND a.id::text LIKE 'd0000000-%';

-- ============ MACs de interface (40) ============
INSERT INTO mac_addresses (id, interface_id, mac, "CreateDateTime", "UpdateDateTime")
SELECT
  ('d0000000-0000-0000-0000-' || lpad(to_hex(65536 + (400 + row_number() OVER (ORDER BY i.id))), 12, '0'))::uuid,
  i.id,
  '02:00:00:' || lpad(to_hex(2 + (row_number() OVER (ORDER BY i.id) / 256)), 2, '0') || ':' ||
  lpad(to_hex(row_number() OVER (ORDER BY i.id) % 256), 2, '0') || ':' ||
  lpad(to_hex(10 + (row_number() OVER (ORDER BY i.id))), 2, '0'),
  NOW() - INTERVAL '60 days', NOW()
FROM network_interfaces i
WHERE i.id::text LIKE 'd0000000-%'
ON CONFLICT (id) DO NOTHING;

-- ============ PORTAS DE SWITCH (48 — 8 por switch) ============
INSERT INTO switch_ports (id, device_id, name, description, link_up, "CreateDateTime", "UpdateDateTime")
SELECT
  ('d0000000-0000-0000-0000-' || lpad(to_hex(65536 + (500 + (rn - 1) * 8 + s.n)), 12, '0'))::uuid,
  d.id,
  'Porta ' || s.n,
  'Porta de acesso (demo)',
  (s.n <= 6),
  NOW() - INTERVAL '55 days', NOW()
FROM (
  SELECT id, row_number() OVER (ORDER BY id) AS rn
  FROM devices WHERE type = 'Switch' AND id::text LIKE 'd0000000-%'
) d
CROSS JOIN (SELECT 1 AS n UNION ALL SELECT 2 UNION ALL SELECT 3 UNION ALL SELECT 4
            UNION ALL SELECT 5 UNION ALL SELECT 6 UNION ALL SELECT 7 UNION ALL SELECT 8) s
ON CONFLICT (id) DO NOTHING;

-- ============ VLANs NOS PORTAS (24) ============
INSERT INTO switch_port_vlans (id, switch_port_id, vlan_id, "CreateDateTime", "UpdateDateTime")
SELECT
  ('d0000000-0000-0000-0000-' || lpad(to_hex(65536 + (600 + row_number() OVER (ORDER BY p.id))), 12, '0'))::uuid,
  p.id,
  CASE WHEN p.name = 'Porta 1' THEN 'd0000000-0000-0000-0000-000000000121'
       WHEN p.name IN ('Porta 2','Porta 3') THEN 'd0000000-0000-0000-0000-000000000122'
       WHEN p.name IN ('Porta 4','Porta 5') THEN 'd0000000-0000-0000-0000-000000000123'
       ELSE 'd0000000-0000-0000-0000-000000000125' END::uuid,
  NOW() - INTERVAL '55 days', NOW()
FROM switch_ports p
WHERE p.id::text LIKE 'd0000000-%' AND p.name IN ('Porta 1','Porta 2','Porta 3','Porta 4','Porta 5')
ON CONFLICT (id) DO NOTHING;

-- ============ CONEXÕES / TOPOLOGIA (12) ============
-- ============ TOPOLOGIA (recria as conexões demo do zero) ============
DELETE FROM connections WHERE source_interface_id::text LIKE 'd0000000-%';

INSERT INTO connections (id, source_interface_id, target_interface_id, "CreateDateTime", "UpdateDateTime")
SELECT
  ('d0000000-0000-0000-0000-' || lpad(to_hex(65536 + (700 + row_number() OVER (ORDER BY a.id, b.id))), 12, '0'))::uuid,
  a.id, b.id, NOW() - INTERVAL '50 days', NOW()
FROM network_interfaces a
JOIN devices da ON da.id = a.device_id AND da.name = 'sw-core-01' AND a.name = 'Gi0/1'
JOIN network_interfaces b ON b.name IN ('Gi0/1','eth1') AND b.id::text LIKE 'd0000000-%'
JOIN devices db ON db.id = b.device_id
WHERE db.name IN ('rtr-edge-01','srv-ad-01','srv-bd-01','srv-app-01','fw-perimetro-01','ups-rack-a1')
  AND a.id <> b.id
ON CONFLICT (id) DO NOTHING;

INSERT INTO connections (id, source_interface_id, target_interface_id, "CreateDateTime", "UpdateDateTime")
SELECT
  ('d0000000-0000-0000-0000-' || lpad(to_hex(65536 + (710 + row_number() OVER (ORDER BY a.id, b.id))), 12, '0'))::uuid,
  a.id, b.id, NOW() - INTERVAL '50 days', NOW()
FROM network_interfaces a
JOIN devices da ON da.id = a.device_id AND da.name = 'sw-core-01' AND a.name = 'Gi0/2'
JOIN network_interfaces b ON b.name = 'Gi0/1' AND b.id::text LIKE 'd0000000-%'
JOIN devices db ON db.id = b.device_id
WHERE db.name IN ('sw-core-02','sw-piso-01','sw-dc-bkp-01','sw-hospital-a','sw-filial-norte','rtr-branch-01')
  AND a.id <> b.id
ON CONFLICT (id) DO NOTHING;

INSERT INTO connections (id, source_interface_id, target_interface_id, "CreateDateTime", "UpdateDateTime")
SELECT
  ('d0000000-0000-0000-0000-' || lpad(to_hex(65536 + (720 + row_number() OVER (ORDER BY a.id, b.id))), 12, '0'))::uuid,
  a.id, b.id, NOW() - INTERVAL '50 days', NOW()
FROM network_interfaces a
JOIN devices da ON da.id = a.device_id AND da.name = 'sw-piso-01' AND a.name = 'Gi0/1'
JOIN network_interfaces b ON b.name IN ('Gi0/1','eth1') AND b.id::text LIKE 'd0000000-%'
JOIN devices db ON db.id = b.device_id
WHERE db.name IN ('ap-sala-reuniao','ap-recepcao','prn-ti-01','phone-ti-01','cam-entrada-01')
  AND a.id <> b.id
ON CONFLICT (id) DO NOTHING;

-- ============ CHECKS DE MONITORAMENTO (Ping + SnmpSystem) ============
INSERT INTO monitoring_checks (id, device_id, kind, enabled, "CreateDateTime", "UpdateDateTime")
SELECT
  ('d0000000-0000-0000-0000-' || lpad(to_hex(65536 + (800 + (row_number() OVER (ORDER BY d.id) - 1) * 2 + k.ord)), 12, '0'))::uuid,
  d.id, k.kind, true, NOW() - INTERVAL '40 days', NOW()
FROM devices d
CROSS JOIN (SELECT 'Ping'::varchar AS kind, 1 AS ord UNION ALL SELECT 'SnmpSystem', 2) k
WHERE d.id::text LIKE 'd0000000-%'
ON CONFLICT (device_id, kind) DO NOTHING;

-- ============ HISTÓRICO DE MONITORAMENTO (60 — 3 por device) ============
INSERT INTO monitoring_results (id, device_id, kind, succeeded, duration_ms, error, values_json, "CreateDateTime", "UpdateDateTime")
SELECT
  ('d0000000-0000-0000-0000-' || lpad(to_hex(65536 + (900 + (rn - 1) * 3 + h.ord)), 12, '0'))::uuid,
  d.id, 'Ping', h.ok, h.ms, h.err, h.vals,
  NOW() - (h.ord * INTERVAL '5 minutes'), NOW()
FROM (
  SELECT id, name, row_number() OVER (ORDER BY id) AS rn
  FROM devices WHERE id::text LIKE 'd0000000-%'
) d
CROSS JOIN (VALUES
  (1, true,  12, NULL, NULL),
  (2, true,  15, NULL, NULL),
  (3, false, 0,  'Timeout aguardando resposta.', NULL)
) h(ord, ok, ms, err, vals)
-- sw-hospital-a e cam-entrada-01 ficam "down" no último poll (topologia mostra down)
WHERE NOT (d.name IN ('sw-hospital-a','cam-entrada-01') AND h.ord = 3)
  AND NOT (d.name NOT IN ('sw-hospital-a','cam-entrada-01') AND h.ord = 3 AND d.rn % 5 = 0)
ON CONFLICT (id) DO NOTHING;

-- último Ping com sucesso para quase todos; os dois "down" acima recebem
-- um resultado final falho bem mais recente:
INSERT INTO monitoring_results (id, device_id, kind, succeeded, duration_ms, error, values_json, "CreateDateTime", "UpdateDateTime")
SELECT
  ('d0000000-0000-0000-0000-' || lpad(to_hex(65536 + (990 + row_number() OVER (ORDER BY d.id))), 12, '0'))::uuid,
  d.id, 'Ping', false, 0, 'Timeout aguardando resposta (demo).', NULL,
  NOW() - INTERVAL '2 minutes', NOW()
FROM devices d
WHERE d.name IN ('sw-hospital-a','cam-entrada-01') AND d.id::text LIKE 'd0000000-%'
ON CONFLICT (id) DO NOTHING;

-- ============ ALERTAS (14) ============
INSERT INTO alerts (id, device_id, kind, severity, status, message, resolved_at, "CreateDateTime", "UpdateDateTime") VALUES
  ('d0000000-0000-0000-0000-000000001001', 'd0000000-0000-0000-0000-000000000136', 'Ping',        'Critical', 'Open',     'Sem resposta ICMP há 15 minutos (demo).',                NULL,                                   NOW() - INTERVAL '14 minutes', NOW()),
  ('d0000000-0000-0000-0000-000000001002', 'd0000000-0000-0000-0000-000000000148', 'Ping',        'Warning',  'Open',     'Latência ICMP acima de 300 ms (demo).',                   NULL,                                   NOW() - INTERVAL '40 minutes', NOW()),
  ('d0000000-0000-0000-0000-000000001003', 'd0000000-0000-0000-0000-000000000142', 'SnmpSystem',  'Warning',  'Open',     'CPU acima de 90% por 10 minutos (demo).',                NULL,                                   NOW() - INTERVAL '2 hours',    NOW()),
  ('d0000000-0000-0000-0000-000000001004', 'd0000000-0000-0000-0000-000000000134', 'Ping',        'Warning',  'Open',     'Perda de pacotes de 30% (demo).',                        NULL,                                   NOW() - INTERVAL '3 hours',    NOW()),
  ('d0000000-0000-0000-0000-000000001005', 'd0000000-0000-0000-0000-000000000139', 'SnmpSystem',  'Critical', 'Open',     'Tabela de sessões quase cheia (demo).',                   NULL,                                   NOW() - INTERVAL '5 hours',    NOW()),
  ('d0000000-0000-0000-0000-000000001006', 'd0000000-0000-0000-0000-000000000149', 'SnmpToner',   'Warning',  'Open',     'Bateria do UPS em 22% (demo).',                          NULL,                                   NOW() - INTERVAL '6 hours',    NOW()),
  ('d0000000-0000-0000-0000-000000001007', 'd0000000-0000-0000-0000-000000000147', 'SnmpDrum',    'Warning',  'Open',     'Toner preto abaixo de 10% (demo).',                      NULL,                                   NOW() - INTERVAL '1 day',      NOW()),
  ('d0000000-0000-0000-0000-000000001008', 'd0000000-0000-0000-0000-000000000132', 'Ping',        'Warning',  'Resolved', 'Failover do core concluído com sucesso (demo).',          NOW() - INTERVAL '20 hours',           NOW() - INTERVAL '22 hours',   NOW()),
  ('d0000000-0000-0000-0000-000000001009', 'd0000000-0000-0000-0000-000000000141', 'HttpWan',     'Warning',  'Resolved', 'HTTP 503 no serviço de diretório (demo).',               NOW() - INTERVAL '2 days',            NOW() - INTERVAL '3 days',     NOW()),
  ('d0000000-0000-0000-0000-000000001010', 'd0000000-0000-0000-0000-000000000137', 'Tcp',         'Critical', 'Resolved', 'Porta 443 inacessível na borda (demo).',                 NOW() - INTERVAL '4 days',            NOW() - INTERVAL '5 days',     NOW()),
  ('d0000000-0000-0000-0000-000000001011', 'd0000000-0000-0000-0000-000000000145', 'Ping',        'Warning',  'Resolved', 'AP reiniciou após atualização (demo).',                  NOW() - INTERVAL '6 days',            NOW() - INTERVAL '7 days',     NOW()),
  ('d0000000-0000-0000-0000-000000001012', 'd0000000-0000-0000-0000-000000000143', 'SnmpSystem',  'Warning',  'Suppressed','CPU normalizou — suprimido por incidente aberto (demo).', NOW() - INTERVAL '8 days',           NOW() - INTERVAL '9 days',     NOW()),
  ('d0000000-0000-0000-0000-000000001013', 'd0000000-0000-0000-0000-000000000135', 'Ping',        'Warning',  'Resolved', 'Link do DC backup oscilou (demo).',                      NOW() - INTERVAL '10 days',           NOW() - INTERVAL '11 days',    NOW()),
  ('d0000000-0000-0000-0000-000000001014', 'd0000000-0000-0000-0000-000000000144', 'SnmpSystem',  'Warning',  'Resolved', 'Espaço em disco em 85% (demo).',                        NOW() - INTERVAL '12 days',           NOW() - INTERVAL '13 days',    NOW())
ON CONFLICT (id) DO NOTHING;

-- ============ INCIDENTES (8) ============
INSERT INTO incidents (id, title, description, severity, status, device_id, resolved_at, "RootCause", "CreateDateTime", "UpdateDateTime") VALUES
  ('d0000000-0000-0000-0000-000000001101', 'Switch do hospital sem resposta', 'Perda total de ICMP no sw-hospital-a (demo).',        'Critical', 'Investigating', 'd0000000-0000-0000-0000-000000000136', NULL,               NULL,                              NOW() - INTERVAL '14 minutes', NOW()),
  ('d0000000-0000-0000-0000-000000001102', 'Alta latência na filial norte',   'Perda de pacotes na WAN da filial (demo).',            'High',     'Identified',    'd0000000-0000-0000-0000-000000000134', NULL,               'Link do ISP degradado (demo).',    NOW() - INTERVAL '3 hours',    NOW()),
  ('d0000000-0000-0000-0000-000000001103', 'CPU crítico no banco',            'srv-bd-01 com CPU > 90% (demo).',                       'High',     'Monitoring',    'd0000000-0000-0000-0000-000000000142', NULL,               'Carga de relatórios (demo).',      NOW() - INTERVAL '2 hours',    NOW()),
  ('d0000000-0000-0000-0000-000000001104', 'UPS em modo bateria',             'Queda de energia no rack A1 (demo).',                  'Medium',   'Open',          'd0000000-0000-0000-0000-000000000149', NULL,               NULL,                              NOW() - INTERVAL '6 hours',    NOW()),
  ('d0000000-0000-0000-0000-000000001105', 'Failover do core 02',             'Troca programada de core (demo).',                      'Medium',   'Resolved',      'd0000000-0000-0000-0000-000000000132', NOW() - INTERVAL '20 hours', 'Manutenção programada (demo).',   NOW() - INTERVAL '22 hours',   NOW()),
  ('d0000000-0000-0000-0000-000000001106', 'HTTP 503 no diretório',           'Serviço fora do ar por 6 minutos (demo).',              'High',     'Resolved',      'd0000000-0000-0000-0000-000000000141', NOW() - INTERVAL '2 days',  'Deploy com roll-back (demo).',    NOW() - INTERVAL '3 days',     NOW()),
  ('d0000000-0000-0000-0000-000000001107', 'Porta 443 inacessível',           'Borda bloqueando HTTPS de saída (demo).',               'Critical', 'Resolved',      'd0000000-0000-0000-0000-000000000137', NOW() - INTERVAL '4 days',  'Regra de firewall mal aplicada.', NOW() - INTERVAL '5 days',     NOW()),
  ('d0000000-0000-0000-0000-000000001108', 'AP da sala de reunião reiniciou', 'Reboot pós-update do firmware (demo).',                'Low',      'Resolved',      'd0000000-0000-0000-0000-000000000145', NOW() - INTERVAL '6 days',  'Atualização automática (demo).',  NOW() - INTERVAL '7 days',     NOW())
ON CONFLICT (id) DO NOTHING;

-- ============ VÍNCULOS INCIDENTE↔ALERTA (6) ============
INSERT INTO incident_alerts (incident_id, alert_id, "CreateDateTime", "UpdateDateTime") VALUES
  ('d0000000-0000-0000-0000-000000001101', 'd0000000-0000-0000-0000-000000001001', NOW(), NOW()),
  ('d0000000-0000-0000-0000-000000001102', 'd0000000-0000-0000-0000-000000001004', NOW(), NOW()),
  ('d0000000-0000-0000-0000-000000001103', 'd0000000-0000-0000-0000-000000001003', NOW(), NOW()),
  ('d0000000-0000-0000-0000-000000001104', 'd0000000-0000-0000-0000-000000001006', NOW(), NOW()),
  ('d0000000-0000-0000-0000-000000001105', 'd0000000-0000-0000-0000-000000001008', NOW(), NOW()),
  ('d0000000-0000-0000-0000-000000001107', 'd0000000-0000-0000-0000-000000001010', NOW(), NOW())
ON CONFLICT DO NOTHING;

-- ============ TASKS (16 — estados variados) ============
INSERT INTO tasks ("Id", "Title", "Description", "Kind", "State", "CreateDateTime", "UpdateDateTime") VALUES
  ('d0000000-0000-0000-0000-000000001301', 'Investigar sw-hospital-a',            'Coletar logs e validar energia.',          'Incident',   'Open',       NOW() - INTERVAL '14 minutes', NOW()),
  ('d0000000-0000-0000-0000-000000001302', 'Atualizar firmware dos switches',     'Janela de manutenção trimestral.',         'Maintenance','InProgress', NOW() - INTERVAL '3 days',     NOW()),
  ('d0000000-0000-0000-0000-000000001303', 'Documentar VLAN de voz',              'Atualizar docs/03 com a VLAN 40.',         'Change',     'Waiting',     NOW() - INTERVAL '5 days',     NOW()),
  ('d0000000-0000-0000-0000-000000001304', 'Revisar regras do firewall DMZ',      'Auditoria trimestral de regras.',          'Preventive', 'Blocked',     NOW() - INTERVAL '6 days',     NOW()),
  ('d0000000-0000-0000-0000-000000001305', 'Trocar UPS rack A1',                  'Bateria abaixo de 25%.',                   'Corrective', 'Open',       NOW() - INTERVAL '6 hours',    NOW()),
  ('d0000000-0000-0000-0000-000000001306', 'Migrar banco para o DC BKP',          'Failover planejado.',                      'Project',    'InProgress', NOW() - INTERVAL '10 days',    NOW()),
  ('d0000000-0000-0000-0000-000000001307', 'Resolver 503 do diretório',           'Roll-back aplicado — falta validar.',      'Incident',   'Resolved',    NOW() - INTERVAL '3 days',     NOW()),
  ('d0000000-0000-0000-0000-000000001308', 'Retoque de cabeamento no 4º andar',   'Par de fibra rompido.',                    'Manual',     'Open',       NOW() - INTERVAL '2 days',     NOW()),
  ('d0000000-0000-0000-0000-000000001309', 'Discovery da rede do hospital',       'Varredura ARP/SNMP no bloco A.',           'Discovery',  'InProgress', NOW() - INTERVAL '1 day',      NOW()),
  ('d0000000-0000-0000-0000-000000001310', 'Revisão de acessos de admin',         'Conferir roles dos usuários.',             'System',     'Waiting',     NOW() - INTERVAL '8 days',     NOW()),
  ('d0000000-0000-0000-0000-000000001311', 'Backup dos switches',                 'Config backup semanal.',                   'Preventive', 'Closed',      NOW() - INTERVAL '14 days',    NOW()),
  ('d0000000-0000-0000-0000-000000001312', 'Alarme de perda de pacotes filial',   'Alerta virou tarefa — acompanhar ISP.',    'Alert',      'Open',       NOW() - INTERVAL '3 hours',    NOW()),
  ('d0000000-0000-0000-0000-000000001313', 'Capacitar operador no novo painel',   'Treinamento da equipe noturna.',           'Manual',     'Cancelled',   NOW() - INTERVAL '20 days',    NOW()),
  ('d0000000-0000-0000-0000-000000001314', 'Auditoria de disco srv-file-01',      '85% de uso — limpar snapshots.',           'Monitoring', 'Resolved',    NOW() - INTERVAL '12 days',    NOW()),
  ('d0000000-0000-0000-0000-000000001315', 'Padronizar nomes de interfaces',      'De eth0 para nomes de site.',              'Change',     'Open',       NOW() - INTERVAL '4 days',     NOW()),
  ('d0000000-0000-0000-0000-000000001316', 'Trocar switch da recepção',           'Portas Gigabit insuficientes.',            'Corrective', 'InProgress', NOW() - INTERVAL '9 hours',    NOW())
ON CONFLICT ("Id") DO NOTHING;

-- ============ HISTÓRICO DAS TASKS (16) ============
INSERT INTO task_histories ("Id", task_id, actor_id, "Action", "Detail", "CreateDateTime", "UpdateDateTime")
SELECT
  ('d0000000-0000-0000-0000-' || lpad(to_hex(65536 + (1400 + row_number() OVER (ORDER BY t."Id"))), 12, '0'))::uuid,
  t."Id", NULL, 'Created', 'Tarefa criada (demo).', t."CreateDateTime", t."CreateDateTime"
FROM tasks t WHERE t."Id"::text LIKE 'd0000000-%'
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO task_histories ("Id", task_id, actor_id, "Action", "Detail", "CreateDateTime", "UpdateDateTime")
SELECT
  ('d0000000-0000-0000-0000-' || lpad(to_hex(65536 + (1450 + row_number() OVER (ORDER BY t."Id"))), 12, '0'))::uuid,
  t."Id", NULL, 'StateChanged', 'Open → ' || t."State", NOW() - INTERVAL '1 hour', NOW() - INTERVAL '1 hour'
FROM tasks t WHERE t."Id"::text LIKE 'd0000000-%' AND t."State" <> 'Open'
ON CONFLICT ("Id") DO NOTHING;

-- ============ COMENTÁRIOS DAS TASKS (6, autor = admin se existir) ============
INSERT INTO task_comments ("Id", task_id, author_id, "Content", "CreateDateTime", "UpdateDateTime")
SELECT
  ('d0000000-0000-0000-0000-' || lpad(to_hex(65536 + (1500 + row_number() OVER (ORDER BY t."Id"))), 12, '0'))::uuid,
  t."Id", u."Id"::uuid,
  CASE row_number() OVER (ORDER BY t."Id") % 3
    WHEN 1 THEN 'Aguardando retorno do site (demo).'
    WHEN 2 THEN 'Prioridade alta — afeta produção (demo).'
    ELSE 'Andamento registrado na reunião (demo).'
  END,
  NOW() - INTERVAL '2 hours', NOW() - INTERVAL '2 hours'
FROM tasks t
CROSS JOIN LATERAL (SELECT "Id" FROM "DomainUsers" WHERE "Email" = 'admin@example.com' LIMIT 1) u
WHERE t."Id"::text LIKE 'd0000000-%'
ORDER BY t."Id"
LIMIT 6
ON CONFLICT ("Id") DO NOTHING;

-- ============ CATEGORIAS DE KNOWLEDGE (5) ============
INSERT INTO knowledge_categories (id, name, description, "CreateDateTime", "UpdateDateTime") VALUES
  ('d0000000-0000-0000-0000-000000001601', 'Redes',      'Roteamento, VLANs e switch (demo)',      NOW() - INTERVAL '60 days', NOW()),
  ('d0000000-0000-0000-0000-000000001602', 'Servidores', 'SO, banco e virtualização (demo)',       NOW() - INTERVAL '60 days', NOW()),
  ('d0000000-0000-0000-0000-000000001603', 'Segurança',  'Firewall, acessos e LGPD (demo)',        NOW() - INTERVAL '60 days', NOW()),
  ('d0000000-0000-0000-0000-000000001604', 'Procedimentos', 'Runbooks e checklists (demo)',         NOW() - INTERVAL '60 days', NOW()),
  ('d0000000-0000-0000-0000-000000001605', 'Telefonia',  'VoIP e aparelhos (demo)',                NOW() - INTERVAL '60 days', NOW())
ON CONFLICT (id) DO NOTHING;

-- ============ ARTIGOS DE KNOWLEDGE (10) ============
INSERT INTO knowledge_articles (id, title, content, category_id, status, "CreateDateTime", "UpdateDateTime") VALUES
  ('d0000000-0000-0000-0000-000000001701', 'Runbook: switch sem resposta',        '1. Verificar energia; 2. Conferir cabo; 3. Ping lateral; 4. Abrir incidente se > 15 min. (demo)', 'd0000000-0000-0000-0000-000000001601', 'Published', NOW() - INTERVAL '40 days', NOW()),
  ('d0000000-0000-0000-0000-000000001702', 'Como criar uma VLAN nova',            'Cadastro em Rede → VLANs; aplicar nas portas e documentar. (demo)',                              'd0000000-0000-0000-0000-000000001601', 'Published', NOW() - INTERVAL '38 days', NOW()),
  ('d0000000-0000-0000-0000-000000001703', 'Procedimento de failover do core',    'Checklist de troca com janela aprovada. (demo)',                                                  'd0000000-0000-0000-0000-000000001604', 'Published', NOW() - INTERVAL '30 days', NOW()),
  ('d0000000-0000-0000-0000-000000001704', 'Backup de configuração de switches',  'Script semanal + guarda em repositório. (demo)',                                                   'd0000000-0000-0000-0000-000000001604', 'Published', NOW() - INTERVAL '28 days', NOW()),
  ('d0000000-0000-0000-0000-000000001705', 'Política de acesso administrativo',   'MFA obrigatório e revisão trimestral de roles. (demo)',                                            'd0000000-0000-0000-0000-000000001603', 'Published', NOW() - INTERVAL '25 days', NOW()),
  ('d0000000-0000-0000-0000-000000001706', 'Upgrade de firmware FortiGate',       'Passo a passo com rollback. (demo)',                                                              'd0000000-0000-0000-0000-000000001603', 'Draft',     NOW() - INTERVAL '10 days', NOW()),
  ('d0000000-0000-0000-0000-000000001707', 'Troubleshooting de VoIP',             'Codec, jitter e VLAN de voz. (demo)',                                                             'd0000000-0000-0000-0000-000000001605', 'Draft',     NOW() - INTERVAL '8 days',  NOW()),
  ('d0000000-0000-0000-0000-000000001708', 'Monitoramento de banco de dados',     'Checks de CPU, disco e conexões. (demo)',                                                         'd0000000-0000-0000-0000-000000001602', 'Published', NOW() - INTERVAL '20 days', NOW()),
  ('d0000000-0000-0000-0000-000000001709', 'Antigo: topologia do DC antigo',      'Substituído — mantido para consulta. (demo)',                                                     'd0000000-0000-0000-0000-000000001601', 'Archived',  NOW() - INTERVAL '90 days', NOW()),
  ('d0000000-0000-0000-0000-000000001710', 'Checklist de onboarding de site',     'Itens de rede, telefonia e câmeras. (demo)',                                                      'd0000000-0000-0000-0000-000000001604', 'Published', NOW() - INTERVAL '15 days', NOW())
ON CONFLICT (id) DO NOTHING;

-- ============ SERVIÇOS (7) ============
INSERT INTO services (id, name, description, "CreateDateTime", "UpdateDateTime") VALUES
  ('d0000000-0000-0000-0000-000000001801', 'E-mail corporativo',   'SMTP e caixas (demo)',        NOW() - INTERVAL '50 days', NOW()),
  ('d0000000-0000-0000-0000-000000001802', 'Intranet',             'Portal interno (demo)',       NOW() - INTERVAL '50 days', NOW()),
  ('d0000000-0000-0000-0000-000000001803', 'Sistema de gestão',    'ERP/ERP legado (demo)',       NOW() - INTERVAL '50 days', NOW()),
  ('d0000000-0000-0000-0000-000000001804', 'Wi-Fi corporativo',     'SSIDs internos (demo)',       NOW() - INTERVAL '50 days', NOW()),
  ('d0000000-0000-0000-0000-000000001805', 'Telefonia IP',          'PBX e aparelhos (demo)',      NOW() - INTERVAL '50 days', NOW()),
  ('d0000000-0000-0000-0000-000000001806', 'Acesso remoto VPN',     'Conexões externas (demo)',    NOW() - INTERVAL '50 days', NOW()),
  ('d0000000-0000-0000-0000-000000001807', 'CFTV',                  'Câmeras e gravação (demo)',   NOW() - INTERVAL '50 days', NOW())
ON CONFLICT (id) DO NOTHING;

-- ============ SLOS (12) ============
INSERT INTO slos (id, name, target_percent, window_days, service_id, "CreateDateTime", "UpdateDateTime") VALUES
  ('d0000000-0000-0000-0000-000000001901', 'Disponibilidade do e-mail',    99.900, 30, 'd0000000-0000-0000-0000-000000001801', NOW() - INTERVAL '45 days', NOW()),
  ('d0000000-0000-0000-0000-000000001902', 'Latência da intranet',         99.500, 30, 'd0000000-0000-0000-0000-000000001802', NOW() - INTERVAL '45 days', NOW()),
  ('d0000000-0000-0000-0000-000000001903', 'Disponibilidade do ERP',       99.700, 30, 'd0000000-0000-0000-0000-000000001803', NOW() - INTERVAL '45 days', NOW()),
  ('d0000000-0000-0000-0000-000000001904', 'Cobertura WiFi',               98.000, 30, 'd0000000-0000-0000-0000-000000001804', NOW() - INTERVAL '45 days', NOW()),
  ('d0000000-0000-0000-0000-000000001905', 'Chamadas VoIP com sucesso',    99.000, 30, 'd0000000-0000-0000-0000-000000001805', NOW() - INTERVAL '45 days', NOW()),
  ('d0000000-0000-0000-0000-000000001906', 'VPN estabelecida',             99.500, 30, 'd0000000-0000-0000-0000-000000001806', NOW() - INTERVAL '45 days', NOW()),
  ('d0000000-0000-0000-0000-000000001907', 'Gravação CFTV disponível',     99.900, 30, 'd0000000-0000-0000-0000-000000001807', NOW() - INTERVAL '45 days', NOW()),
  ('d0000000-0000-0000-0000-000000001908', 'Backup diário concluído',      99.000, 30, 'd0000000-0000-0000-0000-000000001803', NOW() - INTERVAL '45 days', NOW()),
  ('d0000000-0000-0000-0000-000000001909', 'DNS respondendo',              99.950, 30, 'd0000000-0000-0000-0000-000000001802', NOW() - INTERVAL '45 days', NOW()),
  ('d0000000-0000-0000-0000-000000001910', 'Autenticação AD',              99.900, 30, 'd0000000-0000-0000-0000-000000001803', NOW() - INTERVAL '45 days', NOW()),
  ('d0000000-0000-0000-0000-000000001911', 'Internet always-on',           99.500, 30, 'd0000000-0000-0000-0000-000000001806', NOW() - INTERVAL '45 days', NOW()),
  ('d0000000-0000-0000-0000-000000001912', 'Impressão central',            97.000, 30, 'd0000000-0000-0000-0000-000000001802', NOW() - INTERVAL '45 days', NOW())
ON CONFLICT (id) DO NOTHING;

-- ============ SLA POLICIES (7) ============
INSERT INTO sla_policies (id, name, response_minutes, resolution_minutes, service_id, "CreateDateTime", "UpdateDateTime") VALUES
  ('d0000000-0000-0000-0000-000000002001', 'P1 - Produção',      15,  240, 'd0000000-0000-0000-0000-000000001803', NOW() - INTERVAL '45 days', NOW()),
  ('d0000000-0000-0000-0000-000000002002', 'P2 - Alto impacto',  30,  480, 'd0000000-0000-0000-0000-000000001803', NOW() - INTERVAL '45 days', NOW()),
  ('d0000000-0000-0000-0000-000000002003', 'P3 - Médio',         60, 1440, 'd0000000-0000-0000-0000-000000001802', NOW() - INTERVAL '45 days', NOW()),
  ('d0000000-0000-0000-0000-000000002004', 'P4 - Baixo',        240, 4320, 'd0000000-0000-0000-0000-000000001802', NOW() - INTERVAL '45 days', NOW()),
  ('d0000000-0000-0000-0000-000000002005', 'Rede - crítico',     15,  120, 'd0000000-0000-0000-0000-000000001804', NOW() - INTERVAL '45 days', NOW()),
  ('d0000000-0000-0000-0000-000000002006', 'Telefonia',          30,  240, 'd0000000-0000-0000-0000-000000001805', NOW() - INTERVAL '45 days', NOW()),
  ('d0000000-0000-0000-0000-000000002007', 'Monitoramento 24x7',  5,   60, NULL,                                     NOW() - INTERVAL '45 days', NOW())
ON CONFLICT (id) DO NOTHING;

-- ============ MEDIÇÕES DE SLO (36 — 3 por SLO) ============
INSERT INTO slo_measurements (id, slo_id, measured_at, value_percent, "CreateDateTime", "UpdateDateTime")
SELECT
  ('d0000000-0000-0000-0000-' || lpad(to_hex(65536 + (2100 + (rn - 1) * 3 + m.ord)), 12, '0'))::uuid,
  s.id,
  NOW() - (m.d * INTERVAL '1 day'),
  CASE WHEN s.id::text LIKE '%1907' THEN 99.500 - m.ord ELSE 99.990 - (m.ord * 0.05) END,
  NOW(), NOW()
FROM (SELECT id, row_number() OVER (ORDER BY id) AS rn FROM slos WHERE id::text LIKE 'd0000000-%') s
CROSS JOIN (VALUES (1, 1), (2, 7), (3, 14)) m(ord, d)
ON CONFLICT (id) DO NOTHING;

-- ============ ORÇAMENTO DE SLO (12) ============
INSERT INTO slo_budgets (id, slo_id, period_start, period_end, consumed_percent, "CreateDateTime", "UpdateDateTime")
SELECT
  ('d0000000-0000-0000-0000-' || lpad(to_hex(65536 + (2200 + row_number() OVER (ORDER BY id))), 12, '0'))::uuid,
  id, date_trunc('month', NOW()) - INTERVAL '1 month', date_trunc('month', NOW()),
  CASE WHEN id::text LIKE '%1903' THEN 78.500 ELSE 35.250 END,
  NOW(), NOW()
FROM slos WHERE id::text LIKE 'd0000000-%'
ON CONFLICT (id) DO NOTHING;

-- ============ DISCOVERY (6 runs + 20 resultados → alimenta Clientes) ============
INSERT INTO discovery_runs (id, device_id, status, started_at, finished_at, error_message, "CreateDateTime", "UpdateDateTime")
SELECT
  ('d0000000-0000-0000-0000-' || lpad(to_hex(65536 + (2300 + row_number() OVER (ORDER BY d.id))), 12, '0'))::uuid,
  d.id, 'Completed', NOW() - INTERVAL '2 days', NOW() - INTERVAL '2 days' + INTERVAL '3 minutes', NULL,
  NOW() - INTERVAL '2 days', NOW() - INTERVAL '2 days'
FROM devices d WHERE d.name IN ('sw-core-01','sw-core-02','sw-piso-01','rtr-edge-01','fw-perimetro-01','sw-hospital-a')
ON CONFLICT (id) DO NOTHING;

-- ARP entries (alimentam GET /clients)
INSERT INTO discovery_results (id, discovery_run_id, kind, summary, values_json, device_id, created_at, "CreateDateTime", "UpdateDateTime")
SELECT
  ('d0000000-0000-0000-0000-' || lpad(to_hex(65536 + (2400 + row_number() OVER (ORDER BY r.id))), 12, '0'))::uuid,
  r.id, 'ArpEntry',
  'ARP ' || x.ip || ' → ' || x.mac,
  ('{"ip":"' || x.ip || '","mac":"' || x.mac || '","hostname":"' || x.host || '","vlan":"20","local_if":"Gi0/1"}')::text,
  r.device_id, NOW() - INTERVAL '2 days', NOW() - INTERVAL '2 days', NOW() - INTERVAL '2 days'
FROM discovery_runs r
JOIN (VALUES
  ('198.51.100.21','02:11:22:33:44:01','notebook-financeiro'),
  ('198.51.100.22','02:11:22:33:44:02','notebook-rh'),
  ('198.51.100.23','02:11:22:33:44:03','pc-recepcao'),
  ('198.51.100.24','02:11:22:33:44:04','ipad-diretoria'),
  ('198.51.100.25','02:11:22:33:44:05','pc-financeiro-02'),
  ('198.51.100.26','02:11:22:33:44:06','notebook-ti'),
  ('198.51.100.27','02:11:22:33:44:07','pc-vendas-01'),
  ('198.51.100.28','02:11:22:33:44:08','pc-vendas-02'),
  ('198.51.100.29','02:11:22:33:44:09','impressora-andar4'),
  ('198.51.100.30','02:11:22:33:44:0a','tablet-manutencao')
) x(ip, mac, host) ON TRUE
WHERE r.id::text LIKE 'd0000000-%'
ON CONFLICT (id) DO NOTHING;

-- DHCP leases (mescla no GET /clients por MAC)
INSERT INTO discovery_results (id, discovery_run_id, kind, summary, values_json, device_id, created_at, "CreateDateTime", "UpdateDateTime")
SELECT
  ('d0000000-0000-0000-0000-' || lpad(to_hex(65536 + (2500 + row_number() OVER (ORDER BY r.id))), 12, '0'))::uuid,
  r.id, 'DhcpLease',
  'DHCP ' || x.ip || ' → ' || x.mac,
  ('{"ip":"' || x.ip || '","mac":"' || x.mac || '","hostname":"' || x.host || '","vlan":"60","local_if":"Gi0/2"}')::text,
  r.device_id, NOW() - INTERVAL '1 day', NOW() - INTERVAL '1 day', NOW() - INTERVAL '1 day'
FROM discovery_runs r
JOIN (VALUES
  ('203.0.113.61','02:aa:bb:cc:00:01','visitante-01'),
  ('203.0.113.62','02:aa:bb:cc:00:02','visitante-02'),
  ('203.0.113.63','02:aa:bb:cc:00:03','celular-cfo'),
  ('203.0.113.64','02:aa:bb:cc:00:04','smartwatch-01')
) x(ip, mac, host) ON TRUE
WHERE r.id::text LIKE 'd0000000-%'
ON CONFLICT (id) DO NOTHING;

-- ============ NOTIFICAÇÕES + DESTINATÁRIOS (10 → feed do admin) ============
INSERT INTO notifications (id, event_id, event_type, title, message, create_datetime, update_datetime) VALUES
  ('d0000000-0000-0000-0000-000000002501', 'd0000000-0000-0000-0000-00000000f001', 'IncidentCreatedDomainEvent',     'Incidente criado',            'Switch do hospital sem resposta foi aberto como incidente (demo).',      NOW() - INTERVAL '14 minutes', NOW()),
  ('d0000000-0000-0000-0000-000000002502', 'd0000000-0000-0000-0000-00000000f002', 'AlertRaisedDomainEvent',         'Alerta crítico aberto',       'sw-hospital-a sem resposta ICMP (demo).',                                 NOW() - INTERVAL '14 minutes', NOW()),
  ('d0000000-0000-0000-0000-000000002503', 'd0000000-0000-0000-0000-00000000f003', 'UserCreatedDomainEvent',         'Usuário criado',              'Um novo operador foi cadastrado (demo).',                                 NOW() - INTERVAL '1 day',      NOW()),
  ('d0000000-0000-0000-0000-000000002504', 'd0000000-0000-0000-0000-00000000f004', 'IncidentResolvedDomainEvent',    'Incidente resolvido',         'Failover do core 02 foi encerrado (demo).',                              NOW() - INTERVAL '20 hours',   NOW()),
  ('d0000000-0000-0000-0000-000000002505', 'd0000000-0000-0000-0000-00000000f005', 'UserRoleAssignedDomainEvent',    'Papel de usuário atribuído',  'Role Operator aplicada a um colega (demo).',                              NOW() - INTERVAL '2 days',     NOW()),
  ('d0000000-0000-0000-0000-000000002506', 'd0000000-0000-0000-0000-00000000f006', 'InvitationAcceptedDomainEvent',  'Convite aceito',              'Um operador aceitou o convite (demo).',                                  NOW() - INTERVAL '3 days',     NOW()),
  ('d0000000-0000-0000-0000-000000002507', 'd0000000-0000-0000-0000-00000000f007', 'AlertResolvedDomainEvent',       'Alerta resolvido',            'CPU do banco normalizou (demo).',                                        NOW() - INTERVAL '5 hours',    NOW()),
  ('d0000000-0000-0000-0000-000000002508', 'd0000000-0000-0000-0000-00000000f008', 'IncidentCreatedDomainEvent',     'Incidente criado',            'UPS em modo bateria no rack A1 (demo).',                                 NOW() - INTERVAL '6 hours',    NOW()),
  ('d0000000-0000-0000-0000-000000002509', 'd0000000-0000-0000-0000-00000000f009', 'UserSessionRevokedDomainEvent',  'Sessão revogada',             'Uma sessão antiga foi revogada (demo).',                                 NOW() - INTERVAL '4 days',     NOW()),
  ('d0000000-0000-0000-0000-000000002510', 'd0000000-0000-0000-0000-00000000f010', 'InvitationActivatedDomainEvent', 'Convite ativado',             'Novo acesso de leitura ativado (demo).',                                 NOW() - INTERVAL '5 days',     NOW())
ON CONFLICT (id) DO NOTHING;

INSERT INTO notification_recipients (id, notification_id, user_id, role, "CreateDateTime", "UpdateDateTime")
SELECT
  ('d0000000-0000-0000-0000-' || lpad(to_hex(65536 + (2600 + row_number() OVER (ORDER BY n.id))), 12, '0'))::uuid,
  n.id, NULL, 'Admin', NOW(), NOW()
FROM notifications n WHERE n.id::text LIKE 'd0000000-%'
ON CONFLICT (notification_id, role) DO NOTHING;

-- ============ AUDITORIA (10 eventos recentes extras) ============
INSERT INTO "AuditEntries" ("Id", "Action", "Resource", "Outcome", "ActorId", "CorrelationId", "Error", "OccurredAt", "CreateDateTime", "UpdateDateTime")
SELECT
  ('d0000000-0000-0000-0000-' || lpad(to_hex(65536 + (2700 + row_number() OVER (ORDER BY a.ord))), 12, '0'))::uuid,
  a.action, a.resource, a.outcome, 'admin', NULL, NULL,
  NOW() - (a.ord * INTERVAL '25 minutes'), NOW(), NOW()
FROM (VALUES
  (1,  'SearchDevicesQuery',        'Inventory',  'Succeeded'),
  (2,  'GetTopologyQuery',          'Network',    'Succeeded'),
  (3,  'SearchAlertsQuery',         'Monitoring', 'Succeeded'),
  (4,  'ChangeTaskStateCommand',    'Tasks',      'Succeeded'),
  (5,  'SearchIncidentsQuery',      'Incidents',  'Succeeded'),
  (6,  'GetNotificationFeedQuery',  'Notifications', 'Succeeded'),
  (7,  'SearchClientsQuery',        'Discovery',  'Succeeded'),
  (8,  'LoginCommand',              'Identity',   'Succeeded'),
  (9,  'SearchSlosQuery',           'Sla',        'Succeeded'),
  (10, 'GetAuditQuery',             'Admin',      'Failed')
) a(ord, action, resource, outcome)
WHERE NOT EXISTS (
  SELECT 1 FROM "AuditEntries" e WHERE e."Action" = a.action AND e."Resource" = a.resource
)
ON CONFLICT ("Id") DO NOTHING;

COMMIT;

-- ============ RESUMO ESPERADO ============
-- locations 6+ | racks 6+ | vlans 8 | devices 20 | interfaces 40 | IPs 40
-- MACs 40 | portas 48 | conexões ~14 | checks ~40 | resultados ~66
-- alerts 14 (7 open) | incidentes 8 (4 ativos) | tasks 16 (9 não-terminais)
-- knowledge 5 categorias + 10 artigos | services 7 | SLOs 12 | SLAs 7
-- discovery: 6 runs + 24 resultados (14 clientes) | notificações 10 (feed admin)
SELECT 'locations' t, count(*) FROM locations WHERE id::text LIKE 'd0000000-%'
UNION ALL SELECT 'racks', count(*) FROM racks WHERE id::text LIKE 'd0000000-%'
UNION ALL SELECT 'vlans', count(*) FROM vlans WHERE id::text LIKE 'd0000000-%'
UNION ALL SELECT 'devices', count(*) FROM devices WHERE id::text LIKE 'd0000000-%'
UNION ALL SELECT 'interfaces', count(*) FROM network_interfaces WHERE id::text LIKE 'd0000000-%'
UNION ALL SELECT 'switch_ports', count(*) FROM switch_ports WHERE id::text LIKE 'd0000000-%'
UNION ALL SELECT 'connections', count(*) FROM connections WHERE id::text LIKE 'd0000000-%'
UNION ALL SELECT 'alerts(open)', count(*) FROM alerts WHERE id::text LIKE 'd0000000-%' AND status = 'Open'
UNION ALL SELECT 'incidents', count(*) FROM incidents WHERE id::text LIKE 'd0000000-%'
UNION ALL SELECT 'tasks', count(*) FROM tasks WHERE "Id"::text LIKE 'd0000000-%'
UNION ALL SELECT 'articles', count(*) FROM knowledge_articles WHERE id::text LIKE 'd0000000-%'
UNION ALL SELECT 'clients(arp/dhcp)', count(*) FROM discovery_results WHERE id::text LIKE 'd0000000-%'
UNION ALL SELECT 'notifications', count(*) FROM notifications WHERE id::text LIKE 'd0000000-%';
