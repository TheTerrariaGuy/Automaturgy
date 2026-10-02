using System;
using System.IO;
using System.Linq;
using Assets.Scripts;
using UnityEditor;
using UnityEngine;

public static class SpellCompilerChecks
{
    private static int checks;
    private static void Require(bool condition, string message)
    { checks++; if (!condition) throw new InvalidOperationException(message); }
    private static void Reject(Action action, string message)
    {
        bool rejected = false;
        try { action(); } catch (FormatException) { rejected = true; }
        Require(rejected, message);
    }
    private static int[] Set(string expression) => SpellSetExpression.Compile(expression, -1024, 1024);
    private static Reaction[] Rules(string body) => ReactionParser.Parse("FIRE\nR 1000 " + body, requireIds: true)[100].ToArray();

    [MenuItem("Tools/Automaturgy/Validation/Check spell compiler")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run compiler checks outside Play mode.");
        checks = 0;
        Require(Set("+1 + 2 - 1 + 3").SequenceEqual(new[] { 2, 3 }), "Union and subtraction evaluate left to right.");
        Require(Set("1+1+2-2+2").SequenceEqual(new[] { 1, 2 }), "Sets deduplicate and allow re-adding removed values.");
        Require(Set("-2+-1+0+1--1").SequenceEqual(new[] { -2, 0, 1 }), "Signed literals are distinct from binary subtraction.");
        Require(Set("1.-12-15").SequenceEqual(Enumerable.Range(10, 10).Except(new[] { 12, 15 })), "Dot matches one digit.");
        Require(Set("-.").SequenceEqual(Enumerable.Range(-9, 10)), "Negative wildcard values include zero.");
        Require(SpellSetExpression.Compile("20.-203+300", 0, int.MaxValue, ElementDefinitions.Stages.Keys)
            .SequenceEqual(new[] { 200, 201, 202, 204, 205, 206, 207, 300 }), "Tile wildcard expands only authored tile IDs.");
        Require(SpellSetExpression.Compile(".00", 0, int.MaxValue, ElementDefinitions.Stages.Keys)
            .SequenceEqual(new[] { 100, 200, 300, 400 }), "Wildcard can occupy any digit position.");
        Require(SpellSetExpression.Compile("-2147483648", int.MinValue, int.MaxValue).Single() == int.MinValue,
            "Signed integer minimum is accepted without overflow.");
        Require(SpellSetExpression.Compile("..........", int.MaxValue, int.MaxValue).Single() == int.MaxValue,
            "Wildcards prune branches outside the field domain before expansion.");
        foreach (string bad in new[] { "", "1+", "1 2", "1*", "1-1", "01", "--1", "2147483648", ".........." })
            Reject(() => Set(bad), "Invalid or excessive expression rejected: " + bad);

        var expanded = Rules("I ( 1 + 2, -1 + 0, 20. - 203 ) O (1+2,0,101+102) D (0+1) E # comment");
        Require(expanded.Length == 2 && expanded[0].Requirements.Count == 4 && expanded[0].Outputs.Count == 4,
            "Coordinates expand as a Cartesian product; input types stay alternatives.");
        Require(expanded[0].Requirements.All(r => r.Matches(200) && r.Matches(207) && !r.Matches(203) && !r.Matches(210)),
            "Compiled type membership replaces runtime wildcard checks.");
        var before = expanded[0].Requirements.First();
        var rotated = expanded[1].Requirements.First();
        var position = GridMath.Rotate(before.x, before.y, 1);
        Require(rotated.x == position.x && rotated.y == position.y && rotated.Types.SequenceEqual(before.Types),
            "Rotation preserves accepted types.");
        Require(expanded[0].Outputs.All(o => o.priority == ElementDefinitions.ReactionPriority(o.type)),
            "Omitted output priority is baked from each output tile type.");
        var manual = Rules("I (0,0,100) O (0,0,200,-1+99) D (0) E").Single();
        Require(manual.Outputs.Select(o => o.priority).SequenceEqual(new[] { -1, 99 }), "Explicit priority supports set expressions.");
        Require(Rules("I (0,0,100) O (0,0,200,0) D (.) E").Length == 4, "Direction wildcard expands only 0 through 3.");
        Require(Rules("I (0,0,100) O (0,0,200,0) D (3,1,3) E").Select(r => r.Direction).SequenceEqual(new[] { 3, 1 }),
            "Direction tuple order is stable and duplicates are removed.");

        var board = new BoardState(5, 5);
        board.Set(2, 2, 100); board.Set(2, 3, 200); board.Set(3, 3, 300);
        var conjunction = Rules("I (1,0+1,200+300) O (0,0,101) D (0) E");
        new ReactionResolver(board, type => type == 100 ? conjunction : Array.Empty<Reaction>()).Resolve();
        Require(board.Get(2, 2) == 101, "Either accepted type satisfies each required cell.");
        board.Set(2, 2, 100); board.Set(3, 3, 0);
        new ReactionResolver(board, type => type == 100 ? conjunction : Array.Empty<Reaction>()).Resolve();
        Require(board.Get(2, 2) == 100, "Every expanded input coordinate must match.");
        var competing = Rules("I (0,0,100) O (0,0,101+102) D (0) E");
        new ReactionResolver(board, type => type == 100 ? competing : Array.Empty<Reaction>()).Resolve();
        Require(board.Get(2, 2) == 101, "Competing output types use baked tile priorities.");
        board.Set(2, 2, 100);
        var overridden = Rules("I (0,0,100) O (0,0,101) (0,0,200,99) D (0) E");
        new ReactionResolver(board, type => type == 100 ? overridden : Array.Empty<Reaction>()).Resolve();
        Require(board.Get(2, 2) == 200, "Explicit output priority overrides tile defaults.");

        foreach (string bad in new[] {
            "I (0,0,200*) O (0,0,100) D (0) E", "I (0,0,999) O (0,0,100) D (0) E",
            "I (0,0,100) O (0,0,999) D (0) E", "I (0,0,100) O (0,0,100,) D (0) E",
            "I (0,0,100) O (0,0,100) D (4) E", "I (0,0,100) O (0,0,100) D (0) E trailing",
            "I (0,0,100 O (0,0,100) D (0) E", "I (0,0,100) O (..,..,100) D (0) E",
            "I (1025,0,100) O (0,0,100) D (0) E", "I (0,0,...) O (0,0,100) D (0-0) E" })
            Reject(() => Rules(bad), "Bad tuple, domain, old wildcard, or expansion rejected.");
        string huge = "FIRE\n" + string.Join("\n", Enumerable.Range(0, 100).Select(i =>
            "R " + (1000 + i) + " I (0,0,100) O (..,.,100) D (0,1,2,3) E"));
        Reject(() => ReactionParser.Parse(huge, requireIds: true), "Total catalog expansion is bounded.");
        bool diagnostic = false;
        try { ReactionParser.Parse("FIRE\nR 1000 I (0,0,999) O (0,0,100) D (0) E", sourceName: "Broken.txt"); }
        catch (FormatException error)
        { diagnostic = error.Message.Contains("Broken.txt: line 2: input type"); }
        Require(diagnostic, "Diagnostics identify source, line, and field.");

        var source = Resources.Load<TextAsset>(ReactionCatalog.DefaultResourcePath);
        Require(source != null && !source.text.Contains('*'), "Production spells come from a migrated text asset.");
        ReactionCatalog.EnsureInitialized();
        int compileCount = ReactionCatalog.CompilationCount;
        var rulesBefore = ReactionCatalog.Reactions;
        ReactionCatalog.Initialize(source);
        var loadout = new RunLoadout(new[] { 1000, 1009 });
        Require(ReferenceEquals(rulesBefore, ReactionCatalog.Reactions) && ReactionCatalog.CompilationCount == compileCount,
            "Repeated initialization and loadout selection reuse the compiled catalog.");
        Require(loadout.Reactions.All(r => ReactionCatalog.Reactions.Contains(r)), "Loadouts reuse compiled reaction instances.");
        var reset = typeof(ReactionCatalog).GetMethod("Reset", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var broken = new TextAsset("FIRE\nR 1000 I (0,0,999) O (0,0,100) D (0) E") { name = "Broken" };
        try
        {
            reset.Invoke(null, null);
            Reject(() => ReactionCatalog.Initialize(broken), "Invalid source fails compilation.");
            Require(!ReactionCatalog.IsInitialized && ReactionCatalog.CompilationCount == 0, "Failed compilation publishes no partial cache.");
            ReactionCatalog.Initialize(source);
            Require(ReactionCatalog.IsInitialized && ReactionCatalog.CompilationCount == 1, "A fresh session recompiles successfully.");
        }
        finally { UnityEngine.Object.DestroyImmediate(broken); ReactionCatalog.EnsureInitialized(); }
        Require(EditorBuildSettings.scenes.First(s => s.enabled).path == "Assets/Scenes/Bootstrap.unity", "Bootstrap is the entry scene.");
        string result = "PASS: " + checks + " spell compiler assertions.";
        Directory.CreateDirectory("Temp/SpellCompiler");
        File.WriteAllText("Temp/SpellCompiler/Results.txt", result); Debug.Log(result);
    }
}
