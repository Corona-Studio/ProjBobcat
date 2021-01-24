namespace ProjBobcat.Class.Model.ServerPing
{
    public class ServerPingResult
    {
        public PingPayload Response { get; set; }
        public long Latency { get; set; }
    }
}