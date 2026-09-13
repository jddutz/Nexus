using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Audio;
using Nexus.Core;
using Nexus.GameModel;
using Nexus.Graphics;
using Nexus.Graphics.Resources;
using Nexus.Input;
using Nexus.Physics;
using Nexus.Runtime;
using Nexus.Testing;

namespace Tests;

public class BasicRuntimeTests
{
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

    private sealed class NoOpGameSystem : IGameSystem
    {
        public SceneId InitialSceneId => default;

        public void Initialize() { }

        public void Update(double deltaTime) { }
    }

    private sealed class NoOpInputSystem : IInputSystem
    {
        public void Initialize() { }

        public void Update(double deltaTime) { }
    }

    private sealed class NoOpPhysicsSystem : IPhysicsSystem
    {
        public void Initialize() { }

        public void Update(double deltaTime) { }
    }

    private sealed class NoOpGraphicsSystem : IGraphicsSystem
    {
        public IGraphicsResource[] ResourceCatalog { get; set; } = [];

        public void Initialize() { }

        public void Configure() { }

        public void Render() { }
    }

    private sealed class NoOpAudioSystem : IAudioSystem
    {
        public void Initialize() { }

        public void Update(double deltaTime) { }
    }
}
