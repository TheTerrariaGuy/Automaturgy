using System;
using System.IO;
using UnityEngine;

namespace Assets.Scripts.Inventory
{
    public static class InventorySession
    {
        public const string InventoryScene = "Inventory", GameplayScene = "In Game";
        public static InventoryState State { get; private set; }
        public static RunLoadout CurrentRun { get; private set; }
        public static string Notice { get; private set; }
        private static InventorySaveStore store;
        public static event Action SaveStatusChanged;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        { State = null; CurrentRun = null; store = null; Notice = null; SaveStatusChanged = null; }

        public static InventoryState Load()
        {
            if (State != null) return State;
            var definitions = Resources.LoadAll<InventoryItemDefinition>("InventoryItems");
            if (definitions.Length == 0) throw new InvalidOperationException("No inventory item definitions found.");
            store = new InventorySaveStore(Path.Combine(Application.persistentDataPath, "inventory-v1.json"));
            var data = store.Load(() => InventoryState.NewProfile(definitions),
                save => { _ = new InventoryState(definitions, save); }, out string notice);
            State = new InventoryState(definitions, data);
            Notice = notice;
            State.Changed += AutoSave;
            return State;
        }

        private static void AutoSave() { Save(); }
        public static bool Save()
        {
            try
            {
                store.Save(State.Snapshot());
                Notice = null; SaveStatusChanged?.Invoke(); return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Notice = "Inventory could not be saved. Retry before leaving. " + e.Message;
                Debug.LogWarning(Notice); SaveStatusChanged?.Invoke(); return false;
            }
        }

        public static RunLoadout PrepareRun()
        {
            var loadout = Load().BuildLoadout();
            if (!Save()) throw new IOException(Notice);
            CurrentRun = loadout;
            return loadout;
        }

        // Directly opening the gameplay scene uses the saved loadout too, never all spells implicitly.
        public static RunLoadout GetRun() => CurrentRun ??= Load().BuildLoadout();
        public static void SetRun(RunLoadout loadout) => CurrentRun = loadout ?? throw new ArgumentNullException(nameof(loadout));
    }
}
