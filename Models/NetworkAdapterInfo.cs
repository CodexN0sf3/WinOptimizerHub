namespace WinOptimizerHub.Models
{
    public class NetworkAdapterInfo
    {
        public string Name { get; set; } = string.Empty;
        public string MacAddress { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string SubnetMask { get; set; } = string.Empty;
        public string Gateway { get; set; } = string.Empty;
        public string DnsServers { get; set; } = string.Empty;
    }
}
