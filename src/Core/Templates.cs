namespace Nexus.Core;

public interface ITemplate
{
    ITemplate[]? Children { get; }
    IBindingDefinition? Binding { get; }
}

public interface ITemplate<T> : ITemplate
    where T : IGameObject, new() { }

public record struct Template<T>(ITemplate[]? Children = null, IBindingDefinition? Binding = null)
    : ITemplate<T>
    where T : IGameObject, new();
