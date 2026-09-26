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
using Nexus.Input;
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
        services.AddSingleton<IInputSystem, NoOpInputSystem>();
        services.AddSingleton<IGameSystem, NoOpGameSystem>();
        services.AddSingleton<IPhysicsSystem, NoOpPhysicsSystem>();
        services.AddSingleton<IGraphicsSystem, NoOpGraphicsSystem>();
        services.AddSingleton<IAudioSystem, NoOpAudioSystem>();
    }

    private sealed class ExplicitService;

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
        public IScene InitialScene { get; } = new Scene();

        public IScene? CurrentScene => null;

        public void Initialize() { }

        public void Update(double deltaTime) { }
    }

    private sealed class NoOpInputSystem : IInputSystem
    {
        public void Initialize() { }

        public void Update(double deltaTime) { }

        public bool Activate<TComponent>(TComponent component)
            where TComponent : class, IInputComponent => false;

        public bool Deactivate<TComponent>(TComponent component)
            where TComponent : class, IInputComponent => false;
    }

    private sealed class NoOpPhysicsSystem : IPhysicsSystem
    {
        public void Initialize() { }

        public void Update(double deltaTime) { }

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
            where TComponent : class, IGraphicsComponent => false;

        public bool Activate<TComponent>(TComponent component)
            where TComponent : class, IGraphicsComponent => false;

        public void Deactivate<TComponent>(TComponent component)
            where TComponent : class, IGraphicsComponent { }
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
