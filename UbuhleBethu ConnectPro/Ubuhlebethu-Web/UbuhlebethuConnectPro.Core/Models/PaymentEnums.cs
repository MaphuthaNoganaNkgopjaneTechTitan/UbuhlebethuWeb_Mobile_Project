using System.Text.Json.Serialization;

namespace UbuhlebethuConnectPro.Core.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PaymentMethod
    {
        EFT,
        Card,
        Cash,
        Other
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PaymentStatus
    {
        Pending,
        Verified,
        Rejected
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum InvoiceStatus
    {
        Unpaid,
        PartiallyPaid,
        Paid,
        Overdue
    }
}
