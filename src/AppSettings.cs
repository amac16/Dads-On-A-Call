using System;
using System.Drawing;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace DadsOnCall
{
    public enum ScreenPosition
    {
        TopLeft, TopCenter, TopRight,
        CenterLeft, Center, CenterRight,
        BottomLeft, BottomCenter, BottomRight
    }

    public sealed class AppSettings
    {
        public int Width { get; set; }
        public int Height { get; set; }
        public ScreenPosition Position { get; set; }
        public string OnMessage { get; set; }
        public string OffMessage { get; set; }
        public string BackgroundColor { get; set; }
        public string FontColor { get; set; }
        public string FontFamily { get; set; }
        public float FontSize { get; set; }
        public string OffBackgroundColor { get; set; }
        public string OffFontColor { get; set; }
        public string OffFontFamily { get; set; }
        public float OffFontSize { get; set; }
        public string HeadphonesMessage { get; set; }
        public string HeadphonesBackgroundColor { get; set; }
        public string HeadphonesFontColor { get; set; }
        public string HeadphonesFontFamily { get; set; }
        public float HeadphonesFontSize { get; set; }
        public bool OnTimerEnabled { get; set; }
        public int OnTimerHours { get; set; }
        public int OnTimerMinutes { get; set; }
        public bool OffTimerEnabled { get; set; }
        public int OffTimerHours { get; set; }
        public int OffTimerMinutes { get; set; }
        public bool HeadphonesTimerEnabled { get; set; }
        public int HeadphonesTimerHours { get; set; }
        public int HeadphonesTimerMinutes { get; set; }
        public bool OnBlinkEnabled { get; set; }
        public string OnBlinkRate { get; set; }
        public bool OffBlinkEnabled { get; set; }
        public string OffBlinkRate { get; set; }
        public bool HeadphonesBlinkEnabled { get; set; }
        public string HeadphonesBlinkRate { get; set; }

        internal static readonly int[] MinuteChoices = { 0, 1, 2, 3, 4, 5, 10, 15, 30, 45 };
        internal static readonly string[] BlinkRateChoices =
            { "Slow", "Very Slow", "Just Above Average", "Seriously", "Attention Seeking" };

        public AppSettings()
        {
            Width = 520;
            Height = 160;
            Position = ScreenPosition.TopRight;
            OnMessage = IndicatorForm.Message;
            OffMessage = IndicatorForm.OffMessage;
            BackgroundColor = "#B42335";
            FontColor = "#FFFFFF";
            FontFamily = "Segoe UI";
            FontSize = 24;
            OffBackgroundColor = "#187A45";
            OffFontColor = "#FFFFFF";
            OffFontFamily = "Segoe UI";
            OffFontSize = 24;
            HeadphonesMessage = IndicatorForm.HeadphonesMessage;
            HeadphonesBackgroundColor = "#0080C0";
            HeadphonesFontColor = "#FFFFFF";
            HeadphonesFontFamily = "Segoe UI";
            HeadphonesFontSize = 24;
            OnTimerMinutes = 5;
            OffTimerMinutes = 5;
            HeadphonesTimerMinutes = 5;
            OnBlinkRate = BlinkRateChoices[0];
            OffBlinkRate = BlinkRateChoices[0];
            HeadphonesBlinkRate = BlinkRateChoices[0];
        }

        public AppSettings Copy()
        {
            return (AppSettings)MemberwiseClone();
        }

        public void Normalize()
        {
            Width = Math.Max(160, Math.Min(2000, Width));
            Height = Math.Max(60, Math.Min(1200, Height));
            if (!Enum.IsDefined(typeof(ScreenPosition), Position)) Position = ScreenPosition.TopRight;
            OnMessage = NormalizeMessage(OnMessage, IndicatorForm.Message);
            OffMessage = NormalizeMessage(OffMessage, IndicatorForm.OffMessage);
            HeadphonesMessage = NormalizeMessage(HeadphonesMessage, IndicatorForm.HeadphonesMessage);
            FontSize = float.IsNaN(FontSize) || float.IsInfinity(FontSize)
                ? 24 : Math.Max(8, Math.Min(144, FontSize));
            BackgroundColor = NormalizeColor(BackgroundColor, "#B42335");
            FontColor = NormalizeColor(FontColor, "#FFFFFF");
            if (string.IsNullOrWhiteSpace(FontFamily)) FontFamily = "Segoe UI";
            OffFontSize = float.IsNaN(OffFontSize) || float.IsInfinity(OffFontSize)
                ? 24 : Math.Max(8, Math.Min(144, OffFontSize));
            OffBackgroundColor = NormalizeColor(OffBackgroundColor, "#187A45");
            OffFontColor = NormalizeColor(OffFontColor, "#FFFFFF");
            if (string.IsNullOrWhiteSpace(OffFontFamily)) OffFontFamily = "Segoe UI";
            HeadphonesFontSize = float.IsNaN(HeadphonesFontSize) || float.IsInfinity(HeadphonesFontSize)
                ? 24 : Math.Max(8, Math.Min(144, HeadphonesFontSize));
            HeadphonesBackgroundColor = NormalizeColor(HeadphonesBackgroundColor, "#0080C0");
            HeadphonesFontColor = NormalizeColor(HeadphonesFontColor, "#FFFFFF");
            if (string.IsNullOrWhiteSpace(HeadphonesFontFamily)) HeadphonesFontFamily = "Segoe UI";
            OnTimerHours = Math.Max(0, Math.Min(24, OnTimerHours));
            OffTimerHours = Math.Max(0, Math.Min(24, OffTimerHours));
            HeadphonesTimerHours = Math.Max(0, Math.Min(24, HeadphonesTimerHours));
            if (Array.IndexOf(MinuteChoices, OnTimerMinutes) < 0 ||
                (OnTimerEnabled && OnTimerHours == 0 && OnTimerMinutes == 0)) OnTimerMinutes = 5;
            if (Array.IndexOf(MinuteChoices, OffTimerMinutes) < 0 ||
                (OffTimerEnabled && OffTimerHours == 0 && OffTimerMinutes == 0)) OffTimerMinutes = 5;
            if (Array.IndexOf(MinuteChoices, HeadphonesTimerMinutes) < 0 ||
                (HeadphonesTimerEnabled && HeadphonesTimerHours == 0 && HeadphonesTimerMinutes == 0)) HeadphonesTimerMinutes = 5;
            OnBlinkRate = NormalizeBlinkRate(OnBlinkRate);
            OffBlinkRate = NormalizeBlinkRate(OffBlinkRate);
            HeadphonesBlinkRate = NormalizeBlinkRate(HeadphonesBlinkRate);
        }

        private static string NormalizeColor(string value, string fallback)
        {
            int rgb;
            if (value == null || value.Length != 7 || value[0] != '#' ||
                !int.TryParse(value.Substring(1), System.Globalization.NumberStyles.AllowHexSpecifier,
                    System.Globalization.CultureInfo.InvariantCulture, out rgb)) return fallback;
            return value.ToUpperInvariant();
        }

        internal static string NormalizeMessage(string value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;
            value = TruncateText(value.Trim(), 200);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }

        internal static string TruncateText(string value, int maximumLength)
        {
            int length = Math.Min(value.Length, maximumLength);
            if (length > 0 && char.IsHighSurrogate(value[length - 1])) length--;
            return value.Substring(0, length);
        }

        internal Font CreateFont(IndicatorMode mode = IndicatorMode.OnCall)
        {
            string family = mode == IndicatorMode.OffCall ? OffFontFamily : mode == IndicatorMode.HeadphonesOn ? HeadphonesFontFamily : FontFamily;
            float size = mode == IndicatorMode.OffCall ? OffFontSize : mode == IndicatorMode.HeadphonesOn ? HeadphonesFontSize : FontSize;
            try { return new Font(family, size, FontStyle.Regular, GraphicsUnit.Point); }
            catch (ArgumentException) { return new Font("Segoe UI", size, FontStyle.Regular, GraphicsUnit.Point); }
        }

        internal TimeSpan? AutoHideDuration(IndicatorMode mode)
        {
            if (mode == IndicatorMode.OnCall && OnTimerEnabled)
                return TimeSpan.FromHours(OnTimerHours) + TimeSpan.FromMinutes(OnTimerMinutes);
            if (mode == IndicatorMode.OffCall && OffTimerEnabled)
                return TimeSpan.FromHours(OffTimerHours) + TimeSpan.FromMinutes(OffTimerMinutes);
            if (mode == IndicatorMode.HeadphonesOn && HeadphonesTimerEnabled)
                return TimeSpan.FromHours(HeadphonesTimerHours) + TimeSpan.FromMinutes(HeadphonesTimerMinutes);
            return null;
        }

        internal int BlinkInterval(IndicatorMode mode)
        {
            string rate = mode == IndicatorMode.OffCall ? OffBlinkRate : mode == IndicatorMode.HeadphonesOn ? HeadphonesBlinkRate : OnBlinkRate;
            switch (rate)
            {
                case "Very Slow": return 5000;
                case "Just Above Average": return 1000;
                case "Seriously": return 500;
                case "Attention Seeking": return 100;
                default: return 2000;
            }
        }

        internal int BlinkHiddenInterval(IndicatorMode mode)
        {
            return Math.Max(1, BlinkInterval(mode) / 2);
        }

        internal bool BlinkEnabled(IndicatorMode mode)
        {
            return mode == IndicatorMode.OffCall ? OffBlinkEnabled : mode == IndicatorMode.HeadphonesOn ? HeadphonesBlinkEnabled : OnBlinkEnabled;
        }

        private static string NormalizeBlinkRate(string value)
        {
            return Array.IndexOf(BlinkRateChoices, value) >= 0 ? value : BlinkRateChoices[0];
        }

        internal void ValidateTimers()
        {
            if (OnTimerEnabled && OnTimerHours == 0 && OnTimerMinutes == 0)
                throw new InvalidOperationException("Choose a duration greater than zero for the On Call timer.");
            if (OffTimerEnabled && OffTimerHours == 0 && OffTimerMinutes == 0)
                throw new InvalidOperationException("Choose a duration greater than zero for the Off Call timer.");
            if (HeadphonesTimerEnabled && HeadphonesTimerHours == 0 && HeadphonesTimerMinutes == 0)
                throw new InvalidOperationException("Choose a duration greater than zero for the Headphones On timer.");
        }
    }

    internal static class SettingsStore
    {
        public static readonly string DefaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DadsOnCall", "settings.xml");

        public static AppSettings Load(string path, out bool recovered)
        {
            recovered = false;
            if (!File.Exists(path)) return new AppSettings();
            try
            {
                var options = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using (var reader = XmlReader.Create(path, options))
                {
                    var settings = (AppSettings)new XmlSerializer(typeof(AppSettings)).Deserialize(reader);
                    if (settings == null) throw new InvalidOperationException("Empty settings.");
                    settings.Normalize();
                    return settings;
                }
            }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is UnauthorizedAccessException ||
                    ex is InvalidOperationException || ex is XmlException)) throw;
                recovered = true;
                return new AppSettings();
            }
        }

        public static void Save(string path, AppSettings settings)
        {
            settings.Normalize();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var writer = XmlWriter.Create(temporaryPath, new XmlWriterSettings { Indent = true }))
                    new XmlSerializer(typeof(AppSettings)).Serialize(writer, settings);
                if (File.Exists(path)) File.Replace(temporaryPath, path, null);
                else File.Move(temporaryPath, path);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }
    }
}
