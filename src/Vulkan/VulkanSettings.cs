namespace Nexus.Graphics.Vulkan;

/// <summary>
/// Vulkan-specific configuration for device selection and swap chain creation.
/// Provides sensible defaults for standard game rendering (99% use case).
/// </summary>
public sealed record VulkanSettings
{
    /// <summary>
    /// Preferred surface formats in priority order.
    /// First available format will be selected.
    /// Default: SRGB color space for proper gamma correction.
    /// </summary>
    public Format[] PreferredSurfaceFormats { get; set; } =
    [
        Format.B8G8R8A8Srgb, // BGRA with SRGB (most common on Windows/Linux)
        Format.R8G8B8A8Srgb, // RGBA with SRGB (fallback)
    ];

    /// <summary>
    /// Preferred present modes in priority order.
    /// First available mode will be selected.
    /// Default: Mailbox (triple buffering) for smooth gameplay without tearing.
    /// </summary>
    public PresentModeKHR[] PreferredPresentModes { get; set; } =
    [
        PresentModeKHR.MailboxKhr, // Triple buffering (ideal for games)
        PresentModeKHR.FifoKhr, // V-sync (guaranteed available per Vulkan spec)
    ];

    /// <summary>
    /// Minimum number of swap chain images.
    /// Default: 2 (double buffering) - driver may allocate more if needed.
    /// Set to 3 for explicit triple buffering if Mailbox is unavailable.
    /// </summary>
    public uint MinImageCount { get; set; } = 2;

    /// <summary>
    /// Enable transfer source usage for swapchain images.
    /// Required for pixel sampling (reading pixels back to CPU for testing).
    /// Default: false (production). Set to true for testing scenarios.
    /// Performance impact: Minimal - only affects image creation, not rendering.
    /// </summary>
    public bool EnableSwapchainTransfer { get; set; } = false;

    /// <summary>
    /// Required device extensions. Devices without ALL these extensions will be rejected.
    /// Default: Only swap chain support (minimal requirements).
    /// </summary>
    public string[] RequiredDeviceExtensions { get; set; } =
    [
        KhrSwapchain.ExtensionName, // "VK_KHR_swapchain"
    ];

    /// <summary>
    /// Optional device extensions. Device scoring will prefer devices with these extensions.
    /// Default: Empty (no optional extensions).
    /// Example: Add ray tracing extensions for advanced rendering.
    /// </summary>
    public string[] OptionalDeviceExtensions { get; set; } = [];

    /// <summary>
    /// Whether to prefer discrete GPUs over integrated GPUs during device selection.
    /// Default: true (discrete GPUs typically offer better performance).
    /// Set to false for power efficiency on laptops/mobile.
    /// </summary>
    public bool PreferDiscreteGpu { get; set; } = true;

    /// <summary>
    /// Whether to enable Vulkan validation layers in debug builds.
    /// Default: true (catch errors early during development).
    /// Set to false if validation layers cause performance issues during profiling.
    /// </summary>
#if DEBUG
    public bool EnableValidationLayers { get; set; } = true;
#else
    public bool EnableValidationLayers { get; set; } = false;
#endif

    /// <summary>
    /// Whether to collect frame, command, and live-buffer performance counters.
    /// Default: false, so normal rendering does not collect performance statistics.
    /// </summary>
#if DEBUG
    public bool EnablePerformanceMetrics { get; set; } = true;
#else
    public bool EnablePerformanceMetrics { get; set; } = false;
#endif

    /// <summary>
    /// Whether to retain detailed Vulkan resource snapshots and the first drawn-frame command trace.
    /// Default: false, because diagnostic byte snapshots can consume significant memory.
    /// </summary>
#if DEBUG
    public bool EnableDiagnostics { get; set; } = true;
#else
    public bool EnableDiagnostics { get; set; } = false;
#endif

    /// <summary>
    /// Whether to enable Vulkan validation layers in debug builds.
    /// Default: true (catch errors early during development).
    /// Set to false if validation layers cause performance issues during profiling.
    /// </summary>
    public string[] EnabledValidationLayers { get; set; } = ["*"];

    /// <summary>Whether to enable GPU-assisted validation when the Khronos layer supports it.</summary>
#if DEBUG
    public bool EnableGpuAssistedValidation { get; set; } = true;
#else
    public bool EnableGpuAssistedValidation { get; set; } = false;
#endif

    /// <summary>Whether to enable Best Practices checks when the Khronos layer supports them.</summary>
#if DEBUG
    public bool EnableBestPracticesValidation { get; set; } = true;
#else
    public bool EnableBestPracticesValidation { get; set; } = false;
#endif

    /// <summary>Whether to enable synchronization validation when the Khronos layer supports it.</summary>
#if DEBUG
    public bool EnableSynchronizationValidation { get; set; } = true;
#else
    public bool EnableSynchronizationValidation { get; set; } = false;
#endif

    /// <summary>
    /// Whether to enable validation-layer support for GLSL <c>debugPrintfEXT</c> calls.
    /// Shaders must include diagnostic calls; the feature is off by default due to its overhead.
    /// </summary>
    public bool EnableShaderDebugPrintf { get; set; }
}
