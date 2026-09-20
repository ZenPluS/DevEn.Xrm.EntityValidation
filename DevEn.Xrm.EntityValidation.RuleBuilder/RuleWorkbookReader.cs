using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ClosedXML.Excel;

namespace DevEn.Xrm.EntityValidation.RuleBuilder
{
    internal sealed class RuleRow
    {
        public RuleRow(int rowNumber, IDictionary<string, string> values)
        {
            RowNumber = rowNumber;
            Values = values;
        }

        public int RowNumber { get; }

        public IDictionary<string, string> Values { get; }

        public string this[string columnKey] =>
            Values.TryGetValue(columnKey, out var value) && value != null ? value.Trim() : string.Empty;

        public bool IsEmpty => Values.Values.All(string.IsNullOrWhiteSpace);
    }

    internal static class RuleWorkbookReader
    {
        public static IReadOnlyList<RuleRow> Read(string path, out IReadOnlyList<string> warnings)
        {
            var collectedWarnings = new List<string>();
            var rows = new List<RuleRow>();

            using (var workbook = new XLWorkbook(path))
            {
                var sheet = workbook.Worksheets.FirstOrDefault(w => string.Equals(w.Name, TemplateWorkbookWriter.RulesSheetName, StringComparison.OrdinalIgnoreCase))
                    ?? workbook.Worksheets.First();

                var columnsByIndex = ReadHeader(sheet, collectedWarnings);
                if (columnsByIndex.Count == 0)
                {
                    throw new InvalidOperationException(
                        $"Sheet '{sheet.Name}' has no recognizable header: expected at least the columns {string.Join(", ", RuleSchema.AlwaysRequired)}.");
                }

                var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
                for (var rowNumber = 2; rowNumber <= lastRow; rowNumber++)
                {
                    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var column in columnsByIndex)
                    {
                        values[column.Value] = CellText(sheet.Cell(rowNumber, column.Key));
                    }

                    var row = new RuleRow(rowNumber, values);
                    if (!row.IsEmpty)
                    {
                        rows.Add(row);
                    }
                }
            }

            warnings = collectedWarnings;
            return rows;
        }

        private static IDictionary<int, string> ReadHeader(IXLWorksheet sheet, List<string> warnings)
        {
            var columnsByIndex = new Dictionary<int, string>();
            var headerRow = sheet.Row(1);
            var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;

            for (var columnNumber = 1; columnNumber <= lastColumn; columnNumber++)
            {
                var header = headerRow.Cell(columnNumber).GetString().Trim();
                if (header.Length == 0)
                {
                    continue;
                }

                var column = RuleSchema.Columns.FirstOrDefault(c => string.Equals(c.Header, header, StringComparison.OrdinalIgnoreCase));
                if (column == null)
                {
                    warnings.Add($"Column '{header}' is not part of the template and is ignored.");
                    continue;
                }

                columnsByIndex[columnNumber] = column.Key;
            }

            return columnsByIndex;
        }

        /// <summary>
        /// Reads the cell the way it was typed, not the way it is displayed: a number formatted as
        /// "1,000,000" must still reach the JSON as 1000000.
        /// </summary>
        private static string CellText(IXLCell cell)
        {
            if (cell.IsEmpty())
            {
                return string.Empty;
            }

            switch (cell.DataType)
            {
                case XLDataType.Number:
                    return cell.GetDouble().ToString("0.##########", CultureInfo.InvariantCulture);
                case XLDataType.DateTime:
                    var date = cell.GetDateTime();
                    return date.TimeOfDay == TimeSpan.Zero
                        ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                        : date.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture);
                case XLDataType.Boolean:
                    return cell.GetBoolean() ? "TRUE" : "FALSE";
                default:
                    return cell.GetString().Trim();
            }
        }
    }
}
