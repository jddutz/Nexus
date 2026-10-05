namespace Tests;

using Microsoft.Extensions.DependencyInjection;
using Nexus.Graphics.Vulkan;
using Nexus.Core;

/// <summary>Verifies that Vulkan collectors follow application-wide diagnostics settings.</summary>
public sealed class PerformanceCollectorOptionsTests
{
    /// <summary>Verifies that expensive validation defaults are development-only and printf is opt-in.</summary>
    [Fact]
    public void VulkanValidationFeatures_DefaultToDevelopmentOnlySettings()
    {
        var settings = new VulkanSettings();
#if DEBUG
        Assert.True(settings.EnableGpuAssistedValidation);
        Assert.True(settings.EnableBestPracticesValidation);
        Assert.True(settings.EnableSynchronizationValidation);
#else
        Assert.False(settings.EnableGpuAssistedValidation);
        Assert.False(settings.EnableBestPracticesValidation);
        Assert.False(settings.EnableSynchronizationValidation);
#endif
        Assert.False(settings.EnableShaderDebugPrintf);
    }

    /// <summary>Verifies that performance and diagnostic collectors follow their respective flags.</summary>
    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    public void Collectors_follow_independent_settings(bool enableMetrics, bool enableDiagnostics, bool debugging)
    {
        var services = new ServiceCollection();
        services.Configure<DiagnosticsSettings>(settings =>
        {
            settings.EnablePerformanceMetrics = enableMetrics;
            settings.EnableDiagnostics = enableDiagnostics;
            settings.Debugging = debugging;
        });
        services.AddVkGraphicsServices();

        using var provider = services.BuildServiceProvider();
        var metrics = provider.GetRequiredService<PerformanceMetrics>();
        var diagnostics = provider.GetRequiredService<PerformanceDiagnostics>();

        Assert.Equal(enableMetrics || debugging, metrics.IsEnabled);
        Assert.Equal(enableMetrics || enableDiagnostics || debugging, diagnostics.IsEnabled);
    }
}
