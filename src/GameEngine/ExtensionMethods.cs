namespace Nexus.GameEngine;

public static class ExtensionMethods
{
    public static T Create<T>(this ISceneNode node, params object[] args)
        where T : ISceneNode
    {
        return node.Factory.CreateSceneNode<T>(args);
    }
}
