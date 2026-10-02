using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts;
using UnityEditor;
using UnityEngine;

namespace GridMage.Workshop.Editor
{
    public static class WorkshopChecks
    {
        private static int checks;
        private static void Require(bool condition, string message)
        { checks++; if (!condition) throw new InvalidOperationException("Workshop: " + message); }
        private static void Reject(Action action, string message)
        {
            bool rejected = false;
            try { action(); } catch (FormatException) { rejected = true; }
            Require(rejected, message);
        }
        [MenuItem("Tools/Automaturgy/Workshop/Run checks")]
        public static void RunMenu() => Debug.Log(Run());
        public static string Run()
        {
            checks = 0;
            Require(WorkshopPalette.Colors.Count == 84, "All 84 colors exist.");
            Require(WorkshopPalette.Colors[100].Equals(new Color32(120, 12, 10, 255)), "Fire RGB exact.");
            Require(WorkshopPalette.Colors[711].Equals(new Color32(125, 52, 42, 255)), "Darkness RGB exact.");
            var captures = ReactionDocument.Parse("FIRE\nI (1,0,...-4..) (2,0,50.) O (0,1,a) D (0) E");
            var captureRule = WorkshopReactionCompiler.Parse(captures.Source)[100].Single();
            Require(captures.Inputs[0].GridLabel == "a" && captures.Inputs[1].GridLabel == "b" &&
                captureRule.Requirements.First().TypeVariable == 0 && captureRule.Requirements.Last().TypeVariable == 1,
                "Compound wildcard labels correspond to actual compiler capture indices.");
            Require(captures.Inputs[0].HoverDetail.Contains("a = ...-4..") && captures.Outputs[0].GridLabel == "a",
                "Hover preserves wildcard formula; output references retain the same name.");
            var paintedCapture = captures.Paint(false, 3, 0, "a");
            var copied = new WorkshopSimulation(); copied.Apply("", paintedCapture);
            copied.Board.Set(12, 12, 100); copied.Board.Set(12, 13, 608); copied.Board.Set(12, 14, 500); copied.TogglePause(); copied.Step();
            Require(copied.Board.Get(12, 15) == 608, "Painting an output variable copies the actual matched input tile.");
            Require(captures.Paint(false, 3, 0, "a+500").At(false, 3, 0).Single().Fields[2] == "a+500",
                "Output brushes accept expressions using bound variables.");
            Reject(() => captures.Paint(false, 3, 0, "x"), "Output brushes reject unbound variables.");
            Reject(() => captures.Paint(true, 3, 0, "a"), "Input brushes cannot reference output capture variables.");
            var coordinateCaptures = ReactionDocument.Parse("FIRE\nI (.-0-2-3-4-5-6-7-8-9,0,20.) (2,0,50.) O (0,1,100) D (0) E");
            var coordinateRule = WorkshopReactionCompiler.Parse(coordinateCaptures.Source)[100].Single();
            Require(coordinateCaptures.Inputs[0].GridLabel == "b\nx:a" && coordinateCaptures.Inputs[1].GridLabel == "c" &&
                coordinateRule.Requirements.First().XVariable == 0 && coordinateRule.Requirements.First().TypeVariable == 1 && coordinateRule.Requirements.Last().TypeVariable == 2,
                "Coordinate captures consume names before state captures, matching compilation.");
            var relabeled = ReactionDocument.Parse("FIRE\nI (1,0,20.) (2,0,50.) O (0,1,100) D (0) E").Paint(true, 1, 0, "", true);
            Require(relabeled.Inputs.Single().GridLabel == "a", "Names update after deleting an earlier capture.");
            const string windPattern = "I (0,0,500) (1,0,500) O (2,0,508) (1,1,501) (1,-1,501) D (0,1,2,3) E";
            Reject(() => WorkshopReactionCompiler.Parse("FIRE\n" + windPattern), "A wrong source family cannot silently activate an impossible multi-input rule.");
            var windDocument = ReactionDocument.Parse("# Preserve this\nFIRE\n" + windPattern).WithFamily("WIND");
            Require(windDocument.Source == "# Preserve this\nWIND\n" + windPattern, "Source selection preserves the entire reaction body and comments.");
            var windSimulation = new WorkshopSimulation();
            windSimulation.Apply("", windDocument); windSimulation.QueueTile(12, 12, 500); windSimulation.QueueTile(12, 13, 500);
            windSimulation.Deploy(); windSimulation.TogglePause(); windSimulation.Step();
            Require(windSimulation.Board.Get(12, 14) == 508 && windSimulation.Board.Get(13, 13) == 501 && windSimulation.Board.Get(11, 13) == 501,
                "User's two-tile Wind pattern produces all three outputs.");
            Require(WorkshopReactionCompiler.Parse("FIRE\nI (0,0,50.) O (0,1,110) D (0) E")[100].Count == 1,
                "An intentional single-origin overlap rule still accepts another family.");
            Require(WorkshopReactionCompiler.Parse("FIRE\nI (0,0,100+500) (1,0,500) O (2,0,508) D (0) E")[100].Count == 1,
                "Origin alternatives that include the source family remain valid.");
            Reject(() => ReactionDocument.Parse("FIRE\nR 1000 I (1,0,500) O (1,0,508) D (0) E").WithFamily("WIND"),
                "Source picker cannot silently change an existing reaction ID.");
            var doc = ReactionDocument.Parse("# Keep comment\nFIRE\nR 1000 V Effect I (1,0,20.) O (0,0,a,55) (0,0,101,60) D (0,1,2,3) E # Tail");
            Require(doc.Source.Contains("# Tail") && doc.Outputs.Count == 2, "Document preserves comments and repeated output cells.");
            doc = doc.Paint(false, 2, 1, "700");
            Require(doc.Source.Contains("V Effect") && doc.Source.Contains("(0,0,a,55) (0,0,101,60)") && doc.Source.EndsWith("# Tail"), "Painting preserves unrelated syntax and output order.");
            Reject(() => doc.Paint(false, 0, 0, "100"), "Painting cannot collapse multiple writes.");
            var erasedCapture = doc.Paint(true, 1, 0, "", true);
            Require(erasedCapture.Inputs.Count == 0 && erasedCapture.Outputs.Count == 2 &&
                erasedCapture.Outputs.All(e => !e.Fields.Any(f => f.Contains('a'))), "Deleting a capture removes only dependent output tuples.");
            Require(doc.Paint(true, 1, 0, "30.").Inputs.Single().Fields[2] == "30.", "Changing a wildcard state preserves its capture binding.");
            var dependent = ReactionDocument.Parse("# keep\nFIRE\nI (0,0,100) (1,0,20.) (2,0,30.) (3,0,50.) O (0,1,a) (0,2,b) (0,3,c) (1,1,a+b) (b,1,100,c) D (0) E # tail");
            var removedFirst = dependent.Paint(true, 1, 0, "", true);
            Require(removedFirst.Outputs.Count == 3 && removedFirst.At(false, 0, 2).Single().Fields[2] == "a" &&
                removedFirst.At(false, 0, 3).Single().Fields[2] == "b", "Surviving references shift together without rebinding to other inputs.");
            Require(removedFirst.Outputs.Last().Tuple == "(a,1,100,b)", "Deletion remaps coordinate and priority references too.");
            Require(removedFirst.Source.StartsWith("# keep") && removedFirst.Source.EndsWith("# tail") &&
                removedFirst.EditNotice.Contains("2 dependent"), "Deletion preserves comments and reports dependent output removals.");
            var removedMiddle = dependent.Paint(true, 2, 0, "", true);
            Require(removedMiddle.At(false, 0, 1).Single().Fields[2] == "a" && removedMiddle.At(false, 0, 3).Single().Fields[2] == "b" &&
                removedMiddle.Outputs.Count == 2, "Deleting the middle capture preserves earlier bindings and updates later ones.");
            var removedLast = dependent.Paint(true, 3, 0, "", true);
            Require(removedLast.Outputs.Count == 3 && removedLast.At(false, 0, 2).Single().Fields[2] == "b", "Deleting the last capture retains other variable names.");
            Require(WorkshopReactionCompiler.Parse(removedFirst.Source)[100].Single().VariableCount == 2, "Edited rule compiles with surviving capture bindings.");
            Require(dependent.EditCell(true, 1, 0, "").Source == removedFirst.Source, "Clearing a cell popup uses the same safe deletion behavior.");
            Reject(() => doc.Paint(false, 3, 0, "20."), "Output dot painting rejected.");
            Reject(() => doc.EditCell(false, 0, 0, "(0,0,20.)"), "Output popup dot input rejected.");
            var priority = ReactionDocument.Parse("FIRE\nI (0,0,100) O (1,0,101,87) D (0) E").Paint(false, 1, 0, "200");
            Require(priority.Source.Contains("(1,0,200,87)"), "Painting preserves output priority.");
            var zero = ReactionDocument.Parse(ReactionDocument.NewReaction).Paint(true, 0, 0, "0");
            Require(zero.Inputs.Count == 1 && zero.Inputs[0].Fields[2] == "0", "Explicit empty requirement is authored.");
            Require(zero.Paint(true, 0, 0, "", true).Inputs.Count == 0, "Erasing is distinct from requiring empty.");
            var wildcard = ReactionDocument.Parse("FIRE\nI (1+2,0,20.) O (a,0,101) D (0) E");
            Require(wildcard.At(true, 1, 0).Count() == 1 && wildcard.At(true, 2, 0).Count() == 1 && wildcard.HiddenEntries == 1,
                "Coordinate sets project while dynamic outputs remain visibly marked.");
            Reject(() => wildcard.Paint(true, 1, 0, "100"), "Coordinate set cannot be silently split.");
            Reject(() => ReactionDocument.Parse("FIRE\nI (0,0,999) O (0,0,100) D (0) E"), "Unknown palette ID rejected.");
            Reject(() => ReactionDocument.Parse("FIRE\nI (0,0,100) O (0,0,a) D (0) E"), "Unbound capture rejected.");
            Reject(() => ReactionDocument.Parse("FIRE\nI (0,0,100) O (0,0,200) D (4) E"), "Invalid rotation rejected.");
            var typed = doc.EditCell(false, 0, 0, "(0,0,a,80)\n(0,0,101,90)");
            Require(typed.Outputs[0].Fields[3] == "80" && typed.Outputs[1].Fields[3] == "90", "Popup preserves multiple outputs.");
            Require(WorkshopReactionCompiler.Parse(typed.Source)[100].Count == 4, "Multiline popup edits remain a compilable single-line rule.");
            foreach (int id in WorkshopPalette.Colors.Keys)
            {
                var colorRule = WorkshopReactionCompiler.Parse("FIRE\nI (0,0,100) O (1,0," + id + ") D (0) E")[100].Single();
                Require(colorRule.Outputs.Single().type == id, "Every palette ID compiles as an output: " + id);
            }

            foreach (int family in Enumerable.Range(1, 7))
            {
                int type = family * 100;
                var sim = new WorkshopSimulation();
                sim.Apply("", ReactionDocument.Parse(ReactionDocument.Families[family - 1] + "\nI (0,0," + type + ") O (1,0," + (type + 8) + ") D (0) E"));
                Require(sim.QueueTile(12, 12, type), "Queue extended family " + family);
                sim.TogglePause(); sim.Deploy();
                Require(sim.TickCount == 0 && sim.Board.Get(12, 12) == type, "Deploy while paused does not tick.");
                sim.Step(); Require(sim.Board.Get(12, 13) == type + 8, "Extended output simulates " + family);
                Require(sim.Queue.ReservedMana == 0, "Queue has no mana restriction.");
            }
            var full = WorkshopReactionCompiler.Parse("DARKNESS\nI (1,0,70.) O (0,1,a) D (0) E")[700].Single();
            Require(full.Requirements.Single().Types.Count == 10, "Wildcard includes all ten extended reactive stages.");
            Require(full.Outputs.Any(o => o.type == 709 && o.priority == 50), "New type priority is explicit and safe for shared resolver.");
            var rotated = WorkshopReactionCompiler.Parse("WIND\nI (1,0,60.) O (0,1,a) D (1) E")[500].Single();
            var board = new BoardState(7, 7);
            board.Set(3, 3, 500);
            var req = rotated.Requirements.Single(); board.Set(3 + req.y, 3 + req.x, 609);
            new ReactionResolver(board, f => f == 500 ? new[] { rotated } : Array.Empty<Reaction>()).Resolve();
            var outPos = GridMath.Rotate(0, 1, 1);
            Require(board.Get(3 + outPos.y, 3 + outPos.x) == 609, "Extended captured output rotates once.");

            var clock = new WorkshopSimulation(); clock.TogglePause();
            Require(!clock.Advance(100) && clock.TickCount == 0, "Paused clock never accumulates ticks.");
            for (int i = 0; i < 25; i++) clock.Step();
            Require(clock.TickCount == 25, "Rapid manual steps have no cooldown.");
            clock.TogglePause(); Require(!clock.Advance(.5f), "Resume has no catch-up ticks.");
            Require(clock.Advance(.5f) && clock.TickCount == 26, "One-second automatic tick.");
            Require(!clock.Step(), "Manual stepping requires pause.");
            clock.QueueTile(0, 0, 700); clock.Queue.Remove(0, 0); clock.Deploy();
            Require(clock.Board.Get(0, 0) == 0, "Right-click queue removal prevents deployment.");
            clock.QueueTile(0, 0, 700); clock.Board.Set(0, 0, 100); clock.Deploy();
            Require(clock.Board.Get(0, 0) == 100, "Deploy revalidates occupied cells.");

            var merge = new WorkshopSimulation();
            string context = "FIRE\nR 1000 I (0,0,100) O (0,1,101) D (0) E";
            var current = ReactionDocument.Parse("FIRE\nR 1000 I (0,0,100) O (1,0,200) D (0) E");
            merge.Apply(context, current);
            merge.Board.Set(12, 12, 100); merge.TogglePause(); merge.Step();
            Require(merge.Board.Get(12, 13) == 200 && merge.Board.Get(13, 12) == 0, "WIP replaces matching context ID.");
            Reject(() => merge.Apply("bad context", current), "Invalid context rejected atomically.");
            merge.Clear(); merge.Board.Set(12, 12, 100); merge.Step();
            Require(merge.Board.Get(12, 13) == 200, "Failed apply retains last valid simulation rules.");
            var decay = new WorkshopSimulation(); decay.Board.Set(12, 12, 710); decay.TogglePause(); decay.Step();
            Require(decay.Board.Get(12, 12) == 0 && decay.Board.Get(12, 13) == 711, "Extended stage 10 uses shared fading conventions.");
            decay.Step(); Require(decay.Board.Get(12, 13) == 0, "Extended stage 11 expires.");

            // Same engine + supported board IDs must retain gameplay behavior despite the wider authoring domain.
            var source = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Spells.txt");
            var gameplay = ReactionParser.Parse(source.text);
            var workshop = WorkshopReactionCompiler.Parse(source.text);
            var random = new System.Random(819);
            int[] supported = ElementDefinitions.Stages.Keys.ToArray();
            for (int sample = 0; sample < 40; sample++)
            {
                var a = new BoardState(9, 9); var b = new BoardState(9, 9);
                for (int r = 0; r < 9; r++) for (int c = 0; c < 9; c++)
                { int value = random.Next(3) == 0 ? supported[random.Next(supported.Length)] : 0; a.Set(r, c, value); b.Set(r, c, value); }
                var ar = new ReactionResolver(a, f => gameplay.TryGetValue(f, out var list) ? list : Array.Empty<Reaction>());
                var br = new ReactionResolver(b, f => workshop.TryGetValue(f, out var list) ? list : Array.Empty<Reaction>());
                for (int tick = 0; tick < 3; tick++)
                {
                    ar.Resolve(); br.Resolve();
                    Require(a.Cells.Cast<int>().SequenceEqual(b.Cells.Cast<int>()), "Gameplay parity sample " + sample + ", tick " + tick);
                }
            }
            return "PASS: " + checks + " workshop checks, including 120 gameplay parity snapshots.";
        }
    }
}
