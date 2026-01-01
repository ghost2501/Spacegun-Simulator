using System;
using System.IO;

namespace Spacegun_Simulator.Core
{
    public static class UserDataPaths
    {
        private const string AppFolderName = "Spacegun Simulator";

        public static string GetAppDataRoot()
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!string.IsNullOrWhiteSpace(localAppData))
                {
                    return Path.Combine(localAppData, AppFolderName);
                }
            }
            catch
            {
                // Fall back below.
            }

            // Fallback: next to executable (may require elevation if installed under Program Files).
            return AppContext.BaseDirectory;
        }

        public static string GetSavesDirectory()
            => Path.Combine(GetAppDataRoot(), "Saves");

        public static string GetSavesPath(params string[] parts)
        {
            string path = GetSavesDirectory();
            if (parts is null || parts.Length == 0)
                return path;

            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part))
                    continue;
                path = Path.Combine(path, part);
            }

            return path;
        }

        public static string GetAutoSavePath()
            => GetSavesPath("AutoSave.json");

        public static void EnsureSavesDirectoryExists()
        {
            Directory.CreateDirectory(GetSavesDirectory());
        }

        /// <summary>
        /// Best-effort migration from legacy install-relative Saves folder.
        /// This prevents breaking existing saves when moving to per-user AppData.
        /// </summary>
        public static void MigrateLegacySavesIfNeeded()
        {
            try
            {
                string legacySaves = Path.Combine(AppContext.BaseDirectory, "Saves");
                if (!Directory.Exists(legacySaves))
                    return;

                string newSaves = GetSavesDirectory();
                Directory.CreateDirectory(newSaves);

                // Migrate autosave if present and the new one doesn't exist.
                string legacyAuto = Path.Combine(legacySaves, "AutoSave.json");
                string newAuto = GetAutoSavePath();
                if (File.Exists(legacyAuto) && !File.Exists(newAuto))
                {
                    File.Copy(legacyAuto, newAuto, overwrite: false);
                }

                // Migrate common subfolders if present.
                foreach (var sub in new[] { "TuningLab", "Music", "Logs" })
                {
                    string legacyDir = Path.Combine(legacySaves, sub);
                    if (!Directory.Exists(legacyDir))
                        continue;

                    string newDir = Path.Combine(newSaves, sub);
                    Directory.CreateDirectory(newDir);

                    foreach (var file in Directory.GetFiles(legacyDir))
                    {
                        string dest = Path.Combine(newDir, Path.GetFileName(file));
                        if (!File.Exists(dest))
                        {
                            try { File.Copy(file, dest, overwrite: false); }
                            catch { }
                        }
                    }
                }
            }
            catch
            {
                // If Program Files is locked down, this may fail; ignore.
            }
        }
    }
}
