namespace Nexus.Graphics.Geometry;

public class Mesh
{
    public ResourceId Id { get; }
    public string Name { get; }
    public PrimitiveTopologyEnum Topology { get; }
    public IMeshSource Source { get; }

    public Mesh(string name, PrimitiveTopologyEnum topology, IMeshSource source)
    {
        Name = name;
        Topology = topology;
        Source = source;

        Id = new IdentityHashBuilder(nameof(Mesh))
            .Add(Name)
            .Add((uint)Topology)
            .Add(Source.Id)
            .Compute();
    }
}
