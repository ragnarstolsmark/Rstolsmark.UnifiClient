using Newtonsoft.Json;

namespace Rstolsmark.UnifiClient
{
    public class PortForward
    {
        [JsonProperty("_id")]
        public string Id { get; set; }

        public string Name { get; set; }
        public bool Enabled { get; set; }
        [JsonProperty("pfwd_interface")]
        public string PortForwardInterface { get; set; }
        [JsonProperty("src")]
        public string Source { get; set; }
        [JsonProperty("dst_port")]
        public string DestinationPort { get; set; }
        [JsonProperty("fwd")]
        public string Forward { get; set; }
        [JsonProperty("fwd_port")]
        public string ForwardPort { get; set; }
        [JsonProperty("proto")]
        public string Protocol { get; set; }
        public bool Log { get; set; }
        [JsonProperty("site_id")]
        public string SiteId { get; set; }
        [JsonProperty("destination_ip")]
        public string DestinationIp { get; set; }
        [JsonProperty("destination_ips")]
        public string[] DestinationIps { get; set; }
        [JsonProperty("src_limiting_type")]
        public string SourceLimitingType { get; set; }
        [JsonProperty("src_limiting_enabled")]
        public bool SourceLimitingEnabled { get; set; }
    }
}