using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;

namespace SystemSoundsKeeper
{
    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    public sealed class EndpointNotifications : IMMNotificationClient
    {
        public readonly ConcurrentQueue<string> changed = new ConcurrentQueue<string>();
        public int OnDeviceStateChanged(string id, uint state) { changed.Enqueue(id); return 0; }
        public int OnDeviceAdded(string id) { changed.Enqueue(id); return 0; }
        public int OnDeviceRemoved(string id) { changed.Enqueue(id); return 0; }
        public int OnDefaultDeviceChanged(int flow, int role, string id)
        {
            if (flow == 0 && id != null) changed.Enqueue(id);
            return 0;
        }
        public int OnPropertyValueChanged(string id, PropertyKey key) { return 0; }
    }

    internal static class Program
    {
        internal static readonly string Root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".."));
        internal static readonly string ConfigPath = Path.Combine(Root, "config.json");
        private static readonly string LogPath = Path.Combine(Root, "logs", "watcher.log");
        private static readonly Encoding Utf8 = new UTF8Encoding(false);
        private static readonly string InstanceName = MakeInstanceName();

        private static string MakeInstanceName()
        {
            using (var hash = SHA256.Create())
                return "Local\\SystemSoundsKeeper-" + BitConverter.ToString(hash.ComputeHash(Utf8.GetBytes(Root.ToUpperInvariant()))).Replace("-", "").Substring(0, 20);
        }

        [MTAThread]
        public static int Main(string[] args)
        {
            Console.OutputEncoding = Utf8;
            try
            {
                string command = args.Length == 0 ? "help" : args[0].ToLowerInvariant();
                switch (command)
                {
                    case "list":
                        using (var api = new AudioApi())
                        {
                            string json = Json(api.Snapshot(true, true));
                            File.WriteAllText(Path.Combine(Root, "devices.json"), json, Utf8);
                            Console.WriteLine(json);
                        }
                        return 0;
                    case "check":
                        Configuration.Parse(File.ReadAllText(ConfigPath, Utf8));
                        Console.WriteLine("Configuration is valid: " + ConfigPath);
                        return 0;
                    case "apply": return ApplyOnce();
                    case "sync":
                        using (var api = new AudioApi())
                        {
                            ConfigSyncResult sync = SyncDevices(api.Snapshot(true, false));
                            Console.WriteLine("Device config sync: added " + sync.added + " output device(s). Existing settings retained.");
                            if (sync.backupPath != null) Console.WriteLine("Previous config backup: " + sync.backupPath);
                        }
                        return 0;
                    case "watch": return Watch();
                    case "stop": return Stop();
                    case "status":
                        bool running = IsRunning();
                        Console.WriteLine(running ? "Watcher is running." : "Watcher is stopped.");
                        return running ? 0 : 1;
                    default:
                        Console.WriteLine("SystemSoundsKeeper: list | check | apply | sync | watch | status | stop");
                        return command == "help" ? 0 : 2;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message + " (0x" + ex.HResult.ToString("X8") + ")");
                return 2;
            }
        }

        private static string Json(object value) { return new JavaScriptSerializer().Serialize(value); }

        private static ConfigSyncResult SyncDevices(List<DeviceInfo> devices)
        {
            return ConfigSync.UpdateFile(ConfigPath, devices.Select(d => new DeviceRule { id = d.id, name = d.name }));
        }

        private static void Log(string text)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath));
            if (File.Exists(LogPath) && new FileInfo(LogPath).Length > 512 * 1024)
            {
                string oldPath = LogPath + ".old";
                if (File.Exists(oldPath)) File.Delete(oldPath);
                File.Move(LogPath, oldPath);
            }
            File.AppendAllText(LogPath, DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture) + " " + text + Environment.NewLine, Utf8);
        }

        private static int ApplyOnce()
        {
            Configuration config = Configuration.Parse(File.ReadAllText(ConfigPath, Utf8));
            int count = 0;
            int errors = 0;
            using (var api = new AudioApi())
            {
                foreach (DeviceInfo device in api.Snapshot(false, false))
                {
                    if (device.error != null) { Console.Error.WriteLine(device.name + ": " + device.error); errors++; continue; }
                    foreach (SessionInfo session in device.sessions)
                    {
                        try
                        {
                            if (api.Apply(device.id, session.instanceId, config.ForDevice(device.id), Console.WriteLine)) count++;
                        }
                        catch (Exception ex) { Console.Error.WriteLine(device.name + ": " + ex.Message); errors++; }
                    }
                }
            }
            Console.WriteLine("Applied to " + count + " System Sounds session(s). Missing sessions will be handled by watch when they appear.");
            return errors == 0 ? 0 : 2;
        }

        private static bool IsRunning()
        {
            try { using (EventWaitHandle handle = EventWaitHandle.OpenExisting(InstanceName + "-Stop")) return true; }
            catch (WaitHandleCannotBeOpenedException) { return false; }
        }

        private static int Stop()
        {
            try
            {
                using (EventWaitHandle handle = EventWaitHandle.OpenExisting(InstanceName + "-Stop")) handle.Set();
                Console.WriteLine("Stop requested.");
            }
            catch (WaitHandleCannotBeOpenedException) { Console.WriteLine("Watcher is already stopped."); }
            return 0;
        }

        private static int Watch()
        {
            // Validate before acquiring process-wide resources or applying audio changes.
            string previousText = File.ReadAllText(ConfigPath, Utf8);
            Configuration config = Configuration.Parse(previousText);
            using (var mutex = new Mutex(false, InstanceName + "-Mutex"))
            {
                bool ownsMutex;
                try { ownsMutex = mutex.WaitOne(0); } catch (AbandonedMutexException) { ownsMutex = true; }
                if (!ownsMutex) { Console.WriteLine("Watcher is already running."); return 0; }
                try
                {
                    using (var stop = new EventWaitHandle(false, EventResetMode.ManualReset, InstanceName + "-Stop"))
                    {
                        stop.Reset();
                        ConsoleCancelEventHandler cancel = delegate(object sender, ConsoleCancelEventArgs e) { e.Cancel = true; stop.Set(); };
                        Console.CancelKeyPress += cancel;
                        try { WatchLoop(stop, ref config, previousText); }
                        finally { Console.CancelKeyPress -= cancel; }
                    }
                }
                finally { mutex.ReleaseMutex(); }
            }
            return 0;
        }

        private static void WatchLoop(EventWaitHandle stop, ref Configuration config, string previousText)
        {
            var tracker = new SessionTracker();
            var notifications = new EndpointNotifications();
            var previousDefaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var errors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string previousTopology = null;
            bool needsStartupSync = true;
            AudioApi api = null;
            Log("Started pid=" + Process.GetCurrentProcess().Id + "; config=" + ConfigPath);
            try
            {
                while (!stop.WaitOne(0))
                {
                    try
                    {
                        try
                        {
                            string text = File.ReadAllText(ConfigPath, Utf8);
                            if (text != previousText)
                            {
                                previousText = text;
                                Configuration next = Configuration.Parse(text);
                                config = next;
                                tracker.ResetAll();
                                Log("Configuration reloaded; applying configured volumes.");
                            }
                            errors.Remove("config");
                        }
                        catch (Exception ex) { LogErrorOnce(errors, "config", "Keeping last valid configuration: " + ex.Message); }

                        if (api == null)
                        {
                            api = new AudioApi();
                            try { api.Subscribe(notifications); Log("Device notifications registered; polling every 1000ms for sessions."); }
                            catch (Exception ex) { Log("Device notifications unavailable; polling remains active: " + ex.Message); }
                            tracker.ResetAll();
                        }

                        var resetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        string changed;
                        while (notifications.changed.TryDequeue(out changed)) resetIds.Add(changed);
                        foreach (string id in resetIds) tracker.Reset(id);

                        List<DeviceInfo> devices = api.Snapshot(true, false);
                        if (needsStartupSync)
                        {
                            try
                            {
                                ConfigSyncResult sync = SyncDevices(devices);
                                config = Configuration.Parse(sync.text);
                                previousText = sync.text;
                                needsStartupSync = false;
                                Log("Startup device config sync: added " + sync.added + " output device(s). Existing settings retained." +
                                    (sync.backupPath == null ? "" : " Backup: " + sync.backupPath));
                                errors.Remove("config-sync");
                            }
                            catch (Exception ex) { LogErrorOnce(errors, "config-sync", "Device config sync deferred; existing config retained: " + ex.Message); }
                        }
                        string topology = String.Join("|", devices.OrderBy(d => d.id).Select(d => d.id + ":" + d.state + ":" + d.name + ":" + String.Join(",", d.defaultRoles)));
                        if (topology != previousTopology)
                        {
                            File.WriteAllText(Path.Combine(Root, "devices.json"), Json(devices), Utf8);
                            previousTopology = topology;
                            Log("Output device inventory updated (devices.json).");
                        }

                        var live = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                        foreach (DeviceInfo device in devices)
                        {
                            if ((device.state & 1) == 0) { previousDefaults.Remove(device.id); continue; }
                            string roles = String.Join(",", device.defaultRoles);
                            string oldRoles;
                            if (previousDefaults.TryGetValue(device.id, out oldRoles) && oldRoles != roles && roles.Length != 0)
                                tracker.Reset(device.id);
                            previousDefaults[device.id] = roles;
                            if (device.error != null)
                            {
                                LogErrorOnce(errors, device.id, device.name + ": " + device.error);
                                continue;
                            }
                            errors.Remove(device.id);
                            live[device.id] = new HashSet<string>(device.sessions.Select(s => s.instanceId), StringComparer.Ordinal);
                            foreach (SessionInfo session in device.sessions)
                            {
                                if (!tracker.NeedsApply(device.id, session.instanceId)) continue;
                                try
                                {
                                    if (api.Apply(device.id, session.instanceId, config.ForDevice(device.id), Log))
                                        tracker.MarkApplied(device.id, session.instanceId);
                                    errors.Remove(device.id + "-apply");
                                }
                                catch (Exception ex) { LogErrorOnce(errors, device.id + "-apply", device.name + ": " + ex.Message); }
                            }
                        }
                        tracker.Retain(live);
                        errors.Remove("audio");
                    }
                    catch (Exception ex)
                    {
                        LogErrorOnce(errors, "audio", "Audio scan failed; will reconnect: " + ex.Message);
                        if (api != null) { api.Dispose(); api = null; }
                    }
                    stop.WaitOne(1000);
                }
            }
            finally { if (api != null) api.Dispose(); Log("Stopped."); }
        }

        private static void LogErrorOnce(Dictionary<string, string> errors, string key, string message)
        {
            string previous;
            if (!errors.TryGetValue(key, out previous) || previous != message) Log(message);
            errors[key] = message;
        }
    }
}
