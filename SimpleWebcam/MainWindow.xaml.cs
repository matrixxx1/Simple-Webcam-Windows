using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Window = System.Windows.Window;

namespace SimpleWebcam;

public partial class MainWindow : Window
{
    private readonly List<string> _capturedFiles = new();
    private readonly string _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SimpleWebcam", "settings.json");
    private CancellationTokenSource? _previewTokenSource;
    private Task? _previewTask;
    private VideoCapture? _capture;
    private DispatcherTimer? _timedCaptureTimer;
    private readonly AppSettings _settings = new();
    private string _outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Simple Webcam");

    public MainWindow()
    {
        InitializeComponent();
        LoadSettings();
        InitializeCaptureSettingsUi();

        Directory.CreateDirectory(_outputFolder);
        FolderText.Text = _outputFolder;
        AddActivity("Simple Webcam is ready.");
        RefreshCameras();
        LoadGalleryFromDisk();

        if (_settings.AutoStartPreview)
        {
            _ = StartPreviewAsync();
        }
    }

    private void InitializeCaptureSettingsUi()
    {
        AutoStartCheckBox.IsChecked = _settings.AutoStartPreview;
        MirrorCheckBox.IsChecked = _settings.MirrorPreview;
        QualitySlider.Value = _settings.Quality;
        UpdateQualityLabel(_settings.Quality);

        foreach (ComboBoxItem item in FormatCombo.Items)
        {
            if (string.Equals((string)item.Tag, _settings.CaptureFormat, StringComparison.OrdinalIgnoreCase))
            {
                FormatCombo.SelectedItem = item;
                break;
            }
        }

        if (FormatCombo.SelectedItem is null)
        {
            FormatCombo.SelectedIndex = 0;
        }

        foreach (ComboBoxItem item in ResolutionList.Items)
        {
            if (string.Equals(item.Content?.ToString(), _settings.Resolution, StringComparison.OrdinalIgnoreCase))
            {
                ResolutionList.SelectedItem = item;
                break;
            }
        }

        if (ResolutionList.SelectedItem is null)
        {
            ResolutionList.SelectedIndex = 0;
        }
    }

    private void LoadGalleryFromDisk()
    {
        if (!Directory.Exists(_outputFolder))
        {
            return;
        }

        _capturedFiles.Clear();
        foreach (var file in Directory.GetFiles(_outputFolder, "*.*")
                     .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                     .OrderByDescending(File.GetCreationTime))
        {
            _capturedFiles.Add(file);
        }

        LogCaptures();
    }

    private void LoadSettings()
    {
        if (!File.Exists(_settingsPath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json);
            if (loaded is null)
            {
                return;
            }

            _settings.CaptureFormat = loaded.CaptureFormat;
            _settings.Quality = loaded.Quality;
            _settings.AutoStartPreview = loaded.AutoStartPreview;
            _settings.MirrorPreview = loaded.MirrorPreview;
            _settings.Resolution = loaded.Resolution;
            _settings.CameraIndex = loaded.CameraIndex;
        }
        catch
        {
            AddActivity("Could not read settings file, using defaults.");
        }
    }

    private void SaveSettings()
    {
        try
        {
            var folder = Path.GetDirectoryName(_settingsPath);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_settingsPath, json);
        }
        catch (Exception ex)
        {
            AddActivity($"Settings save failed: {ex.Message}");
        }
    }

    private void LogCaptures()
    {
        CapturedFilesList.Items.Clear();
        foreach (var file in _capturedFiles)
        {
            CapturedFilesList.Items.Add(Path.GetFileName(file));
        }
    }

    private void RefreshCameras()
    {
        CameraList.Items.Clear();
        bool found = false;
        int requestedIndex = _settings.CameraIndex;

        for (int idx = 0; idx < 10; idx++)
        {
            try
            {
                using var testCapture = new VideoCapture(idx, VideoCaptureAPIs.DSHOW);
                if (testCapture.IsOpened())
                {
                    found = true;
                    var width = (int)testCapture.Get(VideoCaptureProperties.FrameWidth);
                    var height = (int)testCapture.Get(VideoCaptureProperties.FrameHeight);
                    var label = $"Camera {idx} ({width}x{height})";
                    CameraList.Items.Add(new ComboBoxItem
                    {
                        Content = label,
                        Tag = idx
                    });

                    if (idx == requestedIndex)
                    {
                        CameraList.SelectedItem = CameraList.Items[^1];
                    }
                }
            }
            catch
            {
            }
        }

        if (found)
        {
            DiagnosticBanner.Visibility = Visibility.Collapsed;
            AddActivity($"Found {CameraList.Items.Count} camera device(s).");
            StartButton.IsEnabled = true;
            CaptureButton.IsEnabled = true;
            if (CameraList.SelectedIndex < 0)
            {
                CameraList.SelectedIndex = 0;
            }
        }
        else
        {
            CameraList.Items.Add(new ComboBoxItem { Content = "No camera detected" });
            CameraList.SelectedIndex = 0;
            StartButton.IsEnabled = false;
            CaptureButton.IsEnabled = false;
            StopButton.IsEnabled = false;
            AddActivity("No camera device detected.");
            ShowDiagnostic("No webcam detected. Verify camera is connected, not in use by another app, and privacy permissions allow camera access.");
        }

        RefreshButton.IsEnabled = true;
        FolderText.Text = _outputFolder;
    }

    private void ShowDiagnostic(string message)
    {
        DiagnosticBanner.Visibility = Visibility.Visible;
        DiagnosticText.Text = message;
    }

    private void ClearDiagnostic()
    {
        DiagnosticBanner.Visibility = Visibility.Collapsed;
        DiagnosticText.Text = string.Empty;
    }

    private void OpenFolderInExplorer()
    {
        try
        {
            Directory.CreateDirectory(_outputFolder);
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{_outputFolder}\"",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AddActivity($"Could not open folder: {ex.Message}");
        }
    }

    private void OnCameraSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CameraList.SelectedItem is not ComboBoxItem selected || selected.Tag is not int index)
        {
            return;
        }

        _settings.CameraIndex = index;
        SaveSettings();

        if (_capture is not null)
        {
            AddActivity("Camera changed. Restart preview to apply.");
        }
    }

    private void MirrorPreview_Changed(object sender, RoutedEventArgs e)
    {
        _settings.MirrorPreview = MirrorCheckBox.IsChecked == true;
        SaveSettings();
    }

    private void ApplyResolution(string resolution)
    {
        if (_capture is null || !_capture.IsOpened())
        {
            return;
        }

        _settings.Resolution = resolution;

        switch (resolution)
        {
            case "1280 x 720":
                _capture.Set(VideoCaptureProperties.FrameWidth, 1280);
                _capture.Set(VideoCaptureProperties.FrameHeight, 720);
                break;
            case "1920 x 1080":
                _capture.Set(VideoCaptureProperties.FrameWidth, 1920);
                _capture.Set(VideoCaptureProperties.FrameHeight, 1080);
                break;
            case "640 x 480":
                _capture.Set(VideoCaptureProperties.FrameWidth, 640);
                _capture.Set(VideoCaptureProperties.FrameHeight, 480);
                break;
            default:
                break;
        }

        SaveSettings();
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        await StartPreviewAsync();
    }

    private async Task StartPreviewAsync()
    {
        if (_capture is not null)
        {
            AddActivity("Preview already running.");
            return;
        }

        if (CameraList.SelectedItem is not ComboBoxItem selected || selected.Tag is not int index)
        {
            AddActivity("Select a valid camera before starting preview.");
            return;
        }

        _capture = new VideoCapture(index, VideoCaptureAPIs.DSHOW);
        if (!_capture.IsOpened())
        {
            AddActivity("Unable to open selected camera.");
            _capture.Dispose();
            _capture = null;
            ShowDiagnostic("Could not open camera device. The camera may be in use by another app, or your camera privacy setting may be blocked.");
            return;
        }

        ClearDiagnostic();
        ApplyResolution(GetSelectedResolution());
        _previewTokenSource = new CancellationTokenSource();
        var token = _previewTokenSource.Token;
        _previewTask = Task.Run(() => RunPreviewLoop(token), token);

        SetCaptureButtons(isRunning: true);
        AddActivity($"Preview started on {selected.Content}.");
        await Task.CompletedTask;
    }

    private string GetSelectedResolution()
    {
        return (ResolutionList.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Auto";
    }

    private void RunPreviewLoop(CancellationToken token)
    {
        try
        {
            using var frame = new Mat();
            while (!token.IsCancellationRequested)
            {
                if (_capture == null || !_capture.Read(frame) || frame.Empty())
                {
                    Thread.Sleep(33);
                    continue;
                }

                bool mirror = false;
                Dispatcher.Invoke(() => mirror = MirrorCheckBox.IsChecked == true);

                if (mirror)
                {
                    Cv2.Flip(frame, frame, FlipMode.Y);
                }

                using var snapshot = frame.Clone();
                Dispatcher.BeginInvoke(() =>
                {
                    var source = BitmapSourceConverter.ToBitmapSource(snapshot);
                    source.Freeze();
                    PreviewImage.Source = source;
                });

                Thread.Sleep(30);
            }
        }
        catch (Exception ex)
        {
            Dispatcher.BeginInvoke(() => AddActivity($"Preview error: {ex.Message}"));
        }
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        StopPreview();
    }

    private void StopPreview()
    {
        if (_previewTokenSource is not null)
        {
            _previewTokenSource.Cancel();
            _previewTokenSource.Dispose();
            _previewTokenSource = null;
        }

        _previewTask = null;
        _capture?.Release();
        _capture?.Dispose();
        _capture = null;

        if (_timedCaptureTimer is not null)
        {
            _timedCaptureTimer.Stop();
            _timedCaptureTimer = null;
            TimedCaptureEnabledCheckBox.IsChecked = false;
        }

        SetCaptureButtons(isRunning: false);
        AddActivity("Preview stopped.");
    }

    private void CaptureButton_Click(object sender, RoutedEventArgs e)
    {
        CaptureSnapshot();
    }

    private void CaptureSnapshot()
    {
        if (_capture is null || !_capture.IsOpened())
        {
            AddActivity("Start preview before capture.");
            return;
        }

        using var frame = new Mat();
        if (!_capture.Read(frame) || frame.Empty())
        {
            AddActivity("No frame available to capture yet.");
            return;
        }

        bool mirror = false;
        Dispatcher.Invoke(() => mirror = MirrorCheckBox.IsChecked == true);
        if (mirror)
        {
            Cv2.Flip(frame, frame, FlipMode.Y);
        }

        var format = GetCaptureFormat();
        var extension = format.Equals("png", StringComparison.OrdinalIgnoreCase) ? ".png" : ".jpg";
        Directory.CreateDirectory(_outputFolder);
        var filename = Path.Combine(_outputFolder, $"SimpleWebcam_{DateTime.Now:yyyyMMdd_HHmmss_fff}{extension}");
        var quality = Math.Clamp((int)QualitySlider.Value, 10, 100);

        var encodeParams = new List<int>();
        if (extension == ".jpg")
        {
            encodeParams.Add((int)ImwriteFlags.JpegQuality);
            encodeParams.Add(quality);
        }
        else
        {
            encodeParams.Add((int)ImwriteFlags.PngCompression);
            encodeParams.Add(Math.Clamp(10 - quality / 10, 0, 9));
        }

        if (!Cv2.ImWrite(filename, frame, encodeParams.ToArray()))
        {
            AddActivity("Failed to save snapshot.");
            return;
        }

        _capturedFiles.Insert(0, filename);
        while (_capturedFiles.Count > 500)
        {
            _capturedFiles.RemoveAt(_capturedFiles.Count - 1);
        }

        LogCaptures();
        AddActivity($"Saved snapshot: {Path.GetFileName(filename)}");
    }

    private string GetCaptureFormat()
    {
        var chosen = (FormatCombo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? _settings.CaptureFormat;
        if (string.IsNullOrWhiteSpace(chosen))
        {
            chosen = "jpg";
        }

        return chosen;
    }

    private void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshButton.IsEnabled = false;
        RefreshCameras();
    }

    private void ResolutionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var resolution = GetSelectedResolution();
        ApplyResolution(resolution);
    }

    private void QualitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        var quality = (int)e.NewValue;
        UpdateQualityLabel(quality);
        _settings.Quality = quality;
        SaveSettings();
    }

    private void UpdateQualityLabel(int quality)
    {
        if (QualityLabel is not null)
        {
            QualityLabel.Text = $"Quality: {quality}";
        }
    }

    private void FormatCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var format = GetCaptureFormat();
        _settings.CaptureFormat = format;
        SaveSettings();
    }

    private void AutoStartCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        _settings.AutoStartPreview = AutoStartCheckBox.IsChecked == true;
        SaveSettings();
    }

    private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFolderInExplorer();
    }

    private void OpenFolderInExplorerButton_Click(object sender, RoutedEventArgs e)
    {
        OpenFolderInExplorer();
    }

    private void TimedCaptureEnabledCheckBox_Checked(object sender, RoutedEventArgs e)
    {
        if (_capture is null)
        {
            TimedCaptureEnabledCheckBox.IsChecked = false;
            AddActivity("Start preview before enabling timed capture.");
            return;
        }

        if (!int.TryParse(IntervalTextBox.Text, out var seconds) || seconds <= 0)
        {
            seconds = 5;
            IntervalTextBox.Text = "5";
        }

        _timedCaptureTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(seconds)
        };
        _timedCaptureTimer.Tick += (_, _) => CaptureSnapshot();
        _timedCaptureTimer.Start();
        AddActivity($"Timed capture started every {seconds}s.");
    }

    private void TimedCaptureEnabledCheckBox_Unchecked(object sender, RoutedEventArgs e)
    {
        if (_timedCaptureTimer is not null)
        {
            _timedCaptureTimer.Stop();
            _timedCaptureTimer = null;
        }

        AddActivity("Timed capture stopped.");
    }

    private void DiagnoseButton_Click(object sender, RoutedEventArgs e)
    {
        RunDiagnostics();
    }

    private void RunDiagnostics()
    {
        var diagnostics = new List<string>
        {
            "Diagnostic started.",
            $"Output folder: {_outputFolder}",
            $"Auto-start: {_settings.AutoStartPreview}",
            $"Saved settings file: {_settingsPath}"
        };

        var detected = 0;
        for (int idx = 0; idx < 10; idx++)
        {
            try
            {
                using var testCapture = new VideoCapture(idx, VideoCaptureAPIs.DSHOW);
                if (testCapture.IsOpened())
                {
                    var width = (int)testCapture.Get(VideoCaptureProperties.FrameWidth);
                    var height = (int)testCapture.Get(VideoCaptureProperties.FrameHeight);
                    diagnostics.Add($"Device index {idx}: opened ({width}x{height}).");
                    detected++;
                }
                else
                {
                    diagnostics.Add($"Device index {idx}: unavailable.");
                }
            }
            catch (Exception ex)
            {
                diagnostics.Add($"Device index {idx}: error: {ex.Message}");
            }
        }

        if (detected == 0)
        {
            ShowDiagnostic("No cameras detected in diagnostics. Please check USB/camera connection, close apps using the camera, and confirm Windows Settings -> Privacy & Security -> Camera is on.");
        }
        else
        {
            ClearDiagnostic();
        }

        foreach (var item in diagnostics)
        {
            AddActivity(item);
        }
    }

    private void SetCaptureButtons(bool isRunning)
    {
        StartButton.IsEnabled = !isRunning;
        StopButton.IsEnabled = isRunning;
        RefreshButton.IsEnabled = !isRunning;
        CaptureButton.IsEnabled = isRunning;
        TimedCaptureEnabledCheckBox.IsEnabled = isRunning;
        DiagnoseButton.IsEnabled = !isRunning;
    }

    protected override void OnClosed(EventArgs e)
    {
        StopPreview();
        base.OnClosed(e);
    }

    private void AddActivity(string message)
    {
        ActivityList.Items.Insert(0, $"{DateTime.Now:t} - {message}");
        StatusText.Text = message;
    }

    private class AppSettings
    {
        public string CaptureFormat { get; set; } = "jpg";
        public int Quality { get; set; } = 90;
        public bool AutoStartPreview { get; set; }
        public bool MirrorPreview { get; set; }
        public int CameraIndex { get; set; }
        public string Resolution { get; set; } = "Auto";
    }
}
