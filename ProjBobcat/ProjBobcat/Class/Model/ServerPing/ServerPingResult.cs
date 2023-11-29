namespace ProjBobcat.Class.Model.ServerPing;

public class ServerPingResult
{
    public required PingPayload Response { get; init; }
    public long Latency { get; init; }
}