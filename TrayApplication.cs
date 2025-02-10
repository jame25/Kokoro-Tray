using System;
using System.Windows.Forms;
using System.Drawing;
using NAudio.Wave;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;
using KokoroSharp;

namespace KokoroTray
{
    public class TrayApplication : IDisposable
    {
        // Windows API constants and imports for hotkeys
        private const int WM_HOTKEY = 0x0312;
        private const int MOD_ALT = 0x0001;
        private const int MOD_CONTROL = 0x0002;
        private const int MOD_SHIFT = 0x0004;
        private const int MOD_WIN = 0x0008;
        private const int MOD_NOREPEAT = 0x4000;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vlc);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private Dictionary<int, Action> hotkeyActions = new Dictionary<int, Action>();
        private IntPtr formHandle;
        private int currentHotkeyId = 1;

        private NotifyIcon trayIcon;
        private TTSServiceManager ttsService;
        private bool isPlaying = false;
        private string lastClipboardText = "";
        private bool isMonitoringClipboard = false;
        private System.Windows.Forms.Timer clipboardTimer;
        private CancellationTokenSource playbackCts;
        private readonly SynchronizationContext uiContext;
        private const float DefaultTTSSpeed = 1.0f;
        private readonly string tempWavPath;
        private ToolStripMenuItem presetsMenu;
        private ToolStripMenuItem monitoringItem;
        private ToolStripMenuItem stopSpeechItem;
        private ToolStripMenuItem pauseResumeItem;
        private readonly SemaphoreSlim ttsInitLock = new SemaphoreSlim(1, 1);
        private KokoroPlayback? kokoroPlayback;

        public TrayApplication()
        {
            Logger.Info("Starting Kokoro TTS application");
            uiContext = SynchronizationContext.Current;
            tempWavPath = Path.Combine(Path.GetTempPath(), "kokoro_tts_temp.wav");
            
            // Create an invisible form to receive hotkey messages
            var messageForm = new Form();
            messageForm.Load += (s, e) => formHandle = messageForm.Handle;
            messageForm.FormClosing += (s, e) => UnregisterAllHotkeys();
            messageForm.ShowInTaskbar = false;
            messageForm.Visible = false;
            messageForm.WindowState = FormWindowState.Minimized;
            messageForm.Show();
            messageForm.Hide();

            // Override WndProc to handle hotkey messages
            Application.AddMessageFilter(new HotkeyMessageFilter(this));
            
            // Initialize UI components first
            InitializeTrayIcon();
            InitializeAudio();
            InitializeClipboardMonitor();
            
            // Enable monitoring based on settings
            ToggleClipboardMonitoring(Settings.Instance.GetSetting<bool>("MonitorClipboard", true));

            // Register hotkeys
            RegisterConfiguredHotkeys();

            // Initialize TTS service asynchronously
            Task.Run(async () => 
            {
                try 
                {
                    await InitializeTTSServiceAsync();
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to initialize TTS service in background", ex);
                    uiContext?.Post(_ => 
                    {
                        MessageBox.Show($"Failed to initialize TTS service: {ex.Message}", 
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }, null);
                }
            });
        }

        private void InitializeClipboardMonitor()
        {
            try
            {
                Logger.Info("Initializing clipboard monitor");
                clipboardTimer = new System.Windows.Forms.Timer();
                clipboardTimer.Interval = 500; // Check every 500ms
                clipboardTimer.Tick += ClipboardTimer_Tick;

                // Store initial clipboard content without reading it
                if (Clipboard.ContainsText())
                {
                    lastClipboardText = Clipboard.GetText().Trim();
                    Logger.Info($"Stored initial clipboard content ({lastClipboardText.Length} chars) without reading");
                }

                Logger.Info("Clipboard monitor initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initialize clipboard monitor", ex);
                throw;
            }
        }

        private async void ClipboardTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                clipboardTimer.Stop(); // Pause timer while processing
                if (Clipboard.ContainsText())
                {
                    string clipText = Clipboard.GetText().Trim();
                    int minLength = Settings.Instance.GetSetting<int>("MinimumTextLength", 1);
                    int maxLength = Settings.Instance.GetSetting<int>("MaximumTextLength", 5000);

                    if (!string.IsNullOrEmpty(clipText) && 
                        clipText != lastClipboardText && 
                        clipText.Length >= minLength && 
                        clipText.Length <= maxLength)
                    {
                        Logger.Info($"New text detected in clipboard ({clipText.Length} chars)");
                        lastClipboardText = clipText;
                        
                        try
                        {
                            string voice = Settings.Instance.GetSetting<string>("Voice", "af_heart");
                            float speed = Settings.Instance.GetSetting<float>("Speed", 1.0f);
                            Logger.Info($"Using voice: {voice}, speed: {speed}");
                            await PlayTTSAsync(clipText, voice, speed);
                        }
                        catch (Exception ex)
                        {
                            Logger.Error("Error during TTS playback", ex);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error processing clipboard text", ex);
            }
            finally
            {
                if (isMonitoringClipboard)
                {
                    clipboardTimer.Start();
                }
            }
        }

        private void ToggleClipboardMonitoring(bool enable)
        {
            if (enable && !isMonitoringClipboard)
            {
                Logger.Info("Starting clipboard monitoring");
                clipboardTimer.Start();
                isMonitoringClipboard = true;
                UpdateMonitoringMenuIcon();
            }
            else if (!enable && isMonitoringClipboard)
            {
                Logger.Info("Stopping clipboard monitoring");
                clipboardTimer.Stop();
                isMonitoringClipboard = false;
                UpdateMonitoringMenuIcon();
            }
        }

        private void UpdateMonitoringMenuIcon()
        {
            if (monitoringItem != null)
            {
                monitoringItem.Checked = isMonitoringClipboard;
            }
        }

        private async Task InitializeTTSServiceAsync()
        {
            try
            {
                await ttsInitLock.WaitAsync();
                if (ttsService != null)
                {
                    return;
                }

                Logger.Info("Initializing TTS service");
                var modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "kokoro.onnx");
                if (!File.Exists(modelPath))
                {
                    throw new FileNotFoundException(
                        "Model file 'kokoro.onnx' not found. Please download it from https://github.com/taylorchu/kokoro-onnx/releases/download/v0.2.0/kokoro.onnx " +
                        "and place it in the application directory.", modelPath);
                }
                var voicesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "voices");
                ttsService = new TTSServiceManager(modelPath, voicesPath);
                Logger.Info("TTS service initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initialize TTS service", ex);
                throw;
            }
            finally
            {
                ttsInitLock.Release();
            }
        }

        private async Task EnsureTTSServiceInitialized()
        {
            if (ttsService == null)
            {
                await InitializeTTSServiceAsync();
            }
        }

        private void InitializeAudio()
        {
            try
            {
                Logger.Info("Initializing audio system");
                kokoroPlayback = new KokoroPlayback();
                kokoroPlayback.SetVolume(1.0f);
                kokoroPlayback.NicifySamples = true;
                Logger.Info("Audio system initialized successfully");
                Logger.Info("Audio settings:");
                Logger.Info($"  - Wave Format: {KokoroPlayback.waveFormat}");
                Logger.Info($"  - Nicify Samples: {kokoroPlayback.NicifySamples}");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initialize audio system", ex);
                throw;
            }
        }

        private void InitializeTrayIcon()
        {
            try
            {
                Logger.Info("Initializing system tray icon");
                trayIcon = new NotifyIcon()
                {
                    Icon = Properties.Resources.AppIcon,
                    ContextMenuStrip = new ContextMenuStrip(),
                    Visible = true,
                    Text = "Kokoro Tray"
                };

                monitoringItem = new ToolStripMenuItem("Monitoring", null, OnToggleMonitoring);
                monitoringItem.CheckOnClick = true;
                monitoringItem.Visible = Settings.Instance.GetSetting<bool>("ShowMonitoring", true);

                stopSpeechItem = new ToolStripMenuItem("Stop Speech", null, OnStopSpeech);
                stopSpeechItem.Visible = Settings.Instance.GetSetting<bool>("ShowStopSpeech", true);
                stopSpeechItem.Enabled = false;  // Start disabled since there's no playback initially

                pauseResumeItem = new ToolStripMenuItem("Pause | Resume", null, OnPauseResume);
                pauseResumeItem.Visible = Settings.Instance.GetSetting<bool>("ShowPauseResume", false);  // Hidden by default
                pauseResumeItem.Enabled = false;  // Disabled by default

                // Create Presets submenu
                presetsMenu = new ToolStripMenuItem("Presets");
                UpdatePresetsMenu();  // Initialize the presets menu

                trayIcon.ContextMenuStrip.Items.Add(monitoringItem);
                trayIcon.ContextMenuStrip.Items.Add(stopSpeechItem);
                trayIcon.ContextMenuStrip.Items.Add(pauseResumeItem);
                trayIcon.ContextMenuStrip.Items.Add(presetsMenu);
                trayIcon.ContextMenuStrip.Items.Add("Settings", null, OnSettings);
                trayIcon.ContextMenuStrip.Items.Add("-");
                trayIcon.ContextMenuStrip.Items.Add("Exit", null, OnExit);

                trayIcon.DoubleClick += (s, e) => OnStopSpeech(s, e);
                Logger.Info("System tray icon initialized successfully");
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to initialize system tray icon", ex);
                throw;
            }
        }

        private void UpdatePresetsMenu()
        {
            try
            {
                presetsMenu.DropDownItems.Clear();
                int enabledPresetCount = 0;
                string currentPreset = Settings.Instance.GetSetting<string>("CurrentPreset", "");

                for (int i = 0; i < 4; i++)
                {
                    bool isEnabled = Settings.Instance.GetSetting<bool>($"Preset{i + 1}Enabled", i < 2);
                    if (isEnabled)
                    {
                        enabledPresetCount++;
                        string presetName = Settings.Instance.GetSetting<string>($"Preset{i + 1}Name", $"Preset {i + 1}");
                        var item = new ToolStripMenuItem(presetName, null, OnPresetSelected)
                        {
                            Tag = i + 1,  // Store preset number
                            Checked = presetName == currentPreset
                        };
                        presetsMenu.DropDownItems.Add(item);
                    }
                }

                // Hide the menu if only one or no presets are enabled
                presetsMenu.Visible = enabledPresetCount > 1;
            }
            catch (Exception ex)
            {
                Logger.Error("Error updating presets menu", ex);
            }
        }

        private void OnPresetSelected(object sender, EventArgs e)
        {
            try
            {
                var menuItem = sender as ToolStripMenuItem;
                if (menuItem != null)
                {
                    int presetNumber = (int)menuItem.Tag;
                    string presetName = Settings.Instance.GetSetting<string>($"Preset{presetNumber}Name", $"Preset {presetNumber}");
                    string voice = Settings.Instance.GetSetting<string>($"Preset{presetNumber}Voice", "af_heart");
                    string speed = Settings.Instance.GetSetting<string>($"Preset{presetNumber}Speed", "1.0");

                    // Save current preset
                    Settings.Instance.SetSetting("CurrentPreset", presetName);
                    Settings.Instance.SetSetting("Voice", voice);
                    Settings.Instance.SetSetting("Speed", float.Parse(speed));

                    // Update checkmarks
                    foreach (ToolStripMenuItem item in presetsMenu.DropDownItems)
                    {
                        item.Checked = item == menuItem;
                    }

                    Logger.Info($"Switched to preset: {presetName} (Voice: {voice}, Speed: {speed})");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error selecting preset", ex);
                MessageBox.Show($"Error selecting preset: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnToggleMonitoring(object sender, EventArgs e)
        {
            var menuItem = sender as ToolStripMenuItem;
            if (menuItem != null)
            {
                ToggleClipboardMonitoring(menuItem.Checked);
            }
        }

        private void OnStopSpeech(object sender, EventArgs e)
        {
            try
            {
                Logger.Info("Stop Speech requested");
                if (ttsService?.tts != null)
                {
                    Logger.Info("Stopping TTS playback");
                    ttsService.tts.StopPlayback();
                    isPlaying = false;
                    ttsService.SetPlaybackState(false, false);
                    UpdateTrayMenuState();
                    Logger.Info("Speech playback stopped by user");
                }
                else
                {
                    Logger.Info("No TTS service available to stop");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error stopping speech playback", ex);
            }
        }

        private void UpdateTrayMenuState()
        {
            try
            {
                Logger.Info($"Updating tray menu state - Current state: isPlaying={isPlaying}, ttsService?.IsPaused={ttsService?.IsPaused}");
                
                // Update Stop Speech menu item
                if (stopSpeechItem != null)
                {
                    bool shouldBeEnabled = isPlaying && ttsService?.tts != null;
                    stopSpeechItem.Enabled = shouldBeEnabled;
                    Logger.Info($"Updated stopSpeechItem.Enabled={stopSpeechItem.Enabled} (isPlaying={isPlaying}, ttsService?.tts != null={ttsService?.tts != null})");
                }

                // Update Pause/Resume menu item
                if (pauseResumeItem != null)
                {
                    bool wasPaused = pauseResumeItem.Text == "Resume";
                    pauseResumeItem.Enabled = isPlaying;
                    pauseResumeItem.Text = (ttsService?.IsPaused ?? false) ? "Resume" : "Pause";
                    Logger.Info($"Updated pauseResumeItem - Enabled={pauseResumeItem.Enabled}, Text={pauseResumeItem.Text}, WasPaused={wasPaused}");
                }
                else
                {
                    Logger.Info("pauseResumeItem is null");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error updating tray menu state", ex);
            }
        }

        public async Task PlayTTSAsync(string text, string voice, float speed = DefaultTTSSpeed)
        {
            try
            {
                await EnsureTTSServiceInitialized();
                Logger.Info($"Starting audio generation for text with {text.Length} chars and approximately {text.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries).Length} sentences");
                Logger.Info($"Text content: {text}");
                text = ttsService.ProcessText(text);
                Logger.Info($"Using speed: {speed}x");
                
                // If text is empty after processing, return early
                if (string.IsNullOrWhiteSpace(text))
                {
                    Logger.Info("Text is empty after dictionary processing, skipping TTS");
                    return;
                }
                
                Logger.Info($"Using speed: {speed}x");
                
                var voiceObj = KokoroVoiceManager.GetVoice(voice);
                var config = new KokoroSharp.Processing.KokoroTTSPipelineConfig(new KokoroSharp.Processing.DefaultSegmentationConfig());
                config.Speed = speed;  // Set the speed before calling SpeakFast

                // Set the playing state and update menu BEFORE starting playback
                isPlaying = true;
                ttsService.SetPlaybackState(true, false);
                Logger.Info($"Set isPlaying to {isPlaying} and updated playback state");
                UpdateTrayMenuState();
                Logger.Info("Updated tray menu state before playback");

                var handle = ttsService.tts.SpeakFast(text, voiceObj, config);
                
                // Wait for completion
                var completionSource = new TaskCompletionSource<bool>();
                handle.OnSpeechCompleted += (packet) => {
                    Logger.Info("Speech completed successfully");
                    isPlaying = false;
                    ttsService.SetPlaybackState(false, false);
                    UpdateTrayMenuState();
                    completionSource.TrySetResult(true);
                };
                handle.OnSpeechCanceled += (packet) => {
                    Logger.Info("Speech was cancelled");
                    isPlaying = false;
                    ttsService.SetPlaybackState(false, false);
                    UpdateTrayMenuState();
                    completionSource.TrySetResult(false);
                };
                
                await completionSource.Task;
                Logger.Info("Playback completed");
            }
            catch (Exception ex)
            {
                Logger.Error("Error during TTS playback", ex);
                isPlaying = false;
                ttsService.SetPlaybackState(false, false);
                UpdateTrayMenuState();
                throw;
            }
        }

        private void OnSettings(object sender, EventArgs e)
        {
            Logger.Info("Opening settings dialog");
            using (var form = new SettingsForm(ttsService))
            {
                if (form.ShowDialog() == DialogResult.OK)
                {
                    Logger.Info("Settings updated");
                    UpdatePresetsMenu();  // Refresh presets menu after settings change
                    UpdateMenuItemVisibility();  // Update menu item visibility
                    RegisterConfiguredHotkeys();  // Update hotkey registrations
                    UpdateTrayMenuState();
                }
            }
        }

        private void OnExit(object sender, EventArgs e)
        {
            Logger.Info("Application exit requested");
            ToggleClipboardMonitoring(false);
            trayIcon.Visible = false;
            Application.Exit();
        }

        private void UpdateMenuItemVisibility()
        {
            if (monitoringItem != null)
                monitoringItem.Visible = Settings.Instance.GetSetting<bool>("ShowMonitoring", true);
            if (stopSpeechItem != null)
                stopSpeechItem.Visible = Settings.Instance.GetSetting<bool>("ShowStopSpeech", true);
            if (pauseResumeItem != null)
                pauseResumeItem.Visible = Settings.Instance.GetSetting<bool>("ShowPauseResume", true);
        }

        private void OnPauseResume(object sender, EventArgs e)
        {
            try
            {
                Logger.Info($"OnPauseResume called - Current state: isPlaying={isPlaying}, ttsService?.IsPaused={ttsService?.IsPaused}");
                if (isPlaying)  // Check local isPlaying state
                {
                    Logger.Info("Pause/Resume requested through menu/hotkey");
                    ttsService.TogglePauseResume();
                    UpdateTrayMenuState();
                    Logger.Info($"Pause/Resume completed - New state: isPlaying={isPlaying}, ttsService?.IsPaused={ttsService?.IsPaused}");
                }
                else
                {
                    Logger.Info("Cannot pause/resume - No active playback");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error toggling pause/resume state", ex);
            }
        }

        private class HotkeyMessageFilter : IMessageFilter
        {
            private readonly TrayApplication app;

            public HotkeyMessageFilter(TrayApplication app)
            {
                this.app = app;
            }

            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg == WM_HOTKEY)
                {
                    int hotkeyId = m.WParam.ToInt32();
                    Logger.Info($"Hotkey pressed - ID: {hotkeyId}");
                    if (app.hotkeyActions.TryGetValue(hotkeyId, out Action action))
                    {
                        Logger.Info($"Found action for hotkey {hotkeyId}, executing...");
                        action?.Invoke();
                        return true;
                    }
                    else
                    {
                        Logger.Info($"No action found for hotkey {hotkeyId}");
                    }
                }
                return false;
            }
        }

        private void RegisterConfiguredHotkeys()
        {
            UnregisterAllHotkeys();
            Logger.Info("Registering configured hotkeys");

            for (int i = 0; i < 6; i++)
            {
                int index = i;  // Capture the index for the lambda
                if (Settings.Instance.GetSetting<bool>($"Hotkey{i}Enabled", false))
                {
                    string modifier = Settings.Instance.GetSetting<string>($"Hotkey{i}Modifier", "None");
                    string key = Settings.Instance.GetSetting<string>($"Hotkey{i}Key", "");

                    Logger.Info($"Configuring hotkey {i} - Modifier: {modifier}, Key: {key}, Enabled: true");

                    if (!string.IsNullOrEmpty(key))
                    {
                        int modFlags = GetModifierFlags(modifier);
                        Keys keyCode = (Keys)Enum.Parse(typeof(Keys), key);

                        Action hotkeyAction = () =>
                        {
                            Logger.Info($"Hotkey {i} triggered - Action: {GetHotkeyActionName(index)}");
                            switch (index)
                            {
                                case 0: // Switch Preset
                                    this.SwitchToNextPreset();
                                    break;
                                case 1: // Monitoring
                                    this.ToggleClipboardMonitoring(!this.isMonitoringClipboard);
                                    Settings.Instance.SetSetting("MonitorClipboard", this.isMonitoringClipboard);
                                    break;
                                case 2: // Stop Speech
                                    this.OnStopSpeech(null, EventArgs.Empty);
                                    break;
                                case 3: // Pause | Resume
                                    this.OnPauseResume(null, EventArgs.Empty);
                                    break;
                                case 4: // Speed Increase
                                    this.AdjustSpeed(0.1f);
                                    break;
                                case 5: // Speed Decrease
                                    this.AdjustSpeed(-0.1f);
                                    break;
                            }
                        };

                        int hotkeyId = RegisterHotkey(modFlags, (int)keyCode, hotkeyAction);
                        Logger.Info($"Registered hotkey {i} with ID {hotkeyId}");
                    }
                }
                else
                {
                    Logger.Info($"Hotkey {i} is disabled");
                }
            }
        }

        private string GetHotkeyActionName(int index)
        {
            return index switch
            {
                0 => "Switch Preset",
                1 => "Monitoring",
                2 => "Stop Speech",
                3 => "Pause | Resume",
                4 => "Speed Increase",
                5 => "Speed Decrease",
                _ => "Unknown"
            };
        }

        private int GetModifierFlags(string modifier)
        {
            return modifier.ToLower() switch
            {
                "alt" => MOD_ALT,
                "ctrl" => MOD_CONTROL,
                "shift" => MOD_SHIFT,
                "win" => MOD_WIN,
                _ => 0
            };
        }

        private int RegisterHotkey(int modifiers, int key, Action action)
        {
            int id = currentHotkeyId++;
            if (RegisterHotKey(formHandle, id, modifiers | MOD_NOREPEAT, key))
            {
                hotkeyActions[id] = action;
                return id;
            }
            return -1;
        }

        private void UnregisterAllHotkeys()
        {
            foreach (int id in hotkeyActions.Keys)
            {
                UnregisterHotKey(formHandle, id);
            }
            hotkeyActions.Clear();
            currentHotkeyId = 1;
        }

        private void SwitchToNextPreset()
        {
            var enabledPresets = new List<ToolStripMenuItem>();
            foreach (ToolStripMenuItem item in presetsMenu.DropDownItems)
            {
                enabledPresets.Add(item);
            }

            if (enabledPresets.Count > 1)
            {
                int currentIndex = enabledPresets.FindIndex(x => x.Checked);
                int nextIndex = (currentIndex + 1) % enabledPresets.Count;
                OnPresetSelected(enabledPresets[nextIndex], EventArgs.Empty);
            }
        }

        private void AdjustSpeed(float delta)
        {
            float currentSpeed = Settings.Instance.GetSetting<float>("Speed", 1.0f);
            float newSpeed = Math.Max(0.5f, Math.Min(3.0f, currentSpeed + delta));
            Settings.Instance.SetSetting("Speed", newSpeed);
            Logger.Info($"Speed adjusted to: {newSpeed:F1}x");
        }

        public void Dispose()
        {
            Logger.Info("Disposing application resources");
            UnregisterAllHotkeys();
            playbackCts?.Cancel();
            playbackCts?.Dispose();
            clipboardTimer?.Dispose();
            trayIcon?.Dispose();
            kokoroPlayback?.Dispose();
            ttsService?.Dispose();
        }
    }
} 
