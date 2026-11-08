using System.Text.Json.Serialization;

namespace UbuhlebethuConnectPro.Core.Models
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum QuotationState
    {
        Draft,
        UnderReview,
        InternallyApproved,
        Submitted,
        Approved,
        Invoiced,
        Rejected,
        Closed
    }
}
