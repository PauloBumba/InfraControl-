using Microsoft.AspNetCore.Mvc;

namespace InfraControl.Api.Controllers;

/// <summary>
/// Base MVC dos controllers HTTP. Regras de negócio permanecem na Application;
/// esta classe concentra apenas metadados e helpers de transporte.
/// </summary>
[ApiController]
public abstract class BaseController : ControllerBase
{
    protected string? CorrelationId => HttpContext.TraceIdentifier;
}
