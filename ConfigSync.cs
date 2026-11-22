using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace SystemSoundsKeeper
{
    public sealed class ConfigSyncResult
    {
        public string text;
        public int added;
        public string backupPath;
    }

    public static class ConfigSync
    {
        private static readonly Encoding Utf8 = new UTF8Encoding(false);

        public static ConfigSyncResult Merge(string original, IEnumerable<DeviceRule> discovered)
        {
            // Validate the whole file first. Never silently replace invalid user settings.
            Configuration current = Configuration.Parse(original);
            var serializer = new JavaScriptSerializer();
            var document = (Dictionary<string, object>)serializer.DeserializeObject(original);
            var known = new HashSet<string>(current.devices.Select(d => d.id), StringComparer.OrdinalIgnoreCase);
            string devicesKey = document.Keys.Single(k => String.Equals(k, "devices", StringComparison.OrdinalIgnoreCase));
            var devices = new List<object>((object[])document[devicesKey]);
            int added = 0;
            foreach (DeviceRule device in discovered)
            {
                if (device == null || String.IsNullOrWhiteSpace(device.id) || !known.Add(device.id)) continue;
                devices.Add(new Dictionary<string, object> {
                    { "id", device.id },
                    { "name", String.IsNullOrWhiteSpace(device.name) ? device.id : device.name },
                    { "volumePercent", current.defaultVolumePercent.Value }
                });
                added++;
            }
            // Preserve the original bytes/formatting and modification time on a no-op scan.
            if (added == 0) return new ConfigSyncResult { text = original };
            document[devicesKey] = devices;
            string merged = PrettyJson(serializer.Serialize(document)) + Environment.NewLine;
            Configuration.Parse(merged);
            return new ConfigSyncResult { text = merged, added = added };
        }

        public static ConfigSyncResult UpdateFile(string path, IEnumerable<DeviceRule> discovered)
        {
            path = Path.GetFullPath(path);
            string original = File.ReadAllText(path, Utf8);
            ConfigSyncResult result = Merge(original, discovered);
            if (result.added == 0) return result;

            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            string backups = Path.Combine(Path.GetDirectoryName(path), "logs");
            Directory.CreateDirectory(backups);
            string backup = Path.Combine(backups, "config-before-sync-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                File.WriteAllText(temp, result.text, Utf8);
                // Avoid replacing edits made while scanning/merging; retry on the next startup/sync.
                if (File.ReadAllText(path, Utf8) != original)
                    throw new IOException("Configuration changed during device sync; no changes were written. Retry sync.");
                File.Replace(temp, path, backup);
                result.backupPath = backup;
                return result;
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        private static string PrettyJson(string json)
        {
            var output = new StringBuilder();
            bool quoted = false;
            bool escaped = false;
            int depth = 0;
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                if (quoted)
                {
                    output.Append(c);
                    if (escaped) escaped = false;
                    else if (c == '\\') escaped = true;
                    else if (c == '"') quoted = false;
                    continue;
                }
                if (c == '"') { quoted = true; output.Append(c); }
                else if (c == '{' || c == '[')
                {
                    output.Append(c);
                    if (i + 1 < json.Length && json[i + 1] == (c == '{' ? '}' : ']')) output.Append(json[++i]);
                    else { depth++; output.AppendLine(); output.Append(' ', depth * 2); }
                }
                else if (c == '}' || c == ']') { depth--; output.AppendLine(); output.Append(' ', depth * 2); output.Append(c); }
                else if (c == ',') { output.Append(c); output.AppendLine(); output.Append(' ', depth * 2); }
                else if (c == ':') output.Append(": ");
                else if (!Char.IsWhiteSpace(c)) output.Append(c);
            }
            return output.ToString();
        }
    }
}
