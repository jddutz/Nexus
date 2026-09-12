namespace Nexus.Graphics.Vulkan.Pipelines;

internal interface IPipelineManager : IDisposable
{
    ulong Create(PipelineDescription description);

    void Delete(ulong id);
}
