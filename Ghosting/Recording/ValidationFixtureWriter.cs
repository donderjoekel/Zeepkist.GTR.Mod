using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording;

public sealed class ValidationCaptureMeasurements
{
    public long SampleCalls { get; private set; }
    public long SampleTicks { get; private set; }
    public long MaxSampleTicks { get; private set; }
    public long StopwatchFrequency => Stopwatch.Frequency;
    public long ManagedBytesStart { get; set; }
    public long ManagedBytesFinish { get; set; }
    public void AddSample(long ticks)
    {
        SampleCalls++;
        SampleTicks += ticks;
        MaxSampleTicks = Math.Max(MaxSampleTicks, ticks);
    }
}

public static class ValidationFixtureWriter
{
    public static void Write(string directory, string runUuid, string submission, string level,
        ValidationCaptureMeasurements measurements, double encodeMilliseconds, long compressedBytes)
    {
        // UUID filenames prevent paths supplied by level names, authors or other metadata.
        string stem = Guid.Parse(runUuid).ToString("D");
        Directory.CreateDirectory(directory);
        WriteNew(Path.Combine(directory, stem + ".submission.json"), submission);
        WriteNew(Path.Combine(directory, stem + ".zeeplevel"), level);
        // Completion marker is written last. Partial captures are never evaluated as complete runs.
        WriteNew(Path.Combine(directory, stem + ".capture.json"), JsonConvert.SerializeObject(new
        {
            runUuid = stem, measurements, encodeMilliseconds, compressedBytes,
            managedMemoryScope = "process managed heap; includes unrelated game allocations",
            samplingScope = "sphere extraction and measurement overhead; excludes visual frame capture"
        }));
    }

    private static void WriteNew(string path, string content)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var writer = new StreamWriter(stream, new UTF8Encoding(false));
        writer.Write(content);
    }
}
