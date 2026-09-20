using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;

namespace DevEn.Xrm.EntityValidation.RuleBuilder
{
    internal enum OutputFormat
    {
        Json,
        Excel,
        Both
    }

    /// <summary>
    /// What the tool reads from app.config, so the operator only has to double-click it. Command line
    /// arguments, when present, win over the configuration.
    ///
    /// A relative path cannot be resolved against the current directory alone: that changes with how the
    /// tool is started (from the repository root, from bin\, from a shortcut). It is therefore looked up in
    /// the current directory first and then from the executable's folder upwards, and the path actually
    /// used is always printed. An absolute path is taken as it is.
    /// </summary>
    internal static class AppSettings
    {
        private const string DefaultInputWorkbook = "ValidationRules.Template.xlsx";
        private const string DefaultOutputFolder = "Validation Rules out";
        private const int MaxParentFolders = 6;

        public static string InputWorkbook => Read("InputWorkbook", DefaultInputWorkbook);

        public static string OutputFolder => Read("OutputFolder", DefaultOutputFolder);

        public static OutputFormat OutputFormat
        {
            get
            {
                var configured = Read("OutputFormat", nameof(RuleBuilder.OutputFormat.Both));
                if (Enum.TryParse<OutputFormat>(configured, true, out var format))
                {
                    return format;
                }

                throw new ConfigurationErrorsException(
                    $"OutputFormat '{configured}' is not valid: use Json, Excel or Both.");
            }
        }

        /// <summary>
        /// Full path of an existing file. When nothing matches, returns the candidate based on the current
        /// directory, so the caller still has something concrete to report.
        /// </summary>
        public static string ResolveExistingFile(string path)
        {
            if (Path.IsPathRooted(path))
            {
                return Path.GetFullPath(path);
            }

            return CandidatesFor(path).FirstOrDefault(File.Exists) ?? Path.GetFullPath(path);
        }

        /// <summary>Every location a relative path was looked up in, for the "not found" message.</summary>
        public static IEnumerable<string> CandidatesFor(string path)
        {
            if (Path.IsPathRooted(path))
            {
                yield return Path.GetFullPath(path);
                yield break;
            }

            var alreadyReturned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var folder in BaseFolders())
            {
                var candidate = Path.GetFullPath(Path.Combine(folder, path));
                if (alreadyReturned.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }

        /// <summary>
        /// A relative output folder is created next to the spreadsheet it comes from: whoever fills the
        /// sheet in finds the generated configuration in the same place.
        /// </summary>
        public static string ResolveOutputFolder(string outputFolder, string inputWorkbookFullPath)
        {
            if (Path.IsPathRooted(outputFolder))
            {
                return Path.GetFullPath(outputFolder);
            }

            var inputFolder = Path.GetDirectoryName(inputWorkbookFullPath);
            return string.IsNullOrEmpty(inputFolder)
                ? Path.GetFullPath(outputFolder)
                : Path.GetFullPath(Path.Combine(inputFolder, outputFolder));
        }

        private static IEnumerable<string> BaseFolders()
        {
            yield return Directory.GetCurrentDirectory();

            var folder = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            for (var depth = 0; depth < MaxParentFolders && folder != null; depth++, folder = folder.Parent)
            {
                yield return folder.FullName;
            }
        }

        private static string Read(string key, string fallback)
        {
            var value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
