using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Xml;

namespace DadsOnCall
{
    internal sealed class UpdateInfo
    {
        internal Version Version { get; private set; }
        internal string VersionText { get; private set; }

        internal UpdateInfo(Version version, string versionText)
        {
            Version = version;
            VersionText = versionText;
        }
    }

    internal static class UpdateService
    {
        private const string ManifestUrl = "https://raw.githubusercontent.com/amac16/Dads-On-A-Call/main/src/app.manifest";
        private const string ExecutableUrl = "https://raw.githubusercontent.com/amac16/Dads-On-A-Call/main/dist/DadsOnACall.exe";
        private const string ManifestNamespace = "urn:schemas-microsoft-com:asm.v1";

        internal static UpdateInfo CheckForUpdate()
        {
            string manifest = DownloadText(ManifestUrl);
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            var document = new XmlDocument { XmlResolver = null };
            using (var reader = XmlReader.Create(new StringReader(manifest), settings)) document.Load(reader);
            var identity = document.DocumentElement.SelectSingleNode(
                "asm:assemblyIdentity", CreateNamespaceManager(document.NameTable));
            if (identity == null) throw new InvalidDataException("The remote manifest has no assembly version.");
            string versionText = identity.Attributes["version"] == null ? null : identity.Attributes["version"].Value;
            Version version;
            if (!Version.TryParse(versionText, out version))
                throw new InvalidDataException("The remote manifest has an invalid assembly version.");
            return version > new Version(AppInfo.Version) ? new UpdateInfo(version, versionText) : null;
        }

        internal static string DownloadUpdate()
        {
            string path = Path.Combine(Path.GetTempPath(), "DadsOnACall-update-" + Guid.NewGuid().ToString("N") + ".exe");
            try
            {
                using (var client = CreateClient()) client.DownloadFile(ExecutableUrl, path);
                if (new FileInfo(path).Length < 2) throw new InvalidDataException("The downloaded application is empty.");
                using (var stream = File.OpenRead(path))
                {
                    if (stream.ReadByte() != 'M' || stream.ReadByte() != 'Z')
                        throw new InvalidDataException("The downloaded file is not a Windows application.");
                }
                return path;
            }
            catch
            {
                TryDelete(path);
                throw;
            }
        }

        internal static void LaunchReplacement(string downloadedPath, string executablePath)
        {
            string scriptPath = Path.Combine(Path.GetTempPath(), "DadsOnACall-update-" + Guid.NewGuid().ToString("N") + ".cmd");
            string script = "@echo off\r\n" +
                "set \"source=" + downloadedPath + "\"\r\n" +
                "set \"target=" + executablePath + "\"\r\n" +
                ":wait\r\n" +
                "copy /y \"%source%\" \"%target%\" >nul 2>&1\r\n" +
                "if errorlevel 1 (ping 127.0.0.1 -n 2 >nul & goto wait)\r\n" +
                "start \"\" \"%target%\"\r\n" +
                "del \"%source%\" >nul 2>&1\r\n" +
                "del \"%~f0\" >nul 2>&1\r\n";
            File.WriteAllText(scriptPath, script, Encoding.Default);
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = scriptPath,
                    WorkingDirectory = Path.GetDirectoryName(executablePath),
                    WindowStyle = ProcessWindowStyle.Hidden,
                    CreateNoWindow = true,
                    UseShellExecute = true
                });
            }
            catch
            {
                TryDelete(scriptPath);
                TryDelete(downloadedPath);
                throw;
            }
        }

        private static WebClient CreateClient()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var client = new WebClient();
            client.Headers[HttpRequestHeader.UserAgent] = "DadsOnACall updater";
            return client;
        }

        private static string DownloadText(string url)
        {
            using (var client = CreateClient()) return client.DownloadString(url);
        }

        private static XmlNamespaceManager CreateNamespaceManager(XmlNameTable nameTable)
        {
            var manager = new XmlNamespaceManager(nameTable);
            manager.AddNamespace("asm", ManifestNamespace);
            return manager;
        }

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch { }
        }
    }
}
