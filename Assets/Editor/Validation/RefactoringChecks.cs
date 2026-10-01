using System;
using System.IO;
using System.Linq;
using Assets.Scripts;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using static CheckSupport;

public static class RefactoringChecks
{
    [MenuItem("Tools/Grid Mage/Validation/Check refactoring regressions")]
    public static void Run()
    {
        var cases = JArray.Parse(File.ReadAllText("Tests/Refactoring/CombatBaseline.json"));
        var loadout = new RunLoadout(ReactionCatalog.ReactionIds, new[] { 100, 200, 300, 400 });
        var rules = new[] { 100, 200, 300, 400 }.ToDictionary(type => type, type => loadout.GetReactions(type));
        int count = 0;
        foreach (var test in cases)
        {
            var input = test["input"].ToObject<int[,]>();
            var board = new BoardState(12, 12, test["exists"].ToObject<bool[,]>(), test["elevations"].ToObject<float[,]>());
            board.Replace(input);
            var resolver = new ReactionResolver(board, type => rules.TryGetValue(type, out var list) ? list : Array.Empty<Reaction>());
            var output = resolver.Resolve(phase =>
            {
                string name = phase == ResolutionPhase.Fading ? "afterFade" : phase == ResolutionPhase.Reactions ? "afterSpells" : "final";
                Require(JToken.DeepEquals(JToken.FromObject(board.Cells), test[name]), "Combat case " + count + ", phase " + name);
            });
            var visuals = output.Where(v => v.cells.Count > 0).Select(v => new
            {
                v.id, v.row, v.col, v.direction,
                cells = v.cells.Select(p => new[] { p.x, p.y }).OrderBy(p => p[1]).ThenBy(p => p[0]).ToArray()
            }).ToArray();
            Require(JToken.DeepEquals(JToken.FromObject(visuals), test["visuals"]), "Visual ownership case " + count);
            count++;
        }
        foreach (string invalid in new[] { "", "FIRE\nI (0,0,100) O (0,0,999,0) D (0) E",
            "FIRE\nI (0,0,101*) O (0,0,0,0) D (0) E", "FIRE\nI (0,0,100) O (0,0,0,0) D (4) E",
            "FIRE\nI (0,0,100) O (0,0,0,0)", "I (0,0,100) O (0,0,0,0) D (0) E" })
        {
            bool rejected = false;
            try { ReactionParser.Parse(invalid); } catch (FormatException) { rejected = true; }
            Require(rejected, "Invalid rule must be rejected: " + invalid);
        }
        var queue = new SpellQueue();
        queue.Add(0, 0, 100, 8); queue.Add(0, 1, 200, 6);
        Require(queue.ReservedMana == 14 && queue.HasSpells, "Reservations sum costs.");
        int removed = 0;
        queue.RemoveInvalid((r, c) => c != 0, (r, c) => removed++);
        Require(removed == 1 && queue.ReservedMana == 6 && !queue.Contains(0, 0), "Invalidation releases exactly one reservation.");
        queue.Remove(0, 1);
        Require(!queue.HasSpells && queue.ReservedMana == 0, "Last removal clears rounding residue.");
        Require(!queue.Remove(0, 1), "Removing twice does not refund again.");
        CheckRays();
        var catalog = AssetDatabase.LoadAssetAtPath<SpriteCatalog>("Assets/Rendering/ElementSprites.asset");
        catalog.Validate();
        Require(catalog.Entries.Count == 229, "Sprite migration should retain 229 distinct overrides.");
        var particles = AssetDatabase.LoadAssetAtPath<ParticleCatalog>("Assets/Rendering/Particles/ParticleCatalog.asset");
        Require(particles != null, "Particle catalog is required.");
        Require(particles.reactions.Select(r => r.id).Distinct().Count() == particles.reactions.Length, "Reaction IDs must be unique.");
        Require(particles.tiles.Select(t => t.type).Distinct().Count() == particles.tiles.Length, "Particle stage IDs must be unique.");
        var effects = particles.reactions.ToDictionary(r => r.id, r => r.prefab);
        foreach (var id in rules.Values.SelectMany(r => r).Select(r => r.Effect)
            .Concat(ElementDefinitions.Stages.Values.Select(s => s.DecayEffect)).Where(id => id != null).Distinct())
            Require(effects.TryGetValue(id, out var prefab) && prefab != null, "Missing effect for active rule: " + id);
        TilemapLevelChecks.Run();
        Directory.CreateDirectory("Temp/RefactoringChecks");
        string result = "PASS: " + count + " captured combat cases, 480 phase snapshots, visual ownership, malformed rules, reservations, grid rays, sprite catalog, tilemap parsing.";
        File.WriteAllText("Temp/RefactoringChecks/Results.txt", result);
        Debug.Log(result);
    }

    private static void CheckRays()
    {
        var walls = new int[7, 7];
        var heights = new float[7, 7];
        for (int r = 0; r < 7; r++) for (int c = 0; c < 7; c++) heights[r, c] = Math.Max(r, c) * .5f;
        walls[2, 3] = walls[3, 2] = 1;
        for (int r = 0; r < 7; r++) for (int c = 0; c < 7; c++)
        for (int tr = 0; tr < 7; tr++) for (int tc = 0; tc < 7; tc++)
        {
            if (walls[r, c] == 0 && walls[tr, tc] == 0)
                Require(GridMath.HasLineOfSight(walls, r, c, tr, tc) == GridMath.HasLineOfSight(walls, tr, tc, r, c), "Sight symmetry.");
            Require(GridMath.TestElevationLine(heights, r, c, tr, tc) ==
                GridMath.TestElevationLine(heights, tr, tc, r, c), "Elevation symmetry.");
        }
        Require(!GridMath.HasLineOfSight(walls, 2, 2, 3, 3), "Two corner walls block.");
        walls[2, 3] = 0;
        Require(GridMath.HasLineOfSight(walls, 2, 2, 3, 3), "One corner wall allows passage.");
        walls[3, 3] = 1;
        Require(GridMath.HasLineOfSight(walls, 2, 2, 3, 3, true) &&
            !GridMath.HasLineOfSight(walls, 2, 2, 3, 3), "Target-wall exception remains explicit.");
        Require(GridMath.TestElevationLine(new float[,] { { 1, 1.5f, 2 } }, 0, 0, 0, 2), "Half-step ramp.");
        Require(!GridMath.TestElevationLine(new float[,] { { 1, 2, 1 } }, 0, 0, 0, 2), "Equal endpoints cannot bypass a cliff.");
    }
}
