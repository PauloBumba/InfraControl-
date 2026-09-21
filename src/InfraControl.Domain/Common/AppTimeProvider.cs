namespace InfraControl.Domain.Common;

/// <summary>
/// Static accessor for TimeProvider.
/// In production, registers a São Paulo timezone provider.
/// In tests, can be replaced with a fake.
/// </summary>
public static class AppTimeProvider
{
    private static TimeProvider _current = TimeProvider.System;

    public static TimeProvider Current
    {
        get => _current;
        set => _current = value ?? throw new ArgumentNullException(nameof(value));
    }
}
