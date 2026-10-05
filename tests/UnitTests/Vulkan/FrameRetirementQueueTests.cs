using Nexus.Graphics.Vulkan.Synchronization;

namespace Nexus.UnitTests.Vulkan;

/// <summary>Verifies retired resources wait for every potentially referencing frame slot.</summary>
public sealed class FrameRetirementQueueTests
{
    /// <summary>Verifies replacement resources survive until all in-flight slots complete.</summary>
    [Fact]
    public void Retire_waits_for_every_submitted_slot_before_releasing()
    {
        var released = new List<string>();
        var retirement = new FrameRetirementQueue<string>(3, released.Add);

        retirement.OnFrameSubmitted(0);
        retirement.OnFrameSubmitted(1);
        retirement.OnFrameSubmitted(2);
        retirement.Retire("old-buffer");

        retirement.OnFrameCompleted(0);
        retirement.OnFrameSubmitted(0);
        retirement.OnFrameCompleted(1);
        Assert.DoesNotContain("old-buffer", released);
        retirement.OnFrameCompleted(2);

        Assert.Equal(["old-buffer"], released);
        retirement.OnFrameCompleted(0);
    }

    /// <summary>Verifies resources never used by a submitted frame release immediately.</summary>
    [Fact]
    public void Retire_releases_immediately_when_no_frames_are_in_flight()
    {
        var released = new List<string>();
        var retirement = new FrameRetirementQueue<string>(2, released.Add);

        retirement.Retire("unused-buffer");

        Assert.Equal(["unused-buffer"], released);
    }

    /// <summary>Verifies already completed slots do not delay later resource retirement.</summary>
    [Fact]
    public void Retire_tracks_only_frames_outstanding_at_replacement()
    {
        var released = new List<string>();
        var retirement = new FrameRetirementQueue<string>(2, released.Add);
        retirement.OnFrameSubmitted(0);
        retirement.OnFrameCompleted(0);
        retirement.OnFrameSubmitted(1);

        retirement.Retire("buffer");
        retirement.OnFrameCompleted(1);

        Assert.Equal(["buffer"], released);
    }
}
