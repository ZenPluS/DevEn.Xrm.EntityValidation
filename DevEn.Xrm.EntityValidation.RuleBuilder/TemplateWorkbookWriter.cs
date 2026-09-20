using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;

namespace DevEn.Xrm.EntityValidation.RuleBuilder
{
    /// <summary>
    /// Writes the template the operator fills in: the rule sheet with drop-downs and the formatting that
    /// greys out the columns a rule type doesn't use (and flags the ones it needs but that are still
    /// empty), the worked examples, and a reference sheet.
    /// </summary>
    internal static class TemplateWorkbookWriter
    {
        public const string RulesSheetName = "Rules";
        private const string ListsSheetName = "Lists";
        private const string ReferenceSheetName = "Reference";
        private const int MaxRows = 1000;

        private static readonly XLColor HeaderFill = XLColor.FromHtml("#1F3864");
        private static readonly XLColor CoreHeaderFill = XLColor.FromHtml("#2E75B6");
        private static readonly XLColor NotApplicableFill = XLColor.FromHtml("#E7E6E6");
        private static readonly XLColor MissingFill = XLColor.FromHtml("#FFE699");
        private static readonly XLColor SampleFill = XLColor.FromHtml("#F2F7FB");

        public static void Write(string path)
        {
            using (var workbook = new XLWorkbook())
            {
                var rules = workbook.AddWorksheet(RulesSheetName);
                var lists = workbook.AddWorksheet(ListsSheetName);
                var reference = workbook.AddWorksheet(ReferenceSheetName);

                WriteLists(lists);
                WriteRules(rules, lists);
                WriteReference(reference);

                lists.Hide();
                workbook.SaveAs(path);
            }
        }

        private static void WriteLists(IXLWorksheet lists)
        {
            var listNames = RuleSchema.Columns
                .Where(column => column.ListName != null)
                .Select(column => column.ListName)
                .Distinct()
                .ToList();

            for (var index = 0; index < listNames.Count; index++)
            {
                var column = index + 1;
                lists.Cell(1, column).Value = listNames[index];
                lists.Cell(1, column).Style.Font.Bold = true;

                var values = RuleSchema.ListValues(listNames[index]);
                for (var row = 0; row < values.Length; row++)
                {
                    lists.Cell(row + 2, column).Value = values[row];
                }
            }
        }

        private static IXLRange ListRange(IXLWorksheet lists, string listName)
        {
            var header = lists.Row(1).CellsUsed().First(cell => cell.GetString() == listName);
            var values = RuleSchema.ListValues(listName);
            return lists.Range(2, header.Address.ColumnNumber, values.Length + 1, header.Address.ColumnNumber);
        }

        private static void WriteRules(IXLWorksheet rules, IXLWorksheet lists)
        {
            WriteHeader(rules);
            WriteSamples(rules);
            ApplyDropDowns(rules, lists);
            ApplyEnablementFormatting(rules);

            rules.SheetView.FreezeRows(1);
            rules.SheetView.FreezeColumns(1);
            rules.Range(1, 1, 1, RuleSchema.Columns.Count).SetAutoFilter();
        }

        private static void WriteHeader(IXLWorksheet rules)
        {
            for (var index = 0; index < RuleSchema.Columns.Count; index++)
            {
                var column = RuleSchema.Columns[index];
                var cell = rules.Cell(1, index + 1);

                cell.Value = column.Header;
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = IsCoreColumn(column.Key) ? CoreHeaderFill : HeaderFill;
                cell.Style.Alignment.WrapText = true;
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                cell.GetComment().AddText(column.Help);

                rules.Column(index + 1).Width = column.Width;
            }

            rules.Row(1).Height = 30;
        }

        private static void WriteSamples(IXLWorksheet rules)
        {
            var samples = TemplateSamples.Build();

            for (var sampleIndex = 0; sampleIndex < samples.Count; sampleIndex++)
            {
                var rowNumber = sampleIndex + 2;
                var sample = samples[sampleIndex];

                for (var columnIndex = 0; columnIndex < RuleSchema.Columns.Count; columnIndex++)
                {
                    if (!sample.TryGetValue(RuleSchema.Columns[columnIndex].Key, out var value) || string.IsNullOrEmpty(value))
                    {
                        continue;
                    }

                    var cell = rules.Cell(rowNumber, columnIndex + 1);
                    cell.SetValue(value);
                    cell.Style.Alignment.WrapText = false;
                }

                rules.Range(rowNumber, 1, rowNumber, RuleSchema.Columns.Count).Style.Fill.BackgroundColor = SampleFill;
            }
        }

        private static void ApplyDropDowns(IXLWorksheet rules, IXLWorksheet lists)
        {
            for (var index = 0; index < RuleSchema.Columns.Count; index++)
            {
                var column = RuleSchema.Columns[index];
                if (column.ListName == null)
                {
                    continue;
                }

                var validation = rules.Range(2, index + 1, MaxRows, index + 1).CreateDataValidation();
                validation.List(ListRange(lists, column.ListName), true);
                validation.IgnoreBlanks = true;
                validation.ErrorStyle = XLErrorStyle.Warning;
                validation.ErrorTitle = "Unexpected value";
                validation.ErrorMessage = "Pick one of the listed values, or keep yours if you know what you are doing.";
            }
        }

        /// <summary>
        /// The "enable/disable" part of the template: a cell the chosen rule type never reads is greyed out,
        /// and one it does need but that is still empty is highlighted.
        /// </summary>
        private static void ApplyEnablementFormatting(IXLWorksheet rules)
        {
            var ruleTypeLetter = ColumnLetter(RuleSchema.RuleType);
            var lastColumnLetter = ColumnLetter(RuleSchema.Columns[RuleSchema.Columns.Count - 1].Key);
            var rowHasContent = $"COUNTA($A2:${lastColumnLetter}2)>0";

            for (var index = 0; index < RuleSchema.Columns.Count; index++)
            {
                var column = RuleSchema.Columns[index];
                var range = rules.Range(2, index + 1, MaxRows, index + 1);
                var cell = $"{XLHelper.GetColumnLetterFromNumber(index + 1)}2";

                if (RuleSchema.AlwaysRequired.Contains(column.Key))
                {
                    range.AddConditionalFormat()
                        .WhenIsTrue($"=AND({rowHasContent},ISBLANK({cell}))")
                        .Fill.SetBackgroundColor(MissingFill);
                    continue;
                }

                var usedBy = RuleSchema.RuleTypesUsing(column.Key);
                if (usedBy.Count == 0)
                {
                    continue; // Core column, relevant to every rule type.
                }

                var notApplicable = string.Join(",", usedBy.Select(ruleType => $"${ruleTypeLetter}2<>\"{ruleType}\""));
                range.AddConditionalFormat()
                    .WhenIsTrue($"=AND(${ruleTypeLetter}2<>\"\",{notApplicable})")
                    .Fill.SetBackgroundColor(NotApplicableFill);

                var requiredBy = RuleSchema.RuleTypesRequiring(column.Key);
                if (requiredBy.Count == 0)
                {
                    continue;
                }

                var required = string.Join(",", requiredBy.Select(ruleType => $"${ruleTypeLetter}2=\"{ruleType}\""));
                range.AddConditionalFormat()
                    .WhenIsTrue($"=AND(OR({required}),ISBLANK({cell}))")
                    .Fill.SetBackgroundColor(MissingFill);
            }
        }

        private static void WriteReference(IXLWorksheet reference)
        {
            var row = 1;

            row = WriteTitle(reference, row, "How to use this template");
            row = WriteParagraph(reference, row, "1. One line per rule. " + RuleSchema.Entity + ", " + RuleSchema.Message + ", " + RuleSchema.Stage + " and " + RuleSchema.RuleType + " are always required.");
            row = WriteParagraph(reference, row, "2. Pick the RuleType first: the columns it does not use turn grey, the ones it needs turn amber until you fill them in.");
            row = WriteParagraph(reference, row, "3. Columns holding several values (Values, Fields, ScopeFields) are separated by '" + RuleSchema.ListSeparator + "'.");
            row = WriteParagraph(reference, row, "4. The Notes column is yours: it never ends up in the configuration.");
            row = WriteParagraph(reference, row, "5. Run: DevEn.Xrm.EntityValidation.RuleBuilder build <this file>.xlsx");
            row = WriteParagraph(reference, row, "   It produces one configuration row per table: name '" + RuleSchema.ConfigurationNamePrefix + "<table>' and the JSON to paste into msdyn_value.");
            row = WriteParagraph(reference, row, "   Every rule is checked with the very code that runs in Dataverse, so a broken line is reported instead of being generated.");
            row++;

            row = WriteTitle(reference, row, "Rule types");

            reference.Cell(row, 1).Value = "RuleType";
            reference.Cell(row, 2).Value = "What it checks";
            reference.Cell(row, 3).Value = "Required columns";
            reference.Cell(row, 4).Value = "Optional columns";
            reference.Range(row, 1, row, 4).Style.Font.Bold = true;
            reference.Range(row, 1, row, 4).Style.Fill.BackgroundColor = HeaderFill;
            reference.Range(row, 1, row, 4).Style.Font.FontColor = XLColor.White;
            row++;

            foreach (var ruleType in RuleSchema.RuleTypes)
            {
                var usage = RuleSchema.GetUsage(ruleType);
                reference.Cell(row, 1).Value = ruleType;
                reference.Cell(row, 2).Value = usage.Purpose;
                reference.Cell(row, 3).Value = string.Join(", ", usage.Required);
                reference.Cell(row, 4).Value = usage.Optional.Length == 0 ? "-" : string.Join(", ", usage.Optional);
                row++;
            }

            row++;
            row = WriteTitle(reference, row, "Columns");

            reference.Cell(row, 1).Value = "Column";
            reference.Cell(row, 2).Value = "Meaning";
            reference.Range(row, 1, row, 2).Style.Font.Bold = true;
            reference.Range(row, 1, row, 2).Style.Fill.BackgroundColor = HeaderFill;
            reference.Range(row, 1, row, 2).Style.Font.FontColor = XLColor.White;
            row++;

            foreach (var column in RuleSchema.Columns)
            {
                reference.Cell(row, 1).Value = column.Header;
                reference.Cell(row, 2).Value = column.Help;
                row++;
            }

            reference.Column(1).Width = 24;
            reference.Column(2).Width = 95;
            reference.Column(3).Width = 42;
            reference.Column(4).Width = 42;
        }

        private static int WriteTitle(IXLWorksheet sheet, int row, string text)
        {
            var cell = sheet.Cell(row, 1);
            cell.Value = text;
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontSize = 13;
            return row + 1;
        }

        private static int WriteParagraph(IXLWorksheet sheet, int row, string text)
        {
            sheet.Cell(row, 1).Value = text;
            return row + 1;
        }

        private static bool IsCoreColumn(string key)
        {
            return RuleSchema.AlwaysRequired.Contains(key)
                || key == RuleSchema.RuleId
                || key == RuleSchema.Field
                || key == RuleSchema.ErrorMessage
                || key == RuleSchema.IsActive
                || key == RuleSchema.ExecutionOrder
                || key == RuleSchema.Notes;
        }

        private static string ColumnLetter(string columnKey)
        {
            var index = 0;
            for (var i = 0; i < RuleSchema.Columns.Count; i++)
            {
                if (RuleSchema.Columns[i].Key == columnKey)
                {
                    index = i + 1;
                    break;
                }
            }

            return XLHelper.GetColumnLetterFromNumber(index);
        }
    }
}
