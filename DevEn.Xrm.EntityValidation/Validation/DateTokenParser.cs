using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace DevEn.Xrm.EntityValidation.Validation
{
    /// <summary>
    /// Parses date tokens used in rule parameters: either an absolute ISO-8601 date/time, or one of the
    /// relative tokens "Today", "Now", "Today+Nd"/"Today-Nd" (days), "Today+Nm"/"Today-Nm" (months) and
    /// "Today+Ny"/"Today-Ny" (years). All relative tokens are computed against <see cref="DateTime.UtcNow"/>.
    /// </summary>
    internal static class DateTokenParser
    {
        private static readonly Regex RelativeTokenPattern =
            new Regex(@"^Today([+-]\d+)([dmy])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static bool TryParse(string token, out DateTime value)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                value = default(DateTime);
                return false;
            }

            if (string.Equals(token, "Today", StringComparison.OrdinalIgnoreCase))
            {
                value = DateTime.UtcNow.Date;
                return true;
            }

            if (string.Equals(token, "Now", StringComparison.OrdinalIgnoreCase))
            {
                value = DateTime.UtcNow;
                return true;
            }

            var match = RelativeTokenPattern.Match(token);
            if (match.Success)
            {
                var amount = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
                var today = DateTime.UtcNow.Date;

                switch (match.Groups[2].Value.ToLowerInvariant())
                {
                    case "d":
                        value = today.AddDays(amount);
                        return true;
                    case "m":
                        value = today.AddMonths(amount);
                        return true;
                    default:
                        value = today.AddYears(amount);
                        return true;
                }
            }

            return DateTime.TryParse(
                token,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out value);
        }
    }
}
