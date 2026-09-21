namespace Nexus.Graphics.Textures;

public enum MagFilterEnum
{
    Nearest,
    Linear,
}

public enum MinFilterEnum
{
    Nearest,
    Linear,
    NearestMipmapNearest,
    LinearMipmapNearest,
    NearestMipmapLinear,
    LinearMipmapLinear,
}

public enum WrapModeEnum
{
    ClampToEdge,
    Repeat,
    MirroredRepeat,
}

public interface ISamplingBehavior
{
    SamplingBehaviorId Id { get; }

    MinFilterEnum MinFilter { get; }

    MagFilterEnum MagFilter { get; }

    WrapModeEnum WrapU { get; }

    WrapModeEnum WrapV { get; }
}

public sealed class SamplingBehavior : ISamplingBehavior
{
    public SamplingBehaviorId Id { get; }

    public MinFilterEnum MinFilter { get; }

    public MagFilterEnum MagFilter { get; }

    public WrapModeEnum WrapU { get; }

    public WrapModeEnum WrapV { get; }

    public SamplingBehavior(
        MinFilterEnum minFilter,
        MagFilterEnum magFilter,
        WrapModeEnum wrapU,
        WrapModeEnum wrapV
    )
    {
        MinFilter = minFilter;
        MagFilter = magFilter;
        WrapU = wrapU;
        WrapV = wrapV;

        Id = new IdentityHashBuilder(nameof(SamplingBehavior))
            .Add((uint)MinFilter)
            .Add((uint)MagFilter)
            .Add((uint)WrapU)
            .Add((uint)WrapV)
            .Compute();
    }
}

public static class SamplingBehaviors
{
    public static readonly ISamplingBehavior PixelPerfect = new SamplingBehavior(
        MinFilterEnum.Nearest,
        MagFilterEnum.Nearest,
        WrapModeEnum.ClampToEdge,
        WrapModeEnum.ClampToEdge
    );
    public static readonly ISamplingBehavior Smooth = new SamplingBehavior(
        MinFilterEnum.Linear,
        MagFilterEnum.Linear,
        WrapModeEnum.ClampToEdge,
        WrapModeEnum.ClampToEdge
    );
    public static readonly ISamplingBehavior SmoothMipmap = new SamplingBehavior(
        MinFilterEnum.LinearMipmapLinear,
        MagFilterEnum.Linear,
        WrapModeEnum.ClampToEdge,
        WrapModeEnum.ClampToEdge
    );
    public static readonly ISamplingBehavior SmoothMipmapRepeat = new SamplingBehavior(
        MinFilterEnum.LinearMipmapLinear,
        MagFilterEnum.Linear,
        WrapModeEnum.Repeat,
        WrapModeEnum.Repeat
    );
}
