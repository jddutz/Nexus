using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Audio;
using Nexus.Core;
using Nexus.Core.Scenes;
using Nexus.Graphics;
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
    public void TestRuntimeBuilder_happyPath_runsAndStopsRuntime()
    {
        var services = CreateRuntimeServices();
        var builder = new TestRuntimeBuilder(services);

        var runtime = builder.Build();

        Assert.False(runtime.IsInitialized);
        Assert.False(runtime.IsRunning);

        runtime.Initialize();

        Assert.True(runtime.IsInitialized);
        Assert.True(runtime.IsRunning);

        for (var frame = 0; frame < 5; frame++)
        {
            runtime.Update();
        }

        runtime.Stop();

        Assert.True(runtime.IsInitialized);
        Assert.False(runtime.IsRunning);
    }

    [Fact]
    public void Initialize_isIdempotent()
    {
        var runtime = BuildTestRuntime();

        runtime.Initialize();
        runtime.Initialize();

        Assert.True(runtime.IsInitialized);
        Assert.True(runtime.IsRunning);
    }

    [Fact]
    public void Update_beforeInitialize_throws()
    {
        var runtime = BuildTestRuntime();

        Assert.Throws<InvalidOperationException>(() => runtime.Update());
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
        services.AddSingleton<ITimingSource, TestTimingSource>();
        services.AddSingleton<IInputSystem, NoOpInputSystem>();
        services.AddSingleton<ISceneGraph, NoOpSceneTree>();
        services.AddSingleton<IPhysicsSystem, NoOpPhysicsSystem>();
        services.AddSingleton<IGraphicsSystem, NoOpGraphicsSystem>();
        services.AddSingleton<IAudioService, NoOpAudioService>();
    }

    private sealed class ExplicitService;

    private sealed class NoOpSceneTree : ISceneGraph
    {
        public SceneId InitialSceneId => default;

        public void Update() { }
    }

    private sealed class NoOpInputSystem : IInputSystem
    {
        public void Update() { }
    }

    private sealed class NoOpPhysicsSystem : IPhysicsSystem
    {
        public void Update() { }
    }

    private sealed class NoOpGraphicsSystem : IGraphicsSystem
    {
        public void Configure() { }

        public void Render() { }
    }

    private sealed class NoOpAudioService : IAudioService
    {
        public void Update() { }
    }
}
