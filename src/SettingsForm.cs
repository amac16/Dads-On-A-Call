using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Windows.Forms;

namespace DadsOnCall
{
    internal sealed class SettingsForm : Form
    {
        private readonly NumericUpDown widthInput;
        private readonly NumericUpDown heightInput;
        private readonly AlertSettingsPanel onPanel;
        private readonly AlertSettingsPanel offPanel;
        private readonly CheckBox startupInput;
        private readonly Action<AppSettings, bool> save;
        private readonly PositionPicker positionPicker;
        private readonly Action<ScreenPosition?> previewPosition;
        private readonly Font headingFont = new Font("Segoe UI", 18, FontStyle.Bold);
        private readonly Font uiFont = new Font("Segoe UI", 10);

        public SettingsForm(AppSettings settings, bool startWithWindows, Action<AppSettings, bool> onSave,
            Action<ScreenPosition?> onPreviewPosition = null)
        {
            SuspendLayout();
            save = onSave;
            previewPosition = onPreviewPosition;
            Text = "Dad's On A Call - Settings";
            Font = uiFont;
            BackColor = Color.FromArgb(247, 248, 250);
            ForeColor = Color.FromArgb(30, 38, 50);
            ClientSize = new Size(780, 646);
            AutoScroll = true;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;

            var layout = new TableLayoutPanel
            {
                Size = new Size(600, 646), MinimumSize = new Size(600, 646),
                Padding = new Padding(24), ColumnCount = 2, RowCount = 6
            };
            layout.SuspendLayout();
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            var heading = new Label { Text = "So, like, what's your sign?", Font = headingFont, AutoSize = true };
            layout.Controls.Add(heading, 0, 0);
            layout.SetColumnSpan(heading, 2);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            var hint = new Label
            {
                Text = "Change your sign settings below to let your family \\n know when you're too busy to chat.",
                AutoSize = true, Dock = DockStyle.Fill
            };
            layout.Controls.Add(hint, 0, 1);
            layout.SetColumnSpan(hint, 2);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));

            widthInput = new NumericUpDown { Minimum = 160, Maximum = 2000, Value = settings.Width, Width = 100,
                AccessibleName = "Window width in pixels" };
            heightInput = new NumericUpDown { Minimum = 60, Maximum = 1200, Value = settings.Height, Width = 100,
                AccessibleName = "Window height in pixels" };
            var dimensions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            dimensions.SuspendLayout();
            dimensions.Controls.Add(widthInput);
            dimensions.Controls.Add(new Label { Text = "x", AutoSize = true, Margin = new Padding(5, 7, 5, 0) });
            dimensions.Controls.Add(heightInput);
            layout.Controls.Add(new Label { Text = "Shared window size (px)", AutoSize = true, Margin = new Padding(0, 7, 0, 0) }, 0, 2);
            layout.Controls.Add(dimensions, 1, 2);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));

            var fontNames = new List<string>();
            using (var fonts = new InstalledFontCollection())
            {
                foreach (var family in fonts.Families)
                {
                    fontNames.Add(family.Name);
                    family.Dispose();
                }
            }
            Func<Size> sharedSize = delegate { return new Size((int)widthInput.Value, (int)heightInput.Value); };
            onPanel = new AlertSettingsPanel(false, settings, fontNames.ToArray(), sharedSize);
            offPanel = new AlertSettingsPanel(true, settings, fontNames.ToArray(), sharedSize);
            var tabs = new TabControl { Dock = DockStyle.Fill, AccessibleName = "Indicator settings", Margin = new Padding(0, 0, 0, 12) };
            var onTab = new TabPage("On Call") { BackColor = BackColor };
            var offTab = new TabPage("Off Call") { BackColor = BackColor };
            onTab.Controls.Add(onPanel);
            offTab.Controls.Add(offPanel);
            tabs.TabPages.Add(onTab);
            tabs.TabPages.Add(offTab);
            layout.Controls.Add(tabs, 0, 3);
            layout.SetColumnSpan(tabs, 2);
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var positionPanel = new FlowLayoutPanel
            {
                Location = new Point(600, 116), Size = new Size(180, 460),
                FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(16, 0, 0, 0)
            };
            positionPanel.SuspendLayout();
            positionPanel.Controls.Add(new Label { Text = "Screen position", AutoSize = true, Margin = new Padding(0, 7, 0, 8) });
            positionPicker = new PositionPicker(settings.Position) { Margin = new Padding(0, 0, 0, 8) };
            positionPanel.Controls.Add(positionPicker);
            var positionLabel = new Label
            {
                Text = PositionPicker.PositionNames[(int)settings.Position], AutoSize = true,
                AccessibleName = "Selected screen position", Margin = new Padding(0, 0, 0, 12)
            };
            positionPanel.Controls.Add(positionLabel);
            positionPanel.Controls.Add(new Label
            {
                Text = "Shared by both signs.\n\nClick a section to move\na visible sign instantly.\n\nSave keeps the position.\nCancel puts it back.",
                AutoSize = true, Margin = new Padding(0)
            });
            positionPicker.PositionChanged += delegate(ScreenPosition position)
            {
                positionLabel.Text = PositionPicker.PositionNames[(int)position];
                if (previewPosition != null) previewPosition(position);
            };

            startupInput = new CheckBox
            {
                Text = "Start with Windows", AccessibleName = "Start with Windows",
                Checked = startWithWindows, AutoSize = true, Margin = new Padding(0, 3, 0, 0)
            };
            layout.Controls.Add(startupInput, 0, 4);
            layout.SetColumnSpan(startupInput, 2);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            buttons.SuspendLayout();
            var saveButton = new Button { Text = "Save settings", AutoSize = true, Height = 34 };
            var cancelButton = new Button { Text = "Cancel", AutoSize = true, Height = 34, DialogResult = DialogResult.Cancel };
            saveButton.Click += SaveSettings;
            cancelButton.Click += delegate { Close(); };
            buttons.Controls.Add(saveButton);
            buttons.Controls.Add(cancelButton);
            layout.Controls.Add(buttons, 0, 5);
            layout.SetColumnSpan(buttons, 2);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            AcceptButton = saveButton;
            CancelButton = cancelButton;

            widthInput.ValueChanged += delegate { onPanel.UpdatePreview(); offPanel.UpdatePreview(); };
            heightInput.ValueChanged += delegate { onPanel.UpdatePreview(); offPanel.UpdatePreview(); };
            var canvas = new Panel { Size = new Size(780, 646) };
            canvas.SuspendLayout();
            canvas.Controls.Add(layout);
            canvas.Controls.Add(positionPanel);
            Controls.Add(canvas);
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            dimensions.ResumeLayout(false);
            positionPanel.ResumeLayout(false);
            buttons.ResumeLayout(false);
            layout.ResumeLayout(false);
            canvas.ResumeLayout(false);
            ResumeLayout(true);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            AutoScrollMinSize = Controls[0].Size;
            var workArea = Screen.FromControl(this).WorkingArea;
            Size = new Size(Math.Min(Width, workArea.Width), Math.Min(Height, workArea.Height));
        }

        private void SaveSettings(object sender, EventArgs e)
        {
            var settings = new AppSettings
            {
                Width = (int)widthInput.Value, Height = (int)heightInput.Value, Position = positionPicker.SelectedPosition
            };
            onPanel.WriteSettings(settings);
            offPanel.WriteSettings(settings);
            try
            {
                settings.ValidateTimers();
                save(settings, startupInput.Checked);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Settings could not be fully saved.\n\n" + ex.Message,
                    "Dad's On A Call", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // Clearing the override restores the saved position on Cancel, or the newly saved one on Save.
            if (previewPosition != null) previewPosition(null);
            base.OnFormClosed(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                headingFont.Dispose();
                uiFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
