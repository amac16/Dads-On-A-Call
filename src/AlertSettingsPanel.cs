using System;
using System.Drawing;
using System.Windows.Forms;

namespace DadsOnCall
{
    internal sealed class AlertSettingsPanel : UserControl
    {
        private readonly bool offCall;
        private readonly Func<Size> sharedSize;
        private readonly ComboBox fontInput;
        private readonly TextBox messageInput;
        private readonly NumericUpDown fontSizeInput;
        private readonly Button backgroundInput;
        private readonly Button foregroundInput;
        private readonly CheckBox timerEnabled;
        private readonly ComboBox hoursInput;
        private readonly ComboBox minutesInput;
        private readonly CheckBox blinkEnabled;
        private readonly ComboBox blinkRate;
        private readonly Panel preview;
        private readonly IndicatorContent previewContent = new IndicatorContent();

        public AlertSettingsPanel(bool isOffCall, AppSettings settings, string[] fontNames, Func<Size> getSharedSize)
        {
            SuspendLayout();
            offCall = isOffCall;
            sharedSize = getSharedSize;
            Dock = DockStyle.Fill;
            AutoScaleMode = AutoScaleMode.Inherit;
            string prefix = offCall ? "Off Call " : "On Call ";
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 11
            };
            layout.SuspendLayout();
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            messageInput = new TextBox
            {
                Text = offCall ? settings.OffMessage : settings.OnMessage, MaxLength = 200, Dock = DockStyle.Top
            };
            AddRow(layout, "Message", messageInput, prefix, 0);
            fontInput = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top, Sorted = true };
            fontInput.Items.AddRange(fontNames);
            string family = offCall ? settings.OffFontFamily : settings.FontFamily;
            if (!fontInput.Items.Contains(family)) fontInput.Items.Add(family);
            fontInput.SelectedItem = family;
            AddRow(layout, "Font type", fontInput, prefix, 1);
            fontSizeInput = new NumericUpDown
            {
                Minimum = 8, Maximum = 144, DecimalPlaces = 1, Dock = DockStyle.Top,
                Value = (decimal)(offCall ? settings.OffFontSize : settings.FontSize)
            };
            AddRow(layout, "Font size (pt)", fontSizeInput, prefix, 2);
            backgroundInput = ColorButton(offCall ? settings.OffBackgroundColor : settings.BackgroundColor);
            foregroundInput = ColorButton(offCall ? settings.OffFontColor : settings.FontColor);
            AddRow(layout, "Background color", backgroundInput, prefix, 3);
            AddRow(layout, "Font color", foregroundInput, prefix, 4);

            timerEnabled = new CheckBox
            {
                Text = "Automatically hide this indicator after:", AccessibleName = prefix + "timer enabled",
                AutoSize = true, Checked = offCall ? settings.OffTimerEnabled : settings.OnTimerEnabled,
                Margin = new Padding(0, 5, 0, 0)
            };
            layout.Controls.Add(timerEnabled, 0, 5);
            layout.SetColumnSpan(timerEnabled, 2);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            hoursInput = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 70, AccessibleName = prefix + "timer hours" };
            for (int hour = 0; hour <= 24; hour++) hoursInput.Items.Add(hour);
            hoursInput.SelectedItem = offCall ? settings.OffTimerHours : settings.OnTimerHours;
            minutesInput = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 70, AccessibleName = prefix + "timer minutes" };
            foreach (int minute in AppSettings.MinuteChoices) minutesInput.Items.Add(minute);
            minutesInput.SelectedItem = offCall ? settings.OffTimerMinutes : settings.OnTimerMinutes;
            var duration = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = new Padding(0) };
            duration.SuspendLayout();
            duration.Controls.Add(hoursInput);
            duration.Controls.Add(new Label { Text = "Hours", AutoSize = true, Margin = new Padding(4, 7, 14, 0) });
            duration.Controls.Add(minutesInput);
            duration.Controls.Add(new Label { Text = "Minutes", AutoSize = true, Margin = new Padding(4, 7, 0, 0) });
            layout.Controls.Add(duration, 0, 6);
            layout.SetColumnSpan(duration, 2);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            hoursInput.Enabled = minutesInput.Enabled = timerEnabled.Checked;
            timerEnabled.CheckedChanged += delegate
            {
                hoursInput.Enabled = minutesInput.Enabled = timerEnabled.Checked;
                UpdatePreview();
            };

            blinkEnabled = new CheckBox
            {
                Text = "Blink this indicator", AccessibleName = prefix + "blink enabled",
                AutoSize = true, Checked = offCall ? settings.OffBlinkEnabled : settings.OnBlinkEnabled,
                Margin = new Padding(0, 5, 0, 0)
            };
            layout.Controls.Add(blinkEnabled, 0, 7);
            layout.SetColumnSpan(blinkEnabled, 2);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            blinkRate = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top,
                AccessibleName = prefix + "blink rate"
            };
            blinkRate.Items.AddRange(AppSettings.BlinkRateChoices);
            blinkRate.SelectedItem = offCall ? settings.OffBlinkRate : settings.OnBlinkRate;
            AddRow(layout, "Blink rate", blinkRate, prefix, 8);
            blinkRate.Enabled = blinkEnabled.Checked;
            blinkEnabled.CheckedChanged += delegate
            {
                blinkRate.Enabled = blinkEnabled.Checked;
                UpdatePreview();
            };

            var previewHint = new Label
            {
                Text = "APPEARANCE PREVIEW  (scaled to fit)", AutoSize = true,
                ForeColor = Color.FromArgb(90, 100, 115), Margin = new Padding(0, 5, 0, 0)
            };
            layout.Controls.Add(previewHint, 0, 9);
            layout.SetColumnSpan(previewHint, 2);
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            preview = new Panel
            {
                Dock = DockStyle.Fill, BackColor = Color.FromArgb(226, 229, 234), Margin = new Padding(0),
                AccessibleName = prefix + "preview"
            };
            preview.Paint += PaintPreview;
            layout.Controls.Add(preview, 0, 10);
            layout.SetColumnSpan(preview, 2);
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            fontSizeInput.ValueChanged += delegate { UpdatePreview(); };
            fontInput.SelectedIndexChanged += delegate { UpdatePreview(); };
            hoursInput.SelectedIndexChanged += delegate { UpdatePreview(); };
            minutesInput.SelectedIndexChanged += delegate { UpdatePreview(); };
            blinkRate.SelectedIndexChanged += delegate { UpdatePreview(); };
            messageInput.TextChanged += delegate { UpdatePreview(); };
            UpdatePreview();
            Controls.Add(layout);
            duration.ResumeLayout(false);
            layout.ResumeLayout(false);
            ResumeLayout(true);
        }

        private static void AddRow(TableLayoutPanel layout, string caption, Control control, string prefix, int row)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            layout.Controls.Add(new Label { Text = caption, AutoSize = true, Margin = new Padding(0, 7, 0, 0) }, 0, row);
            control.AccessibleName = prefix + caption;
            layout.Controls.Add(control, 1, row);
        }

        private Button ColorButton(string html)
        {
            var button = new Button { Dock = DockStyle.Top, Height = 28, FlatStyle = FlatStyle.Flat };
            SetButtonColor(button, ColorTranslator.FromHtml(html));
            button.Click += delegate
            {
                using (var dialog = new ColorDialog { Color = button.BackColor, FullOpen = true })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    SetButtonColor(button, dialog.Color);
                    UpdatePreview();
                }
            };
            return button;
        }

        private static void SetButtonColor(Button button, Color color)
        {
            button.BackColor = color;
            button.ForeColor = color.GetBrightness() > 0.55f ? Color.Black : Color.White;
            button.Text = string.Format("#{0:X2}{1:X2}{2:X2}", color.R, color.G, color.B);
        }

        public void WriteSettings(AppSettings settings)
        {
            if (offCall)
            {
                settings.OffMessage = AppSettings.NormalizeMessage(messageInput.Text, IndicatorForm.OffMessage);
                settings.OffFontFamily = (string)fontInput.SelectedItem;
                settings.OffFontSize = (float)fontSizeInput.Value;
                settings.OffBackgroundColor = backgroundInput.Text;
                settings.OffFontColor = foregroundInput.Text;
                settings.OffTimerEnabled = timerEnabled.Checked;
                settings.OffTimerHours = (int)hoursInput.SelectedItem;
                settings.OffTimerMinutes = (int)minutesInput.SelectedItem;
                settings.OffBlinkEnabled = blinkEnabled.Checked;
                settings.OffBlinkRate = (string)blinkRate.SelectedItem;
            }
            else
            {
                settings.OnMessage = AppSettings.NormalizeMessage(messageInput.Text, IndicatorForm.Message);
                settings.FontFamily = (string)fontInput.SelectedItem;
                settings.FontSize = (float)fontSizeInput.Value;
                settings.BackgroundColor = backgroundInput.Text;
                settings.FontColor = foregroundInput.Text;
                settings.OnTimerEnabled = timerEnabled.Checked;
                settings.OnTimerHours = (int)hoursInput.SelectedItem;
                settings.OnTimerMinutes = (int)minutesInput.SelectedItem;
                settings.OnBlinkEnabled = blinkEnabled.Checked;
                settings.OnBlinkRate = (string)blinkRate.SelectedItem;
            }
        }

        public void UpdatePreview()
        {
            var settings = new AppSettings();
            WriteSettings(settings);
            previewContent.ApplySettings(settings, offCall);
            previewContent.UpdateRemaining(settings.AutoHideDuration(offCall ? IndicatorMode.OffCall : IndicatorMode.OnCall));
            preview.Invalidate();
        }

        private void PaintPreview(object sender, PaintEventArgs e)
        {
            Size size = sharedSize();
            var actualBounds = IndicatorForm.DockBounds(Screen.PrimaryScreen.WorkingArea, size.Width, size.Height);
            float scale = Math.Min(1f, Math.Min((float)preview.ClientSize.Width / actualBounds.Width,
                (float)preview.ClientSize.Height / actualBounds.Height));
            if (scale <= 0) return;
            previewContent.Size = actualBounds.Size;
            // Render at the real dimensions first so the preview preserves text wrapping.
            using (var image = new Bitmap(actualBounds.Width, actualBounds.Height))
            {
                image.SetResolution(e.Graphics.DpiX, e.Graphics.DpiY);
                previewContent.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size));
                int width = (int)(actualBounds.Width * scale);
                int height = (int)(actualBounds.Height * scale);
                e.Graphics.DrawImage(image, new Rectangle((preview.Width - width) / 2, (preview.Height - height) / 2, width, height));
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) previewContent.Dispose();
            base.Dispose(disposing);
        }
    }
}
