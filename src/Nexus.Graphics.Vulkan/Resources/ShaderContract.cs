namespace Nexus.Graphics.Vulkan.Resources;

public sealed record ShaderContract : IGraphicsContract
{
    public ContractId Id { get; }

    public ShaderContract()
    {
        Id = new IdentityHashBuilder(nameof(ShaderContract)).Compute();
    }
}
