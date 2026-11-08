using UbuhlebethuConnectPro.Core.Models;

namespace UbuhlebethuConnectPro.Core.Services.AI
{
    public enum AIRequestType { QuotationEstimate, ChatbotQuery, SearchQuery }

    /// <summary>Product of the Factory Method pattern (Task 1, section 7.1).</summary>
    public abstract class AIRequest
    {
        public string Prompt { get; init; } = string.Empty;
        public string Context { get; init; } = string.Empty;
        public abstract AIRequestType Type { get; }
    }

    public class QuotationEstimateRequest : AIRequest
    {
        public override AIRequestType Type => AIRequestType.QuotationEstimate;
        public QuoteRequest Quote { get; init; } = new();
    }
    public class ChatbotQueryRequest : AIRequest { public override AIRequestType Type => AIRequestType.ChatbotQuery; }
    public class SearchQueryRequest : AIRequest { public override AIRequestType Type => AIRequestType.SearchQuery; }

    /// <summary>Creator: each concrete creator builds the correct request payload.</summary>
    public abstract class AIRequestCreator
    {
        public abstract AIRequest CreateRequest(object input);
    }

    public class QuotationEstimateRequestCreator : AIRequestCreator
    {
        public override AIRequest CreateRequest(object input)
        {
            var q = (QuoteRequest)input;
            return new QuotationEstimateRequest
            {
                Quote = q,
                Context = "You are a South African quantity surveyor assistant. Amounts are in ZAR.",
                Prompt = "Give a draft cost estimate for this construction request. " +
                         "Respond ONLY with JSON: {\"estimate\": <number>, \"confidence\": <0..1>}.\n" +
                         $"Project type: {Trim(q.ProjectType, 100)}\nLocation: {Trim(q.SiteLocation, 200)}\n" +
                         $"Client stated budget: {q.EstimatedBudget}\nScope: {Trim(q.ProjectDescription, 1500)}"
            };
        }
        private static string Trim(string s, int n) => s.Length <= n ? s : s[..n];
    }

    public class ChatbotQueryRequestCreator : AIRequestCreator
    {
        public override AIRequest CreateRequest(object input)
        {
            var (question, context) = ((string, string))input;
            return new ChatbotQueryRequest
            {
                Context = "You are the Ubuhlebethu ConnectPro assistant. Answer only from the data supplied. " +
                          "If the answer is not in the data, say you do not know.",
                Prompt = $"DATA:\n{context}\n\nQUESTION: {(question.Length > 500 ? question[..500] : question)}"
            };
        }
    }

    public class SearchQueryRequestCreator : AIRequestCreator
    {
        public override AIRequest CreateRequest(object input) => new SearchQueryRequest
        {
            Context = "Translate the search into keywords.",
            Prompt = (string)input
        };
    }

    public class AIRequestFactory
    {
        public AIRequest Create(AIRequestType type, object input) => type switch
        {
            AIRequestType.QuotationEstimate => new QuotationEstimateRequestCreator().CreateRequest(input),
            AIRequestType.ChatbotQuery => new ChatbotQueryRequestCreator().CreateRequest(input),
            AIRequestType.SearchQuery => new SearchQueryRequestCreator().CreateRequest(input),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }
}
