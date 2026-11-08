namespace UbuhlebethuConnectPro.Core.Models
{
    public class ValidationFailedException : System.Exception
    {
        public ValidationFailedException(string message) : base(message) { }
    }
}