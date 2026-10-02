using System.Diagnostics;
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using TNRD.Zeepkist.GTR.Ghosting.Recording;
using UnityEngine;
using Xunit;
using Xunit.Abstractions;

namespace Zeepkist.GTR.Mod.Tests;

[CollectionDefinition("Memory measurements", DisableParallelization = true)]
public class MemoryMeasurementCollection;

// Managed data only. These numbers are not Unity frame times, Mono CPU measurements, or GPU measurements.
[Collection("Memory measurements")]
public class GhostFrameMemoryMeasurements(ITestOutputHelper output)
{
    // Same fields as pre-change V6Ghost.Frame. One class object per frame was retained by List<Frame>.
    private sealed class LegacyFrame : IFrame
    {
        public float Time { get; init; }
        public Vector3 Position { get; init; }
        public Quaternion Rotation { get; init; }
        public float Speed { get; init; }
        public float Steering { get; init; }
        public InputFlags InputFlags { get; init; }
        public SoapboxFlags SoapboxFlags { get; init; }
        public bool RagdollState { get; init; }
        public Vector3? RagdollPosition { get; init; }
        public Quaternion? RagdollRotation { get; init; }
    }

    [Theory]
    [Trait("Category", "Performance")]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(1000)]
    public void FrameStorageAndSampling_MeasureAgainstClassBaseline(int ghosts)
    {
        const int framesPerGhost = 256;
        // Warm constructors/search before allocation measurements.
        _ = new V6Ghost.Frame(0, default, default, 0, 0, 0, 0, false, null, null);
        _ = new LegacyFrame();
        long before = GC.GetAllocatedBytesForCurrentThread();
        var baseline = new List<LegacyFrame>[ghosts];
        for (int ghost = 0; ghost < ghosts; ghost++)
        {
            baseline[ghost] = new List<LegacyFrame>(framesPerGhost);
            for (int frame = 0; frame < framesPerGhost; frame++)
                baseline[ghost].Add(new LegacyFrame { Time = frame * 0.02f, Position = new(frame, 0, 0) });
        }
        long baselineBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        var current = new V6Ghost.Frame[ghosts][];
        for (int ghost = 0; ghost < ghosts; ghost++)
        {
            current[ghost] = new V6Ghost.Frame[framesPerGhost];
            for (int frame = 0; frame < framesPerGhost; frame++)
                current[ghost][frame] = new(frame * 0.02f, new(frame, 0, 0), default, 0, 0, 0, 0, false, null, null);
        }
        long currentBytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(currentBytes < baselineBytes);
        var oldAccess = baseline.Select(frames => new Func<int, float>(index => frames[index].Time)).ToArray();
        var newAccess = current.Select(FrameTimes).ToArray();
        var oldTimes = Measure(oldAccess, framesPerGhost);
        var newTimes = Measure(newAccess, framesPerGhost);
        output.WriteLine($"ghosts={ghosts}; frames/ghost={framesPerGhost}; class bytes={baselineBytes}; struct bytes={currentBytes}");
        output.WriteLine($"class sampling us p50/p95/p99={Percentile(oldTimes, .5):F2}/{Percentile(oldTimes, .95):F2}/{Percentile(oldTimes, .99):F2}");
        output.WriteLine($"struct sampling us p50/p95/p99={Percentile(newTimes, .5):F2}/{Percentile(newTimes, .95):F2}/{Percentile(newTimes, .99):F2}");
        GC.KeepAlive(baseline);
        GC.KeepAlive(current);
    }

    private static Func<int, float> FrameTimes<T>(T[] frames) where T : struct, IFrame => index => frames[index].Time;

    private static double[] Measure(Func<int, float>[] times, int count)
    {
        var durations = new double[100];
        for (int warmup = 0; warmup < 100; warmup++)
            foreach (var getTime in times)
                GhostFrameSearch.TryGetFrameSample(count, 2.01f, getTime, out _);
        for (int iteration = 0; iteration < 100; iteration++)
        {
            long beforeBytes = GC.GetAllocatedBytesForCurrentThread();
            long start = Stopwatch.GetTimestamp();
            foreach (var getTime in times)
                if (!GhostFrameSearch.TryGetFrameSample(count, 2.01f, getTime, out _))
                    throw new InvalidOperationException("Missing benchmark sample");
            durations[iteration] = (Stopwatch.GetTimestamp() - start) * 1_000_000d / Stopwatch.Frequency;
            Assert.Equal(beforeBytes, GC.GetAllocatedBytesForCurrentThread());
        }
        Array.Sort(durations);
        return durations;
    }

    private static double Percentile(double[] sorted, double percentile) => sorted[(int)Math.Ceiling(sorted.Length * percentile) - 1];
}
