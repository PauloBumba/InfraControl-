#Requires -Version 5.1
<#
  audit-collect.ps1 - Coleta de diagnostico do Zabbix 8.0 para auditoria/homologacao.

  API JSON-RPC (subselects como array de campos - padrao Zabbix 8.0)
  + SQL direto no PostgreSQL (schema 8.0: interface, item_rtdata, trigger_rtdata,
  problem, events, alerts, media_type_message).

  Uso:
    .\audit-collect.ps1 [-ZabbixUrl http://localhost:8086] [-OutDir C:\temp\audit]

  Credenciais: env ZABBIX_API_USER / ZABBIX_API_PASSWORD (padrao Admin/zabbix).
#>
param(
    [string]$ZabbixUrl = "http://localhost:8086",
    [string]$OutDir = (Join-Path $env:TEMP "zabbix-audit")
)

$ErrorActionPreference = "Stop"
$script:ZabbixUrl = $ZabbixUrl.TrimEnd("/")
$script:apiToken = $null
$script:reqId = 0

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function Invoke-ZabbixApi {
    param([string]$Method, [hashtable]$Params, [string]$OutFile)
    $script:reqId++
    $payload = @{ jsonrpc = "2.0"; method = $Method; params = $Params; id = $script:reqId }
    $json = $payload | ConvertTo-Json -Depth 30 -Compress
    $bodyFile = Join-Path $env:TEMP ("zbx-req-" + [guid]::NewGuid().ToString("N") + ".json")
    $respFile = Join-Path $env:TEMP ("zbx-resp-" + [guid]::NewGuid().ToString("N") + ".json")
    [System.IO.File]::WriteAllText($bodyFile, $json, (New-Object System.Text.UTF8Encoding($false)))
    try {
        & curl.exe -s --max-time 600 -X POST "$script:ZabbixUrl/api_jsonrpc.php" `
            -H "Content-Type: application/json" -H "Authorization: Bearer $($script:apiToken)" `
            --data-binary "@$bodyFile" -o $respFile
        $respText = [System.IO.File]::ReadAllText($respFile)
        if ([string]::IsNullOrWhiteSpace($respText)) { throw "resposta vazia" }
        $resp = $respText | ConvertFrom-Json
        if ($resp.error) {
            throw "Zabbix API $Method error $($resp.error.code): $($resp.error.message) $($resp.error.data)"
        }
        if ($OutFile) {
            ($resp.result | ConvertTo-Json -Depth 30) | Out-File -FilePath $OutFile -Encoding utf8
            Write-Host ("  {0}: {1} registros -> {2}" -f $Method, @($resp.result).Count, (Split-Path $OutFile -Leaf))
        }
        return $resp.result
    }
    finally {
        Remove-Item $bodyFile, $respFile -ErrorAction SilentlyContinue
    }
}

function Invoke-ApiSafe {
    param([string]$Label, [string]$Method, [hashtable]$Params, [string]$OutFile)
    try {
        Invoke-ZabbixApi $Method $Params $OutFile
    }
    catch {
        Write-Host ("  [ERRO] {0} ({1}): {2}" -f $Label, $Method, $_.Exception.Message)
    }
}

function Invoke-Psql {
    param([string]$Sql, [string]$OutFile)
    try {
        $out = & docker exec infracontrol-zabbix-db psql -U zabbix -d zabbix -tA -F "|" -c $Sql 2>&1
        $out | Out-File -FilePath $OutFile -Encoding utf8
        Write-Host ("  SQL -> {0} ({1} linhas)" -f (Split-Path $OutFile -Leaf), @($out).Count)
    }
    catch {
        Write-Host ("  [ERRO] SQL {0}: {1}" -f (Split-Path $OutFile -Leaf), $_.Exception.Message)
    }
}

# ---- login ----
$apiUser = if ($env:ZABBIX_API_USER) { $env:ZABBIX_API_USER } else { "Admin" }
$apiPass = if ($env:ZABBIX_API_PASSWORD) { $env:ZABBIX_API_PASSWORD } else { "zabbix" }
$loginBody = (@{ jsonrpc = "2.0"; method = "user.login"
    params = @{ username = $apiUser; password = $apiPass }; id = 1 } | ConvertTo-Json -Compress)
$lf = Join-Path $env:TEMP "zbx-login-audit.json"
[System.IO.File]::WriteAllText($lf, $loginBody, (New-Object System.Text.UTF8Encoding($false)))
& curl.exe -s -X POST "$script:ZabbixUrl/api_jsonrpc.php" -H "Content-Type: application/json" --data-binary "@$lf" -o "$lf.resp"
$login = ([System.IO.File]::ReadAllText("$lf.resp")) | ConvertFrom-Json
Remove-Item $lf, "$lf.resp" -ErrorAction SilentlyContinue
if ($login.error) { throw "Login falhou: $($login.error.message)" }
$script:apiToken = $login.result
Write-Host "Login OK ($apiUser). OutDir=$OutDir"

# ---- API (Zabbix 8.0: subselects = array de campos) ----
Invoke-ApiSafe "hosts" "host.get" @{
    output = @("hostid", "host", "name", "status", "flags", "description", "monitored_by")
    selectInterfaces = @("interfaceid", "type", "main", "useip", "ip", "dns", "port", "available", "error")
    selectParentTemplates = @("templateid", "name")
    selectGroups = @("groupid", "name")
    limit = 5000
} (Join-Path $OutDir "hosts_all.json")

Invoke-ApiSafe "itens-unsupported" "item.get" @{
    output = @("itemid", "hostid", "name", "key_", "type", "status", "flags")
    filter = @{ state = "1" }
    limit = 5000
} (Join-Path $OutDir "items_unsupported.json")

Invoke-ApiSafe "problemas" "problem.get" @{
    recent = $true
    output = @("eventid", "objectid", "name", "severity", "clock", "acknowledged", "suppressed", "cause_eventid", "r_eventid")
    selectTags = @("tag", "value")
    limit = 5000
} (Join-Path $OutDir "problems.json")

Invoke-ApiSafe "triggers-desabilitadas" "trigger.get" @{
    output = @("triggerid", "description", "expression", "status", "priority", "lastchange", "templateid", "type", "flags", "manual_close", "comments", "recovery_mode")
    filter = @{ status = "1" }
    limit = 5000
} (Join-Path $OutDir "triggers_disabled.json")

Invoke-ApiSafe "triggers-todas" "trigger.get" @{
    output = @("triggerid", "description", "status", "priority", "lastchange", "templateid", "type", "flags")
    limit = 10000
} (Join-Path $OutDir "triggers_all.json")

Invoke-ApiSafe "actions" "action.get" @{
    output = "extend"
    selectOperations = "extend"
} (Join-Path $OutDir "actions.json")

Invoke-ApiSafe "mediatypes" "mediatype.get" @{
    output = "extend"
    limit = 5000
} (Join-Path $OutDir "mediatypes.json")

Invoke-ApiSafe "users" "user.get" @{
    output = @("userid", "username", "name", "surname", "roleid", "lang", "theme", "rows_per_page", "timezone")
    limit = 5000
} (Join-Path $OutDir "users.json")

# ---- SQL agregacoes (schema Zabbix 8.0) ----
Invoke-Psql "SELECT 'hosts_real|' || count(*) FROM hosts WHERE flags=0 UNION ALL SELECT 'hosts_templates|' || count(*) FROM hosts WHERE flags=4 UNION ALL SELECT 'hosts_disabled|' || count(*) FROM hosts WHERE flags=0 AND status<>0 UNION ALL SELECT 'hosts_by_status|' || status || '|' || count(*) FROM hosts WHERE flags=0 GROUP BY status" "sql_hosts.txt"
Invoke-Psql "SELECT 'group|' || g.name || '|' || count(DISTINCT hg.hostid) FROM hosts_groups hg JOIN hstgrp g ON g.groupid=hg.groupid JOIN hosts h ON h.hostid=hg.hostid AND h.flags=0 GROUP BY g.name ORDER BY 3 DESC" "sql_groups.txt"
Invoke-Psql "SELECT 'dup_ip|' || i.ip || '|' || count(DISTINCT i.hostid) FROM interface i JOIN hosts h ON h.hostid=i.hostid WHERE h.flags=0 AND i.main=1 AND i.ip<>'' GROUP BY i.ip HAVING count(DISTINCT i.hostid)>1 ORDER BY 3 DESC" "sql_dup_ip.txt"
Invoke-Psql "SELECT 'dup_dns|' || i.dns || '|' || count(DISTINCT i.hostid) FROM interface i JOIN hosts h ON h.hostid=i.hostid WHERE h.flags=0 AND i.main=1 AND i.dns<>'' GROUP BY i.dns HAVING count(DISTINCT i.hostid)>1 ORDER BY 3 DESC" "sql_dup_dns.txt"
Invoke-Psql "SELECT 'item_status|' || i.status || '|state|' || COALESCE(r.state,-1) || '|' || count(*) FROM items i JOIN hosts h ON h.hostid=i.hostid LEFT JOIN item_rtdata r ON r.itemid=i.itemid WHERE h.flags=0 GROUP BY i.status, r.state ORDER BY 1,2" "sql_items_state.txt"
Invoke-Psql "SELECT 'unsupported_detail_count|' || count(*) FROM items i JOIN hosts h ON h.hostid=i.hostid JOIN item_rtdata r ON r.itemid=i.itemid WHERE h.flags=0 AND r.state=1" "sql_items_unsupported_count.txt"
Invoke-Psql "SELECT 'item_never_hist|' || count(*) FROM items i JOIN hosts h ON h.hostid=i.hostid WHERE h.flags=0 AND i.status=0 AND NOT EXISTS (SELECT 1 FROM history_uint hu WHERE hu.itemid=i.itemid) AND NOT EXISTS (SELECT 1 FROM history hst WHERE hst.itemid=i.itemid) AND NOT EXISTS (SELECT 1 FROM history_str hs WHERE hs.itemid=i.itemid) AND NOT EXISTS (SELECT 1 FROM history_text ht WHERE ht.itemid=i.itemid) AND NOT EXISTS (SELECT 1 FROM history_log hl WHERE hl.itemid=i.itemid) AND NOT EXISTS (SELECT 1 FROM history_json hj WHERE hj.itemid=i.itemid)" "sql_items_never.txt"
Invoke-Psql "SELECT 'trig_status|' || t.status || '|value|' || COALESCE(rt.value,-1) || '|' || count(*) FROM triggers t LEFT JOIN trigger_rtdata rt ON rt.triggerid=t.triggerid JOIN functions f ON f.triggerid=t.triggerid JOIN items i ON i.itemid=f.itemid JOIN hosts h ON h.hostid=i.hostid AND h.flags=0 GROUP BY t.status, rt.value ORDER BY 1,2" "sql_triggers.txt"
Invoke-Psql "SELECT 'trig_disabled_with_problem|' || count(DISTINCT t.triggerid) FROM triggers t JOIN trigger_rtdata rt ON rt.triggerid=t.triggerid AND rt.value=1 JOIN functions f ON f.triggerid=t.triggerid JOIN items i ON i.itemid=f.itemid JOIN hosts h ON h.hostid=i.hostid AND h.flags=0 WHERE t.status=1" "sql_triggers_disabled_problem.txt"
Invoke-Psql "SELECT 'problem_sev|' || severity || '|' || count(*) FROM problem GROUP BY severity ORDER BY 1" "sql_problems_by_sev.txt"
Invoke-Psql "SELECT 'problem_src|' || source || '|obj|' || object || '|' || count(*) FROM problem GROUP BY source, object ORDER BY 1,2" "sql_problems_src.txt"
Invoke-Psql "SELECT 'event_24h|' || value || '|' || count(*) FROM events WHERE clock > extract(epoch from now()) - 86400 AND source=0 GROUP BY value" "sql_events_24h.txt"
Invoke-Psql "SELECT 'event_72h|' || value || '|' || count(*) FROM events WHERE clock > extract(epoch from now()) - 72*3600 AND source=0 GROUP BY value" "sql_events_72h.txt"
Invoke-Psql "SELECT to_char(to_timestamp(clock),'YYYY-MM-DD') || '|' || value || '|' || count(*) FROM events WHERE source=0 AND clock > extract(epoch from now()) - 14*86400 GROUP BY 1,2 ORDER BY 1,2" "sql_events_by_day.txt"
Invoke-Psql "SELECT 'alert_24h_status|' || status || '|type|' || alerttype || '|' || count(*) FROM alerts WHERE clock > extract(epoch from now()) - 86400 GROUP BY status, alerttype ORDER BY 1,2" "sql_alerts_24h.txt"
Invoke-Psql "SELECT 'alert_24h_mediatype|' || mediatypeid || '|' || count(*) || '|errors|' || count(*) FILTER (WHERE error<>'' AND error IS NOT NULL) FROM alerts WHERE clock > extract(epoch from now()) - 86400 GROUP BY mediatypeid ORDER BY 2 DESC" "sql_alerts_mediatype.txt"
Invoke-Psql "SELECT 'alert_error|' || COALESCE(NULLIF(error,''),'(none)') || '|' || count(*) FROM alerts WHERE clock > extract(epoch from now()) - 7*86400 GROUP BY error ORDER BY 3 DESC LIMIT 15" "sql_alerts_errors.txt"
Invoke-Psql "SELECT 'interface_avail|' || i.available || '|type|' || i.type || '|' || count(*) FROM interface i JOIN hosts h ON h.hostid=i.hostid WHERE h.flags=0 AND i.main=1 GROUP BY i.available, i.type ORDER BY 1,2" "sql_iface_avail.txt"
Invoke-Psql "SELECT h.host || '|' || i.ip || '|' || i.dns || '|' || i.port || '|' || i.type || '|' || i.available || '|' || COALESCE(i.error,'') FROM interface i JOIN hosts h ON h.hostid=i.hostid WHERE h.flags=0 AND i.main=1 ORDER BY h.host" "sql_interfaces_main.txt"
Invoke-Psql "SELECT 'template_used|' || t.name || '|' || count(*) FROM hosts_templates ht JOIN hosts t ON t.hostid=ht.templateid AND t.flags=4 JOIN hosts h ON h.hostid=ht.hostid AND h.flags=0 GROUP BY t.name ORDER BY 2 DESC" "sql_templates_usage.txt"
Invoke-Psql "SELECT 'template_unused|' || t.name FROM hosts t WHERE t.flags=4 AND t.hostid NOT IN (SELECT templateid FROM hosts_templates ht JOIN hosts h ON h.hostid=ht.hostid WHERE h.flags=0) ORDER BY 1" "sql_templates_unused.txt"
Invoke-Psql "SELECT 'item_err|' || h.host || '|' || i.name || '|' || COALESCE(r.error,'') FROM item_rtdata r JOIN items i ON i.itemid=r.itemid JOIN hosts h ON h.hostid=i.hostid WHERE h.flags=0 AND r.state=1 ORDER BY h.host LIMIT 100" "sql_items_unsupported_detail.txt"
Invoke-Psql "SELECT h.host || '|' || i.name || '|' || i.key_ FROM items i JOIN hosts h ON h.hostid=i.hostid WHERE h.flags=0 AND i.status=0 AND NOT EXISTS (SELECT 1 FROM history_uint hu WHERE hu.itemid=i.itemid) AND NOT EXISTS (SELECT 1 FROM history hst WHERE hst.itemid=i.itemid) AND NOT EXISTS (SELECT 1 FROM history_str hs WHERE hs.itemid=i.itemid) AND NOT EXISTS (SELECT 1 FROM history_text ht WHERE ht.itemid=i.itemid) AND NOT EXISTS (SELECT 1 FROM history_log hl WHERE hl.itemid=i.itemid) AND NOT EXISTS (SELECT 1 FROM history_json hj WHERE hj.itemid=i.itemid) ORDER BY h.host LIMIT 200" "sql_items_never_by_host.txt"
Invoke-Psql "SELECT DISTINCT ON (hu.itemid) i.key_ || '|' || hu.value_uint || '|' || to_char(to_timestamp(hu.clock),'YYYY-MM-DD HH24:MI') FROM history_uint hu JOIN items i ON i.itemid=hu.itemid WHERE i.key_ LIKE 'zabbix[process%' ORDER BY hu.itemid, hu.clock DESC" "sql_pinger.txt"
Invoke-Psql "SELECT DISTINCT ON (hu.itemid) i.key_ || '|' || hu.value_str FROM history_str hu JOIN items i ON i.itemid=hu.itemid WHERE i.key_ LIKE 'zabbix[queue%' ORDER BY hu.itemid, hu.clock DESC" "sql_queue.txt"
Invoke-Psql "SELECT 'mediatype|' || mediatypeid || '|' || name || '|type|' || type || '|status|' || status || '|maxattempts|' || maxattempts || '|attempt_interval|' || attempt_interval FROM media_type WHERE status=0 ORDER BY mediatypeid" "sql_mediatypes.txt"
Invoke-Psql "SELECT 'medmsg|' || m.mediatypeid || '|' || m.eventsource || '|recovery|' || m.recovery || '|' || left(m.subject,60) || '|' || left(m.message,80) FROM media_type_message m JOIN media_type mt ON mt.mediatypeid=m.mediatypeid WHERE mt.status=0 ORDER BY m.mediatypeid" "sql_mediatype_messages.txt"
Invoke-Psql "SELECT 'action|' || a.actionid || '|' || a.name || '|src|' || a.eventsource || '|status|' || a.status || '|esc|' || a.esc_period || '|pause_symp|' || a.pause_suppressed || '|notify_cancel|' || a.notify_if_canceled FROM actions a ORDER BY a.actionid" "sql_actions.txt"
Invoke-Psql "SELECT 'op|' || o.actionid || '|opid|' || o.operationid || '|type|' || o.operationtype || '|recovery|' || o.recovery || '|escstep|' || o.esc_step_from || '-' || o.esc_step_to || '|msg_mt|' || om.mediatypeid FROM operations o LEFT JOIN opmessage om ON om.operationid=o.operationid ORDER BY o.actionid, o.operationid" "sql_operations.txt"
Invoke-Psql "SELECT 'opcond|' || c.operationid || '|type|' || c.conditiontype || '|op|' || c.operator || '|val|' || c.value FROM opconditions c ORDER BY c.operationid" "sql_opconditions.txt"
Invoke-Psql "SELECT 'user|' || u.username || '|role|' || r.name || '|status|' || u.roleid FROM users u LEFT JOIN role r ON r.roleid=u.roleid ORDER BY u.userid" "sql_users.txt"
Invoke-Psql "SELECT 'problem_top|' || left(p.name,80) || '|' || count(*) FROM problem p GROUP BY p.name ORDER BY 2 DESC LIMIT 40" "sql_problem_top.txt"
Invoke-Psql "SELECT 'trig_dup|' || t.description || '|' || count(*) FROM triggers t JOIN functions f ON f.triggerid=t.triggerid JOIN items i ON i.itemid=f.itemid JOIN hosts h ON h.hostid=i.hostid AND h.flags=0 GROUP BY t.description HAVING count(*)>3 ORDER BY 2 DESC LIMIT 30" "sql_trigger_dup.txt"
Invoke-Psql "SELECT h.host || '|' || t.description || '|' || t.priority || '|' || COALESCE(rt.value,-1) || '|' || to_char(to_timestamp(COALESCE(rt.lastchange,0)),'YYYY-MM-DD HH24:MI') FROM triggers t JOIN trigger_rtdata rt ON rt.triggerid=t.triggerid JOIN functions f ON f.triggerid=t.triggerid JOIN items i ON i.itemid=f.itemid JOIN hosts h ON h.hostid=i.hostid AND h.flags=0 WHERE t.status=1 AND rt.value=1 ORDER BY h.host LIMIT 200" "sql_triggers_disabled_problem_list.txt"
Invoke-Psql "SELECT 'hist_growth|' || to_char(date_trunc('day', to_timestamp(clock)),'YYYY-MM-DD') || '|' || count(*) FROM history_uint WHERE clock > extract(epoch from now()) - 30*86400 GROUP BY 1 ORDER BY 1 DESC LIMIT 15" "sql_hist_growth.txt"
Invoke-Psql "SELECT 'db_size|' || pg_size_pretty(pg_database_size('zabbix'))" "sql_db_size.txt"
Invoke-Psql "SELECT 'trig_rt_err|' || t.description || '|' || left(rt.error,80) || '|' || count(*) FROM trigger_rtdata rt JOIN triggers t ON t.triggerid=rt.triggerid WHERE rt.error<>'' AND rt.error IS NOT NULL GROUP BY 1,2 ORDER BY 3 DESC LIMIT 20" "sql_trigger_errors.txt"

Write-Host ""
Write-Host "COLETA CONCLUIDA -> $OutDir"
