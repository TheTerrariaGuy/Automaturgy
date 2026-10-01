using Assets.Scripts;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace GridMage.Workshop
{
    /// <summary>
    /// Workshop-local adaptation of ReactionParser: seven headers, the full palette domain,
    /// and explicit fallback priorities. Keep syntax/capture semantics aligned with gameplay;
    /// WorkshopChecks compares supported simulation snapshots. No global catalogs are changed.
    /// </summary>
    public static class WorkshopReactionCompiler
    {
        public const int MaxMappingsPerRule = 4096, MaxMappingsPerCatalog = 262144;
        public const int MaxVariables = 24;
        private static readonly Regex Tokens = new(@"\([^()]*\)|[^\s()]+", RegexOptions.Compiled);

        public static IReadOnlyDictionary<int, List<Reaction>> Parse(string text, bool allowEmpty = false,
            bool requireIds = false, string sourceName = "Spells.txt")
        {
            if (string.IsNullOrWhiteSpace(text)) throw new FormatException(sourceName + ": reaction definitions are empty.");
            var rules = new Dictionary<int, List<Reaction>>();
            var ids = new HashSet<int>();
            int family = 0, lineNumber = 0, totalMappings = 0;
            foreach (string raw in text.Split('\n'))
            {
                lineNumber++;
                string line = raw.Split('#')[0].Trim();
                if (line.Length == 0 || line == "START" || line == "END") continue;
                int header = line switch { "FIRE" => 100, "WATER" => 200, "ELECTRICITY" => 300, "STONE" => 400, "WIND" => 500, "LIGHT" => 600, "DARKNESS" => 700, _ => 0 };
                if (header != 0) { family = header; if (!rules.ContainsKey(family)) rules.Add(family, new()); continue; }
                try
                {
                    if (family == 0) throw new FormatException("A reaction needs an element header.");
                    var matches = Tokens.Matches(line);
                    int end = 0;
                    foreach (Match match in matches)
                    {
                        if (!string.IsNullOrWhiteSpace(line.Substring(end, match.Index - end))) throw new FormatException("Malformed tuple.");
                        end = match.Index + match.Length;
                    }
                    if (!string.IsNullOrWhiteSpace(line.Substring(end))) throw new FormatException("Malformed tuple.");
                    string[] tokens = matches.Cast<Match>().Select(m => m.Value).ToArray();
                    int i = 0, id = 0;
                    if (tokens[i] == "R")
                    {
                        id = int.Parse(tokens[++i], CultureInfo.InvariantCulture); i++;
                        if (id / 1000 != family / 100 || !ids.Add(id))
                            throw new FormatException("Reaction ID must be unique and match its element's thousands range.");
                    }
                    else if (requireIds) throw new FormatException("Expected R followed by a reaction ID.");
                    string effect = null;
                    if (tokens[i] == "V") { effect = tokens[++i]; i++; }
                    Expect(tokens, ref i, "I");
                    var requirements = new List<Requirement>();
                    var outputs = new List<ReactionOutput>();
                    var variables = new List<int[]>();
                    int mappings = 0;
                    while (i < tokens.Length && tokens[i] != "O")
                    {
                        string[] parts = Tuple(tokens[i++], 3, 3);
                        var xs = Coordinates(parts[0], "input x");
                        var ys = Coordinates(parts[1], "input y");
                        var types = Types(parts[2], "input type");
                        int xVariable = Capture(parts[0], xs), yVariable = Capture(parts[1], ys), typeVariable = Capture(parts[2], types);
                        AddMappings((long)xs.Length * ys.Length * types.Length);
                        foreach (int x in xs) foreach (int y in ys)
                            requirements.Add(new Requirement(x, y, types, xVariable, yVariable, typeVariable));
                    }
                    Expect(tokens, ref i, "O");
                    while (i < tokens.Length && tokens[i] != "D")
                    {
                        string[] parts = Tuple(tokens[i++], 3, 4);
                        outputs.Add(CompileOutput(parts, variables, AddMappings));
                    }
                    Expect(tokens, ref i, "D");
                    if (i >= tokens.Length) throw new FormatException("Missing directions.");
                    var directions = Tuple(tokens[i++], 1, int.MaxValue)
                        .SelectMany(p => Field(p, "direction", 0, 3, new[] { 0, 1, 2, 3 })).Distinct().ToArray();
                    Expect(tokens, ref i, "E");
                    if (i != tokens.Length || requirements.Count == 0 || outputs.Count == 0)
                        throw new FormatException("Reaction needs inputs, outputs, and no trailing tokens.");
                    // A normal rule is evaluated only at tiles in its header's family.
                    // A single origin requirement can intentionally match a different family
                    // during the overlap phase; multi-requirement rules cannot do that.
                    if (requirements.Count > 1)
                    {
                        var incompatible = requirements.FirstOrDefault(r => r.x == 0 && r.y == 0 &&
                            !r.Types.Any(t => ElementState.IsReactive(t) && ElementState.BaseType(t) == family));
                        if (incompatible != null)
                        {
                            var alternatives = incompatible.Types.Where(ElementState.IsReactive)
                                .Select(ElementState.BaseType).Distinct().ToArray();
                            string suggestion = alternatives.Length == 1 ?
                                "Choose " + ReactionDocument.Families[alternatives[0] / 100 - 1] + " as the source, or change the origin input." :
                                "Change the source family or the origin input.";
                            throw new FormatException("Source " + ReactionDocument.Families[family / 100 - 1] +
                                " cannot match the input at (0,0). " + suggestion);
                        }
                    }
                    totalMappings += mappings * directions.Length;
                    if (totalMappings > MaxMappingsPerCatalog) throw new FormatException("Catalog expansion limit exceeded.");
                    foreach (int turns in directions)
                        rules[family].Add(new Reaction(requirements.Select(r => Rotate(r, turns)),
                            outputs.Select(o => o.Rotate(turns)), variables.Count, effect, turns, id));

                    int Capture(string expression, int[] values)
                    {
                        if (!expression.Contains('.')) return -1;
                        if (variables.Count == MaxVariables) throw new FormatException("A reaction supports at most 24 capture variables (a through x).");
                        variables.Add(values);
                        return variables.Count - 1;
                    }

                    void AddMappings(long count)
                    {
                        if (count > MaxMappingsPerRule - mappings) throw new FormatException("Reaction expansion exceeds " + MaxMappingsPerRule + " mappings.");
                        mappings += (int)count;
                    }
                }
                catch (Exception e) when (e is FormatException || e is IndexOutOfRangeException || e is OverflowException)
                { throw new FormatException(sourceName + ": line " + lineNumber + ": " + e.Message, e); }
            }
            if (rules.Count == 0 || (!allowEmpty && rules.Values.All(r => r.Count == 0)))
                throw new FormatException(sourceName + ": no reactions were defined.");
            return rules;
        }
        private static int DefaultPriority(int type) => ElementDefinitions.Stages.TryGetValue(type, out var stage) ? stage.ReactionPriority : 50;
        private static ReactionOutput CompileOutput(string[] parts, List<int[]> variables, Action<long> addMappings)
        {
            int[] references = parts.SelectMany(p => p.Where(char.IsLetter)).Distinct().OrderBy(c => c)
                .Select(c => c - 'a').ToArray();
            foreach (int variable in references)
                if (variable < 0 || variable >= variables.Count)
                    throw new FormatException("Output references unbound variable: " + (char)('a' + variable));
            int[][] domains = references.Select(index => variables[index]).ToArray();
            long combinations = 1;
            foreach (var domain in domains)
            {
                combinations *= domain.Length;
                if (combinations > MaxMappingsPerRule) throw new FormatException("Output variable combinations exceed the reaction expansion limit.");
            }
            var rows = new List<IEnumerable<Offset>>();
            var bindings = new Dictionary<char, int>();
            CompileRow(0);
            return new ReactionOutput(references, domains, rows);

            void CompileRow(int variable)
            {
                if (variable < references.Length)
                {
                    foreach (int value in domains[variable])
                    {
                        bindings[(char)('a' + references[variable])] = value;
                        CompileRow(variable + 1);
                    }
                    return;
                }
                bool allowEmpty = references.Length > 0;
                var xs = Field(parts[0], "output x", -SpellSetExpression.MaxCoordinate, SpellSetExpression.MaxCoordinate, variables: bindings, allowEmpty: allowEmpty);
                var ys = Field(parts[1], "output y", -SpellSetExpression.MaxCoordinate, SpellSetExpression.MaxCoordinate, variables: bindings, allowEmpty: allowEmpty);
                var types = Field(parts[2], "output type", 0, int.MaxValue, WorkshopPalette.Domain, bindings, allowEmpty);
                int[] priorities = parts.Length == 4 ? Field(parts[3], "output priority", int.MinValue, int.MaxValue, variables: bindings, allowEmpty: allowEmpty) : null;
                // Empty rows also consume the compilation budget, even though they emit no writes.
                addMappings(Math.Max(1L, (long)xs.Length * ys.Length * types.Length * (priorities?.Length ?? 1)));
                var row = new List<Offset>();
                foreach (int x in xs) foreach (int y in ys) foreach (int type in types)
                {
                    if (priorities == null) row.Add(new Offset(x, y, type, DefaultPriority(type)));
                    else foreach (int priority in priorities) row.Add(new Offset(x, y, type, priority));
                }
                rows.Add(row);
            }
        }
        private static int[] Coordinates(string expression, string field) =>
            Field(expression, field, -SpellSetExpression.MaxCoordinate, SpellSetExpression.MaxCoordinate);
        private static int[] Types(string expression, string field) => Field(expression, field, 0, int.MaxValue, WorkshopPalette.Domain);
        private static int[] Field(string expression, string field, int minimum, int maximum, IEnumerable<int> domain = null,
            IReadOnlyDictionary<char, int> variables = null, bool allowEmpty = false)
        {
            try { return SpellSetExpression.Compile(expression, minimum, maximum, domain, variables, allowEmpty); }
            catch (FormatException e) { throw new FormatException(field + " '" + expression + "': " + e.Message, e); }
        }
        private static void Expect(string[] tokens, ref int i, string expected)
        {
            if (i >= tokens.Length || tokens[i++] != expected) throw new FormatException("Expected " + expected + ".");
        }
        private static string[] Tuple(string token, int minimum, int maximum)
        {
            if (!token.StartsWith("(") || !token.EndsWith(")")) throw new FormatException("Expected a tuple: " + token);
            string[] parts = token.Substring(1, token.Length - 2).Split(',');
            if (parts.Length < minimum || parts.Length > maximum) throw new FormatException("Wrong tuple length: " + token);
            return parts;
        }
        private static Requirement Rotate(Requirement r, int turns)
        {
            var p = GridMath.Rotate(r.x, r.y, turns);
            return new Requirement(p.x, p.y, r.Types, r.XVariable, r.YVariable, r.TypeVariable, r.CaptureX, r.CaptureY);
        }
    }
}

