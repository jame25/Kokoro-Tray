using System;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace KokoroTray
{
    public class CustomPanel : Panel
    {
        public CustomPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | 
                    ControlStyles.AllPaintingInWmPaint | 
                    ControlStyles.UserPaint, true);
        }
    }

    public class SettingsForm : Form
    {
        private TabControl tabControl;
        private TabPage appearanceTab;
        private TabPage hotkeysTab;
        private TabPage presetsTab;
        private Button saveButton;
        private Button cancelButton;

        // Presets tab controls
        private TextBox[] presetNameBoxes;
        private ComboBox[] presetModelBoxes;
        private ComboBox[] presetSpeedBoxes;
        private CheckBox[] presetCheckBoxes;

        // Hotkeys tab controls
        private ComboBox[] hotkeyModifiers;
        private TextBox[] hotkeyKeys;
        private CheckBox[] hotkeyEnabled;

        public SettingsForm()
        {
            InitializeComponent();
            LoadCurrentSettings();
            this.Icon = Properties.Resources.AppIcon;
        }

        private void InitializeComponent()
        {
            this.Text = "Settings";
            this.Size = new Size(500, 400);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterScreen;

            // Initialize TabControl
            tabControl = new TabControl
            {
                Location = new Point(12, 12),
                Size = new Size(460, 300)
            };

            // Create tabs
            appearanceTab = new TabPage("Appearance");
            hotkeysTab = new TabPage("Hotkeys");
            presetsTab = new TabPage("Presets");

            InitializeAppearanceTab();
            InitializeHotkeysTab();
            InitializePresetsTab();

            // Add tabs to TabControl
            tabControl.TabPages.Add(appearanceTab);
            tabControl.TabPages.Add(hotkeysTab);
            tabControl.TabPages.Add(presetsTab);

            // Set Presets tab as default
            tabControl.SelectedTab = presetsTab;

            // Buttons
            saveButton = new Button
            {
                Text = "Save",
                DialogResult = DialogResult.OK,
                Location = new Point(297, 325),
                Size = new Size(80, 25)
            };
            saveButton.Click += SaveButton_Click;

            cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Location = new Point(392, 325),
                Size = new Size(80, 25)
            };

            // Add controls to form
            this.Controls.AddRange(new Control[] {
                tabControl,
                saveButton,
                cancelButton
            });

            this.AcceptButton = saveButton;
            this.CancelButton = cancelButton;
        }

        private void InitializeAppearanceTab()
        {
            // Title label
            var titleLabel = new Label
            {
                Text = "Show/Hide Menu Items:",
                Location = new Point(10, 15),
                Size = new Size(200, 20),
                Font = new Font(this.Font, FontStyle.Bold)
            };

            // Menu item checkboxes
            var monitoringCheckbox = new CheckBox
            {
                Text = "Monitoring",
                Location = new Point(20, 45),
                Size = new Size(200, 20),
                Checked = Settings.Instance.GetSetting<bool>("ShowMonitoring", true)
            };

            var stopSpeechCheckbox = new CheckBox
            {
                Text = "Stop Speech",
                Location = new Point(20, 70),
                Size = new Size(200, 20),
                Checked = Settings.Instance.GetSetting<bool>("ShowStopSpeech", true)
            };

            var pauseResumeCheckbox = new CheckBox
            {
                Text = "Pause | Resume",
                Location = new Point(20, 95),
                Size = new Size(200, 20),
                Checked = Settings.Instance.GetSetting<bool>("ShowPauseResume", false)  // Explicitly set default to false
            };

            // Add event handlers to save settings when changed
            monitoringCheckbox.CheckedChanged += (s, e) => 
            {
                Settings.Instance.SetSetting("ShowMonitoring", monitoringCheckbox.Checked);
            };
            stopSpeechCheckbox.CheckedChanged += (s, e) => 
            {
                Settings.Instance.SetSetting("ShowStopSpeech", stopSpeechCheckbox.Checked);
            };
            pauseResumeCheckbox.CheckedChanged += (s, e) => 
            {
                Settings.Instance.SetSetting("ShowPauseResume", pauseResumeCheckbox.Checked);
            };

            // Add controls to the tab
            appearanceTab.Controls.AddRange(new Control[] {
                titleLabel,
                monitoringCheckbox,
                stopSpeechCheckbox,
                pauseResumeCheckbox
            });
        }

        private void InitializeHotkeysTab()
        {
            // Create hotkey rows
            var hotkeyLabels = new[] {
                "Switch Preset:",
                "Monitoring:",
                "Stop Speech:",
                "Pause | Resume:",
                "Speed Increase:",
                "Speed Decrease:"
            };

            // Initialize arrays for controls
            hotkeyModifiers = new ComboBox[hotkeyLabels.Length];
            hotkeyKeys = new TextBox[hotkeyLabels.Length];
            hotkeyEnabled = new CheckBox[hotkeyLabels.Length];

            for (int i = 0; i < hotkeyLabels.Length; i++)
            {
                // Label
                var label = new Label
                {
                    Text = hotkeyLabels[i],
                    Location = new Point(10, 15 + (i * 30)),
                    Size = new Size(100, 23),
                    TextAlign = ContentAlignment.MiddleLeft
                };

                // Modifier ComboBox
                hotkeyModifiers[i] = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Location = new Point(110, 15 + (i * 30)),
                    Size = new Size(70, 23)
                };
                hotkeyModifiers[i].Items.AddRange(new object[] { "None", "Alt", "Ctrl", "Shift", "Win" });
                hotkeyModifiers[i].SelectedItem = "Alt";  // Default to Alt

                // Key TextBox
                hotkeyKeys[i] = new TextBox
                {
                    Location = new Point(190, 15 + (i * 30)),
                    Size = new Size(50, 23),
                    ReadOnly = true  // Will be populated by actual key press
                };
                
                // Handle key input
                int index = i;  // Capture the index for the lambda
                hotkeyKeys[i].KeyDown += (s, e) =>
                {
                    e.SuppressKeyPress = true;  // Prevent beep
                    if (e.KeyCode != Keys.ShiftKey && e.KeyCode != Keys.ControlKey && 
                        e.KeyCode != Keys.Alt && e.KeyCode != Keys.Menu)
                    {
                        hotkeyKeys[index].Text = e.KeyCode.ToString();
                    }
                };

                // Checkbox
                hotkeyEnabled[i] = new CheckBox
                {
                    Location = new Point(250, 15 + (i * 30)),
                    Size = new Size(20, 23),
                    Checked = false  // Disabled by default
                };

                // Enable/disable controls based on checkbox
                int controlIndex = i;  // Capture the index for the lambda
                hotkeyEnabled[i].CheckedChanged += (s, e) =>
                {
                    bool enabled = hotkeyEnabled[controlIndex].Checked;
                    hotkeyModifiers[controlIndex].Enabled = enabled;
                    hotkeyKeys[controlIndex].Enabled = enabled;
                };

                // Set initial enabled state
                hotkeyModifiers[i].Enabled = false;
                hotkeyKeys[i].Enabled = false;

                hotkeysTab.Controls.AddRange(new Control[] { 
                    label, 
                    hotkeyModifiers[i], 
                    hotkeyKeys[i], 
                    hotkeyEnabled[i] 
                });
            }
        }

        private void InitializePresetsTab()
        {
            const int numPresets = 4;
            const int startY = 35;
            const int rowHeight = 30;
            const int presetPanelHeight = 25;

            presetNameBoxes = new TextBox[numPresets];
            presetModelBoxes = new ComboBox[numPresets];
            presetSpeedBoxes = new ComboBox[numPresets];
            presetCheckBoxes = new CheckBox[numPresets];
            Panel[] presetPanels = new Panel[numPresets];
            PaintEventHandler[] paintHandlers = new PaintEventHandler[numPresets];

            // Add column headers
            var headers = new[] { "Name", "Voice", "Speed" };
            var headerWidths = new[] { 100, 200, 40 };  // Increased from 36 to 40 (additional 10%)
            for (int i = 0; i < headers.Length; i++)
            {
                var header = new Label
                {
                    Text = headers[i],
                    Location = new Point(10 + (i > 0 ? headerWidths.Take(i).Sum() + 10 * i : 0), 10),
                    Size = new Size(headerWidths[i], 20)
                };
                presetsTab.Controls.Add(header);
            }

            // Get available voices from the voices directory
            var voicesPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "voices");
            var availableVoices = Directory.Exists(voicesPath) 
                ? Directory.GetFiles(voicesPath, "*.npy")
                    .Select(path => Path.GetFileNameWithoutExtension(path))
                    .OrderBy(name => name)
                    .ToArray()
                : new[] { "af_heart" };  // Fallback to default if directory doesn't exist

            // Create rows for each preset
            for (int i = 0; i < numPresets; i++)
            {
                int yPos = startY + (rowHeight * i);

                // Create panel for the preset row
                presetPanels[i] = new CustomPanel
                {
                    Location = new Point(5, yPos - 1),
                    Size = new Size(440, presetPanelHeight),
                    BorderStyle = BorderStyle.None,
                    BackColor = SystemColors.Control
                };
                presetsTab.Controls.Add(presetPanels[i]);

                // Preset name textbox
                presetNameBoxes[i] = new TextBox
                {
                    Location = new Point(5, 1),
                    Size = new Size(headerWidths[0], 23),
                    Text = $"Preset {i + 1}"
                };

                // Model combobox
                presetModelBoxes[i] = new ComboBox
                {
                    Location = new Point(115, 1),
                    Size = new Size(headerWidths[1], 23),
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                presetModelBoxes[i].Items.AddRange(availableVoices);
                presetModelBoxes[i].SelectedItem = i == 0 ? "af_heart" : availableVoices.FirstOrDefault();

                // Speed combobox
                presetSpeedBoxes[i] = new ComboBox
                {
                    Location = new Point(325, 1),
                    Size = new Size(headerWidths[2], 23),
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                presetSpeedBoxes[i].Items.AddRange(new object[] { 
                    "0.5", "0.6", "0.7", "0.8", "0.9", "1.0",
                    "1.1", "1.2", "1.3", "1.4", "1.5", "1.6", "1.7", "1.8", "1.9", "2.0",
                    "2.1", "2.2", "2.3", "2.4", "2.5", "2.6", "2.7", "2.8", "2.9", "3.0"
                });
                presetSpeedBoxes[i].SelectedItem = i == 0 ? "1.0" : "1.0";  // Default to 1.0 speed for all presets

                // Add controls to tab
                presetPanels[i].Controls.AddRange(new Control[] {
                    presetNameBoxes[i],
                    presetModelBoxes[i],
                    presetSpeedBoxes[i]
                });

                // Create checkbox first
                presetCheckBoxes[i] = new CheckBox
                {
                    Text = (i + 1).ToString(),
                    Location = new Point(15 + (i * 30), startY + (rowHeight * numPresets) + 20),
                    Size = new Size(30, 20),
                    Checked = Settings.Instance.GetSetting<bool>($"Preset{i + 1}Enabled", i == 0)  // Only first preset enabled by default
                };
                // Add event handler for checkbox state changes
                int presetIndex = i;  // Capture the index for the lambda
                presetCheckBoxes[i].CheckedChanged += (s, e) =>
                {
                    bool isEnabled = ((CheckBox)s).Checked;
                    presetNameBoxes[presetIndex].Enabled = isEnabled;
                    presetModelBoxes[presetIndex].Enabled = isEnabled;
                    presetSpeedBoxes[presetIndex].Enabled = isEnabled;
                    UpdateActivePresetVisual();  // Update the visual state
                };
                presetsTab.Controls.Add(presetCheckBoxes[i]);
            }

            // Initialize enabled state of controls based on checkbox state
            for (int i = 0; i < numPresets; i++)
            {
                bool isEnabled = presetCheckBoxes[i].Checked;
                presetNameBoxes[i].Enabled = isEnabled;
                presetModelBoxes[i].Enabled = isEnabled;
                presetSpeedBoxes[i].Enabled = isEnabled;
            }

            // Update active preset visual feedback
            void UpdateActivePresetVisual()
            {
                string currentPreset = Settings.Instance.GetSetting<string>("CurrentPreset", "");
                for (int i = 0; i < numPresets; i++)
                {
                    if (presetPanels[i] != null && presetCheckBoxes[i] != null)
                    {
                        // Remove existing paint handler if any
                        if (paintHandlers[i] != null)
                        {
                            presetPanels[i].Paint -= paintHandlers[i];
                            paintHandlers[i] = null;
                        }

                        bool isActive = presetNameBoxes[i].Text == currentPreset && presetCheckBoxes[i].Checked;
                        if (isActive)
                        {
                            paintHandlers[i] = (s, e) =>
                            {
                                var panel = (Panel)s;
                                e.Graphics.Clear(panel.BackColor);
                                using (var pen = new Pen(Color.FromArgb(40, 180, 40), 2.4f))
                                {
                                    // Calculate inset border (9% from each edge - 4.5% per side)
                                    int inset = panel.Width / 22;  // ~4.5% from each side = ~9% total
                                    int width = panel.Width - (inset * 2);
                                    int height = panel.Height - (inset * 2);
                                    e.Graphics.DrawRectangle(pen, inset, inset, width - 1, height - 1);
                                }
                            };
                            presetPanels[i].Paint += paintHandlers[i];
                            presetPanels[i].Invalidate();  // Force redraw
                        }
                        else
                        {
                            presetPanels[i].Invalidate();  // Force redraw for non-active panels too
                        }
                    }
                }
            }

            // Call initially
            UpdateActivePresetVisual();

            // Update visual when preset names change
            foreach (var textBox in presetNameBoxes)
            {
                textBox.TextChanged += (s, e) => UpdateActivePresetVisual();
            }

            // Subscribe to Settings changes to update the visual in real-time
            Settings.Instance.SettingChanged += (sender, key) =>
            {
                if (key == "CurrentPreset")
                {
                    if (this.InvokeRequired)
                    {
                        this.Invoke(new Action(UpdateActivePresetVisual));
                    }
                    else
                    {
                        UpdateActivePresetVisual();
                    }
                }
            };
        }

        private void LoadCurrentSettings()
        {
            // Load preset settings
            for (int i = 0; i < presetNameBoxes.Length; i++)
            {
                presetNameBoxes[i].Text = Settings.Instance.GetSetting<string>($"Preset{i + 1}Name", $"Preset {i + 1}");
                presetModelBoxes[i].SelectedItem = Settings.Instance.GetSetting<string>($"Preset{i + 1}Voice", i == 0 ? "af_heart" : "af_bella");
                presetSpeedBoxes[i].SelectedItem = Settings.Instance.GetSetting<string>($"Preset{i + 1}Speed", "1.0");
                presetCheckBoxes[i].Checked = Settings.Instance.GetSetting<bool>($"Preset{i + 1}Enabled", i == 0);
            }

            // Load hotkey settings
            if (hotkeyModifiers != null && hotkeyKeys != null && hotkeyEnabled != null)
            {
                for (int i = 0; i < hotkeyModifiers.Length; i++)
                {
                    hotkeyModifiers[i].SelectedItem = Settings.Instance.GetSetting<string>($"Hotkey{i}Modifier", "Alt");
                    hotkeyKeys[i].Text = Settings.Instance.GetSetting<string>($"Hotkey{i}Key", "");
                    bool enabled = Settings.Instance.GetSetting<bool>($"Hotkey{i}Enabled", false);
                    hotkeyEnabled[i].Checked = enabled;
                    hotkeyModifiers[i].Enabled = enabled;
                    hotkeyKeys[i].Enabled = enabled;
                }
            }
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            try
            {
                var settingsToUpdate = new Dictionary<string, object>();

                // Save preset settings
                for (int i = 0; i < presetNameBoxes.Length; i++)
                {
                    settingsToUpdate[$"Preset{i + 1}Name"] = presetNameBoxes[i].Text;
                    settingsToUpdate[$"Preset{i + 1}Voice"] = presetModelBoxes[i].SelectedItem?.ToString() ?? "af_bella";
                    settingsToUpdate[$"Preset{i + 1}Speed"] = presetSpeedBoxes[i].SelectedItem?.ToString() ?? "1.0";
                    settingsToUpdate[$"Preset{i + 1}Enabled"] = presetCheckBoxes[i].Checked;
                }

                // Save hotkey settings
                for (int i = 0; i < hotkeyModifiers.Length; i++)
                {
                    settingsToUpdate[$"Hotkey{i}Modifier"] = hotkeyModifiers[i].SelectedItem?.ToString() ?? "";
                    settingsToUpdate[$"Hotkey{i}Key"] = hotkeyKeys[i].Text ?? "";
                    settingsToUpdate[$"Hotkey{i}Enabled"] = hotkeyEnabled[i].Checked;
                }

                // Find and apply the active preset's settings globally
                string currentPreset = Settings.Instance.GetSetting<string>("CurrentPreset", "");
                for (int i = 0; i < presetNameBoxes.Length; i++)
                {
                    if (presetNameBoxes[i].Text == currentPreset && presetCheckBoxes[i].Checked)
                    {
                        // Apply this preset's voice and speed globally
                        settingsToUpdate["Voice"] = presetModelBoxes[i].SelectedItem?.ToString() ?? "af_bella";
                        settingsToUpdate["Speed"] = float.Parse(presetSpeedBoxes[i].SelectedItem?.ToString() ?? "1.0");
                        break;
                    }
                }

                // Save all settings at once
                Settings.Instance.BatchSetSettings(settingsToUpdate);

                Logger.Info("Settings saved successfully");
            }
            catch (Exception ex)
            {
                Logger.Error("Error saving settings", ex);
                MessageBox.Show("Error saving settings: " + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                this.DialogResult = DialogResult.None;
            }
        }
    }
} 