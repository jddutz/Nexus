namespace Tests;

using Microsoft.Extensions.DependencyInjection;
using Nexus.Graphics.Vulkan;

/// <summary>Verifies that Vulkan performance and diagnostic collection use independent settings.</summary>
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

    /// <summary>Verifies that each collector follows only its corresponding Vulkan setting.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Collectors_follow_independent_settings(bool enableMetrics, bool enableDiagnostics)
    {
        var services = new ServiceCollection();
        services.Configure<VulkanSettings>(settings =>
        {
            settings.EnablePerformanceMetrics = enableMetrics;
            settings.EnableDiagnostics = enableDiagnostics;
        });
        services.AddVkGraphicsServices();

        using var provider = services.BuildServiceProvider();
        var metrics = provider.GetRequiredService<PerformanceMetrics>();
        var diagnostics = provider.GetRequiredService<PerformanceDiagnostics>();

        Assert.Equal(enableMetrics, metrics.IsEnabled);
        Assert.Equal(enableDiagnostics, diagnostics.IsEnabled);
    }
}
