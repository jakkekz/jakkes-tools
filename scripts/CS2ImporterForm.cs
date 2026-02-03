using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CS2KZMappingTools
{
    public class CS2ImporterForm : Form
    {
        private ThemeManager _themeManager;
        private MappingManager _mappingManager;
        
        private Button _selectBspButton = null!;
        private Button _openCsgoHammerButton = null!;
        private Button _goButton = null!;
        private Button _openLogButton = null!;
        private Button _openFolderButton = null!;
        private TextBox _addonNameTextBox = null!;
        private Label _bspPathLabel = null!;
        private Label _statusLabel = null!;
        private Label _progressLabel = null!;
        private ProgressBar _progressBar = null!;
        private Panel _statsPanel = null!;
        private Label _materialsLabel = null!;
        private Label _modelsLabel = null!;
        private Label _vmapsLabel = null!;
        private GroupBox _guideGroupBox = null!;
        private RichTextBox _guideTextBox = null!;
        
        private string? _csgoBasefolder;
        private string? _vmfFolder;
        private string? _mapName;
        private string? _previousMapName;
        private string _launchOptions = "-usebsp";
        private string _vmfDefaultPath = "C:\\";
        
        private bool _importInProgress;
        private bool _heightExpanded;
        private int _totalMaterials;
        private int _importedMaterials;
        private int _totalModels;
        private int _importedModels;
        private int _totalVmaps;
        private int _importedVmaps;
        private string _currentStage = "";
        private bool _vpkLockDetected;
        private bool _vmfStructureError;
        
        private Point _mouseDownPoint;
        private bool _isDragging;
        private StringBuilder _logBuilder = new StringBuilder();
        private string? _logFilePath;
        
        public CS2ImporterForm()
        {
            _themeManager = ThemeManager.Instance;
            _mappingManager = new MappingManager();
            
            InitializeComponent();
            ApplyTheme();
            
            // Ensure Python dependencies are installed
            Task.Run(() => ResourceExtractor.EnsurePythonDependencies());
            
            LoadConfig();
            AutoDetectCS2();
        }
        
        private void InitializeComponent()
        {
            // Form settings
            Text = "CS2 Map Importer";
            Size = new Size(360, 310);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(30, 30, 30);
            
            // Guide GroupBox (collapsible)
            _guideGroupBox = new GroupBox
            {
                Text = "► Read Before Import",
                Location = new Point(10, 42),
                Size = new Size(340, 30),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            _guideGroupBox.Click += GuideGroupBox_Click;
            
            _guideTextBox = new RichTextBox
            {
                Location = new Point(10, 25),
                Size = new Size(320, 260),
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(40, 40, 40),
                ForeColor = Color.White,
                Visible = false,
                Text = "0. Close CS2\n\n" +
                       "1. Download the .bsp\n" +
                       "   Ex: https://files.femboy.kz/fastdl/csgo/maps/\n\n" +
                       "2. Select the .bsp (auto-decompiles)\n" +
                       "   - VMF is decompiled to \"sdk_content/maps\"\n" +
                       "   - Open VMF in CS:GO Hammer and save it\n" +
                       "   - If you want to edit the .vmf do it now in this .vmf\n" +
                       "       - for ex. func_illusionary -> func_detail\n\n" +
                       "3. Choose a new Addon Name\n\n" +
                       "4. GO!\n\n" +
                       "5. Open map in CS2 hammer and collapse the prefabs"
            };
            _guideGroupBox.Controls.Add(_guideTextBox);
            
            // BSP selection
            var bspLabel = new Label
            {
                Text = "1. BSP File:",
                Location = new Point(10, 82),
                Size = new Size(80, 20),
                ForeColor = Color.White
            };
            
            _selectBspButton = new Button
            {
                Text = "1. Select BSP File",
                Location = new Point(10, 107),
                Size = new Size(200, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            _selectBspButton.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
            _selectBspButton.Click += SelectBspButton_Click;
            
            _bspPathLabel = new Label
            {
                Text = "None selected (decompiled)",
                Location = new Point(10, 142),
                Size = new Size(340, 20),
                ForeColor = Color.Red,
                AutoEllipsis = true
            };
            
            _openCsgoHammerButton = new Button
            {
                Text = "2. Open CSGO Hammer and save .vmf",
                Location = new Point(10, 167),
                Size = new Size(200, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Visible = false
            };
            _openCsgoHammerButton.FlatAppearance.BorderColor = Color.FromArgb(70, 70, 70);
            _openCsgoHammerButton.Click += OpenCsgoHammerButton_Click;
            
            // Addon name
            var addonLabel = new Label
            {
                Text = "3. Addon Name:",
                Location = new Point(10, 205),
                Size = new Size(110, 20),
                ForeColor = Color.White
            };
            
            _addonNameTextBox = new TextBox
            {
                Location = new Point(10, 230),
                Size = new Size(200, 25),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            
            // GO button
            _goButton = new Button
            {
                Text = "4. GO!",
                Location = new Point(10, 265),
                Size = new Size(70, 35),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(51, 178, 51),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _goButton.FlatAppearance.BorderSize = 0;
            _goButton.Click += GoButton_Click;
            
            // Progress tracking (hidden initially)
            _statusLabel = new Label
            {
                Location = new Point(10, 305),
                Size = new Size(340, 20),
                ForeColor = Color.Yellow,
                Visible = false
            };
            
            _progressBar = new ProgressBar
            {
                Location = new Point(10, 328),
                Size = new Size(200, 20),
                Visible = false
            };
            
            _progressLabel = new Label
            {
                Location = new Point(215, 328),
                Size = new Size(50, 20),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = false
            };
            
            // Stats panel (hidden initially)
            _statsPanel = new Panel
            {
                Location = new Point(10, 352),
                Size = new Size(200, 65),
                Visible = false
            };
            
            _materialsLabel = new Label
            {
                Location = new Point(0, 0),
                Size = new Size(200, 18),
                ForeColor = Color.Gray,
                Text = "Materials: 0/0"
            };
            
            _modelsLabel = new Label
            {
                Location = new Point(0, 20),
                Size = new Size(200, 18),
                ForeColor = Color.Gray,
                Text = "Models: 0/0"
            };
            
            _vmapsLabel = new Label
            {
                Location = new Point(0, 40),
                Size = new Size(200, 18),
                ForeColor = Color.Gray,
                Text = "VMAPs: 0/0"
            };
            
            _statsPanel.Controls.AddRange(new Control[] { _materialsLabel, _modelsLabel, _vmapsLabel });
            
            // Completion buttons (hidden initially)
            _openLogButton = new Button
            {
                Text = "Open Log",
                Location = new Point(10, 422),
                Size = new Size(95, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(51, 122, 204),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Visible = false
            };
            _openLogButton.FlatAppearance.BorderSize = 0;
            _openLogButton.Click += OpenLogButton_Click;
            
            _openFolderButton = new Button
            {
                Text = "Open Folder",
                Location = new Point(115, 422),
                Size = new Size(105, 28),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(204, 178, 51),
                ForeColor = Color.White,
                Cursor = Cursors.Hand,
                Visible = false
            };
            _openFolderButton.FlatAppearance.BorderSize = 0;
            _openFolderButton.Click += OpenFolderButton_Click;
            
            // Custom title bar
            var titleBar = new Panel
            {
                Height = 32,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(24, 24, 24)
            };
            
            var titleLabel = new Label
            {
                Text = "CS2 Map Importer",
                Location = new Point(10, 8),
                AutoSize = true,
                ForeColor = Color.White
            };
            
            var closeButton = new Button
            {
                Text = "✕",
                Location = new Point(this.Width - 45, 0),
                Size = new Size(45, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand
            };
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(232, 17, 35);
            closeButton.Click += (s, e) => Close();
            
            var minimizeButton = new Button
            {
                Text = "─",
                Location = new Point(this.Width - 90, 0),
                Size = new Size(45, 32),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.Transparent,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10),
                Cursor = Cursors.Hand
            };
            minimizeButton.FlatAppearance.BorderSize = 0;
            minimizeButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(51, 51, 51);
            minimizeButton.Click += (s, e) => WindowState = FormWindowState.Minimized;
            
            titleBar.Controls.AddRange(new Control[] { titleLabel, minimizeButton, closeButton });
            titleBar.MouseDown += TitleBar_MouseDown;
            titleBar.MouseMove += TitleBar_MouseMove;
            titleBar.MouseUp += TitleBar_MouseUp;
            
            // Add all controls
            Controls.AddRange(new Control[]
            {
                titleBar,
                _guideGroupBox,
                bspLabel,
                _selectBspButton,
                _openCsgoHammerButton,
                _bspPathLabel,
                addonLabel,
                _addonNameTextBox,
                _goButton,
                _statusLabel,
                _progressBar,
                _progressLabel,
                _statsPanel,
                _openLogButton,
                _openFolderButton
            });
        }
        
        private void GuideGroupBox_Click(object? sender, EventArgs e)
        {
            _guideTextBox.Visible = !_guideTextBox.Visible;
            _guideGroupBox.Text = _guideTextBox.Visible ? "▼ Read Before Import" : "► Read Before Import";
            _guideGroupBox.Height = _guideTextBox.Visible ? 295 : 30;
            
            // Adjust form size and control positions
            int heightDelta = _guideTextBox.Visible ? 265 : -265;
            Height += heightDelta;
            
            var controlsToMove = new Control[] { 
                Controls.Cast<Control>().First(c => c.Text == "1. BSP File:"),
                _selectBspButton,
                _bspPathLabel,
                _openCsgoHammerButton,
                Controls.Cast<Control>().First(c => c.Text == "3. Addon Name:"),
                _addonNameTextBox,
                _goButton,
                _statusLabel,
                _progressBar,
                _progressLabel,
                _statsPanel,
                _openLogButton,
                _openFolderButton
            };
            
            foreach (var control in controlsToMove)
            {
                control.Top += heightDelta;
            }
        }
        
        private void ApplyTheme()
        {
            var theme = _themeManager.GetCurrentTheme();
            BackColor = theme.WindowBackground;
        }
        
        private async void SelectBspButton_Click(object? sender, EventArgs e)
        {
            using var openFileDialog = new OpenFileDialog
            {
                Title = "Select a BSP file to import",
                Filter = "BSP files (*.bsp)|*.bsp|All files (*.*)|*.*",
                InitialDirectory = _vmfDefaultPath
            };
            
            if (openFileDialog.ShowDialog() != DialogResult.OK)
                return;
            
            string bspPath = openFileDialog.FileName;
            string bspFilename = Path.GetFileName(bspPath);
            string newMapName = Path.GetFileNameWithoutExtension(bspFilename);
            
            // Update addon name if empty or matches previous default
            if (string.IsNullOrWhiteSpace(_addonNameTextBox.Text) || _addonNameTextBox.Text == _previousMapName)
            {
                _addonNameTextBox.Text = newMapName;
            }
            
            _previousMapName = newMapName;
            _mapName = newMapName;
            
            // Extract BSP
            _bspPathLabel.Text = "Extracting...";
            _bspPathLabel.ForeColor = Color.Yellow;
            
            bool success = await Task.Run(() => ExtractBsp(bspPath));
            
            if (success)
            {
                _bspPathLabel.Text = $"{_mapName}.bsp (decompiled)";
                _bspPathLabel.ForeColor = Color.LimeGreen;
                _vmfDefaultPath = Path.GetDirectoryName(bspPath) ?? "C:\\";
                _openCsgoHammerButton.Visible = true;
            }
            else
            {
                _bspPathLabel.Text = "Extraction failed";
                _bspPathLabel.ForeColor = Color.Red;
            }
        }
        
        private bool ExtractBsp(string bspPath)
        {
            try
            {
                if (_csgoBasefolder == null)
                {
                    MessageBox.Show("CS2 path not detected", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return false;
                }
                
                // Check/download BSPSource
                string tempDir = Path.Combine(Path.GetTempPath(), ".cs2kz-mapping-tools");
                Directory.CreateDirectory(tempDir);
                string bspsrcDir = Path.Combine(tempDir, "bspsrc");
                string bspsrcBat = Path.Combine(bspsrcDir, "bspsrc.bat");
                
                if (!File.Exists(bspsrcBat))
                {
                    // Download BSPSource (simplified - you'd need full implementation)
                    // For now, assume it exists
                }
                
                // Create temp output directory
                string tempOutputDir = Path.Combine(tempDir, $"bspsrc_output_{Guid.NewGuid():N}");
                Directory.CreateDirectory(tempOutputDir);
                
                string mapBaseName = Path.GetFileNameWithoutExtension(bspPath);
                string tempVmf = Path.Combine(tempOutputDir, $"{mapBaseName}.vmf");
                
                // Run BSPSource
                string javaExe = Path.Combine(bspsrcDir, "bin", "java.exe");
                var startInfo = new ProcessStartInfo
                {
                    FileName = javaExe,
                    Arguments = $"-m info.ata4.bspsrc.app/info.ata4.bspsrc.app.src.cli.BspSourceCli --unpack_embedded --no_ttfix -o \"{tempVmf}\" \"{bspPath}\"",
                    WorkingDirectory = bspsrcDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                };
                
                using var process = Process.Start(startInfo);
                if (process == null)
                    return false;
                
                process.WaitForExit(120000); // 2 minute timeout
                
                // Move files to appropriate locations
                string baseDir = _csgoBasefolder.Replace("/", "\\");
                string csgoDir = Path.Combine(baseDir, "csgo");
                string sdkContentMaps = Path.Combine(baseDir, "sdk_content", "maps");
                Directory.CreateDirectory(sdkContentMaps);
                
                string extractedFolder = Path.Combine(tempOutputDir, mapBaseName);
                string finalVmf = Path.Combine(sdkContentMaps, $"{mapBaseName}.vmf");
                
                if (File.Exists(tempVmf))
                {
                    File.Copy(tempVmf, finalVmf, true);
                    _vmfFolder = Path.Combine(baseDir, "sdk_content");
                    
                    // Copy original BSP
                    string finalBsp = Path.Combine(sdkContentMaps, Path.GetFileName(bspPath));
                    File.Copy(bspPath, finalBsp, true);
                    
                    // Extract embedded materials and models from BSPSource output
                    // BSPSource extracts to temp_output_dir/mapname/ folder
                    if (Directory.Exists(extractedFolder))
                    {
                        // Handle extracted models
                        string tempModels = Path.Combine(extractedFolder, "models");
                        if (Directory.Exists(tempModels))
                        {
                            LogMessage("Extracting embedded models...");
                            string csgoModels = Path.Combine(csgoDir, "models");
                            string mapsModels = Path.Combine(sdkContentMaps, "models");
                            Directory.CreateDirectory(csgoModels);
                            Directory.CreateDirectory(mapsModels);
                            
                            int modelCount = CopyDirectoryRecursive(tempModels, csgoModels, mapsModels);
                            LogMessage($"✓ Extracted {modelCount} model files");
                        }
                        
                        // Handle extracted materials
                        string tempMaterials = Path.Combine(extractedFolder, "materials");
                        if (Directory.Exists(tempMaterials))
                        {
                            LogMessage("Extracting embedded materials...");
                            string csgoMaterials = Path.Combine(csgoDir, "materials");
                            string mapsMaterials = Path.Combine(sdkContentMaps, "materials");
                            Directory.CreateDirectory(csgoMaterials);
                            Directory.CreateDirectory(mapsMaterials);
                            
                            var extractedVmts = new List<string>();
                            int materialCount = CopyDirectoryRecursive(tempMaterials, csgoMaterials, mapsMaterials, extractedVmts);
                            LogMessage($"✓ Extracted {materialCount} material files");
                            
                            // Create _embedded_refs.txt for import script
                            if (extractedVmts.Count > 0)
                            {
                                string refsFile = Path.Combine(sdkContentMaps, $"{mapBaseName}_embedded_refs.txt");
                                using (var writer = new StreamWriter(refsFile))
                                {
                                    writer.WriteLine("importfilelist");
                                    writer.WriteLine("{");
                                    foreach (var vmt in extractedVmts)
                                    {
                                        writer.WriteLine($"\t\"file\" \"materials/{vmt}.vmt\"");
                                    }
                                    writer.WriteLine("}");
                                }
                                LogMessage($"✓ Created refs file with {extractedVmts.Count} embedded materials");
                            }
                        }
                    }
                    
                    // Clean up
                    try { Directory.Delete(tempOutputDir, true); } catch { }
                    
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error during extraction: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }
        
        private int CopyDirectoryRecursive(string sourceDir, string destDir1, string destDir2, List<string>? vmtList = null)
        {
            int fileCount = 0;
            
            void CopyFilesRecursive(string srcDir, string relativePath = "")
            {
                foreach (var file in Directory.GetFiles(srcDir))
                {
                    string fileName = Path.GetFileName(file);
                    string relPath = string.IsNullOrEmpty(relativePath) ? fileName : Path.Combine(relativePath, fileName);
                    
                    // Copy to both destination directories
                    string dest1Path = Path.Combine(destDir1, relPath);
                    string dest2Path = Path.Combine(destDir2, relPath);
                    
                    Directory.CreateDirectory(Path.GetDirectoryName(dest1Path)!);
                    Directory.CreateDirectory(Path.GetDirectoryName(dest2Path)!);
                    
                    File.Copy(file, dest1Path, true);
                    File.Copy(file, dest2Path, true);
                    fileCount++;
                    
                    // Track VMT files for refs list
                    if (vmtList != null && fileName.EndsWith(".vmt", StringComparison.OrdinalIgnoreCase))
                    {
                        // Convert to material path (remove .vmt and use forward slashes)
                        string matPath = relPath.Replace('\\', '/');
                        matPath = matPath.Substring(0, matPath.Length - 4); // Remove .vmt extension
                        vmtList.Add(matPath);
                    }
                }
                
                foreach (var dir in Directory.GetDirectories(srcDir))
                {
                    string dirName = Path.GetFileName(dir);
                    string newRelPath = string.IsNullOrEmpty(relativePath) ? dirName : Path.Combine(relativePath, dirName);
                    CopyFilesRecursive(dir, newRelPath);
                }
            }
            
            CopyFilesRecursive(sourceDir);
            return fileCount;
        }
        
        private async void GoButton_Click(object? sender, EventArgs e)
        {
            if (_importInProgress)
                return;
            
            if (_csgoBasefolder == null)
            {
                MessageBox.Show("CS2 path not detected", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            
            if (string.IsNullOrWhiteSpace(_vmfFolder) || string.IsNullOrWhiteSpace(_mapName))
            {
                MessageBox.Show("VMF file not selected", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            
            if (string.IsNullOrWhiteSpace(_addonNameTextBox.Text))
            {
                MessageBox.Show("Addon name not specified", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            
            // Ensure Python dependencies are installed before starting import
            _statusLabel.Text = "Installing Python dependencies...";
            _statusLabel.Visible = true;
            await Task.Run(() => ResourceExtractor.EnsurePythonDependencies());
            _statusLabel.Visible = false;
            
            _importInProgress = true;
            _vpkLockDetected = false;
            _vmfStructureError = false;
            _heightExpanded = false;
            
            // Reset progress
            _totalMaterials = 0;
            _importedMaterials = 0;
            _totalModels = 0;
            _importedModels = 0;
            _totalVmaps = 0;
            _importedVmaps = 0;
            _currentStage = "Starting import...";
            _logBuilder.Clear();
            
            // Create log file
            string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            Directory.CreateDirectory(logDir);
            _logFilePath = Path.Combine(logDir, $"cs2importer_{DateTime.Now:yyyyMMdd_HHmmss}.log");
            
            // Show progress UI
            _statusLabel.Visible = true;
            _progressBar.Visible = true;
            _progressLabel.Visible = true;
            _statsPanel.Visible = true;
            _goButton.Enabled = false;
            
            Height += 130;
            
            SaveConfig();
            
            await RunImportAsync();
        }
        
        private async Task RunImportAsync()
        {
            try
            {
                string cd = Path.Combine(_csgoBasefolder!, "game", "csgo", "import_scripts");
                
                // Get extracted resources path (where Python scripts are)
                string basePath = ResourceExtractor.ExtractResources();
                string jakkeScript = Path.Combine(basePath, "scripts", "porting", "import_map_community_jakke.py");
                
                // Fallback: check if running in development (look for .sln file)
                if (!File.Exists(jakkeScript))
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    // Go up from bin/Debug/net8.0-windows/win-x64 to project root
                    while (!string.IsNullOrEmpty(baseDir) && !File.Exists(Path.Combine(baseDir, "CS2KZMappingTools.sln")))
                    {
                        var parent = Directory.GetParent(baseDir);
                        if (parent == null) break;
                        baseDir = parent.FullName;
                    }
                    jakkeScript = Path.Combine(baseDir, "scripts", "porting", "import_map_community_jakke.py");
                }
                
                string sdkContentDir = Path.Combine(_csgoBasefolder!, "sdk_content");
                
                // Try to find bundled Python first, fall back to system Python
                string pythonExe = "python";
                basePath = ResourceExtractor.ExtractResources(); // Reuse existing basePath variable
                string bundledPython = Path.Combine(basePath, "python-embed", "python.exe");
                
                if (File.Exists(bundledPython))
                {
                    pythonExe = bundledPython;
                    LogMessage($"Using bundled Python: {pythonExe}");
                }
                else
                {
                    // Check if system Python exists
                    var pythonCheck = new ProcessStartInfo
                    {
                        FileName = "cmd.exe",
                        Arguments = "/c python --version",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    
                    try
                    {
                        using var checkProcess = Process.Start(pythonCheck);
                        checkProcess?.WaitForExit(2000);
                        
                        if (checkProcess?.ExitCode != 0)
                        {
                            throw new Exception("Python not found");
                        }
                        
                        LogMessage($"Using system Python");
                    }
                    catch
                    {
                        LogMessage("[ERR] Python not found!");
                        LogMessage("[ERR] Please install Python 3.11+ from https://www.python.org/downloads/");
                        LogMessage("[ERR] Make sure to check 'Add Python to PATH' during installation");
                        _importInProgress = false;
                        return;
                    }
                }
                
                string args = $"-u \"{jakkeScript}\" " +
                             $"\"{Path.Combine(_csgoBasefolder!, "csgo")}\" " +
                             $"\"{sdkContentDir}\" " +
                             $"\"{Path.Combine(_csgoBasefolder!, "game", "csgo")}\" " +
                             $"{_addonNameTextBox.Text} " +
                             $"{_mapName} " +
                             $"{_launchOptions}";
                
                LogMessage($"Starting import: {pythonExe} {args}");
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = pythonExe,
                    Arguments = args,
                    WorkingDirectory = cd,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                
                using var process = new Process { StartInfo = startInfo };
                
                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        LogMessage($"[OUT] {e.Data}");
                        ParseProgress(e.Data);
                    }
                };
                
                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        LogMessage($"[ERR] {e.Data}");
                        ParseProgress(e.Data);
                    }
                };
                
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                
                await process.WaitForExitAsync();
                
                LogMessage($"Process exited with code: {process.ExitCode}");
                
                // Save log to file
                if (_logFilePath != null)
                {
                    await File.WriteAllTextAsync(_logFilePath, _logBuilder.ToString());
                }
                
                // Check if import failed due to VPK lock
                if (process.ExitCode != 0 && _vpkLockDetected)
                {
                    Invoke((MethodInvoker)delegate
                    {
                        _importInProgress = false;
                        _goButton.Enabled = true;
                        _statusLabel.Text = "Failed - Please close CS2!";
                        _statusLabel.ForeColor = Color.Red;
                        MessageBox.Show(
                            "Import failed because CS2 or Hammer is running.\n\n" +
                            "Please close CS2 and all Hammer instances, then try again.",
                            "Import Error - Close CS2",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    });
                    return;
                }
                
                // Check if import failed due to VMF structure
                if (process.ExitCode != 0 && _vmfStructureError)
                {
                    Invoke((MethodInvoker)delegate
                    {
                        _importInProgress = false;
                        _goButton.Enabled = true;
                        _statusLabel.Text = "Import failed!";
                        _statusLabel.ForeColor = Color.Red;
                        _openLogButton.Visible = true;
                        if (!_heightExpanded)
                        {
                            Height += 50;
                            _heightExpanded = true;
                        }
                        MessageBox.Show(
                            "Import failed - VMF file issue.\n\n" +
                            "The script will automatically clean up problematic files on the next run.\n\n" +
                            "Please try clicking GO! again. The issue should be fixed automatically.",
                            "Import Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    });
                    return;
                }
                
                // Check for other errors
                if (process.ExitCode != 0)
                {
                    Invoke((MethodInvoker)delegate
                    {
                        _importInProgress = false;
                        _goButton.Enabled = true;
                        _statusLabel.Text = "Import failed!";
                        _statusLabel.ForeColor = Color.Red;
                        _openLogButton.Visible = true;
                        if (!_heightExpanded)
                        {
                            Height += 50;
                            _heightExpanded = true;
                        }
                        MessageBox.Show(
                            "Import failed. Check the log for details.",
                            "Import Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                    });
                    return;
                }
                
                Invoke((MethodInvoker)delegate
                {
                    _importInProgress = false;
                    _goButton.Enabled = true;
                    _statusLabel.Text = "Complete!";
                    _statusLabel.ForeColor = Color.LimeGreen;
                    _progressBar.Value = 100;
                    _progressLabel.Text = "100%";
                    _openLogButton.Visible = true;
                    _openFolderButton.Visible = true;
                    if (!_heightExpanded)
                    {
                        Height += 50;
                        _heightExpanded = true;
                    }
                });
            }
            catch (Exception ex)
            {
                LogMessage($"Exception: {ex}");
                if (_logFilePath != null)
                {
                    await File.WriteAllTextAsync(_logFilePath, _logBuilder.ToString());
                }
                Invoke((MethodInvoker)delegate
                {
                    MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    _importInProgress = false;
                    _goButton.Enabled = true;
                });
            }
        }
        
        private void LogMessage(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            _logBuilder.AppendLine($"[{timestamp}] {message}");
        }
        
        private void ParseProgress(string message)
        {
            // Check for VPK lock error
            if (message.Contains("ERROR: vpk.signatures file is locked") || 
                message.Contains("Please close CS2 and all Hammer instances"))
            {
                _vpkLockDetected = true;
            }
            
            // Check for VMF structure errors
            if (message.Contains("Failed to write map document to specified file") ||
                message.Contains("CVMFtoVMAP: Missing a required top-level key"))
            {
                _vmfStructureError = true;
            }
            
            // Check for duplicate entity name conflict
            if (message.Contains("FATAL ERROR: Conversion of entity I/O") && 
                message.Contains("sharing target name"))
            {
                // Extract entity name from error message
                var match = Regex.Match(message, @"sharing target name ""([^""]+)""");
                string entityName = match.Success ? match.Groups[1].Value : "unknown";
                
                Invoke((MethodInvoker)delegate
                {
                    MessageBox.Show(
                        $"The VMF has duplicate entities with the same name: \"{entityName}\"\n\n" +
                        $"To fix this:\n" +
                        $"1. Open the VMF in CS:GO Hammer (in sdk_content/maps/)\n" +
                        $"2. Find entities named \"{entityName}\" (use Ctrl+Shift+F)\n" +
                        $"3. Rename one of them (e.g., \"{entityName}2\")\n" +
                        $"4. Save and import again",
                        "Duplicate Entity Name Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning
                    );
                });
            }
            
            // Parse material count
            if (message.Contains("unique material references in VMF"))
            {
                var match = Regex.Match(message, @"Found (\d+) unique material references");
                if (match.Success)
                {
                    _totalMaterials = int.Parse(match.Groups[1].Value);
                    _currentStage = "Porting materials...";
                }
            }
            else if (message.Contains("Imported") && message.Contains("materials"))
            {
                var match = Regex.Match(message, @"Imported (\d+) materials");
                if (match.Success)
                    _importedMaterials = int.Parse(match.Groups[1].Value);
            }
            // Parse model count
            else if (message.Contains("unique model references in VMF"))
            {
                var match = Regex.Match(message, @"Found (\d+) unique model references");
                if (match.Success)
                {
                    _totalModels = int.Parse(match.Groups[1].Value);
                    _currentStage = "Porting models...";
                }
            }
            else if (message.Contains("Imported") && message.Contains("models"))
            {
                var match = Regex.Match(message, @"Imported (\d+) models");
                if (match.Success)
                    _importedModels = int.Parse(match.Groups[1].Value);
            }
            // Parse VMAP count
            else if (message.Contains("Found") && message.Contains("VMAP files to move"))
            {
                var match = Regex.Match(message, @"Found (\d+) VMAP files");
                if (match.Success)
                {
                    _totalVmaps = int.Parse(match.Groups[1].Value);
                    _currentStage = "Processing VMAP files...";
                }
            }
            else if ((message.Contains("-> Moved") || message.Contains("-> Found")) && message.Contains(".vmap"))
            {
                _importedVmaps++;
            }
            
            // Update UI
            Invoke((MethodInvoker)delegate
            {
                _statusLabel.Text = _currentStage;
                
                int total = _totalMaterials + _totalModels + _totalVmaps;
                int completed = _importedMaterials + _importedModels + _importedVmaps;
                
                if (total > 0)
                {
                    _progressBar.Value = Math.Min(100, (int)((double)completed / total * 100));
                    _progressLabel.Text = $"{_progressBar.Value}%";
                }
                
                _materialsLabel.Text = $"Materials: {_importedMaterials}/{_totalMaterials}";
                _modelsLabel.Text = $"Models: {_importedModels}/{_totalModels}";
                _vmapsLabel.Text = $"VMAPs: {_importedVmaps}/{_totalVmaps}";
            });
        }
        
        private void OpenLogButton_Click(object? sender, EventArgs e)
        {
            if (_logFilePath != null && File.Exists(_logFilePath))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(_logFilePath) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to open log: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                MessageBox.Show("No log file available", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        
        private void OpenCsgoHammerButton_Click(object? sender, EventArgs e)
        {
            if (_csgoBasefolder == null || string.IsNullOrWhiteSpace(_mapName))
                return;
            
            try
            {
                string sdkContentMaps = Path.Combine(_csgoBasefolder, "sdk_content", "maps");
                string vmfPath = Path.Combine(sdkContentMaps, $"{_mapName}.vmf");
                
                if (!File.Exists(vmfPath))
                {
                    MessageBox.Show($"VMF file not found: {vmfPath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                
                // Find CS:GO bin folder
                string binFolder = Path.Combine(_csgoBasefolder, "bin");
                
                // Try hammerplusplus.exe first
                string hammerPlusPlusPath = Path.Combine(binFolder, "hammerplusplus.exe");
                if (File.Exists(hammerPlusPlusPath))
                {
                    // Launch without arguments - let user open the VMF through Hammer's File > Open
                    Process.Start(hammerPlusPlusPath);
                    return;
                }
                
                // Fall back to SDKLauncher.exe
                string sdkLauncherPath = Path.Combine(binFolder, "SDKLauncher.exe");
                if (File.Exists(sdkLauncherPath))
                {
                    Process.Start(sdkLauncherPath);
                    return;
                }
                
                // If neither exist, just open the VMF with default handler
                Process.Start(new ProcessStartInfo(vmfPath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open Hammer: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void OpenFolderButton_Click(object? sender, EventArgs e)
        {
            if (_csgoBasefolder != null && !string.IsNullOrWhiteSpace(_addonNameTextBox.Text))
            {
                string addonPath = Path.Combine(_csgoBasefolder, "content", "csgo_addons", _addonNameTextBox.Text);
                if (Directory.Exists(addonPath))
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = addonPath,
                        UseShellExecute = true,
                        Verb = "open"
                    };
                    Process.Start(startInfo);
                }
                else
                {
                    MessageBox.Show($"Addon folder not found: {addonPath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        
        private void AutoDetectCS2()
        {
            var task = _mappingManager.GetSteamDirectoryAsync();
            task.ContinueWith(t =>
            {
                if (t.Result != null)
                {
                    string libraryFoldersPath = Path.Combine(t.Result, "steamapps", "libraryfolders.vdf");
                    var cs2Task = _mappingManager.FindCS2LibraryPathAsync(libraryFoldersPath);
                    cs2Task.ContinueWith(cs2 =>
                    {
                        if (cs2.Result != null)
                        {
                            _csgoBasefolder = Path.Combine(cs2.Result, "steamapps", "common", "Counter-Strike Global Offensive");
                        }
                    });
                }
            });
        }
        
        private void LoadConfig()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts", "porting", "cs2importer.cfg");
                if (File.Exists(configPath))
                {
                    var lines = File.ReadAllLines(configPath);
                    if (lines.Length > 0)
                        _launchOptions = lines[0].Trim();
                    if (lines.Length > 1 && !string.IsNullOrWhiteSpace(lines[1]))
                        _csgoBasefolder = lines[1].Trim();
                    if (lines.Length > 2 && !string.IsNullOrWhiteSpace(lines[2]))
                        _vmfDefaultPath = lines[2].Trim();
                }
            }
            catch { }
        }
        
        private void SaveConfig()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts", "porting", "cs2importer.cfg");
                File.WriteAllText(configPath, $"{_launchOptions}\n{_csgoBasefolder ?? ""}\n{_vmfDefaultPath}");
            }
            catch { }
        }
        
        private void TitleBar_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _isDragging = true;
                _mouseDownPoint = e.Location;
            }
        }
        
        private void TitleBar_MouseMove(object? sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                Point newLocation = Location;
                newLocation.X += e.X - _mouseDownPoint.X;
                newLocation.Y += e.Y - _mouseDownPoint.Y;
                Location = newLocation;
            }
        }
        
        private void TitleBar_MouseUp(object? sender, MouseEventArgs e)
        {
            _isDragging = false;
        }
    }
}
