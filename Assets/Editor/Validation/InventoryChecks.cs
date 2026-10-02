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

    [MenuItem("Tools/Automaturgy/Validation/Check inventory")]
    public static void Run()
    {
        assertions = 0;
        var all = new RunLoadout(ReactionCatalog.ReactionIds, new[] { 100, 200, 300, 400 });
        Require(all.ReactionIds.Count == 22, "All authored reaction IDs are available individually.");
        string legacyText = File.ReadAllText("Assets/Data/Elements/Reactions.txt");
        var legacy = ReactionParser.Parse(legacyText);
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
        var reordered = new RunLoadout(ReactionCatalog.ReactionIds.Reverse().Concat(new[] { 1000, 3004 }));
        Require(reordered.Reactions.SequenceEqual(all.Reactions), "Layout and duplicate grants cannot alter rule ordering.");
        var stone = new RunLoadout(Array.Empty<int>(), new[] { 400 });
        Require(stone.CanPlace(400) && stone.Reactions.Count == 0, "Stone unlocks without dummy rules.");
        Require(!new RunLoadout(new[] { 1000, 1009, 3004 }).CanPlace(100), "Reaction grants do not implicitly grant placement.");
        Require(new RunLoadout(Array.Empty<int>()).FirstElement == 0, "Empty loadout is valid.");
        var selected = new RunLoadout(new[] { 3004, 1009, 1009 }, new[] { 400, 100, 400 });
        Require(selected.ReactionIds.SequenceEqual(new[] { 1009, 3004 }), "Individual mixed-family grants are deduplicated in catalog order.");
        Require(selected.GetReactions(100).Count == 4 && selected.GetReactions(100).All(r => r.Id == 1009) &&
            selected.GetReactions(300).Count == 4 && selected.GetReactions(200).Count == 0,
            "Selecting a reaction includes its rotations and no sibling reactions.");
        Require(selected.PlacementElements.SequenceEqual(new[] { 100, 400 }) && selected.FirstElement == 100,
            "Placement grants are independent, deduplicated, and sorted.");
        Reject(() => new RunLoadout(new[] { 999 }), "Unknown reactions rejected.");
        Reject(() => new RunLoadout(Array.Empty<int>(), new[] { 101 }), "Invalid placement elements rejected.");
        Reject(() => ReactionParser.Parse("FIRE\nR 2000 I (0,0,100) O (0,0,0,0) D (0) E", requireIds: true), "Wrong reaction ID family rejected.");
        Reject(() => ReactionParser.Parse("FIRE\nR 1000 I (0,0,100) O (0,0,0,0) D (0) E\nR 1000 I (0,0,100) O (0,0,0,0) D (0) E", requireIds: true), "Duplicate reaction IDs rejected.");

        var definitions = Resources.LoadAll<InventoryItemDefinition>("InventoryItems");
        Require(definitions.Length == 6, "Six editable starter assets.");
        CheckGrants(definitions, "fire", 100, Enumerable.Range(1000, 9).ToArray());
        CheckGrants(definitions, "fire_interactions", 0, Enumerable.Range(1009, 6).ToArray());
        CheckGrants(definitions, "water", 200, 2000);
        CheckGrants(definitions, "electricity", 300, 3000, 3001, 3002, 3003);
        CheckGrants(definitions, "electricity_interactions", 0, 3004, 3005);
        CheckGrants(definitions, "stone", 400);
        var starter = InventoryState.NewProfile(definitions);
        var starterState = new InventoryState(definitions, starter);
        Require(starterState.Items.Count == 6 && starterState.Recovery.Count == 0, "Starter items fit in storage.");
        Require(starterState.BuildLoadout().ReactionIds.Count == 0 && starterState.BuildLoadout().PlacementElements.Count == 0, "Stored items are inactive.");
        var single = ScriptableObject.CreateInstance<InventoryItemDefinition>();
        var lShape = ScriptableObject.CreateInstance<InventoryItemDefinition>();
        try
        {
            single.itemId = "single"; single.displayName = "Single"; single.placementElement = 200;
            single.reactionIds = new() { 2000, 1009 };
            lShape.itemId = "ell"; lShape.displayName = "L"; lShape.placementElement = 100;
            lShape.reactionIds = new() { 1009, 3004 };
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
            Require(state.BuildLoadout().ReactionIds.SequenceEqual(new[] { 1009, 3004 }), "Active block grants exactly its individual reaction list.");
            Require(state.Move(dot, InventoryGrid.Active, 0, 0) &&
                state.BuildLoadout().ReactionIds.SequenceEqual(new[] { 1009, 2000, 3004 }),
                "Overlapping reaction grants from different blocks activate once.");
            Require(state.Move(dot, InventoryGrid.Storage, 1, 0) &&
                state.BuildLoadout().ReactionIds.SequenceEqual(new[] { 1009, 3004 }) && !state.BuildLoadout().CanPlace(200),
                "Unequipping removes exclusive grants and retains shared reactions.");
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
            single.reactionIds.Add(999);
            Reject(single.Validate, "Items reject unknown reaction IDs."); single.reactionIds.Remove(999);
            single.placementElement = 101;
            Reject(single.Validate, "Items reject invalid placement elements.");
        }
        finally { UnityEngine.Object.DestroyImmediate(single); UnityEngine.Object.DestroyImmediate(lShape); }
        Directory.CreateDirectory("Temp/InventoryChecks");
        string result = "PASS: " + assertions + " inventory/catalog/save assertions.";
        File.WriteAllText("Temp/InventoryChecks/Results.txt", result); Debug.Log(result);
    }
    private static void CheckGrants(InventoryItemDefinition[] definitions, string id, int placement, params int[] reactions)
    {
        var definition = definitions.Single(d => d.itemId == id);
        Require(definition.placementElement == placement && definition.reactionIds.SequenceEqual(reactions),
            id + " retains its authored placement and reaction grants.");
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
