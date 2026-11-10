using InfraControl.Api.Authorization;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Company;
using InfraControl.Application.Company;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers.Company;

/// <summary>
/// Configuração institucional da empresa (spec evolução 2026-11-07 §3):
/// singleton alimentado nos relatórios gerados. Leitura para todos os
/// autenticados; escrita exclusiva de Administrador.
/// </summary>
[Route("api/v1/company-settings")]
public sealed class CompanySettingsController(ISender sender) : BaseController
{
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetCompanySettingsQuery(), cancellationToken)));

    [HttpPut]
    [Authorize(Policy = AuthorizationPolicies.Administer)]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertCompanySettingsViewModel model,
        CancellationToken cancellationToken)
    {
        model ??= new UpsertCompanySettingsViewModel();

        var result = await sender.Send(
            new UpsertCompanySettingsCommand(
                model.Name,
                model.LegalName,
                model.Cnpj,
                model.LogoPath,
                model.Street,
                model.Number,
                model.Complement,
                model.District,
                model.City,
                model.State,
                model.ZipCode,
                model.Phone,
                model.Email,
                model.Website,
                model.Departments?
                    .Select(d => new CompanyDepartmentInput(d.Name, d.Responsible))
                    .ToArray()),
            cancellationToken);

        return Ok(ApiResponse.Ok(result));
    }
}
