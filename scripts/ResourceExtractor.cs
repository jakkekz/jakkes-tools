using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace CS2KZMappingTools
{
    public static class ResourceExtractor
    {
        private static string? _extractPath;
        private static bool _extracted = false;
        private static bool _dependenciesInstalled = false;

        public static string ExtractResources()
        {
            if (_extractPath != null && _extracted)
                return _extractPath;

            // Extract to temp folder (same location as other tools)
            _extractPath = Path.Combine(Path.GetTempPath(), ".CS2KZ-mapping-tools");

            Directory.CreateDirectory(_extractPath);

            var assembly = Assembly.GetExecutingAssembly();
            var resourceNames = assembly.GetManifestResourceNames();

            foreach (var resourceName in resourceNames)
            {
                try
                {
                    // Resource names are in format: CS2KZMappingTools.folder.subfolder.file.ext
                    // We need to reconstruct the path
                    if (!resourceName.StartsWith("CS2KZMappingTools."))
                        continue;

                    var relativeName = resourceName.Substring("CS2KZMappingTools.".Length);
                    
                    // Skip if it's a compiled resource
                    if (relativeName.Contains(".g.") || relativeName.EndsWith(".resources"))
                        continue;

                    // Build the file path
                    string filePath = "";
                    
                    // Handle different resource types
                    if (relativeName.StartsWith("icons."))
                    {
                        var fileName = relativeName.Substring("icons.".Length);
                        filePath = Path.Combine(_extractPath, "icons", fileName);
                    }
                    else if (relativeName.StartsWith("scripts."))
                    {
                        var parts = relativeName.Substring("scripts.".Length).Split('.');
                        // Reconstruct path with proper extension
                        if (parts.Length >= 2)
                        {
                            var pathParts = parts.Take(parts.Length - 1).ToList();
                            var extension = parts.Last();
                            
                            // Handle nested folders
                            var fileName = string.Join(".", pathParts) + "." + extension;
                            if (pathParts.Count > 1)
                            {
                                var folder = pathParts[0];
                                fileName = string.Join(Path.DirectorySeparatorChar.ToString(), pathParts.Skip(1)) + "." + extension;
                                filePath = Path.Combine(_extractPath, "scripts", folder, fileName);
                            }
                            else
                            {
                                filePath = Path.Combine(_extractPath, "scripts", fileName);
                            }
                        }
                    }
                    else if (relativeName.StartsWith("fonts."))
                    {
                        var fileName = relativeName.Substring("fonts.".Length);
                        filePath = Path.Combine(_extractPath, "fonts", fileName);
                    }
                    else if (relativeName.StartsWith("chars."))
                    {
                        var fileName = relativeName.Substring("chars.".Length);
                        filePath = Path.Combine(_extractPath, "chars", fileName);
                    }
                    else if (relativeName.StartsWith("utils."))
                    {
                        var fileName = relativeName.Substring("utils.".Length);
                        filePath = Path.Combine(_extractPath, "utils", fileName);
                    }
                    else if (relativeName == "requirements.txt")
                    {
                        filePath = Path.Combine(_extractPath, "requirements.txt");
                    }
                    else
                    {
                        continue; // Skip unknown resources
                    }

                    if (string.IsNullOrEmpty(filePath))
                        continue;

                    var directory = Path.GetDirectoryName(filePath);
                    if (directory != null && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    // Always extract to ensure we have latest version
                    using var stream = assembly.GetManifestResourceStream(resourceName);
                    if (stream != null)
                    {
                        using var fileStream = File.Create(filePath);
                        stream.CopyTo(fileStream);
                    }
                }
                catch
                {
                    // Continue on error
                }
            }

            _extracted = true;
            return _extractPath;
        }

        public static void EnsurePythonDependencies()
        {
            if (_dependenciesInstalled)
                return;

            try
            {
                string basePath = ExtractResources();
                string requirementsPath = Path.Combine(basePath, "requirements.txt");

                // Check if requirements.txt was extracted
                if (!File.Exists(requirementsPath))
                {
                    // Create requirements.txt if not embedded
                    File.WriteAllText(requirementsPath, @"imgui[glfw]==2.0.0
PyOpenGL
vdf
Pillow
numpy
colorama
");
                }

                // Install dependencies silently in the background
                var startInfo = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c pip install -q -r \"{requirementsPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                var process = Process.Start(startInfo);
                if (process != null)
                {
                    process.WaitForExit(30000); // Wait max 30 seconds
                    _dependenciesInstalled = true;
                }
            }
            catch
            {
                // Silently fail - dependencies might already be installed
                _dependenciesInstalled = true;
            }
        }
    }
}
