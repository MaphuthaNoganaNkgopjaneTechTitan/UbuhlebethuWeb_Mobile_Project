namespace UbuhlebethuConnectPro.Core.Models.DTOs
{
    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public System.Collections.Generic.IList<string> Roles { get; set; } = new System.Collections.Generic.List<string>();
        public bool RequiresPasswordReset { get; set; }
    }
}
