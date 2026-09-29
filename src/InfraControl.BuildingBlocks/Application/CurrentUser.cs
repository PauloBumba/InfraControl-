namespace InfraControl.BuildingBlocks.Application;

public interface ICurrentUserContext
{
    Guid? UserId { get; }
    string? UserName { get; }
    bool IsAuthenticated { get; }
    IReadOnlyCollection<string> Roles { get; }
}
