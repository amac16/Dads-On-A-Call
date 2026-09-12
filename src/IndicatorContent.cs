using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace DadsOnCall
{
    // Shared by the real sign and the settings preview so timed layouts stay identical.
    internal sealed class IndicatorContent : Control
    {
        private readonly Button[] addButtons;
        private readonly Button endButton;
        private readonly LinkLabel settingsLink;
        private AppSettings settings;
        private bool offCall;
        private bool timed;
        private Font messageFont;
        private Font countdownFont;
        private Font buttonFont;
        private Rectangle messageBounds;
        private Rectangle countdownBounds;
        private Rectangle addLabelBounds;
        private Rectangle endButtonBounds;
        internal string CountdownText { get; private set; }
        internal event Action<int> AddTimeRequested;
        internal event Action EndRequested;
        internal event Action SettingsRequested;
        internal bool ShowSettingsLink
        {
            get { return settingsLink.Visible; }
            set
            {
                settingsLink.Visible = value;
                LayoutContent();
            }
        }
        internal bool ShowEndButton
        {
            get { return endButton.Visible; }
            set
            {
                endButton.Visible = value;
                LayoutContent();
            }
        }

        public IndicatorContent()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            CountdownText = "";
            int[] increments = { 5, 10, 15, 30, 60 };
            addButtons = new Button[increments.Length];
            for (int i = 0; i < increments.Length; i++)
            {
                int minutes = increments[i];
                var button = new NoFocusButton
                {
                    Text = minutes == 60 ? "1hr" : minutes + "m", FlatStyle = FlatStyle.Flat,
                    AccessibleName = "Add " + minutes + " minutes",
                    AccessibleDescription = "Extend the current countdown by " + minutes + " minutes.",
                    UseVisualStyleBackColor = false, Visible = false
                };
                button.Click += delegate
                {
                    var handler = AddTimeRequested;
                    if (timed && handler != null) handler(minutes);
                };
                addButtons[i] = button;
                Controls.Add(button);
            }
            endButton = new NoFocusButton
            {
                Text = "End", FlatStyle = FlatStyle.Flat, AccessibleName = "End alert",
                AccessibleDescription = "End this alert.", UseVisualStyleBackColor = false, Visible = false
            };
            endButton.Click += delegate
            {
                var handler = EndRequested;
                if (handler != null) handler();
            };
            Controls.Add(endButton);
            settingsLink = new LinkLabel
            {
                Text = "Settings", AutoSize = false, TextAlign = ContentAlignment.MiddleLeft,
                FlatStyle = FlatStyle.System, TabStop = false, Visible = false,
                AccessibleName = "Settings", AccessibleDescription = "Open alert settings."
            };
            settingsLink.LinkClicked += delegate
            {
                var handler = SettingsRequested;
                if (handler != null) handler();
            };
            Controls.Add(settingsLink);
        }

        internal void ApplySettings(AppSettings value, bool isOffCall)
        {
            settings = value.Copy();
            offCall = isOffCall;
            Text = offCall ? settings.OffMessage : settings.OnMessage;
            AccessibleName = Text;
            BackColor = ColorTranslator.FromHtml(offCall ? settings.OffBackgroundColor : settings.BackgroundColor);
            ForeColor = ColorTranslator.FromHtml(offCall ? settings.OffFontColor : settings.FontColor);
            foreach (var button in addButtons)
            {
                button.BackColor = Darken(BackColor, 0.84);
                button.ForeColor = ForeColor;
                button.FlatAppearance.BorderColor = Darken(BackColor, 0.70);
                button.FlatAppearance.MouseOverBackColor = Darken(BackColor, 0.74);
                button.FlatAppearance.MouseDownBackColor = Darken(BackColor, 0.64);
            }
            endButton.BackColor = Darken(BackColor, 0.84);
            endButton.ForeColor = ForeColor;
            endButton.FlatAppearance.BorderColor = Darken(BackColor, 0.70);
            endButton.FlatAppearance.MouseOverBackColor = Darken(BackColor, 0.74);
            endButton.FlatAppearance.MouseDownBackColor = Darken(BackColor, 0.64);
            settingsLink.LinkColor = ForeColor;
            settingsLink.ActiveLinkColor = ForeColor;
            settingsLink.VisitedLinkColor = ForeColor;
            settingsLink.BackColor = BackColor;
            LayoutContent();
        }

        private static Color Darken(Color color, double factor)
        {
            return Color.FromArgb((int)(color.R * factor), (int)(color.G * factor), (int)(color.B * factor));
        }

        internal static string FormatCountdown(TimeSpan remaining)
        {
            long seconds = Math.Max(0, (long)Math.Ceiling(remaining.TotalSeconds));
            return string.Format(CultureInfo.InvariantCulture, "For {0}:{1:00} more minutes.", seconds / 60, seconds % 60);
        }

        internal void UpdateRemaining(TimeSpan? remaining)
        {
            bool wasTimed = timed;
            timed = remaining.HasValue;
            string text = timed ? FormatCountdown(remaining.Value) : "";
            if (text != CountdownText)
            {
                CountdownText = text;
                AccessibleDescription = text;
                Invalidate(countdownBounds);
            }
            if (wasTimed != timed)
            {
                foreach (var button in addButtons) button.Visible = timed;
                LayoutContent();
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutContent();
        }

        private void LayoutContent()
        {
            if (settings == null || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
            float dpi;
            using (var graphics = CreateGraphics()) dpi = graphics.DpiX / 96f;
            float scale = timed ? Math.Min(1f, Math.Min(Math.Max(1, Width - 16) / (290f * dpi),
                Math.Max(1, Height - 12) / (110f * dpi))) : 1f;
            var oldMessage = messageFont;
            var oldCountdown = countdownFont;
            var oldButton = buttonFont;
            using (var configured = settings.CreateFont(offCall))
            {
                messageFont = new Font(configured.FontFamily, Math.Max(1, configured.Size * scale), FontStyle.Regular);
                float countdownSize = Math.Max(1, Math.Min(14, configured.Size * 0.6f) * scale);
                try { countdownFont = new Font(configured.FontFamily, countdownSize, FontStyle.Bold); }
                catch (ArgumentException) { countdownFont = new Font("Segoe UI", countdownSize, FontStyle.Bold); }
                buttonFont = new Font("Segoe UI", Math.Max(1, 9 * scale), FontStyle.Regular);
            }
            foreach (var button in addButtons) button.Font = buttonFont;
            endButton.Font = buttonFont;
            if (oldMessage != null) oldMessage.Dispose();
            if (oldCountdown != null) oldCountdown.Dispose();
            if (oldButton != null) oldButton.Dispose();

            messageBounds = Rectangle.Inflate(ClientRectangle, -12, -8);
            if (timed)
            {
                float unit = dpi * scale;
                int padding = Math.Max(2, (int)(6 * unit));
                int gap = Math.Max(1, (int)(4 * unit));
                int buttonWidth = Math.Max(1, (int)(40 * unit));
                int buttonHeight = Math.Max(1, (int)(24 * unit));
                int endWidth = Math.Max(1, (int)(80 * unit));
                int endHeight = Math.Max(1, (int)(48 * unit));
                int labelWidth = Math.Max(1, (int)(58 * unit));
                int rowWidth = labelWidth + 5 * (buttonWidth + gap);
                int rowTop = Height - padding - buttonHeight;
                addLabelBounds = new Rectangle((Width - rowWidth) / 2, rowTop, labelWidth, buttonHeight);
                for (int i = 0; i < addButtons.Length; i++)
                    addButtons[i].Bounds = new Rectangle(addLabelBounds.Right + gap + i * (buttonWidth + gap), rowTop, buttonWidth, buttonHeight);
                int countdownHeight = Math.Max(1, countdownFont.Height + padding);
                countdownBounds = new Rectangle(padding, rowTop - gap - countdownHeight, Width - 2 * padding, countdownHeight);
                int endTop = Math.Max(padding, Height / 2 - endHeight / 2);
                endButtonBounds = new Rectangle((Width - endWidth) / 2, endTop, endWidth, endHeight);
                endButton.Bounds = endButtonBounds;
                messageBounds = new Rectangle(padding, padding, Width - 2 * padding,
                    Math.Max(1, (endButton.Visible ? endButtonBounds.Top : countdownBounds.Top) - 2 * padding));
            }
            else if (endButton.Visible)
            {
                float unit = dpi;
                int padding = Math.Max(2, (int)(6 * unit));
                int endWidth = Math.Max(1, (int)(80 * unit));
                int endHeight = Math.Max(1, (int)(48 * unit));
                int endTop = Math.Max(padding, Height / 2 - endHeight / 2);
                endButtonBounds = new Rectangle((Width - endWidth) / 2, endTop, endWidth, endHeight);
                endButton.Bounds = endButtonBounds;
                messageBounds = new Rectangle(padding, padding, Width - 2 * padding,
                    Math.Max(1, endButtonBounds.Top - 2 * padding));
            }
            int linkHeight = Math.Max(1, buttonFont.Height + 2);
            settingsLink.Bounds = new Rectangle(12, Height - 8 - linkHeight, 70, linkHeight);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (messageFont == null) return;
            var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
            TextRenderer.DrawText(e.Graphics, Text, messageFont, messageBounds, ForeColor,
                flags | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis);
            if (timed)
            {
                TextRenderer.DrawText(e.Graphics, CountdownText, countdownFont, countdownBounds, ForeColor,
                    flags | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
                TextRenderer.DrawText(e.Graphics, "Add time", buttonFont, addLabelBounds, ForeColor, flags | TextFormatFlags.SingleLine);
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                if (messageFont != null) messageFont.Dispose();
                if (countdownFont != null) countdownFont.Dispose();
                if (buttonFont != null) buttonFont.Dispose();
            }
        }

        private sealed class NoFocusButton : Button
        {
            public NoFocusButton()
            {
                SetStyle(ControlStyles.Selectable, false);
                TabStop = false;
            }

            protected override void WndProc(ref System.Windows.Forms.Message message)
            {
                if (message.Msg == 0x0021)
                {
                    message.Result = new IntPtr(3); // MA_NOACTIVATE, without eating the click.
                    return;
                }
                base.WndProc(ref message);
            }
        }
    }
}
