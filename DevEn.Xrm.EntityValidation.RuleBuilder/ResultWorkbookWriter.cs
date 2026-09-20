using System;
using System.Collections.Generic;
using System.Linq;
using ClosedXML.Excel;

namespace DevEn.Xrm.EntityValidation.RuleBuilder
{
    /// <summary>
    /// Writes back a copy of the spreadsheet that was read, with the generated rule added on each line.
    /// Handy to review the result where the rules were written, and to hand back a file that already says
    /// which configuration row every line ends up in.
    /// </summary>
    internal static class ResultWorkbookWriter
    {
        private const string ConfigurationNameHeader = "ConfigurationName";
        private const string GeneratedJsonHeader = "GeneratedJson";
        private const string ResultHeader = "Result";

        private static readonly XLColor GeneratedHeaderFill = XLColor.FromHtml("#375623");
        private static readonly XLColor ErrorFill = XLColor.FromHtml("#F8CBAD");

        public static void Write(string inputPath, string outputPath, IReadOnlyList<RuleOutcome> outcomes)
        {
            using (var workbook = new XLWorkbook(inputPath))
            {
                var sheet = workbook.Worksheets.FirstOrDefault(w => string.Equals(w.Name, TemplateWorkbookWriter.RulesSheetName, StringComparison.OrdinalIgnoreCase))
                    ?? workbook.Worksheets.First();

                var nameColumn = EnsureColumn(sheet, ConfigurationNameHeader, 28);
                var jsonColumn = EnsureColumn(sheet, GeneratedJsonHeader, 80);
                var resultColumn = EnsureColumn(sheet, ResultHeader, 60);

                foreach (var outcome in outcomes)
                {
                    sheet.Cell(outcome.RowNumber, nameColumn).SetValue(outcome.ConfigurationName);
                    sheet.Cell(outcome.RowNumber, jsonColumn).SetValue(outcome.Json);
                    sheet.Cell(outcome.RowNumber, resultColumn).SetValue(outcome.Result);

                    if (outcome.Errors.Count > 0)
                    {
                        sheet.Range(outcome.RowNumber, nameColumn, outcome.RowNumber, resultColumn)
                            .Style.Fill.BackgroundColor = ErrorFill;
                    }
                }

                workbook.SaveAs(outputPath);
            }
        }

        private static int EnsureColumn(IXLWorksheet sheet, string header, int width)
        {
            var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;

            for (var columnNumber = 1; columnNumber <= lastColumn; columnNumber++)
            {
                if (string.Equals(sheet.Cell(1, columnNumber).GetString().Trim(), header, StringComparison.OrdinalIgnoreCase))
                {
                    return columnNumber; // Generated a second time on the same file: reuse the column.
                }
            }

            var newColumn = lastColumn + 1;
            var cell = sheet.Cell(1, newColumn);
            cell.Value = header;
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = GeneratedHeaderFill;
            cell.Style.Alignment.WrapText = true;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            sheet.Column(newColumn).Width = width;

            return newColumn;
        }
    }
}
