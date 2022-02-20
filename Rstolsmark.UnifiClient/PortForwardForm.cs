using Newtonsoft.Json;

namespace Rstolsmark.UnifiClient
{
    public class PortForwardForm
    {
        public string Name { get; set; }
        public bool? Enabled { get; set; }
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
        public bool? Log { get; set; }
        [JsonProperty("site_id")]
        public string SiteId { get; set; }
    }
}