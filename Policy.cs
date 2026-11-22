using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace SystemSoundsKeeper
{
    public sealed class DeviceRule
    {
        public string id { get; set; }
        public string name { get; set; }
        public double? volumePercent { get; set; }
    }

    public sealed class Configuration
    {
        public double? defaultVolumePercent { get; set; }
        public List<DeviceRule> devices { get; set; }

        public static Configuration Parse(string json)
        {
            Configuration result = new JavaScriptSerializer().Deserialize<Configuration>(json);
            if (result == null) throw new InvalidDataException("Configuration must be a JSON object.");
            ValidateVolume(result.defaultVolumePercent, "defaultVolumePercent");
            if (result.devices == null) throw new InvalidDataException("devices must be an array (use [] for no overrides).");
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (DeviceRule rule in result.devices)
            {
                if (rule == null || String.IsNullOrWhiteSpace(rule.id))
                    throw new InvalidDataException("Every device rule requires an id.");
                if (!ids.Add(rule.id)) throw new InvalidDataException("Duplicate device id: " + rule.id);
                ValidateVolume(rule.volumePercent, "volumePercent for " + rule.id);
            }
            return result;
        }

        private static void ValidateVolume(double? value, string field)
        {
            if (!value.HasValue || Double.IsNaN(value.Value) || Double.IsInfinity(value.Value) || value < 0 || value > 100)
                throw new InvalidDataException(field + " must be a number from 0 to 100.");
        }

        public double ForDevice(string id)
        {
            foreach (DeviceRule rule in devices)
                if (String.Equals(rule.id, id, StringComparison.OrdinalIgnoreCase)) return rule.volumePercent.Value;
            return defaultVolumePercent.Value;
        }
    }

    // A successful application is remembered only for the lifetime of an audio session.
    // User volume changes never become configuration and never trigger a reset.
    public sealed class SessionTracker
    {
        private readonly Dictionary<string, HashSet<string>> handled =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        public void Reset(string deviceId) { handled.Remove(deviceId); }
        public void ResetAll() { handled.Clear(); }

        public bool NeedsApply(string deviceId, string instanceId)
        {
            HashSet<string> ids;
            return !handled.TryGetValue(deviceId, out ids) || !ids.Contains(instanceId);
        }

        public void MarkApplied(string deviceId, string instanceId)
        {
            HashSet<string> ids;
            if (!handled.TryGetValue(deviceId, out ids))
                handled[deviceId] = ids = new HashSet<string>(StringComparer.Ordinal);
            ids.Add(instanceId);
        }

        public void Retain(Dictionary<string, HashSet<string>> liveSessions)
        {
            foreach (string deviceId in new List<string>(handled.Keys))
            {
                HashSet<string> current;
                if (!liveSessions.TryGetValue(deviceId, out current)) handled.Remove(deviceId);
                else handled[deviceId].IntersectWith(current);
            }
        }
    }
}
