namespace Chop.Api.Platform;

public sealed class PlatformReliabilityOptions
{
    public int OutboxPollIntervalMs { get; set; } = 500;

    public int OutboxBatchSize { get; set; } = 100;

    public int OutboxMaxAttempts { get; set; } = 5;

    public int OutboxRetryBaseSeconds { get; set; } = 2;

    public int OutboxRetryMaxSeconds { get; set; } = 60;

    public int OutboxLagUnhealthyThresholdSeconds { get; set; } = 120;

    public int OutboxRetentionDays { get; set; } = 7;

    public int AuditRetentionDays { get; set; } = 90;
}
