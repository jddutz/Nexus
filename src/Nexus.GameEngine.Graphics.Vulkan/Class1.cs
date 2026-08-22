namespace Nexus.GameEngine.Graphics.Vulkan;

public sealed class VulkanApi : IDisposable
{
    private Vk? _api;

    public Vk Api => _api ??= Vk.GetApi();

    public void Dispose()
    {
        _api?.Dispose();
        _api = null;
    }
}
