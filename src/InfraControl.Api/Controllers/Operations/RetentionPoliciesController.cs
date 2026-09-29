using InfraControl.Application.Operations.Retention;
using InfraControl.BuildingBlocks.Application.Response;
using InfraControl.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using InfraControl.Api.Authorization;

namespace InfraControl.Api.Controllers.Operations;

/// <summary>
/// Políticas de retenção por tipo de dado (F12-T002, docs/11): sobrescrevem
/// os defaults de <c>Retention:*</c> usados pelo cleanup.
/// </summary>
[Route("api/v1/operations/retention-policies")]
[Authorize(Policy = AuthorizationPolicies.Administer)]
public sealed class RetentionPoliciesController(ISender sender) : BaseController
{
    /// <summary>Estado efetivo dos 7 tipos (valor da policy ou da config).</summary>
    [HttpGet]
    public async Task<IActionResult> GetPolicies(CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(new GetRetentionPoliciesQuery(), cancellationToken)));

    /// <summary>Estado efetivo de um tipo específico.</summary>
    [HttpGet("{dataType}")]
    public async Task<IActionResult> GetPolicy(
        string dataType,
        CancellationToken cancellationToken = default)
    {
        var parsed = Parse(dataType);
        return Ok(ApiResponse.Ok(await sender.Send(
            new GetRetentionPoliciesQuery(parsed), cancellationToken)));
    }

    /// <summary>Cadastra ou atualiza a política do tipo.</summary>
    [HttpPut("{dataType}")]
    public async Task<IActionResult> UpsertPolicy(
        string dataType,
        UpsertRetentionPolicyRequest request,
        CancellationToken cancellationToken = default)
        => Ok(ApiResponse.Ok(await sender.Send(
            new UpsertRetentionPolicyCommand(Parse(dataType), request.Days),
            cancellationToken)));

    /// <summary>Remove a política — o tipo volta ao default de config.</summary>
    [HttpDelete("{dataType}")]
    public async Task<IActionResult> DeletePolicy(
        string dataType,
        CancellationToken cancellationToken = default)
    {
        await sender.Send(new DeleteRetentionPolicyCommand(Parse(dataType)), cancellationToken);
        return Ok(ApiResponse.Ok());
    }

    private static RetentionDataType Parse(string dataType)
        => Enum.TryParse<RetentionDataType>(dataType, ignoreCase: true, out var parsed)
            ? parsed
            : throw new ArgumentException(
                "Tipo de dado de retenção inválido.", nameof(dataType));
}

/// <summary>Payload do PUT de política de retenção.</summary>
public sealed record UpsertRetentionPolicyRequest(int Days);
