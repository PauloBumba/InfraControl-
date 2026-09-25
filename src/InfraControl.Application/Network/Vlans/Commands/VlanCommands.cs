using InfraControl.Application.Network.SwitchPortVlans.Ports;
using InfraControl.Application.Network.Vlans.Ports;
using InfraControl.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace InfraControl.Application.Network.Vlans.Commands;

// F3-T003: erros nascem aqui/domínio em pt-BR e sobem no envelope via
// GlobalExceptionHandler: ArgumentException → 400, KeyNotFoundException → 404,
// InvalidOperationException → 409 (número ou nome duplicado). Vlan é entidade
// avulsa — sem guard de pai. Registro do repositório é automático (F0-T024).

public sealed record CreateVlanCommand(
    string Name,
    int VlanNumber,
    string? Description = null) : BaseCommand<VlanDto>, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.InventoryManage.ToString()];
}

public sealed class CreateVlanCommandHandler(
    IVlanRepository repository,
    ILogger<CreateVlanCommandHandler> logger) : IRequestHandler<CreateVlanCommand, VlanDto>
{
    public async Task<VlanDto> Handle(CreateVlanCommand request, CancellationToken cancellationToken)
    {
        var vlan = Vlan.Create(request.Name, request.VlanNumber, request.Description);

        var existingNumber = await repository.GetByNumberAsync(vlan.VlanNumber, cancellationToken);
        if (existingNumber is not null)
            throw new InvalidOperationException($"Já existe uma VLAN com o número '{vlan.VlanNumber}'.");

        var existingName = await repository.GetByNameAsync(vlan.Name, cancellationToken);
        if (existingName is not null)
            throw new InvalidOperationException($"Já existe uma VLAN com o nome '{vlan.Name}'.");

        await repository.AddAsync(vlan, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Vlan {VlanId} created ({Name}, {VlanNumber}).",
            vlan.Id, vlan.Name, vlan.VlanNumber);

        return new VlanDto(vlan.Id, vlan.Name, vlan.VlanNumber, vlan.Description);
    }
}

public sealed record UpdateVlanCommand(
    Guid Id,
    string Name,
    int VlanNumber,
    string? Description = null) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.InventoryManage.ToString()];
}

public sealed class UpdateVlanCommandHandler(
    IVlanRepository repository,
    ILogger<UpdateVlanCommandHandler> logger) : IRequestHandler<UpdateVlanCommand>
{
    public async Task Handle(UpdateVlanCommand request, CancellationToken cancellationToken)
    {
        var vlan = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"VLAN '{request.Id}' não encontrada.");

        vlan.Update(request.Name, request.VlanNumber, request.Description);

        var existingNumber = await repository.GetByNumberAsync(vlan.VlanNumber, cancellationToken);
        if (existingNumber is not null && existingNumber.Id != vlan.Id)
            throw new InvalidOperationException($"Já existe uma VLAN com o número '{vlan.VlanNumber}'.");

        var existingName = await repository.GetByNameAsync(vlan.Name, cancellationToken);
        if (existingName is not null && existingName.Id != vlan.Id)
            throw new InvalidOperationException($"Já existe uma VLAN com o nome '{vlan.Name}'.");

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Vlan {VlanId} updated ({Name}, {VlanNumber}).",
            vlan.Id, vlan.Name, vlan.VlanNumber);
    }
}

public sealed record DeleteVlanCommand(Guid Id) : BaseCommand, IAuthorizedRequest
{
    public IReadOnlyCollection<string> RequiredPermissions =>
        [Permission.InventoryManage.ToString()];
}

public sealed class DeleteVlanCommandHandler(
    IVlanRepository repository,
    ISwitchPortVlanRepository switchPortVlanRepository,
    ILogger<DeleteVlanCommandHandler> logger) : IRequestHandler<DeleteVlanCommand>
{
    public async Task Handle(DeleteVlanCommand request, CancellationToken cancellationToken)
    {
        var vlan = await repository.GetByIdAsync(request.Id, cancellationToken)
            ?? throw new KeyNotFoundException($"VLAN '{request.Id}' não encontrada.");

        // F3-T004: VLANs com portas vinculadas não podem ser excluídas.
        if (await switchPortVlanRepository.HasPortLinksAsync(vlan.Id, cancellationToken))
            throw new InvalidOperationException("Não é possível excluir a VLAN: existem portas vinculadas.");

        repository.Remove(vlan);
        await repository.SaveChangesAsync(cancellationToken);

        // Sem outros dependentes: a N:N com portas é o vínculo (F3-T004);
        // devices.vlan_id (Guid?) existe sem FK até hoje.
        logger.LogInformation("Vlan {VlanId} deleted ({Name}, {VlanNumber}).",
            vlan.Id, vlan.Name, vlan.VlanNumber);
    }
}
