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

        public static string ExtractResources(Action<string>? logCallback = null)
        {
            if (_extractPath != null && _extracted)
                return _extractPath;

            // Extract to temp folder (same location as other tools)
            _extractPath = Path.Combine(Path.GetTempPath(), ".CS2KZ-mapping-tools");

            Directory.CreateDirectory(_extractPath);

            var assembly = Assembly.GetExecutingAssembly();
            var resourceNames = assembly.GetManifestResourceNames();
            
            // Log python-embed resources for debugging
            var pythonEmbedResources = resourceNames.Where(r => r.Contains("python-embed")).ToList();
            if (pythonEmbedResources.Count > 0)
            {
                logCallback?.Invoke($"Found {pythonEmbedResources.Count} python-embed resources in assembly");
                // Log first few for debugging
                foreach (var res in pythonEmbedResources.Take(3))
                {
                    logCallback?.Invoke($"  Example: {res}");
                }
                if (pythonEmbedResources.Count > 3)
                {
                    logCallback?.Invoke($"  ... and {pythonEmbedResources.Count - 3} more");
                }
            }
            else
            {
                logCallback?.Invoke("No python-embed resources found (Lite edition)");
            }

            int pythonFilesExtracted = 0;
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
                    else if (relativeName.StartsWith("python-embed."))
                    {
                        // Handle embedded Python files
                        // Resource name format: CS2KZMappingTools.python-embed.folder.file.ext
                        // We need to convert dots back to directory separators, but preserve file extension
                        var fileName = relativeName.Substring("python-embed.".Length);
                        
                        // Find the last dot (file extension)
                        int lastDotIndex = fileName.LastIndexOf('.');
                        if (lastDotIndex > 0)
                        {
                            string pathWithoutExt = fileName.Substring(0, lastDotIndex);
                            string extension = fileName.Substring(lastDotIndex);
                            
                            // Convert remaining dots to directory separators
                            string pathPart = pathWithoutExt.Replace('.', Path.DirectorySeparatorChar);
                            filePath = Path.Combine(_extractPath, "python-embed", pathPart + extension);
                        }
                        else
                        {
                            // No extension, treat whole thing as path
                            string pathPart = fileName.Replace('.', Path.DirectorySeparatorChar);
                            filePath = Path.Combine(_extractPath, "python-embed", pathPart);
                        }
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
                        
                        // Count python-embed file extraction
                        if (resourceName.Contains("python-embed"))
                        {
                            pythonFilesExtracted++;
                        }
                    }
                }
                catch
                {
                    // Continue on error
                }
            }
            
            if (pythonFilesExtracted > 0)
            {
                logCallback?.Invoke($"✓ Extracted {pythonFilesExtracted} embedded Python files");
            }

            _extracted = true;
            return _extractPath;
        }

        public static string EnsurePythonDependencies(Action<string>? logCallback = null)
        {
            if (_dependenciesInstalled)
                return "Dependencies already installed";

            try
            {
                logCallback?.Invoke("Extracting embedded resources...");
                
                string basePath = ExtractResources(logCallback);
                string requirementsPath = Path.Combine(basePath, "requirements.txt");
                
                // Check for embedded Python first (Complete Edition)
                string embeddedPythonPath = Path.Combine(basePath, "python-embed", "python.exe");
                
                logCallback?.Invoke($"Looking for embedded Python at: {embeddedPythonPath}");
                
                // Check if any python-embed resources exist at all
                var assembly = Assembly.GetExecutingAssembly();
                var pythonResources = assembly.GetManifestResourceNames()
                    .Where(r => r.Contains("python-embed"))
                    .ToList();
                
                if (File.Exists(embeddedPythonPath))
                {
                    logCallback?.Invoke("✓ Using embedded Python (Complete Edition)");
                    logCallback?.Invoke($"Python location: {embeddedPythonPath}");
                    _dependenciesInstalled = true;
                    return "Success";
                }
                else if (pythonResources.Any())
                {
                    logCallback?.Invoke($"⚠ Warning: Found {pythonResources.Count} python-embed resources but python.exe not extracted");
                    logCallback?.Invoke($"⚠ This may be a build issue. Falling back to system Python...");
                }
                else
                {
                    logCallback?.Invoke("This is the Lite edition - checking for system Python...");
                }

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
                    logCallback?.Invoke("Created requirements.txt");
                }

                // Check if system Python is available (Lite Edition)
                var pythonCheck = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = "--version",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                try
                {
                    using var checkProcess = Process.Start(pythonCheck);
                    if (checkProcess != null)
                    {
                        checkProcess.WaitForExit(5000);
                        if (checkProcess.ExitCode != 0)
                        {
                            string error = "Python not found. Please install Python 3.11+ from https://www.python.org/downloads/";
                            logCallback?.Invoke($"[ERR] {error}");
                            return error;
                        }
                        string version = checkProcess.StandardOutput.ReadToEnd().Trim();
                        logCallback?.Invoke($"Found system {version}");
                    }
                }
                catch (Exception ex)
                {
                    string error = $"Python not found: {ex.Message}";
                    logCallback?.Invoke($"[ERR] {error}");
                    return error;
                }

                // Check if dependencies are already installed
                logCallback?.Invoke("Checking installed packages...");
                var checkInstalled = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = "-m pip list",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var listProcess = Process.Start(checkInstalled);
                if (listProcess != null)
                {
                    listProcess.WaitForExit(10000);
                    string installedPackages = listProcess.StandardOutput.ReadToEnd().ToLower();
                    
                    if (installedPackages.Contains("imgui") && 
                        installedPackages.Contains("pyopengl") && 
                        installedPackages.Contains("vdf") && 
                        installedPackages.Contains("pillow"))
                    {
                        logCallback?.Invoke("✓ All dependencies already installed");
                        _dependenciesInstalled = true;
                        return "Success";
                    }
                }

                // Install dependencies with progress
                logCallback?.Invoke("Installing Python dependencies (this may take 1-2 minutes)...");
                var startInfo = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = $"-m pip install -r \"{requirementsPath}\" --disable-pip-version-check",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    // Read output in real-time
                    string output = "";
                    string errors = "";
                    
                    process.OutputDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            output += e.Data + "\n";
                            if (e.Data.Contains("Successfully installed") || e.Data.Contains("Requirement already satisfied"))
                            {
                                logCallback?.Invoke($"  {e.Data}");
                            }
                        }
                    };
                    
                    process.ErrorDataReceived += (s, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            errors += e.Data + "\n";
                            logCallback?.Invoke($"  [pip] {e.Data}");
                        }
                    };
                    
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    
                    // Wait up to 2 minutes for installation
                    bool finished = process.WaitForExit(120000);
                    
                    if (!finished)
                    {
                        process.Kill();
                        string error = "Installation timed out after 2 minutes";
                        logCallback?.Invoke($"[ERR] {error}");
                        return error;
                    }
                    
                    if (process.ExitCode == 0)
                    {
                        logCallback?.Invoke("✓ Dependencies installed successfully");
                        _dependenciesInstalled = true;
                        return "Success";
                    }
                    else
                    {
                        string error = $"Installation failed with exit code {process.ExitCode}";
                        logCallback?.Invoke($"[ERR] {error}");
                        if (!string.IsNullOrEmpty(errors))
                        {
                            logCallback?.Invoke($"[ERR] {errors}");
                        }
                        return error;
                    }
                }
                
                return "Failed to start pip";
            }
            catch (Exception ex)
            {
                string error = $"Exception during dependency installation: {ex.Message}";
                logCallback?.Invoke($"[ERR] {error}");
                return error;
            }
        }
    }
}
