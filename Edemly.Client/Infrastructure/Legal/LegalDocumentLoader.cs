#nullable enable

using Edemly.Client.Application.Localization;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace Edemly.Client.Infrastructure.Legal
{
    public sealed class LegalDocumentLoader : ILegalDocumentLoader
    {
        public async Task<string> LoadPoliciesAsync()
        {
            var filePath = ResolvePoliciesPath();
            return File.Exists(filePath)
                ? await File.ReadAllTextAsync(filePath)
                : DefaultLanguage.PoliciesContent;
        }

        private static string ResolvePoliciesPath()
        {
            var exeFolder = Path.GetDirectoryName(
                    Assembly.GetEntryAssembly()?.Location
                    ?? AppDomain.CurrentDomain.BaseDirectory)
                ?? string.Empty;

            var fileName = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName is "uk" or "ua"
                ? "terms_privacy_uk.txt"
                : "terms_privacy_en.txt";

            var installedPath = Path.Combine(exeFolder, "Assets", "Legal", fileName);
            if (File.Exists(installedPath))
            {
                return installedPath;
            }

            var projectDirectory = Directory.GetParent(exeFolder)?.Parent?.Parent?.FullName
                ?? exeFolder;

            return Path.Combine(projectDirectory, "Assets", "Legal", fileName);
        }
    }
}
