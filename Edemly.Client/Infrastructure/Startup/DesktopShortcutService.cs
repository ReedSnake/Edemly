#nullable enable

using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Edemly.Client.Infrastructure.Startup
{
    public sealed class DesktopShortcutService : IDesktopShortcutService
    {
        public bool TryCreateOrReplaceShortcut(string shortcutFileName, string? preferredExecutablePath, string shortcutArgument)
        {
            try
            {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                var shortcutPath = Path.Combine(desktop, shortcutFileName);

                TryDeleteExistingShortcut(shortcutPath);

                var executablePath = ResolveExecutablePath(preferredExecutablePath);
                if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
                {
                    return false;
                }

                var arguments = string.IsNullOrWhiteSpace(shortcutArgument)
                    ? string.Empty
                    : $"\"{shortcutArgument}\"";

                var wshType = Type.GetTypeFromProgID("WScript.Shell");
                if (wshType is null)
                {
                    return false;
                }

                var wsh = Activator.CreateInstance(wshType);
                var shortcut = wshType.InvokeMember(
                    "CreateShortcut",
                    BindingFlags.InvokeMethod,
                    null,
                    wsh,
                    new object[] { shortcutPath });

                if (shortcut is null)
                {
                    return false;
                }

                var shortcutType = shortcut.GetType();
                shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { executablePath });
                shortcutType.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { arguments });
                shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { Path.GetDirectoryName(executablePath) ?? string.Empty });
                shortcutType.InvokeMember("WindowStyle", BindingFlags.SetProperty, null, shortcut, new object[] { 1 });
                shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { "Edemly" });

                TrySetShortcutIcon(shortcutType, shortcut, executablePath);

                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
                return File.Exists(shortcutPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DESKTOP_SHORTCUT] Create failed: {ex}");
                return false;
            }
        }

        private static void TryDeleteExistingShortcut(string shortcutPath)
        {
            try
            {
                if (File.Exists(shortcutPath))
                {
                    File.Delete(shortcutPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DESKTOP_SHORTCUT] Delete failed: {ex}");
            }
        }

        private static void TrySetShortcutIcon(Type shortcutType, object shortcut, string executablePath)
        {
            try
            {
                shortcutType.InvokeMember(
                    "IconLocation",
                    BindingFlags.SetProperty,
                    null,
                    shortcut,
                    new object[] { executablePath + ",0" });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DESKTOP_SHORTCUT] Icon set failed: {ex}");
            }
        }

        private static string ResolveExecutablePath(string? preferredExecutablePath)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(preferredExecutablePath) && File.Exists(preferredExecutablePath))
                {
                    return preferredExecutablePath;
                }

                var entryAssemblyPath = Assembly.GetEntryAssembly()?.Location;
                if (!string.IsNullOrWhiteSpace(entryAssemblyPath) && File.Exists(entryAssemblyPath))
                {
                    return entryAssemblyPath;
                }

                try
                {
                    var processPath = Process.GetCurrentProcess().MainModule?.FileName;
                    if (!string.IsNullOrWhiteSpace(processPath) && File.Exists(processPath))
                    {
                        return processPath;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[DESKTOP_SHORTCUT] Process path lookup failed: {ex}");
                }

                var baseDirectory = AppDomain.CurrentDomain.BaseDirectory ?? Directory.GetCurrentDirectory();
                var candidates = BuildExecutableCandidates();

                var directoryInfo = new DirectoryInfo(baseDirectory);
                for (var depth = 0; directoryInfo is not null && depth < 4; depth++)
                {
                    var existingPath = candidates
                        .Select(candidate => Path.Combine(directoryInfo.FullName, candidate))
                        .FirstOrDefault(File.Exists);

                    if (!string.IsNullOrWhiteSpace(existingPath))
                    {
                        return existingPath;
                    }

                    directoryInfo = directoryInfo.Parent;
                }

                return ResolveFromProgramFiles(candidates);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DESKTOP_SHORTCUT] Resolve executable failed: {ex}");
                return string.Empty;
            }
        }

        private static List<string> BuildExecutableCandidates()
        {
            var candidates = new List<string>
            {
                "Edemly.exe",
                "Edemly.Client.exe"
            };

            var assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
            if (!string.IsNullOrWhiteSpace(assemblyName))
            {
                candidates.Add(assemblyName + ".exe");
            }

            return candidates;
        }

        private static string ResolveFromProgramFiles(IEnumerable<string> candidates)
        {
            try
            {
                var programFilesDirectories = new[]
                    {
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                    }
                    .Where(path => !string.IsNullOrWhiteSpace(path));

                foreach (var programFilesDirectory in programFilesDirectories)
                {
                    var existingPath = candidates
                        .Select(candidate => Path.Combine(programFilesDirectory, "Edemly", candidate))
                        .FirstOrDefault(File.Exists);

                    if (!string.IsNullOrWhiteSpace(existingPath))
                    {
                        return existingPath;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DESKTOP_SHORTCUT] Program Files search failed: {ex}");
            }

            return string.Empty;
        }
    }
}
