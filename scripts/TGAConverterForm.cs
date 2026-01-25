using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CS2KZMappingTools
{
    public class TGAConverterForm : Form
    {
        private ThemeManager _themeManager;
        private ListView _fileListView = null!;
        private Button _addFilesButton = null!;
        private Button _addAddonButton = null!;
        private Button _removeSelectedButton = null!;
        private Button _clearAllButton = null!;
        private Button _convertButton = null!;
        private ComboBox _formatComboBox = null!;
        private ComboBox _addonComboBox = null!;
        private CheckBox _updateVmatsCheckBox = null!;
        private NumericUpDown _widthNumeric = null!;
        private NumericUpDown _heightNumeric = null!;
        private CheckBox _maintainAspectCheckBox = null!;
        private CheckBox _useOriginalSizeCheckBox = null!;
        private TrackBar _qualitySlider = null!;
        private Label _qualityLabel = null!;
        private ProgressBar _progressBar = null!;
        private Label _statusLabel = null!;
        private Label _spaceSavingsLabel = null!;
        private Label _dragDropLabel = null!;
        private Label _progressPercentLabel = null!;
        
        private List<string> _queuedFiles = new List<string>();
        private Dictionary<string, string> _convertedFiles = new Dictionary<string, string>(); // Maps original TGA path to converted file path
        private bool _isConverting;
        private int _sortColumn = -1;
        private bool _sortAscending = true;
        private string? _cs2Path;
        private string[] _addonList = Array.Empty<string>();
        
        public TGAConverterForm()
        {
            _themeManager = ThemeManager.Instance;
            InitializeComponent();
            ApplyTheme();
            AutoDetectCS2();
        }
        
        private void InitializeComponent()
        {
            Text = "TGA Converter";
            Size = new Size(900, 800);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(30, 30, 30);
            AllowDrop = true;
            
            DragEnter += Form_DragEnter;
            DragDrop += Form_DragDrop;
            
            // Title bar
            var titleBar = CreateTitleBar();
            
            // File list
            var listLabel = new Label
            {
                Text = "Files:",
                Location = new Point(10, 42),
                Size = new Size(200, 20),
                ForeColor = Color.White
            };
            
            _fileListView = new ListView
            {
                Location = new Point(10, 67),
                Size = new Size(860, 280),
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                View = View.Details,
                FullRowSelect = true,
                GridLines = false,
                AllowDrop = true,
                Scrollable = true,
                OwnerDraw = true
            };
            _fileListView.Columns.Add("File Path", 450);
            _fileListView.Columns.Add("Resolution", 120);
            _fileListView.Columns.Add("Size (KB)", 130);
            _fileListView.Columns.Add("Status", 155);
            _fileListView.DragEnter += ListView_DragEnter;
            _fileListView.DragDrop += ListView_DragDrop;
            _fileListView.ColumnClick += ListView_ColumnClick;
            _fileListView.DrawColumnHeader += ListView_DrawColumnHeader;
            _fileListView.DrawSubItem += ListView_DrawSubItem;
            
            // Drag-drop hint label
            _dragDropLabel = new Label
            {
                Text = "☁ Drag & Drop TGA Files Here ☁\n\nOr click 'Add Files' below",
                Location = new Point(10, 150),
                Size = new Size(860, 80),
                ForeColor = Color.FromArgb(100, 100, 100),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 14, FontStyle.Italic),
                BackColor = Color.Transparent
            };
            _dragDropLabel.MouseDown += (s, e) => _fileListView.Focus();
            _dragDropLabel.BringToFront();
            
            // File management buttons
            _addFilesButton = new Button
            {
                Text = "Add Files",
                Location = new Point(10, 357),
                Size = new Size(120, 35),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(51, 122, 204),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            _addFilesButton.FlatAppearance.BorderSize = 0;
            _addFilesButton.Click += AddFilesButton_Click;
            
            // Addon ComboBox (next to Add Files button)
            _addonComboBox = new ComboBox
            {
                Location = new Point(140, 357),
                Size = new Size(250, 35),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White
            };
            
            // Add Addon button (after addon dropdown)
            _addAddonButton = new Button
            {
                Text = "Add Addon",
                Location = new Point(400, 357),
                Size = new Size(120, 35),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(51, 153, 102),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            _addAddonButton.FlatAppearance.BorderSize = 0;
            _addAddonButton.Click += AddAddonButton_Click;
            
            // Drag and drop hint
            var dragDropHint = new Label
            {
                Text = "or drag and drop",
                Location = new Point(10, 400),
                Size = new Size(150, 15),
                ForeColor = Color.FromArgb(150, 150, 150),
                Font = new Font("Segoe UI", 8, FontStyle.Italic)
            };
            
            _removeSelectedButton = new Button
            {
                Text = "Remove Selected",
                Location = new Point(530, 357),
                Size = new Size(140, 35),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(204, 102, 51),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            _removeSelectedButton.FlatAppearance.BorderSize = 0;
            _removeSelectedButton.Click += RemoveSelectedButton_Click;
            
            _clearAllButton = new Button
            {
                Text = "Clear All",
                Location = new Point(680, 357),
                Size = new Size(100, 35),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(178, 51, 51),
                ForeColor = Color.White,
                Cursor = Cursors.Hand
            };
            _clearAllButton.FlatAppearance.BorderSize = 0;
            _clearAllButton.Click += ClearAllButton_Click;
            
            // Output settings
            var settingsLabel = new Label
            {
                Text = "Output Settings:",
                Location = new Point(10, 492),
                Size = new Size(200, 20),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold)
            };
            
            var formatLabel = new Label
            {
                Text = "Format:",
                Location = new Point(10, 522),
                Size = new Size(80, 20),
                ForeColor = Color.White
            };
            
            _formatComboBox = new ComboBox
            {
                Location = new Point(100, 520),
                Size = new Size(100, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White
            };
            _formatComboBox.Items.AddRange(new object[] { "PNG", "JPG" });
            _formatComboBox.SelectedIndex = 0;
            _formatComboBox.SelectedIndexChanged += FormatComboBox_SelectedIndexChanged;
            
            var resolutionLabel = new Label
            {
                Text = "Resolution:",
                Location = new Point(10, 557),
                Size = new Size(80, 20),
                ForeColor = Color.White
            };
            
            _useOriginalSizeCheckBox = new CheckBox
            {
                Text = "Use Original Size",
                Location = new Point(100, 555),
                Size = new Size(150, 25),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Checked = true
            };
            _useOriginalSizeCheckBox.CheckedChanged += UseOriginalSizeCheckBox_CheckedChanged;
            
            var widthLabel = new Label
            {
                Text = "Width:",
                Location = new Point(10, 592),
                Size = new Size(80, 20),
                ForeColor = Color.White
            };
            
            _widthNumeric = new NumericUpDown
            {
                Location = new Point(100, 590),
                Size = new Size(100, 25),
                Minimum = 1,
                Maximum = 16384,
                Value = 1024,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                Enabled = false
            };
            _widthNumeric.ValueChanged += WidthNumeric_ValueChanged;
            
            var heightLabel = new Label
            {
                Text = "Height:",
                Location = new Point(210, 592),
                Size = new Size(50, 20),
                ForeColor = Color.White
            };
            
            _heightNumeric = new NumericUpDown
            {
                Location = new Point(270, 590),
                Size = new Size(100, 25),
                Minimum = 1,
                Maximum = 16384,
                Value = 1024,
                BackColor = Color.FromArgb(45, 45, 45),
                ForeColor = Color.White,
                Enabled = false
            };
            _heightNumeric.ValueChanged += HeightNumeric_ValueChanged;
            
            _maintainAspectCheckBox = new CheckBox
            {
                Text = "Maintain Aspect Ratio",
                Location = new Point(380, 590),
                Size = new Size(170, 25),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Checked = true,
                Enabled = false
            };
            _maintainAspectCheckBox.ForeColor = Color.White; // Ensure white text
            
            var qualityLabelText = new Label
            {
                Text = "Quality:",
                Location = new Point(10, 627),
                Size = new Size(80, 20),
                ForeColor = Color.White
            };
            
            _qualitySlider = new TrackBar
            {
                Location = new Point(100, 622),
                Size = new Size(300, 45),
                Minimum = 0,
                Maximum = 100,
                Value = 100,
                TickFrequency = 10
            };
            _qualitySlider.ValueChanged += QualitySlider_ValueChanged;
            
            _qualityLabel = new Label
            {
                Text = "100%",
                Location = new Point(410, 627),
                Size = new Size(50, 20),
                ForeColor = Color.LimeGreen,
                Font = new Font("Segoe UI", 9, FontStyle.Bold)
            };
            
            // Update .vmat files checkbox
            _updateVmatsCheckBox = new CheckBox
            {
                Text = "Update .vmat files automatically",
                Location = new Point(10, 665),
                Size = new Size(250, 25),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Checked = false
            };
            
            // Convert button (moved to bottom left)
            _convertButton = new Button
            {
                Text = "Convert All",
                Location = new Point(10, 700),
                Size = new Size(150, 50),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(51, 178, 51),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _convertButton.FlatAppearance.BorderSize = 0;
            _convertButton.Click += ConvertButton_Click;
            
            // Progress bar (next to convert button)
            _progressBar = new ProgressBar
            {
                Location = new Point(170, 710),
                Size = new Size(710, 30),
                Visible = false
            };
            
            // Progress percentage label (overlay on progress bar)
            _progressPercentLabel = new Label
            {
                Location = new Point(170, 710),
                Size = new Size(710, 30),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Visible = false
            };
            _progressPercentLabel.BringToFront();
            
            // Status label (above progress bar)
            _statusLabel = new Label
            {
                Location = new Point(170, 685),
                Size = new Size(710, 20),
                ForeColor = Color.Yellow,
                Visible = false,
                TextAlign = ContentAlignment.MiddleLeft
            };
            
            // Space savings label (below progress bar)
            _spaceSavingsLabel = new Label
            {
                Text = "",
                Location = new Point(170, 745),
                Size = new Size(710, 20),
                ForeColor = Color.LimeGreen,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Visible = false
            };
            
            Controls.AddRange(new Control[]
            {
                titleBar,
                listLabel,
                _fileListView,
                _dragDropLabel,
                _addFilesButton,
                _addAddonButton,
                _addonComboBox,
                dragDropHint,
                _removeSelectedButton,
                _clearAllButton,
                settingsLabel,
                formatLabel,
                _formatComboBox,
                resolutionLabel,
                _useOriginalSizeCheckBox,
                _updateVmatsCheckBox,
                widthLabel,
                _widthNumeric,
                heightLabel,
                _heightNumeric,
                _maintainAspectCheckBox,
                qualityLabelText,
                _qualitySlider,
                _qualityLabel,
                _spaceSavingsLabel,
                _convertButton,
                _statusLabel,
                _progressBar,
                _progressPercentLabel
            });
        }
        
        private Panel CreateTitleBar()
        {
            var titleBar = new Panel
            {
                Height = 32,
                Dock = DockStyle.Top,
                BackColor = Color.FromArgb(20, 20, 20)
            };
            
            var titleLabel = new Label
            {
                Text = "TGA Converter",
                Location = new Point(10, 8),
                AutoSize = true,
                ForeColor = Color.White
            };
            
            var closeButton = new Button
            {
                Text = "✕",
                Location = new Point(Width - 45, 0),
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
                Location = new Point(Width - 90, 0),
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
            
            Point mouseDownPoint = Point.Empty;
            bool isDragging = false;
            
            titleBar.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    isDragging = true;
                    mouseDownPoint = e.Location;
                }
            };
            
            titleBar.MouseMove += (s, e) =>
            {
                if (isDragging)
                {
                    Point newLocation = Location;
                    newLocation.X += e.X - mouseDownPoint.X;
                    newLocation.Y += e.Y - mouseDownPoint.Y;
                    Location = newLocation;
                }
            };
            
            titleBar.MouseUp += (s, e) => { isDragging = false; };
            
            return titleBar;
        }
        
        private void ApplyTheme()
        {
            var theme = _themeManager.GetCurrentTheme();
            BackColor = theme.WindowBackground;
            
            // Ensure checkboxes have white text
            _maintainAspectCheckBox.ForeColor = Color.White;
            _useOriginalSizeCheckBox.ForeColor = Color.White;
        }
        
        private void Form_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                e.Effect = DragDropEffects.Copy;
        }
        
        private void Form_DragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] files)
            {
                AddFilesToQueue(files);
            }
        }
        
        private void ListView_DragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                e.Effect = DragDropEffects.Copy;
        }
        
        private void ListView_DragDrop(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetData(DataFormats.FileDrop) is string[] files)
            {
                AddFilesToQueue(files);
            }
        }
        
        private void AddFilesButton_Click(object? sender, EventArgs e)
        {
            using var openFileDialog = new OpenFileDialog
            {
                Title = "Select TGA Files",
                Filter = "TGA files (*.tga)|*.tga|All files (*.*)|*.*",
                Multiselect = true
            };
            
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                AddFilesToQueue(openFileDialog.FileNames);
            }
        }
        
        private void AddFilesToQueue(string[] files)
        {
            foreach (var file in files)
            {
                if (!File.Exists(file))
                    continue;
                
                string ext = Path.GetExtension(file).ToLowerInvariant();
                if (ext != ".tga")
                    continue;
                
                if (_queuedFiles.Contains(file))
                    continue;
                
                _queuedFiles.Add(file);
                
                try
                {
                    var fileInfo = new FileInfo(file);
                    var item = new ListViewItem(ShortenPath(file)); // Show shortened path
                    
                    // Try to read TGA dimensions
                    int width = 0, height = 0;
                    try
                    {
                        (width, height) = ReadTGADimensions(file);
                        item.SubItems.Add($"{width}x{height}");
                    }
                    catch
                    {
                        item.SubItems.Add("Unknown");
                    }
                    
                    long beforeKB = fileInfo.Length / 1024;
                    item.SubItems.Add($"{beforeKB}");
                    
                    var statusItem = item.SubItems.Add("Queued");
                    statusItem.ForeColor = Color.White;
                    item.Tag = file;
                    
                    _fileListView.Items.Add(item);
                    
                    // Hide drag-drop hint when files are added
                    if (_fileListView.Items.Count > 0)
                        _dragDropLabel.Visible = false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error adding file {Path.GetFileName(file)}: {ex.Message}", 
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        
        private string ShortenPath(string fullPath)
        {
            try
            {
                var parts = fullPath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                
                // If path has 4 or fewer parts, return as-is
                if (parts.Length <= 4)
                    return fullPath;
                
                // Take last 4 parts (3 folders + filename)
                var lastParts = parts.TakeLast(4);
                return "...\\" + string.Join("\\", lastParts);
            }
            catch
            {
                return fullPath;
            }
        }
        
        private (int width, int height) ReadTGADimensions(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);
            
            // TGA header is 18 bytes
            // Width is at offset 12 (2 bytes, little endian)
            // Height is at offset 14 (2 bytes, little endian)
            fs.Seek(12, SeekOrigin.Begin);
            int width = br.ReadUInt16();
            int height = br.ReadUInt16();
            
            return (width, height);
        }
        
        private void RemoveSelectedButton_Click(object? sender, EventArgs e)
        {
            foreach (ListViewItem item in _fileListView.SelectedItems)
            {
                if (item.Tag is string filePath)
                    _queuedFiles.Remove(filePath);
                _fileListView.Items.Remove(item);
            }
            
            // Refresh to fix alternating row colors
            _fileListView.Invalidate();
            
            // Show drag-drop hint if no files remain
            if (_fileListView.Items.Count == 0)
                _dragDropLabel.Visible = true;
        }
        
        private void ClearAllButton_Click(object? sender, EventArgs e)
        {
            _queuedFiles.Clear();
            _fileListView.Items.Clear();
            _dragDropLabel.Visible = true;
            
            // Hide and reset progress UI
            _progressBar.Visible = false;
            _progressBar.Value = 0;
            _progressPercentLabel.Visible = false;
            _progressPercentLabel.Text = "";
            _statusLabel.Visible = false;
            _statusLabel.Text = "";
            _spaceSavingsLabel.Visible = false;
            _spaceSavingsLabel.Text = "";
        }
        
        private void UseOriginalSizeCheckBox_CheckedChanged(object? sender, EventArgs e)
        {
            bool useOriginal = _useOriginalSizeCheckBox.Checked;
            _widthNumeric.Enabled = !useOriginal;
            _heightNumeric.Enabled = !useOriginal;
            _maintainAspectCheckBox.Enabled = !useOriginal;
        }
        
        private void WidthNumeric_ValueChanged(object? sender, EventArgs e)
        {
            if (_maintainAspectCheckBox.Checked && !_useOriginalSizeCheckBox.Checked)
            {
                // Update height to maintain aspect ratio (assume 1:1 for now)
                _heightNumeric.ValueChanged -= HeightNumeric_ValueChanged;
                _heightNumeric.Value = _widthNumeric.Value;
                _heightNumeric.ValueChanged += HeightNumeric_ValueChanged;
            }
        }
        
        private void HeightNumeric_ValueChanged(object? sender, EventArgs e)
        {
            if (_maintainAspectCheckBox.Checked && !_useOriginalSizeCheckBox.Checked)
            {
                // Update width to maintain aspect ratio (assume 1:1 for now)
                _widthNumeric.ValueChanged -= WidthNumeric_ValueChanged;
                _widthNumeric.Value = _heightNumeric.Value;
                _widthNumeric.ValueChanged += WidthNumeric_ValueChanged;
            }
        }
        
        private void QualitySlider_ValueChanged(object? sender, EventArgs e)
        {
            int quality = _qualitySlider.Value;
            _qualityLabel.Text = $"{quality}%";
            
            if (quality >= 90)
                _qualityLabel.ForeColor = Color.LimeGreen;
            else if (quality >= 70)
                _qualityLabel.ForeColor = Color.Yellow;
            else if (quality >= 50)
                _qualityLabel.ForeColor = Color.Orange;
            else
                _qualityLabel.ForeColor = Color.Red;
        }
        
        private void FormatComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            // Only show quality slider for JPG
            bool isJpg = _formatComboBox.SelectedIndex == 1;
            _qualitySlider.Visible = isJpg;
            _qualityLabel.Visible = isJpg;
        }
        
        private async void ConvertButton_Click(object? sender, EventArgs e)
        {
            if (_isConverting)
                return;
            
            if (_queuedFiles.Count == 0)
            {
                MessageBox.Show("No files in queue", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            LogToConsole($"[TGA Converter] Starting conversion of {_queuedFiles.Count} file(s)");
            
            _isConverting = true;
            _convertButton.Enabled = false;
            _addFilesButton.Enabled = false;
            _progressBar.Visible = true;
            _progressPercentLabel.Visible = true;
            _statusLabel.Visible = true;
            _progressBar.Maximum = _queuedFiles.Count;
            _progressBar.Value = 0;
            _progressPercentLabel.Text = "0%";
            
            await Task.Run(() => ConvertFiles());
            
            _isConverting = false;
            _convertButton.Enabled = true;
            _addFilesButton.Enabled = true;
            
            // Ensure progress bar is at 100%
            _progressBar.Value = _progressBar.Maximum;
            
            // Update status to show completion and keep progress bar visible
            _statusLabel.Text = $"Completed! {_queuedFiles.Count}/{_queuedFiles.Count}";
            _statusLabel.ForeColor = Color.LimeGreen;
            _progressPercentLabel.Text = "100%";
            
            // Calculate total space saved
            CalculateAndShowSpaceSavings();
            
            LogToConsole("[TGA Converter] Conversion complete!");
        }
        
        private void LogToConsole(string message)
        {
            try
            {
                // Find the main form and log to its console
                var mainForm = Application.OpenForms.OfType<MainForm>().FirstOrDefault();
                if (mainForm != null)
                {
                    mainForm.LogToConsole(message);
                }
            }
            catch { }
        }
        
        private void CalculateAndShowSpaceSavings()
        {
            long totalBefore = 0;
            long totalAfter = 0;
            
            foreach (ListViewItem item in _fileListView.Items)
            {
                // Before size from column 2 - extract just the original size if it contains (converted)
                string sizeText = item.SubItems[2].Text;
                
                // If it contains parentheses, extract the original size before the (
                if (sizeText.Contains("("))
                {
                    int parenIndex = sizeText.IndexOf('(');
                    sizeText = sizeText.Substring(0, parenIndex).Trim();
                }
                
                if (long.TryParse(sizeText, out long beforeKB))
                    totalBefore += beforeKB;
                
                // After size: check if converted successfully
                if (item.SubItems[3].Text == "Converted" && item.Tag is string filePath)
                {
                    try
                    {
                        string fileName = Path.GetFileNameWithoutExtension(filePath);
                        string outputFolder = Path.GetDirectoryName(filePath) ?? "";
                        
                        // Try to find the converted file (PNG or JPG)
                        string pngFile = Path.Combine(outputFolder, $"{fileName}.png");
                        string jpgFile = Path.Combine(outputFolder, $"{fileName}.jpg");
                        
                        if (File.Exists(pngFile))
                        {
                            totalAfter += new FileInfo(pngFile).Length / 1024;
                        }
                        else if (File.Exists(jpgFile))
                        {
                            totalAfter += new FileInfo(jpgFile).Length / 1024;
                        }
                    }
                    catch { }
                }
            }
            
            long saved = totalBefore - totalAfter;
            double percentReduction = totalBefore > 0 ? (saved * 100.0 / totalBefore) : 0;
            
            if (saved > 0)
            {
                _spaceSavingsLabel.Text = $"Space saved: {saved:N0} KB ({percentReduction:F1}% reduction)";
                _spaceSavingsLabel.ForeColor = Color.LimeGreen;
            }
            else if (saved < 0)
            {
                _spaceSavingsLabel.Text = $"Space increased: {Math.Abs(saved):N0} KB ({Math.Abs(percentReduction):F1}% larger)";
                _spaceSavingsLabel.ForeColor = Color.OrangeRed;
            }
            else
            {
                _spaceSavingsLabel.Text = "No space change";
                _spaceSavingsLabel.ForeColor = Color.Gray;
            }
            
            _spaceSavingsLabel.Visible = true;
        }
        
        private void ConvertFiles()
        {
            string format = "";
            bool useOriginalSize = false;
            int targetWidth = 0;
            int targetHeight = 0;
            int quality = 100;
            
            Invoke((MethodInvoker)delegate
            {
                format = _formatComboBox.SelectedItem?.ToString() ?? "PNG";
                useOriginalSize = _useOriginalSizeCheckBox.Checked;
                targetWidth = (int)_widthNumeric.Value;
                targetHeight = (int)_heightNumeric.Value;
                quality = _qualitySlider.Value;
            });
            
            _convertedFiles.Clear(); // Clear previous conversions
            
            for (int i = 0; i < _queuedFiles.Count; i++)
            {
                string inputFile = _queuedFiles[i];
                string fileName = Path.GetFileNameWithoutExtension(inputFile);
                string extension = format.ToLowerInvariant();
                string outputFolder = Path.GetDirectoryName(inputFile) ?? "";
                string outputFile = Path.Combine(outputFolder, $"{fileName}.{extension}");
                
                Invoke((MethodInvoker)delegate
                {
                    string shortFileName = Path.GetFileName(inputFile);
                    _statusLabel.Text = $"Converting {shortFileName}... ({i + 1}/{_queuedFiles.Count})";
                    _fileListView.Items[i].SubItems[3].Text = "Converting...";
                    _fileListView.Items[i].SubItems[3].ForeColor = Color.Yellow;
                    LogToConsole($"[TGA Converter] Converting {shortFileName} ({i + 1}/{_queuedFiles.Count})");
                });
                
                try
                {
                    using var tgaImage = LoadTGA(inputFile);
                    Bitmap outputImage;
                    
                    if (useOriginalSize)
                    {
                        outputImage = tgaImage;
                    }
                    else
                    {
                        outputImage = new Bitmap(targetWidth, targetHeight);
                        using var g = Graphics.FromImage(outputImage);
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.DrawImage(tgaImage, 0, 0, targetWidth, targetHeight);
                    }
                    
                    ImageFormat imageFormat = format == "PNG" ? ImageFormat.Png : ImageFormat.Jpeg;
                    
                    if (format == "JPG")
                    {
                        SaveJpeg(outputImage, outputFile, quality);
                    }
                    else
                    {
                        outputImage.Save(outputFile, ImageFormat.Png);
                    }
                    
                    if (!useOriginalSize && outputImage != tgaImage)
                        outputImage.Dispose();
                    
                    // Track conversion for vmat updates
                    _convertedFiles[inputFile] = outputFile;
                    
                    // Force a small delay and ensure file is flushed to disk
                    System.Threading.Thread.Sleep(10);
                    
                    // Get the actual output file size
                    long outputSize = 0;
                    int retries = 3;
                    while (retries > 0 && outputSize == 0)
                    {
                        if (File.Exists(outputFile))
                        {
                            try
                            {
                                outputSize = new FileInfo(outputFile).Length / 1024;
                                if (outputSize == 0)
                                {
                                    System.Threading.Thread.Sleep(10);
                                    retries--;
                                }
                            }
                            catch
                            {
                                System.Threading.Thread.Sleep(10);
                                retries--;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                    
                    Invoke((MethodInvoker)delegate
                    {
                        // Update status column
                        _fileListView.Items[i].SubItems[3].Text = "Converted";
                        _fileListView.Items[i].SubItems[3].ForeColor = Color.LimeGreen;
                        
                        // Update size column to show original and converted size
                        string originalSizeText = _fileListView.Items[i].SubItems[2].Text;
                        _fileListView.Items[i].SubItems[2].Text = $"{originalSizeText} ({outputSize})";
                        // Note: We can't directly color part of the text, so we'll handle it in DrawSubItem
                        
                        _progressBar.Value = i + 1;
                        int percent = (int)(((i + 1) * 100.0) / _queuedFiles.Count);
                        _progressPercentLabel.Text = $"{percent}%";
                        LogToConsole($"[TGA Converter] ✓ {Path.GetFileName(inputFile)} -> {outputSize} KB");
                    });
                }
                catch (Exception ex)
                {
                    Invoke((MethodInvoker)delegate
                    {
                        _fileListView.Items[i].SubItems[3].Text = $"Error: {ex.Message}";
                        _fileListView.Items[i].SubItems[3].ForeColor = Color.Red;
                        LogToConsole($"[TGA Converter] ✗ Error converting {Path.GetFileName(inputFile)}: {ex.Message}");
                    });
                }
            }
            
            // Update vmat files after conversion is complete
            Invoke((MethodInvoker)delegate
            {
                UpdateVmatFiles();
            });
        }
        
        private Bitmap LoadTGA(string filePath)
        {
            using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);
            
            // Read TGA header
            byte idLength = br.ReadByte();
            byte colorMapType = br.ReadByte();
            byte imageType = br.ReadByte();
            
            // Color map specification
            br.ReadUInt16(); // First entry index
            br.ReadUInt16(); // Color map length
            br.ReadByte();   // Color map entry size
            
            // Image specification
            br.ReadUInt16(); // X origin
            br.ReadUInt16(); // Y origin
            ushort width = br.ReadUInt16();
            ushort height = br.ReadUInt16();
            byte pixelDepth = br.ReadByte();
            byte imageDescriptor = br.ReadByte();
            
            // Skip image ID
            fs.Seek(idLength, SeekOrigin.Current);
            
            // Determine image type - support uncompressed (2, 3) and RLE compressed (10, 11)
            bool isRLE = (imageType == 10 || imageType == 11);
            bool isGrayscale = (imageType == 3 || imageType == 11);
            bool isColorMapped = (imageType == 1 || imageType == 9);
            
            if (imageType == 0)
                throw new NotSupportedException("TGA type 0 (no image data) is not supported.");
            if (isColorMapped)
                throw new NotSupportedException($"Color-mapped TGA (type {imageType}) is not currently supported.");
            if (imageType != 1 && imageType != 2 && imageType != 3 && imageType != 9 && imageType != 10 && imageType != 11)
                throw new NotSupportedException($"Unknown TGA type: {imageType}");
            
            int bytesPerPixel = pixelDepth / 8;
            
            if (isGrayscale)
            {
                // Type 3/11: 8-bit grayscale or 16-bit grayscale with alpha
                if (bytesPerPixel != 1 && bytesPerPixel != 2)
                    throw new NotSupportedException($"Unsupported grayscale pixel depth: {pixelDepth}-bit. Only 8-bit and 16-bit grayscale TGA files are supported.");
            }
            else
            {
                // Type 2/10: RGB/RGBA
                if (bytesPerPixel != 3 && bytesPerPixel != 4)
                    throw new NotSupportedException($"Unsupported pixel depth: {pixelDepth}-bit. Only 24-bit RGB and 32-bit RGBA TGA files are supported.");
            }
            
            // Create bitmap - grayscale will be converted to RGB for compatibility
            PixelFormat pixelFormat;
            if (isGrayscale)
            {
                pixelFormat = bytesPerPixel == 2 ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb;
            }
            else
            {
                pixelFormat = bytesPerPixel == 4 ? PixelFormat.Format32bppArgb : PixelFormat.Format24bppRgb;
            }
            
            var bitmap = new Bitmap(width, height, pixelFormat);
            var bitmapData = bitmap.LockBits(new Rectangle(0, 0, width, height), 
                ImageLockMode.WriteOnly, bitmap.PixelFormat);
            
            try
            {
                byte[] imageData;
                
                if (isRLE)
                {
                    // Decompress RLE data
                    imageData = DecompressRLE(br, width * height * bytesPerPixel, bytesPerPixel);
                }
                else
                {
                    // Read uncompressed data
                    imageData = br.ReadBytes(width * height * bytesPerPixel);
                }
                
                unsafe
                {
                    byte* ptr = (byte*)bitmapData.Scan0;
                    int stride = bitmapData.Stride;
                    
                    // Bit 5 of imageDescriptor indicates origin: 0 = bottom-left, 1 = top-left
                    bool originAtTop = (imageDescriptor & 0x20) != 0;
                    
                    for (int y = 0; y < height; y++)
                    {
                        // Flip if origin is at bottom (traditional TGA), don't flip if origin is at top
                        int actualY = originAtTop ? y : (height - 1 - y);
                        byte* row = ptr + (actualY * stride);
                        
                        for (int x = 0; x < width; x++)
                        {
                            int offset = (y * width + x) * bytesPerPixel;
                            
                            byte r, g, b, a;
                            
                            if (isGrayscale)
                            {
                                // Grayscale: replicate gray value to RGB channels
                                byte gray = imageData[offset];
                                r = g = b = gray;
                                a = bytesPerPixel == 2 ? imageData[offset + 1] : (byte)255;
                            }
                            else
                            {
                                // RGB/RGBA: TGA stores as BGR(A)
                                b = imageData[offset];
                                g = imageData[offset + 1];
                                r = imageData[offset + 2];
                                a = bytesPerPixel == 4 ? imageData[offset + 3] : (byte)255;
                            }
                            
                            int outputBytesPerPixel = (pixelFormat == PixelFormat.Format32bppArgb) ? 4 : 3;
                            int pixelOffset = x * outputBytesPerPixel;
                            row[pixelOffset] = b;
                            row[pixelOffset + 1] = g;
                            row[pixelOffset + 2] = r;
                            if (outputBytesPerPixel == 4)
                                row[pixelOffset + 3] = a;
                        }
                    }
                }
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }
            
            return bitmap;
        }
        
        private byte[] DecompressRLE(BinaryReader br, int expectedSize, int bytesPerPixel)
        {
            var result = new List<byte>();
            
            while (result.Count < expectedSize)
            {
                byte packetHeader = br.ReadByte();
                int packetType = (packetHeader & 0x80) >> 7; // Bit 7: 1 = RLE, 0 = Raw
                int pixelCount = (packetHeader & 0x7F) + 1;   // Bits 0-6: count - 1
                
                if (packetType == 1)
                {
                    // RLE packet: repeat next pixel N times
                    byte[] pixel = br.ReadBytes(bytesPerPixel);
                    for (int i = 0; i < pixelCount; i++)
                    {
                        result.AddRange(pixel);
                    }
                }
                else
                {
                    // Raw packet: read N pixels as-is
                    byte[] pixels = br.ReadBytes(pixelCount * bytesPerPixel);
                    result.AddRange(pixels);
                }
            }
            
            return result.ToArray();
        }
        
        private void SaveJpeg(Bitmap image, string outputFile, int quality)
        {
            var encoderParams = new EncoderParameters(1);
            encoderParams.Param[0] = new EncoderParameter(Encoder.Quality, (long)quality);
            
            var jpegCodec = ImageCodecInfo.GetImageEncoders()
                .First(codec => codec.FormatID == ImageFormat.Jpeg.Guid);
            
            image.Save(outputFile, jpegCodec, encoderParams);
        }
        
        private void ListView_ColumnClick(object? sender, ColumnClickEventArgs e)
        {
            if (_isConverting) return; // Don't sort while converting
            
            // Toggle sort order if clicking the same column
            if (e.Column == _sortColumn)
            {
                _sortAscending = !_sortAscending;
            }
            else
            {
                _sortColumn = e.Column;
                _sortAscending = true;
            }
            
            SortListView();
        }
        
        private void SortListView()
        {
            if (_sortColumn < 0 || _fileListView.Items.Count == 0) return;
            
            var items = new List<ListViewItem>();
            for (int i = 0; i < _fileListView.Items.Count; i++)
            {
                items.Add(_fileListView.Items[i]);
            }
            
            items.Sort((a, b) =>
            {
                string valA = a.SubItems[_sortColumn].Text;
                string valB = b.SubItems[_sortColumn].Text;
                
                int result = 0;
                
                // Column 1 (Resolution) and Column 2 (Size) are numeric
                if (_sortColumn == 1 || _sortColumn == 2)
                {
                    // Extract numeric part for resolution (e.g., "1024x1024" -> compare by first number)
                    if (_sortColumn == 1)
                    {
                        int widthA = int.TryParse(valA.Split('x')[0], out int wa) ? wa : 0;
                        int widthB = int.TryParse(valB.Split('x')[0], out int wb) ? wb : 0;
                        result = widthA.CompareTo(widthB);
                    }
                    else // Size (KB)
                    {
                        int sizeA = int.TryParse(valA, out int sa) ? sa : 0;
                        int sizeB = int.TryParse(valB, out int sb) ? sb : 0;
                        result = sizeA.CompareTo(sizeB);
                    }
                }
                else
                {
                    result = string.Compare(valA, valB, StringComparison.OrdinalIgnoreCase);
                }
                
                return _sortAscending ? result : -result;
            });
            
            _fileListView.Items.Clear();
            _fileListView.Items.AddRange(items.ToArray());
        }
        
        private void ListView_DrawColumnHeader(object? sender, DrawListViewColumnHeaderEventArgs e)
        {
            e.Graphics.FillRectangle(new SolidBrush(Color.FromArgb(35, 35, 35)), e.Bounds);
            
            // Draw sort indicator
            string sortIndicator = "";
            if (e.ColumnIndex == _sortColumn)
            {
                sortIndicator = _sortAscending ? " ▲" : " ▼";
            }
            
            TextRenderer.DrawText(e.Graphics, (e.Header?.Text ?? "") + sortIndicator, _fileListView.Font,
                e.Bounds, Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left);
        }
        
        private void ListView_DrawSubItem(object? sender, DrawListViewSubItemEventArgs e)
        {
            if (e.Item == null || e.SubItem == null) return;
            
            // Determine background color for this row
            Color backColor = Color.FromArgb(45, 45, 45);
            if (e.Item.Selected && _fileListView.Focused)
            {
                backColor = Color.FromArgb(51, 122, 204);
            }
            else if (e.ItemIndex % 2 == 1)
            {
                backColor = Color.FromArgb(40, 40, 40);
            }
            
            // Draw background for this cell
            e.Graphics.FillRectangle(new SolidBrush(backColor), e.Bounds);
            
            // Special handling for Size (KB) column (column 2) to show converted size in green
            if (e.ColumnIndex == 2 && e.SubItem.Text.Contains("("))
            {
                // Split text into original size and converted size
                int openParen = e.SubItem.Text.IndexOf('(');
                string originalPart = e.SubItem.Text.Substring(0, openParen).TrimEnd();
                string convertedPart = e.SubItem.Text.Substring(openParen); // "(XXX)"
                
                // Draw original size in white
                var originalSize = TextRenderer.MeasureText(e.Graphics, originalPart + " ", _fileListView.Font);
                TextRenderer.DrawText(e.Graphics, originalPart + " ", _fileListView.Font,
                    e.Bounds, Color.White, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
                
                // Draw converted size in green
                Rectangle greenBounds = new Rectangle(e.Bounds.X + originalSize.Width, e.Bounds.Y, 
                    e.Bounds.Width - originalSize.Width, e.Bounds.Height);
                TextRenderer.DrawText(e.Graphics, convertedPart, _fileListView.Font,
                    greenBounds, Color.LimeGreen, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
            }
            else
            {
                // Draw text with normal color
                Color textColor = e.SubItem.ForeColor != Color.Empty ? e.SubItem.ForeColor : Color.White;
                
                TextRenderer.DrawText(e.Graphics, e.SubItem.Text, _fileListView.Font,
                    e.Bounds, textColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
            }
        }
        
        private void AutoDetectCS2()
        {
            Task.Run(() =>
            {
                try
                {
                    _cs2Path = GetCs2Path();
                    if (!string.IsNullOrEmpty(_cs2Path) && Directory.Exists(_cs2Path))
                    {
                        LogToConsole($"[TGA Converter] Auto-detected CS2: {_cs2Path}");
                        
                        var addonsPath = Path.Combine(_cs2Path, "content", "csgo_addons");
                        LogToConsole($"[TGA Converter] Looking for addons in: {addonsPath}");
                        
                        if (Directory.Exists(addonsPath))
                        {
                            _addonList = Directory.GetDirectories(addonsPath)
                                .Select(d => Path.GetFileName(d))
                                .Where(name => !string.IsNullOrEmpty(name) && 
                                              !name.Equals("addon_template", StringComparison.OrdinalIgnoreCase) &&
                                              !name.Equals("addontemplate", StringComparison.OrdinalIgnoreCase))
                                .ToArray();
                            
                            if (_addonList.Length > 0)
                            {
                                LogToConsole($"[TGA Converter] Found {_addonList.Length} addon(s): {string.Join(", ", _addonList)}");
                                Invoke(new Action(() =>
                                {
                                    _addonComboBox.Items.Clear();
                                    _addonComboBox.Items.AddRange(_addonList);
                                    if (_addonList.Length > 0)
                                    {
                                        _addonComboBox.SelectedIndex = 0;
                                    }
                                }));
                            }
                            else
                            {
                                LogToConsole("[TGA Converter] No addons found in directory. You can still add files manually.");
                            }
                        }
                        else
                        {
                            LogToConsole($"[TGA Converter] Addons directory does not exist: {addonsPath}");
                            LogToConsole("[TGA Converter] You can still add files manually.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogToConsole($"[TGA Converter] Error detecting CS2: {ex.Message}");
                }
            });
        }
        
        private string? GetCs2Path()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (key?.GetValue("SteamPath") is string steamPath)
                {
                    var libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                    if (!File.Exists(libraryFoldersPath))
                        libraryFoldersPath = Path.Combine(steamPath, "SteamApps", "libraryfolders.vdf");
                    
                    var libraryPath = FindCs2LibraryPath(libraryFoldersPath);
                    if (!string.IsNullOrEmpty(libraryPath))
                    {
                        var cs2Path = Path.Combine(libraryPath, "steamapps", "common", "Counter-Strike Global Offensive");
                        if (!Directory.Exists(cs2Path))
                            cs2Path = Path.Combine(libraryPath, "SteamApps", "common", "Counter-Strike Global Offensive");
                        
                        if (Directory.Exists(cs2Path))
                            return cs2Path;
                    }
                }
            }
            catch { }
            return null;
        }
        
        private string? FindCs2LibraryPath(string libraryFoldersPath)
        {
            try
            {
                if (!File.Exists(libraryFoldersPath))
                    return null;
                
                var content = File.ReadAllText(libraryFoldersPath);
                var pathRegex = new Regex(@"""path""\s+""([^""]+)""");
                var appsRegex = new Regex(@"""apps""[^{]*{[^}]*""730""");
                
                var matches = pathRegex.Matches(content);
                var sections = content.Split(new[] { "\"" }, StringSplitOptions.None);
                
                for (int i = 0; i < matches.Count; i++)
                {
                    var path = matches[i].Groups[1].Value.Replace("\\\\", "\\");
                    var sectionStart = matches[i].Index;
                    var nextSectionStart = i < matches.Count - 1 ? matches[i + 1].Index : content.Length;
                    var section = content.Substring(sectionStart, nextSectionStart - sectionStart);
                    
                    if (appsRegex.IsMatch(section))
                        return path;
                }
            }
            catch { }
            return null;
        }
        
        private void AddAddonButton_Click(object? sender, EventArgs e)
        {
            if (_addonComboBox.SelectedIndex < 0 || string.IsNullOrEmpty(_cs2Path))
                return;
            
            var selectedAddon = _addonComboBox.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedAddon))
                return;
            
            var addonPath = Path.Combine(_cs2Path, "content", "csgo_addons", selectedAddon);
            if (!Directory.Exists(addonPath))
            {
                MessageBox.Show($"Addon path not found: {addonPath}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            
            LogToConsole($"[TGA Converter] Scanning addon: {selectedAddon}");
            
            var tgaFiles = Directory.GetFiles(addonPath, "*.tga", SearchOption.AllDirectories).ToList();
            
            if (tgaFiles.Count == 0)
            {
                MessageBox.Show($"No TGA files found in addon: {selectedAddon}", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            
            LogToConsole($"[TGA Converter] Found {tgaFiles.Count} TGA file(s) in {selectedAddon}");
            AddFilesToQueue(tgaFiles.ToArray());
        }
        
        private void UpdateVmatFiles()
        {
            if (!_updateVmatsCheckBox.Checked || _convertedFiles.Count == 0)
                return;
            
            LogToConsole($"[TGA Converter] Updating .vmat files...");
            int updatedCount = 0;
            
            foreach (var kvp in _convertedFiles)
            {
                string tgaPath = kvp.Key;
                string convertedPath = kvp.Value;
                
                try
                {
                    // Get the directory containing the TGA file
                    string? tgaDir = Path.GetDirectoryName(tgaPath);
                    if (string.IsNullOrEmpty(tgaDir))
                        continue;
                    
                    // Find the addon root directory (content/csgo_addons/[addon_name])
                    string? addonRoot = tgaDir;
                    while (!string.IsNullOrEmpty(addonRoot))
                    {
                        string? parentDir = Path.GetDirectoryName(addonRoot);
                        if (string.IsNullOrEmpty(parentDir))
                            break;
                        
                        // Check if parent is csgo_addons
                        if (Path.GetFileName(parentDir) == "csgo_addons")
                            break;
                        
                        addonRoot = parentDir;
                    }
                    
                    // Search for vmat files only within the addon root directory
                    var vmatFiles = Directory.GetFiles(addonRoot, "*.vmat", SearchOption.AllDirectories).ToList();
                    
                    // Get relative path format for the TGA file (materials/...)
                    string tgaFileName = Path.GetFileNameWithoutExtension(tgaPath);
                    string convertedFileName = Path.GetFileNameWithoutExtension(convertedPath);
                    string convertedExt = Path.GetExtension(convertedPath);
                    
                    foreach (var vmatFile in vmatFiles.Distinct())
                    {
                        try
                        {
                            string content = File.ReadAllText(vmatFile);
                            bool modified = false;
                            
                            // Replace references to the TGA file with the converted file
                            // Pattern: "materials/path/to/file.tga" -> "materials/path/to/file.png"
                            var pattern = $@"({tgaFileName})\.tga";
                            var replacement = $"$1{convertedExt}";
                            
                            if (Regex.IsMatch(content, pattern, RegexOptions.IgnoreCase))
                            {
                                content = Regex.Replace(content, pattern, replacement, RegexOptions.IgnoreCase);
                                File.WriteAllText(vmatFile, content);
                                modified = true;
                                updatedCount++;
                                LogToConsole($"[TGA Converter] Updated: {Path.GetFileName(vmatFile)}");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogToConsole($"[TGA Converter] Error updating {Path.GetFileName(vmatFile)}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogToConsole($"[TGA Converter] Error processing {Path.GetFileName(tgaPath)}: {ex.Message}");
                }
            }
            
            if (updatedCount > 0)
                LogToConsole($"[TGA Converter] Updated {updatedCount} .vmat file(s)");
            else
                LogToConsole($"[TGA Converter] No .vmat files needed updating");
        }
    }
}
