using System.Collections.Generic;

namespace DevEn.Xrm.EntityValidation.Engine
{
    /// <summary>
    /// Result of evaluating a set of rules against a record, for the callers that want to report the
    /// outcome instead of blocking an operation with an exception (a "Validate" button on a form, say).
    /// </summary>
    internal sealed class ValidationOutcome
    {
        public ValidationOutcome(IReadOnlyList<string> messages, IReadOnlyList<string> failedRuleIds)
        {
            Messages = messages;
            FailedRuleIds = failedRuleIds;
        }

        public bool IsValid => Messages.Count == 0;

        /// <summary>Distinct error messages of the rules that failed, in evaluation order.</summary>
        public IReadOnlyList<string> Messages { get; }

        /// <summary>Id of every rule that failed, including the ones sharing an error message.</summary>
        public IReadOnlyList<string> FailedRuleIds { get; }
    }
}
