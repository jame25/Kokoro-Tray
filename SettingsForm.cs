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
        private TabPage dictionariesTab;
        private Button saveButton;
        private Button cancelButton;

        // Dictionary tab controls
        private ListBox ignoreListBox;
        private ListBox bannedListBox;
        private ListBox replaceListBox;
        private TextBox ignoreTextBox;
        private TextBox bannedTextBox;
        private TextBox replaceTextBox;
        private TextBox replaceValueTextBox;
        private Button ignoreAddButton;
        private Button bannedAddButton;
        private Button replaceAddButton;
        private Button ignoreRemoveButton;
        private Button bannedRemoveButton;
        private Button replaceRemoveButton;
        private DictionaryManager dictionaryManager;
        private TTSServiceManager ttsService;

        // Presets tab controls
        private TextBox[] presetNameBoxes;
        private ComboBox[] presetModelBoxes;
        private ComboBox[] presetSpeedBoxes;
        private CheckBox[] presetCheckBoxes;

        // Hotkeys tab controls
        private ComboBox[] hotkeyModifiers;
        private TextBox[] hotkeyKeys;
        private CheckBox[] hotkeyEnabled;

        public SettingsForm(TTSServiceManager ttsService)
        {
            this.ttsService = ttsService;
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
            dictionariesTab = new TabPage("Dictionaries");

            InitializeAppearanceTab();
            InitializeHotkeysTab();
            InitializePresetsTab();
            InitializeDictionariesTab();

            // Add tabs to TabControl
            tabControl.TabPages.Add(appearanceTab);
            tabControl.TabPages.Add(hotkeysTab);
            tabControl.TabPages.Add(presetsTab);
            tabControl.TabPages.Add(dictionariesTab);

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

        private void InitializeDictionariesTab()
        {
            // Initialize dictionary manager
            var dictionaryPath = Path.Combine(AppContext.BaseDirectory, "dict");
            dictionaryManager = new DictionaryManager(dictionaryPath);

            // Panel for ignore dictionary
            var ignorePanel = new Panel
            {
                Location = new Point(10, 10),
                Size = new Size(140, 250)
            };

            var ignoreLabel = new Label
            {
                Text = "Ignore Words",
                Location = new Point(0, 0),
                Size = new Size(140, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(this.Font, FontStyle.Bold)
            };

            ignoreListBox = new ListBox
            {
                Location = new Point(0, 25),
                Size = new Size(140, 150),
                SelectionMode = SelectionMode.One
            };

            ignoreTextBox = new TextBox
            {
                Location = new Point(0, 180),
                Size = new Size(140, 23),
                PlaceholderText = "Enter word to ignore"
            };

            ignoreAddButton = new Button
            {
                Text = "Add",
                Location = new Point(0, 210),
                Size = new Size(65, 23)
            };

            ignoreRemoveButton = new Button
            {
                Text = "Remove",
                Location = new Point(75, 210),
                Size = new Size(65, 23)
            };

            // Panel for banned dictionary
            var bannedPanel = new Panel
            {
                Location = new Point(160, 10),
                Size = new Size(140, 250)
            };

            var bannedLabel = new Label
            {
                Text = "Banned Phrases",
                Location = new Point(0, 0),
                Size = new Size(140, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(this.Font, FontStyle.Bold)
            };

            bannedListBox = new ListBox
            {
                Location = new Point(0, 25),
                Size = new Size(140, 150),
                SelectionMode = SelectionMode.One
            };

            bannedTextBox = new TextBox
            {
                Location = new Point(0, 180),
                Size = new Size(140, 23),
                PlaceholderText = "Enter phrase to ban"
            };

            bannedAddButton = new Button
            {
                Text = "Add",
                Location = new Point(0, 210),
                Size = new Size(65, 23)
            };

            bannedRemoveButton = new Button
            {
                Text = "Remove",
                Location = new Point(75, 210),
                Size = new Size(65, 23)
            };

            // Panel for replace dictionary
            var replacePanel = new Panel
            {
                Location = new Point(310, 10),
                Size = new Size(140, 250)
            };

            var replaceLabel = new Label
            {
                Text = "Replacements",
                Location = new Point(0, 0),
                Size = new Size(140, 20),
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font(this.Font, FontStyle.Bold)
            };

            replaceListBox = new ListBox
            {
                Location = new Point(0, 25),
                Size = new Size(140, 150),
                SelectionMode = SelectionMode.One
            };

            replaceTextBox = new TextBox
            {
                Location = new Point(0, 180),
                Size = new Size(140, 23),
                PlaceholderText = "Word to replace"
            };

            replaceValueTextBox = new TextBox
            {
                Location = new Point(0, 205),
                Size = new Size(140, 23),
                PlaceholderText = "Replacement text"
            };

            replaceAddButton = new Button
            {
                Text = "Add",
                Location = new Point(0, 230),
                Size = new Size(65, 23)
            };

            replaceRemoveButton = new Button
            {
                Text = "Remove",
                Location = new Point(75, 230),
                Size = new Size(65, 23)
            };

            // Add event handlers
            ignoreAddButton.Click += (s, e) => {
                if (!string.IsNullOrWhiteSpace(ignoreTextBox.Text))
                {
                    ignoreListBox.Items.Add(ignoreTextBox.Text.Trim());
                    ignoreTextBox.Clear();
                }
            };

            ignoreRemoveButton.Click += (s, e) => {
                if (ignoreListBox.SelectedIndex != -1)
                {
                    ignoreListBox.Items.RemoveAt(ignoreListBox.SelectedIndex);
                }
            };

            bannedAddButton.Click += (s, e) => {
                if (!string.IsNullOrWhiteSpace(bannedTextBox.Text))
                {
                    bannedListBox.Items.Add(bannedTextBox.Text.Trim());
                    bannedTextBox.Clear();
                }
            };

            bannedRemoveButton.Click += (s, e) => {
                if (bannedListBox.SelectedIndex != -1)
                {
                    bannedListBox.Items.RemoveAt(bannedListBox.SelectedIndex);
                }
            };

            replaceAddButton.Click += (s, e) => {
                if (!string.IsNullOrWhiteSpace(replaceTextBox.Text) && !string.IsNullOrWhiteSpace(replaceValueTextBox.Text))
                {
                    replaceListBox.Items.Add($"{replaceTextBox.Text.Trim()}={replaceValueTextBox.Text.Trim()}");
                    replaceTextBox.Clear();
                    replaceValueTextBox.Clear();
                }
            };

            replaceRemoveButton.Click += (s, e) => {
                if (replaceListBox.SelectedIndex != -1)
                {
                    replaceListBox.Items.RemoveAt(replaceListBox.SelectedIndex);
                }
            };

            // Add controls to panels
            ignorePanel.Controls.AddRange(new Control[] {
                ignoreLabel,
                ignoreListBox,
                ignoreTextBox,
                ignoreAddButton,
                ignoreRemoveButton
            });

            bannedPanel.Controls.AddRange(new Control[] {
                bannedLabel,
                bannedListBox,
                bannedTextBox,
                bannedAddButton,
                bannedRemoveButton
            });

            replacePanel.Controls.AddRange(new Control[] {
                replaceLabel,
                replaceListBox,
                replaceTextBox,
                replaceValueTextBox,
                replaceAddButton,
                replaceRemoveButton
            });

            // Add panels to tab
            dictionariesTab.Controls.AddRange(new Control[] {
                ignorePanel,
                bannedPanel,
                replacePanel
            });

            // Load dictionary contents
            LoadDictionaryContents();
        }

        private void LoadDictionaryContents()
        {
            var dictionaryPath = Path.Combine(AppContext.BaseDirectory, "dict");

            // Load ignore dictionary
            var ignorePath = Path.Combine(dictionaryPath, "ignore.dict");
            if (File.Exists(ignorePath))
            {
                var ignoreWords = File.ReadAllLines(ignorePath)
                    .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                    .Select(line => line.Trim());
                ignoreListBox.Items.AddRange(ignoreWords.ToArray());
            }

            // Load banned dictionary
            var bannedPath = Path.Combine(dictionaryPath, "banned.dict");
            if (File.Exists(bannedPath))
            {
                var bannedPhrases = File.ReadAllLines(bannedPath)
                    .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                    .Select(line => line.Trim());
                bannedListBox.Items.AddRange(bannedPhrases.ToArray());
            }

            // Load replacements dictionary
            var replacePath = Path.Combine(dictionaryPath, "replace.dict");
            if (File.Exists(replacePath))
            {
                var replacements = File.ReadAllLines(replacePath)
                    .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                    .Select(line => line.Trim());
                replaceListBox.Items.AddRange(replacements.ToArray());
            }
        }

        private void SaveDictionaryContents()
        {
            var dictionaryPath = Path.Combine(AppContext.BaseDirectory, "dict");
            Directory.CreateDirectory(dictionaryPath);

            // Save ignore dictionary
            var ignorePath = Path.Combine(dictionaryPath, "ignore.dict");
            File.WriteAllLines(ignorePath, ignoreListBox.Items.Cast<string>());

            // Save banned dictionary
            var bannedPath = Path.Combine(dictionaryPath, "banned.dict");
            File.WriteAllLines(bannedPath, bannedListBox.Items.Cast<string>());

            // Save replacements dictionary
            var replacePath = Path.Combine(dictionaryPath, "replace.dict");
            File.WriteAllLines(replacePath, replaceListBox.Items.Cast<string>());

            // Reload the dictionaries in both DictionaryManager instances
            dictionaryManager.ReloadDictionaries();
            if (ttsService != null)
            {
                ttsService.ReloadDictionaries();
            }
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
            SaveDictionaryContents();
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
