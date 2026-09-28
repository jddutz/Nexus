namespace Tests;

using Microsoft.Extensions.Logging;
using Nexus.Core.Events;
using Nexus.Input;
using Nexus.Input.Devices;
using Nexus.Input.Events;
using Silk.NET.Maths;

/// <summary>
/// Verifies EventHub diagnostics include serialized event payloads.
/// </summary>
public class EventHubTests
{
    /// <summary>
    /// Verifies draining logs the JSON payload and still dispatches cyclic event data.
    /// </summary>
    [Fact]
    public void Drain_logsSerializedEventAndDispatchesIt()
    {
        var logger = new CapturingLogger<EventHub>();
        var eventHub = new EventHub(logger, diagnosticsEnabled: true);
        var handler = new ProbeHandler();
        eventHub.Register(handler);
        var message = new ProbeEvent(42);
        message.Self = message;

        eventHub.Publish(message);
        eventHub.Drain();

        Assert.Equal(1, handler.EventCount);
        var logMessage = Assert.Single(logger.Messages);
        Assert.Contains(nameof(ProbeEvent), logMessage);
        Assert.Contains("\"Value\":42", logMessage);
        Assert.Contains("\"Self\":null", logMessage);
    }

    /// <summary>
    /// Verifies draining logs an event even when no handler is registered for it.
    /// </summary>
    [Fact]
    public void Drain_logsEventsWithoutHandlers()
    {
        var logger = new CapturingLogger<EventHub>();
        var eventHub = new EventHub(logger, diagnosticsEnabled: true);

        eventHub.Publish(new ProbeEvent(7));
        eventHub.Drain();

        var logMessage = Assert.Single(logger.Messages);
        Assert.Contains(nameof(ProbeEvent), logMessage);
        Assert.Contains("\"Value\":7", logMessage);
    }

    /// <summary>
    /// Verifies event dispatch continues without logging when diagnostics are disabled.
    /// </summary>
    [Fact]
    public void Drain_doesNotLogWhenDiagnosticsAreDisabled()
    {
        var logger = new CapturingLogger<EventHub>();
        var eventHub = new EventHub(logger);
        var handler = new ProbeHandler();

        eventHub.Register(handler);
        eventHub.Publish(new ProbeEvent(13));
        eventHub.Drain();

        Assert.Equal(1, handler.EventCount);
        Assert.Empty(logger.Messages);
    }

    /// <summary>
    /// Verifies mouse movement JSON contains captured coordinates and stable device identity.
    /// </summary>
    [Fact]
    public void Drain_logsMouseCoordinatesWithoutMutableDevicePosition()
    {
        var logger = new CapturingLogger<EventHub>();
        var eventHub = new EventHub(logger, diagnosticsEnabled: true, logHighFrequencyEvents: true);
        var mouse = new TestMouse(new Vector2D<float>(800, 600));

        eventHub.Publish(new MouseMovedEvent(mouse, new Vector2D<float>(12, 34)));
        eventHub.Drain();

        var logMessage = Assert.Single(logger.Messages);
        Assert.Contains("\"Position\":{\"X\":12,\"Y\":34}", logMessage);
        Assert.Contains("\"Name\":\"Test Mouse\"", logMessage);
        Assert.DoesNotContain("\"X\":800", logMessage);
        Assert.DoesNotContain("Length", logMessage);
    }

    /// <summary>
    /// Verifies an event type is suppressed after the count limit without suppressing dispatch or other event types.
    /// </summary>
    [Fact]
    public void Drain_suppressesFrequentEventTypeAndLogsTransition()
    {
        var logger = new CapturingLogger<EventHub>();
        var eventHub = new EventHub(logger, diagnosticsEnabled: true);
        var handler = new ProbeHandler();

        eventHub.Register(handler);
        for (var i = 0; i < 22; i++)
            eventHub.Publish(new ProbeEvent(i));

        eventHub.Drain();

        Assert.Equal(22, handler.EventCount);
        Assert.Equal(21, logger.Messages.Count);
        Assert.Contains("Suppressing diagnostic logging", logger.Messages[^1]);
        Assert.Contains(nameof(ProbeEvent), logger.Messages[^1]);

        eventHub.Publish(new OtherProbeEvent());
        eventHub.Drain();

        Assert.Equal(22, logger.Messages.Count);
        Assert.Contains(nameof(OtherProbeEvent), logger.Messages[^1]);
    }

    /// <summary>
    /// Verifies the diagnostic event count limit can be configured.
    /// </summary>
    [Fact]
    public void Drain_usesConfiguredEventLogLimit()
    {
        var logger = new CapturingLogger<EventHub>();
        var eventHub = new EventHub(logger, diagnosticsEnabled: true, eventLogLimit: 1);

        eventHub.Publish(new ProbeEvent(1));
        eventHub.Publish(new ProbeEvent(2));
        eventHub.Publish(new ProbeEvent(3));
        eventHub.Drain();

        Assert.Equal(2, logger.Messages.Count);
        Assert.Contains("Suppressing diagnostic logging", logger.Messages[^1]);
    }

    /// <summary>
    /// Represents an event with a cyclic reference for JSON diagnostics testing.
    /// </summary>
    private sealed class ProbeEvent(int value) : IEvent
    {
        /// <summary>
        /// Gets the event's sample value.
        /// </summary>
        public int Value { get; } = value;

        /// <summary>
        /// Gets or sets a self-reference used to verify cycle handling.
        /// </summary>
        public ProbeEvent? Self { get; set; }
    }

    /// <summary>
    /// Represents an event with an independent diagnostic suppression identity.
    /// </summary>
    private sealed class OtherProbeEvent : IEvent { }

    /// <summary>
    /// Counts handled probe events.
    /// </summary>
    private sealed class ProbeHandler
    {
        /// <summary>
        /// Gets the number of probe events handled.
        /// </summary>
        public int EventCount { get; private set; }

        /// <summary>
        /// Handles a probe event.
        /// </summary>
        /// <param name="message">The event being handled.</param>
        public void Handle(ProbeEvent message) => EventCount++;
    }

    /// <summary>
    /// Provides stable mouse identity with a current position distinct from the event snapshot.
    /// </summary>
    private sealed class TestMouse(Vector2D<float> position) : IMouseInputDevice
    {
        /// <inheritdoc />
        public InputDeviceId Id { get; } = new(19);

        /// <inheritdoc />
        public string Name => "Test Mouse";

        /// <inheritdoc />
        public bool IsConnected => true;

        /// <inheritdoc />
        public Vector2D<float> Position { get; } = position;

        /// <inheritdoc />
        public event Action<IMouseInputDevice, Vector2D<float>>? Moved
        {
            add { }
            remove { }
        }

        /// <inheritdoc />
        public event Action<IMouseInputDevice, MouseButtonEnum>? ButtonPressed
        {
            add { }
            remove { }
        }

        /// <inheritdoc />
        public event Action<IMouseInputDevice, MouseButtonEnum>? ButtonReleased
        {
            add { }
            remove { }
        }

        /// <inheritdoc />
        public event Action<IMouseInputDevice, Vector2D<float>>? WheelMoved
        {
            add { }
            remove { }
        }

        /// <inheritdoc />
        public bool IsButtonDown(MouseButtonEnum button) => false;
    }

    /// <summary>
    /// Captures formatted messages from an ILogger implementation.
    /// </summary>
    /// <typeparam name="TCategory">The logger category type.</typeparam>
    private sealed class CapturingLogger<TCategory> : ILogger<TCategory>
    {
        /// <summary>
        /// Gets formatted log messages.
        /// </summary>
        public List<string> Messages { get; } = [];

        /// <inheritdoc />
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <inheritdoc />
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => Messages.Add(formatter(state, exception));
    }
}
