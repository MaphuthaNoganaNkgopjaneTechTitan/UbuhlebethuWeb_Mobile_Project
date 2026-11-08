using System;
using UbuhlebethuConnectPro.Core.Models;

namespace UbuhlebethuConnectPro.Core.Services
{
    public class QuotationStateMachine
    {
        private static readonly Dictionary<string, QuotationState> _stringToEnum = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Draft", QuotationState.Draft },
            { "Submitted", QuotationState.Submitted },
            { "UnderReview", QuotationState.UnderReview },
            { "Under Review", QuotationState.UnderReview },
            { "Reviewed", QuotationState.UnderReview }, // Map "Reviewed" to "UnderReview"
            { "InternallyApproved", QuotationState.InternallyApproved },
            { "ManagerApproved", QuotationState.InternallyApproved },
            { "Approved", QuotationState.Approved },
            { "Invoiced", QuotationState.Invoiced },
            { "Paid", QuotationState.Closed }, // Map "Paid" to "Closed"
            { "Rejected", QuotationState.Rejected },
            { "Closed", QuotationState.Closed }
        };

        public bool CanTransition(QuotationState currentState, QuotationState nextState)
        {
            return (currentState, nextState) switch
            {
                (QuotationState.Draft, QuotationState.Submitted) => true,
                (QuotationState.Draft, QuotationState.UnderReview) => true,
                (QuotationState.Draft, QuotationState.InternallyApproved) => true,
                (QuotationState.Submitted, QuotationState.UnderReview) => true,
                (QuotationState.Submitted, QuotationState.InternallyApproved) => true,
                (QuotationState.Draft, QuotationState.Rejected) => true,
                (QuotationState.Submitted, QuotationState.Rejected) => true,
                (QuotationState.UnderReview, QuotationState.InternallyApproved) => true,
                (QuotationState.UnderReview, QuotationState.Rejected) => true,
                (QuotationState.InternallyApproved, QuotationState.Draft) => true, // Client requests changes
                (QuotationState.InternallyApproved, QuotationState.Approved) => true, // Client accepts
                (QuotationState.InternallyApproved, QuotationState.Closed) => true, // Client declines
                (QuotationState.Approved, QuotationState.Invoiced) => true, // Invoice generated
                (QuotationState.Invoiced, QuotationState.Closed) => true, // Paid/completed
                
                // Expiration/Cancel can happen from any non-terminal state
                (QuotationState.Draft, QuotationState.Closed) => true,
                (QuotationState.Submitted, QuotationState.Closed) => true,
                (QuotationState.UnderReview, QuotationState.Closed) => true,

                _ => false
            };
        }

        public bool CanTransition(string currentState, string nextState)
        {
            if (!_stringToEnum.TryGetValue(currentState, out var current) || !_stringToEnum.TryGetValue(nextState, out var next))
                return false;
            return CanTransition(current, next);
        }

        public QuotationState TransitionTo(QuotationState currentState, QuotationState nextState)
        {
            if (!CanTransition(currentState, nextState))
            {
                throw new InvalidOperationException($"Invalid state transition from {currentState} to {nextState}");
            }
            return nextState;
        }

        public string TransitionTo(string currentState, string nextState)
        {
            if (!CanTransition(currentState, nextState))
            {
                throw new InvalidOperationException($"Invalid state transition from {currentState} to {nextState}");
            }
            return nextState;
        }
    }
}
