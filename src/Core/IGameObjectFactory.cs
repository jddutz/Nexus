namespace Nexus.Core;

public interface IGameObjectFactory
{
    T Create<T>(ITemplate<T> template)
        where T : IGameObject, new();
}
