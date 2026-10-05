using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nexus.Audio;
using Nexus.Core;
using Nexus.Core.Events;
using Nexus.Game;
using Nexus.Graphics;
using Nexus.Graphics.Vulkan;
using Nexus.GUI;
using Nexus.Input;
using Nexus.Input.Devices;
using Nexus.Physics;
using Nexus.Runtime;
using Nexus.Testing;
using Silk.NET.Windowing;

namespace Tests;

public class BasicRuntimeTests
{
    [Fact]
    public void VulkanGraphicsServices_includeValidation()
    {
        var services = new ServiceCollection();

        services.AddVkGraphicsServices();

        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IValidation)
                && descriptor.ImplementationType == typeof(Validation)
        );
    }

    private static INexusRuntime CreateRuntimeFixture() =>
        new RuntimeBuilder()
            .AddServices(CreateRuntimeServices())
            .AddConfiguration(new ConfigurationBuilder().Build())
            .Build();

    [Fact]
    public void TestRuntimeBuilder_happyPath_initializesRuntime()
    {
        var services = CreateRuntimeServices();
        var builder = new TestRuntimeBuilder(services);

        var runtime = builder.Build();

        Assert.False(runtime.IsInitialized);

        runtime.Initialize();

        Assert.True(runtime.IsInitialized);
    }

    [Fact]
    public void Initialize_isIdempotent()
    {
        var runtime = BuildTestRuntime();

        runtime.Initialize();
        runtime.Initialize();

        Assert.True(runtime.IsInitialized);
    }

    [Fact]
    public void Update_beforeInitialize_throws()
    {
        var runtime = Assert.IsType<NexusRuntime>(BuildTestRuntime());

        Assert.Throws<InvalidOperationException>(() => runtime.OnUpdate(0d));
    }

    [Fact]
    public void InitializeAndUpdate_orderServicesAroundSceneAndPhysicsLifecycle()
    {
        var calls = new List<string>();
        var runtime = new NexusRuntime(
            CreateRecordingProxy<IEventHub>("events", calls),
            CreateRecordingProxy<IGameSystem>("game", calls),
            CreateRecordingProxy<IPhysicsSystem>("physics", calls),
            CreateRecordingProxy<IAudioSystem>("audio", calls),
            CreateRecordingProxy<IInputSystem>("input", calls),
            CreateRecordingProxy<IGraphicsSystem>("graphics", calls),
            CreateRecordingProxy<IGraphicalUserInterface>("gui", calls)
        );

        runtime.Initialize();

        Assert.Equal(
            new[]
            {
                "input.Initialize",
                "physics.Initialize",
                "audio.Initialize",
                "graphics.Initialize",
                "gui.Initialize",
                "game.Initialize",
            },
            calls
        );

        calls.Clear();
        runtime.OnUpdate(1d / 60d);

        Assert.Equal(
            new[]
            {
                "events.Drain",
                "game.Update",
                "physics.Update",
                "gui.Update",
                "audio.Update",
                "input.Update",
            },
            calls
        );
    }

    [Fact]
    public void Render_requestsWindowClose_afterConfiguredFrameCount()
    {
        var window = DispatchProxy.Create<IWindow, CloseTrackingWindow>();
        var closeTrackingWindow = (CloseTrackingWindow)(object)window;
        var services = CreateRuntimeServices();
        services.AddSingleton<IEventHub, EventHub>();
        services.AddSingleton<IWindow>(window);
        services.AddSingleton<IOptions<ApplicationSettings>>(
            Options.Create(new ApplicationSettings { MaxFrameCount = 2 })
        );
        services.AddSingleton<INexusRuntime, NexusRuntime>();

        using var serviceProvider = services.BuildServiceProvider();
        var runtime = Assert.IsType<NexusRuntime>(
            serviceProvider.GetRequiredService<INexusRuntime>()
        );

        runtime.OnRender(0d);
        Assert.False(closeTrackingWindow.CloseWasRequested);

        runtime.OnRender(0d);
        Assert.True(closeTrackingWindow.CloseWasRequested);
    }

    [Fact]
    public void Render_requestsWindowClose_afterConfiguredRunTime()
    {
        var window = DispatchProxy.Create<IWindow, CloseTrackingWindow>();
        var closeTrackingWindow = (CloseTrackingWindow)(object)window;
        var services = CreateRuntimeServices();
        services.AddSingleton<IEventHub, EventHub>();
        services.AddSingleton<IWindow>(window);
        services.AddSingleton<IOptions<ApplicationSettings>>(
            Options.Create(new ApplicationSettings { MaxRunTime = TimeSpan.FromTicks(1) })
        );
        services.AddSingleton<INexusRuntime, NexusRuntime>();

        using var serviceProvider = services.BuildServiceProvider();
        var runtime = Assert.IsType<NexusRuntime>(
            serviceProvider.GetRequiredService<INexusRuntime>()
        );

        runtime.Initialize();
        Thread.Sleep(1);
        runtime.OnRender(0d);

        Assert.True(closeTrackingWindow.CloseWasRequested);
    }

    [Fact]
    public void RuntimeBuilder_useOpenGL_isNotImplemented()
    {
        Assert.Throws<NotImplementedException>(() => new RuntimeBuilder().UseOpenGL());
    }

    [Fact]
    public void TestRuntimeBuilder_rejectsNullServices()
    {
        Assert.Throws<ArgumentNullException>(() => new TestRuntimeBuilder(null!));
    }

    [Fact]
    public void RuntimeBuilder_usesExplicitServicesWithoutExposingTheProvider()
    {
        var services = new ServiceCollection();
        var marker = new ExplicitService();
        services.AddSingleton(marker);
        AddRuntimeServices(services);

        var runtime = new RuntimeBuilder()
            .AddServices(services)
            .AddConfiguration(new ConfigurationBuilder().Build())
            .Build();

        Assert.NotNull(runtime);
        Assert.Contains(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(ExplicitService)
                && descriptor.ImplementationInstance == marker
        );
    }

    [Fact]
    public void Builders_createTheSameConcreteRuntimeType()
    {
        var testRuntime = BuildTestRuntime();
        var standardRuntime = CreateRuntimeFixture();

        Assert.IsType<NexusRuntime>(testRuntime);
        Assert.IsType<NexusRuntime>(standardRuntime);
    }

    private static INexusRuntime BuildTestRuntime() =>
        new TestRuntimeBuilder(CreateRuntimeServices()).Build();

    private static IServiceCollection CreateRuntimeServices()
    {
        var services = new ServiceCollection();
        AddRuntimeServices(services);
        return services;
    }

    private static void AddRuntimeServices(IServiceCollection services)
    {
        services.AddNexusGui();
        services.AddSingleton<IInputSystem, NoOpInputSystem>();
        services.AddSingleton<IGameSystem, NoOpGameSystem>();
        services.AddSingleton<IPhysicsSystem, NoOpPhysicsSystem>();
        services.AddSingleton<IGraphicsSystem, NoOpGraphicsSystem>();
        services.AddSingleton<IAudioSystem, NoOpAudioSystem>();
    }

    /// <summary>Creates a proxy that records calls for runtime scheduling tests.</summary>
    /// <typeparam name="T">The service contract to proxy.</typeparam>
    /// <param name="name">The service name recorded with each call.</param>
    /// <param name="calls">The shared call log.</param>
    /// <returns>A configured proxy for <typeparamref name="T"/>.</returns>
    private static T CreateRecordingProxy<T>(string name, IList<string> calls)
        where T : class
    {
        var proxy = DispatchProxy.Create<T, CallRecordingProxy>();
        ((CallRecordingProxy)(object)proxy).Configure(name, calls);
        return proxy;
    }

    private sealed class ExplicitService;

    /// <summary>Records invoked service methods without executing system behavior.</summary>
    public class CallRecordingProxy : DispatchProxy
    {
        private string _name = string.Empty;
        private IList<string> _calls = [];

        /// <summary>Configures the service name and destination call log.</summary>
        /// <param name="name">The service name recorded with each call.</param>
        /// <param name="calls">The shared call log.</param>
        public void Configure(string name, IList<string> calls)
        {
            _name = name;
            _calls = calls;
        }

        /// <inheritdoc />
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod is null)
                throw new InvalidOperationException("A proxied method was not provided.");

            _calls.Add($"{_name}.{targetMethod.Name}");

            var returnType = targetMethod.ReturnType;
            return returnType == typeof(void) || !returnType.IsValueType
                ? null
                : Activator.CreateInstance(returnType);
        }
    }

    /// <summary>
    /// Tracks close requests made through a proxied window without creating a native window.
    /// </summary>
    public class CloseTrackingWindow : DispatchProxy
    {
        /// <summary>Gets whether the runtime requested that the window close.</summary>
        public bool CloseWasRequested { get; private set; }

        /// <inheritdoc />
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod?.Name == nameof(IWindow.Close))
                CloseWasRequested = true;

            var returnType = targetMethod?.ReturnType;
            return returnType is null || returnType == typeof(void) ? null
                : returnType.IsValueType ? Activator.CreateInstance(returnType)
                : null;
        }
    }

    private sealed class NoOpGameSystem : IGameSystem
    {
        public IScene? CurrentScene => null;

        public void Initialize() { }

        public void Update(double deltaTime) { }
    }

    private sealed class NoOpInputSystem : IInputSystem
    {
        public IKeyboardInputState Keyboard { get; } = new KeyboardInputState();

        /// <summary>Gets aggregate mouse state.</summary>
        public IMouseInputState Mouse { get; } = new MouseInputState();

        /// <summary>Gets the currently available controllers.</summary>
        public IReadOnlyCollection<IController> Controllers { get; } = [];

        /// <summary>Looks up a controller; this no-op system never registers one.</summary>
        /// <param name="id">The controller identifier.</param>
        /// <param name="controller">Always null.</param>
        /// <returns>Always false.</returns>
        public bool TryGetController(InputDeviceId id, out IController? controller)
        {
            controller = null;
            return false;
        }

        public void Initialize() { }

        public void Update(double deltaTime) { }
    }

    private sealed class NoOpPhysicsSystem : IPhysicsSystem
    {
        public IReadOnlyCollection<PhysicsWorld2D> Worlds { get; } = [];

        public void Initialize() { }

        public void Update(double deltaTime) { }

        public PhysicsWorld2D CreateWorld2D() => new();

        public bool RemoveWorld(PhysicsWorld2D world) => false;

        public bool CanActivate<TComponent>(TComponent component)
            where TComponent : class, IComponent => false;

        public bool Activate<TComponent>(TComponent component)
            where TComponent : class, IPhysicsComponent => false;

        public bool Deactivate<TComponent>(TComponent component)
            where TComponent : class, IPhysicsComponent => false;
    }

    private sealed class NoOpGraphicsSystem : IGraphicsSystem
    {
        public RenderLayerCollection RenderLayers { get; } = new();

        public void Initialize() { }

        public void Render() { }

        public bool CanActivate<TComponent>(TComponent component)
            where TComponent : class, IRenderer => false;

        public bool Activate<TComponent>(TComponent component)
            where TComponent : class, IRenderer => false;

        public void Deactivate<TComponent>(TComponent component)
            where TComponent : class, IRenderer { }
    }

    private sealed class NoOpAudioSystem : IAudioSystem
    {
        public void Initialize() { }

        public void Update(double deltaTime) { }

        public bool CanActivate<TComponent>(TComponent component)
            where TComponent : class, IComponent => false;

        public bool Activate<TComponent>(TComponent component)
            where TComponent : class, IAudioComponent => false;

        public bool Deactivate<TComponent>(TComponent component)
            where TComponent : class, IAudioComponent => false;
    }
}
