using System.Diagnostics.Metrics;

namespace Chop.Api.Platform;

public sealed class OutboxMetrics
{
    private static readonly Meter Meter = new("Chop.Api.Platform.Outbox", "1.0.0");
    private readonly Counter<long> _publishedCounter = Meter.CreateCounter<long>("outbox_published_total");
    private readonly Counter<long> _failedCounter = Meter.CreateCounter<long>("outbox_failed_total");
    private readonly Counter<long> _retryCounter = Meter.CreateCounter<long>("outbox_retry_total");
    private readonly Histogram<double> _publishLagSeconds = Meter.CreateHistogram<double>("outbox_publish_lag_seconds");

    public void RecordPublished(string eventType, TimeSpan lag)
    {
        _publishedCounter.Add(1, new KeyValuePair<string, object?>("event_type", eventType));
        _publishLagSeconds.Record(Math.Max(lag.TotalSeconds, 0), new KeyValuePair<string, object?>("event_type", eventType));
    }

    public void RecordFailed(string eventType, bool willRetry)
    {
        _failedCounter.Add(1, new KeyValuePair<string, object?>("event_type", eventType));
        if (willRetry)
        {
            _retryCounter.Add(1, new KeyValuePair<string, object?>("event_type", eventType));
        }
    }
}
