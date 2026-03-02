namespace Chop.Api.Platform;

public sealed class RealtimeBusOptions
{
    public bool Enabled { get; set; }

    public string HostName { get; set; } = "127.0.0.1";

    public int Port { get; set; } = 5672;

    public string VirtualHost { get; set; } = "/";

    public string UserName { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public string Exchange { get; set; } = "chop.realtime";

    public string ExchangeType { get; set; } = "topic";

    public string Queue { get; set; } = "chop.realtime.signalr";

    public string RoutingKeyPattern { get; set; } = "realtime.#";

    public bool Durable { get; set; } = true;
}
