using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Runtime;
using Nexus.Runtime.Abstractions;
using Nexus.Testing;

namespace Tests;

public class BasicRuntimeTests
{
    [Fact]
    public void TestRuntimeBuilder_happyPath_runsAndStopsRuntime()
    {
        var services = new ServiceCollection();
        var builder = new TestRuntimeBuilder(services);

        using var runtime = builder.Build();

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
    public void RuntimeBuilder_happyPath_runsAndStopsRuntime()
    {
        using var runtime = new RuntimeBuilder(
            new ServiceCollection(),
            new ConfigurationBuilder().Build()
        ).Build();

        runtime.Initialize();
        runtime.Update();
        runtime.Stop();

        Assert.True(runtime.IsInitialized);
        Assert.False(runtime.IsRunning);
    }

    [Fact]
    public void Initialize_isIdempotent()
    {
        using var runtime = BuildTestRuntime();

        runtime.Initialize();
        runtime.Initialize();

        Assert.True(runtime.IsInitialized);
        Assert.True(runtime.IsRunning);
    }

    [Fact]
    public void Update_beforeInitialize_throws()
    {
        using var runtime = BuildTestRuntime();

        Assert.Throws<InvalidOperationException>(() => runtime.Update());
    }

    [Fact]
    public void TestRuntimeBuilder_rejectsNullServices()
    {
        Assert.Throws<ArgumentNullException>(() => new TestRuntimeBuilder(null!));
    }

    [Fact]
    public void RuntimeBuilder_rejectsNullServices()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RuntimeBuilder(null!, new ConfigurationBuilder().Build())
        );
    }

    [Fact]
    public void RuntimeBuilder_rejectsNullConfiguration()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new RuntimeBuilder(new ServiceCollection(), null!)
        );
    }

    [Fact]
    public void Dispose_isIdempotent_andDisposesServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<DisposableService>();
        var runtime = new TestRuntimeBuilder(services).Build();
        var service = runtime.Services.GetRequiredService<DisposableService>();

        runtime.Dispose();
        runtime.Dispose();

        Assert.True(service.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => runtime.Initialize());
        Assert.Throws<ObjectDisposedException>(() => runtime.Update());
        Assert.Throws<ObjectDisposedException>(() => runtime.Stop());
    }

    [Fact]
    public void TestRuntimeBuilder_registersNoSubsystemsByDefault_butAllowsExplicitServices()
    {
        var services = new ServiceCollection();
        var marker = new ExplicitService();
        services.AddSingleton(marker);

        using var runtime = new TestRuntimeBuilder(services).Build();

        Assert.Same(marker, runtime.Services.GetRequiredService<ExplicitService>());
        Assert.Null(runtime.Services.GetService<IWindowService>());
    }

    [Fact]
    public void Builders_createTheSameConcreteRuntimeType()
    {
        using var testRuntime = BuildTestRuntime();
        using var standardRuntime = new RuntimeBuilder(
            new ServiceCollection(),
            new ConfigurationBuilder().Build()
        ).Build();

        Assert.IsType<NexusRuntime>(testRuntime);
        Assert.IsType<NexusRuntime>(standardRuntime);
    }

    private static IRuntime BuildTestRuntime() =>
        new TestRuntimeBuilder(new ServiceCollection()).Build();

    private sealed class ExplicitService;

    private sealed class DisposableService : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }
}
