using InfraControl.Application.Company.Ports;
using InfraControl.BuildingBlocks.Application;
using InfraControl.Domain.Entities;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Company;

public sealed record CompanyDepartmentDto(
    Guid Id,
    string Name,
    string? Responsible);

/// <summary>Visão institucional da empresa para UI e relatórios (spec §3).</summary>
public sealed record CompanySettingsDto(
    Guid Id,
    string Name,
    string? LegalName,
    string? Cnpj,
    string? LogoPath,
    string? Street,
    string? Number,
    string? Complement,
    string? District,
    string? City,
    string? State,
    string? ZipCode,
    string? Phone,
    string? Email,
    string? Website,
    IReadOnlyCollection<CompanyDepartmentDto> Departments,
    DateTime? UpdatedAt)
{
    public static CompanySettingsDto From(CompanySettings settings) => new(
        settings.Id,
        settings.Name,
        settings.LegalName,
        settings.Cnpj,
        settings.LogoPath,
        settings.Street,
        settings.Number,
        settings.Complement,
        settings.District,
        settings.City,
        settings.State,
        settings.ZipCode,
        settings.Phone,
        settings.Email,
        settings.Website,
        settings.Departments
            .OrderBy(d => d.Name)
            .Select(d => new CompanyDepartmentDto(d.Id, d.Name, d.Responsible))
            .ToArray(),
        settings.UpdateDateTime);
}

public sealed record GetCompanySettingsQuery
    : IQuery<CompanySettingsDto?>, IAuthorizedRequest
{
    // Leitura ampla: relatórios e cabeçalhos institucionais são consumidos
    // por qualquer usuário autenticado; a escrita é Administer.
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Read;
}

public sealed class GetCompanySettingsQueryHandler(ICompanySettingsRepository repository)
    : IRequestHandler<GetCompanySettingsQuery, CompanySettingsDto?>
{
    public async Task<CompanySettingsDto?> Handle(
        GetCompanySettingsQuery request, CancellationToken cancellationToken)
    {
        var settings = await repository.GetAsync(cancellationToken);
        return settings is null ? null : CompanySettingsDto.From(settings);
    }
}

public sealed record CompanyDepartmentInput(string Name, string? Responsible = null);

public sealed record UpsertCompanySettingsCommand(
    string? Name = null,
    string? LegalName = null,
    string? Cnpj = null,
    string? LogoPath = null,
    string? Street = null,
    string? Number = null,
    string? Complement = null,
    string? District = null,
    string? City = null,
    string? State = null,
    string? ZipCode = null,
    string? Phone = null,
    string? Email = null,
    string? Website = null,
    IReadOnlyCollection<CompanyDepartmentInput>? Departments = null)
    : BaseCommand<CompanySettingsDto>, IAuthorizedRequest, IAuditableRequest
{
    public string AuditResource => "CompanySettings";
    public IReadOnlyCollection<string> RequiredRoles => RoleAccess.Administer;
}

public sealed class UpsertCompanySettingsCommandHandler(
    ICompanySettingsRepository repository,
    ILogger<UpsertCompanySettingsCommandHandler> logger)
    : IRequestHandler<UpsertCompanySettingsCommand, CompanySettingsDto>
{
    public async Task<CompanySettingsDto> Handle(
        UpsertCompanySettingsCommand request, CancellationToken cancellationToken)
    {
        var cnpj = string.IsNullOrWhiteSpace(request.Cnpj) ? null : request.Cnpj.Trim();
        if (cnpj is not null)
        {
            var digits = new string(cnpj.Where(char.IsDigit).ToArray());
            if (digits.Length != 14)
                throw new ArgumentException(
                    "O CNPJ deve conter 14 dígitos.", nameof(request.Cnpj));
        }

        var departments = (request.Departments ?? [])
            .Select(d => CompanyDepartment.Create(d.Name, d.Responsible))
            .ToArray();

        var settings = await repository.GetAsync(cancellationToken);
        if (settings is null)
        {
            settings = CompanySettings.Create(request.Name!);
            await repository.AddAsync(settings, cancellationToken);
        }

        settings.Update(
            request.Name!,
            request.LegalName,
            cnpj,
            request.LogoPath,
            request.Street,
            request.Number,
            request.Complement,
            request.District,
            request.City,
            request.State,
            request.ZipCode,
            request.Phone,
            request.Email,
            request.Website,
            departments);

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("CompanySettings {Id} upserted.", settings.Id);
        return CompanySettingsDto.From(settings);
    }
}
