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

        /// <summary>
        /// Returns every active rule of the entity, whatever message or stage it was written for, ordered
        /// by ascending execution order. Used by the on-demand path, which validates a record outside any
        /// specific operation.
        /// </summary>
        IReadOnlyList<ValidationRuleDefinition> GetAllActiveRules(string targetEntityLogicalName);
    }
}
