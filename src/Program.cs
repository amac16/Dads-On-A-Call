using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace DadsOnCall
{
    internal static class AppInfo
    {
        internal const string Version = "1.0.6";
    }

    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool created;
            using (var mutex = new Mutex(true, "Local\\DadsOnCall.TrayApp", out created))
            {
                if (!created)
                {
                    MessageBox.Show("DadsOnACall is already running. Look for the D icon in your system tray, including the hidden icons menu.",
                        "DadsOnACall", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (var context = new TrayApplication()) Application.Run(context);
                mutex.ReleaseMutex();
            }
        }
    }

    internal sealed class TrayApplication : ApplicationContext
    {
        private AppSettings settings;
        private readonly IndicatorForm indicator;
        private SettingsForm settingsWindow;
        private readonly NotifyIcon tray;
        private readonly ContextMenuStrip menu;
        private readonly ToolStripMenuItem toggleItem;
        private readonly ToolStripMenuItem offToggleItem;
        private readonly ToolStripMenuItem headphonesToggleItem;
        private readonly Icon idleIcon;
        private readonly Icon offIcon;
        private readonly Icon onIcon;
        private readonly Icon headphonesIcon;
        private readonly StartupRegistration startup = new StartupRegistration(StartupRegistration.DefaultKeyPath);
        private readonly System.Windows.Forms.Timer autoHideTimer;
        private readonly Func<long> clock;
        private long? hideAt;
        private ScreenPosition? previewPosition;
        internal IndicatorMode Mode { get; private set; }

        public TrayApplication(AppSettings initialSettings = null, Func<long> elapsedMilliseconds = null)
        {
            bool recovered = false;
            settings = initialSettings == null ? SettingsStore.Load(SettingsStore.DefaultPath, out recovered) : initialSettings.Copy();
            settings.Normalize();
            // GetTickCount64 is monotonic and includes sleep, unlike a wall-clock deadline.
            clock = elapsedMilliseconds ?? delegate { return (long)GetTickCount64(); };
            autoHideTimer = new System.Windows.Forms.Timer { Interval = 250 };
            autoHideTimer.Tick += delegate { CheckAutoHide(); };
            indicator = new IndicatorForm(settings);
            indicator.AddTimeRequested += AddMinutes;
            indicator.EndRequested += EndIndicator;
            indicator.SettingsRequested += ShowSettings;
            idleIcon = CreateIcon(Color.FromArgb(76, 89, 108));
            offIcon = CreateIcon(Color.FromArgb(24, 122, 69));
            onIcon = CreateIcon(Color.FromArgb(180, 35, 53));
            headphonesIcon = CreateIcon(Color.Yellow);
            menu = new ContextMenuStrip();
            toggleItem = new ToolStripMenuItem("Start On Call Indicator", null, delegate { Toggle(IndicatorMode.OnCall); });
            offToggleItem = new ToolStripMenuItem("Start Off Call Indicator", null, delegate { Toggle(IndicatorMode.OffCall); });
            headphonesToggleItem = new ToolStripMenuItem("Start Headphones On Indicator", null, delegate { Toggle(IndicatorMode.HeadphonesOn); });
            menu.Items.Add(toggleItem);
            menu.Items.Add(offToggleItem);
            menu.Items.Add(headphonesToggleItem);
            menu.Items.Add("Settings...", null, delegate { ShowSettings(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Exit", null, delegate { ExitThread(); });
            tray = new NotifyIcon
            {
                Icon = idleIcon, Text = "DadsOnACall - Indicators hidden", ContextMenuStrip = menu, Visible = true
            };
            tray.MouseClick += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button == MouseButtons.Left) Toggle(IndicatorMode.OnCall);
            };
            tray.ShowBalloonTip(4000, "DadsOnACall",
                recovered ? "Saved settings could not be read. Defaults are in use. Right-click the tray icon to configure."
                    : "Ready. Left-click for On Call. Right-click for Off Call or settings.",
                recovered ? ToolTipIcon.Warning : ToolTipIcon.Info);
        }

        [DllImport("kernel32.dll")]
        private static extern ulong GetTickCount64();

        internal void Toggle(IndicatorMode mode)
        {
            SetMode(Mode == mode ? IndicatorMode.Hidden : mode);
        }

        private void SetMode(IndicatorMode mode)
        {
            Mode = mode;
            if (mode == IndicatorMode.Hidden) indicator.Hide();
            else
            {
                indicator.ApplySettings(settings, mode);
                if (previewPosition.HasValue) indicator.SetPosition(previewPosition.Value);
                indicator.Show();
            }
            RestartAutoHide();
            tray.Icon = mode == IndicatorMode.OnCall ? onIcon : mode == IndicatorMode.OffCall ? offIcon : mode == IndicatorMode.HeadphonesOn ? headphonesIcon : idleIcon;
            UpdateTrayText();
            toggleItem.Text = (mode == IndicatorMode.OnCall ? "Stop" : "Start") + " On Call Indicator";
            toggleItem.Checked = mode == IndicatorMode.OnCall;
            offToggleItem.Text = (mode == IndicatorMode.OffCall ? "Stop" : "Start") + " Off Call Indicator";
            offToggleItem.Checked = mode == IndicatorMode.OffCall;
            headphonesToggleItem.Text = (mode == IndicatorMode.HeadphonesOn ? "Stop" : "Start") + " Headphones On Indicator";
            headphonesToggleItem.Checked = mode == IndicatorMode.HeadphonesOn;
        }

        private void RestartAutoHide()
        {
            var duration = settings.AutoHideDuration(Mode);
            hideAt = duration.HasValue ? clock() + (long)duration.Value.TotalMilliseconds : (long?)null;
            autoHideTimer.Enabled = hideAt.HasValue;
            CheckAutoHide();
        }

        internal void CheckAutoHide()
        {
            long remaining = hideAt.HasValue ? hideAt.Value - clock() : 0;
            if (hideAt.HasValue && remaining <= 0)
            {
                SetMode(IndicatorMode.Hidden);
                return;
            }
            indicator.UpdateRemaining(hideAt.HasValue ? TimeSpan.FromMilliseconds(remaining) : (TimeSpan?)null);
        }

        internal void AddMinutes(int minutes)
        {
            if (!hideAt.HasValue || Mode == IndicatorMode.Hidden || minutes <= 0) return;
            // A queued click must not revive a sign whose deadline has already passed.
            if (clock() >= hideAt.Value)
            {
                CheckAutoHide();
                return;
            }
            hideAt = checked(hideAt.Value + (long)minutes * 60000);
            CheckAutoHide();
        }

        private void EndIndicator()
        {
            if (Mode != IndicatorMode.Hidden) SetMode(IndicatorMode.Hidden);
        }

        internal void ApplySettings(AppSettings updated)
        {
            var previousDuration = settings.AutoHideDuration(Mode);
            settings = updated.Copy();
            settings.Normalize();
            indicator.ApplySettings(settings, Mode);
            if (previewPosition.HasValue) indicator.SetPosition(previewPosition.Value);
            UpdateTrayText();
            // Appearance edits and the other mode's timer do not restart the active countdown.
            if (previousDuration != settings.AutoHideDuration(Mode)) RestartAutoHide();
            else CheckAutoHide();
        }

        private void UpdateTrayText()
        {
            string text = Mode == IndicatorMode.OnCall ? settings.OnMessage : Mode == IndicatorMode.OffCall
                ? settings.OffMessage : Mode == IndicatorMode.HeadphonesOn ? settings.HeadphonesMessage : "DadsOnACall - Indicators hidden";
            // NotifyIcon tooltips on .NET Framework are limited to 63 characters.
            tray.Text = text.Length > 63 ? AppSettings.TruncateText(text, 60) + "..." : text;
        }

        internal void PreviewPosition(ScreenPosition? position)
        {
            previewPosition = position;
            indicator.SetPosition(position ?? settings.Position);
        }

        private void ShowSettings()
        {
            if (settingsWindow != null)
            {
                settingsWindow.Activate();
                return;
            }
            bool startWithWindows;
            try { startWithWindows = startup.IsEnabled(); }
            catch (Exception ex)
            {
                MessageBox.Show("Windows startup settings could not be read.\n\n" + ex.Message,
                    "DadsOnACall", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            settingsWindow = new SettingsForm(settings.Copy(), startWithWindows, delegate(AppSettings updated, bool enableStartup)
            {
                SettingsStore.Save(SettingsStore.DefaultPath, updated);
                ApplySettings(updated);
                try { startup.SetEnabled(enableStartup, Application.ExecutablePath); }
                catch (Exception ex)
                {
                    MessageBox.Show("Your message settings were saved, but Windows startup could not be updated.\n\n" + ex.Message,
                        "DadsOnACall", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }, PreviewPosition, delegate(string downloadedPath)
            {
                UpdateService.LaunchReplacement(downloadedPath, Application.ExecutablePath);
                ExitThread();
            }, PreviewSettings);
            settingsWindow.Icon = idleIcon;
            settingsWindow.FormClosed += delegate { settingsWindow = null; };
            settingsWindow.Show();
        }

        private void PreviewSettings(AppSettings updated)
        {
            ApplySettings(updated);
        }

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr handle);

        internal static Icon CreateIcon(Color color)
        {
            using (var bitmap = new Bitmap(32, 32))
            using (var graphics = Graphics.FromImage(bitmap))
            using (var brush = new SolidBrush(color))
            using (var font = new Font("Segoe UI", 19, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);
                graphics.FillEllipse(brush, 1, 1, 30, 30);
                graphics.DrawString("D", font, Brushes.White, new RectangleF(0, 0, 32, 32), format);
                IntPtr handle = bitmap.GetHicon();
                try
                {
                    using (var borrowed = Icon.FromHandle(handle)) return (Icon)borrowed.Clone();
                }
                finally { DestroyIcon(handle); }
            }
        }

        protected override void ExitThreadCore()
        {
            autoHideTimer.Stop();
            tray.Visible = false;
            indicator.Hide();
            if (settingsWindow != null) settingsWindow.Close();
            base.ExitThreadCore();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (settingsWindow != null) settingsWindow.Dispose();
                autoHideTimer.Dispose();
                tray.Dispose();
                menu.Dispose();
                indicator.Dispose();
                idleIcon.Dispose();
                offIcon.Dispose();
                onIcon.Dispose();
                headphonesIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
