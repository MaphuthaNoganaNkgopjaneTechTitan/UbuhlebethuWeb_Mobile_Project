namespace UbuhlebethuConnectPro.Core.Options
{
    public class NvidiaOptions
    {
        public const string Section = "Nvidia";
        public string? ApiKey { get; set; }
        public string Model { get; set; } = "nvidia/nemotron-3-super-120b-a12b";
        public int ChatTimeoutSeconds { get; set; } = 25;
    }
}
