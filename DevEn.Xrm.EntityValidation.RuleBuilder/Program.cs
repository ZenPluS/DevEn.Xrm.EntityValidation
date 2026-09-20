using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DevEn.Xrm.EntityValidation.RuleBuilder
{
    /// <summary>
    /// Run it with no arguments and it works off app.config: reads the configured spreadsheet and writes
    /// the configuration rows to the configured folder. Arguments are there for the occasional one-off run.
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                var hasCommand = args.Length > 0 && !args[0].StartsWith("-", StringComparison.Ordinal);
                var command = hasCommand ? args[0].ToLowerInvariant() : "build";
                var values = args.Skip(hasCommand ? 1 : 0).Where(argument => !argument.StartsWith("-", StringComparison.Ordinal)).ToArray();
                var force = args.Any(argument => string.Equals(argument, "--force", StringComparison.OrdinalIgnoreCase));

                switch (command)
                {
                    case "template":
                        return WriteTemplate(values.FirstOrDefault() ?? AppSettings.InputWorkbook, force);
                    case "build":
                        return BuildConfiguration(
                            values.FirstOrDefault() ?? AppSettings.InputWorkbook,
                            values.Skip(1).FirstOrDefault() ?? AppSettings.OutputFolder);
                    default:
                        PrintUsage();
                        return 1;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 2;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Turns a spreadsheet of validation rules into the Dataverse configuration rows.");
            Console.WriteLine();
            Console.WriteLine("  (no argument)                     build, using the paths in app.config");
            Console.WriteLine("  build [file.xlsx] [output folder] same thing, overriding the configured paths");
            Console.WriteLine("  template [file.xlsx] [--force]    writes the template, examples included");
            Console.WriteLine();
            Console.WriteLine("app.config:");
            Console.WriteLine($"  InputWorkbook = {AppSettings.InputWorkbook}");
            Console.WriteLine($"  OutputFolder  = {AppSettings.OutputFolder}");
            Console.WriteLine($"  OutputFormat  = {AppSettings.OutputFormat}");
        }

        private static int WriteTemplate(string path, bool force)
        {
            var fullPath = AppSettings.ResolveExistingFile(path);

            if (File.Exists(fullPath) && !force)
            {
                Console.Error.WriteLine($"{fullPath} already exists: add --force to overwrite it.");
                return 2;
            }

            var folder = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(folder))
            {
                Directory.CreateDirectory(folder);
            }

            TemplateWorkbookWriter.Write(fullPath);
            Console.WriteLine($"Template written: {fullPath}");
            Console.WriteLine("Fill in the 'Rules' sheet - the 'Reference' sheet explains every column.");
            return 0;
        }

        private static int BuildConfiguration(string workbookPath, string outputFolder)
        {
            var workbookFullPath = AppSettings.ResolveExistingFile(workbookPath);
            if (!File.Exists(workbookFullPath))
            {
                Console.Error.WriteLine($"Spreadsheet '{workbookPath}' not found. Looked in:");
                foreach (var candidate in AppSettings.CandidatesFor(workbookPath))
                {
                    Console.Error.WriteLine($"  - {candidate}");
                }

                Console.Error.WriteLine("Set InputWorkbook in app.config (an absolute path always works), or pass the path as an argument.");
                return 2;
            }

            var outputFullPath = AppSettings.ResolveOutputFolder(outputFolder, workbookFullPath);
            var format = AppSettings.OutputFormat;
            Console.WriteLine($"Spreadsheet : {workbookFullPath}");
            Console.WriteLine($"Output      : {outputFullPath} ({format})");

            var rows = RuleWorkbookReader.Read(workbookFullPath, out var readWarnings);
            var result = ConfigurationBuilder.Build(rows);
            Console.WriteLine($"Rules read  : {rows.Count}");
            Console.WriteLine();

            foreach (var warning in readWarnings.Concat(result.Warnings).Concat(result.RowWarnings))
            {
                Console.WriteLine($"  warning  {warning}");
            }

            Directory.CreateDirectory(outputFullPath);

            if (format != OutputFormat.Json)
            {
                // Written even when a line is broken: the sheet is where the mistake gets fixed.
                var reviewPath = Path.Combine(outputFullPath, Path.GetFileNameWithoutExtension(workbookFullPath) + " - generated.xlsx");
                ResultWorkbookWriter.Write(workbookFullPath, reviewPath, result.Rules);
                Console.WriteLine($"  spreadsheet with the generated rules: {Path.GetFileName(reviewPath)}");
            }

            if (result.HasErrors)
            {
                Console.Error.WriteLine();
                foreach (var error in result.Errors)
                {
                    Console.Error.WriteLine($"  ERROR    {error}");
                }

                Console.Error.WriteLine();
                Console.Error.WriteLine($"{result.Errors.Count()} error(s): no configuration row was generated.");
                return 2;
            }

            if (format != OutputFormat.Excel)
            {
                WriteJsonFiles(result.Configurations, outputFullPath);
            }

            Console.WriteLine();
            Console.WriteLine("Configuration rows to create on the msdyn_configuration table:");
            foreach (var configuration in result.Configurations)
            {
                var value = format == OutputFormat.Excel
                    ? $"{configuration.RuleCount} rule(s), see the GeneratedJson column"
                    : $"{configuration.FileName} ({configuration.RuleCount} rule(s))";

                Console.WriteLine($"  msdyn_name : {configuration.ConfigurationName}");
                Console.WriteLine($"  msdyn_value: {value}");
                Console.WriteLine("  statecode  : Active");
                Console.WriteLine();
            }

            Console.WriteLine($"Done: {outputFullPath}");
            return 0;
        }

        private static void WriteJsonFiles(IReadOnlyList<EntityConfiguration> configurations, string outputFolder)
        {
            var utf8WithoutBom = new UTF8Encoding(false);

            foreach (var configuration in configurations)
            {
                File.WriteAllText(Path.Combine(outputFolder, configuration.FileName), configuration.Json, utf8WithoutBom);
            }

            // One ready-to-import file, so the rows can be created without opening each JSON.
            var csv = new StringBuilder();
            csv.AppendLine("msdyn_name,msdyn_value");
            foreach (var configuration in configurations)
            {
                csv.AppendLine($"\"{configuration.ConfigurationName}\",\"{configuration.Json.Replace("\"", "\"\"")}\"");
            }

            File.WriteAllText(Path.Combine(outputFolder, "configuration-rows.csv"), csv.ToString(), utf8WithoutBom);
        }
    }
}
