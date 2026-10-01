using System;
using System.IO;
using UnityEngine;

namespace GridMage.Workshop
{
    public static class WorkshopDraftStore
    {
        [Serializable] public sealed class Draft
        {
            public int version = 1;
            public string wip, context;
            public string[] brushes;
            public int selectedBrush;
        }
        private static string PathName => Path.Combine(Application.persistentDataPath, "workshop-draft-v1.json");
        public static Draft Load()
        {
            if (!File.Exists(PathName)) return null;
            var draft = JsonUtility.FromJson<Draft>(File.ReadAllText(PathName));
            if (draft == null || draft.version != 1 || draft.wip == null || draft.context == null || draft.brushes == null || draft.brushes.Length != 9)
                throw new FormatException("Workshop draft is invalid; it has been preserved. Use Save draft to replace it explicitly.");
            return draft;
        }
        public static void Save(Draft draft)
        {
            string path = PathName;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path + ".tmp", JsonUtility.ToJson(draft, true));
            if (File.Exists(path)) File.Replace(path + ".tmp", path, path + ".bak");
            else File.Move(path + ".tmp", path);
        }
    }
}
