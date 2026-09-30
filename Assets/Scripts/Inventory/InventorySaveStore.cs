using System;
using System.IO;
using UnityEngine;

namespace Assets.Scripts.Inventory
{
    /// <summary>One file commits both grids. Failed/corrupt reads never silently create a new profile.</summary>
    public sealed class InventorySaveStore
    {
        public string Path { get; }
        public InventorySaveStore(string path) { Path = path; }
        public InventorySaveData Load(Func<InventorySaveData> create, Action<InventorySaveData> validate, out string notice)
        {
            notice = null;
            if (!File.Exists(Path) && !File.Exists(Path + ".bak")) return create();
            Exception primaryError = null;
            foreach (string candidate in new[] { Path, Path + ".bak" })
            {
                if (!File.Exists(candidate)) continue;
                try
                {
                    string json = File.ReadAllText(candidate);
                    if (!json.TrimStart().StartsWith("{") || !json.Contains("\"version\"") || !json.Contains("\"items\""))
                        throw new InvalidDataException("Missing inventory save fields.");
                    var data = JsonUtility.FromJson<InventorySaveData>(json);
                    if (data == null || data.version != 1) throw new InvalidDataException("Unsupported inventory save version.");
                    validate(data);
                    if (candidate != Path)
                    {
                        // Preserve the unreadable primary before restoring the known-good backup.
                        if (File.Exists(Path)) File.Copy(Path, Path + ".corrupt-" + DateTime.UtcNow.Ticks);
                        File.Copy(candidate, Path, true);
                        notice = "Inventory restored from backup.";
                    }
                    return data;
                }
                catch (Exception e) when (e is IOException || e is InvalidDataException || e is ArgumentException || e is InvalidOperationException)
                { primaryError ??= e; }
            }
            throw new IOException("Unable to load inventory. Existing save files were preserved.", primaryError);
        }

        public void Save(InventorySaveData data)
        {
            string directory = System.IO.Path.GetDirectoryName(Path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            string temporary = Path + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false), 1024, leaveOpen: true))
                { writer.Write(JsonUtility.ToJson(data, true)); writer.Flush(); }
                stream.Flush(true);
            }
            if (File.Exists(Path)) File.Replace(temporary, Path, Path + ".bak");
            else File.Move(temporary, Path);
        }
    }
}
