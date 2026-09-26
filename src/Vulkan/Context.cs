using Microsoft.Extensions.Options;
using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan.Extensions.EXT;

namespace Nexus.Graphics.Vulkan;

/// <summary>
/// Vulkan context implementation that initializes all Vulkan resources in the constructor.
/// </summary>
///
/// Initialization sequence (based on Vulkan tutorial):
/// [x] Create Instance
/// [x] Setup Debug Messenger
/// [x] Create Surface
/// [x] Pick Physical Device
/// [x] Create Logical Device
/// [ ] Create Swap Chain
/// [ ] Create Image Views
/// [ ] Create Render Pass
/// [ ] Create Framebuffers
/// [ ] Create Graphics Pipeline
/// [ ] Create Command Pool
///
/// All initialization happens in the constructor because the new startup sequence ensures
/// the window exists before VulkanContext is resolved from the DI container.
public unsafe class Context
{
    public const string GRAPHICS_ENGINE_NAME = "Nexus Game Engine";

    private const string NOT_VK_SURFACE_WINDOW =
        "The main application window was not set up for Vulkan.";

    private readonly IValidation? _validationLayers;

    public VulkanSettings Settings { get; private set; }
    public IWindow Window { get; private set; }

    public Context(
        IWindow window,
        IOptions<VulkanSettings> options,
        IValidation? validationLayers = null
    )
    {
        _validationLayers = validationLayers;
        Settings = options.Value;
        Window = window;

        // Step 2: Load Vulkan API - provides access to all Vulkan functions
        _vulkanApi = Vk.GetApi();

        // Step 3: Create Vulkan instance - the connection between app and Vulkan library
        ApplicationInfo appInfo = new()
        {
            SType = StructureType.ApplicationInfo,
            PApplicationName = (byte*)
                Marshal.StringToHGlobalAnsi(window.Title ?? GRAPHICS_ENGINE_NAME),
            ApplicationVersion = new Version32(1, 0, 0),
            PEngineName = (byte*)Marshal.StringToHGlobalAnsi(GRAPHICS_ENGINE_NAME),
            EngineVersion = new Version32(1, 0, 0),
            ApiVersion = Vk.Version13,
        };

        InstanceCreateInfo createInfo = new()
        {
            SType = StructureType.InstanceCreateInfo,
            PApplicationInfo = &appInfo,
        };

        if (window.VkSurface == null)
            throw new InvalidOperationException(NOT_VK_SURFACE_WINDOW);

        // Get required extensions from the window system (platform-specific like Win32, X11, etc.)
        var glfwExtensions = window.VkSurface.GetRequiredExtensions(out var glfwExtensionCount);

        var validationLayerNames =
            _validationLayers?.AreEnabled == true ? _validationLayers.LayerNames : [];
        var validationFeatureNames = GetValidationFeatureNames();
        var layerSettingsLayer =
            validationFeatureNames.Length == 0
                ? null
                : FindInstanceExtensionLayer(validationLayerNames, "VK_EXT_layer_settings");
        var validationFeaturesLayer =
            validationFeatureNames.Length == 0 || layerSettingsLayer is not null
                ? null
                : FindInstanceExtensionLayer(validationLayerNames, "VK_EXT_validation_features");
        var useLayerSettings = layerSettingsLayer is not null;

        if (
            validationFeatureNames.Length > 0
            && !useLayerSettings
            && validationFeaturesLayer is null
        )
        {
            Debug.WriteLine(
                "[WARN] Requested Vulkan validation features, but the selected validation layer exposes neither VK_EXT_layer_settings nor VK_EXT_validation_features."
            );
        }

        // Count total extensions needed.
        var totalExtensions = glfwExtensionCount;
        nint debugUtilsNamePtr = 0;
        nint layerSettingsExtensionNamePtr = 0;
        nint validationFeaturesExtensionNamePtr = 0;
        if (validationLayerNames.Length > 0)
        {
            totalExtensions++;
            debugUtilsNamePtr = SilkMarshal.StringToPtr(ExtDebugUtils.ExtensionName);
        }
        if (useLayerSettings)
        {
            totalExtensions++;
            layerSettingsExtensionNamePtr = SilkMarshal.StringToPtr("VK_EXT_layer_settings");
        }
        else if (validationFeaturesLayer is not null)
        {
            totalExtensions++;
            validationFeaturesExtensionNamePtr = SilkMarshal.StringToPtr(
                "VK_EXT_validation_features"
            );
        }

        // Build extension array
        var extensionsArray = stackalloc byte*[(int)totalExtensions];
        for (uint i = 0; i < glfwExtensionCount; i++)
        {
            extensionsArray[i] = glfwExtensions[i];
        }
        if (validationLayerNames.Length > 0)
        {
            extensionsArray[glfwExtensionCount] = (byte*)debugUtilsNamePtr;
        }
        if (useLayerSettings)
            extensionsArray[glfwExtensionCount + 1] = (byte*)layerSettingsExtensionNamePtr;
        else if (validationFeaturesLayer is not null)
            extensionsArray[glfwExtensionCount + 1] = (byte*)validationFeaturesExtensionNamePtr;

        createInfo.EnabledExtensionCount = totalExtensions;
        createInfo.PpEnabledExtensionNames = extensionsArray;

        // Enable validation layers if available
        if (validationLayerNames.Length > 0)
        {
            var layerNames = validationLayerNames;
            var layerNamePtrs = stackalloc nint[layerNames.Length];
            var layerNameBytePtrs = stackalloc byte*[layerNames.Length];

            for (int i = 0; i < layerNames.Length; i++)
            {
                layerNamePtrs[i] = SilkMarshal.StringToPtr(layerNames[i]);
                layerNameBytePtrs[i] = (byte*)layerNamePtrs[i];
            }

            createInfo.EnabledLayerCount = (uint)layerNames.Length;
            createInfo.PpEnabledLayerNames = layerNameBytePtrs;

            var result = CreateInstanceWithValidationSettings(
                ref createInfo,
                validationFeatureNames,
                layerSettingsLayer,
                validationFeaturesLayer,
                out _instance
            );

            // Cleanup layer name pointers
            for (int i = 0; i < layerNames.Length; i++)
            {
                SilkMarshal.Free(layerNamePtrs[i]);
            }

            if (result != Result.Success)
            {
                throw new Exception($"Failed to create Vulkan instance: {result}");
            }
        }
        else
        {
            createInfo.EnabledLayerCount = 0;
            var result = _vulkanApi.CreateInstance(in createInfo, null, out _instance);
            if (result != Result.Success)
            {
                throw new Exception($"Failed to create Vulkan instance: {result}");
            }
        }

        // Cleanup debug utils extension name if added
        if (debugUtilsNamePtr != 0)
        {
            SilkMarshal.Free(debugUtilsNamePtr);
        }
        if (layerSettingsExtensionNamePtr != 0)
            SilkMarshal.Free(layerSettingsExtensionNamePtr);
        if (validationFeaturesExtensionNamePtr != 0)
            SilkMarshal.Free(validationFeaturesExtensionNamePtr);

        Marshal.FreeHGlobal((IntPtr)appInfo.PApplicationName);
        Marshal.FreeHGlobal((IntPtr)appInfo.PEngineName);

        // Initialize validation layers (always call to provide debugger feedback)
        _validationLayers?.Initialize(_vulkanApi, _instance);

        // Step 4: Create surface - platform-agnostic abstraction for rendering to the window
        Surface = CreateSurface(window);

        // Step 5: Select physical device - choose which GPU to use for rendering
        PhysicalDevice = SelectPhysicalDevice();

        // Step 6: Create logical device and queues - the interface for submitting work to the GPU
        var (device, graphicsQueue, presentQueue) = CreateDeviceAndQueues();
        Device = device;
        GraphicsQueue = graphicsQueue;
        PresentQueue = presentQueue;
    }

    /// <summary>Gets the validation feature names requested by the active Vulkan settings.</summary>
    /// <returns>The enabled feature names understood by VK_LAYER_KHRONOS_validation.</returns>
    private string[] GetValidationFeatureNames()
    {
        if (_validationLayers?.AreEnabled != true)
            return [];

        var features = new List<string>();
        if (Settings.EnableGpuAssistedValidation)
        {
            features.Add("VK_VALIDATION_FEATURE_ENABLE_GPU_ASSISTED_EXT");
            features.Add("VK_VALIDATION_FEATURE_ENABLE_GPU_ASSISTED_RESERVE_BINDING_SLOT_EXT");
        }
        if (Settings.EnableBestPracticesValidation)
            features.Add("VK_VALIDATION_FEATURE_ENABLE_BEST_PRACTICES_EXT");
        if (Settings.EnableSynchronizationValidation)
            features.Add("VK_VALIDATION_FEATURE_ENABLE_SYNCHRONIZATION_VALIDATION_EXT");
        if (Settings.EnableShaderDebugPrintf)
            features.Add("VK_VALIDATION_FEATURE_ENABLE_DEBUG_PRINTF_EXT");

        return [.. features];
    }

    /// <summary>Finds a selected validation layer that advertises the requested instance extension.</summary>
    /// <param name="layerNames">The validation layers enabled for instance creation.</param>
    /// <param name="extensionName">The extension name to locate.</param>
    /// <returns>The first layer advertising the extension, or <see langword="null"/>.</returns>
    private string? FindInstanceExtensionLayer(string[] layerNames, string extensionName)
    {
        foreach (var layerName in layerNames)
        {
            if (HasInstanceExtension(layerName, extensionName))
                return layerName;
        }

        return null;
    }

    /// <summary>Checks whether a Vulkan layer advertises an instance extension.</summary>
    /// <param name="layerName">The layer being queried.</param>
    /// <param name="extensionName">The extension name to find.</param>
    /// <returns><see langword="true"/> when the layer advertises the extension.</returns>
    private bool HasInstanceExtension(string layerName, string extensionName)
    {
        var layerNamePointer = SilkMarshal.StringToPtr(layerName);
        try
        {
            uint extensionCount = 0;
            var result = _vulkanApi.EnumerateInstanceExtensionProperties(
                (byte*)layerNamePointer,
                &extensionCount,
                null
            );
            if (result != Result.Success || extensionCount == 0)
                return false;

            var extensions = new ExtensionProperties[extensionCount];
            fixed (ExtensionProperties* extensionPointer = extensions)
            {
                result = _vulkanApi.EnumerateInstanceExtensionProperties(
                    (byte*)layerNamePointer,
                    &extensionCount,
                    extensionPointer
                );
            }

            return result == Result.Success
                && extensions.Any(extension =>
                    Marshal.PtrToStringAnsi((nint)extension.ExtensionName) == extensionName
                );
        }
        finally
        {
            SilkMarshal.Free(layerNamePointer);
        }
    }

    /// <summary>Creates the instance with modern layer settings or the legacy validation-features structure.</summary>
    /// <param name="createInfo">The Vulkan instance creation structure to update.</param>
    /// <param name="featureNames">The validation feature names requested by settings.</param>
    /// <param name="layerSettingsLayer">The layer advertising VK_EXT_layer_settings.</param>
    /// <param name="validationFeaturesLayer">The layer advertising VK_EXT_validation_features.</param>
    /// <param name="instance">The created Vulkan instance.</param>
    /// <returns>The Vulkan result from instance creation.</returns>
    private Result CreateInstanceWithValidationSettings(
        ref InstanceCreateInfo createInfo,
        string[] featureNames,
        string? layerSettingsLayer,
        string? validationFeaturesLayer,
        out Instance instance
    )
    {
        if (featureNames.Length == 0)
            return _vulkanApi.CreateInstance(in createInfo, null, out instance);

        if (layerSettingsLayer is not null)
        {
            var allocatedStrings = new List<nint>();
            try
            {
                var layerNamePointer = SilkMarshal.StringToPtr(layerSettingsLayer);
                allocatedStrings.Add(layerNamePointer);
                var settingNamePointer = SilkMarshal.StringToPtr("enables");
                allocatedStrings.Add(settingNamePointer);
                var featurePointers = stackalloc byte*[featureNames.Length];
                for (var index = 0; index < featureNames.Length; index++)
                {
                    var featurePointer = SilkMarshal.StringToPtr(featureNames[index]);
                    allocatedStrings.Add(featurePointer);
                    featurePointers[index] = (byte*)featurePointer;
                }

                var setting = new LayerSettingEXT
                {
                    PLayerName = (byte*)layerNamePointer,
                    PSettingName = (byte*)settingNamePointer,
                    Type = LayerSettingTypeEXT.StringExt,
                    ValueCount = checked((uint)featureNames.Length),
                    PValues = featurePointers,
                };
                var settingsInfo = new LayerSettingsCreateInfoEXT
                {
                    SType = StructureType.LayerSettingsCreateInfoExt,
                    SettingCount = 1,
                    PSettings = &setting,
                };

                createInfo.PNext = &settingsInfo;
                return _vulkanApi.CreateInstance(in createInfo, null, out instance);
            }
            finally
            {
                foreach (var pointer in allocatedStrings)
                    SilkMarshal.Free(pointer);
            }
        }

        if (validationFeaturesLayer is not null)
        {
            var enabledFeatures = featureNames.Select(ToValidationFeature).ToArray();
            fixed (ValidationFeatureEnableEXT* featurePointer = enabledFeatures)
            {
                var validationFeatures = new ValidationFeaturesEXT
                {
                    SType = StructureType.ValidationFeaturesExt,
                    EnabledValidationFeatureCount = checked((uint)enabledFeatures.Length),
                    PEnabledValidationFeatures = featurePointer,
                };
                createInfo.PNext = &validationFeatures;
                return _vulkanApi.CreateInstance(in createInfo, null, out instance);
            }
        }

        return _vulkanApi.CreateInstance(in createInfo, null, out instance);
    }

    /// <summary>Maps a Khronos validation feature name to its Vulkan enum value.</summary>
    /// <param name="featureName">The validation feature name.</param>
    /// <returns>The corresponding Vulkan validation feature.</returns>
    private static ValidationFeatureEnableEXT ToValidationFeature(string featureName) =>
        featureName switch
        {
            "VK_VALIDATION_FEATURE_ENABLE_GPU_ASSISTED_EXT" =>
                ValidationFeatureEnableEXT.GpuAssistedExt,
            "VK_VALIDATION_FEATURE_ENABLE_GPU_ASSISTED_RESERVE_BINDING_SLOT_EXT" =>
                ValidationFeatureEnableEXT.GpuAssistedReserveBindingSlotExt,
            "VK_VALIDATION_FEATURE_ENABLE_BEST_PRACTICES_EXT" =>
                ValidationFeatureEnableEXT.BestPracticesExt,
            "VK_VALIDATION_FEATURE_ENABLE_SYNCHRONIZATION_VALIDATION_EXT" =>
                ValidationFeatureEnableEXT.SynchronizationValidationExt,
            "VK_VALIDATION_FEATURE_ENABLE_DEBUG_PRINTF_EXT" =>
                ValidationFeatureEnableEXT.DebugPrintfExt,
            _ => throw new ArgumentOutOfRangeException(nameof(featureName), featureName, null),
        };

    /// <summary>
    /// Gets the Vulkan API object that provides access to all Vulkan functions.
    /// This is the main entry point for calling Vulkan API methods.
    /// </summary>
    private Vk _vulkanApi;
    public Vk VulkanApi => _vulkanApi;

    /// <summary>
    /// Gets the Vulkan instance, which is the connection between the application and the Vulkan library.
    /// The instance is used to query physical devices and create surfaces.
    /// </summary>
    private Instance _instance;
    public Instance Instance => _instance;

    /// <summary>
    /// Gets the window surface that Vulkan will render to.
    /// This is an abstraction of the native platform window that allows Vulkan to present rendered images.
    /// Created from the window's VkSurface during initialization.
    /// </summary>
    public SurfaceKHR Surface { get; private init; }

    /// <summary>
    /// Gets the physical device (GPU) selected for rendering.
    /// Represents the actual hardware device that will execute Vulkan commands.
    /// Used to query device capabilities and create the logical device.
    /// </summary>
    public PhysicalDevice PhysicalDevice { get; private init; }

    /// <summary>
    /// Gets the logical device, which is the application's interface to the physical device.
    /// All Vulkan operations are performed through this logical device.
    /// Created with specific queues and features enabled based on application needs.
    /// </summary>
    public Device Device { get; private init; }

    /// <summary>
    /// Gets the graphics queue handle used for submitting rendering commands.
    /// Graphics queues support drawing operations and can execute graphics pipelines.
    /// Commands submitted to this queue are executed by the GPU.
    /// </summary>
    public Queue GraphicsQueue { get; private init; }

    /// <summary>
    /// Gets the present queue handle used for presenting rendered images to the surface.
    /// This queue supports presentation operations to display images on screen.
    /// May be the same queue as GraphicsQueue if the device supports both operations on one queue.
    /// </summary>
    public Queue PresentQueue { get; private init; }

    /// <summary>
    /// Gets whether the Vulkan context has been initialized.
    /// Always returns true since initialization occurs in the constructor.
    /// </summary>
    public bool IsInitialized => true;

    /// <summary>
    /// Creates a Vulkan surface from the window that will be used as the render target.
    /// The surface is a platform-agnostic abstraction over the native window system.
    /// </summary>
    /// <param name="window">The window to create the surface from. Must have VkSurface support.</param>
    /// <returns>The created Vulkan surface handle.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the window doesn't support Vulkan surfaces.</exception>
    private SurfaceKHR CreateSurface(IWindow window)
    {
        // Get the platform-specific Vulkan surface from the window
        var vkSurface = window.VkSurface;
        if (vkSurface is null)
        {
            var errorMessage =
                "Window.VkSurface is null. Window is not configured for Vulkan. "
                + "Ensure the window API is configured for Vulkan before constructing VulkanContext.";

            throw new InvalidOperationException(errorMessage);
        }

        // Create the Vulkan surface handle - connects Vulkan to native windowing system (Win32, X11, Wayland, etc.)
        var surfaceKhr = vkSurface
            .Create<AllocationCallbacks>(Instance.ToHandle(), null)
            .ToSurface();
        return surfaceKhr;
    }

    /// <summary>
    /// Selects a physical device (GPU) to use for rendering.
    /// Enumerates all available Vulkan-capable GPUs and selects one based on suitability.
    /// </summary>
    /// <returns>The selected physical device handle.</returns>
    /// <exception cref="Exception">Thrown if no Vulkan-capable GPUs are found.</exception>
    /// <remarks>
    /// Currently selects the first available device. Future implementation should score
    /// devices based on features, memory, and queue family support.
    /// </remarks>
    private PhysicalDevice SelectPhysicalDevice()
    {
        // First call: Get the count of available physical devices (GPUs)
        uint deviceCount = 0;
        var result = VulkanApi.EnumeratePhysicalDevices(Instance, &deviceCount, null);

        if (deviceCount == 0)
        {
            throw new Exception("Failed to find GPUs with Vulkan support");
        }

        // Second call: Retrieve the actual device handles
        var devices = stackalloc PhysicalDevice[(int)deviceCount];
        result = VulkanApi.EnumeratePhysicalDevices(Instance, &deviceCount, devices);

        for (uint i = 0; i < deviceCount; i++)
        {
            PhysicalDeviceProperties props;
            VulkanApi.GetPhysicalDeviceProperties(devices[i], &props);
            var name = Marshal.PtrToStringAnsi((nint)props.DeviceName);
        }

        // Score all suitable devices
        var suitableDevices = new List<(PhysicalDevice device, int score)>();

        for (uint i = 0; i < deviceCount; i++)
        {
            var device = devices[i];

            // Check if device meets requirements
            if (!IsDeviceSuitable(device, out int score))
            {
                PhysicalDeviceProperties props;
                VulkanApi.GetPhysicalDeviceProperties(device, &props);
                var name = Marshal.PtrToStringAnsi((nint)props.DeviceName);
                continue;
            }

            suitableDevices.Add((device, score));

            PhysicalDeviceProperties properties;
            VulkanApi.GetPhysicalDeviceProperties(device, &properties);
            var deviceName = Marshal.PtrToStringAnsi((nint)properties.DeviceName);
        }

        if (suitableDevices.Count == 0)
        {
            throw new Exception("Failed to find a suitable GPU");
        }

        // Select highest-scored device
        var selected = suitableDevices.OrderByDescending(d => d.score).First();

        PhysicalDeviceProperties selectedProps;
        VulkanApi.GetPhysicalDeviceProperties(selected.device, &selectedProps);
        var selectedName = Marshal.PtrToStringAnsi((nint)selectedProps.DeviceName);

        return selected.device;
    }

    private bool IsDeviceSuitable(PhysicalDevice device, out int score)
    {
        score = 0;

        // 1. Check queue families
        if (!HasRequiredQueueFamilies(device))
        {
            return false;
        }

        // 2. Check required extensions (from GraphicsSettings)
        if (!HasRequiredExtensions(device))
        {
            return false;
        }

        // 3. Check swap chain support
        var swapChainSupport = QuerySwapChainSupportInternal(device);
        if (!swapChainSupport.IsAdequate)
        {
            return false;
        }

        // 4. Score device based on capabilities
        score = ScoreDevice(device);

        return true;
    }

    private bool HasRequiredQueueFamilies(PhysicalDevice device)
    {
        uint queueFamilyCount = 0;
        VulkanApi.GetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount, null);

        var queueFamilies = stackalloc QueueFamilyProperties[(int)queueFamilyCount];
        VulkanApi.GetPhysicalDeviceQueueFamilyProperties(device, &queueFamilyCount, queueFamilies);

        if (!VulkanApi.TryGetInstanceExtension(Instance, out KhrSurface khrSurface))
        {
            return false;
        }

        bool hasGraphics = false;
        bool hasPresent = false;

        for (uint i = 0; i < queueFamilyCount; i++)
        {
            if ((queueFamilies[i].QueueFlags & QueueFlags.GraphicsBit) != 0)
            {
                hasGraphics = true;
            }

            Bool32 presentSupport = false;
            khrSurface.GetPhysicalDeviceSurfaceSupport(device, i, Surface, &presentSupport);
            if (presentSupport)
            {
                hasPresent = true;
            }

            if (hasGraphics && hasPresent)
            {
                return true;
            }
        }

        return false;
    }

    private bool HasRequiredExtensions(PhysicalDevice device)
    {
        uint extensionCount;
        VulkanApi.EnumerateDeviceExtensionProperties(device, (byte*)null, &extensionCount, null);

        var availableExtensions = new ExtensionProperties[extensionCount];
        fixed (ExtensionProperties* pExtensions = availableExtensions)
        {
            VulkanApi.EnumerateDeviceExtensionProperties(
                device,
                (byte*)null,
                &extensionCount,
                pExtensions
            );
        }

        var availableExtensionNames = availableExtensions
            .Select(ext => Marshal.PtrToStringAnsi((nint)ext.ExtensionName))
            .ToHashSet();

        // Check all required extensions from settings
        foreach (var required in GetRequiredDeviceExtensions())
        {
            if (!availableExtensionNames.Contains(required))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Gets device extensions required by settings, including the optional shader printf dependency.</summary>
    /// <returns>The distinct device extensions required for logical-device creation.</returns>
    private string[] GetRequiredDeviceExtensions()
    {
        var extensions = Settings.RequiredDeviceExtensions.ToList();
        if (_validationLayers?.AreEnabled == true && Settings.EnableShaderDebugPrintf)
            extensions.Add("VK_KHR_shader_non_semantic_info");
        return [.. extensions.Distinct(StringComparer.Ordinal)];
    }

    private int ScoreDevice(PhysicalDevice device)
    {
        PhysicalDeviceProperties props;
        VulkanApi.GetPhysicalDeviceProperties(device, &props);

        int score = 0;

        // Prefer discrete GPU if configured
        if (Settings.PreferDiscreteGpu && props.DeviceType == PhysicalDeviceType.DiscreteGpu)
        {
            score += 1000;
        }

        // Higher maximum texture size is better
        score += (int)props.Limits.MaxImageDimension2D;

        // Bonus points for optional extensions
        uint extensionCount;
        VulkanApi.EnumerateDeviceExtensionProperties(device, (byte*)null, &extensionCount, null);
        var availableExtensions = new ExtensionProperties[extensionCount];
        fixed (ExtensionProperties* pExtensions = availableExtensions)
        {
            VulkanApi.EnumerateDeviceExtensionProperties(
                device,
                (byte*)null,
                &extensionCount,
                pExtensions
            );
        }

        var availableExtensionNames = availableExtensions
            .Select(ext => Marshal.PtrToStringAnsi((nint)ext.ExtensionName))
            .ToHashSet();

        foreach (var optional in Settings.OptionalDeviceExtensions)
        {
            if (availableExtensionNames.Contains(optional))
            {
                score += 100;
            }
        }

        return score;
    }

    /// <summary>
    /// Creates the logical device and retrieves queue handles for graphics and presentation.
    /// This involves finding suitable queue families, creating the device with required extensions,
    /// and obtaining handles to the graphics and present queues.
    /// </summary>
    /// <returns>A tuple containing the logical device, graphics queue, and present queue.</returns>
    /// <exception cref="Exception">Thrown if suitable queue families cannot be found or device creation fails.</exception>
    /// <remarks>
    /// Queue families are groups of queues with similar capabilities. We need:
    /// - A graphics queue family for rendering commands
    /// - A present queue family for displaying images to the surface
    /// These may be the same family on some hardware.
    /// </remarks>
    private (Device, Queue, Queue) CreateDeviceAndQueues()
    {
        var requiredDeviceExtensions = GetRequiredDeviceExtensions();
        uint queueFamilyCount = 0;
        VulkanApi.GetPhysicalDeviceQueueFamilyProperties(PhysicalDevice, &queueFamilyCount, null);

        var queueFamilies = stackalloc QueueFamilyProperties[(int)queueFamilyCount];
        VulkanApi.GetPhysicalDeviceQueueFamilyProperties(
            PhysicalDevice,
            &queueFamilyCount,
            queueFamilies
        );

        uint? graphicsFamily = null;
        uint? presentFamily = null;

        if (!VulkanApi.TryGetInstanceExtension(Instance, out KhrSurface khrSurface))
        {
            throw new Exception("KHR_surface extension not available");
        }

        for (uint i = 0; i < queueFamilyCount; i++)
        {
            if ((queueFamilies[i].QueueFlags & QueueFlags.GraphicsBit) != 0)
            {
                graphicsFamily = i;
            }

            Bool32 presentSupport = false;
            khrSurface.GetPhysicalDeviceSurfaceSupport(PhysicalDevice, i, Surface, &presentSupport);
            if (presentSupport)
            {
                presentFamily = i;
            }

            if (graphicsFamily.HasValue && presentFamily.HasValue)
            {
                break;
            }
        }

        if (!graphicsFamily.HasValue || !presentFamily.HasValue)
        {
            throw new Exception("Failed to find suitable queue families");
        }

        var uniqueQueueFamilies =
            graphicsFamily.Value == presentFamily.Value
                ? new[] { graphicsFamily.Value }
                : [graphicsFamily.Value, presentFamily.Value];

        var queueCreateInfos = stackalloc DeviceQueueCreateInfo[uniqueQueueFamilies.Length];
        float queuePriority = 1.0f;

        for (int i = 0; i < uniqueQueueFamilies.Length; i++)
        {
            queueCreateInfos[i] = new DeviceQueueCreateInfo
            {
                SType = StructureType.DeviceQueueCreateInfo,
                QueueFamilyIndex = uniqueQueueFamilies[i],
                QueueCount = 1,
                PQueuePriorities = &queuePriority,
            };
        }

        var deviceFeatures = new PhysicalDeviceFeatures();
        var dynamicRenderingFeatures = new PhysicalDeviceDynamicRenderingFeatures
        {
            SType = StructureType.PhysicalDeviceDynamicRenderingFeatures,
            DynamicRendering = true,
        };

        var extensionNames = stackalloc byte*[requiredDeviceExtensions.Length];
        for (int i = 0; i < requiredDeviceExtensions.Length; i++)
        {
            extensionNames[i] = (byte*)SilkMarshal.StringToPtr(requiredDeviceExtensions[i]);
        }

        var createInfo = new DeviceCreateInfo
        {
            SType = StructureType.DeviceCreateInfo,
            PNext = &dynamicRenderingFeatures,
            QueueCreateInfoCount = (uint)uniqueQueueFamilies.Length,
            PQueueCreateInfos = queueCreateInfos,
            PEnabledFeatures = &deviceFeatures,
            EnabledExtensionCount = (uint)requiredDeviceExtensions.Length,
            PpEnabledExtensionNames = extensionNames,
        };

        // Creating the logical device using selected physical device and queues
        Device device;
        Result result;
        try
        {
            result = VulkanApi.CreateDevice(PhysicalDevice, &createInfo, null, &device);
        }
        finally
        {
            for (int i = 0; i < requiredDeviceExtensions.Length; i++)
            {
                SilkMarshal.Free((nint)extensionNames[i]);
            }
        }

        if (result != Result.Success)
        {
            throw new Exception($"Failed to create logical device: {result}");
        }

        Queue graphicsQueue,
            presentQueue;
        VulkanApi.GetDeviceQueue(device, graphicsFamily.Value, 0, &graphicsQueue);

        VulkanApi.GetDeviceQueue(device, presentFamily.Value, 0, &presentQueue);

        return (device, graphicsQueue, presentQueue);
    }

    /// <summary>
    /// Disposes the Vulkan context and cleans up all Vulkan resources.
    /// Resources are destroyed in reverse order of creation to ensure proper cleanup.
    /// </summary>
    /// <remarks>
    /// Vulkan requires explicit cleanup of all created objects. The cleanup order is critical:
    /// 1. Wait for device operations to complete
    /// 2. Destroy logical device (implicitly destroys queues)
    /// 3. Destroy surface
    /// 4. Destroy instance
    /// 5. Unload Vulkan library
    /// </remarks>
    public void Dispose()
    {
        // Clean up Vulkan resources in reverse order of creation

        // 1. Wait for device to finish any pending operations
        // Critical: ensures no commands are executing when we destroy resources
        if (Device.Handle != 0)
        {
            VulkanApi.DeviceWaitIdle(Device);
        }

        // 2. Destroy logical device (also implicitly destroys all associated queues)
        if (Device.Handle != 0)
        {
            VulkanApi.DestroyDevice(Device, null);
        }

        // 3. Destroy surface (must be before destroying the instance)
        if (
            Surface.Handle != 0
            && VulkanApi.TryGetInstanceExtension(Instance, out KhrSurface khrSurface)
        )
        {
            khrSurface.DestroySurface(Instance, Surface, null);
        }

        // 4. Destroy instance (created first, destroyed last)
        if (Instance.Handle != 0)
        {
            VulkanApi.DestroyInstance(Instance, null);
        }

        // 5. Unload Vulkan library and free function pointers
        VulkanApi.Dispose();
    }

    /// <summary>
    /// Public API for Swapchain to query swap chain capabilities.
    /// </summary>
    public SwapChainSupportDetails QuerySwapChainSupport()
    {
        return QuerySwapChainSupportInternal(PhysicalDevice);
    }

    /// <summary>
    /// Finds a queue family index that supports the specified queue flags.
    /// </summary>
    public uint? FindQueueFamily(QueueFlags flags)
    {
        uint queueFamilyCount = 0;
        VulkanApi.GetPhysicalDeviceQueueFamilyProperties(PhysicalDevice, &queueFamilyCount, null);

        var queueFamilies = stackalloc QueueFamilyProperties[(int)queueFamilyCount];
        VulkanApi.GetPhysicalDeviceQueueFamilyProperties(
            PhysicalDevice,
            &queueFamilyCount,
            queueFamilies
        );

        for (uint i = 0; i < queueFamilyCount; i++)
        {
            if ((queueFamilies[i].QueueFlags & flags) == flags)
            {
                return i;
            }
        }

        return null;
    }

    /// <summary>
    /// Internal method used during device selection to query swap chain support.
    /// </summary>
    private SwapChainSupportDetails QuerySwapChainSupportInternal(PhysicalDevice device)
    {
        if (!VulkanApi.TryGetInstanceExtension(Instance, out KhrSurface khrSurface))
        {
            throw new Exception("KHR_surface extension not available");
        }

        // Query capabilities
        SurfaceCapabilitiesKHR capabilities;
        khrSurface.GetPhysicalDeviceSurfaceCapabilities(device, Surface, &capabilities);

        // Query formats
        uint formatCount;
        khrSurface.GetPhysicalDeviceSurfaceFormats(device, Surface, &formatCount, null);
        var formats = new SurfaceFormatKHR[formatCount];
        if (formatCount > 0)
        {
            fixed (SurfaceFormatKHR* pFormats = formats)
            {
                khrSurface.GetPhysicalDeviceSurfaceFormats(device, Surface, &formatCount, pFormats);
            }
        }

        // Query present modes
        uint presentModeCount;
        khrSurface.GetPhysicalDeviceSurfacePresentModes(device, Surface, &presentModeCount, null);
        var presentModes = new PresentModeKHR[presentModeCount];
        if (presentModeCount > 0)
        {
            fixed (PresentModeKHR* pPresentModes = presentModes)
            {
                khrSurface.GetPhysicalDeviceSurfacePresentModes(
                    device,
                    Surface,
                    &presentModeCount,
                    pPresentModes
                );
            }
        }

        return new SwapChainSupportDetails
        {
            Capabilities = capabilities,
            Formats = formats,
            PresentModes = presentModes,
        };
    }
}
