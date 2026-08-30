using Microsoft.Xrm.Sdk;
using DevEn.Xrm.EntityValidation.Model;

namespace DevEn.Xrm.EntityValidation.Validation
{
    /// <summary>
    /// Implements the logic for a single rule type (e.g. "Required", "Regex", "Range"...). The message
    /// shown to the user on failure is always <see cref="ValidationRuleDefinition.ErrorMessage"/>, never
    /// hardcoded text here: this keeps the message entirely configurable from D365.
    /// A <see cref="Configuration.ValidationConfigurationException"/> should instead be thrown when the
    /// rule itself is configured incorrectly/inconsistently (that's not a business failure).
    /// </summary>
    internal interface IRuleEvaluator
    {
        /// <summary>
        /// The RuleType value handled by this evaluator (the registry compares it case-insensitively).
        /// </summary>
        string RuleType { get; }

        /// <summary>
        /// Returns <c>true</c> if the current value satisfies the rule. Format/content validators (all
        /// except "Required") always return <c>true</c> when the field is absent or null: compose with a
        /// separate "Required" rule to also enforce its presence.
        /// <paramref name="organizationService"/> runs in the calling user's security context (not
        /// elevated) and is only needed by evaluators that query Dataverse (e.g. Uniqueness,
        /// RelatedRecordState); other evaluators simply ignore it.
        /// </summary>
        bool IsValid(Entity effectiveEntity, ValidationRuleDefinition rule, IOrganizationService organizationService);
    }
}
