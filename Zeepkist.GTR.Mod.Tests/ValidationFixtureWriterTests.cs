using System;
using System.IO;
using Newtonsoft.Json.Linq;
using TNRD.Zeepkist.GTR.Ghosting.Recording;
using Xunit;

public class ValidationFixtureWriterTests
{
    [Fact]
    public void CompletedCaptureContainsSubmissionLevelAndMeasuredScopes()
    {
        string directory = Path.Combine(Path.GetTempPath(), "gtr-validation-" + Guid.NewGuid());
        string run = Guid.NewGuid().ToString("D");
        try
        {
            var measurements = new ValidationCaptureMeasurements { ManagedBytesStart = 100, ManagedBytesFinish = 150 };
            measurements.AddSample(10); measurements.AddSample(30);
            ValidationFixtureWriter.Write(directory, run, "submission", "level", measurements, 2.5, 1000);
            Assert.Equal("submission", File.ReadAllText(Path.Combine(directory, run + ".submission.json")));
            Assert.Equal("level", File.ReadAllText(Path.Combine(directory, run + ".zeeplevel")));
            var marker = JObject.Parse(File.ReadAllText(Path.Combine(directory, run + ".capture.json")));
            Assert.Equal(2, marker["measurements"]["SampleCalls"].Value<int>());
            Assert.Equal(40, marker["measurements"]["SampleTicks"].Value<long>());
            Assert.Equal(30, marker["measurements"]["MaxSampleTicks"].Value<long>());
            Assert.Equal(1000, marker["compressedBytes"].Value<int>());
            Assert.Contains("unrelated game allocations", marker["managedMemoryScope"].Value<string>());
            Assert.Throws<IOException>(() => ValidationFixtureWriter.Write(directory, run, "changed", "changed", measurements, 1, 1));
            Assert.Equal("submission", File.ReadAllText(Path.Combine(directory, run + ".submission.json")));
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void InvalidUuidCannotWritePaths()
    {
        string directory = Path.Combine(Path.GetTempPath(), "gtr-validation-" + Guid.NewGuid());
        Assert.Throws<FormatException>(() => ValidationFixtureWriter.Write(directory, "../escape", "", "", null, 0, 0));
        Assert.False(Directory.Exists(directory));
    }
}
