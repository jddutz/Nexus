namespace Nexus.Core;

public interface IBindingDefinition
{
    string SourceProperty { get; }
    string TargetProperty { get; }
}
