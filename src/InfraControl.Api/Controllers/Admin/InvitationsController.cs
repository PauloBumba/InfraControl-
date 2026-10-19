using System.Security.Claims;
using InfraControl.Application.Identity.Invitations.Commands;
using InfraControl.Application.Identity.Invitations.Queries;
using InfraControl.Api.Controllers;
using InfraControl.Api.ViewModels.Admin;
using InfraControl.BuildingBlocks.Application.Response;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using InfraControl.Api.Authorization;
using InfraControl.Api.Services;

namespace InfraControl.Api.Controllers.Admin;

/// <summary>
/// F0-T023: falhas (KeyNotFound/UnauthorizedAccess/InvalidOperation/Argument)
/// são embrulhadas pelo GlobalExceptionHandler — aqui só o sucesso compõe corpo.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.Administer)]
[Route("api/v1/admin/invitations")]
public sealed class InvitationsController(ISender sender, IOptions<FrontendOptions> frontendOptions) : BaseController
{
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? status = null,
        [FromQuery] string? email = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var statusEnum = Enum.TryParse<Domain.Enums.InvitationStatus>(status, true, out var parsed)
            ? parsed
            : (Domain.Enums.InvitationStatus?)null;

        var result = await sender.Send(
            new SearchInvitationsQuery(statusEnum, email, skip, take),
            cancellationToken);

        return Ok(ApiResponse.Ok(result));
    }

    [HttpGet("{invitationId:guid}")]
    public async Task<IActionResult> Get(Guid invitationId, CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new GetInvitationQuery(invitationId), cancellationToken)));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInvitationViewModel model, CancellationToken cancellationToken)
    {
        if (User.GetSessionUserIdOrNull() is not { } inviterId)
            return Unauthorized(ApiResponse<object>.Fail(["É necessário um convidante autenticado."]));

        // F1-T017: origem do link do convite — config Frontend:BaseUrl (decisão
        // do usuário 01/10/2026) com fallback à origem da própria request
        // (produção com nginx same-origin serve SPA e API no mesmo host).
        var configured = frontendOptions.Value.BaseUrl;
        var baseUrl = string.IsNullOrWhiteSpace(configured)
            ? $"{Request.Scheme}://{Request.Host}"
            : configured.TrimEnd('/');
        var result = await sender.Send(
            new CreateInvitationCommand(
                model.Email, ParseRoles(model.Roles), inviterId, model.ExpiresInDays, baseUrl),
            cancellationToken);

        return StatusCode(StatusCodes.Status201Created, ApiResponse.Ok(result));
    }

    [HttpPut("{invitationId:guid}")]
    public async Task<IActionResult> Update(
        Guid invitationId,
        [FromBody] UpdateInvitationViewModel model,
        CancellationToken cancellationToken)
        => Ok(ApiResponse.Ok(await sender.Send(
            new UpdateInvitationCommand(invitationId, ParseRoles(model.Roles)),
            cancellationToken)));

    [HttpDelete("{invitationId:guid}")]
    public async Task<IActionResult> Cancel(Guid invitationId, CancellationToken cancellationToken)
    {
        await sender.Send(new CancelInvitationCommand(invitationId), cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// ADR-061 §2 — contrato de enum = string no wire; o parse para
    /// <c>UserRole</c> acontece aqui (molde do Search de status acima;
    /// case-insensitive). Valor desconhecido → ArgumentException → 400
    /// pt-BR pelo GlobalExceptionHandler.
    /// </summary>
    private static IReadOnlyCollection<Domain.Enums.UserRole> ParseRoles(
        IReadOnlyCollection<string>? roles)
        => (roles ?? [])
            .Select(role => Enum.TryParse<Domain.Enums.UserRole>(
                    role, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed)
                ? parsed
                : throw new ArgumentException(
                    $"Função inválida: '{role}'. Use 'Admin', 'Operator' ou 'Viewer'."))
            .Distinct()
            .ToArray();
}
