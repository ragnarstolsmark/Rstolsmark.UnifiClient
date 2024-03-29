namespace Rstolsmark.UnifiClient
{
    public class UnifiClientOptions
    {
        public string BaseUrl { get; set; }
        public Credentials Credentials { get; set; }
        public bool AllowInvalidCertificate { get; set; }
        public string DefaultInterface { get; set; }
        public int? TimeoutSeconds { get; set; }
    }
}