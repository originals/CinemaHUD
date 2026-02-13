using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Blish_HUD;
using Blish_HUD.Modules.Managers;

namespace CinemaModule.Services
{
    /// <summary>
    /// This 'hack' exists because LibVLC requires DLLs to be present on actual filesystem.
    /// Core.Initialize(libvlcBinPath) expects a real directory path containing
    /// libvlc.dll, libvlccore.dll, and the plugins folder, it cannot load from memory/zip...
    /// TODO , get direct path to zip and extract?
    /// </summary>
    internal class LibVlcService
    {
        private static readonly Logger Logger = Logger.GetLogger<LibVlcService>();

        private const string LibVlcVersion = "1"; // to be incremented when the files are updated
        private const string VersionFileName = ".libvlc_version";

        private readonly ContentsManager _contentsManager;

        public LibVlcService(ContentsManager contentsManager)
        {
            _contentsManager = contentsManager;
        }

        public async Task ExtractAsync(string targetDir)
        {
            if (IsUpdateRequired(targetDir))
            {
                Logger.Info($"LibVLC update required. Clearing existing files...");
                ClearDirectory(targetDir);
            }

            var files = LoadFileList();
            bool anyExtracted = false;

            foreach (string file in files)
            {
                string targetPath = Path.Combine(targetDir, file.Replace("libvlc/", ""));

                if (File.Exists(targetPath))
                {
                    continue;
                }

                await ExtractFileAsync(file, targetPath);
                anyExtracted = true;
            }

            if (anyExtracted)
            {
                WriteVersionFile(targetDir);
            }
        }

        private bool IsUpdateRequired(string targetDir)
        {
            string versionFilePath = Path.Combine(targetDir, VersionFileName);

            if (!File.Exists(versionFilePath))
                return Directory.Exists(targetDir) && Directory.GetFiles(targetDir, "*", SearchOption.AllDirectories).Length > 0;

            try
            {
                string installedVersion = File.ReadAllText(versionFilePath).Trim();
                return installedVersion != LibVlcVersion;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to read LibVLC version file");
                return true;
            }
        }

        private void WriteVersionFile(string targetDir)
        {
            try
            {
                string versionFilePath = Path.Combine(targetDir, VersionFileName);
                File.WriteAllText(versionFilePath, LibVlcVersion);
                Logger.Info($"LibVLC version {LibVlcVersion} marker written");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to write LibVLC version file");
            }
        }

        private void ClearDirectory(string targetDir)
        {
            try
            {
                if (!Directory.Exists(targetDir))
                    return;

                foreach (string file in Directory.GetFiles(targetDir, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"Failed to delete file {file}: {ex.Message}");
                    }
                }

                foreach (string dir in Directory.GetDirectories(targetDir, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        if (Directory.Exists(dir) && Directory.GetFiles(dir).Length == 0)
                        {
                            Directory.Delete(dir, false);
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"Failed to delete directory {dir}: {ex.Message}");
                    }
                }

                Logger.Info("Cleared existing LibVLC files");
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Failed to clear LibVLC directory");
            }
        }

        private async Task ExtractFileAsync(string sourceFile, string targetPath)
        {
            using (var stream = _contentsManager.GetFileStream(sourceFile))
            {
                if (stream != null)
                {
                    string targetDirectory = Path.GetDirectoryName(targetPath);
                    if (!Directory.Exists(targetDirectory))
                    {
                        Directory.CreateDirectory(targetDirectory);
                    }

                    using (var fileStream = File.Create(targetPath))
                    {
                        await stream.CopyToAsync(fileStream);
                    }

                    Logger.Info($"Extracted {sourceFile} to {targetPath}");
                }
                else
                {
                    Logger.Warn($"ContentsManager returned null for: {sourceFile}");
                }
            }
        }

        private IEnumerable<string> LoadFileList()
        {
            using (var stream = _contentsManager.GetFileStream("libvlc-files.txt"))
            {
                if (stream == null)
                {
                    Logger.Error("Could not find libvlc-files.txt in module resources");
                    yield break;
                }

                using (var reader = new StreamReader(stream))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (!string.IsNullOrWhiteSpace(line))
                        {
                            yield return line.Trim();
                        }
                    }
                }
            }
        }

        public static string GetBinPath(string libvlcDir)
        {
            return Path.Combine(libvlcDir, "win-x64");
        }
    }
}
