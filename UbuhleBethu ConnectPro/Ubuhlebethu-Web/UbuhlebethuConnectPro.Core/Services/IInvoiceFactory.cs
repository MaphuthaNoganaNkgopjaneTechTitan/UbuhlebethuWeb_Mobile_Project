using UbuhlebethuConnectPro.Core.Models;

namespace UbuhlebethuConnectPro.Core.Services
{
    public interface IInvoiceFactory
    {
        Invoice CreateFromQuote(QuoteRequest quote);
    }
}
