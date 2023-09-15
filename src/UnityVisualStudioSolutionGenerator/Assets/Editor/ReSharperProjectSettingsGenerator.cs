#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using UnityVisualStudioSolutionGenerator.Configuration;

namespace UnityVisualStudioSolutionGenerator
{
    /// <summary>
    ///     Helper to generate a ReSharper settings file (.csproj.DotSettings) for each .csproj file. It contains value so that the namespace of code is
    ///     expected to start at the project root and all sup directories should be included in the namespace.
    /// </summary>
    internal static class ReSharperProjectSettingsGenerator
    {
        /// <summary>
        ///     Generate a ReSharper settings file (.csproj.DotSettings) for a .csproj file <paramref name="projectFilePath" />. It contains value so that the
        ///     namespace of code is expected to start at the project root and all sup directories should be included in the namespace.
        /// </summary>
        /// <param name="projectFilePath">The absolute path of the .csproj file to generate a matching ReSharper settings file.</param>
        [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "We don't use the string to compare.")]
        internal static void WriteSettingsIfMissing(string projectFilePath)
        {
            if (!GeneratorSettings.GenerateReSharperProjectSettings)
            {
                return;
            }

            var settingsFilePath = $"{projectFilePath}.DotSettings";
            var projectDirectory = Path.GetDirectoryName(projectFilePath) ??
                                   throw new InvalidOperationException($"Failed to get directory name of path '{projectFilePath}'");
            var projectSupDirectoriesEncoded = Directory.EnumerateDirectories(projectDirectory, "*", SearchOption.AllDirectories)
                .Where(
                    directory =>
                        Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories).Any() && // only if directory has source code
                        !Directory.EnumerateFiles(directory, "*.asmdef", SearchOption.TopDirectoryOnly).Any() && // exclude sub-projects
                        !Directory.EnumerateFiles(directory, "*.asmref", SearchOption.TopDirectoryOnly).Any()) // exclude sub-projects references
                .Select(
                    directory => Path.GetRelativePath(projectDirectory, directory)
                        .ToLowerInvariant()
                        .Replace('/', '\\')
                        .Replace("\\", "_005C", StringComparison.Ordinal)
                        .Replace(".", "_002E", StringComparison.Ordinal))
                .ToList();
            if (File.Exists(settingsFilePath))
            {
                var currentContent = File.ReadAllText(settingsFilePath);
                if (projectSupDirectoriesEncoded.TrueForAll(
                        relativePathEncoded => currentContent.Contains(relativePathEncoded, StringComparison.Ordinal)))
                {
                    return;
                }
            }

            using var writer = new StreamWriter(settingsFilePath, false, Encoding.UTF8);
            writer.WriteLine(
                "<wpf:ResourceDictionary xml:space=\"preserve\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:s=\"clr-namespace:System;assembly=mscorlib\" xmlns:ss=\"urn:shemas-jetbrains-com:settings-storage-xaml\" xmlns:wpf=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\">");
            foreach (var relativePathEncoded in projectSupDirectoriesEncoded)
            {
                writer.WriteLine(
                    $"    <s:Boolean x:Key=\"/Default/CodeInspection/NamespaceProvider/NamespaceFoldersToSkip/={relativePathEncoded}/@EntryIndexedValue\">False</s:Boolean>");
            }

            writer.WriteLine("</wpf:ResourceDictionary>");
            LogHelper.LogVerbose($"Generated ReSharper project settings file {Path.GetFileName(settingsFilePath)}");
        }
    }
}
