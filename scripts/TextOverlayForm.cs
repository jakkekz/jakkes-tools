using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

#nullable enable

namespace CS2KZMappingTools
{
    public partial class TextOverlayForm : Form
    {
        private readonly ThemeManager _themeManager;
        public event Action<string>? LogMessage;
        
        private ComboBox? _shaderTypeComboBox;
        private RadioButton? _textRadio;
        private RadioButton? _ljNumbersRadio;
        private Panel? _ljNumbersPanel;
        private NumericUpDown? _minNumberInput;
        private NumericUpDown? _maxNumberInput;
        private NumericUpDown? _incrementInput;
        private Label? _textLabel;
        private TextBox? _textInput;
        private ComboBox? _fontComboBox;
        private ComboBox? _alignmentComboBox;
        private ComboBox? _resolutionComboBox;
        private ComboBox? _outputLocationComboBox;
        private ComboBox? _addonComboBox;
        private TextBox? _customPathInput;
        private Button? _browseButton;
        private Button? _generateButton;
        private Button? _closeButton;
        private ProgressBar? _progressBar;
        private Label? _statusLabel;
        private PictureBox? _previewPictureBox;
        
        private string _cs2Path = "";

        public TextOverlayForm(ThemeManager themeManager)
        {
            _themeManager = themeManager;
            InitializeComponent();
            FindCs2Path();
            PopulateAddonDropdown();
            PopulateFontComboBox();
            ApplyTheme();
        }

        private void ApplyTheme()
        {
            var theme = _themeManager.GetCurrentTheme();
            
            // Apply theme to form
            BackColor = theme.WindowBackground;
            ForeColor = theme.Text;

            // Apply theme to controls
            foreach (Control control in Controls)
            {
                ApplyThemeToControl(control);
            }
        }

        private void ApplyThemeToControl(Control control)
        {
            var theme = _themeManager.GetCurrentTheme();
            
            if (control is TextBox textBox)
            {
                textBox.BackColor = theme.ButtonBackground;
                textBox.ForeColor = theme.Text;
            }
            else if (control is ComboBox comboBox)
            {
                comboBox.BackColor = theme.ButtonBackground;
                comboBox.ForeColor = theme.Text;
            }
            else if (control is Button button)
            {
                button.BackColor = theme.ButtonBackground;
                button.ForeColor = theme.Text;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = theme.Border;
            }
            else if (control is Label label)
            {
                label.ForeColor = theme.Text;
            }
            else if (control is NumericUpDown numericUpDown)
            {
                numericUpDown.BackColor = theme.ButtonBackground;
                numericUpDown.ForeColor = theme.Text;
            }
            else if (control is RadioButton radioButton)
            {
                radioButton.ForeColor = theme.Text;
            }
            else if (control is Panel panel)
            {
                panel.BackColor = theme.WindowBackground;
                foreach (Control child in panel.Controls)
                {
                    ApplyThemeToControl(child);
                }
            }

            // Apply recursively to child controls
            foreach (Control child in control.Controls)
            {
                ApplyThemeToControl(child);
            }
        }

        private void InitializeComponent()
        {
            this.Text = "Fuck point_worldtext";
            this.Size = new Size(520, 760);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Padding = new Padding(20);
            this.Font = new Font("Segoe UI", 9F);

            int yPos = 0;
            int labelWidth = 120;
            int controlWidth = 350;

            // Title
            var titleLabel = new Label
            {
                Text = "Fuck point_worldtext",
                Location = new Point(0, yPos),
                Size = new Size(480, 30),
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };
            yPos += 35;

            // Shader Type
            var shaderLabel = new Label
            {
                Text = "Shader Type:",
                Location = new Point(0, yPos),
                Size = new Size(labelWidth, 25),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _shaderTypeComboBox = new ComboBox
            {
                Location = new Point(labelWidth, yPos),
                Size = new Size(controlWidth, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F)
            };
            _shaderTypeComboBox.Items.AddRange(new object[] { "Csgo Static Overlay", "Csgo Complex" });
            _shaderTypeComboBox.SelectedIndex = 0; // Default to Static Overlay
            yPos += 30;

            // Content Type
            var contentTypeLabel = new Label
            {
                Text = "Content Type:",
                Location = new Point(0, yPos),
                Size = new Size(labelWidth, 25),
                TextAlign = ContentAlignment.MiddleLeft
            };

            var contentTypePanel = new Panel
            {
                Location = new Point(labelWidth, yPos),
                Size = new Size(controlWidth, 25)
            };

            _textRadio = new RadioButton
            {
                Text = "Text",
                Location = new Point(0, 0),
                Size = new Size(100, 25),
                Checked = true
            };
            _textRadio.CheckedChanged += ContentType_CheckedChanged;

            _ljNumbersRadio = new RadioButton
            {
                Text = "LJ Numbers",
                Location = new Point(120, 0),
                Size = new Size(120, 25)
            };
            _ljNumbersRadio.CheckedChanged += ContentType_CheckedChanged;

            contentTypePanel.Controls.Add(_textRadio);
            contentTypePanel.Controls.Add(_ljNumbersRadio);
            yPos += 30;

            // Store the yPos for text input and LJ numbers panel (they share the same position)
            int textContentYPos = yPos;

            // Text Input
            _textLabel = new Label
            {
                Text = "Text:",
                Location = new Point(0, textContentYPos),
                Size = new Size(labelWidth, 25),
                TextAlign = ContentAlignment.TopLeft
            };

            _textInput = new TextBox
            {
                Location = new Point(labelWidth, textContentYPos),
                Size = new Size(controlWidth, 60),
                Font = new Font("Segoe UI", 9F),
                Text = "Sample Text",
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                AcceptsReturn = true
            };
            _textInput.TextChanged += (s, e) => UpdatePreview();

            // LJ Numbers Panel (at the same position as text input)
            _ljNumbersPanel = new Panel
            {
                Location = new Point(0, textContentYPos),
                Size = new Size(480, 65),
                Visible = false
            };

            var rangeLabel = new Label
            {
                Text = "Range:",
                Location = new Point(0, 0),
                Size = new Size(labelWidth, 25),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _minNumberInput = new NumericUpDown
            {
                Location = new Point(labelWidth, 0),
                Size = new Size(100, 25),
                Minimum = 0,
                Maximum = 9999,
                Value = 210
            };

            var dashLabel = new Label
            {
                Text = "-",
                Location = new Point(labelWidth + 105, 0),
                Size = new Size(15, 25),
                TextAlign = ContentAlignment.MiddleCenter
            };

            _maxNumberInput = new NumericUpDown
            {
                Location = new Point(labelWidth + 125, 0),
                Size = new Size(100, 25),
                Minimum = 0,
                Maximum = 9999,
                Value = 280
            };

            var incrementLabel = new Label
            {
                Text = "Increment:",
                Location = new Point(0, 32),
                Size = new Size(labelWidth, 25),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _incrementInput = new NumericUpDown
            {
                Location = new Point(labelWidth, 32),
                Size = new Size(controlWidth, 25),
                Minimum = 1,
                Maximum = 100,
                Value = 2
            };

            _ljNumbersPanel.Controls.AddRange(new Control[] {
                rangeLabel, _minNumberInput, dashLabel, _maxNumberInput,
                incrementLabel, _incrementInput
            });

            // Advance yPos - text input takes 65px, LJ panel takes 65px
            yPos = textContentYPos + 70;

            // Text Alignment
            var alignmentLabel = new Label
            {
                Text = "Alignment:",
                Location = new Point(0, yPos),
                Size = new Size(labelWidth, 25),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _alignmentComboBox = new ComboBox
            {
                Location = new Point(labelWidth, yPos),
                Size = new Size(controlWidth, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F)
            };
            _alignmentComboBox.Items.AddRange(new object[] { "Left", "Center", "Right" });
            _alignmentComboBox.SelectedIndex = 1; // Default to Center
            _alignmentComboBox.SelectedIndexChanged += (s, e) => UpdatePreview();
            yPos += 30;

            // Font Selection
            var fontLabel = new Label
            {
                Text = "Font:",
                Location = new Point(0, yPos),
                Size = new Size(labelWidth, 25),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _fontComboBox = new ComboBox
            {
                Location = new Point(labelWidth, yPos),
                Size = new Size(controlWidth, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F),
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 20
            };
            _fontComboBox.DrawItem += FontComboBox_DrawItem;
            _fontComboBox.SelectedIndexChanged += (s, e) => UpdatePreview();
            yPos += 30;

            // Resolution
            var resolutionLabel = new Label
            {
                Text = "Resolution:",
                Location = new Point(0, yPos),
                Size = new Size(labelWidth, 25),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _resolutionComboBox = new ComboBox
            {
                Location = new Point(labelWidth, yPos),
                Size = new Size(controlWidth, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F)
            };
            _resolutionComboBox.Items.AddRange(new object[] { 
                "256x256", "512x512", "1024x1024", "2048x2048", "4096x4096" 
            });
            _resolutionComboBox.SelectedIndex = 1; // Default to 512x512
            yPos += 30;

            // Preview
            var previewLabel = new Label
            {
                Text = "Preview:",
                Location = new Point(0, yPos),
                Size = new Size(labelWidth, 25),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _previewPictureBox = new PictureBox
            {
                Location = new Point(labelWidth, yPos),
                Size = new Size(250, 250),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.Black,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            yPos += 260;

            // Output Location
            var locationLabel = new Label
            {
                Text = "Output Location:",
                Location = new Point(0, yPos),
                Size = new Size(labelWidth, 25),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _outputLocationComboBox = new ComboBox
            {
                Location = new Point(labelWidth, yPos),
                Size = new Size(controlWidth, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F)
            };
            _outputLocationComboBox.Items.AddRange(new object[] { "Addon", "Custom Path" });
            _outputLocationComboBox.SelectedIndex = 0; // Default to Addon
            _outputLocationComboBox.SelectedIndexChanged += OutputLocation_Changed;
            yPos += 30;

            // Addon ComboBox
            var addonLabel = new Label
            {
                Text = "Addon:",
                Location = new Point(0, yPos),
                Size = new Size(labelWidth, 25),
                TextAlign = ContentAlignment.MiddleLeft
            };

            _addonComboBox = new ComboBox
            {
                Location = new Point(labelWidth, yPos),
                Size = new Size(controlWidth, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 9F)
            };
            yPos += 30;

            // Custom Path Input
            var customPathLabel = new Label
            {
                Text = "Custom Path:",
                Location = new Point(0, yPos),
                Size = new Size(labelWidth, 25),
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = false
            };

            _customPathInput = new TextBox
            {
                Location = new Point(labelWidth, yPos),
                Size = new Size(270, 25),
                Font = new Font("Segoe UI", 9F),
                Visible = false
            };

            _browseButton = new Button
            {
                Text = "Browse...",
                Location = new Point(labelWidth + 280, yPos),
                Size = new Size(70, 25),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F),
                Visible = false
            };
            _browseButton.Click += BrowseButton_Click;
            yPos += 30;

            // Progress Bar
            _progressBar = new ProgressBar
            {
                Location = new Point(0, yPos),
                Size = new Size(480, 25),
                Style = ProgressBarStyle.Continuous,
                Visible = false
            };
            yPos += 28;

            // Status Label
            _statusLabel = new Label
            {
                Text = "Configure settings and click Generate",
                Location = new Point(0, yPos),
                Size = new Size(480, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 9F)
            };
            yPos += 28;

            // Buttons
            _generateButton = new Button
            {
                Text = "Generate",
                Location = new Point(160, yPos),
                Size = new Size(120, 35),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold)
            };
            _generateButton.Click += GenerateButton_Click;

            _closeButton = new Button
            {
                Text = "Close",
                Location = new Point(290, yPos),
                Size = new Size(100, 35),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9F)
            };
            _closeButton.Click += (s, e) => this.Close();

            // Add controls to form
            this.Controls.AddRange(new Control[] {
                titleLabel,
                shaderLabel, _shaderTypeComboBox,
                contentTypeLabel, contentTypePanel,
                _textLabel, _textInput,
                _ljNumbersPanel,
                alignmentLabel, _alignmentComboBox,
                fontLabel, _fontComboBox,
                resolutionLabel, _resolutionComboBox,
                previewLabel, _previewPictureBox,
                locationLabel, _outputLocationComboBox,
                addonLabel, _addonComboBox,
                customPathLabel, _customPathInput, _browseButton,
                _progressBar, _statusLabel,
                _generateButton, _closeButton
            });
            
            // Initial preview update
            UpdatePreview();
        }

        private void ContentType_CheckedChanged(object? sender, EventArgs e)
        {
            if (_textRadio?.Checked == true)
            {
                _textLabel!.Visible = true;
                _textInput!.Visible = true;
                _ljNumbersPanel!.Visible = false;
            }
            else if (_ljNumbersRadio?.Checked == true)
            {
                _textLabel!.Visible = false;
                _textInput!.Visible = false;
                _ljNumbersPanel!.Visible = true;
            }
        }

        private void OutputLocation_Changed(object? sender, EventArgs e)
        {
            bool isCustomPath = _outputLocationComboBox?.SelectedIndex == 1;
            
            // Find the labels and show/hide them
            foreach (Control control in this.Controls)
            {
                if (control is Label label)
                {
                    if (label.Text == "Addon:")
                    {
                        label.Visible = !isCustomPath;
                    }
                    else if (label.Text == "Custom Path:")
                    {
                        label.Visible = isCustomPath;
                    }
                }
            }
            
            _addonComboBox!.Visible = !isCustomPath;
            _customPathInput!.Visible = isCustomPath;
            _browseButton!.Visible = isCustomPath;
        }

        private void BrowseButton_Click(object? sender, EventArgs e)
        {
            using var dialog = new FolderBrowserDialog
            {
                Description = "Select output folder",
                ShowNewFolderButton = true
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                _customPathInput!.Text = dialog.SelectedPath;
            }
        }

        private async void GenerateButton_Click(object? sender, EventArgs e)
        {
            // Validate inputs
            if (_outputLocationComboBox!.SelectedIndex == 0) // Addon
            {
                if (_addonComboBox!.SelectedItem == null)
                {
                    MessageBox.Show("Please select an addon.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }
            else // Custom Path
            {
                if (string.IsNullOrWhiteSpace(_customPathInput!.Text))
                {
                    MessageBox.Show("Please select a custom path.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            if (_textRadio!.Checked && string.IsNullOrWhiteSpace(_textInput!.Text))
            {
                MessageBox.Show("Please enter text to generate.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (_ljNumbersRadio!.Checked)
            {
                if (_minNumberInput!.Value >= _maxNumberInput!.Value)
                {
                    MessageBox.Show("Maximum number must be greater than minimum number.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
            }

            _generateButton!.Enabled = false;
            _progressBar!.Visible = true;
            _progressBar.Value = 0;

            try
            {
                await GenerateTextOverlaysAsync();
                _statusLabel!.Text = "Generation completed successfully!";
                LogToEvent("Text overlay generation completed successfully!");
            }
            catch (Exception ex)
            {
                _statusLabel!.Text = "Error occurred";
                LogToEvent($"Error: {ex.Message}");
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _generateButton.Enabled = true;
                _progressBar.Visible = false;
            }
        }

        private async Task GenerateTextOverlaysAsync()
        {
            await Task.Run(() =>
            {
                // Get settings
                string shaderType = _shaderTypeComboBox!.SelectedItem!.ToString()!;
                bool isStaticOverlay = shaderType == "Csgo Static Overlay";
                int resolution = int.Parse(_resolutionComboBox!.SelectedItem!.ToString()!.Split('x')[0]);
                string fontName = _fontComboBox!.SelectedItem!.ToString()!;

                // Determine output path
                string outputPath;
                if (_outputLocationComboBox!.SelectedIndex == 0) // Addon
                {
                    string addonName = _addonComboBox!.SelectedItem!.ToString()!;
                    outputPath = Path.Combine(_cs2Path, "content", "csgo_addons", addonName, "materials", "fuckpointworldtext");
                }
                else // Custom Path
                {
                    outputPath = _customPathInput!.Text;
                }

                // Create output directory
                Directory.CreateDirectory(outputPath);

                // Generate content
                List<string> textsToGenerate = new List<string>();
                
                if (_textRadio!.Checked)
                {
                    textsToGenerate.Add(_textInput!.Text);
                }
                else // LJ Numbers
                {
                    int min = (int)_minNumberInput!.Value;
                    int max = (int)_maxNumberInput!.Value;
                    int increment = (int)_incrementInput!.Value;

                    for (int i = min; i <= max; i += increment)
                    {
                        textsToGenerate.Add(i.ToString());
                    }
                }

                int totalItems = textsToGenerate.Count;
                int currentItem = 0;

                foreach (string text in textsToGenerate)
                {
                    currentItem++;
                    
                    // Update progress
                    if (_progressBar!.InvokeRequired)
                    {
                        _progressBar.Invoke(new Action(() => {
                            _progressBar.Value = (int)((float)currentItem / totalItems * 100);
                        }));
                    }

                    // Sanitize filename
                    string filename = SanitizeFilename(text);
                    
                    LogToEvent($"Generating {filename}...");

                    // Generate texture
                    string texturePath = Path.Combine(outputPath, $"{filename}.png");
                    
                    // Get alignment
                    string alignment = _alignmentComboBox?.SelectedItem?.ToString() ?? "Center";
                    
                    GenerateTextImage(text, texturePath, resolution, fontName, alignment);

                    // Generate .vmat file
                    string vmatPath = Path.Combine(outputPath, $"{filename}.vmat");
                    string relativePath = $"materials/fuckpointworldtext/{filename}.png";
                    
                    GenerateVmatFile(vmatPath, relativePath, isStaticOverlay);

                    LogToEvent($"Generated {filename} successfully");
                }
            });
        }

        private void GenerateTextImage(string text, string texturePath, int resolution, string fontName, string alignment)
        {
            // Create main texture (white text on black background)
            using (Bitmap texture = new Bitmap(resolution, resolution))
            using (Graphics g = Graphics.FromImage(texture))
            {
                g.Clear(Color.Black);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.AntiAlias;

                // Calculate font size to fit the image
                float fontSize = resolution / 4f;
                Font font = new Font(fontName, fontSize, FontStyle.Bold);

                // Measure text to adjust font size if needed
                SizeF textSize = g.MeasureString(text, font);
                while ((textSize.Width > resolution * 0.9f || textSize.Height > resolution * 0.9f) && fontSize > 10)
                {
                    fontSize -= 5;
                    font.Dispose();
                    font = new Font(fontName, fontSize, FontStyle.Bold);
                    textSize = g.MeasureString(text, font);
                }

                // Determine alignment
                StringAlignment horizontalAlignment = alignment switch
                {
                    "Left" => StringAlignment.Near,
                    "Right" => StringAlignment.Far,
                    _ => StringAlignment.Center
                };

                // Draw text
                using (Brush brush = new SolidBrush(Color.White))
                {
                    StringFormat format = new StringFormat
                    {
                        Alignment = horizontalAlignment,
                        LineAlignment = StringAlignment.Center
                    };
                    g.DrawString(text, font, brush, new RectangleF(0, 0, resolution, resolution), format);
                }

                font.Dispose();

                // Save texture
                texture.Save(texturePath, ImageFormat.Png);
            }
        }

        private void GenerateVmatFile(string vmatPath, string texturePath, bool isStaticOverlay)
        {
            string vmatContent;

            if (isStaticOverlay)
            {
                vmatContent = $@"// THIS FILE IS AUTO-GENERATED

Layer0
{{
	shader ""csgo_static_overlay.vfx""

	//---- Blend Mode ----
	F_BLEND_MODE 1 // Translucent

	//---- Color ----
	g_flModelTintAmount ""1.000""
	g_flTexCoordRotation ""0.000""
	g_fTextureColorBrightness ""1.000""
	g_fTextureColorContrast ""1.000""
	g_fTextureColorSaturation ""1.000""
	g_nScaleTexCoordUByModelScaleAxis ""0"" // None
	g_nScaleTexCoordVByModelScaleAxis ""0"" // None
	g_vColorTint ""[1.000000 1.000000 1.000000 0.000000]""
	g_vTexCoordCenter ""[0.500 0.500]""
	g_vTexCoordOffset ""[0.000 0.000]""
	g_vTexCoordScale ""[1.000 1.000]""
	g_vTexCoordScrollSpeed ""[0.000 0.000]""
	g_vTextureColorCorrectionTint ""[1.000000 1.000000 1.000000 0.000000]""
	TextureColor ""{texturePath}""

	//---- Fog ----
	g_bFogEnabled ""1""

	//---- Texture Address Mode ----
	g_nTextureAddressModeU ""0"" // Wrap
	g_nTextureAddressModeV ""0"" // Wrap

	//---- Translucent ----
	g_flOpacityScale ""1.000""
	TextureTranslucency ""{texturePath}""


	VariableState
	{{
		""Color""
		{{
			""Color Correction"" 0
		}}
		""Fog""
		{{
		}}
		""Texture Address Mode""
		{{
		}}
		""Translucent""
		{{
		}}
	}}
}}";
            }
            else // CSGO Complex
            {
                vmatContent = $@"// THIS FILE IS AUTO-GENERATED

Layer0
{{
	shader ""csgo_complex.vfx""

	//---- Translucent ----
	F_TRANSLUCENT 1

	//---- Ambient Occlusion ----
	TextureAmbientOcclusion ""materials/default/default_ao.tga""

	//---- Color ----
	g_flModelTintAmount ""1.000""
	g_flTexCoordRotation ""0.000""
	g_nScaleTexCoordUByModelScaleAxis ""0"" // None
	g_nScaleTexCoordVByModelScaleAxis ""0"" // None
	g_vColorTint ""[1.000000 1.000000 1.000000 0.000000]""
	g_vTexCoordCenter ""[0.500 0.500]""
	g_vTexCoordOffset ""[0.000 0.000]""
	g_vTexCoordScale ""[1.000 1.000]""
	g_vTexCoordScrollSpeed ""[0.000 0.000]""
	TextureColor ""{texturePath}""

	//---- Fog ----
	g_bFogEnabled ""1""

	//---- Lighting ----
	g_flMetalness ""0.000""
	TextureRoughness ""materials/default/default_rough.tga""

	//---- Normal Map ----
	TextureNormal ""materials/default/default_normal.tga""

	//---- Texture Address Mode ----
	g_nTextureAddressModeU ""0"" // Wrap
	g_nTextureAddressModeV ""0"" // Wrap

	//---- Translucent ----
	g_flOpacityScale ""1.000""
	TextureTranslucency ""{texturePath}""


	VariableState
	{{
		""Ambient Occlusion""
		{{
		}}
		""Color""
		{{
		}}
		""Fog""
		{{
		}}
		""Lighting""
		{{
			""Roughness"" 0
			""Metalness"" 0
		}}
		""Normal Map""
		{{
		}}
		""Texture Address Mode""
		{{
		}}
		""Translucent""
		{{
		}}
	}}
}}";
            }

            File.WriteAllText(vmatPath, vmatContent);
        }

        private string SanitizeFilename(string filename)
        {
            // Remove or replace invalid filename characters
            string invalid = new string(Path.GetInvalidFileNameChars());
            foreach (char c in invalid)
            {
                filename = filename.Replace(c.ToString(), "");
            }
            // Also remove some additional problematic characters
            filename = filename.Replace(" ", "_");
            filename = filename.Replace(":", "");
            filename = filename.Replace("/", "");
            filename = filename.Replace("\\", "");
            return filename;
        }

        private void PopulateFontComboBox()
        {
            if (_fontComboBox == null) return;

            // Get all installed fonts
            using (InstalledFontCollection fontsCollection = new InstalledFontCollection())
            {
                FontFamily[] fontFamilies = fontsCollection.Families;
                
                foreach (FontFamily font in fontFamilies)
                {
                    _fontComboBox.Items.Add(font.Name);
                }
            }

            // Set default font
            if (_fontComboBox.Items.Contains("Arial"))
            {
                _fontComboBox.SelectedItem = "Arial";
            }
            else if (_fontComboBox.Items.Count > 0)
            {
                _fontComboBox.SelectedIndex = 0;
            }
        }

        private void FindCs2Path()
        {
            try
            {
                // Get Steam path from registry
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (key?.GetValue("SteamPath") is string steamPath)
                {
                    var libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                    if (!File.Exists(libraryFoldersPath))
                    {
                        libraryFoldersPath = Path.Combine(steamPath, "SteamApps", "libraryfolders.vdf");
                    }
                    
                    _cs2Path = FindCs2LibraryPath(libraryFoldersPath);

                    if (!string.IsNullOrEmpty(_cs2Path))
                    {
                        var appManifestPath = Path.Combine(_cs2Path, "steamapps", "appmanifest_730.acf");
                        if (!File.Exists(appManifestPath))
                        {
                            appManifestPath = Path.Combine(_cs2Path, "SteamApps", "appmanifest_730.acf");
                        }
                        
                        if (File.Exists(appManifestPath))
                        {
                            var installDir = ParseAppManifest(appManifestPath);
                            if (!string.IsNullOrEmpty(installDir))
                            {
                                _cs2Path = Path.Combine(_cs2Path, "steamapps", "common", installDir);
                                if (!Directory.Exists(_cs2Path))
                                {
                                    _cs2Path = Path.Combine(_cs2Path.Replace("steamapps", "SteamApps"), installDir);
                                }
                                LogToEvent($"Found CS2 at: {_cs2Path}");
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogToEvent($"Error finding CS2 path: {ex.Message}");
            }

            LogToEvent("Could not find CS2 installation automatically");
        }

        private void PopulateAddonDropdown()
        {
            _addonComboBox!.Items.Clear();
            
            if (!string.IsNullOrEmpty(_cs2Path))
            {
                var addonsPath = Path.Combine(_cs2Path, "content", "csgo_addons");
                if (Directory.Exists(addonsPath))
                {
                    var addonDirs = Directory.GetDirectories(addonsPath)
                        .Select(Path.GetFileName)
                        .Where(name => !string.IsNullOrEmpty(name) && 
                                      !name.Equals("addon_template", StringComparison.OrdinalIgnoreCase) &&
                                      !name.Equals("addontemplate", StringComparison.OrdinalIgnoreCase))
                        .OrderBy(name => name)
                        .ToArray();
                    
                    _addonComboBox.Items.AddRange(addonDirs.Cast<object>().ToArray());
                    
                    if (addonDirs.Length > 0)
                    {
                        _addonComboBox.SelectedIndex = 0;
                    }
                    
                    LogToEvent($"Found {addonDirs.Length} addons");
                }
            }
        }

        private string? FindCs2LibraryPath(string libraryFoldersPath)
        {
            if (!File.Exists(libraryFoldersPath)) return null;

            try
            {
                // Check default Steam location first
                var steamPath = Path.GetDirectoryName(Path.GetDirectoryName(libraryFoldersPath));
                if (!string.IsNullOrEmpty(steamPath))
                {
                    var defaultManifest = Path.Combine(steamPath, "steamapps", "appmanifest_730.acf");
                    if (!File.Exists(defaultManifest))
                    {
                        defaultManifest = Path.Combine(steamPath, "SteamApps", "appmanifest_730.acf");
                    }
                    if (File.Exists(defaultManifest))
                    {
                        return steamPath;
                    }
                }
                
                // Check all library folders
                var content = File.ReadAllText(libraryFoldersPath);
                var lines = content.Split('\n');
                
                foreach (var line in lines)
                {
                    var trimmed = line.Trim();
                    if (trimmed.Contains("\"path\""))
                    {
                        var match = Regex.Match(trimmed, "\"path\"\\s+\"([^\"]+)\"");
                        if (match.Success)
                        {
                            var libraryPath = match.Groups[1].Value.Replace("\\\\", "\\");
                            
                            var appManifest = Path.Combine(libraryPath, "steamapps", "appmanifest_730.acf");
                            if (!File.Exists(appManifest))
                            {
                                appManifest = Path.Combine(libraryPath, "SteamApps", "appmanifest_730.acf");
                            }
                            if (File.Exists(appManifest))
                            {
                                return libraryPath;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogToEvent($"Error reading library folders: {ex.Message}");
            }

            return null;
        }

        private string? ParseAppManifest(string manifestPath)
        {
            try
            {
                var content = File.ReadAllText(manifestPath);
                var match = Regex.Match(content, "\"installdir\"\\s+\"([^\"]+)\"");
                if (match.Success)
                {
                    return match.Groups[1].Value;
                }
            }
            catch (Exception ex)
            {
                LogToEvent($"Error parsing app manifest: {ex.Message}");
            }

            return null;
        }

        private void LogToEvent(string message)
        {
            LogMessage?.Invoke($"[TextOverlay] {message}");
        }

        private void UpdatePreview()
        {
            if (_previewPictureBox == null || _fontComboBox == null || _textInput == null)
                return;

            try
            {
                string text = _textInput.Text;
                if (string.IsNullOrWhiteSpace(text))
                    text = "Sample";

                string fontName = _fontComboBox.SelectedItem?.ToString() ?? "Arial";
                
                // Dispose previous image
                if (_previewPictureBox.Image != null)
                {
                    _previewPictureBox.Image.Dispose();
                }

                // Create preview image
                int size = 250;
                Bitmap preview = new Bitmap(size, size);
                using (Graphics g = Graphics.FromImage(preview))
                {
                    g.Clear(Color.Black);
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAlias;

                    // Calculate font size
                    float fontSize = size / 4f;
                    Font font = new Font(fontName, fontSize, FontStyle.Bold);

                    // Measure and adjust font size
                    SizeF textSize = g.MeasureString(text, font);
                    while ((textSize.Width > size * 0.9f || textSize.Height > size * 0.9f) && fontSize > 10)
                    {
                        fontSize -= 5;
                        font.Dispose();
                        font = new Font(fontName, fontSize, FontStyle.Bold);
                        textSize = g.MeasureString(text, font);
                    }

                    // Get alignment
                    string alignment = _alignmentComboBox?.SelectedItem?.ToString() ?? "Center";
                    StringAlignment horizontalAlignment = alignment switch
                    {
                        "Left" => StringAlignment.Near,
                        "Right" => StringAlignment.Far,
                        _ => StringAlignment.Center
                    };

                    // Draw text
                    using (Brush brush = new SolidBrush(Color.White))
                    {
                        StringFormat format = new StringFormat
                        {
                            Alignment = horizontalAlignment,
                            LineAlignment = StringAlignment.Center
                        };
                        g.DrawString(text, font, brush, new RectangleF(0, 0, size, size), format);
                    }

                    font.Dispose();
                }

                _previewPictureBox.Image = preview;
            }
            catch (Exception ex)
            {
                LogToEvent($"Preview error: {ex.Message}");
            }
        }

        private void FontComboBox_DrawItem(object? sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || _fontComboBox == null) return;

            e.DrawBackground();
            
            string fontName = _fontComboBox.Items[e.Index].ToString() ?? "Arial";
            
            try
            {
                using (Font font = new Font(fontName, 10f, FontStyle.Regular))
                {
                    e.Graphics.DrawString(fontName, font, new SolidBrush(e.ForeColor), e.Bounds.Left, e.Bounds.Top + 2);
                }
            }
            catch
            {
                // If font can't be created, use default
                e.Graphics.DrawString(fontName, e.Font ?? SystemFonts.DefaultFont, new SolidBrush(e.ForeColor), e.Bounds.Left, e.Bounds.Top + 2);
            }
            
            e.DrawFocusRectangle();
        }

    }
}
