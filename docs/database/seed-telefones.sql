-- Seed de telefones IP (inventário) — dados reais da planilha de ramais
-- fornecida pelo usuário em 2026-11-10 (1º/2º/3º andar + Pronto-Socorro).
--
-- Uso: psql -U infracontrol -d infracontrol -f docs/database/seed-telefones.sql
--   ou: docker cp docs/database/telefones.sql infracontrol-postgres:/tmp/ &&
--       docker exec infracontrol-postgres psql -U infracontrol -d infracontrol \
--         -v ON_ERROR_STOP=1 -f /tmp/telefones.sql
--
-- Idempotente: locations guardados por LOWER(name); devices guardados por
-- LOWER(name), ip e LOWER(mac); monitoring_checks guardados por
-- (device_id, kind). Reexecutar não duplica nada.
--
-- Correções aplicadas sobre a planilha (óbvias):
--   * Consultório 2: IP 193.168.21.133 → 192.168.21.133 (typo de rede);
--   * "Tomograddddddddddddfia Sala de Comando" → "Tomografia — Sala de Comando";
--   * "Secretaría" → "Secretaria"; "Medicos"/"Consultorio" → acentuados;
--   * ramais com TEXTO na coluna (FARMA COMERCIAL, Sanificação, Obstetríca,
--     SUPERVISAO CC, Agenda CC) → gravados em description, extension NULL.
--
-- Exceções FORA deste seed (listadas ao usuário, aguardam decisão):
--   * Marketing (1º)          — planilha com X/X/X (sem dado algum);
--   * Lavanderia (2º)         — planilha com X/X/X (sem dado algum);
--   * Supervisão Enfermagem Setor 1 (2º) — IP 192.168.21.183 duplicado com TI
--     (IP é lógico por device) e sem ramal/MAC na origem.
--
-- MACs normalizados no formato do domínio (F2-T005): aa:bb:cc:dd:ee:ff minúsculo.
-- Ramal → coluna extension (F30-T006c); perfil default do tipo Phone = Ping
-- (ADR-014): monitoring_checks CSV 'Ping' + linha em monitoring_checks.

BEGIN;

-- ============ LOCAIS (4 andares/setores) ============
INSERT INTO locations (id, name, description, "CreateDateTime", "UpdateDateTime")
SELECT v.id, v.name, v.description, NOW(), NOW()
FROM (VALUES
  ('f00d0000-0000-4000-8000-000000000001'::uuid, '1º Andar',        'Local físico — 1º andar (cadastro de telefones)'),
  ('f00d0000-0000-4000-8000-000000000002'::uuid, '2º Andar',        'Local físico — 2º andar (cadastro de telefones)'),
  ('f00d0000-0000-4000-8000-000000000003'::uuid, '3º Andar',        'Local físico — 3º andar (cadastro de telefones)'),
  ('f00d0000-0000-4000-8000-000000000004'::uuid, 'Pronto-Socorro',  'Local físico — Pronto-Socorro (cadastro de telefones)')
) AS v(id, name, description)
WHERE NOT EXISTS (
  SELECT 1 FROM locations l WHERE LOWER(l.name) = LOWER(v.name)
)
ON CONFLICT (id) DO NOTHING;

-- ============ TELEFONES (58 devices type=Phone) ============
INSERT INTO devices (
  id, location_id, rack_id, type, name, ip, mac, serial, model, vendor,
  vlan_id, description, extension, sip_server, operating_system, image_path,
  monitoring_checks, profile_is_custom, "CreateDateTime", "UpdateDateTime"
)
SELECT
  v.id,
  l.id,
  NULL,
  'Phone',
  v.name,
  v.ip,
  v.mac,
  NULL,
  NULL,
  NULL,
  NULL,
  v.description,
  v.extension,
  NULL,
  NULL,
  NULL,
  'Ping',
  false,
  NOW(),
  NOW()
FROM (VALUES
  -- ===== 1º Andar (28) =====
  ('f00d0000-0000-4000-8000-000000000101'::uuid, '1º Andar', 'Farmácia Comercial',                '192.168.10.217', '00:26:8b:9d:8a:c1', '1570', 'Telefone IP — 1º Andar'),
  ('f00d0000-0000-4000-8000-000000000102'::uuid, '1º Andar', 'Supervisão Farmácia Comercial',    '192.168.21.26',  '00:26:8b:9d:61:b5', NULL,  'Telefone IP — 1º Andar; anotação de origem: "FARMA COMERCIAL"'),
  ('f00d0000-0000-4000-8000-000000000103'::uuid, '1º Andar', 'NIR',                               '192.168.21.140', '00:26:8b:9d:65:4d', '1518', 'Telefone IP — 1º Andar'),
  ('f00d0000-0000-4000-8000-000000000104'::uuid, '1º Andar', 'Recursos Humanos',                  '192.168.21.161', '00:26:8b:9d:61:34', '1562', 'Telefone IP — 1º Andar'),
  ('f00d0000-0000-4000-8000-000000000105'::uuid, '1º Andar', 'Segurança do Trabalho',             '192.168.21.136', '00:26:8b:9d:61:b0', '1533', 'Telefone IP — 1º Andar'),
  ('f00d0000-0000-4000-8000-000000000106'::uuid, '1º Andar', 'Departamento Pessoal',              '192.168.21.30',  '00:26:8b:9d:61:b3', '1535', 'Telefone IP — 1º Andar'),
  ('f00d0000-0000-4000-8000-000000000107'::uuid, '1º Andar', 'Financeiro 1526',                   '192.168.21.117', '00:26:8b:9d:65:45', '1526', 'Telefone IP — 1º Andar (setor Financeiro, aparelho 1)'),
  ('f00d0000-0000-4000-8000-000000000108'::uuid, '1º Andar', 'Financeiro 1527',                   '192.168.21.115', '00:26:8b:9d:68:5d', '1527', 'Telefone IP — 1º Andar (setor Financeiro, aparelho 2)'),
  ('f00d0000-0000-4000-8000-000000000109'::uuid, '1º Andar', 'Pastoral',                          '192.168.21.61',  '00:26:8b:9d:61:82', '1572', 'Telefone IP — 1º Andar'),
  ('f00d0000-0000-4000-8000-000000000110'::uuid, '1º Andar', 'Faturamento',                       '192.168.21.25',  '00:26:8b:9d:61:7c', '1576', 'Telefone IP — 1º Andar'),
  ('f00d0000-0000-4000-8000-000000000111'::uuid, '1º Andar', 'Qualidade/Marketing',               '192.168.21.168', '00:26:8b:9d:61:b1', '1534', 'Telefone IP — 1º Andar'),
  ('f00d0000-0000-4000-8000-000000000112'::uuid, '1º Andar', 'Comercial',                         '192.168.21.20',  '00:26:8b:9d:65:46', '1525', 'Telefone IP — 1º Andar'),
  ('f00d0000-0000-4000-8000-000000000113'::uuid, '1º Andar', 'Secretaria Jurídica Administrativa','192.168.21.164', '00:26:8b:9d:61:7f', '1566', 'Telefone IP — 1º Andar'),
  ('f00d0000-0000-4000-8000-000000000114'::uuid, '1º Andar', 'Gestão de Unidade de Saúde',        '192.168.21.58',  '00:26:8b:9d:61:ae', '1530', 'Telefone IP — 1º Andar'),
  ('f00d0000-0000-4000-8000-000000000115'::uuid, '1º Andar', 'Sala de Reunião',                   '192.168.21.54',  '00:26:8b:9d:68:63', '1548', 'Telefone IP — 1º Andar'),
  ('f00d0000-0000-4000-8000-000000000116'::uuid, '1º Andar', 'Psicologia Clínica',                '192.168.21.118', '00:26:8b:9d:61:b2', '1536', 'Telefone IP — 1º Andar'),
  ('f00d0000-0000-4000-8000-000000000117'::uuid, '1º Andar', 'Contabilidade',                     '192.168.21.162', '00:26:8b:9d:61:38', '1557', 'Telefone IP — 1º Andar'),
  ('f00d0000-0000-4000-8000-000000000118'::uuid, '1º Andar', 'Compras',                           '192.168.21.127', '00:26:8b:9d:61:ac', '1538', 'Telefone IP — 1º Andar'),
  ('f00d0000-0000-4000-8000-000000000119'::uuid, '1º Andar', 'Supervisão de Compras',             '192.168.10.151', '00:26:8b:9d:5b:10', NULL,  'Telefone IP — 1º Andar; anotação de origem: "Sup Compras" (sem ramal na planilha)'),
  ('f00d0000-0000-4000-8000-000000000120'::uuid, '1º Andar', 'TI',                                '192.168.21.183', '00:26:8b:9d:61:7a', '1565', 'Telefone IP — 1º Andar'),
  ('f00d0000-0000-4000-8000-000000000121'::uuid, '1º Andar', 'Recepção 1522',                     '192.168.21.179', '00:26:8b:9d:65:47', '1522', 'Telefone IP — 1º Andar (setor Recepção, aparelho 1)'),
  ('f00d0000-0000-4000-8000-000000000122'::uuid, '1º Andar', 'Recepção 1580',                     '192.168.21.15',  '00:26:8b:9d:66:3b', '1580', 'Telefone IP — 1º Andar (setor Recepção, aparelho 2)'),
  ('f00d0000-0000-4000-8000-000000000123'::uuid, '1º Andar', 'Recepção 1500',                     '192.168.21.21',  '00:26:8b:9d:2c:c2', '1500', 'Telefone IP — 1º Andar (setor Recepção, aparelho 3)'),
  ('f00d0000-0000-4000-8000-000000000124'::uuid, '1º Andar', 'Cozinha',                           '192.168.21.143', '00:26:8b:9d:61:3b', '1553', 'Telefone IP — 1º Andar'),
  ('f00d0000-0000-4000-8000-000000000125'::uuid, '1º Andar', 'UTI 1',                             '192.168.21.165', '00:26:8b:9d:61:7d', '1563', 'Telefone IP — 1º Andar'),
  ('f00d0000-0000-4000-8000-000000000126'::uuid, '1º Andar', 'Sala de Revelação',                 '192.168.21.128', '00:26:8b:9d:62:b4', '1561', 'Telefone IP — 1º Andar'),
  ('f00d0000-0000-4000-8000-000000000127'::uuid, '1º Andar', 'Sala de Digitação',                 '192.168.21.139', '00:26:8b:9d:61:35', '1560', 'Telefone IP — 1º Andar'),
  ('f00d0000-0000-4000-8000-000000000128'::uuid, '1º Andar', 'Tomografia — Sala de Comando',      '192.168.21.172', '00:26:8b:9d:66:3d', '1573', 'Telefone IP — 1º Andar; nome de origem corrigido ("Tomograd...dfia Sala de Comando")'),
  -- ===== 2º Andar (11) =====
  ('f00d0000-0000-4000-8000-000000000129'::uuid, '2º Andar', 'Posto de Enfermagem Setor 1',       '192.168.21.156', '00:26:8b:9d:62:b2', '1507', 'Telefone IP — 2º Andar'),
  ('f00d0000-0000-4000-8000-000000000130'::uuid, '2º Andar', 'Sala dos Médicos Setor 1',          '192.168.21.155', '00:26:8b:9d:62:b3', '1508', 'Telefone IP — 2º Andar'),
  ('f00d0000-0000-4000-8000-000000000131'::uuid, '2º Andar', 'Copa',                             '192.168.21.154', '00:26:8b:9d:61:3c', '1552', 'Telefone IP — 2º Andar'),
  ('f00d0000-0000-4000-8000-000000000132'::uuid, '2º Andar', 'Nutrição',                          '192.168.21.153', '00:26:8b:9d:61:36', '1559', 'Telefone IP — 2º Andar'),
  ('f00d0000-0000-4000-8000-000000000133'::uuid, '2º Andar', 'Posto de Enfermagem Setor 2',       '192.168.21.157', '00:26:8b:9d:66:34', '1596', 'Telefone IP — 2º Andar'),
  ('f00d0000-0000-4000-8000-000000000134'::uuid, '2º Andar', 'Posto Enfermagem Setor 4',          '192.168.21.188', '00:26:8b:9d:65:49', '1512', 'Telefone IP — 2º Andar'),
  ('f00d0000-0000-4000-8000-000000000135'::uuid, '2º Andar', 'Laboratório',                       '192.168.21.167', '00:26:8b:9d:61:b4', '1528', 'Telefone IP — 2º Andar'),
  ('f00d0000-0000-4000-8000-000000000136'::uuid, '2º Andar', 'Farmácia Central',                  '192.168.21.152', '00:26:8b:9d:61:3a', '1554', 'Telefone IP — 2º Andar'),
  ('f00d0000-0000-4000-8000-000000000137'::uuid, '2º Andar', 'Estoque',                           '192.168.21.175', '00:26:8b:9d:68:62', '1539', 'Telefone IP — 2º Andar'),
  ('f00d0000-0000-4000-8000-000000000138'::uuid, '2º Andar', 'Manutenção',                        '192.168.21.150', NULL,               NULL,  'Telefone IP — 2º Andar; sem ramal e sem MAC na planilha de origem'),
  ('f00d0000-0000-4000-8000-000000000139'::uuid, '2º Andar', 'Sanificação',                       '192.168.21.135', '00:26:8b:9d:65:48', NULL,  'Telefone IP — 2º Andar; anotação de origem: "Sanificação"'),
  -- ===== 3º Andar (11) =====
  ('f00d0000-0000-4000-8000-000000000140'::uuid, '3º Andar', 'Posto de Enfermagem Setor 3',       '192.168.21.142', '00:26:8b:9d:65:4b', '1514', 'Telefone IP — 3º Andar'),
  ('f00d0000-0000-4000-8000-000000000141'::uuid, '3º Andar', 'Comissões',                         '192.168.21.200', '00:26:8b:9d:65:4a', '1513', 'Telefone IP — 3º Andar'),
  ('f00d0000-0000-4000-8000-000000000142'::uuid, '3º Andar', 'Obstetrícia',                       '192.168.10.199', '00:26:8b:9d:2c:bf', NULL,  'Telefone IP — 3º Andar; anotação de origem: "Obstetríca"'),
  ('f00d0000-0000-4000-8000-000000000143'::uuid, '3º Andar', 'Endoscopia',                        '192.168.21.134', '00:26:8b:9d:66:35', '1588', 'Telefone IP — 3º Andar'),
  ('f00d0000-0000-4000-8000-000000000144'::uuid, '3º Andar', 'UTI 2',                             '192.168.21.141', '00:26:8b:9d:61:39', '1555', 'Telefone IP — 3º Andar'),
  ('f00d0000-0000-4000-8000-000000000145'::uuid, '3º Andar', 'OPME',                              '192.168.21.126', '00:26:8b:9d:61:3d', '1556', 'Telefone IP — 3º Andar'),
  ('f00d0000-0000-4000-8000-000000000146'::uuid, '3º Andar', 'Quartos Plantonistas UTI 2',        '192.168.21.105', '00:26:8b:9d:68:5f', '1549', 'Telefone IP — 3º Andar'),
  ('f00d0000-0000-4000-8000-000000000147'::uuid, '3º Andar', 'Agência Transfusional',             '192.168.21.129', '00:26:8b:9d:62:b9', '1503', 'Telefone IP — 3º Andar'),
  ('f00d0000-0000-4000-8000-000000000148'::uuid, '3º Andar', 'Recuperação Anestésica',            '192.168.21.137', '00:26:8b:9d:68:61', '1551', 'Telefone IP — 3º Andar'),
  ('f00d0000-0000-4000-8000-000000000149'::uuid, '3º Andar', 'Coordenação Centro Cirúrgico',      '192.168.21.199', '00:26:8b:9d:2c:c0', NULL,  'Telefone IP — 3º Andar; anotação de origem: "SUPERVISAO CC"'),
  ('f00d0000-0000-4000-8000-000000000150'::uuid, '3º Andar', 'Conforto Médico',                   '192.168.10.184', '00:26:8b:9d:66:39', NULL,  'Telefone IP — 3º Andar; anotação de origem: "Agenda CC"'),
  -- ===== Pronto-Socorro (8) =====
  ('f00d0000-0000-4000-8000-000000000151'::uuid, 'Pronto-Socorro', 'Posto de Enfermagem PS',     '192.168.21.147', '00:26:8b:9d:66:3a', '1584', 'Telefone IP — Pronto-Socorro'),
  ('f00d0000-0000-4000-8000-000000000152'::uuid, 'Pronto-Socorro', 'Supervisão Enfermagem PS',   '192.168.21.121', '00:26:8b:9d:61:7e', '1564', 'Telefone IP — Pronto-Socorro'),
  ('f00d0000-0000-4000-8000-000000000153'::uuid, 'Pronto-Socorro', 'Recepção PS 1550',           '192.168.21.182', '00:26:8b:9d:68:5a', '1550', 'Telefone IP — Pronto-Socorro (setor Recepção PS, aparelho 1)'),
  ('f00d0000-0000-4000-8000-000000000154'::uuid, 'Pronto-Socorro', 'Recepção PS 1506',           '192.168.21.181', '00:26:8b:9d:62:b1', '1506', 'Telefone IP — Pronto-Socorro (setor Recepção PS, aparelho 2)'),
  ('f00d0000-0000-4000-8000-000000000155'::uuid, 'Pronto-Socorro', 'Consultório 1',              '192.168.21.144', '00:26:8b:9d:66:37', '1586', 'Telefone IP — Pronto-Socorro'),
  ('f00d0000-0000-4000-8000-000000000156'::uuid, 'Pronto-Socorro', 'Consultório 2',              '192.168.21.133', '00:26:8b:9d:61:83', '1574', 'Telefone IP — Pronto-Socorro; IP de origem 193.168.21.133 corrigido para 192.168.21.133 (typo)'),
  ('f00d0000-0000-4000-8000-000000000157'::uuid, 'Pronto-Socorro', 'Consultório 3',              '192.168.21.14',  '00:26:8b:9d:68:5e', '1542', 'Telefone IP — Pronto-Socorro'),
  ('f00d0000-0000-4000-8000-000000000158'::uuid, 'Pronto-Socorro', 'Farmácia Satélite',          '192.168.21.149', '00:26:8b:9d:62:b8', '1502', 'Telefone IP — Pronto-Socorro')
) AS v(id, location_name, name, ip, mac, extension, description)
JOIN locations l
  ON LOWER(l.name) = LOWER(v.location_name)
-- Guards de idempotência e de colisão lógica (nome/ip/mac únicos por device):
WHERE NOT EXISTS (
    SELECT 1 FROM devices d WHERE LOWER(d.name) = LOWER(v.name)
  )
  AND NOT EXISTS (
    SELECT 1 FROM devices d WHERE v.ip IS NOT NULL AND d.ip = v.ip
  )
  AND NOT EXISTS (
    SELECT 1 FROM devices d WHERE v.mac IS NOT NULL AND LOWER(d.mac) = LOWER(v.mac)
  )
ON CONFLICT (id) DO NOTHING;

-- ============ CHECKS (perfil default do Phone = Ping, ADR-014) ============
INSERT INTO monitoring_checks (id, device_id, kind, enabled, "CreateDateTime", "UpdateDateTime")
SELECT gen_random_uuid(), d.id, 'Ping', true, NOW(), NOW()
FROM devices d
WHERE d.id::text LIKE 'f00d0000-%'
  AND d.type = 'Phone'
ON CONFLICT (device_id, kind) DO NOTHING;

COMMIT;
