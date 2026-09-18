using System;
using System.Globalization;
using Microsoft.Xrm.Sdk;

namespace DevEn.Xrm.EntityValidation.Validation
{
    /// <summary>
    /// Extracts numeric/date/text values consistently from the most common Dataverse attribute types, so
    /// evaluators don't have to repeat the same conversion logic.
    /// </summary>
    internal static class AttributeValueConverter
    {
        public static object GetRawValue(Entity entity, string attributeLogicalName)
        {
            if (entity == null || string.IsNullOrEmpty(attributeLogicalName))
            {
                return null;
            }

            return entity.Contains(attributeLogicalName) ? entity[attributeLogicalName] : null;
        }

        public static bool TryGetNumeric(object value, out decimal result)
        {
            switch (value)
            {
                case Money money:
                    result = money.Value;
                    return true;
                case OptionSetValue optionSet:
                    result = optionSet.Value;
                    return true;
                case decimal dec:
                    result = dec;
                    return true;
                case int i:
                    result = i;
                    return true;
                case long l:
                    result = l;
                    return true;
                case double d:
                    result = (decimal)d;
                    return true;
                default:
                    result = default(decimal);
                    return false;
            }
        }

        public static bool TryGetDateTime(object value, out DateTime result)
        {
            if (value is DateTime dateTime)
            {
                result = dateTime;
                return true;
            }

            result = default(DateTime);
            return false;
        }

        /// <summary>
        /// Permissive conversion to text, used by Regex/AllowedValues where "compare the textual
        /// representation of an arbitrary value" is a legitimate use case (e.g. an integer in an
        /// allowed-values list).
        /// </summary>
        public static bool TryGetComparableText(object value, out string result)
        {
            switch (value)
            {
                case null:
                    result = null;
                    return false;
                case string text:
                    result = text;
                    return true;
                case OptionSetValue optionSet:
                    result = optionSet.Value.ToString(CultureInfo.InvariantCulture);
                    return true;
                case Money money:
                    result = money.Value.ToString(CultureInfo.InvariantCulture);
                    return true;
                default:
                    result = Convert.ToString(value, CultureInfo.InvariantCulture);
                    return !string.IsNullOrEmpty(result);
            }
        }

        /// <summary>
        /// Strict conversion to text, used by FieldComparison: unlike <see cref="TryGetComparableText"/>, it
        /// does NOT convert arbitrary types (numbers, dates...) via ToString(), because doing so would let
        /// two fields of incompatible type (e.g. a date and a number) be silently compared instead of
        /// raising a rule configuration error.
        /// </summary>
        public static bool TryGetComparableTextStrict(object value, out string result)
        {
            switch (value)
            {
                case string text:
                    result = text;
                    return true;
                case OptionSetValue optionSet:
                    result = optionSet.Value.ToString(CultureInfo.InvariantCulture);
                    return true;
                default:
                    result = null;
                    return false;
            }
        }
    }
}
