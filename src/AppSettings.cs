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
        public bool OnTimerEnabled { get; set; }
        public int OnTimerHours { get; set; }
        public int OnTimerMinutes { get; set; }
        public bool OffTimerEnabled { get; set; }
        public int OffTimerHours { get; set; }
        public int OffTimerMinutes { get; set; }

        internal static readonly int[] MinuteChoices = { 0, 1, 2, 3, 4, 5, 10, 15, 30, 45 };

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
            OnTimerMinutes = 5;
            OffTimerMinutes = 5;
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
            OnTimerHours = Math.Max(0, Math.Min(24, OnTimerHours));
            OffTimerHours = Math.Max(0, Math.Min(24, OffTimerHours));
            if (Array.IndexOf(MinuteChoices, OnTimerMinutes) < 0 ||
                (OnTimerEnabled && OnTimerHours == 0 && OnTimerMinutes == 0)) OnTimerMinutes = 5;
            if (Array.IndexOf(MinuteChoices, OffTimerMinutes) < 0 ||
                (OffTimerEnabled && OffTimerHours == 0 && OffTimerMinutes == 0)) OffTimerMinutes = 5;
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

        public Font CreateFont(bool offCall = false)
        {
            string family = offCall ? OffFontFamily : FontFamily;
            float size = offCall ? OffFontSize : FontSize;
            try { return new Font(family, size, FontStyle.Regular, GraphicsUnit.Point); }
            catch (ArgumentException) { return new Font("Segoe UI", size, FontStyle.Regular, GraphicsUnit.Point); }
        }

        internal TimeSpan? AutoHideDuration(IndicatorMode mode)
        {
            if (mode == IndicatorMode.OnCall && OnTimerEnabled)
                return TimeSpan.FromHours(OnTimerHours) + TimeSpan.FromMinutes(OnTimerMinutes);
            if (mode == IndicatorMode.OffCall && OffTimerEnabled)
                return TimeSpan.FromHours(OffTimerHours) + TimeSpan.FromMinutes(OffTimerMinutes);
            return null;
        }

        internal void ValidateTimers()
        {
            if (OnTimerEnabled && OnTimerHours == 0 && OnTimerMinutes == 0)
                throw new InvalidOperationException("Choose a duration greater than zero for the On Call timer.");
            if (OffTimerEnabled && OffTimerHours == 0 && OffTimerMinutes == 0)
                throw new InvalidOperationException("Choose a duration greater than zero for the Off Call timer.");
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
