using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace SystemSoundsKeeper
{
    public static class PolicyTests
    {
        private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Reject(string json)
        {
            bool failed = false;
            try { Configuration.Parse(json); } catch { failed = true; }
            Assert(failed, "Invalid configuration was accepted: " + json);
        }

        private static void TestConfigSync()
        {
            string original = "{\n  \"defaultVolumePercent\":45,\"comment\":\"保留备注\",\"devices\":[" +
                "{\"id\":\"usb\",\"name\":\"手写设备备注\",\"volumePercent\":23,\"extra\":{\"note\":\"a,\\\"b\\\"[c]\\\\d\"}}," +
                "{\"id\":\"offline\",\"name\":\"离线设备\",\"volumePercent\":17}]}";
            var discovered = new[] {
                new DeviceRule { id = "USB", name = "Driver renamed this", volumePercent = 100 },
                new DeviceRule { id = "bluetooth", name = "新蓝牙耳机", volumePercent = 100 },
                new DeviceRule { id = "BLUETOOTH", name = "Duplicate" }
            };
            ConfigSyncResult merged = ConfigSync.Merge(original, discovered);
            Assert(merged.added == 1, "Only new endpoint IDs may be appended; IDs are case insensitive.");
            Configuration config = Configuration.Parse(merged.text);
            Assert(config.devices.Count == 3 && config.devices[2].id == "bluetooth", "Existing order/offline entries must be retained and new devices appended.");
            Assert(config.devices[0].name == "手写设备备注" && config.ForDevice("usb") == 23, "Existing notes/custom volumes must never be replaced by discovery.");
            Assert(config.ForDevice("offline") == 17 && config.ForDevice("bluetooth") == 45, "Offline overrides must remain; new devices inherit the configured default, not their current audio volume.");
            var serializer = new JavaScriptSerializer();
            var before = (Dictionary<string, object>)serializer.DeserializeObject(original);
            var after = (Dictionary<string, object>)serializer.DeserializeObject(merged.text);
            Assert((string)after["comment"] == "保留备注", "Unknown root fields must be preserved.");
            var oldUsb = (Dictionary<string, object>)((object[])before["devices"])[0];
            var newUsb = (Dictionary<string, object>)((object[])after["devices"])[0];
            Assert(serializer.Serialize(oldUsb["extra"]) == serializer.Serialize(newUsb["extra"]), "Unknown nested fields/escaped strings must be preserved.");
            Assert(ConfigSync.Merge(original, new[] { discovered[0] }).text == original, "No-op merging must keep the exact original formatting.");
            Assert(ConfigSync.Merge(merged.text, discovered).added == 0, "Repeated discovery must be idempotent.");

            string directory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config-sync-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "config.json");
            try
            {
                File.WriteAllText(path, original, new UTF8Encoding(false));
                ConfigSyncResult saved = ConfigSync.UpdateFile(path, discovered);
                Assert(File.ReadAllText(saved.backupPath) == original, "Backup must contain the entire original configuration.");
                Assert(File.ReadAllText(path) == merged.text, "Merged configuration must be persisted.");
                byte[] bytes = File.ReadAllBytes(path);
                Assert(bytes[0] != 0xEF, "Configuration must be UTF-8 without BOM.");
                DateTime stamp = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                File.SetLastWriteTimeUtc(path, stamp);
                ConfigSyncResult repeated = ConfigSync.UpdateFile(path, discovered);
                Assert(repeated.added == 0 && repeated.backupPath == null && File.GetLastWriteTimeUtc(path) == stamp, "No-op scans must not rewrite the file or create backups.");
                string invalid = "{\"defaultVolumePercent\":999,\"devices\":[]}";
                File.WriteAllText(path, invalid, new UTF8Encoding(false));
                bool rejected = false;
                try { ConfigSync.UpdateFile(path, discovered); } catch { rejected = true; }
                Assert(rejected && File.ReadAllText(path) == invalid, "Invalid user configuration must be left untouched.");
            }
            finally
            {
                // Only files created inside this unique test directory are removed.
                foreach (string file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories)) File.Delete(file);
                string logs = Path.Combine(directory, "logs");
                if (Directory.Exists(logs)) Directory.Delete(logs);
                Directory.Delete(directory);
            }
        }

        public static int Main()
        {
            try
            {
                Configuration config = Configuration.Parse("{\"defaultVolumePercent\":50,\"devices\":[{\"id\":\"usb\",\"volumePercent\":30}]}");
                Assert(config.ForDevice("USB") == 30, "A device override must take precedence regardless of ID casing.");
                Assert(config.ForDevice("new-bluetooth") == 50, "A new/unconfigured output must use the global default.");
                Reject("{\"defaultVolumePercent\":101,\"devices\":[]}");
                Reject("{\"defaultVolumePercent\":50,\"devices\":[{\"id\":\"a\",\"volumePercent\":-1}]}");
                Reject("{\"defaultVolumePercent\":50,\"devices\":[{\"id\":\"a\",\"volumePercent\":20},{\"id\":\"A\",\"volumePercent\":30}]}");
                Reject("{\"devices\":[]}");
                Reject("{\"defaultVolumePercent\":50}");
                Reject("{\"defaultVolumePercent\":50,\"devices\":[{\"id\":\"a\"}]}");

                var tracker = new SessionTracker();
                Assert(tracker.NeedsApply("usb", "session-1"), "Initial session must be configured.");
                tracker.MarkApplied("usb", "session-1");
                Assert(!tracker.NeedsApply("usb", "session-1"), "Manual volume changes must not be continually overwritten.");
                Assert(tracker.NeedsApply("bluetooth", "late-session"), "A session appearing after connection must be configured.");
                tracker.MarkApplied("bluetooth", "late-session");
                tracker.Reset("usb");
                Assert(tracker.NeedsApply("usb", "session-1"), "Switching/reconnecting must reset the affected output.");
                Assert(!tracker.NeedsApply("bluetooth", "late-session"), "A different output's switch must not reset this output.");
                tracker.MarkApplied("usb", "session-1");
                tracker.Retain(new Dictionary<string, HashSet<string>> { { "bluetooth", new HashSet<string> { "late-session" } } });
                Assert(tracker.NeedsApply("usb", "session-1"), "An output removed then reconnected must be configured again.");
                tracker.ResetAll();
                Assert(tracker.NeedsApply("bluetooth", "late-session"), "Configuration reload must reapply existing sessions.");
                TestConfigSync();
                Console.WriteLine("PASS: per-device defaults, invalid configuration, delayed sessions, manual adjustments, reconnect, config reload.");
                Console.WriteLine("PASS: incremental device sync, offline/custom/unknown settings retained, idempotency, backup, invalid file protection.");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine("FAIL: " + ex.Message); return 1; }
        }
    }
}
