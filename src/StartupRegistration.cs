using Microsoft.Win32;

namespace DadsOnCall
{
    internal sealed class StartupRegistration
    {
        internal const string DefaultKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        internal const string ValueName = "DadsOnCall";
        private readonly string keyPath;

        public StartupRegistration(string registryKeyPath)
        {
            keyPath = registryKeyPath;
        }

        public bool IsEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(keyPath))
                return key != null && !string.IsNullOrWhiteSpace(key.GetValue(ValueName) as string);
        }

        public void SetEnabled(bool enabled, string executablePath)
        {
            if (enabled)
            {
                using (var key = Registry.CurrentUser.CreateSubKey(keyPath))
                    key.SetValue(ValueName, "\"" + executablePath + "\"", RegistryValueKind.String);
            }
            else
            {
                using (var key = Registry.CurrentUser.OpenSubKey(keyPath, true))
                    if (key != null) key.DeleteValue(ValueName, false);
            }
        }
    }
}
