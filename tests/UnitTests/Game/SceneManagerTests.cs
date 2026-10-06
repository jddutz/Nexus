namespace Tests;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nexus.Core.Events;
using Nexus.Game;

/// <summary>Verifies pending scene transition requests.</summary>
public class SceneManagerTests
{
    /// <summary>Verifies a matching scene-loaded event clears the pending request.</summary>
    [Fact]
    public void Handle_matchingSceneLoadedEventClearsPendingRequest()
    {
        var eventHub = new EventHub();
        var sceneManager = new SceneManager(eventHub, NullLogger<SceneManager>.Instance);

        sceneManager.LoadScene("MainMenu");

        Assert.True(sceneManager.IsSceneChangePending);
        Assert.Equal("MainMenu", sceneManager.PendingSceneId);

        var loadedScene = new MainMenuScene
        {
            MainCamera = new Nexus.Graphics.Cameras.StaticCamera(),
        };
        eventHub.Publish(new SceneLoadedEvent(loadedScene));
        eventHub.Drain();

        Assert.False(sceneManager.IsSceneChangePending);
        Assert.Equal(string.Empty, sceneManager.PendingSceneId);
    }

    /// <summary>Verifies the latest request replaces the previous pending request.</summary>
    [Fact]
    public void LoadScene_latestRequestReplacesPreviousRequest()
    {
        var sceneManager = new SceneManager(
            new EventHub(),
            NullLogger<SceneManager>.Instance
        );

        sceneManager.LoadScene("Previous");
        sceneManager.LoadScene("Next");

        Assert.True(sceneManager.IsSceneChangePending);
        Assert.Equal("Next", sceneManager.PendingSceneId);
    }

    /// <summary>Verifies a type request uses its scene registration attribute.</summary>
    [Fact]
    public void LoadScene_byTypeUsesSceneAttributeName()
    {
        var sceneManager = new SceneManager(
            new EventHub(),
            NullLogger<SceneManager>.Instance
        );

        sceneManager.LoadScene<SceneRegistryTests.OverriddenDiscoveredScene>();

        Assert.Equal("RenamedDiscoveredScene", sceneManager.PendingSceneId);
    }

    /// <summary>Verifies empty identifiers are rejected.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void LoadScene_byIdentifierThrowsForWhitespace(string sceneId)
    {
        var sceneManager = new SceneManager(
            new EventHub(),
            NullLogger<SceneManager>.Instance
        );

        Assert.Throws<ArgumentException>(() => sceneManager.LoadScene(sceneId));
        Assert.False(sceneManager.IsSceneChangePending);
    }

    /// <summary>Verifies a mismatched loaded scene is warned about and clears the request.</summary>
    [Fact]
    public void Handle_mismatchedSceneLoadedEventLogsWarningAndClearsPendingRequest()
    {
        var eventHub = new EventHub();
        var logger = new WarningRecordingLogger();
        var sceneManager = new SceneManager(eventHub, logger);
        sceneManager.LoadScene("ExpectedScene");

        var loadedScene = new OtherScene
        {
            MainCamera = new Nexus.Graphics.Cameras.StaticCamera(),
        };
        eventHub.Publish(new SceneLoadedEvent(loadedScene));
        eventHub.Drain();

        Assert.False(sceneManager.IsSceneChangePending);
        Assert.Equal(string.Empty, sceneManager.PendingSceneId);
        var warning = Assert.Single(logger.Warnings);
        Assert.Contains("ExpectedScene", warning);
        Assert.Contains(nameof(OtherScene), warning);
    }

    /// <summary>Verifies an unexpected scene-loaded event logs a warning without pending state.</summary>
    [Fact]
    public void Handle_sceneLoadedWithoutPendingRequestLogsWarning()
    {
        var eventHub = new EventHub();
        var logger = new WarningRecordingLogger();
        var sceneManager = new SceneManager(eventHub, logger);

        eventHub.Publish(
            new SceneLoadedEvent
            (
                new OtherScene { MainCamera = new Nexus.Graphics.Cameras.StaticCamera() }
            )
        );
        eventHub.Drain();

        Assert.False(sceneManager.IsSceneChangePending);
        Assert.Equal(string.Empty, sceneManager.PendingSceneId);
        Assert.Contains("without a pending scene change request", Assert.Single(logger.Warnings));
    }

    /// <summary>A scene whose identifier is derived from its type name.</summary>
    private sealed class OtherScene : Scene;

    /// <summary>A scene whose registration name is used to test matching loaded events.</summary>
    [Scene("MainMenu")]
    private sealed class MainMenuScene : Scene;

    /// <summary>Captures warning-level messages from the scene manager.</summary>
    private sealed class WarningRecordingLogger : ILogger<SceneManager>
    {
        /// <summary>Gets the warning messages that were logged.</summary>
        public List<string> Warnings { get; } = [];

        /// <summary>Creates no logging scope.</summary>
        /// <typeparam name="TState">The scope state type.</typeparam>
        /// <param name="state">The scope state.</param>
        /// <returns>Always null.</returns>
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        /// <summary>Enables all log levels for the test logger.</summary>
        /// <param name="logLevel">The level to check.</param>
        /// <returns>Always true.</returns>
        public bool IsEnabled(LogLevel logLevel) => true;

        /// <summary>Captures warning-level log messages.</summary>
        /// <typeparam name="TState">The log state type.</typeparam>
        /// <param name="logLevel">The message level.</param>
        /// <param name="eventId">The event identifier.</param>
        /// <param name="state">The formatted log state.</param>
        /// <param name="exception">An optional exception.</param>
        /// <param name="formatter">Formats the log state.</param>
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            if (logLevel == LogLevel.Warning)
                Warnings.Add(formatter(state, exception));
        }
    }
}
