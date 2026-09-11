using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Reflection;
using System.Diagnostics;
using System.Threading;

namespace DadsOnCall
{
    internal static class Tests
    {
        private static int assertions;

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong(IntPtr handle, int index);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);

        [STAThread]
        private static int Main(string[] args)
        {
            string directory = Path.Combine(Path.GetTempPath(), "DadsOnCall.Tests-" + Guid.NewGuid().ToString("N"));
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Directory.CreateDirectory(directory);
                TestSettings(directory);
                TestBounds();
                TestMessagesAndPositions(args.Length > 0 ? args[0] : null);
                TestStartup();
                TestTimers();
                TestCountdown(args.Length > 0 ? args[0] : null);
                TestWindows(directory, args.Length > 0 ? args[0] : null);
                Console.WriteLine("PASS: " + assertions + " assertions.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
            finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }

        private static void Check(bool condition, string description)
        {
            if (!condition) throw new Exception("FAIL: " + description);
            assertions++;
            Console.WriteLine("PASS: " + description);
        }

        private static void TestSettings(string directory)
        {
            string path = Path.Combine(directory, "settings.xml");
            bool recovered;
            var settings = SettingsStore.Load(path, out recovered);
            Check(!recovered && settings.Width == 520, "Missing settings use defaults");
            Check(settings.OnMessage == IndicatorForm.Message && settings.OffMessage == IndicatorForm.OffMessage &&
                settings.Position == ScreenPosition.TopRight, "Messages and position retain original defaults");
            Check(settings.OffBackgroundColor == "#187A45" && settings.OffFontColor == "#FFFFFF" &&
                !settings.OnTimerEnabled && !settings.OffTimerEnabled, "Off Call defaults green and both timers default disabled");
            settings.Width = 580;
            settings.Height = 180;
            settings.FontFamily = "Arial";
            settings.FontSize = 35.5f;
            settings.BackgroundColor = "#123456";
            settings.FontColor = "#abcdef";
            settings.OnMessage = "Quiet please & thank you";
            settings.OffMessage = "Ready for family time!";
            settings.Position = ScreenPosition.BottomCenter;
            settings.OffBackgroundColor = "#126734";
            settings.OffFontColor = "#eeeeee";
            settings.OffFontFamily = "Consolas";
            settings.OffFontSize = 20;
            settings.OnTimerEnabled = true;
            settings.OnTimerHours = 1;
            settings.OnTimerMinutes = 30;
            settings.OffTimerEnabled = true;
            settings.OffTimerHours = 0;
            settings.OffTimerMinutes = 2;
            SettingsStore.Save(path, settings);
            var loaded = SettingsStore.Load(path, out recovered);
            Check(!recovered && loaded.Width == 580 && loaded.Height == 180, "Dimensions persist");
            Check(loaded.FontFamily == "Arial" && loaded.FontSize == 35.5f, "Font persists");
            Check(loaded.BackgroundColor == "#123456" && loaded.FontColor == "#ABCDEF", "Colors persist and normalize");
            Check(loaded.OnMessage == settings.OnMessage && loaded.OffMessage == settings.OffMessage &&
                loaded.Position == ScreenPosition.BottomCenter, "Both custom messages and shared position persist");
            Check(loaded.OffBackgroundColor == "#126734" && loaded.OffFontColor == "#EEEEEE" &&
                loaded.OffFontFamily == "Consolas" && loaded.OffFontSize == 20, "Off Call appearance persists independently");
            Check(loaded.OnTimerEnabled && loaded.OnTimerHours == 1 && loaded.OnTimerMinutes == 30 &&
                loaded.OffTimerEnabled && loaded.OffTimerHours == 0 && loaded.OffTimerMinutes == 2, "Both timer configurations persist independently");
            settings.Width = 600;
            SettingsStore.Save(path, settings);
            Check(SettingsStore.Load(path, out recovered).Width == 600, "Existing settings are atomically replaced");
            File.WriteAllText(path, "<AppSettings><Width>640</Width><Height>190</Height><BackgroundColor>#112233</BackgroundColor>" +
                "<FontColor>#FFEEDD</FontColor><FontFamily>Arial</FontFamily><FontSize>31</FontSize></AppSettings>");
            loaded = SettingsStore.Load(path, out recovered);
            Check(!recovered && loaded.Width == 640 && loaded.Height == 190 && loaded.BackgroundColor == "#112233" &&
                loaded.FontColor == "#FFEEDD" && loaded.FontFamily == "Arial" && loaded.FontSize == 31 &&
                loaded.OffBackgroundColor == "#187A45" && !loaded.OnTimerEnabled && !loaded.OffTimerEnabled,
                "Existing settings retain On Call appearance and receive safe new defaults");
            Check(loaded.OnMessage == IndicatorForm.Message && loaded.OffMessage == IndicatorForm.OffMessage &&
                loaded.Position == ScreenPosition.TopRight, "Older settings receive default messages and top-right placement");
            File.WriteAllText(path, "not XML");
            Check(SettingsStore.Load(path, out recovered).Width == 520 && recovered, "Corrupt settings recover safely");
            File.WriteAllText(path, "<!DOCTYPE AppSettings [<!ENTITY x SYSTEM 'file:///invalid'>]><AppSettings><FontFamily>&x;</FontFamily></AppSettings>");
            SettingsStore.Load(path, out recovered);
            Check(recovered, "XML external entities are rejected");
            settings.Width = -1;
            settings.Height = int.MaxValue;
            settings.FontSize = float.NaN;
            settings.BackgroundColor = "bad";
            settings.FontColor = null;
            settings.FontFamily = "";
            settings.Normalize();
            Check(settings.Width == 160 && settings.Height == 1200, "Dimensions are clamped");
            Check(settings.FontSize == 24 && settings.FontFamily == "Segoe UI", "Invalid font values recover");
            Check(settings.BackgroundColor == "#B42335" && settings.FontColor == "#FFFFFF", "Invalid colors recover");
            var copy = settings.Copy();
            copy.Width = 999;
            Check(settings.Width == 160, "Settings edits do not mutate originals");
            settings.OffBackgroundColor = "# FFFFF";
            settings.OffFontColor = "invalid";
            settings.OffFontFamily = "";
            settings.OffFontSize = float.PositiveInfinity;
            settings.OnTimerHours = -5;
            settings.OnTimerMinutes = -1;
            settings.OffTimerHours = 99;
            settings.OffTimerMinutes = 59;
            settings.Normalize();
            Check(settings.OffBackgroundColor == "#187A45" && settings.OffFontColor == "#FFFFFF" &&
                settings.OffFontFamily == "Segoe UI" && settings.OffFontSize == 24, "Invalid Off Call appearance recovers safely");
            Check(settings.OnTimerHours == 0 && settings.OnTimerMinutes == 5 && settings.OffTimerHours == 24 &&
                settings.OffTimerMinutes == 5, "Invalid timer values are normalized to supported choices");
            settings.OnTimerMinutes = 0;
            bool rejected = false;
            try { settings.ValidateTimers(); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "Enabled zero-duration On Call timer is rejected before saving");
            settings.OnTimerEnabled = false;
            settings.OffTimerHours = settings.OffTimerMinutes = 0;
            rejected = false;
            try { settings.ValidateTimers(); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "Enabled zero-duration Off Call timer is rejected before saving");
            settings.Normalize();
            Check(settings.OffTimerMinutes == 5, "Invalid persisted zero-duration timer recovers safely");
            settings.OffTimerEnabled = false;
            settings.OffTimerMinutes = 0;
            settings.ValidateTimers();
            Check(settings.AutoHideDuration(IndicatorMode.OnCall) == null && settings.AutoHideDuration(IndicatorMode.OffCall) == null,
                "Disabled zero-duration timers are allowed and do not run");
            settings.OnMessage = "   ";
            settings.OffMessage = null;
            settings.Position = (ScreenPosition)100;
            settings.Normalize();
            Check(settings.OnMessage == IndicatorForm.Message && settings.OffMessage == IndicatorForm.OffMessage &&
                settings.Position == ScreenPosition.TopRight, "Empty messages and invalid positions recover to defaults");
            settings.OnMessage = new string('a', 199) + "\uD83D\uDE00";
            SettingsStore.Save(path, settings);
            loaded = SettingsStore.Load(path, out recovered);
            Check(!recovered && loaded.OnMessage == new string('a', 199), "Message length limit does not split Unicode surrogate pairs");
            Check(AppSettings.TruncateText(new string('a', 59) + "\uD83D\uDE00", 60) == new string('a', 59),
                "Tooltip truncation preserves valid Unicode");
        }

        private static void TestTimers()
        {
            long now = 1000;
            var settings = new AppSettings { OnTimerEnabled = true, OnTimerMinutes = 1, OffTimerEnabled = true, OffTimerMinutes = 2 };
            using (var app = new TrayApplication(settings, delegate { return now; }))
            {
                var menu = (ContextMenuStrip)typeof(TrayApplication).GetField("menu", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(app);
                var on = (ToolStripMenuItem)menu.Items[0];
                var off = (ToolStripMenuItem)menu.Items[1];
                var indicator = (IndicatorForm)typeof(TrayApplication).GetField("indicator", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(app);
                Check(on.Text == "Start On Call Indicator" && off.Text == "Start Off Call Indicator", "Off Call action is directly below On Call action");
                on.PerformClick();
                Check(app.Mode == IndicatorMode.OnCall && on.Checked && !off.Checked && on.Text == "Stop On Call Indicator",
                    "On Call menu action shows the correct active state");
                now += 59999;
                app.CheckAutoHide();
                Check(app.Mode == IndicatorMode.OnCall, "On Call stays visible before its deadline");
                now++;
                app.CheckAutoHide();
                Check(app.Mode == IndicatorMode.Hidden && !indicator.Visible && !on.Checked && on.Text == "Start On Call Indicator",
                    "On Call expires exactly at its deadline and resets the menu");
                off.PerformClick();
                Check(app.Mode == IndicatorMode.OffCall && off.Checked && !on.Checked && off.Text == "Stop Off Call Indicator" &&
                    indicator.Text == IndicatorForm.OffMessage && indicator.BackColor == ColorTranslator.FromHtml("#187A45"),
                    "Off Call action shows the green Off Call message");
                now += 60000;
                app.CheckAutoHide();
                Check(app.Mode == IndicatorMode.OffCall, "Off Call uses its own longer timer");
                now += 60000;
                app.CheckAutoHide();
                Check(app.Mode == IndicatorMode.Hidden && !off.Checked && !indicator.Visible, "Off Call timer hides only the sign and clears its menu state");

                on.PerformClick();
                now += 30000;
                off.PerformClick();
                now += 30000;
                app.CheckAutoHide();
                Check(app.Mode == IndicatorMode.OffCall && !on.Checked && off.Checked, "Switching modes cancels the previous mode's deadline");
                now += 89999;
                app.CheckAutoHide();
                Check(app.Mode == IndicatorMode.OffCall, "Replacement mode gets its full configured duration");
                now++;
                app.CheckAutoHide();
                Check(app.Mode == IndicatorMode.Hidden, "Replacement mode expires at its own deadline");

                off.PerformClick();
                now += 30000;
                off.PerformClick();
                now += 300000;
                app.CheckAutoHide();
                Check(app.Mode == IndicatorMode.Hidden, "Manual stop cancels the timer");
                off.PerformClick();
                now += 119999;
                app.CheckAutoHide();
                Check(app.Mode == IndicatorMode.OffCall, "Restarting a stopped sign starts a fresh timer");
                on.PerformClick();
                Check(app.Mode == IndicatorMode.OnCall && !off.Checked && on.Checked, "On Call replaces Off Call");
                now += 30000;
                settings.BackgroundColor = "#223344";
                settings.OffTimerMinutes = 5;
                app.ApplySettings(settings);
                now += 30000;
                app.CheckAutoHide();
                Check(app.Mode == IndicatorMode.Hidden, "Appearance and inactive timer edits preserve the active deadline");

                on.PerformClick();
                now += 30000;
                settings.OnTimerMinutes = 2;
                app.ApplySettings(settings);
                now += 60000;
                app.CheckAutoHide();
                Check(app.Mode == IndicatorMode.OnCall, "Changing active duration restarts the countdown");
                now += 60000;
                app.CheckAutoHide();
                Check(app.Mode == IndicatorMode.Hidden, "Updated timer expires after the new full duration");
                on.PerformClick();
                settings.OnTimerEnabled = false;
                app.ApplySettings(settings);
                now += 10000000;
                app.CheckAutoHide();
                Check(app.Mode == IndicatorMode.OnCall, "Disabling active timer cancels pending expiration");
                settings.OnTimerEnabled = true;
                settings.OnTimerHours = 24;
                settings.OnTimerMinutes = 45;
                app.ApplySettings(settings);
                now += (long)TimeSpan.FromHours(24).TotalMilliseconds;
                app.CheckAutoHide();
                Check(app.Mode == IndicatorMode.OnCall, "Hours and minutes are added without timer overflow");
                now += (long)TimeSpan.FromMinutes(45).TotalMilliseconds;
                // Exercise the actual WinForms Tick wiring with an advanced clock, not a 24-hour wait.
                var wait = Stopwatch.StartNew();
                while (app.Mode != IndicatorMode.Hidden && wait.ElapsedMilliseconds < 2000)
                {
                    Application.DoEvents();
                    Thread.Sleep(10);
                }
                Check(app.Mode == IndicatorMode.Hidden, "WinForms timer tick hides an expired sign after elapsed time or sleep");
                off.PerformClick();
                settings.OffTimerEnabled = false;
                app.ApplySettings(settings);
                now += 10000000;
                app.CheckAutoHide();
                Check(app.Mode == IndicatorMode.OffCall, "Off Call timer can be disabled independently");
            }
        }

        private static void TestBounds()
        {
            Check(IndicatorForm.DockBounds(new Rectangle(0, 0, 1920, 1040), 420, 130) ==
                new Rectangle(1500, 0, 420, 130), "Indicator docks top-right");
            Check(IndicatorForm.DockBounds(new Rectangle(-1920, 40, 1920, 1040), 420, 130) ==
                new Rectangle(-420, 40, 420, 130), "Docking supports negative coordinates and top taskbar");
            Check(IndicatorForm.DockBounds(new Rectangle(0, 0, 800, 600), 2000, 1200) ==
                new Rectangle(0, 0, 800, 600), "Oversized indicator fits the work area");
            var area = new Rectangle(-1000, 40, 1001, 801);
            var locations = new[]
            {
                new Point(-1000, 40), new Point(-600, 40), new Point(-200, 40),
                new Point(-1000, 390), new Point(-600, 390), new Point(-200, 390),
                new Point(-1000, 740), new Point(-600, 740), new Point(-200, 740)
            };
            for (int i = 0; i < locations.Length; i++)
            {
                Check(IndicatorForm.DockBounds(area, 201, 101, (ScreenPosition)i) == new Rectangle(locations[i], new Size(201, 101)),
                    PositionPicker.PositionNames[i] + " aligns correctly with negative screen coordinates and work-area offset");
                Check(IndicatorForm.DockBounds(area, 2000, 1200, (ScreenPosition)i) == area,
                    PositionPicker.PositionNames[i] + " caps oversized windows to the work area");
            }
        }

        private static void TestMessagesAndPositions(string captureDirectory)
        {
            long now = 1000;
            var saved = new AppSettings { OnTimerEnabled = true, OffTimerEnabled = true };
            using (var app = new TrayApplication(saved, delegate { return now; }))
            {
                var indicator = (IndicatorForm)typeof(TrayApplication).GetField("indicator", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(app);
                var content = (IndicatorContent)indicator.Controls[0];
                var area = Screen.PrimaryScreen.WorkingArea;
                app.Toggle(IndicatorMode.OnCall);
                app.AddMinutes(5);
                now += 60000;
                app.CheckAutoHide();
                bool saveCalled = false;
                using (var form = new SettingsForm(saved.Copy(), false, delegate { saveCalled = true; }, app.PreviewPosition))
                {
                    form.Show();
                    Application.DoEvents();
                    Check(Find<TextBox>(form, "On Call Message").Text == IndicatorForm.Message &&
                        Find<TextBox>(form, "Off Call Message").Text == IndicatorForm.OffMessage, "Both message inputs show their current defaults");
                    Find<TextBox>(form, "On Call Message").Text = "Dad's in a meeting";
                    var previewPanel = (AlertSettingsPanel)Find<Panel>(form, "On Call preview").Parent.Parent;
                    var preview = (IndicatorContent)typeof(AlertSettingsPanel).GetField("previewContent", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(previewPanel);
                    Check(preview.Text == "Dad's in a meeting" && indicator.Text == IndicatorForm.Message,
                        "Message edits update the settings preview without applying unsaved text");
                    foreach (ScreenPosition position in Enum.GetValues(typeof(ScreenPosition)))
                    {
                        var button = Find<Button>(form, "Position " + PositionPicker.PositionNames[(int)position]);
                        button.PerformClick();
                        Check(indicator.Bounds == IndicatorForm.DockBounds(area, saved.Width, saved.Height, position) && !saveCalled,
                            "Picker moves immediately to " + position + " without Save");
                        Check(Find<PositionPicker>(form, "Screen position picker").SelectedPosition == position,
                            "Picker highlights " + position);
                    }
                    indicator.Reposition();
                    Check(indicator.Bounds == IndicatorForm.DockBounds(area, saved.Width, saved.Height, ScreenPosition.BottomRight),
                        "Periodic repositioning retains the live selection");
                    app.CheckAutoHide();
                    Check(content.CountdownText == "For 9:00 more minutes.", "Live position changes preserve the running countdown and quick-added time");
                    ((Button)form.CancelButton).PerformClick();
                    Check(indicator.Bounds == IndicatorForm.DockBounds(area, saved.Width, saved.Height, saved.Position) && !saveCalled &&
                        indicator.Text == IndicatorForm.Message, "Cancel restores saved position and discards unsaved messages");
                }
                using (var form = new SettingsForm(saved.Copy(), false, delegate { }, app.PreviewPosition))
                {
                    form.Show();
                    Application.DoEvents();
                    Find<Button>(form, "Position Bottom left").PerformClick();
                    form.Close();
                    Check(indicator.Bounds == IndicatorForm.DockBounds(area, saved.Width, saved.Height, saved.Position),
                        "Closing Settings with X restores saved position");
                }
                using (var form = new SettingsForm(saved.Copy(), false, delegate(AppSettings value, bool startup)
                {
                    saved = value.Copy();
                    app.ApplySettings(value);
                }, app.PreviewPosition))
                {
                    form.Show();
                    Application.DoEvents();
                    Find<TextBox>(form, "On Call Message").Text = "Dad's in a meeting";
                    Find<TextBox>(form, "Off Call Message").Text = "Back for snacks & stories";
                    Find<Button>(form, "Position Center left").PerformClick();
                    app.Toggle(IndicatorMode.OffCall);
                    Check(indicator.Bounds == IndicatorForm.DockBounds(area, saved.Width, saved.Height, ScreenPosition.CenterLeft),
                        "Switching modes uses the current unsaved global position");
                    ((Button)form.AcceptButton).PerformClick();
                    Check(saved.OnMessage == "Dad's in a meeting" && saved.OffMessage == "Back for snacks & stories" &&
                        saved.Position == ScreenPosition.CenterLeft && indicator.Text == saved.OffMessage && content.Text == saved.OffMessage,
                        "Save applies both custom messages and commits the selected shared position");
                    Check(indicator.Bounds == IndicatorForm.DockBounds(area, saved.Width, saved.Height, ScreenPosition.CenterLeft),
                        "Closing after Save retains the new position");
                }
                Capture(indicator, captureDirectory, "custom-message.png");
                app.Toggle(IndicatorMode.OffCall);
                using (var form = new SettingsForm(saved.Copy(), false, delegate { }, app.PreviewPosition))
                {
                    form.Show();
                    Application.DoEvents();
                    Check(Find<PositionPicker>(form, "Screen position picker").SelectedPosition == saved.Position &&
                        Find<TextBox>(form, "On Call Message").Text == saved.OnMessage && Find<TextBox>(form, "Off Call Message").Text == saved.OffMessage,
                        "Reopening Settings restores the saved position and both custom messages");
                    Find<Button>(form, "Position Bottom center").PerformClick();
                    Check(!indicator.Visible && app.Mode == IndicatorMode.Hidden, "Picking a position does not turn on a hidden indicator");
                    app.Toggle(IndicatorMode.OnCall);
                    Check(indicator.Bounds == IndicatorForm.DockBounds(area, saved.Width, saved.Height, ScreenPosition.BottomCenter) && indicator.Text == saved.OnMessage,
                        "Next indicator start uses the live selection and saved custom text");
                    ((Button)form.CancelButton).PerformClick();
                }
                var changed = saved.Copy();
                changed.OnMessage = new string('a', 200);
                app.ApplySettings(changed);
                var tray = (NotifyIcon)typeof(TrayApplication).GetField("tray", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(app);
                Check(indicator.Text == changed.OnMessage && tray.Text.Length <= 63, "Long custom messages do not overflow the tray tooltip");
                changed.OnMessage = "";
                changed.OffMessage = " ";
                app.ApplySettings(changed);
                Check(indicator.Text == IndicatorForm.Message, "Blank On Call message restores its default");
                app.Toggle(IndicatorMode.OffCall);
                Check(indicator.Text == IndicatorForm.OffMessage, "Blank Off Call message restores its default");
            }
        }

        private static void TestCountdown(string captureDirectory)
        {
            Check(IndicatorContent.FormatCountdown(TimeSpan.FromMinutes(5)) == "For 5:00 more minutes.", "Countdown formats whole minutes with new wording");
            Check(IndicatorContent.FormatCountdown(TimeSpan.FromSeconds(299)) == "For 4:59 more minutes.", "Countdown shows live seconds within minutes");
            Check(IndicatorContent.FormatCountdown(TimeSpan.FromSeconds(3899)) == "For 64:59 more minutes.", "Countdown uses total minutes for longer durations");
            Check(IndicatorContent.FormatCountdown(TimeSpan.FromHours(25)) == "For 1500:00 more minutes.", "Countdown does not wrap at one day");
            Check(IndicatorContent.FormatCountdown(TimeSpan.FromMilliseconds(1)) == "For 0:01 more minutes.", "Countdown rounds up the final partial second");
            Check(IndicatorContent.FormatCountdown(TimeSpan.FromSeconds(-1)) == "For 0:00 more minutes.", "Countdown never displays negative time");
            using (var focusWindow = new Form())
            {
                focusWindow.Show();
                focusWindow.Activate();
                Application.DoEvents();
                IntPtr foreground = GetForegroundWindow();
                foreach (var mode in new[] { IndicatorMode.OnCall, IndicatorMode.OffCall })
                {
                    long now = 1000;
                    var settings = new AppSettings { OnTimerEnabled = true, OffTimerEnabled = true };
                    using (var app = new TrayApplication(settings, delegate { return now; }))
                    {
                        var indicator = (IndicatorForm)typeof(TrayApplication).GetField("indicator", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(app);
                        var content = (IndicatorContent)indicator.Controls[0];
                        app.Toggle(mode);
                        Application.DoEvents();
                        Check(content.CountdownText == "For 5:00 more minutes.", mode + " displays initial remaining time immediately");
                        var countdownFont = (Font)typeof(IndicatorContent).GetField("countdownFont", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(content);
                        var messageFont = (Font)typeof(IndicatorContent).GetField("messageFont", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(content);
                        Check(countdownFont.Bold && countdownFont.Size < messageFont.Size, mode + " countdown font is bold and smaller than the main text");
                        now += 1000;
                        var tickWait = Stopwatch.StartNew();
                        while (content.CountdownText == "For 5:00 more minutes." && tickWait.ElapsedMilliseconds < 2000)
                        {
                            Application.DoEvents();
                            Thread.Sleep(10);
                        }
                        Check(content.CountdownText == "For 4:59 more minutes.", mode + " Windows timer tick updates the live countdown");
                        long deadline = 301000;
                        foreach (int minutes in new[] { 5, 10, 15, 30, 60 })
                        {
                            var button = Find<Button>(indicator, "Add " + minutes + " minutes");
                            Check(button.Visible && content.ClientRectangle.Contains(button.Bounds) && button.BackColor.GetBrightness() < content.BackColor.GetBrightness(),
                                mode + " " + button.Text + " button is visible, contained, and darker than the background");
                            button.GetType().GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(button, new object[] { EventArgs.Empty });
                            deadline += minutes * 60000L;
                            Check(content.CountdownText == IndicatorContent.FormatCountdown(TimeSpan.FromMilliseconds(deadline - now)),
                                mode + " " + button.Text + " adds the exact increment immediately");
                        }
                        Check(GetForegroundWindow() == foreground, mode + " button actions do not activate the popup");
                        Capture(indicator, captureDirectory, mode == IndicatorMode.OnCall ? "on-countdown.png" : "off-countdown.png");

                        // Send a real mouse click through Windows to check focus and button event delivery.
                        var fiveMinuteButton = Find<Button>(indicator, "Add 5 minutes");
                        Point originalCursor = Cursor.Position;
                        try
                        {
                            Cursor.Position = fiveMinuteButton.PointToScreen(new Point(fiveMinuteButton.Width / 2, fiveMinuteButton.Height / 2));
                            mouse_event(0x0002, 0, 0, 0, UIntPtr.Zero);
                            mouse_event(0x0004, 0, 0, 0, UIntPtr.Zero);
                            var wait = Stopwatch.StartNew();
                            string previous = content.CountdownText;
                            while (content.CountdownText == previous && wait.ElapsedMilliseconds < 2000)
                            {
                                Application.DoEvents();
                                Thread.Sleep(10);
                            }
                        }
                        finally { Cursor.Position = originalCursor; }
                        deadline += 300000;
                        Check(content.CountdownText == IndicatorContent.FormatCountdown(TimeSpan.FromMilliseconds(deadline - now)),
                            mode + " a single physical mouse click extends the countdown once");
                        Check(GetForegroundWindow() == foreground && (GetWindowLong(indicator.Handle, -20) & 0x8) != 0,
                            mode + " clicking Add time preserves keyboard focus and topmost state");
                        settings.FontColor = "#FFEEDD";
                        settings.OffFontColor = "#FFEEDD";
                        app.ApplySettings(settings);
                        Check(content.CountdownText == IndicatorContent.FormatCountdown(TimeSpan.FromMilliseconds(deadline - now)),
                            mode + " appearance edits preserve quick-added time");
                        now = deadline - 1;
                        app.CheckAutoHide();
                        Check(app.Mode == mode && content.CountdownText == "For 0:01 more minutes.", mode + " remains visible until extended deadline");
                        now++;
                        app.AddMinutes(5);
                        Check(app.Mode == IndicatorMode.Hidden && content.CountdownText == "", mode + " an expired timer cannot be revived by a queued click");
                        app.Toggle(mode);
                        Check(content.CountdownText == "For 5:00 more minutes.", mode + " restarting uses saved duration, not quick-added time");
                        settings.OnTimerEnabled = settings.OffTimerEnabled = false;
                        app.ApplySettings(settings);
                        app.AddMinutes(60);
                        Check(content.CountdownText == "" && !fiveMinuteButton.Visible && app.Mode == mode,
                            mode + " disabling auto-hide removes the countdown and quick-add row");
                        settings.OnTimerEnabled = settings.OffTimerEnabled = true;
                        app.ApplySettings(settings);
                        app.AddMinutes(60);
                        var otherMode = mode == IndicatorMode.OnCall ? IndicatorMode.OffCall : IndicatorMode.OnCall;
                        app.Toggle(otherMode);
                        Check(content.CountdownText == "For 5:00 more minutes.", "Switching from " + mode + " does not transfer added time");
                        settings.Width = 160;
                        settings.Height = 60;
                        app.ApplySettings(settings);
                        Check(content.ClientRectangle.Contains(fiveMinuteButton.Bounds) &&
                            content.ClientRectangle.Contains(Find<Button>(indicator, "Add 60 minutes").Bounds),
                            "Quick-add row fits even at the minimum configured size");
                    }
                }
            }
        }

        private static void TestStartup()
        {
            // Exercise the real registry API without changing the user's actual startup entries.
            string keyPath = @"Software\DadsOnCall.Tests-" + Guid.NewGuid().ToString("N");
            var startup = new StartupRegistration(keyPath);
            try
            {
                Check(!startup.IsEnabled(), "Startup defaults to disabled when no entry exists");
                startup.SetEnabled(false, "unused");
                Check(!startup.IsEnabled(), "Disabling absent startup entry is safe");
                startup.SetEnabled(true, @"C:\Apps With Spaces\DadsOnCall.exe");
                Check(startup.IsEnabled(), "Enabling startup creates a per-user entry");
                using (var key = Registry.CurrentUser.OpenSubKey(keyPath, true))
                {
                    Check((string)key.GetValue(StartupRegistration.ValueName) == "\"C:\\Apps With Spaces\\DadsOnCall.exe\"",
                        "Startup executable path is quoted for spaces");
                    key.SetValue("UnrelatedApp", "keep");
                }
                startup.SetEnabled(true, @"C:\Moved\DadsOnCall.exe");
                using (var key = Registry.CurrentUser.OpenSubKey(keyPath))
                    Check((string)key.GetValue(StartupRegistration.ValueName) == "\"C:\\Moved\\DadsOnCall.exe\"",
                        "Saving enabled startup refreshes the executable location");
                startup.SetEnabled(false, "unused");
                Check(!startup.IsEnabled(), "Disabling startup removes the app entry");
                using (var key = Registry.CurrentUser.OpenSubKey(keyPath))
                    Check((string)key.GetValue("UnrelatedApp") == "keep", "Other startup entries are preserved");
            }
            finally { Registry.CurrentUser.DeleteSubKeyTree(keyPath, false); }
        }

        private static void TestWindows(string directory, string captureDirectory)
        {
            using (var focusWindow = new Form())
            using (var indicator = new IndicatorForm(new AppSettings()))
            {
                focusWindow.Show();
                focusWindow.Activate();
                Application.DoEvents();
                IntPtr foreground = GetForegroundWindow();
                indicator.Show();
                Application.DoEvents();
                Check(GetForegroundWindow() == foreground, "Showing indicator preserves keyboard focus");
                Check(indicator.Visible && !indicator.ShowInTaskbar, "Indicator is visible and not in taskbar");
                int style = GetWindowLong(indicator.Handle, -20);
                Check((style & 0x08000000) != 0 && (style & 0x80) != 0 && (style & 0x8) != 0,
                    "Native no-activate, tool-window, and topmost styles are set");
                Check(indicator.Bounds.Right == Screen.PrimaryScreen.WorkingArea.Right &&
                    indicator.Bounds.Top == Screen.PrimaryScreen.WorkingArea.Top, "Indicator is docked on the actual screen");
                Capture(indicator, captureDirectory, "indicator.png");
                var changed = new AppSettings { Width = 530, Height = 160, BackgroundColor = "#123456" };
                indicator.ApplySettings(changed);
                Check(indicator.Width == Math.Min(530, Screen.PrimaryScreen.WorkingArea.Width) &&
                    indicator.BackColor == ColorTranslator.FromHtml("#123456"), "Active indicator applies appearance changes");
                Check(GetForegroundWindow() == foreground, "Updating indicator preserves keyboard focus");
                indicator.Hide();
                Check(!indicator.Visible, "Indicator hides");
                indicator.Show();
                Application.DoEvents();
                Check(GetForegroundWindow() == foreground && (GetWindowLong(indicator.Handle, -20) & 0x8) != 0,
                    "Repeated show stays topmost without taking focus");
                indicator.ApplySettings(changed, IndicatorMode.OffCall);
                Application.DoEvents();
                Check(indicator.Text == "Dad's Off His Call" && indicator.BackColor == ColorTranslator.FromHtml("#187A45") &&
                    indicator.Width == Math.Min(changed.Width, Screen.PrimaryScreen.WorkingArea.Width) && GetForegroundWindow() == foreground,
                    "Off Call uses its own appearance with shared dimensions and preserves focus");
                Capture(indicator, captureDirectory, "off-indicator.png");
                changed.OffFontColor = "#EEDDBB";
                changed.OffBackgroundColor = "#125533";
                changed.OffFontFamily = "Consolas";
                changed.OffFontSize = 22;
                indicator.ApplySettings(changed, IndicatorMode.OffCall);
                Check(indicator.ForeColor == ColorTranslator.FromHtml("#EEDDBB") && indicator.BackColor == ColorTranslator.FromHtml("#125533"),
                    "Off Call applies its customized colors independently");
            }
            bool saved = false;
            using (var form = new SettingsForm(new AppSettings(), false, delegate { saved = true; }))
            {
                form.Show();
                Application.DoEvents();
                using (var bitmap = new Bitmap(form.Width, form.Height)) form.DrawToBitmap(bitmap, form.ClientRectangle);
                Check(form.Visible, "Settings and preview render successfully");
                Capture(form, captureDirectory, "settings.png");
                var fontSize = Find<NumericUpDown>(form, "On Call Font size (pt)");
                var background = Find<Button>(form, "On Call Background color");
                Check(fontSize.Bottom <= background.Top, "DPI scaling preserves settings row layout");
                Check(fontSize.Width >= 120 && background.Width >= 160, "Font size and color controls remain readable after tab layout");
                var saveButton = (Button)form.AcceptButton;
                Check(saveButton.Parent.ClientRectangle.Contains(saveButton.Bounds), "Save button is fully visible at current DPI");
                var tabs = Find<TabControl>(form, "Indicator settings");
                foreach (string prefix in new[] { "On Call ", "Off Call " })
                {
                    tabs.SelectedIndex = prefix == "On Call " ? 0 : 1;
                    Application.DoEvents();
                    var enabled = Find<CheckBox>(form, prefix + "timer enabled");
                    var hours = Find<ComboBox>(form, prefix + "timer hours");
                    var minutes = Find<ComboBox>(form, prefix + "timer minutes");
                    Check(!enabled.Checked && !hours.Enabled && !minutes.Enabled, prefix + "timer defaults unchecked with disabled dropdowns");
                    Check(hours.Items.Count == 25 && (int)hours.Items[0] == 0 && (int)hours.Items[24] == 24,
                        prefix + "hours offer 0 through 24");
                    Check(minutes.Items.Count == AppSettings.MinuteChoices.Length, prefix + "minutes contain all supported choices");
                    for (int i = 0; i < minutes.Items.Count; i++)
                        if ((int)minutes.Items[i] != AppSettings.MinuteChoices[i]) throw new Exception("Incorrect minute choices");
                    enabled.Checked = true;
                    Check(hours.Enabled && minutes.Enabled, prefix + "timer checkbox enables its own duration controls");
                    var preview = Find<Panel>(form, prefix + "preview");
                    Check(preview.Height > 30 && enabled.Parent.ClientRectangle.Contains(enabled.Bounds), prefix + "preview and timer controls fit at current DPI");
                }
                Capture(form, captureDirectory, "off-settings.png");
                var startup = Find<CheckBox>(form, "Start with Windows");
                Check(!startup.Checked && startup.Parent.ClientRectangle.Contains(startup.Bounds),
                    "Startup checkbox reflects disabled state and fits the settings window");
                startup.Checked = true;
                form.Size = new Size(form.Width - 150, form.Height / 2);
                Application.DoEvents();
                Check(form.VerticalScroll.Visible && form.HorizontalScroll.Visible, "Settings scroll on smaller screens rather than clipping controls");
                form.ScrollControlIntoView(saveButton);
                Application.DoEvents();
                Check(form.RectangleToScreen(form.ClientRectangle).Contains(saveButton.RectangleToScreen(saveButton.ClientRectangle)),
                    "Save button can be reached by scrolling a smaller settings window");
                ((Button)form.CancelButton).PerformClick();
                Check(form.IsDisposed && !saved, "Cancel closes modeless settings without saving");
            }
            string path = Path.Combine(directory, "ui-settings.xml");
            bool startupSaved = true;
            using (var form = new SettingsForm(new AppSettings(), true, delegate(AppSettings value, bool enableStartup)
            {
                SettingsStore.Save(path, value);
                startupSaved = enableStartup;
            }))
            {
                form.Show();
                Application.DoEvents();
                Find<NumericUpDown>(form, "Window width in pixels").Value = 640;
                Find<NumericUpDown>(form, "Window height in pixels").Value = 190;
                Find<NumericUpDown>(form, "On Call Font size (pt)").Value = 31;
                Find<ComboBox>(form, "On Call Font type").SelectedItem = "Arial";
                Find<CheckBox>(form, "On Call timer enabled").Checked = true;
                Find<ComboBox>(form, "On Call timer hours").SelectedItem = 1;
                Find<ComboBox>(form, "On Call timer minutes").SelectedItem = 0;
                Find<TabControl>(form, "Indicator settings").SelectedIndex = 1;
                Application.DoEvents();
                Find<NumericUpDown>(form, "Off Call Font size (pt)").Value = 20;
                Find<ComboBox>(form, "Off Call Font type").SelectedItem = "Consolas";
                Find<CheckBox>(form, "Off Call timer enabled").Checked = true;
                Find<ComboBox>(form, "Off Call timer hours").SelectedItem = 0;
                Find<ComboBox>(form, "Off Call timer minutes").SelectedItem = 10;
                var startup = Find<CheckBox>(form, "Start with Windows");
                Check(startup.Checked, "Startup checkbox reflects enabled state");
                startup.Checked = false;
                ((Button)form.AcceptButton).PerformClick();
                bool recovered;
                var settings = SettingsStore.Load(path, out recovered);
                Check(form.IsDisposed && !recovered && settings.Width == 640 && settings.Height == 190 &&
                    settings.FontSize == 31 && settings.FontFamily == "Arial", "Save closes settings and persists edited controls");
                Check(!startupSaved, "Save passes the changed startup preference");
                Check(settings.OffFontSize == 20 && settings.OffFontFamily == "Consolas" && settings.OnTimerEnabled &&
                    settings.OnTimerHours == 1 && settings.OnTimerMinutes == 0 && settings.OffTimerEnabled &&
                    settings.OffTimerHours == 0 && settings.OffTimerMinutes == 10, "Save persists both tabs including independent timers");
                using (var reopened = new SettingsForm(settings, false, delegate { }))
                {
                    reopened.Show();
                    Application.DoEvents();
                    Check(Find<CheckBox>(reopened, "On Call timer enabled").Checked &&
                        (int)Find<ComboBox>(reopened, "On Call timer hours").SelectedItem == 1 &&
                        (int)Find<ComboBox>(reopened, "Off Call timer minutes").SelectedItem == 10 &&
                        Find<NumericUpDown>(reopened, "Off Call Font size (pt)").Value == 20,
                        "Reopened settings restore both saved tabs");
                    reopened.Close();
                }
            }
            using (var app = new TrayApplication(new AppSettings()))
            {
                Check(app.Mode == IndicatorMode.Hidden, "Tray app starts with both indicators hidden");
                app.Toggle(IndicatorMode.OnCall);
                Application.DoEvents();
                Check(app.Mode == IndicatorMode.OnCall, "Tray action starts indicator");
                app.Toggle(IndicatorMode.OnCall);
                Check(app.Mode == IndicatorMode.Hidden, "Tray action stops indicator");
            }
        }

        private static T Find<T>(Control root, string accessibleName) where T : Control
        {
            foreach (Control child in root.Controls)
            {
                if (child is T && child.AccessibleName == accessibleName) return (T)child;
                var found = Find<T>(child, accessibleName);
                if (found != null) return found;
            }
            return null;
        }

        private static void Capture(Form form, string directory, string name)
        {
            if (directory == null) return;
            form.Refresh();
            Application.DoEvents();
            Thread.Sleep(200);
            using (var bitmap = new Bitmap(form.Width, form.Height))
            {
                using (var graphics = Graphics.FromImage(bitmap))
                    graphics.CopyFromScreen(form.Location, Point.Empty, form.Size);
                bitmap.Save(Path.Combine(directory, name), System.Drawing.Imaging.ImageFormat.Png);
            }
        }
    }
}
