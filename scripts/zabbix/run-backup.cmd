@echo off
rem Wrapper do Task Scheduler (o schtasks trava com "powershell" direto no /tr)
rem Tarefa: "InfraControl Zabbix Backup" - diario 02:00 (docs/65 Sprint 1)
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\Users\paulo.bumba\Documents\GitHub\InfraControl-\scripts\zabbix\backup-zabbix.ps1"
exit /b %errorlevel%
