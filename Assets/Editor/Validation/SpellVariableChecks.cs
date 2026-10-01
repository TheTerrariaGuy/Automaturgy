using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Assets.Scripts;
using UnityEditor;
using UnityEngine;

public static class SpellVariableChecks
{
    private static int checks;
    private static void Require(bool condition, string message)
    { checks++; if (!condition) throw new InvalidOperationException(message); }
    private static void Reject(string body, string message)
    {
        bool rejected = false;
        try { Rules(body); } catch (FormatException) { rejected = true; }
        Require(rejected, message);
    }
    private static Reaction[] Rules(string body) => ReactionParser.Parse("FIRE\nR 1000 " + body, requireIds: true)[100].ToArray();
    private static void Resolve(BoardState board, Reaction[] rules) =>
        new ReactionResolver(board, type => type == 100 ? rules : Array.Empty<Reaction>()).Resolve();

    [MenuItem("Tools/Grid Mage/Validation/Check spell variables")]
    public static void Run()
    {
        checks = 0;
        var messages = new List<string>();
        void Log(string message, string stack, LogType type)
        { if (type == LogType.Warning && message.StartsWith("Reaction ")) messages.Add(message); }
        Application.logMessageReceived += Log;
        try
        {
            foreach (int family in new[] { 100, 200, 300, 400 })
            {
                var expanded = SpellSetExpression.Compile((family / 10) + ".", 0, int.MaxValue, ElementDefinitions.Stages.Keys);
                Require(expanded.SequenceEqual(ElementDefinitions.Stages.Keys.Where(t => t / 100 == family / 100 && t % 100 < 10).OrderBy(t => t)),
                    "Digit patterns cover the old reactive-family wildcard for " + family);
                Reject("I (0,0," + family + "*) O (0,0,100) D (0) E", "Old wildcard rejected.");
            }
            Require(!File.ReadAllText("Assets/Data/Elements/Reactions.txt").Contains('*'), "Historical fixture also uses digit wildcards.");

            var rule = Rules("I (1,0,20.+300) O (0,0,a) D (0) E").Single();
            Require(rule.VariableCount == 1 && rule.Requirements.Single().TypeVariable == 0,
                "An entire field gets one variable even when its set contains multiple terms.");
            var board = new BoardState(7, 7);
            board.Set(3, 3, 100); board.Set(3, 4, 203);
            Resolve(board, new[] { rule });
            Require(board.Get(3, 3) == 203, "Variable substitutes the whole matched tile type, not just its wildcard digit.");
            board.Set(3, 3, 100); board.Set(3, 4, 300);
            Resolve(board, new[] { rule });
            Require(board.Get(3, 3) == 300, "Literal alternatives in a wildcard-containing field are captured too.");
            Require(messages.Count == 0, "Single captures do not warn.");

            const string one = ".-0-2-3-4-5-6-7-8-9";
            foreach (int direction in new[] { 0, 1, 2, 3 })
            {
                var rotated = Rules("I (" + one + ",0,20.) O (a,1,b) D (" + direction + ") E").Single();
                var req = rotated.Requirements.Single();
                Require(rotated.VariableCount == 2 && req.XVariable == 0 && req.TypeVariable == 1, "Fields bind in x/y/type order.");
                board = new BoardState(7, 7);
                board.Set(3, 3, 100);
                board.Set(3 + req.y, 3 + req.x, 202);
                Resolve(board, new[] { rotated });
                var output = GridMath.Rotate(1, 1, direction);
                Require(board.Get(3 + output.y, 3 + output.x) == 202, "Captured offsets and output coordinates rotate exactly once.");
            }
            var xy = Rules("I (" + one + "," + one + ",20.) O (a,b,c) D (0) E").Single();
            Require(xy.VariableCount == 3 && xy.Requirements.Single().YVariable == 1 && xy.Requirements.Single().TypeVariable == 2,
                "Y-coordinate captures consume their own variable.");
            // A simple coordinate capture used with unary minus in an output.
            var negated = Rules("I (" + one + ",0,200) O (-a,0,101) D (0) E");
            board = new BoardState(7, 7); board.Set(3, 3, 100); board.Set(3, 4, 200);
            Resolve(board, negated);
            Require(board.Get(3, 2) == 101, "Signed output variable resolves to a concrete offset.");

            var last = Rules("I (2+1,0,20.) O (0,0,a) D (0) E");
            board = new BoardState(7, 7); board.Set(3, 3, 100); board.Set(3, 4, 201); board.Set(3, 5, 202);
            Resolve(board, last);
            Require(board.Get(3, 3) == 202, "Last detected value wins in deterministic expanded-coordinate order.");
            Require(messages.Count == 1 && messages[0].Contains("variable a matched 2") && messages[0].Contains("value 202"),
                "Multiple captures log the variable, detection count, and chosen final value.");
            board.Set(3, 3, 100); board.Set(3, 5, 0);
            Resolve(board, last);
            Require(board.Get(3, 3) == 100 && messages.Count == 1, "Failed detections emit neither writes nor capture warnings.");

            var repeated = Rules("I (1+2,0,20.) O (0,0,a) D (0) E");
            board = new BoardState(7, 7); board.Set(3, 3, 100); board.Set(3, 4, 201); board.Set(3, 5, 201);
            Resolve(board, repeated);
            Require(messages.Count == 2 && messages[1].Contains("matched 2"), "Repeated captures log even when values happen to be equal.");

            var union = Rules("I (1,0,20.) O (0,0,a+300) D (0) E");
            board = new BoardState(7, 7); board.Set(3, 3, 100); board.Set(3, 4, 201);
            Resolve(board, union);
            Require(board.Get(3, 3) == 201, "Output union uses the captured value and each concrete tile's default priority.");
            var difference = Rules("I (1,0,20.) O (0,0,a-201) D (0) E");
            board.Set(3, 3, 100);
            Resolve(board, difference);
            Require(board.Get(3, 3) == 100, "A binding that makes an output set empty emits no writes.");
            board.Set(3, 4, 202);
            Resolve(board, difference);
            Require(board.Get(3, 3) == 202, "Other bindings select their precompiled output rows.");
            var priority = Rules("I (1,0,20.) O (0,0,101,a) (0,0,200,100) D (0) E");
            board.Set(3, 3, 100);
            Resolve(board, priority);
            Require(board.Get(3, 3) == 101, "Captured values may override output priority.");

            var overlap = ReactionParser.Parse("FIRE\nR 1000 I (1,0,200) O (0,0,300) D (0) E\n" +
                "R 1001 I (0,0,30.) O (1,1,a) D (0) E", requireIds: true)[100].ToArray();
            board = new BoardState(7, 7); board.Set(3, 3, 100); board.Set(3, 4, 200);
            Resolve(board, overlap);
            Require(board.Get(4, 4) == 300, "Overlap reactions capture the tile matched in the overlap snapshot.");
            var separate = ReactionParser.Parse("FIRE\nR 1000 I (1,0,20.) (2,0,300) O (0,0,a) D (0) E\n" +
                "R 1001 I (0,1,30.) O (1,1,a) D (0) E", requireIds: true)[100].ToArray();
            board = new BoardState(7, 7); board.Set(3, 3, 100); board.Set(3, 4, 201); board.Set(4, 3, 301);
            Resolve(board, separate);
            Require(board.Get(4, 4) == 301, "Variables restart at a and failed matches cannot leak captures into another reaction.");

            string inputs = string.Join(" ", Enumerable.Repeat("(0,0,10.)", 24));
            string outputs = string.Join(" ", Enumerable.Range(0, 24).Select(i => "(0,1," + (char)('a' + i) + ")"));
            var maximum = Rules("I " + inputs + " O " + outputs + " D (0) E").Single();
            Require(maximum.VariableCount == 24 && maximum.Requirements.Last().TypeVariable == 23,
                "All 24 variables a through x are supported.");
            var captures = Enumerable.Range(0, 24).Select(i => 100 + i % 8).ToArray();
            Require(maximum.OutputMappings.Select((mapping, i) => mapping.Resolve(captures).Single().type == captures[i]).All(v => v),
                "Each tuple bakes only variables it uses, avoiding a 24-variable Cartesian product.");
            foreach (string invalid in new[] {
                "I " + inputs + " (0,0,10.) O (0,0,a) D (0) E",
                "I (0,0,100) O (0,0,a) D (0) E", "I (0,0,10.) O (0,0,b) D (0) E",
                "I (0,0,10.) O (0,0,y) D (0) E", "I (0,0,10.) O (0,0,A) D (0) E",
                "I (0,0,10.) O (0,0,2a) D (0) E", "I (0,0,a) O (0,0,100) D (0) E",
                "I (.,0,100) O (0,0,a) D (0) E",
                "I (0,0,10.) (0,0,10.) (0,0,10.) (0,0,10.) (0,0,10.) O (0,0,a+b+c+d+e) D (0) E" })
                Reject(invalid, "Unbound captures, invalid substitutions, too many variables, or excessive combinations rejected.");
        }
        finally { Application.logMessageReceived -= Log; }
        string result = "PASS: " + checks + " spell variable assertions.";
        Directory.CreateDirectory("Temp/SpellCompiler");
        File.WriteAllText("Temp/SpellCompiler/Variables.txt", result); Debug.Log(result);
    }
}
