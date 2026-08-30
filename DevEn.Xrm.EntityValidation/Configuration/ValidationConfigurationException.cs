using System;
using System.Runtime.Serialization;

namespace DevEn.Xrm.EntityValidation.Configuration
{
    /// <summary>
    /// Thrown when the validation rule configuration (the plugin step's Unsecure Configuration, or rows
    /// in the configuration table) is missing, malformed, or inconsistent. Distinct from a normal
    /// business-rule failure (which produces an end-user message and is not a programming/configuration
    /// error).
    /// </summary>
    [Serializable]
    public sealed class ValidationConfigurationException : Exception
    {
        public ValidationConfigurationException()
        {
        }

        public ValidationConfigurationException(string message)
            : base(message)
        {
        }

        public ValidationConfigurationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        private ValidationConfigurationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
