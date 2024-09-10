using System;
using Serilog;
using Serilog.Configuration;
using Serilog.Events;
using Serilog.Sinks.OpenObserve;
using Serilog.Sinks.PeriodicBatching;

namespace TNRD.Zeepkist.GTR.Logging;

public static class OpenObserveSinkExtensions
{
    public static LoggerConfiguration OpenObserve(
        this LoggerSinkConfiguration loggerConfiguration,
        string url,
        string organization,
        string login = "",
        string key = "",
        string streamName = "default",
        LogEventLevel restrictedToMinimumLevel = LogEventLevel.Information)
    {
        if (string.IsNullOrEmpty(url))
            throw new ArgumentException("Argument is null or empty: url", nameof(url));
        if (string.IsNullOrEmpty(organization))
            throw new ArgumentException("Argument is null or empty: organization", nameof(organization));
        if (string.IsNullOrEmpty(login))
            throw new ArgumentException("Argument is null or empty: login", nameof(login));
        if (string.IsNullOrEmpty(key))
            throw new ArgumentException("Argument is null or empty: key", nameof(key));
        Sink batchedSink = new Sink(new HttpClient(url, organization, login, key, streamName));
        return loggerConfiguration.Sink(new PeriodicBatchingSink(batchedSink, new PeriodicBatchingSinkOptions()),
            restrictedToMinimumLevel);
    }
}