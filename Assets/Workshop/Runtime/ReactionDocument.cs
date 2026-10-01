using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Assets.Scripts;

namespace GridMage.Workshop
{
    /// <summary>Source-preserving document. Grid edits replace only selected tuple spans.</summary>
    public sealed class ReactionDocument
    {
        public const string NewReaction = "FIRE\nI O D (0,1,2,3) E";
        public static readonly string[] Families = { "FIRE", "WATER", "ELECTRICITY", "STONE", "WIND", "LIGHT", "DARKNESS" };
        private static readonly Regex Tokens = new Regex(@"\([^()]*\)|[^\s()]+", RegexOptions.Compiled);
        public sealed class Entry
        {
            public string[] Fields;
            public int Start, Length;
            public int[] X, Y;
            public readonly string[] CaptureNames = new string[3];
            public string GridLabel => (CaptureNames[2] ?? Fields[2]) +
                (CaptureNames[0] == null && CaptureNames[1] == null ? "" : "\n" +
                    string.Join(" ", Enumerable.Range(0, 2).Where(f => CaptureNames[f] != null).Select(f => (f == 0 ? "x:" : "y:") + CaptureNames[f])));
            public string HoverDetail => Tuple + string.Concat(Enumerable.Range(0, 3).Where(f => CaptureNames[f] != null)
                .Select(f => "\n" + CaptureNames[f] + " = " + Fields[f] + " (" + (f == 0 ? "x" : f == 1 ? "y" : "state") + ")"));
            public bool IsConcrete => int.TryParse(Fields[0], out _) && int.TryParse(Fields[1], out _);
            public string Tuple => "(" + string.Join(",", Fields) + ")";
            public bool Contains(int x, int y) => X != null && Y != null && X.Contains(x) && Y.Contains(y);
        }
        public string Source { get; private set; }
        public string Family { get; private set; }
        public int Id { get; private set; }
        public string EditNotice { get; private set; }
        public readonly List<Entry> Inputs = new List<Entry>(), Outputs = new List<Entry>();
        private int inputEnd, outputEnd, familyStart;
        public IEnumerable<Entry> At(bool input, int x, int y) => (input ? Inputs : Outputs).Where(e => e.Contains(x, y));
        public int HiddenEntries => Inputs.Concat(Outputs).Count(e => e.X == null || e.Y == null || e.X.Any(x => Math.Abs(x) > 7) || e.Y.Any(y => Math.Abs(y) > 7));

        public static ReactionDocument Parse(string source)
        {
            var doc = new ReactionDocument { Source = source ?? "" };
            var mask = Regex.Replace(doc.Source, @"#[^\r\n]*", m => new string(' ', m.Length));
            var matches = Tokens.Matches(mask).Cast<Match>().ToArray();
            int end = 0;
            foreach (var m in matches)
            {
                if (!string.IsNullOrWhiteSpace(mask.Substring(end, m.Index - end))) throw new FormatException("Malformed tuple.");
                end = m.Index + m.Length;
            }
            if (!string.IsNullOrWhiteSpace(mask.Substring(end))) throw new FormatException("Malformed tuple.");
            int i = 0;
            string Take() { if (i >= matches.Length) throw new FormatException("Incomplete reaction."); return matches[i++].Value; }
            if (i < matches.Length && matches[i].Value == "START") i++;
            doc.Family = Take();
            doc.familyStart = matches[i - 1].Index;
            if (!Families.Contains(doc.Family)) throw new FormatException("Begin WIP with an element header, e.g. FIRE.");
            if (i < matches.Length && matches[i].Value == "R")
            {
                i++;
                if (!int.TryParse(Take(), out int id) || id / 1000 != Array.IndexOf(Families, doc.Family) + 1)
                    throw new FormatException("Reaction ID must match the family's thousands range.");
                doc.Id = id;
            }
            if (i < matches.Length && matches[i].Value == "V") { i++; Take(); }
            if (Take() != "I") throw new FormatException("Expected I.");
            ReadEntries(doc.Inputs, "O", true);
            doc.inputEnd = matches[i].Index;
            i++;
            ReadEntries(doc.Outputs, "D", false);
            doc.outputEnd = matches[i].Index;
            i++;
            string directions = Take();
            var parts = Tuple(directions, 1, 4);
            foreach (string part in parts) SpellSetExpression.Compile(part, 0, 3, new[] { 0, 1, 2, 3 });
            if (Take() != "E") throw new FormatException("Expected E.");
            if (i < matches.Length && matches[i].Value == "END") i++;
            if (i != matches.Length) throw new FormatException("WIP holds exactly one reaction. Put other rules in Context.");
            doc.ValidateExpressions();
            return doc;

            void ReadEntries(List<Entry> entries, string stop, bool input)
            {
                while (i < matches.Length && matches[i].Value != stop)
                {
                    var m = matches[i++];
                    var e = new Entry { Fields = Tuple(m.Value, 3, input ? 3 : 4), Start = m.Index, Length = m.Length };
                    e.X = Coordinates(e.Fields[0]); e.Y = Coordinates(e.Fields[1]);
                    if (input && (e.X == null || e.Y == null)) throw new FormatException("Input coordinates cannot use variables.");
                    entries.Add(e);
                }
                if (i >= matches.Length) throw new FormatException("Expected " + stop + ".");
            }
        }

        private static string[] Tuple(string text, int min, int max)
        {
            if (!text.StartsWith("(") || !text.EndsWith(")")) throw new FormatException("Expected a tuple: " + text);
            var fields = text.Substring(1, text.Length - 2).Split(',').Select(s => s.Trim()).ToArray();
            if (fields.Length < min || fields.Length > max) throw new FormatException("Wrong tuple length: " + text);
            return fields;
        }
        private static int[] Coordinates(string expression) => expression.Any(char.IsLetter) ? null :
            SpellSetExpression.Compile(expression, -SpellSetExpression.MaxCoordinate, SpellSetExpression.MaxCoordinate);

        private void ValidateExpressions()
        {
            var variables = new List<int[]>();
            long mappings = 0;
            foreach (var e in Inputs)
            {
                int[] types = SpellSetExpression.Compile(e.Fields[2], 0, int.MaxValue, WorkshopPalette.Domain);
                var domains = new[] { e.X, e.Y, types };
                for (int f = 0; f < 3; f++) if (e.Fields[f].Contains('.'))
                {
                    e.CaptureNames[f] = ((char)('a' + variables.Count)).ToString();
                    variables.Add(domains[f]);
                }
                mappings += (long)e.X.Length * e.Y.Length * types.Length;
            }
            if (variables.Count > ReactionParser.MaxVariables) throw new FormatException("At most 24 capture variables (a-x) are supported.");
            foreach (var e in Outputs)
            {
                var refs = e.Fields.SelectMany(s => s.Where(char.IsLetter)).Distinct().OrderBy(c => c).ToArray();
                long combinations = 1;
                foreach (char c in refs)
                {
                    if (c < 'a' || c - 'a' >= variables.Count) throw new FormatException("Unbound output variable: " + c);
                    combinations *= variables[c - 'a'].Length;
                    if (combinations > ReactionParser.MaxMappingsPerRule) throw new FormatException("Too many output variable combinations.");
                }
                var bindings = new Dictionary<char, int>();
                ValidateRow(0);
                void ValidateRow(int depth)
                {
                    if (depth < refs.Length)
                    {
                        foreach (int value in variables[refs[depth] - 'a']) { bindings[refs[depth]] = value; ValidateRow(depth + 1); }
                        return;
                    }
                    long count = 1;
                    for (int f = 0; f < e.Fields.Length; f++)
                    {
                        int min = f < 2 ? -SpellSetExpression.MaxCoordinate : f == 2 ? 0 : int.MinValue;
                        int max = f < 2 ? SpellSetExpression.MaxCoordinate : int.MaxValue;
                        count *= SpellSetExpression.Compile(e.Fields[f], min, max, f == 2 ? WorkshopPalette.Domain : null, bindings, refs.Length > 0).Length;
                        if (count > ReactionParser.MaxMappingsPerRule) throw new FormatException("Reaction expansion limit exceeded.");
                    }
                    mappings += Math.Max(1, count);
                    CheckBudget();
                }
            }
            CheckBudget();
            void CheckBudget() { if (mappings > ReactionParser.MaxMappingsPerRule) throw new FormatException("Reaction expansion limit exceeded."); }
        }

        public ReactionDocument WithFamily(string family)
        {
            int index = Array.IndexOf(Families, family);
            if (index < 0) throw new FormatException("Unknown source family: " + family);
            if (Id != 0 && Id / 1000 != index + 1)
                throw new FormatException("R " + Id + " belongs to " + Family + ". Edit the header and ID together in WIP; " +
                    family + " IDs are " + ((index + 1) * 1000) + "-" + ((index + 2) * 1000 - 1) + ".");
            return Parse(Source.Remove(familyStart, Family.Length).Insert(familyStart, family));
        }

        public ReactionDocument Paint(bool input, int x, int y, string type, bool erase = false)
        {
            var entries = At(input, x, y).ToArray();
            if (entries.Any(e => !e.IsConcrete)) throw new FormatException("This cell belongs to a coordinate expression. Edit its tuple with Shift-click or in WIP.");
            if (erase && input) return EraseInputs(entries);
            if (!erase && !input && type.Contains('.')) throw new FormatException("Dot notation is only available on the input grid.");
            // Inputs are standalone match expressions. Output expressions must be validated
            // by Replace/Parse against this document's captured variables, not in isolation.
            if (!erase && input) SpellSetExpression.Compile(type, 0, int.MaxValue, WorkshopPalette.Domain);
            if (entries.Length > 1 && !erase) throw new FormatException("This cell contains multiple tuples. Shift-click to edit them without losing priorities.");
            string priority = !input && entries.Length == 1 && entries[0].Fields.Length == 4 ? "," + entries[0].Fields[3] : "";
            string replacement = erase ? "" : "(" + x + "," + y + "," + type + priority + ")";
            return Replace(input, entries, replacement);
        }

        public ReactionDocument EditCell(bool input, int x, int y, string tuples)
        {
            if (!input && tuples.Contains('.')) throw new FormatException("Dot notation is only available on the input grid.");
            var entries = At(input, x, y).ToArray();
            if (entries.Length == 0) throw new FormatException("There is no authored entry at this cell.");
            // Only tuples may be entered here, never section/header tokens.
            foreach (Match m in Tokens.Matches(tuples)) Tuple(m.Value, 3, input ? 3 : 4);
            if (input && string.IsNullOrWhiteSpace(tuples)) return EraseInputs(entries);
            return Replace(input, entries, Regex.Replace(tuples.Trim(), @"\s*\r?\n\s*", " "));
        }

        private ReactionDocument EraseInputs(Entry[] entries)
        {
            if (entries.Length == 0) return this;
            var removed = new HashSet<Entry>(entries);
            var deletedNames = new HashSet<char>();
            var renamed = new Dictionary<char, char>();
            int nextName = 0;
            foreach (var entry in Inputs)
                foreach (string name in entry.CaptureNames.Where(n => n != null))
                    if (removed.Contains(entry)) deletedNames.Add(name[0]);
                    else renamed[name[0]] = (char)('a' + nextName++);

            var edits = entries.Select(e => (entry: e, text: "")).ToList();
            int dependentOutputs = 0;
            foreach (var entry in Outputs)
            {
                if (entry.Fields.Any(field => field.Any(deletedNames.Contains)))
                {
                    // A write cannot be evaluated without its captured value. Remove the
                    // complete tuple rather than altering the meaning of a set expression.
                    edits.Add((entry, "")); dependentOutputs++;
                }
                else
                {
                    // Replace in one pass: b -> a and c -> b must not turn both into a.
                    string text = Regex.Replace(entry.Tuple, "[a-x]", m => renamed[m.Value[0]].ToString());
                    if (text != entry.Tuple) edits.Add((entry, text));
                }
            }
            string source = Source;
            foreach (var edit in edits.OrderByDescending(e => e.entry.Start))
                source = source.Remove(edit.entry.Start, edit.entry.Length).Insert(edit.entry.Start, edit.text);
            var result = Parse(source);
            if (deletedNames.Count > 0)
                result.EditNotice = "Removed " + string.Join(", ", deletedNames.OrderBy(c => c)) + "; " + dependentOutputs +
                    " dependent output tuple(s) removed. Surviving variable references updated.";
            return result;
        }

        private ReactionDocument Replace(bool input, Entry[] entries, string replacement)
        {
            string next = Source;
            if (entries.Length == 0) next = next.Insert(input ? inputEnd : outputEnd, replacement + " ");
            else for (int i = entries.Length - 1; i >= 0; i--)
                next = next.Remove(entries[i].Start, entries[i].Length).Insert(entries[i].Start, i == 0 ? replacement : "");
            var result = Parse(next);
            // Removing or moving wildcard fields would otherwise silently rebind output variables.
            if (input && Outputs.Any(e => e.Fields.Any(s => s.Any(char.IsLetter))) && CaptureSignature(this) != CaptureSignature(result))
                throw new FormatException("This changes capture bindings used by outputs. Update the input and output expressions together in WIP, then Apply.");
            return result;
        }
        private static string CaptureSignature(ReactionDocument doc) => string.Join("|", doc.Inputs.SelectMany(e =>
            e.Fields.Select((field, index) => field.Contains('.') ? index + ":" + e.Fields[0] + "," + e.Fields[1] : null).Where(s => s != null)));
    }
}
