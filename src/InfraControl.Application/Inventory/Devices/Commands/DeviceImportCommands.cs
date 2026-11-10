using InfraControl.Application.Inventory.Devices.Ports;
using InfraControl.Application.Inventory.Locations.Ports;
using InfraControl.Application.Monitoring.Checks.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Inventory.Devices.Commands;

/// <summary>
/// F50-T008: importação em massa de devices por CSV (colunas padronizadas,
/// separador `;` ou `,` autodetectado — o Excel em pt-BR salva com `;`).
/// Sucesso parcial linha a linha: o que é válido é criado, nome já existente
/// é pulado (re-importação idempotente) e linha inválida vira erro na linha,
/// sem abortar o arquivo inteiro.
/// </summary>
public sealed record ImportDevicesCommand(Guid LocationId, string Csv)
    : BaseCommand<ImportDevicesResult>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Operate;
}

/// <summary>Relatório da importação — uma linha por linha do CSV processada.</summary>
public sealed record ImportDevicesResult(
    IReadOnlyList<ImportDeviceRowResult> Rows,
    int Created,
    int Skipped,
    int Failed);

/// <summary>
/// <c>Status</c> em string (o padrão de DTO deste projeto) — "created",
/// "skipped" ou "error".
/// </summary>
public sealed record ImportDeviceRowResult(
    int Line,
    string Name,
    string Status,
    string Message);

/// <summary>Valores de <see cref="ImportDeviceRowResult.Status"/>.</summary>
public static class ImportRowStatus
{
    public const string Created = "created";
    public const string Skipped = "skipped";
    public const string Error = "error";
}

public sealed class ImportDevicesCommandHandler(
    IDeviceRepository repository,
    ILocationRepository locationRepository,
    IMonitoringCheckRepository checkRepository,
    ILogger<ImportDevicesCommandHandler> logger) : IRequestHandler<ImportDevicesCommand, ImportDevicesResult>
{
    /// <summary>Limite defensivo por importação (conteúdo colado no dialog).</summary>
    internal const int MaxRows = 5000;

    public async Task<ImportDevicesResult> Handle(
        ImportDevicesCommand request, CancellationToken cancellationToken)
    {
        var location = await locationRepository.GetByIdAsync(request.LocationId, cancellationToken);
        if (location is null)
            throw new KeyNotFoundException($"Local '{request.LocationId}' não encontrado.");

        var parsed = ParseCsv(request.Csv);
        var rows = new List<ImportDeviceRowResult>(parsed.Count);
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var created = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var line in parsed)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(line.Name))
                {
                    failed++;
                    rows.Add(new ImportDeviceRowResult(
                        line.Line, line.Name, ImportRowStatus.Error,
                        "O nome do device é obrigatório."));
                    continue;
                }

                if (!seenNames.Add(line.Name))
                {
                    failed++;
                    rows.Add(new ImportDeviceRowResult(
                        line.Line, line.Name, ImportRowStatus.Error,
                        $"Nome '{line.Name}' repetido no arquivo."));
                    continue;
                }

                var type = CreateDeviceCommandHandler.ParseType(line.Type);
                var device = Device.Create(
                    request.LocationId,
                    line.Name,
                    type,
                    ip: line.Ip,
                    mac: line.Mac,
                    serial: line.Serial,
                    model: line.Model,
                    vendor: line.Vendor,
                    description: line.Description);

                var existing = await repository.GetByNameAsync(device.Name, cancellationToken);
                if (existing is not null)
                {
                    skipped++;
                    rows.Add(new ImportDeviceRowResult(
                        line.Line, device.Name, ImportRowStatus.Skipped,
                        $"Já existe um device com o nome '{device.Name}'."));
                    continue;
                }

                await repository.AddAsync(device, cancellationToken);

                // Mesmo materializar o perfil do create manual (ADR-014 §4):
                // a UI de consumíveis da impressora depende de SnmpToner/
                // SnmpDrum nascerem aqui.
                await checkRepository.SynchronizeFromProfileAsync(
                    device.Id, device.MonitoringChecks, cancellationToken);
                await repository.SaveChangesAsync(cancellationToken);

                created++;
                rows.Add(new ImportDeviceRowResult(
                    line.Line, device.Name, ImportRowStatus.Created, "Device criado."));
            }
            catch (ArgumentException ex)
            {
                failed++;
                rows.Add(new ImportDeviceRowResult(
                    line.Line, line.Name, ImportRowStatus.Error, ex.Message));
            }
        }

        logger.LogInformation(
            "CSV import in location {LocationId}: {Created} created, {Skipped} skipped, {Failed} failed.",
            request.LocationId, created, skipped, failed);

        return new ImportDevicesResult(rows, created, skipped, failed);
    }

    /// <summary>Linha original do arquivo (1-based, cabeçalho inclusive).</summary>
    internal sealed record CsvLine(
        int Line,
        string Name,
        string? Ip,
        string Type,
        string? Vendor,
        string? Model,
        string? Mac,
        string? Serial,
        string? Description);

    /// <summary>
    /// Cabeçalho obrigatório: <c>nome</c> e <c>tipo</c> (ordem livre);
    /// demais colunas opcionais: <c>ip, vendor, modelo, mac, serial,
    /// descricao</c>. BOM do Excel é ignorado. Campos não podem conter o
    /// separador (o template usa `;`, que aceita vírgula dentro do texto).
    /// </summary>
    internal static IReadOnlyList<CsvLine> ParseCsv(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
            throw new ArgumentException("O conteúdo CSV está vazio.", nameof(csv));

        var content = csv.TrimStart('\uFEFF');
        var lines = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        var headerIndex = -1;
        for (var i = 0; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;
            headerIndex = i;
            break;
        }

        if (headerIndex < 0)
            throw new ArgumentException("O conteúdo CSV está vazio.", nameof(csv));

        var headerLine = lines[headerIndex];
        var separator = headerLine.Contains(';') ? ';' : ',';
        var header = headerLine
            .Split(separator)
            .Select(cell => cell.Trim().ToLowerInvariant())
            .ToArray();

        var nameCol = Array.IndexOf(header, "nome");
        var typeCol = Array.IndexOf(header, "tipo");
        if (nameCol < 0 || typeCol < 0)
            throw new ArgumentException(
                "Formato inválido: a primeira linha precisa conter as colunas 'nome' e 'tipo'. " +
                "Colunas aceitas: nome, ip, tipo, vendor, modelo, mac, serial, descricao.",
                nameof(csv));

        var ipCol = Array.IndexOf(header, "ip");
        var vendorCol = Array.IndexOf(header, "vendor");
        var modelCol = Array.IndexOf(header, "modelo");
        var macCol = Array.IndexOf(header, "mac");
        var serialCol = Array.IndexOf(header, "serial");
        var descriptionCol = Array.IndexOf(header, "descricao");

        var result = new List<CsvLine>();
        for (var i = headerIndex + 1; i < lines.Length; i++)
        {
            var raw = lines[i];
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            var cells = raw.Split(separator);
            string? Cell(int column) =>
                column >= 0 && column < cells.Length ? cells[column].Trim() : null;

            result.Add(new CsvLine(
                i + 1,
                Cell(nameCol) ?? string.Empty,
                Cell(ipCol),
                Cell(typeCol) ?? string.Empty,
                Cell(vendorCol),
                Cell(modelCol),
                Cell(macCol),
                Cell(serialCol),
                Cell(descriptionCol)));

            if (result.Count > MaxRows)
                throw new ArgumentException(
                    $"O arquivo excede o limite de {MaxRows} dispositivos por importação.",
                    nameof(csv));
        }

        if (result.Count == 0)
            throw new ArgumentException(
                "O CSV não tem linhas de dados após o cabeçalho.", nameof(csv));

        return result;
    }
}
