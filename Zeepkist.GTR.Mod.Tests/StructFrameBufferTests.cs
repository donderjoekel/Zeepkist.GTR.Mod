using TNRD.Zeepkist.GTR.Utilities;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class StructFrameBufferTests
{
    [Fact]
    public void CaptureAcrossBlocks_PreservesOrderAndExactFinishFrame()
    {
        var frames = new StructFrameBuffer<float>(512);
        for (int i = 0; i < 1300; i++) frames.Add(i * 0.02f);
        frames.Add(25.999f);
        Assert.Equal(1301, frames.Count);
        Assert.Equal(25.999f, frames[^1]);
        Assert.Equal(Enumerable.Range(0, 1300).Select(i => i * 0.02f).Append(25.999f), frames);
        Assert.Equal(511 * 0.02f, frames[511]);
        Assert.Equal(512 * 0.02f, frames[512]);
        Assert.Equal(1024 * 0.02f, frames[1024]);
    }

    [Fact]
    public void CaptureCopiesValues_AndNewBufferCannotChangeDetachedBuffer()
    {
        var detached = new StructFrameBuffer<(float Time, int State)>(2);
        var frame = (Time: 1f, State: 1);
        detached.Add(frame);
        frame.State = 9;
        var next = new StructFrameBuffer<(float Time, int State)>(2);
        next.Add(frame);
        Assert.Equal(1, detached[0].State);
        Assert.Equal(9, next[0].State);
        Assert.Throws<ArgumentOutOfRangeException>(() => detached[-1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => detached[1]);
    }
}
