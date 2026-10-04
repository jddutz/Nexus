using Nexus.Core;
namespace Tests;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nexus.Game;

/// <summary>
/// Verifies scenes are created through factories registered by identifier.
/// </summary>
public class SceneRegistryTests
{
    /// <summary>
    /// Verifies a registered factory is used and unknown identifiers are not loaded.
    /// </summary>
    [Fact]
    public void Load_usesRegisteredFactoryAndReturnsNullForUnknownScene()
    {
        var registry = SceneRegistryTestHelper.CreateEmptyRegistry();
        const string sceneName = "WelcomeScreen";
        var scene = new Scene(NodeId.New())
        {
            MainCamera = new Nexus.Graphics.Cameras.StaticCamera(),
        };

        registry.Register(sceneName, () => scene);

        Assert.Equal([sceneName], registry.RegisteredSceneNames);
        Assert.Same(scene, registry.Load(sceneName));
        Assert.Null(registry.Load("UnknownScene"));
    }

    /// <summary>
    /// Verifies a scene name cannot silently replace its registered factory.
    /// </summary>
    [Fact]
    public void Register_throwsWhenSceneNameIsAlreadyRegistered()
    {
        var registry = SceneRegistryTestHelper.CreateEmptyRegistry();
        const string sceneName = "DuplicateScene";
        registry.Register(
            sceneName,
            () =>
                new Scene(NodeId.New())
                {
                    MainCamera = new Nexus.Graphics.Cameras.StaticCamera(),
                }
        );

        Assert.Throws<ArgumentException>(() =>
            registry.Register(
                sceneName,
                () =>
                    new Scene(NodeId.New())
                    {
                        MainCamera = new Nexus.Graphics.Cameras.StaticCamera(),
                    }
            )
        );
    }

    /// <summary>
    /// Verifies a registered factory cannot return a null scene.
    /// </summary>
    [Fact]
    public void Load_throwsWhenRegisteredFactoryReturnsNull()
    {
        var registry = SceneRegistryTestHelper.CreateEmptyRegistry();
        const string sceneName = "NullScene";
        registry.Register(sceneName, () => null!);

        Assert.Throws<InvalidOperationException>(() => registry.Load(sceneName));
    }

    /// <summary>Verifies the default registry discovers concrete scenes and applies name overrides.</summary>
    [Fact]
    public void Discover_registersConcreteScenesAndUsesAttributeOnlyToOverrideName()
    {
        using var services =
            SceneRegistryTestHelper.CreateServicesScanningAssemblyContaining<UnattributedDiscoveredScene>();
        var registry = new SceneRegistry(
            services,
            services.GetRequiredService<IOptions<SceneRegistrySettings>>()
        );

        Assert.Contains(nameof(UnattributedDiscoveredScene), registry.RegisteredSceneNames);
        Assert.Contains("RenamedDiscoveredScene", registry.RegisteredSceneNames);
        Assert.DoesNotContain(nameof(OverriddenDiscoveredScene), registry.RegisteredSceneNames);
        Assert.IsType<UnattributedDiscoveredScene>(
            registry.Load(nameof(UnattributedDiscoveredScene))
        );
    }

    /// <summary>Verifies scene-name overrides reject whitespace names.</summary>
    [Fact]
    public void SceneAttribute_throwsWhenNameIsWhitespace()
    {
        Assert.Throws<ArgumentException>(() => new SceneAttribute("  "));
    }

    /// <summary>A concrete scene discovered by its default class name.</summary>
    public sealed class UnattributedDiscoveredScene : Scene
    {
        /// <summary>Creates a scene used to verify default name discovery.</summary>
        public UnattributedDiscoveredScene() { }
    }

    /// <summary>A scene whose discovery name is overridden by its attribute.</summary>
    [Scene("RenamedDiscoveredScene")]
    public sealed class OverriddenDiscoveredScene : Scene
    {
        /// <summary>Creates a scene used to verify attribute name overrides.</summary>
        public OverriddenDiscoveredScene() { }
    }
}
