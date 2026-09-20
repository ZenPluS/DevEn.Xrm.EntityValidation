using System;
using System.Collections.Generic;

namespace DevEn.Xrm.EntityValidation.RuleBuilder
{
    /// <summary>
    /// The rows shipped with the template: five worked examples per rule type, so whoever fills the sheet
    /// in always has a working line to copy right above their own.
    /// </summary>
    internal static class TemplateSamples
    {
        public static IReadOnlyList<IDictionary<string, string>> Build()
        {
            var samples = new List<IDictionary<string, string>>();

            // ---------------- Required ----------------
            samples.Add(Sample("account", "account-name-required", "Create", "PreOperation", "name", "Required",
                "The account name is required.",
                "The simplest rule: no parameters."));
            samples.Add(Sample("contact", "contact-email-required", "Create", "PreOperation", "emailaddress1", "Required",
                "The email address is required.",
                "A blank string counts as empty."));
            samples.Add(Sample("opportunity", "opp-estimatedvalue-required", "Update", "PreOperation", "estimatedvalue", "Required",
                "Enter the estimated revenue.",
                "On Update, register the PreImage or the rule only sees the changed attributes."));
            samples.Add(Sample("account", "account-phone-required", "Update", "PreOperation", "telephone1", "Required",
                "The phone number is required.",
                "Example of a rule switched off without deleting it.",
                RuleSchema.IsActive, "FALSE"));
            samples.Add(Sample("incident", "case-customer-required", "Create", "PreValidation", "customerid", "Required",
                "The case customer is required.",
                "PreValidation runs before the transaction: good for cheap checks.",
                RuleSchema.ExecutionOrder, "0"));

            // ---------------- Regex ----------------
            samples.Add(Sample("contact", "contact-email-format", "Create", "PreOperation", "emailaddress1", "Regex",
                "Enter a valid email address.",
                "Patterns use .NET syntax and run with a 2-second timeout.",
                RuleSchema.Pattern, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"));
            samples.Add(Sample("account", "account-number-format", "Create", "PreOperation", "accountnumber", "Regex",
                "The account number must look like ACC-000000.",
                "Anchor with ^ and $ or a partial match is enough to pass.",
                RuleSchema.Pattern, "^ACC-[0-9]{6}$"));
            samples.Add(Sample("contact", "contact-phone-format", "Update", "PreOperation", "telephone1", "Regex",
                "The phone number may only contain digits, spaces and a leading +.",
                "Absent value = rule satisfied: pair it with a Required rule if needed.",
                RuleSchema.Pattern, @"^\+?[0-9 ]{6,20}$"));
            samples.Add(Sample("account", "account-website-https", "Create", "PreOperation", "websiteurl", "Regex",
                "The website must start with https://.",
                "Simple pattern, no escaping needed.",
                RuleSchema.Pattern, "^https://.+"));
            samples.Add(Sample("contact", "contact-fiscalcode-format", "Create", "PreOperation", "new_fiscalcode", "Regex",
                "The tax code format is not valid.",
                "In Excel a leading ^ is fine, the cell is text.",
                RuleSchema.Pattern, "^[A-Z]{6}[0-9]{2}[A-Z][0-9]{2}[A-Z][0-9]{3}[A-Z]$"));

            // ---------------- Range ----------------
            samples.Add(Sample("account", "account-employees-min", "Create", "PreOperation", "numberofemployees", "Range",
                "The number of employees must be at least 1.",
                "Only Min: no upper bound.",
                RuleSchema.Min, "1"));
            samples.Add(Sample("opportunity", "opp-value-range", "Update", "PreOperation", "estimatedvalue", "Range",
                "The estimated revenue must be between 0 and 1,000,000.",
                "Money columns compare on the raw amount, with no currency conversion.",
                RuleSchema.Min, "0", RuleSchema.Max, "1000000"));
            samples.Add(Sample("account", "account-creditlimit-positive", "Update", "PreOperation", "creditlimit", "Range",
                "The credit limit cannot be negative.",
                "Use a dot as the decimal separator.",
                RuleSchema.Min, "0"));
            samples.Add(Sample("quotedetail", "quotedetail-quantity-range", "Create", "PreOperation", "quantity", "Range",
                "The quantity must be between 1 and 999.",
                "Works on Whole Number, Decimal, Float and Currency columns.",
                RuleSchema.Min, "1", RuleSchema.Max, "999"));
            samples.Add(Sample("contact", "contact-score-range", "Update", "PreOperation", "new_score", "Range",
                "The score must be between 0 and 100.",
                "ExecutionOrder only decides the order of the messages.",
                RuleSchema.Min, "0", RuleSchema.Max, "100", RuleSchema.ExecutionOrder, "10"));

            // ---------------- StringLength ----------------
            samples.Add(Sample("account", "account-number-length", "Create", "PreOperation", "accountnumber", "StringLength",
                "The account number must be between 5 and 20 characters.",
                "Both bounds are optional, at least one is expected.",
                RuleSchema.MinLength, "5", RuleSchema.MaxLength, "20"));
            samples.Add(Sample("account", "account-description-max", "Update", "PreOperation", "description", "StringLength",
                "The description cannot exceed 2000 characters.",
                "Useful on Multiple Lines of Text columns.",
                RuleSchema.MaxLength, "2000"));
            samples.Add(Sample("contact", "contact-firstname-min", "Create", "PreOperation", "firstname", "StringLength",
                "The first name must be at least 2 characters.",
                "Applies to text columns only: any other type is a configuration error.",
                RuleSchema.MinLength, "2"));
            samples.Add(Sample("contact", "contact-lastname-length", "Create", "PreOperation", "lastname", "StringLength",
                "The last name must be between 2 and 50 characters.",
                string.Empty,
                RuleSchema.MinLength, "2", RuleSchema.MaxLength, "50"));
            samples.Add(Sample("opportunity", "opp-name-length", "Create", "PreOperation", "name", "StringLength",
                "The opportunity name must be between 3 and 100 characters.",
                string.Empty,
                RuleSchema.MinLength, "3", RuleSchema.MaxLength, "100"));

            // ---------------- AllowedValues ----------------
            samples.Add(Sample("account", "account-region-allowed", "Create", "PreOperation", "new_region", "AllowedValues",
                "The region must be EMEA, AMER or APAC.",
                "Separate the values with ';'.",
                RuleSchema.Values, "EMEA;AMER;APAC"));
            samples.Add(Sample("contact", "contact-channel-allowed", "Create", "PreOperation", "new_channel", "AllowedValues",
                "The channel is not among the allowed ones.",
                "CaseSensitive FALSE (the default) ignores upper/lower case.",
                RuleSchema.Values, "Web;Phone;Email", RuleSchema.CaseSensitive, "FALSE"));
            samples.Add(Sample("account", "account-industry-allowed", "Update", "PreOperation", "industrycode", "AllowedValues",
                "This industry is not handled.",
                "On a choice column, list the numeric values.",
                RuleSchema.Values, "1;2;3"));
            samples.Add(Sample("new_order", "order-currency-allowed", "Create", "PreOperation", "new_currencycode", "AllowedValues",
                "Only EUR, USD and GBP are accepted.",
                "CaseSensitive TRUE requires the exact case.",
                RuleSchema.Values, "EUR;USD;GBP", RuleSchema.CaseSensitive, "TRUE"));
            samples.Add(Sample("contact", "contact-preferredcontact-allowed", "Update", "PreOperation", "preferredcontactmethodcode", "AllowedValues",
                "This contact method is not enabled.",
                string.Empty,
                RuleSchema.Values, "1;2;3"));

            // ---------------- FieldComparison ----------------
            samples.Add(Sample("opportunity", "opp-closedate-after-created", "Update", "PreOperation", "actualclosedate", "FieldComparison",
                "The close date cannot be before the creation date.",
                "Date/date, number/number, boolean/boolean or text/text pairs only.",
                RuleSchema.CompareToAttribute, "createdon", RuleSchema.Operator, "GreaterThanOrEqual"));
            samples.Add(Sample("new_contract", "contract-end-after-start", "Create", "PreOperation", "new_enddate", "FieldComparison",
                "The end date must be after the start date.",
                "Either attribute missing = rule satisfied.",
                RuleSchema.CompareToAttribute, "new_startdate", RuleSchema.Operator, "GreaterThan"));
            samples.Add(Sample("quote", "quote-total-within-budget", "Update", "PreOperation", "totalamount", "FieldComparison",
                "The total exceeds the approved budget.",
                "Both amounts must be in the same currency.",
                RuleSchema.CompareToAttribute, "new_budget", RuleSchema.Operator, "LessThanOrEqual"));
            samples.Add(Sample("account", "account-discount-within-max", "Update", "PreOperation", "new_discount", "FieldComparison",
                "The discount exceeds the allowed maximum.",
                string.Empty,
                RuleSchema.CompareToAttribute, "new_maxdiscount", RuleSchema.Operator, "LessThanOrEqual"));
            samples.Add(Sample("contact", "contact-emails-differ", "Update", "PreOperation", "new_secondaryemail", "FieldComparison",
                "The secondary email must differ from the primary one.",
                "Text comparison, case-sensitive.",
                RuleSchema.CompareToAttribute, "emailaddress1", RuleSchema.Operator, "NotEqual"));

            // ---------------- DateRange ----------------
            samples.Add(Sample("new_contract", "contract-start-window", "Create", "PreOperation", "new_startdate", "DateRange",
                "The start date must fall within the next year.",
                "Relative tokens: Today, Now, Today+30d, Today-1y, Today+6m. Always UTC.",
                RuleSchema.Min, "Today", RuleSchema.Max, "Today+365d"));
            samples.Add(Sample("contact", "contact-birthdate-past", "Create", "PreOperation", "birthdate", "DateRange",
                "The date of birth cannot be in the future.",
                "Only Max: no lower bound.",
                RuleSchema.Max, "Today"));
            samples.Add(Sample("appointment", "appointment-not-in-the-past", "Create", "PreOperation", "scheduledstart", "DateRange",
                "The appointment cannot start in the past.",
                "Now includes the time, Today is midnight UTC.",
                RuleSchema.Min, "Now"));
            samples.Add(Sample("opportunity", "opp-closedate-window", "Update", "PreOperation", "estimatedclosedate", "DateRange",
                "The estimated close date must fall within the next six months.",
                string.Empty,
                RuleSchema.Min, "Today", RuleSchema.Max, "Today+180d"));
            samples.Add(Sample("new_task", "task-duedate-recent", "Create", "PreOperation", "new_duedate", "DateRange",
                "The due date cannot be older than 30 days.",
                "An absolute ISO date (2026-01-01) is accepted too.",
                RuleSchema.Min, "Today-30d"));

            // ---------------- Expression ----------------
            samples.Add(Sample("account", "account-amount-within-margin", "Update", "PreOperation", "new_amount", "Expression",
                "The amount exceeds the credit limit by more than 10%.",
                "Arithmetic between attributes: what FieldComparison cannot express.",
                RuleSchema.Condition, @"{""field"":""new_amount"",""op"":""<="",""compareTo"":{""field"":""creditlimit"",""multiply"":1.1}}"));
            samples.Add(Sample("new_contract", "contract-within-thirty-days", "Create", "PreOperation", "new_enddate", "Expression",
                "The end date must be within 30 days of the start date.",
                "addDays shifts a date; differenceInDays returns the gap as a number.",
                RuleSchema.Condition, @"{""field"":""new_enddate"",""op"":""<="",""compareTo"":{""field"":""new_startdate"",""addDays"":30}}"));
            samples.Add(Sample("account", "account-italian-emea", "Create", "PreOperation", string.Empty, "Expression",
                "Italian accounts must belong to the EMEA region.",
                "all = every condition must hold. Field is not needed here.",
                RuleSchema.Condition, @"{""all"":[{""field"":""new_country"",""op"":""=="",""value"":""IT""},{""field"":""new_region"",""op"":""=="",""value"":""EMEA""}]}"));
            samples.Add(Sample("account", "account-customer-or-partner", "Update", "PreOperation", string.Empty, "Expression",
                "The account type must be Customer or Partner.",
                "any = at least one condition must hold.",
                RuleSchema.Condition, @"{""any"":[{""field"":""customertypecode"",""op"":""=="",""value"":1},{""field"":""customertypecode"",""op"":""=="",""value"":3}]}"));
            samples.Add(Sample("opportunity", "opp-closedate-at-least-a-week", "Create", "PreOperation", "estimatedclosedate", "Expression",
                "The estimated close date must be at least a week out.",
                @"Use ""date"" for a date, ""value"" for text or numbers.",
                RuleSchema.Condition, @"{""field"":""estimatedclosedate"",""op"":"">="",""date"":""Today+7d""}"));

            // ---------------- AtLeastOneOf ----------------
            samples.Add(Sample("contact", "contact-one-contact-method", "Create", "PreOperation", string.Empty, "AtLeastOneOf",
                "Enter at least one contact method (email, phone or mobile).",
                "The attributes go in Fields, separated by ';'. Field stays empty.",
                RuleSchema.Fields, "emailaddress1;telephone1;mobilephone", RuleSchema.MinimumRequired, "1"));
            samples.Add(Sample("account", "account-phone-or-website", "Create", "PreOperation", string.Empty, "AtLeastOneOf",
                "Enter at least a phone number or a website.",
                "MinimumRequired empty means 1.",
                RuleSchema.Fields, "telephone1;websiteurl"));
            samples.Add(Sample("lead", "lead-one-contact-method", "Create", "PreOperation", string.Empty, "AtLeastOneOf",
                "A lead needs at least an email address or a phone number.",
                string.Empty,
                RuleSchema.Fields, "emailaddress1;telephone1"));
            samples.Add(Sample("contact", "contact-full-address", "Update", "PreOperation", string.Empty, "AtLeastOneOf",
                "The address must be complete: street, city and postal code.",
                "MinimumRequired equal to the number of attributes = all of them are required.",
                RuleSchema.Fields, "address1_line1;address1_city;address1_postalcode", RuleSchema.MinimumRequired, "3"));
            samples.Add(Sample("account", "account-tax-identifier", "Create", "PreOperation", string.Empty, "AtLeastOneOf",
                "Enter the VAT number or the tax code.",
                string.Empty,
                RuleSchema.Fields, "new_vatnumber;new_fiscalcode"));

            // ---------------- Conditional ----------------
            samples.Add(Sample("account", "account-taxid-if-company", "Create", "PreOperation", "new_taxid", "Conditional",
                "The tax code is required for this customer type.",
                "when/then: the inner rule applies to the same Field.",
                RuleSchema.WhenField, "customertypecode", RuleSchema.WhenOperator, "Equal", RuleSchema.WhenValue, "3",
                RuleSchema.ThenRuleType, "Required"));
            samples.Add(Sample("contact", "contact-vat-if-italy", "Create", "PreOperation", "new_vat", "Conditional",
                "The Italian VAT number must be IT followed by 11 digits.",
                "ThenParameters holds the inner rule's parameters as JSON.",
                RuleSchema.WhenField, "new_country", RuleSchema.WhenOperator, "Equal", RuleSchema.WhenValue, "IT",
                RuleSchema.ThenRuleType, "Regex", RuleSchema.ThenParameters, @"{""pattern"":""^IT[0-9]{11}$""}"));
            samples.Add(Sample("opportunity", "opp-approval-if-high-value", "Update", "PreOperation", "new_approver", "Conditional",
                "A high-value opportunity needs an approver.",
                "The condition compares as text: a Two Options column reads True/False.",
                RuleSchema.WhenField, "new_highvalue", RuleSchema.WhenOperator, "Equal", RuleSchema.WhenValue, "True",
                RuleSchema.ThenRuleType, "Required"));
            samples.Add(Sample("account", "account-approval-outside-emea", "Create", "PreOperation", "new_parentapproval", "Conditional",
                "Accounts outside EMEA need the parent company's approval.",
                "NotEqual inverts the condition.",
                RuleSchema.WhenField, "new_region", RuleSchema.WhenOperator, "NotEqual", RuleSchema.WhenValue, "EMEA",
                RuleSchema.ThenRuleType, "Required"));
            samples.Add(Sample("new_order", "order-express-delivery-window", "Create", "PreOperation", "new_deliverydate", "Conditional",
                "An express delivery must be scheduled within 7 days.",
                "Any rule type can go in ThenRuleType, including Expression.",
                RuleSchema.WhenField, "new_shippingmode", RuleSchema.WhenOperator, "Equal", RuleSchema.WhenValue, "Express",
                RuleSchema.ThenRuleType, "DateRange", RuleSchema.ThenParameters, @"{""max"":""Today+7d""}"));

            // ---------------- Uniqueness ----------------
            samples.Add(Sample("account", "account-number-unique", "Create", "PreOperation", "accountnumber", "Uniqueness",
                "This account number is already in use.",
                "Live query in the calling user's security context.",
                RuleSchema.ScopeFields, string.Empty));
            samples.Add(Sample("contact", "contact-email-unique", "Create", "PreOperation", "emailaddress1", "Uniqueness",
                "This email address already belongs to another contact.",
                "On Update the record excludes itself.",
                RuleSchema.ScopeFields, string.Empty));
            samples.Add(Sample("account", "account-externalcode-unique-per-parent", "Create", "PreOperation", "new_externalcode", "Uniqueness",
                "This external code is already used under the same parent account.",
                "ScopeFields narrows uniqueness to a subset.",
                RuleSchema.ScopeFields, "parentaccountid"));
            samples.Add(Sample("new_product", "product-sku-unique", "Create", "PreOperation", "new_sku", "Uniqueness",
                "This SKU already exists.",
                "For a hard guarantee use an alternate key as well: this check is not transactional.",
                RuleSchema.ScopeFields, string.Empty));
            samples.Add(Sample("contact", "contact-badge-unique-per-company", "Update", "PreOperation", "new_badgeid", "Uniqueness",
                "This badge number is already assigned within the company.",
                "Several attributes can be listed, separated by ';'.",
                RuleSchema.ScopeFields, "new_companyid"));

            // ---------------- RelatedRecordState ----------------
            samples.Add(Sample("account", "account-parent-active", "Create", "PreOperation", "parentaccountid", "RelatedRecordState",
                "The parent account must be active.",
                "Applies to lookup columns only.",
                RuleSchema.ExpectedState, "Active"));
            samples.Add(Sample("opportunity", "opp-customer-active", "Create", "PreOperation", "customerid", "RelatedRecordState",
                "The opportunity's customer must be active.",
                "A record that cannot be read = rule not satisfied.",
                RuleSchema.ExpectedState, "Active"));
            samples.Add(Sample("contact", "contact-parentaccount-active", "Update", "PreOperation", "parentcustomerid", "RelatedRecordState",
                "The related company must be active.",
                "ExpectedState empty means Active.",
                RuleSchema.ExpectedState, string.Empty));
            samples.Add(Sample("new_order", "order-pricelist-active", "Create", "PreOperation", "new_pricelistid", "RelatedRecordState",
                "The price list must be active.",
                string.Empty,
                RuleSchema.ExpectedState, "Active"));
            samples.Add(Sample("new_ticket", "ticket-contract-archived", "Update", "PreOperation", "new_contractid", "RelatedRecordState",
                "The linked contract must be archived.",
                "Inactive covers the reverse case.",
                RuleSchema.ExpectedState, "Inactive"));

            return samples;
        }

        private static IDictionary<string, string> Sample(
            string entity,
            string ruleId,
            string message,
            string stage,
            string field,
            string ruleType,
            string errorMessage,
            string notes,
            params string[] parameters)
        {
            if (parameters.Length % 2 != 0)
            {
                throw new ArgumentException("Parameters must be passed as key/value pairs.", nameof(parameters));
            }

            var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { RuleSchema.Entity, entity },
                { RuleSchema.RuleId, ruleId },
                { RuleSchema.Message, message },
                { RuleSchema.Stage, stage },
                { RuleSchema.Field, field },
                { RuleSchema.RuleType, ruleType },
                { RuleSchema.ErrorMessage, errorMessage },
                { RuleSchema.Notes, notes }
            };

            for (var index = 0; index < parameters.Length; index += 2)
            {
                row[parameters[index]] = parameters[index + 1];
            }

            return row;
        }
    }
}
