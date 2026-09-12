using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DadsOnCall
{
    internal enum IndicatorMode { Hidden, OnCall, OffCall }

    internal sealed class IndicatorForm : Form
    {
        public const string Message = "Dad's On A Call";
        public const string OffMessage = "Dad's Off His Call";
        private AppSettings settings;
        private readonly IndicatorContent content;
        private readonly Timer positionTimer;
        private readonly Timer blinkTimer;
        private IndicatorMode mode;
        private bool blinkVisible;
        internal event Action<int> AddTimeRequested;
        internal event Action EndRequested;

        public IndicatorForm(AppSettings initialSettings)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            AutoScaleMode = AutoScaleMode.None;
            DoubleBuffered = true;
            Text = Message;
            AccessibleName = Message;
            content = new IndicatorContent { Dock = DockStyle.Fill };
            content.AddTimeRequested += delegate(int minutes)
            {
                var handler = AddTimeRequested;
                if (handler != null) handler(minutes);
            };
            content.ShowEndButton = true;
            content.EndRequested += delegate
            {
                var handler = EndRequested;
                if (handler != null) handler();
            };
            Controls.Add(content);
            // Also catches taskbar moves and monitor disconnects while the indicator is visible.
            positionTimer = new Timer { Interval = 1000 };
            positionTimer.Tick += delegate { Reposition(); };
            blinkTimer = new Timer();
            blinkTimer.Tick += delegate
            {
                blinkVisible = !blinkVisible;
                Opacity = blinkVisible ? 1.0 : 0.0;
                blinkTimer.Interval = blinkVisible
                    ? settings.BlinkInterval(mode) : settings.BlinkHiddenInterval(mode);
            };
            VisibleChanged += delegate
            {
                positionTimer.Enabled = Visible;
                UpdateBlinkTimer();
                // WinForms' TopMost property can activate a newly shown form on .NET Framework.
                if (Visible) SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0, 0x0013);
            };
            ApplySettings(initialSettings);
        }

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);

        protected override bool ShowWithoutActivation { get { return true; } }

        protected override void WndProc(ref System.Windows.Forms.Message message)
        {
            if (message.Msg == 0x0021) // WM_MOUSEACTIVATE: accept clicks without activating the window.
            {
                message.Result = new IntPtr(3); // MA_NOACTIVATE
                return;
            }
            base.WndProc(ref message);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.ExStyle |= 0x08000000 | 0x00000080; // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
                return parameters;
            }
        }

        public void ApplySettings(AppSettings value, IndicatorMode mode = IndicatorMode.OnCall)
        {
            settings = value.Copy();
            settings.Normalize();
            this.mode = mode;
            bool offCall = mode == IndicatorMode.OffCall;
            Text = offCall ? settings.OffMessage : settings.OnMessage;
            AccessibleName = Text;
            BackColor = ColorTranslator.FromHtml(offCall ? settings.OffBackgroundColor : settings.BackgroundColor);
            ForeColor = ColorTranslator.FromHtml(offCall ? settings.OffFontColor : settings.FontColor);
            content.ApplySettings(settings, offCall);
            UpdateBlinkTimer();
            Reposition();
            Invalidate();
        }

        private void UpdateBlinkTimer()
        {
            blinkTimer.Stop();
            blinkVisible = true;
            Opacity = 1.0;
            if (settings != null && Visible && settings.BlinkEnabled(mode))
            {
                blinkTimer.Interval = settings.BlinkInterval(mode);
                blinkTimer.Start();
            }
        }

        internal static Rectangle DockBounds(Rectangle workArea, int width, int height, ScreenPosition position = ScreenPosition.TopRight)
        {
            width = Math.Min(width, workArea.Width);
            height = Math.Min(height, workArea.Height);
            if (!Enum.IsDefined(typeof(ScreenPosition), position)) position = ScreenPosition.TopRight;
            int column = (int)position % 3;
            int row = (int)position / 3;
            return new Rectangle(workArea.Left + (workArea.Width - width) * column / 2,
                workArea.Top + (workArea.Height - height) * row / 2, width, height);
        }

        internal void SetPosition(ScreenPosition position)
        {
            settings.Position = position;
            Reposition();
        }

        public void Reposition()
        {
            var desired = DockBounds(Screen.PrimaryScreen.WorkingArea, settings.Width, settings.Height, settings.Position);
            if (Bounds != desired) Bounds = desired;
        }

        internal void UpdateRemaining(TimeSpan? remaining)
        {
            content.UpdateRemaining(remaining);
            AccessibleDescription = content.CountdownText;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (positionTimer != null) positionTimer.Dispose();
                if (blinkTimer != null) blinkTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
