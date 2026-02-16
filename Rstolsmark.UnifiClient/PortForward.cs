using System.Text.Json.Serialization;

namespace Rstolsmark.UnifiClient
{
    public class PortForward
    {
        [JsonPropertyName("_id")]
        public string Id { get; set; }

        public string Name { get; set; }
        public bool Enabled { get; set; }
        [JsonPropertyName("pfwd_interface")]
        public string PortForwardInterface { get; set; }
        [JsonPropertyName("src")]
        public string Source { get; set; }
        [JsonPropertyName("dst_port")]
        public string DestinationPort { get; set; }
        [JsonPropertyName("fwd")]
        public string Forward { get; set; }
        [JsonPropertyName("fwd_port")]
        public string ForwardPort { get; set; }
        [JsonPropertyName("proto")]
        public string Protocol { get; set; }
        public bool Log { get; set; }
        [JsonPropertyName("site_id")]
        public string SiteId { get; set; }
        [JsonPropertyName("destination_ip")]
        public string DestinationIp { get; set; }
        [JsonPropertyName("destination_ips")]
        public string[] DestinationIps { get; set; }
        [JsonPropertyName("src_limiting_type")]
        public string SourceLimitingType { get; set; }
        [JsonPropertyName("src_limiting_enabled")]
        public bool? SourceLimitingEnabled { get; set; }
    }
}