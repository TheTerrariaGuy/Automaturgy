using System;
using System.IO;
using System.Linq;
using Assets.Scripts;
using Assets.Scripts.Inventory;
using UnityEditor;
using UnityEngine;

public static class InventoryChecks
{
    private static int assertions;
    private static void Require(bool condition, string message)
    { assertions++; if (!condition) throw new InvalidOperationException(message); }
    private static void Reject(Action action, string message)
    {
        bool rejected = false;
        try { action(); } catch (Exception e) when (e is FormatException || e is InvalidOperationException || e is ArgumentException || e is IOException) { rejected = true; }
        Require(rejected, message);
    }

    [MenuItem("Tools/Grid Mage/Validation/Check inventory")]
    public static void Run()
    {
        assertions = 0;
        var all = new RunLoadout(ReactionCatalog.Packages.Select(p => p.Id));
        Require(all.PackageIds.SequenceEqual(new[] { 100, 101, 200, 300, 301, 400 }), "Six packages in authored order.");
        var legacy = ReactionParser.Parse(File.ReadAllText("Assets/Data/Elements/Reactions.txt"));
        foreach (var family in legacy)
        {
            var actual = all.GetReactions(family.Key);
            Require(actual.Count == family.Value.Count, "Migration preserves expanded rule count.");
            for (int i = 0; i < actual.Count; i++)
            {
                var old = family.Value[i]; var rule = actual[i];
                Require(rule.Requirements.SequenceEqual(old.Requirements) && rule.Outputs.SequenceEqual(old.Outputs) &&
                    rule.Direction == old.Direction && rule.Effect == old.Effect, "Migration preserves exact rule order, geometry, effects, and priorities.");
                Require(rule.Id / 1000 == family.Key / 100, "Reaction ID family.");
            }
        }
        var reordered = new RunLoadout(new[] { 301, 100, 400, 200, 101, 300, 100 });
        Require(reordered.Reactions.SequenceEqual(all.Reactions), "Layout and duplicate grants cannot alter rule ordering.");
        Require(new RunLoadout(new[] { 400 }).CanPlace(400) && new RunLoadout(new[] { 400 }).Reactions.Count == 0, "Stone unlocks without dummy rules.");
        Require(!new RunLoadout(new[] { 101, 301 }).CanPlace(100), "Cross reactions do not grant placement.");
        Require(new RunLoadout(Array.Empty<int>()).FirstElement == 0, "Empty loadout is valid.");
        Require(new RunLoadout(new[] { 100 }).GetReactions(100).Any(r => r.Effect == "Fire_Burnout"), "Fire decay belongs to fundamental block.");
        Reject(() => new RunLoadout(new[] { 999 }), "Unknown packages rejected.");
        Reject(() => ReactionCatalog.ParsePackages("PACKAGE 100 WATER"), "Package family mismatch rejected.");
        Reject(() => ReactionCatalog.ParsePackages("PACKAGE 400 STONE\nPACKAGE 400 STONE"), "Duplicate package IDs rejected.");
        Reject(() => ReactionCatalog.ParsePackages("PACKAGE 100 FIRE\nR 2000 I (0,0,100) O (0,0,0,0) D (0) E"), "Wrong reaction ID family rejected.");
        Reject(() => ReactionCatalog.ParsePackages("PACKAGE 100 FIRE\nR 1000 I (0,0,100) O (0,0,0,0) D (0) E\nPACKAGE 101 FIRE\nR 1000 I (0,0,100) O (0,0,0,0) D (0) E"), "Duplicate IDs across packages rejected.");

        var definitions = Resources.LoadAll<InventoryItemDefinition>("InventoryItems");
        Require(definitions.Length == 6, "Six editable starter assets.");
        var starter = InventoryState.NewProfile(definitions);
        var starterState = new InventoryState(definitions, starter);
        Require(starterState.Items.Count == 6 && starterState.Recovery.Count == 0, "Starter items fit in storage.");
        Require(starterState.BuildLoadout().PackageIds.Count == 0, "Stored items are inactive.");
        var single = ScriptableObject.CreateInstance<InventoryItemDefinition>();
        var lShape = ScriptableObject.CreateInstance<InventoryItemDefinition>();
        try
        {
            single.itemId = "single"; single.displayName = "Single"; single.packageId = 200;
            lShape.itemId = "ell"; lShape.displayName = "L"; lShape.packageId = 100;
            lShape.shape = new() { new(0,0), new(0,1), new(1,1) };
            var catalog = new[] { single, lShape };
            var data = new InventorySaveData { items = new()
            {
                new() { instanceId = "a", itemId = "ell", x = 0, y = 0 },
                new() { instanceId = "b", itemId = "single", x = 1, y = 0 }
            }};
            var state = new InventoryState(catalog, data);
            Require(state.Items.Count == 2 && state.Recovery.Count == 0, "A cell can interlock in an L-shaped hole.");
            var ell = state.Items[0]; var dot = state.Items[1];
            Require(ReferenceEquals(state.At(InventoryGrid.Storage, 1, 1), ell), "Every shape cell selects one instance.");
            Require(!state.CanPlace(ell, InventoryGrid.Active, 4, 4), "Bounds check entire shape.");
            Require(!state.CanPlace(ell, InventoryGrid.Active, -1, 0), "Negative origins rejected.");
            Require(!state.CanPlace(ell, (InventoryGrid)9, 0, 0), "Unknown grid rejected.");
            string before = JsonUtility.ToJson(state.Snapshot());
            Require(!state.Move(dot, InventoryGrid.Storage, 0, 1), "Overlapping drop rejected.");
            Require(before == JsonUtility.ToJson(state.Snapshot()), "Failed moves leave both grids untouched.");
            int changes = 0; state.Changed += () => changes++;
            Require(state.Move(ell, InventoryGrid.Active, 3, 3) && changes == 1, "Transfer commits once.");
            Require(state.At(InventoryGrid.Storage, 0, 0) == null && ReferenceEquals(state.At(InventoryGrid.Active, 4, 4), ell), "Transfer clears old cells and fills new cells.");
            Require(state.BuildLoadout().CanPlace(100) && !state.BuildLoadout().CanPlace(200), "Only active fundamental grants placement.");
            Require(state.Move(ell, InventoryGrid.Active, 3, 2), "Moving over own cells is valid.");
            var reloaded = new InventoryState(catalog, state.Snapshot());
            Require(reloaded.At(InventoryGrid.Active, 4, 3)?.instanceId == "a", "Load rebuilds shaped occupancy.");
            var invalid = state.Snapshot();
            invalid.items.Add(new ItemPlacement { instanceId = "lost", itemId = "missing" });
            invalid.items.Add(new ItemPlacement { instanceId = "blocked", itemId = "single", x = 1, y = 0 });
            var recovery = new InventoryState(catalog, invalid);
            Require(recovery.Items.Count == 2 && recovery.Recovery.Count == 2, "Unknown and overlapping items retained for recovery.");
            Require(recovery.RecoverAvailable() == 1 && recovery.Recovery.Count == 1, "Known overflow can be reclaimed; unknown entries preserved.");
            Require(new InventoryState(catalog, recovery.Snapshot()).Recovery.Count == 1, "Recovery survives serialization.");
            lShape.shape.Add(new Vector2Int(0, 0));
            Reject(lShape.Validate, "Duplicate shape cells rejected."); lShape.shape.RemoveAt(3);
            CheckFiles(state, catalog);
        }
        finally { UnityEngine.Object.DestroyImmediate(single); UnityEngine.Object.DestroyImmediate(lShape); }
        Directory.CreateDirectory("Temp/InventoryChecks");
        string result = "PASS: " + assertions + " inventory/catalog/save assertions.";
        File.WriteAllText("Temp/InventoryChecks/Results.txt", result); Debug.Log(result);
    }
    private static void CheckFiles(InventoryState state, InventoryItemDefinition[] definitions)
    {
        string directory = "Temp/InventoryChecks/" + Guid.NewGuid().ToString("N"); Directory.CreateDirectory(directory);
        var store = new InventorySaveStore(Path.Combine(directory, "inventory.json"));
        Action<InventorySaveData> validate = save => { _ = new InventoryState(definitions, save); };
        store.Save(state.Snapshot());
        var loaded = store.Load(() => throw new Exception("Must load existing save."), validate, out _);
        Require(JsonUtility.ToJson(loaded) == JsonUtility.ToJson(state.Snapshot()), "JSON roundtrip retains identity and positions.");
        state.Move(state.Items[0], InventoryGrid.Storage, 5, 4);
        store.Save(state.Snapshot());
        Require(File.Exists(store.Path + ".bak") && !File.Exists(store.Path + ".tmp"), "Replacement retains backup, consumes temporary file.");
        File.WriteAllText(store.Path, "broken");
        var backup = store.Load(() => throw new Exception("Must recover."), validate, out string notice);
        Require(notice != null && JsonUtility.ToJson(backup) == JsonUtility.ToJson(loaded), "Corrupt primary recovers last complete save.");
        Require(Directory.GetFiles(directory, "*.corrupt-*").Length == 1, "Corrupt primary preserved for diagnosis.");
        File.WriteAllText(store.Path, "broken"); File.WriteAllText(store.Path + ".bak", "broken");
        Reject(() => store.Load(() => new InventorySaveData(), validate, out _), "Two corrupt saves cannot silently reset progress.");
    }
}
