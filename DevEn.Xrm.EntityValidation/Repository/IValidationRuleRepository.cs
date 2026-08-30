using System.Collections.Generic;
using DevEn.Xrm.EntityValidation.Configuration;
using DevEn.Xrm.EntityValidation.Model;

namespace DevEn.Xrm.EntityValidation.Repository
{
    /// <summary>
    /// Retrieves the active validation rules configured for a given entity/message/stage combination.
    /// </summary>
    internal interface IValidationRuleRepository
    {
        /// <summary>
        /// Returns the active rules, already ordered by ascending execution order.
        /// </summary>
        IReadOnlyList<ValidationRuleDefinition> GetActiveRules(string targetEntityLogicalName, string messageName, PipelineStage stage);
    }
}
