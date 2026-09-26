using System.Diagnostics.Metrics;
using FluentAssertions;
using InfraControl.Application.Common.Behaviors;
using InfraControl.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace InfraControl.UnitTests.Infrastructure;

/// <summary>
/// Dia 5 — Performance (docs/20): medição dos requests MediatR —
/// Information quando rápido, Warning acima de Performance:SlowRequestMs,
/// outcome failure em exceção e histograma com tags request/outcome
/// (infracontrol.request.duration).
/// </summary>
public sealed class PerformanceBehaviorTests
{
    [Fact]
    public async Task FastRequest_ShouldLogInformationAndRecordHistogram()
    {
        var logger = new ListLogger<PerformanceBehavior<TestRequest, string>>();
        var behavior = Behavior(logger, slowRequestMs: 10_000);

        using var listener = StartListener();
        var response = await behavior.Handle(
            new TestRequest(), () => Task.FromResult("ok"), CancellationToken.None);

        response.Should().Be("ok");
        logger.Entries.Should().ContainSingle(entry =>
            entry.Level == LogLevel.Information &&
            entry.Message.Contains("completed in") &&
            entry.Message.Contains("success"));

        var measurement = listener.Measurements.Should().ContainSingle().Subject;
        measurement.Tags.Should().Contain(tag =>
            tag.Key == "request" && Equals(tag.Value, nameof(TestRequest)));
        measurement.Tags.Should().Contain(tag =>
            tag.Key == "outcome" && Equals(tag.Value, "success"));
    }

    [Fact]
    public async Task SlowRequest_ShouldLogWarning()
    {
        var logger = new ListLogger<PerformanceBehavior<TestRequest, string>>();
        var behavior = Behavior(logger, slowRequestMs: 1);

        await behavior.Handle(
            new TestRequest(),
            () => Task.Delay(50).ContinueWith(_ => "ok"),
            CancellationToken.None);

        logger.Entries.Should().ContainSingle(entry =>
            entry.Level == LogLevel.Warning &&
            entry.Message.Contains("completed in"));
        logger.Entries.Should().NotContain(entry => entry.Level == LogLevel.Information);
    }

    [Fact]
    public async Task FailingRequest_ShouldRethrowAndRecordFailureOutcome()
    {
        var logger = new ListLogger<PerformanceBehavior<TestRequest, string>>();
        var behavior = Behavior(logger, slowRequestMs: 10_000);

        using var listener = StartListener();
        var action = () => behavior.Handle(
            new TestRequest(),
            () => throw new InvalidOperationException("boom"),
            CancellationToken.None);

        (await action.Should().ThrowAsync<InvalidOperationException>())
            .WithMessage("boom");

        logger.Entries.Should().ContainSingle(entry =>
            entry.Message.Contains("failure"));

        var measurement = listener.Measurements.Should().ContainSingle().Subject;
        measurement.Tags.Should().Contain(tag =>
            tag.Key == "outcome" && Equals(tag.Value, "failure"));
    }

    private static PerformanceBehavior<TestRequest, string> Behavior(
        ListLogger<PerformanceBehavior<TestRequest, string>> logger, int slowRequestMs)
        => new(logger, Options.Create(new PerformanceOptions
        {
            SlowRequestMs = slowRequestMs
        }));

    private static MeasurementListener StartListener() => new();

    private sealed record TestRequest;

    private sealed class MeasurementListener : IDisposable
    {
        public List<(double Value, IReadOnlyList<KeyValuePair<string, object?>> Tags)> Measurements { get; } = [];

        public MeasurementListener()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Name == "infracontrol.request.duration")
                    listener.EnableMeasurementEvents(instrument);
            };
            _listener.SetMeasurementEventCallback<double>(
                (instrument, value, tags, _) =>
                    Measurements.Add((value, tags.ToArray())));
            _listener.Start();
        }

        private readonly MeterListener _listener = new();

        public void Dispose() => _listener.Dispose();
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
            => new NullScope();

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add((logLevel, formatter(state, exception)));
    }

    private sealed class NullScope : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
