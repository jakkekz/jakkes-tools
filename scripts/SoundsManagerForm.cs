using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace CS2KZMappingTools
{
    public partial class SoundsManagerForm : Form
    {
        private readonly ThemeManager _themeManager;
        public event Action<string>? LogMessage;

        // UI Controls
        private ComboBox? _addonNameComboBox;
        private ComboBox? _soundTypeComboBox;
        private Button? _browseSoundButton;
        private Label? _soundFileLabel;
        private TextBox? _outputNameTextBox;
        private TextBox? _soundNameTextBox;
        private TrackBar? _volumeTrackBar;
        private Label? _volumeValueLabel;
        private TrackBar? _pitchTrackBar;
        private Label? _pitchValueLabel;
        private NumericUpDown? _distanceNearNumeric;
        private NumericUpDown? _distanceNearVolumeNumeric;
        private NumericUpDown? _distanceMidNumeric;
        private NumericUpDown? _distanceMidVolumeNumeric;
        private NumericUpDown? _distanceFarNumeric;
        private NumericUpDown? _distanceFarVolumeNumeric;
        private CheckBox? _occlusionCheckBox;
        private NumericUpDown? _occlusionIntensityNumeric;
        private Button? _addSoundButton;
        private Button? _openFolderButton;
        private Button? _playButton;
        private Button? _pauseButton;
        private Button? _stopButton;
        private Panel? _waveformPanel;
        private HScrollBar? _waveformScrollBar;
        private TrackBar? _previewVolumeTrackBar;
        private Label? _previewVolumeLabel;
        private TrackBar? _progressTrackBar;
        private System.Windows.Forms.Timer? _progressUpdateTimer;
        private Label? _timelineLabel;
        private NumericUpDown? _cutStartMinute;
        private NumericUpDown? _cutStartSecond;
        private NumericUpDown? _cutEndMinute;
        private NumericUpDown? _cutEndSecond;
        private Button? _applyCutButton;
        private Button? _undoCutButton;

        // Application state
        private string? _cs2BasePath;
        private string _soundFilePath = "";
        private string _originalSoundFilePath = ""; // Store original before cuts
        private List<string> _availableAddons = new List<string>();
        
        // Audio playback with NAudio
        private WaveOutEvent? _waveOut;
        private IWaveProvider? _audioFile;
        private ISampleProvider? _pitchShifter;
        private bool _isPlaying = false;
        private bool _isPaused = false;
        private bool _isDraggingWaveform = false;
        private float[] _waveformData = Array.Empty<float>();
        private int _waveformScrollPosition = 0;

        public SoundsManagerForm(ThemeManager themeManager)
        {
            _themeManager = themeManager;
            InitializeComponent();
            DetectCS2Path();
            ScanAvailableAddons();
            ApplyTheme();
            
            // Initialize progress update timer
            _progressUpdateTimer = new System.Windows.Forms.Timer();
            _progressUpdateTimer.Interval = 16; // ~60fps for smooth updates
            _progressUpdateTimer.Tick += ProgressUpdateTimer_Tick;
            
            // Add form closing event to cleanup audio resources
            this.FormClosing += SoundsManagerForm_FormClosing;
        }
        
        private void SoundsManagerForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            // Cleanup NAudio resources
            _waveOut?.Stop();
            _waveOut?.Dispose();
            
            // Dispose audio file if it's disposable
            if (_audioFile is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        private void SetButtonEnabled(Button? button, bool enabled)
        {
            if (button != null)
            {
                button.Enabled = enabled;
                button.Invalidate();
            }
        }

        private void InitializeComponent()
        {
            this.Text = "CS2 Sounds Manager";
            this.Size = new Size(900, 750);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.MinimumSize = new Size(800, 650);
            
            // Set icon to sounds.png
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icons", "sounds.png");
            if (File.Exists(iconPath))
            {
                try
                {
                    using (var bmp = new Bitmap(iconPath))
                    {
                        this.Icon = Icon.FromHandle(bmp.GetHicon());
                    }
                }
                catch { /* Icon loading failed, use default */ }
            }

            // Create main panel with auto-scroll
            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true
            };

            // Create layout panel
            var layoutPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 1,
                Padding = new Padding(10)
            };
            layoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layoutPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));

            // === LEFT PANEL ===
            var leftPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(5)
            };

            int leftY = 10;

            // Addon Name
            var addonLabel = new Label
            {
                Text = "Addon Name:",
                Location = new Point(10, leftY),
                Size = new Size(280, 25)
            };
            leftPanel.Controls.Add(addonLabel);
            leftY += 30;

            _addonNameComboBox = new ComboBox
            {
                Location = new Point(10, leftY),
                Size = new Size(280, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Cursor = Cursors.Hand
            };
            _addonNameComboBox.Items.AddRange(_availableAddons.ToArray());
            if (_addonNameComboBox.Items.Count > 0)
                _addonNameComboBox.SelectedIndex = 0;
            leftPanel.Controls.Add(_addonNameComboBox);
            leftY += 35;

            // Sound File Selection
            var soundFileHeaderLabel = new Label
            {
                Text = "Sound File:",
                Location = new Point(10, leftY),
                Size = new Size(280, 25),
                Font = new Font(this.Font.FontFamily, 10, FontStyle.Bold)
            };
            leftPanel.Controls.Add(soundFileHeaderLabel);
            leftY += 30;

            _soundFileLabel = new Label
            {
                Text = "No file selected",
                Location = new Point(10, leftY),
                Size = new Size(280, 40),
                ForeColor = Color.Red
            };
            leftPanel.Controls.Add(_soundFileLabel);
            leftY += 45;

            _browseSoundButton = new Button
            {
                Text = "Browse Sound File",
                Location = new Point(10, leftY),
                Size = new Size(280, 35),
                Cursor = Cursors.Hand
            };
            _browseSoundButton.Click += BrowseSoundButton_Click;
            leftPanel.Controls.Add(_browseSoundButton);
            leftY += 45;

            // Audio Preview Controls
            var previewLabel = new Label
            {
                Text = "Audio Preview:",
                Location = new Point(10, leftY),
                Size = new Size(280, 25),
                Font = new Font(this.Font.FontFamily, 9, FontStyle.Bold)
            };
            leftPanel.Controls.Add(previewLabel);
            leftY += 30;

            var previewPanel = new FlowLayoutPanel
            {
                Location = new Point(10, leftY),
                Size = new Size(280, 40),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            _playButton = new Button
            {
                Text = "▶",
                Size = new Size(40, 35),
                Margin = new Padding(0, 0, 5, 0),
                Enabled = false,
                FlatStyle = FlatStyle.Flat,
                Font = new Font(this.Font.FontFamily, 12, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _playButton.FlatAppearance.BorderSize = 1;
            _playButton.FlatAppearance.BorderColor = Color.FromArgb(34, 139, 34);
            _playButton.Paint += (s, e) =>
            {
                var btn = (Button)s;
                e.Graphics.Clear(btn.BackColor);
                var color = Color.FromArgb(34, 139, 34);
                var fontSize = !btn.Enabled ? 16f : 12f; // Active (disabled) = larger
                using (var font = new Font(btn.Font.FontFamily, fontSize, FontStyle.Bold))
                using (var brush = new SolidBrush(color))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString(btn.Text, font, brush, btn.ClientRectangle, sf);
                }
            };
            _playButton.EnabledChanged += (s, e) =>
            {
                var btn = (Button)s;
                btn.Size = !btn.Enabled ? new Size(45, 40) : new Size(40, 35); // Active (disabled) = larger
                btn.Invalidate();
            };
            _playButton.Click += PlayButton_Click;
            previewPanel.Controls.Add(_playButton);

            _pauseButton = new Button
            {
                Text = "⏸",
                Size = new Size(40, 35),
                Margin = new Padding(0, 0, 5, 0),
                Enabled = false,
                FlatStyle = FlatStyle.Flat,
                Font = new Font(this.Font.FontFamily, 12, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _pauseButton.FlatAppearance.BorderSize = 1;
            _pauseButton.FlatAppearance.BorderColor = Color.FromArgb(218, 165, 32);
            _pauseButton.Paint += (s, e) =>
            {
                var btn = (Button)s;
                e.Graphics.Clear(btn.BackColor);
                var color = Color.FromArgb(218, 165, 32);
                var fontSize = !btn.Enabled ? 16f : 12f; // Active (disabled) = larger
                using (var font = new Font(btn.Font.FontFamily, fontSize, FontStyle.Bold))
                using (var brush = new SolidBrush(color))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString(btn.Text, font, brush, btn.ClientRectangle, sf);
                }
            };
            _pauseButton.EnabledChanged += (s, e) =>
            {
                var btn = (Button)s;
                btn.Size = !btn.Enabled ? new Size(45, 40) : new Size(40, 35); // Active (disabled) = larger
                btn.Invalidate();
            };
            _pauseButton.Click += PauseButton_Click;
            previewPanel.Controls.Add(_pauseButton);

            _stopButton = new Button
            {
                Text = "⏹",
                Size = new Size(40, 35),
                Margin = new Padding(0, 0, 5, 0),
                Enabled = false,
                FlatStyle = FlatStyle.Flat,
                Font = new Font(this.Font.FontFamily, 12, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _stopButton.FlatAppearance.BorderSize = 1;
            _stopButton.FlatAppearance.BorderColor = Color.FromArgb(220, 20, 60);
            _stopButton.Paint += (s, e) =>
            {
                var btn = (Button)s;
                e.Graphics.Clear(btn.BackColor);
                var color = Color.FromArgb(220, 20, 60);
                var fontSize = !btn.Enabled ? 16f : 12f; // Active (disabled) = larger
                using (var font = new Font(btn.Font.FontFamily, fontSize, FontStyle.Bold))
                using (var brush = new SolidBrush(color))
                {
                    var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                    e.Graphics.DrawString(btn.Text, font, brush, btn.ClientRectangle, sf);
                }
            };
            _stopButton.EnabledChanged += (s, e) =>
            {
                var btn = (Button)s;
                btn.Size = !btn.Enabled ? new Size(45, 40) : new Size(40, 35); // Active (disabled) = larger
                btn.Invalidate();
            };
            _stopButton.Click += StopButton_Click;
            previewPanel.Controls.Add(_stopButton);

            leftPanel.Controls.Add(previewPanel);
            leftY += 50;

            // Preview Volume Control
            _previewVolumeLabel = new Label
            {
                Text = "Preview Volume: 50",
                Location = new Point(10, leftY),
                Size = new Size(150, 25)
            };
            leftPanel.Controls.Add(_previewVolumeLabel);

            _previewVolumeTrackBar = new TrackBar
            {
                Location = new Point(160, leftY - 3),
                Size = new Size(130, 45),
                Minimum = 0,
                Maximum = 100,
                Value = 50,
                TickFrequency = 10,
                Cursor = Cursors.Hand
            };
            _previewVolumeTrackBar.ValueChanged += PreviewVolumeTrackBar_ValueChanged;
            leftPanel.Controls.Add(_previewVolumeTrackBar);
            leftY += 40;

            // Waveform Visualization (Seekable)
            var waveformLabel = new Label
            {
                Text = "Waveform (Click or drag to seek):",
                Location = new Point(10, leftY),
                Size = new Size(180, 25),
                Font = new Font(this.Font.FontFamily, 9, FontStyle.Bold)
            };
            leftPanel.Controls.Add(waveformLabel);
            
            // Timeline label showing current/total time
            _timelineLabel = new Label
            {
                Text = "0:00 / 0:00",
                Location = new Point(195, leftY),
                Size = new Size(95, 25),
                TextAlign = ContentAlignment.MiddleRight,
                Font = new Font(this.Font.FontFamily, 8.5f)
            };
            leftPanel.Controls.Add(_timelineLabel);
            leftY += 30;

            _waveformPanel = new Panel
            {
                Location = new Point(10, leftY),
                Size = new Size(280, 120),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.Black,
                Cursor = Cursors.Hand
            };
            // Enable double buffering to prevent flashing
            _waveformPanel.GetType().GetProperty("DoubleBuffered", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                ?.SetValue(_waveformPanel, true);
            _waveformPanel.Paint += WaveformPanel_Paint;
            _waveformPanel.MouseClick += WaveformPanel_MouseClick;
            _waveformPanel.MouseDown += WaveformPanel_MouseDown;
            _waveformPanel.MouseMove += WaveformPanel_MouseMove;
            _waveformPanel.MouseUp += WaveformPanel_MouseUp;
            leftPanel.Controls.Add(_waveformPanel);
            leftY += 125;

            // Hidden progress trackbar (for backwards compatibility)
            _progressTrackBar = new TrackBar
            {
                Visible = false,
                Minimum = 0,
                Maximum = 1000,
                Value = 0
            };

            _waveformScrollBar = new HScrollBar
            {
                Location = new Point(10, leftY),
                Size = new Size(280, 17),
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Visible = false,
                Cursor = Cursors.Hand
            };
            _waveformScrollBar.Scroll += WaveformScrollBar_Scroll;
            leftPanel.Controls.Add(_waveformScrollBar);
            leftY += 27;

            // Output Name
            var outputNameLabel = new Label
            {
                Text = "Output Name:",
                Location = new Point(10, leftY),
                Size = new Size(280, 25)
            };
            leftPanel.Controls.Add(outputNameLabel);
            leftY += 30;

            _outputNameTextBox = new TextBox
            {
                Location = new Point(10, leftY),
                Size = new Size(280, 25)
            };
            leftPanel.Controls.Add(_outputNameTextBox);
            leftY += 35;

            // Sound Cutting Section
            var cutSoundLabel = new Label
            {
                Text = "Cut Sound:",
                Location = new Point(10, leftY),
                Size = new Size(280, 20),
                Font = new Font(this.Font.FontFamily, 9, FontStyle.Bold)
            };
            leftPanel.Controls.Add(cutSoundLabel);
            leftY += 25;

            // Start time
            var startLabel = new Label
            {
                Text = "Start:",
                Location = new Point(10, leftY + 3),
                Size = new Size(40, 20)
            };
            leftPanel.Controls.Add(startLabel);

            _cutStartMinute = new NumericUpDown
            {
                Location = new Point(50, leftY),
                Size = new Size(50, 25),
                Maximum = 999,
                Minimum = 0,
                Value = 0,
                Cursor = Cursors.Hand
            };
            leftPanel.Controls.Add(_cutStartMinute);

            var startMinLabel = new Label
            {
                Text = "m",
                Location = new Point(102, leftY + 3),
                Size = new Size(15, 20)
            };
            leftPanel.Controls.Add(startMinLabel);

            _cutStartSecond = new NumericUpDown
            {
                Location = new Point(120, leftY),
                Size = new Size(50, 25),
                Maximum = 59,
                Minimum = 0,
                Value = 0,
                Cursor = Cursors.Hand
            };
            leftPanel.Controls.Add(_cutStartSecond);

            var startSecLabel = new Label
            {
                Text = "s",
                Location = new Point(172, leftY + 3),
                Size = new Size(15, 20)
            };
            leftPanel.Controls.Add(startSecLabel);
            leftY += 30;

            // End time
            var endLabel = new Label
            {
                Text = "End:",
                Location = new Point(10, leftY + 3),
                Size = new Size(40, 20)
            };
            leftPanel.Controls.Add(endLabel);

            _cutEndMinute = new NumericUpDown
            {
                Location = new Point(50, leftY),
                Size = new Size(50, 25),
                Maximum = 999,
                Minimum = 0,
                Value = 0,
                Cursor = Cursors.Hand
            };
            leftPanel.Controls.Add(_cutEndMinute);

            var endMinLabel = new Label
            {
                Text = "m",
                Location = new Point(102, leftY + 3),
                Size = new Size(15, 20)
            };
            leftPanel.Controls.Add(endMinLabel);

            _cutEndSecond = new NumericUpDown
            {
                Location = new Point(120, leftY),
                Size = new Size(50, 25),
                Maximum = 59,
                Minimum = 0,
                Value = 0,
                Cursor = Cursors.Hand
            };
            leftPanel.Controls.Add(_cutEndSecond);

            var endSecLabel = new Label
            {
                Text = "s",
                Location = new Point(172, leftY + 3),
                Size = new Size(15, 20)
            };
            leftPanel.Controls.Add(endSecLabel);
            leftY += 30;

            // Apply Cut button
            _applyCutButton = new Button
            {
                Text = "Apply Cut",
                Location = new Point(10, leftY),
                Size = new Size(90, 30),
                Enabled = false,
                Cursor = Cursors.Hand
            };
            _applyCutButton.Click += ApplyCutButton_Click;
            leftPanel.Controls.Add(_applyCutButton);
            
            // Undo Cut button
            _undoCutButton = new Button
            {
                Text = "Undo Cut",
                Location = new Point(105, leftY),
                Size = new Size(90, 30),
                Enabled = false,
                Cursor = Cursors.Hand
            };
            _undoCutButton.Click += UndoCutButton_Click;
            leftPanel.Controls.Add(_undoCutButton);
            leftY += 40;

            // Sound Event Name (hidden, synced with Output Name)
            _soundNameTextBox = new TextBox
            {
                Visible = false
            };
            // Sync sound name with output name
            _outputNameTextBox.TextChanged += (s, e) => _soundNameTextBox.Text = _outputNameTextBox.Text;

            // === RIGHT PANEL ===
            var rightPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(5)
            };

            int rightY = 10;

            // Sound Settings Header
            var settingsHeaderLabel = new Label
            {
                Text = "Sound Settings",
                Location = new Point(10, rightY),
                Size = new Size(380, 25),
                Font = new Font(this.Font.FontFamily, 11, FontStyle.Bold)
            };
            rightPanel.Controls.Add(settingsHeaderLabel);
            rightY += 35;

            // Sound Type
            var soundTypeLabel = new Label
            {
                Text = "Sound Type:",
                Location = new Point(10, rightY),
                Size = new Size(120, 25)
            };
            rightPanel.Controls.Add(soundTypeLabel);

            _soundTypeComboBox = new ComboBox
            {
                Location = new Point(140, rightY),
                Size = new Size(250, 25),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Cursor = Cursors.Hand
            };
            _soundTypeComboBox.Items.AddRange(new object[] { "csgo_mega", "csgo_music", "csgo_3d" });
            _soundTypeComboBox.SelectedIndex = 0;
            rightPanel.Controls.Add(_soundTypeComboBox);
            rightY += 35;

            // Volume Slider
            var volumeLabel = new Label
            {
                Text = "Volume:",
                Location = new Point(10, rightY),
                Size = new Size(120, 25)
            };
            rightPanel.Controls.Add(volumeLabel);

            _volumeValueLabel = new Label
            {
                Text = "1.0",
                Location = new Point(350, rightY),
                Size = new Size(40, 25),
                TextAlign = ContentAlignment.MiddleRight
            };
            rightPanel.Controls.Add(_volumeValueLabel);

            _volumeTrackBar = new TrackBar
            {
                Location = new Point(140, rightY - 5),
                Size = new Size(200, 45),
                Minimum = 0,
                Maximum = 100,
                Value = 10,
                TickFrequency = 10,
                Cursor = Cursors.Hand
            };
            _volumeTrackBar.ValueChanged += VolumeTrackBar_ValueChanged;
            rightPanel.Controls.Add(_volumeTrackBar);
            rightY += 45;

            // Pitch Slider
            var pitchLabel = new Label
            {
                Text = "Pitch:",
                Location = new Point(10, rightY),
                Size = new Size(120, 25)
            };
            rightPanel.Controls.Add(pitchLabel);

            _pitchValueLabel = new Label
            {
                Text = "1.00",
                Location = new Point(350, rightY),
                Size = new Size(40, 25),
                TextAlign = ContentAlignment.MiddleRight
            };
            rightPanel.Controls.Add(_pitchValueLabel);

            _pitchTrackBar = new TrackBar
            {
                Location = new Point(140, rightY - 5),
                Size = new Size(200, 45),
                Minimum = 50,
                Maximum = 200,
                Value = 100,
                TickFrequency = 10,
                Cursor = Cursors.Hand
            };
            _pitchTrackBar.ValueChanged += PitchTrackBar_ValueChanged;
            rightPanel.Controls.Add(_pitchTrackBar);
            rightY += 45;

            // Distance Settings Header
            var distanceHeaderLabel = new Label
            {
                Text = "Distance Volume Mapping",
                Location = new Point(10, rightY),
                Size = new Size(380, 25),
                Font = new Font(this.Font.FontFamily, 10, FontStyle.Bold)
            };
            rightPanel.Controls.Add(distanceHeaderLabel);
            rightY += 30;

            // Distance Near
            var distanceNearLabel = new Label
            {
                Text = "Near Distance:",
                Location = new Point(10, rightY),
                Size = new Size(120, 25)
            };
            rightPanel.Controls.Add(distanceNearLabel);

            _distanceNearNumeric = new NumericUpDown
            {
                Location = new Point(140, rightY),
                Size = new Size(100, 25),
                DecimalPlaces = 1,
                Increment = 10,
                Minimum = 0,
                Maximum = 10000,
                Value = 0,
                Cursor = Cursors.Hand
            };
            rightPanel.Controls.Add(_distanceNearNumeric);

            var nearVolumeLabel = new Label
            {
                Text = "Volume:",
                Location = new Point(250, rightY),
                Size = new Size(60, 25)
            };
            rightPanel.Controls.Add(nearVolumeLabel);

            _distanceNearVolumeNumeric = new NumericUpDown
            {
                Location = new Point(315, rightY),
                Size = new Size(75, 25),
                DecimalPlaces = 2,
                Increment = 0.1M,
                Minimum = 0,
                Maximum = 1,
                Value = 1.0M,
                Cursor = Cursors.Hand
            };
            rightPanel.Controls.Add(_distanceNearVolumeNumeric);
            rightY += 35;

            // Distance Mid
            var distanceMidLabel = new Label
            {
                Text = "Mid Distance:",
                Location = new Point(10, rightY),
                Size = new Size(120, 25)
            };
            rightPanel.Controls.Add(distanceMidLabel);

            _distanceMidNumeric = new NumericUpDown
            {
                Location = new Point(140, rightY),
                Size = new Size(100, 25),
                DecimalPlaces = 1,
                Increment = 50,
                Minimum = 0,
                Maximum = 10000,
                Value = 1000,
                Cursor = Cursors.Hand
            };
            rightPanel.Controls.Add(_distanceMidNumeric);

            var midVolumeLabel = new Label
            {
                Text = "Volume:",
                Location = new Point(250, rightY),
                Size = new Size(60, 25)
            };
            rightPanel.Controls.Add(midVolumeLabel);

            _distanceMidVolumeNumeric = new NumericUpDown
            {
                Location = new Point(315, rightY),
                Size = new Size(75, 25),
                DecimalPlaces = 2,
                Increment = 0.1M,
                Minimum = 0,
                Maximum = 1,
                Value = 0.5M,
                Cursor = Cursors.Hand
            };
            rightPanel.Controls.Add(_distanceMidVolumeNumeric);
            rightY += 35;

            // Distance Far
            var distanceFarLabel = new Label
            {
                Text = "Far Distance:",
                Location = new Point(10, rightY),
                Size = new Size(120, 25)
            };
            rightPanel.Controls.Add(distanceFarLabel);

            _distanceFarNumeric = new NumericUpDown
            {
                Location = new Point(140, rightY),
                Size = new Size(100, 25),
                DecimalPlaces = 1,
                Increment = 100,
                Minimum = 0,
                Maximum = 10000,
                Value = 3000,
                Cursor = Cursors.Hand
            };
            rightPanel.Controls.Add(_distanceFarNumeric);

            var farVolumeLabel = new Label
            {
                Text = "Volume:",
                Location = new Point(250, rightY),
                Size = new Size(60, 25)
            };
            rightPanel.Controls.Add(farVolumeLabel);

            _distanceFarVolumeNumeric = new NumericUpDown
            {
                Location = new Point(315, rightY),
                Size = new Size(75, 25),
                DecimalPlaces = 2,
                Increment = 0.1M,
                Minimum = 0,
                Maximum = 1,
                Value = 0,
                Cursor = Cursors.Hand
            };
            rightPanel.Controls.Add(_distanceFarVolumeNumeric);
            rightY += 40;

            // Occlusion
            _occlusionCheckBox = new CheckBox
            {
                Text = "Enable Occlusion",
                Location = new Point(10, rightY),
                Size = new Size(380, 25),
                Cursor = Cursors.Hand
            };
            _occlusionCheckBox.CheckedChanged += OcclusionCheckBox_CheckedChanged;
            rightPanel.Controls.Add(_occlusionCheckBox);
            rightY += 30;

            var occlusionIntensityLabel = new Label
            {
                Text = "Occlusion Intensity: 100",
                Location = new Point(10, rightY),
                Size = new Size(150, 25)
            };
            rightPanel.Controls.Add(occlusionIntensityLabel);

            _occlusionIntensityNumeric = new NumericUpDown
            {
                Visible = false,
                Value = 100
            };

            var occlusionSlider = new TrackBar
            {
                Location = new Point(160, rightY - 3),
                Size = new Size(230, 45),
                Minimum = 0,
                Maximum = 100,
                Value = 50,
                TickFrequency = 10,
                Enabled = false,
                Cursor = Cursors.Hand
            };
            occlusionSlider.ValueChanged += (s, e) =>
            {
                _occlusionIntensityNumeric.Value = occlusionSlider.Value * 2; // Scale to 0-200 range
                occlusionIntensityLabel.Text = $"Occlusion Intensity: {occlusionSlider.Value}";
            };
            _occlusionCheckBox.CheckedChanged += (s, e) =>
            {
                occlusionSlider.Enabled = _occlusionCheckBox.Checked;
                occlusionIntensityLabel.Enabled = _occlusionCheckBox.Checked;
            };
            rightPanel.Controls.Add(occlusionSlider);
            rightY += 40;

            // Add panels to layout
            layoutPanel.Controls.Add(leftPanel, 0, 0);
            layoutPanel.Controls.Add(rightPanel, 1, 0);
            mainPanel.Controls.Add(layoutPanel);

            // Bottom panel for buttons
            var bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 70,
                Padding = new Padding(10)
            };

            // Action Buttons
            var buttonPanel = new FlowLayoutPanel
            {
                Location = new Point(10, 5),
                Size = new Size(860, 50),
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false
            };

            _openFolderButton = new Button
            {
                Text = "Open Addon Folder",
                Size = new Size(200, 40),
                Margin = new Padding(0, 0, 10, 0),
                Cursor = Cursors.Hand
            };
            _openFolderButton.Click += OpenFolderButton_Click;
            buttonPanel.Controls.Add(_openFolderButton);
            
            // Spacer to push Add Sound button to the right
            var spacer = new Panel
            {
                Width = 430,
                Height = 40,
                Margin = new Padding(0)
            };
            buttonPanel.Controls.Add(spacer);

            _addSoundButton = new Button
            {
                Text = "Add Sound to Addon",
                Size = new Size(200, 40),
                Margin = new Padding(0, 0, 10, 0),
                BackColor = Color.FromArgb(34, 139, 34),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font(this.Font.FontFamily, 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            _addSoundButton.FlatAppearance.BorderSize = 0;
            _addSoundButton.Click += AddSoundButton_Click;
            buttonPanel.Controls.Add(_addSoundButton);

            bottomPanel.Controls.Add(buttonPanel);

            this.Controls.Add(mainPanel);
            this.Controls.Add(bottomPanel);

            // Log initial status
            Log("CS2 Sounds Manager initialized");
            if (!string.IsNullOrEmpty(_cs2BasePath))
            {
                Log($"✓ CS2 detected at: {_cs2BasePath}");
            }
            else
            {
                Log("✗ CS2 installation not found");
            }
        }

        private void ApplyTheme()
        {
            var theme = _themeManager.GetCurrentTheme();
            this.BackColor = theme.WindowBackground;
            this.ForeColor = theme.Text;

            foreach (Control control in this.Controls)
            {
                ApplyThemeToControl(control, theme);
            }
        }

        private void ApplyThemeToControl(Control control, Theme theme)
        {
            if (control is Button button)
            {
                button.BackColor = theme.ButtonBackground;
                button.ForeColor = theme.Text;
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = theme.Border;
            }
            else if (control is TextBox || control is ComboBox || control is NumericUpDown)
            {
                control.BackColor = theme.WindowBackground;
                control.ForeColor = theme.Text;
            }
            else if (control is Label label && label != _soundFileLabel)
            {
                label.ForeColor = theme.Text;
            }
            else if (control is Panel || control is TableLayoutPanel || control is FlowLayoutPanel)
            {
                control.BackColor = theme.WindowBackground;
                foreach (Control child in control.Controls)
                {
                    ApplyThemeToControl(child, theme);
                }
            }
        }

        private void OcclusionCheckBox_CheckedChanged(object? sender, EventArgs e)
        {
            if (_occlusionCheckBox == null || _occlusionIntensityNumeric == null) return;

            bool enabled = _occlusionCheckBox.Checked;
            _occlusionIntensityNumeric.Enabled = enabled;

            // Enable/disable the label too
            if (_occlusionIntensityNumeric.Parent != null)
            {
                foreach (Control control in _occlusionIntensityNumeric.Parent.Controls)
                {
                    if (control is Label label && label.Text.Contains("Occlusion Intensity"))
                    {
                        label.Enabled = enabled;
                        break;
                    }
                }
            }
        }

        private void DetectCS2Path()
        {
            try
            {
                Log("Detecting CS2 installation...");
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (key?.GetValue("SteamPath") is not string steamPath)
                {
                    Log("✗ Steam installation not found in registry");
                    return;
                }
                
                Log($"✓ Steam path: {steamPath}");

                var libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(libraryFoldersPath))
                {
                    libraryFoldersPath = Path.Combine(steamPath, "SteamApps", "libraryfolders.vdf");
                }

                _cs2BasePath = FindCS2LibraryPath(libraryFoldersPath);

                if (!string.IsNullOrEmpty(_cs2BasePath) && Directory.Exists(_cs2BasePath))
                {
                    Log($"✓ CS2 detected at: {_cs2BasePath}");
                }
                else
                {
                    Log("✗ CS2 installation not found");
                }
            }
            catch (Exception ex)
            {
                Log($"✗ Error detecting CS2 path: {ex.Message}");
            }
        }

        private string? FindCS2LibraryPath(string libraryFoldersPath)
        {
            if (!File.Exists(libraryFoldersPath)) return null;

            try
            {
                // Check default Steam location first
                var steamPath = Path.GetDirectoryName(Path.GetDirectoryName(libraryFoldersPath));
                if (!string.IsNullOrEmpty(steamPath))
                {
                    var defaultPath = Path.Combine(steamPath, "steamapps", "common", "Counter-Strike Global Offensive");
                    if (!Directory.Exists(defaultPath))
                    {
                        defaultPath = Path.Combine(steamPath, "SteamApps", "common", "Counter-Strike Global Offensive");
                    }
                    if (Directory.Exists(defaultPath))
                    {
                        return defaultPath;
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
                        var match = Regex.Match(trimmed, @"""path""\s+""([^""]+)""");
                        if (match.Success)
                        {
                            var libraryPath = match.Groups[1].Value.Replace(@"\\", @"\");
                            var cs2Path = Path.Combine(libraryPath, "steamapps", "common", "Counter-Strike Global Offensive");
                            if (!Directory.Exists(cs2Path))
                            {
                                cs2Path = Path.Combine(libraryPath, "SteamApps", "common", "Counter-Strike Global Offensive");
                            }
                            if (Directory.Exists(cs2Path))
                            {
                                return cs2Path;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"✗ Error finding CS2 path: {ex.Message}");
            }

            return null;
        }

        private void ScanAvailableAddons()
        {
            if (string.IsNullOrEmpty(_cs2BasePath))
            {
                Log("⚠ Cannot scan addons - CS2 path not detected");
                return;
            }

            var addonsPath = Path.Combine(_cs2BasePath, "content", "csgo_addons");
            Log($"Scanning for addons in: {addonsPath}");
            
            if (!Directory.Exists(addonsPath))
            {
                Log($"✗ Addons folder not found: {addonsPath}");
                return;
            }

            try
            {
                _availableAddons = Directory.GetDirectories(addonsPath)
                    .Select(d => Path.GetFileName(d))
                    .Where(name => !string.IsNullOrEmpty(name) && name != "addon_template")
                    .OrderBy(name => name)
                    .ToList()!;

                if (_availableAddons.Any())
                {
                    Log($"✓ Found {_availableAddons.Count} addon(s): {string.Join(", ", _availableAddons)}");
                    
                    // Populate ComboBox on UI thread
                    if (_addonNameComboBox != null)
                    {
                        if (_addonNameComboBox.InvokeRequired)
                        {
                            _addonNameComboBox.Invoke((MethodInvoker)(() =>
                            {
                                _addonNameComboBox.Items.Clear();
                                foreach (var addon in _availableAddons)
                                {
                                    _addonNameComboBox.Items.Add(addon);
                                }
                                if (_availableAddons.Count > 0)
                                {
                                    _addonNameComboBox.SelectedIndex = 0;
                                }
                            }));
                        }
                        else
                        {
                            _addonNameComboBox.Items.Clear();
                            foreach (var addon in _availableAddons)
                            {
                                _addonNameComboBox.Items.Add(addon);
                            }
                            if (_availableAddons.Count > 0)
                            {
                                _addonNameComboBox.SelectedIndex = 0;
                            }
                        }
                    }
                }
                else
                {
                    Log("⚠ No addons found (excluding addon_template)");
                }
            }
            catch (Exception ex)
            {
                Log($"✗ Error scanning addons: {ex.Message}");
            }
        }

        private void BrowseSoundButton_Click(object? sender, EventArgs e)
        {
            using var openFileDialog = new OpenFileDialog
            {
                Title = "Select Sound File",
                Filter = "Audio Files (*.mp3;*.wav)|*.mp3;*.wav|MP3 Files (*.mp3)|*.mp3|WAV Files (*.wav)|*.wav|All Files (*.*)|*.*",
                FilterIndex = 1
            };

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                _soundFilePath = openFileDialog.FileName;
                var fileName = Path.GetFileName(_soundFilePath);
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(_soundFilePath);

                if (_soundFileLabel != null)
                {
                    _soundFileLabel.Text = fileName;
                    _soundFileLabel.ForeColor = Color.Green;
                }

                if (_outputNameTextBox != null)
                {
                    _outputNameTextBox.Text = fileNameWithoutExt;
                }

                if (_soundNameTextBox != null)
                {
                    _soundNameTextBox.Text = fileNameWithoutExt;
                }

                Log($"✓ Selected: {fileName}");

                // Load audio for preview
                LoadAudioForPreview(_soundFilePath);
            }
        }

        private void PlayButton_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_soundFilePath) || !File.Exists(_soundFilePath))
            {
                MessageBox.Show("Please select a sound file first", "No Sound", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                // Ensure audio is loaded
                if (_waveOut == null || _audioFile == null)
                {
                    Log("⚠ Audio not loaded, attempting to load...");
                    LoadAudioForPreview(_soundFilePath);
                    // Auto-play will handle playing
                    return;
                }

                if (_isPaused)
                {
                    // Resume playback
                    _waveOut.Play();
                    _isPaused = false;
                    _isPlaying = true;
                }
                else
                {
                    _waveOut.Play();
                    _isPlaying = true;
                }

                if (_playButton != null) { _playButton.Enabled = false; _playButton.Invalidate(); }
                if (_pauseButton != null) { _pauseButton.Enabled = true; _pauseButton.Invalidate(); }
                if (_stopButton != null) { _stopButton.Enabled = true; _stopButton.Invalidate(); }
                
                // Start progress timer
                _progressUpdateTimer?.Start();

                Log("▶ Playing audio...");
            }
            catch (Exception ex)
            {
                _isPlaying = false;
                if (_playButton != null) { _playButton.Enabled = true; _playButton.Invalidate(); }
                if (_pauseButton != null) { _pauseButton.Enabled = false; _pauseButton.Invalidate(); }
                if (_stopButton != null) { _stopButton.Enabled = false; _stopButton.Invalidate(); }
                
                Log($"✗ Error playing audio: {ex.Message}");
                Log("💡 Your WAV file must be uncompressed PCM format. Try converting with: ffmpeg -i input.wav -acodec pcm_s16le output.wav");
                MessageBox.Show(
                    $"Cannot play this audio file.\n\n" +
                    $"Error: {ex.Message}\n\n" +
                    $"SoundPlayer only supports uncompressed PCM WAV files.\n" +
                    $"Try converting your file with ffmpeg or another audio tool to PCM format.",
                    "Audio Format Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void PauseButton_Click(object? sender, EventArgs e)
        {
            if (!_isPlaying) return;

            try
            {
                // NAudio supports proper pause
                _waveOut?.Pause();
                _isPaused = true;
                _isPlaying = false;
                
                // Stop progress timer
                _progressUpdateTimer?.Stop();

                if (_playButton != null) { _playButton.Enabled = true; _playButton.Invalidate(); }
                if (_pauseButton != null) { _pauseButton.Enabled = false; _pauseButton.Invalidate(); }

                Log("⏸ Paused");
            }
            catch (Exception ex)
            {
                Log($"✗ Error pausing audio: {ex.Message}");
            }
        }

        private void StopButton_Click(object? sender, EventArgs e)
        {
            if (!_isPlaying && !_isPaused) return;

            try
            {
                _waveOut?.Stop();
                
                // Stop progress timer
                _progressUpdateTimer?.Stop();
                
                // Try to reset position if supported
                if (_audioFile is AudioFileReader afr)
                {
                    afr.Position = 0;
                }
                else if (_audioFile is WaveChannel32 wc)
                {
                    wc.Position = 0;
                }
                
                _isPlaying = false;
                _isPaused = false;
                _waveformScrollPosition = 0;
                
                // Reset timeline label
                if (_timelineLabel != null && _audioFile != null)
                {
                    TimeSpan totalTime = TimeSpan.Zero;
                    if (_audioFile is AudioFileReader afr2)
                    {
                        totalTime = afr2.TotalTime;
                    }
                    else if (_audioFile is WaveChannel32 wc2)
                    {
                        totalTime = TimeSpan.FromSeconds((double)wc2.Length / wc2.WaveFormat.AverageBytesPerSecond);
                    }
                    _timelineLabel.Text = $"0:00 / {FormatTime(totalTime)}";
                }
                
                if (_progressTrackBar != null)
                    _progressTrackBar.Value = 0;

                if (_playButton != null) { _playButton.Enabled = true; _playButton.Invalidate(); }
                if (_pauseButton != null) { _pauseButton.Enabled = false; _pauseButton.Invalidate(); }
                if (_stopButton != null) { _stopButton.Enabled = false; _stopButton.Invalidate(); }

                _waveformPanel?.Invalidate();

                Log("⏹ Stopped");
            }
            catch (Exception ex)
            {
                Log($"✗ Error stopping audio: {ex.Message}");
            }
        }
        
        private void PreviewVolumeTrackBar_ValueChanged(object? sender, EventArgs e)
        {
            if (_previewVolumeTrackBar == null || _previewVolumeLabel == null) return;
            
            float volume = _previewVolumeTrackBar.Value / 100f;
            _previewVolumeLabel.Text = _previewVolumeTrackBar.Value.ToString();
            
            // Apply volume to WaveOut
            if (_waveOut != null)
            {
                _waveOut.Volume = volume;
            }
        }
        
        private void ProgressTrackBar_Scroll(object? sender, EventArgs e)
        {
            if (_progressTrackBar == null || _audioFile == null) return;
            
            try
            {
                // Seek to the position
                float position = _progressTrackBar.Value / 1000f;
                
                if (_audioFile is AudioFileReader afr)
                {
                    afr.Position = (long)(afr.Length * position);
                }
                else if (_audioFile is WaveChannel32 wc)
                {
                    wc.Position = (long)(wc.Length * position);
                }
            }
            catch (Exception ex)
            {
                Log($"✗ Error seeking: {ex.Message}");
            }
        }
        
        private string FormatTime(TimeSpan time)
        {
            return $"{(int)time.TotalMinutes}:{time.Seconds:D2}";
        }
        
        private void ProgressUpdateTimer_Tick(object? sender, EventArgs e)
        {
            if (_waveOut == null || _audioFile == null || _progressTrackBar == null) return;
            if (!_isPlaying || _isPaused) return;
            
            try
            {
                long position = 0;
                long length = 1;
                
                if (_audioFile is AudioFileReader afr)
                {
                    position = afr.Position;
                    length = afr.Length;
                }
                else if (_audioFile is WaveChannel32 wc)
                {
                    position = wc.Position;
                    length = wc.Length;
                }
                
                int progressValue = (int)((double)position / length * 1000);
                _progressTrackBar.Value = Math.Min(progressValue, 1000);
                
                // Update timeline label
                if (_timelineLabel != null)
                {
                    TimeSpan currentTime = TimeSpan.Zero;
                    TimeSpan totalTime = TimeSpan.Zero;
                    
                    if (_audioFile is AudioFileReader afr2)
                    {
                        currentTime = afr2.CurrentTime;
                        totalTime = afr2.TotalTime;
                    }
                    else if (_audioFile is WaveChannel32 wc2)
                    {
                        currentTime = TimeSpan.FromSeconds((double)wc2.Position / wc2.WaveFormat.AverageBytesPerSecond);
                        totalTime = TimeSpan.FromSeconds((double)wc2.Length / wc2.WaveFormat.AverageBytesPerSecond);
                    }
                    
                    _timelineLabel.Text = $"{FormatTime(currentTime)} / {FormatTime(totalTime)}";
                }
                
                // Invalidate waveform to show playback position
                _waveformPanel?.Invalidate();
            }
            catch { /* Ignore errors during update */ }
        }

        private void LoadAudioForPreview(string filePath)
        {
            try
            {
                // Dispose previous player
                _waveOut?.Stop();
                _waveOut?.Dispose();
                if (_audioFile is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                _waveOut = null;
                _audioFile = null;
                
                // Check if file is WAV or MP3
                string ext = Path.GetExtension(filePath).ToLower();
                if (ext != ".wav" && ext != ".mp3")
                {
                    Log($"⚠ Only WAV and MP3 files are supported for playback (got {ext})");
                    if (_playButton != null) { _playButton.Enabled = false; _playButton.Invalidate(); }
                    if (_pauseButton != null) { _pauseButton.Enabled = false; _pauseButton.Invalidate(); }
                    if (_stopButton != null) { _stopButton.Enabled = false; _stopButton.Invalidate(); }
                    return;
                }
                
                // Try MediaFoundationReader first (more reliable, supports MP3 and compressed WAV)
                try
                {
                    var reader = new MediaFoundationReader(filePath);
                    _audioFile = new WaveChannel32(reader) { PadWithZeroes = false };
                    
                    // Add pitch shifting
                    var sampleProvider = _audioFile.ToSampleProvider();
                    var pitchValue = _pitchTrackBar != null ? _pitchTrackBar.Value / 100.0f : 1.0f;
                    _pitchShifter = new SmbPitchShiftingSampleProvider(sampleProvider);
                    ((SmbPitchShiftingSampleProvider)_pitchShifter).PitchFactor = pitchValue;
                    
                    Log($"✓ Audio loaded with MediaFoundation ({ext.ToUpper()} format)");
                }
                catch
                {
                    // Fallback to AudioFileReader
                    var reader = new AudioFileReader(filePath);
                    _audioFile = reader;
                    
                    // Add pitch shifting
                    var pitchValue = _pitchTrackBar != null ? _pitchTrackBar.Value / 100.0f : 1.0f;
                    _pitchShifter = new SmbPitchShiftingSampleProvider(reader);
                    ((SmbPitchShiftingSampleProvider)_pitchShifter).PitchFactor = pitchValue;
                    
                    Log($"✓ Audio loaded for preview ({ext.ToUpper()} format)");
                }
                
                _waveOut = new WaveOutEvent();
                _waveOut.Init(_pitchShifter);
                
                // Add playback stopped event for auto-loop
                _waveOut.PlaybackStopped += WaveOut_PlaybackStopped;
                
                // Set initial volume to 50%
                if (_previewVolumeTrackBar != null)
                {
                    _waveOut.Volume = _previewVolumeTrackBar.Value / 100f;
                }
                
                // Enable controls
                if (_playButton != null) { _playButton.Enabled = true; _playButton.Invalidate(); }
                if (_pauseButton != null) { _pauseButton.Enabled = false; _pauseButton.Invalidate(); }
                if (_stopButton != null) { _stopButton.Enabled = false; _stopButton.Invalidate(); }
                if (_progressTrackBar != null) _progressTrackBar.Enabled = true;
                
                // Initialize timeline label
                if (_timelineLabel != null && _audioFile != null)
                {
                    TimeSpan totalTime = TimeSpan.Zero;
                    if (_audioFile is AudioFileReader afr2)
                    {
                        totalTime = afr2.TotalTime;
                    }
                    else if (_audioFile is WaveChannel32 wc2)
                    {
                        totalTime = TimeSpan.FromSeconds((double)wc2.Length / wc2.WaveFormat.AverageBytesPerSecond);
                    }
                    _timelineLabel.Text = $"0:00 / {FormatTime(totalTime)}";
                    
                    // Set default end time for cutting to total duration
                    if (_cutEndMinute != null && _cutEndSecond != null)
                    {
                        _cutEndMinute.Value = (int)totalTime.TotalMinutes;
                        _cutEndSecond.Value = totalTime.Seconds;
                    }
                }
                
                // Enable cut button
                if (_applyCutButton != null) _applyCutButton.Enabled = true;

                // Load waveform data
                Task.Run(() => LoadWaveformData(filePath));
                
                // Auto-play the file
                _waveOut.Play();
                _isPlaying = true;
                _isPaused = false;
                if (_pauseButton != null) { _pauseButton.Enabled = true; _pauseButton.Invalidate(); }
                if (_playButton != null) { _playButton.Enabled = false; _playButton.Invalidate(); }
                
                // Start progress timer
                _progressUpdateTimer?.Start();
                
                Log("▶ Playing automatically...");
            }
            catch (Exception ex)
            {
                Log($"✗ Error loading audio: {ex.Message}");
                
                // Show helpful error message
                string errorMsg = "Unable to load audio file.";
                if (ex.Message.Contains("NoDriver") || ex.Message.Contains("acm"))
                {
                    errorMsg = "Audio codec not available. Try using Windows Media Player to play the file first,\nor convert to a different format.";
                }
                Log($"💡 {errorMsg}");
                
                _waveOut?.Dispose();
                if (_audioFile is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                _waveOut = null;
                _audioFile = null;
                
                if (_playButton != null) { _playButton.Enabled = false; _playButton.Invalidate(); }
                if (_pauseButton != null) { _pauseButton.Enabled = false; _pauseButton.Invalidate(); }
                if (_stopButton != null) { _stopButton.Enabled = false; _stopButton.Invalidate(); }
            }
        }
        
        private void LoadWaveformData(string filePath)
        {
            try
            {
                // This is a simplified waveform extraction
                // For better visualization, consider using NAudio library
                if (filePath.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                {
                    using var reader = new BinaryReader(File.OpenRead(filePath));
                    
                    // Skip WAV header (44 bytes)
                    reader.BaseStream.Seek(44, SeekOrigin.Begin);
                    
                    var samples = new List<float>();
                    var maxSamples = 2000; // Limit samples for performance
                    var totalSamples = (reader.BaseStream.Length - 44) / 2; // 16-bit samples
                    var sampleStep = Math.Max(1, (int)(totalSamples / maxSamples));
                    
                    for (int i = 0; i < totalSamples && samples.Count < maxSamples; i += sampleStep)
                    {
                        if (reader.BaseStream.Position >= reader.BaseStream.Length) break;
                        var sample = reader.ReadInt16() / 32768f; // Normalize to -1 to 1
                        samples.Add(Math.Abs(sample));
                    }
                    
                    _waveformData = samples.ToArray();
                }
                else
                {
                    // For MP3 or unsupported formats, create a simple placeholder
                    _waveformData = new float[100];
                    var random = new Random();
                    for (int i = 0; i < _waveformData.Length; i++)
                    {
                        _waveformData[i] = (float)random.NextDouble() * 0.5f;
                    }
                }
                
                // Update UI on main thread
                _waveformPanel?.Invoke((MethodInvoker)(() =>
                {
                    if (_waveformScrollBar != null)
                    {
                        _waveformScrollBar.Maximum = Math.Max(0, _waveformData.Length - 100);
                        _waveformScrollBar.LargeChange = 100;
                        _waveformScrollBar.SmallChange = 10;
                    }
                    _waveformPanel?.Invalidate();
                }));
            }
            catch (Exception ex)
            {
                Invoke((MethodInvoker)(() => Log($"✗ Error loading waveform: {ex.Message}")));
            }
        }

        private void WaveformPanel_Paint(object? sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            var width = _waveformPanel?.Width ?? 280;
            var height = _waveformPanel?.Height ?? 120;
            var centerY = height / 2f;

            // Draw progress indicator (if audio is loaded)
            if (_audioFile != null && _progressTrackBar != null)
            {
                var progressX = (width * _progressTrackBar.Value) / 1000f;
                using var progressBrush = new SolidBrush(Color.FromArgb(80, 255, 255, 255));
                g.FillRectangle(progressBrush, 0, 0, progressX, height);
            }

            if (_waveformData == null || _waveformData.Length == 0)
            {
                // Draw placeholder text
                using var font = new Font("Arial", 10);
                using var brush = new SolidBrush(Color.FromArgb(100, 100, 100));
                var text = "No audio loaded";
                var size = g.MeasureString(text, font);
                g.DrawString(text, font, brush, (width - size.Width) / 2, (height - size.Height) / 2);
                return;
            }

            // Always show the entire waveform by mapping all samples to the panel width
            var totalSamples = _waveformData.Length;
            var xStep = (float)width / totalSamples;

            using var pen = new Pen(Color.FromArgb(0, 200, 255), 2f);

            for (int i = 0; i < totalSamples - 1; i++)
            {
                var x1 = i * xStep;
                var x2 = (i + 1) * xStep;
                var y1 = centerY - (_waveformData[i] * centerY * 0.95f);
                var y2 = centerY - (_waveformData[Math.Min(i + 1, totalSamples - 1)] * centerY * 0.95f);

                g.DrawLine(pen, x1, y1, x2, y2);
                g.DrawLine(pen, x1, centerY + (centerY - y1), x2, centerY + (centerY - y2));
            }

            // Draw center line
            using var centerPen = new Pen(Color.FromArgb(80, 80, 80), 1f);
            g.DrawLine(centerPen, 0, centerY, width, centerY);
            
            // Draw playback position line (wider and more visible for easy clicking)
            if (_audioFile != null && _progressTrackBar != null)
            {
                var progressX = (width * _progressTrackBar.Value) / 1000f;
                
                // Draw semi-transparent area around the line for easier clicking
                using var areaBrush = new SolidBrush(Color.FromArgb(40, 255, 255, 0));
                float clickWidth = 8f;
                g.FillRectangle(areaBrush, progressX - clickWidth/2, 0, clickWidth, height);
                
                // Draw the main position line
                using var positionPen = new Pen(Color.FromArgb(255, 255, 0), 3f);
                g.DrawLine(positionPen, progressX, 0, progressX, height);
            }
        }

        private void WaveformPanel_MouseClick(object? sender, MouseEventArgs e)
        {
            SeekToWaveformPosition(e.X);
        }
        
        private void WaveformPanel_MouseDown(object? sender, MouseEventArgs e)
        {
            if (_audioFile != null)
            {
                _isDraggingWaveform = true;
                SeekToWaveformPosition(e.X);
            }
        }
        
        private void WaveformPanel_MouseMove(object? sender, MouseEventArgs e)
        {
            if (_isDraggingWaveform && e.Button == MouseButtons.Left)
            {
                SeekToWaveformPosition(e.X);
            }
        }
        
        private void WaveformPanel_MouseUp(object? sender, MouseEventArgs e)
        {
            _isDraggingWaveform = false;
        }
        
        private void SeekToWaveformPosition(int mouseX)
        {
            if (_audioFile == null || _waveformPanel == null || _progressTrackBar == null) return;
            
            try
            {
                // Calculate position as percentage
                var clickPercent = Math.Max(0, Math.Min(1, (float)mouseX / _waveformPanel.Width));
                
                // Seek to the position in audio first
                if (_audioFile is AudioFileReader afr)
                {
                    afr.Position = (long)(afr.Length * clickPercent);
                }
                else if (_audioFile is WaveChannel32 wc)
                {
                    wc.Position = (long)(wc.Length * clickPercent);
                }
                
                // Update progress trackbar to reflect new position
                _progressTrackBar.Value = (int)(clickPercent * 1000);
                
                // Update timeline label even when paused/stopped
                if (_timelineLabel != null && _audioFile != null)
                {
                    TimeSpan currentTime = TimeSpan.Zero;
                    TimeSpan totalTime = TimeSpan.Zero;
                    
                    if (_audioFile is AudioFileReader afr2)
                    {
                        currentTime = afr2.CurrentTime;
                        totalTime = afr2.TotalTime;
                    }
                    else if (_audioFile is WaveChannel32 wc2)
                    {
                        currentTime = TimeSpan.FromSeconds((double)wc2.Position / wc2.WaveFormat.AverageBytesPerSecond);
                        totalTime = TimeSpan.FromSeconds((double)wc2.Length / wc2.WaveFormat.AverageBytesPerSecond);
                    }
                    
                    _timelineLabel.Text = $"{FormatTime(currentTime)} / {FormatTime(totalTime)}";
                }
                
                // Request redraw of waveform
                _waveformPanel?.Invalidate();
            }
            catch (Exception ex)
            {
                Log($"✗ Error seeking: {ex.Message}");
            }
        }

        private void WaveformScrollBar_Scroll(object? sender, ScrollEventArgs e)
        {
            _waveformScrollPosition = e.NewValue;
            _waveformPanel?.Invalidate();
        }

        private void VolumeTrackBar_ValueChanged(object? sender, EventArgs e)
        {
            if (_volumeTrackBar != null && _volumeValueLabel != null)
            {
                var volume = _volumeTrackBar.Value / 10.0;
                _volumeValueLabel.Text = volume.ToString("F1");
            }
        }

        private void PitchTrackBar_ValueChanged(object? sender, EventArgs e)
        {
            if (_pitchTrackBar != null && _pitchValueLabel != null)
            {
                var pitch = _pitchTrackBar.Value / 100.0;
                _pitchValueLabel.Text = pitch.ToString("F2");
                
                // Update pitch in real-time if audio is playing
                if (_pitchShifter is SmbPitchShiftingSampleProvider pitchProvider)
                {
                    pitchProvider.PitchFactor = (float)pitch;
                }
            }
        }

        private void OpenFolderButton_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_cs2BasePath))
            {
                MessageBox.Show("CS2 path not detected", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (_addonNameComboBox == null || string.IsNullOrWhiteSpace(_addonNameComboBox.Text))
            {
                MessageBox.Show("Please enter or select an addon name", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var addonName = _addonNameComboBox.Text.Trim();
            var soundsFolder = Path.Combine(_cs2BasePath, "content", "csgo_addons", addonName, "sounds");

            try
            {
                Directory.CreateDirectory(soundsFolder);
                Process.Start("explorer.exe", soundsFolder);
                Log($"✓ Opened: {soundsFolder}");
            }
            catch (Exception ex)
            {
                Log($"✗ Error opening folder: {ex.Message}");
                MessageBox.Show($"Error opening folder: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyCutButton_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_soundFilePath) || !File.Exists(_soundFilePath))
            {
                MessageBox.Show("No audio file loaded", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            if (_cutStartMinute == null || _cutStartSecond == null || _cutEndMinute == null || _cutEndSecond == null)
                return;

            try
            {
                // Get start and end times in seconds
                double startTime = (double)_cutStartMinute.Value * 60 + (double)_cutStartSecond.Value;
                double endTime = (double)_cutEndMinute.Value * 60 + (double)_cutEndSecond.Value;

                if (startTime >= endTime)
                {
                    MessageBox.Show("Start time must be less than end time", "Invalid Range", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                Log($"Cutting audio from {FormatTime(TimeSpan.FromSeconds(startTime))} to {FormatTime(TimeSpan.FromSeconds(endTime))}...");

                // Stop playback
                _waveOut?.Stop();
                _progressUpdateTimer?.Stop();
                _isPlaying = false;
                _isPaused = false;

                // Create temporary output file
                string tempOutput = Path.Combine(Path.GetTempPath(), $"cut_{Guid.NewGuid()}.wav");

                // Read the audio file and trim it
                using (var reader = new AudioFileReader(_soundFilePath))
                {
                    // Seek to start position
                    reader.CurrentTime = TimeSpan.FromSeconds(startTime);
                    
                    // Calculate how many samples to read
                    double duration = endTime - startTime;
                    int samplesToRead = (int)(duration * reader.WaveFormat.SampleRate * reader.WaveFormat.Channels);
                    
                    // Read samples
                    var sampleProvider = reader.ToSampleProvider();
                    var samples = new List<float>();
                    var buffer = new float[reader.WaveFormat.SampleRate * reader.WaveFormat.Channels];
                    int samplesRead;
                    int totalRead = 0;
                    
                    while (totalRead < samplesToRead && (samplesRead = sampleProvider.Read(buffer, 0, Math.Min(buffer.Length, samplesToRead - totalRead))) > 0)
                    {
                        for (int i = 0; i < samplesRead; i++)
                        {
                            samples.Add(buffer[i]);
                        }
                        totalRead += samplesRead;
                    }
                    
                    // Write to WAV file using WaveFileWriter
                    using (var writer = new WaveFileWriter(tempOutput, new WaveFormat(reader.WaveFormat.SampleRate, reader.WaveFormat.Channels)))
                    {
                        writer.WriteSamples(samples.ToArray(), 0, samples.Count);
                    }
                }

                // Store the original if this is the first cut
                if (string.IsNullOrEmpty(_originalSoundFilePath))
                {
                    _originalSoundFilePath = _soundFilePath;
                }

                // Update the sound file path to the cut version
                _soundFilePath = tempOutput;
                
                // Reload audio for preview
                LoadAudioForPreview(_soundFilePath);
                
                // Enable undo button
                if (_undoCutButton != null) _undoCutButton.Enabled = true;
                
                // Reset cut times
                _cutStartMinute.Value = 0;
                _cutStartSecond.Value = 0;
                
                Log($"✓ Audio cut successfully! Duration: {FormatTime(TimeSpan.FromSeconds(endTime - startTime))}");
            }
            catch (Exception ex)
            {
                Log($"✗ Error cutting audio: {ex.Message}");
                MessageBox.Show($"Error cutting audio: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void UndoCutButton_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_originalSoundFilePath) || !File.Exists(_originalSoundFilePath))
            {
                MessageBox.Show("No original file to restore", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            
            try
            {
                Log("Restoring original audio...");
                
                // Restore original file path
                _soundFilePath = _originalSoundFilePath;
                _originalSoundFilePath = ""; // Clear original path
                
                // Reload original audio
                LoadAudioForPreview(_soundFilePath);
                
                // Disable undo button
                if (_undoCutButton != null) _undoCutButton.Enabled = false;
                
                Log("✓ Original audio restored");
            }
            catch (Exception ex)
            {
                Log($"✗ Error restoring original: {ex.Message}");
                MessageBox.Show($"Error restoring original: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void WaveOut_PlaybackStopped(object? sender, StoppedEventArgs e)
        {
            // Auto-restart playback when audio finishes
            if (_waveOut != null && _audioFile != null && _isPlaying && !_isPaused)
            {
                try
                {
                    // Reset position to start
                    if (_audioFile is AudioFileReader afr)
                    {
                        afr.Position = 0;
                    }
                    else if (_audioFile is WaveChannel32 wc)
                    {
                        wc.Position = 0;
                    }
                    
                    // Reset progress
                    if (_progressTrackBar != null)
                        _progressTrackBar.Value = 0;
                    
                    // Restart playback
                    _waveOut.Play();
                    _isPlaying = true;
                    
                    Log("↻ Restarting playback...");
                }
                catch
                {
                    // Ignore errors on auto-restart
                }
            }
        }

        private void AddSoundButton_Click(object? sender, EventArgs e)
        {
            // Validate inputs
            if (_addonNameComboBox == null || string.IsNullOrWhiteSpace(_addonNameComboBox.Text))
            {
                MessageBox.Show("Please enter or select an addon name", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrEmpty(_soundFilePath) || !File.Exists(_soundFilePath))
            {
                MessageBox.Show("Please select a valid sound file", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (string.IsNullOrEmpty(_cs2BasePath))
            {
                MessageBox.Show("CS2 path not detected", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (_outputNameTextBox == null || string.IsNullOrWhiteSpace(_outputNameTextBox.Text))
            {
                MessageBox.Show("Please enter an output name", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_soundNameTextBox == null || string.IsNullOrWhiteSpace(_soundNameTextBox.Text))
            {
                MessageBox.Show("Please enter a sound event name", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var addonName = _addonNameComboBox.Text.Trim();
                var outputName = _outputNameTextBox.Text.Trim();
                var soundEventName = _soundNameTextBox.Text.Trim();

                // Create sounds folder
                var soundsFolder = Path.Combine(_cs2BasePath, "content", "csgo_addons", addonName, "sounds");
                Directory.CreateDirectory(soundsFolder);
                Log($"✓ Content sounds folder: {soundsFolder}");

                // Copy sound file
                var fileExtension = Path.GetExtension(_soundFilePath);
                var destFileName = outputName + fileExtension;
                var destPath = Path.Combine(soundsFolder, destFileName);
                File.Copy(_soundFilePath, destPath, overwrite: true);
                Log($"✓ Content root file ({fileExtension}): {destPath}");

                // Compile sound file
                CompileSoundFile(destPath);

                // Update soundevents file
                var soundeventsFolder = Path.Combine(_cs2BasePath, "content", "csgo_addons", addonName, "soundevents");
                Directory.CreateDirectory(soundeventsFolder);
                var soundeventsFile = Path.Combine(soundeventsFolder, "soundevents_addon.vsndevts");
                UpdateSoundeventsFile(soundeventsFile, destFileName, soundEventName);

                // Compile soundevents file
                CompileSoundFile(soundeventsFile);

                Log($"✓ Sound added successfully! Event name: {soundEventName}");
                MessageBox.Show($"Sound added successfully!\n\nEvent name: {soundEventName}\n\nYou can now use this sound in Hammer.", 
                    "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                Log($"✗ Error adding sound: {ex.Message}");
                MessageBox.Show($"Error adding sound: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateSoundeventsFile(string soundeventsFile, string soundFileName, string eventName)
        {
            if (_soundTypeComboBox == null || _volumeTrackBar == null || _pitchTrackBar == null ||
                _distanceNearNumeric == null || _distanceNearVolumeNumeric == null ||
                _distanceMidNumeric == null || _distanceMidVolumeNumeric == null ||
                _distanceFarNumeric == null || _distanceFarVolumeNumeric == null ||
                _occlusionCheckBox == null || _occlusionIntensityNumeric == null)
            {
                throw new InvalidOperationException("Controls not initialized");
            }

            var soundType = _soundTypeComboBox.SelectedItem?.ToString() ?? "csgo_mega";
            var volume = _volumeTrackBar.Value / 10.0f; // Convert from 0-100 to 0.0-10.0
            var pitch = _pitchTrackBar.Value / 100.0f; // Convert from 50-200 to 0.5-2.0
            var distNear = (float)_distanceNearNumeric.Value;
            var distNearVol = (float)_distanceNearVolumeNumeric.Value;
            var distMid = (float)_distanceMidNumeric.Value;
            var distMidVol = (float)_distanceMidVolumeNumeric.Value;
            var distFar = (float)_distanceFarNumeric.Value;
            var distFarVol = (float)_distanceFarVolumeNumeric.Value;
            var occlusion = _occlusionCheckBox.Checked;
            var occlusionIntensity = (int)_occlusionIntensityNumeric.Value;

            var vsndFileName = Path.GetFileNameWithoutExtension(soundFileName) + ".vsnd";
            var vsndReference = $"sounds/{vsndFileName}";

            // Generate soundevent entry
            var soundeventEntry = new StringBuilder();
            soundeventEntry.AppendLine($"\t\"{eventName}\" =");
            soundeventEntry.AppendLine("\t{");
            soundeventEntry.AppendLine($"\t\ttype = \"{soundType}\"");
            soundeventEntry.AppendLine($"\t\tvsnd_files_track_01 = \"{vsndReference}\"");
            soundeventEntry.AppendLine($"\t\tvolume = {volume:F1}");
            soundeventEntry.AppendLine($"\t\tpitch = {pitch:F2}");
            soundeventEntry.AppendLine("\t\tuse_distance_volume_mapping_curve = true");
            soundeventEntry.AppendLine("\t\tdistance_volume_mapping_curve = ");
            soundeventEntry.AppendLine("\t\t[");
            soundeventEntry.AppendLine($"\t\t\t[{distNear:F1}, {distNearVol:F1}, 0.000, 0.000, 1.000, 1.000,],");
            soundeventEntry.AppendLine($"\t\t\t[{distMid:F1}, {distMidVol:F1}, 0.000, 0.000, 1.000, 1.000],");
            soundeventEntry.AppendLine($"\t\t\t[{distFar:F1}, {distFarVol:F1}, 0.0, 0.0, 1.0, 1.0],");
            soundeventEntry.AppendLine("\t\t]");
            soundeventEntry.AppendLine($"\t\tocclusion = {occlusion.ToString().ToLower()}");
            soundeventEntry.AppendLine($"\t\tocclusion_intensity = {occlusionIntensity}");
            soundeventEntry.AppendLine("\t}");

            if (File.Exists(soundeventsFile))
            {
                // File exists, update it
                var content = File.ReadAllText(soundeventsFile);

                // Remove existing entry with the same name if it exists
                var pattern = $@"\t""{Regex.Escape(eventName)}""\s*=\s*\{{[^}}]*\}}\r?\n?";
                content = Regex.Replace(content, pattern, "", RegexOptions.Singleline);

                // Find the last closing brace and insert before it
                var lastBraceIndex = content.LastIndexOf('}');
                if (lastBraceIndex != -1)
                {
                    content = content.Substring(0, lastBraceIndex) + soundeventEntry.ToString() + content.Substring(lastBraceIndex);
                }
                else
                {
                    content += soundeventEntry.ToString() + "\n}";
                }

                File.WriteAllText(soundeventsFile, content, Encoding.UTF8);
                Log($"✓ Updated soundevents file: {soundeventsFile}");
            }
            else
            {
                // Create new file
                var header = "<!-- kv3 encoding:text:version{e21c7f3c-8a33-41c5-9977-a76d3a32aa0d} format:generic:version{7412167c-06e9-4698-aff2-e63eb59037e7} -->\n{\n";
                var footer = "}\n";
                var fullContent = header + soundeventEntry.ToString() + footer;
                File.WriteAllText(soundeventsFile, fullContent, Encoding.UTF8);
                Log($"✓ Created soundevents file: {soundeventsFile}");
            }
        }

        private void CompileSoundFile(string filePath)
        {
            if (string.IsNullOrEmpty(_cs2BasePath)) return;

            var resourceCompilerPath = Path.Combine(_cs2BasePath, "game", "bin", "win64", "resourcecompiler.exe");
            if (!File.Exists(resourceCompilerPath))
            {
                Log($"✗ Warning: resourcecompiler.exe not found at {resourceCompilerPath}");
                return;
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = resourceCompilerPath,
                    Arguments = $"\"{filePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };

                using var process = Process.Start(startInfo);
                if (process != null)
                {
                    process.WaitForExit(10000); // 10 second timeout
                    if (process.ExitCode == 0)
                    {
                        Log($"✓ Compiled: {Path.GetFileName(filePath)}");
                    }
                    else
                    {
                        Log($"✗ Warning: Compilation failed for {Path.GetFileName(filePath)}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"✗ Error compiling file: {ex.Message}");
            }
        }

        private void Log(string message)
        {
            LogMessage?.Invoke(message);
        }
    }
}
