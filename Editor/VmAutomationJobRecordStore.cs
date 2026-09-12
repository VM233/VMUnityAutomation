using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace VMUnityAutomation.Editor
{
    /// <summary>Atomic per-job persistence. The calling state owner serializes access.</summary>
    internal sealed class VmAutomationJobRecordStore
    {
        private readonly string aggregatePath;
        private readonly string directory;
        private readonly string indexPath;
        private List<string> publishedKeys = new();
        private readonly Dictionary<(string type, string id), string> identityKeys = new();

        internal VmAutomationJobRecordStore(string aggregatePath)
        {
            this.aggregatePath = Path.GetFullPath(aggregatePath);
            directory = Path.ChangeExtension(this.aggregatePath, "records");
            indexPath = Path.Combine(directory, "index.json");
        }

        internal List<Dictionary<string, object>> Load()
        {
            if (VmAutomationPersistenceFile.TryReadAllText(indexPath, out string indexJson))
            {
                if (!(MiniJson.Deserialize(indexJson) is IList index))
                    throw new InvalidDataException($"Job record index '{indexPath}' must be an array.");
                var keys = new List<string>(index.Count);
                var records = new List<Dictionary<string, object>>(index.Count);
                var unique = new HashSet<string>(StringComparer.Ordinal);
                using (var hash = SHA256.Create())
                {
                    foreach (object value in index)
                    {
                        if (!(value is string key) || key.Length != 64 ||
                            key.Any(c => !(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')) || !unique.Add(key))
                            throw new InvalidDataException($"Job record index '{indexPath}' contains an invalid identity.");
                        Dictionary<string, object> record = ParseRecord(
                            VmAutomationPersistenceFile.ReadAllText(RecordPath(key)));
                        if (Key(record, hash) != key)
                            throw new InvalidDataException($"Job record '{key}' has changed identity.");
                        keys.Add(key);
                        records.Add(record);
                    }
                }
                publishedKeys = keys;
                VmAutomationPersistenceFile.DeleteIfExists(aggregatePath);
                foreach (string file in Directory.EnumerateFiles(directory, "*.json"))
                {
                    if (file == indexPath) continue;
                    string key = Path.GetFileNameWithoutExtension(file);
                    if (!unique.Contains(key)) VmAutomationPersistenceFile.DeleteIfExists(file);
                }
                return records;
            }

            if (!VmAutomationPersistenceFile.TryReadAllText(aggregatePath, out string aggregateJson))
                return new List<Dictionary<string, object>>();
            if (!(MiniJson.Deserialize(aggregateJson) is IList legacy))
                throw new InvalidDataException($"Job aggregate '{aggregatePath}' must be an array.");
            var migrated = new List<Dictionary<string, object>>(legacy.Count);
            foreach (object value in legacy)
            {
                Dictionary<string, object> record = VmAutomationResponse.ToDictionary(value);
                if (record == null) throw new InvalidDataException($"Job aggregate '{aggregatePath}' contains a non-object record.");
                migrated.Add(record);
            }
            PublishAll(migrated);
            VmAutomationPersistenceFile.DeleteIfExists(aggregatePath);
            return migrated;
        }

        internal void PublishChanged(IReadOnlyList<Dictionary<string, object>> records,
            Dictionary<string, object> changed) => Publish(records, new[] { changed });

        internal void PublishAll(IReadOnlyList<Dictionary<string, object>> records) =>
            Publish(records, records);

        private void Publish(IReadOnlyList<Dictionary<string, object>> records,
            IEnumerable<Dictionary<string, object>> changed)
        {
            var keys = new List<string>(records.Count);
            var unique = new HashSet<string>(StringComparer.Ordinal);
            using (var hash = SHA256.Create())
            {
                foreach (Dictionary<string, object> record in records)
                {
                    string key = Key(record, hash);
                    if (!unique.Add(key)) throw new InvalidDataException($"Duplicate job record '{key}'.");
                    keys.Add(key);
                }
                foreach (Dictionary<string, object> record in changed)
                {
                    string key = Key(record, hash);
                    if (!unique.Contains(key))
                        throw new InvalidDataException($"Changed job record '{key}' is absent from its membership index.");
                    VmAutomationPersistenceFile.WriteAllText(RecordPath(key), MiniJson.Serialize(record));
                }
            }
            bool membershipChanged = !publishedKeys.SequenceEqual(keys) || !File.Exists(indexPath);
            if (membershipChanged)
                VmAutomationPersistenceFile.WriteAllText(indexPath, MiniJson.Serialize(keys));
            foreach (string key in publishedKeys)
                if (!unique.Contains(key)) VmAutomationPersistenceFile.DeleteIfExists(RecordPath(key));
            publishedKeys = keys;
            if (membershipChanged)
                foreach (var identity in identityKeys.Where(pair => !unique.Contains(pair.Value)).Select(pair => pair.Key).ToArray())
                    identityKeys.Remove(identity);
        }

        internal string RecordPath(Dictionary<string, object> record)
        {
            using (var hash = SHA256.Create()) return RecordPath(Key(record, hash));
        }

        private string RecordPath(string key) => Path.Combine(directory, key + ".json");

        private string Key(Dictionary<string, object> record, HashAlgorithm hash)
        {
            if (!record.TryGetValue("jobType", out object type) || !(type is string jobType) ||
                !record.TryGetValue("jobId", out object id) || !(id is string jobId) ||
                string.IsNullOrEmpty(jobType) || string.IsNullOrEmpty(jobId))
                throw new InvalidDataException("A persisted job requires its type and ID.");
            var identity = (jobType, jobId);
            if (identityKeys.TryGetValue(identity, out string existing)) return existing;
            byte[] digest = hash.ComputeHash(Encoding.UTF8.GetBytes(
                jobType.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":" + jobType + jobId));
            const string hexadecimal = "0123456789abcdef";
            var key = new char[64];
            for (int index = 0; index < digest.Length; index++)
            {
                key[index * 2] = hexadecimal[digest[index] >> 4];
                key[index * 2 + 1] = hexadecimal[digest[index] & 15];
            }
            string result = new(key);
            identityKeys.Add(identity, result);
            return result;
        }

        private static Dictionary<string, object> ParseRecord(string json) =>
            VmAutomationResponse.ToDictionary(MiniJson.Deserialize(json)) ??
            throw new InvalidDataException("A persisted job record must be an object.");
    }
}
