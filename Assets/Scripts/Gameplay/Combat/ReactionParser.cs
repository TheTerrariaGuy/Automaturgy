using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Assets.Scripts
{
    /// <summary>Parses one reaction per line, retaining authoring and rotation order for deterministic ties.</summary>
    public static class ReactionParser
    {
        public static IReadOnlyDictionary<int, List<Reaction>> Parse(string text, bool allowEmpty = false, bool requireIds = false)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new FormatException("Reaction definitions are empty.");
            var rules = new Dictionary<int, List<Reaction>>();
            var ids = new HashSet<int>();
            int family = 0, lineNumber = 0;
            foreach (string raw in text.Split('\n'))
            {
                lineNumber++;
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line == "START" || line == "END") continue;
                int header = line switch { "FIRE" => 100, "WATER" => 200, "ELECTRICITY" => 300, "STONE" => 400, _ => 0 };
                if (header != 0) { family = header; if (!rules.ContainsKey(family)) rules.Add(family, new()); continue; }
                try
                {
                    if (family == 0) throw new FormatException("A reaction needs an element header.");
                    string[] tokens = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    int i = 0;
                    int id = 0;
                    if (tokens[i] == "R")
                    {
                        id = Number(tokens[++i]); i++;
                        if (id / 1000 != family / 100 || !ids.Add(id))
                            throw new FormatException("Reaction ID must be unique and match its element's thousands range.");
                    }
                    else if (requireIds) throw new FormatException("Expected R followed by a reaction ID.");
                    string effect = null;
                    if (tokens[i] == "V") { effect = tokens[++i]; i++; }
                    Expect(tokens, ref i, "I");
                    var requirements = new List<Requirement>();
                    while (i < tokens.Length && tokens[i] != "O")
                    {
                        string[] parts = Tuple(tokens[i++], 3);
                        bool matchFamily = parts[2].EndsWith("*", StringComparison.Ordinal);
                        int type = Number(matchFamily ? parts[2].TrimEnd('*') : parts[2]);
                        if (matchFamily ? type < 100 || type > 400 || type % 100 != 0 : !ElementDefinitions.Stages.ContainsKey(type))
                            throw new FormatException("Unknown or invalid requirement type: " + parts[2]);
                        requirements.Add(new Requirement(Number(parts[0]), Number(parts[1]), type, matchFamily));
                    }
                    Expect(tokens, ref i, "O");
                    var outputs = new List<Offset>();
                    while (i < tokens.Length && tokens[i] != "D")
                    {
                        string[] parts = Tuple(tokens[i++], 4);
                        int type = Number(parts[2]);
                        if (!ElementDefinitions.Stages.ContainsKey(type)) throw new FormatException("Unknown output type: " + type);
                        outputs.Add(new Offset(Number(parts[0]), Number(parts[1]), type, Number(parts[3])));
                    }
                    Expect(tokens, ref i, "D");
                    if (i >= tokens.Length) throw new FormatException("Missing directions.");
                    string[] directions = Tuple(tokens[i++], -1);
                    Expect(tokens, ref i, "E");
                    if (i != tokens.Length || requirements.Count == 0 || outputs.Count == 0)
                        throw new FormatException("Reaction needs inputs, outputs, and no trailing tokens.");
                    foreach (string direction in directions)
                    {
                        int turns = Number(direction);
                        if (turns < 0 || turns > 3) throw new FormatException("Directions must be 0, 1, 2, or 3.");
                        rules[family].Add(new Reaction(requirements.Select(r => Rotate(r, turns)),
                            outputs.Select(o => Rotate(o, turns)), effect, turns, id));
                    }
                }
                catch (Exception e) when (e is FormatException || e is IndexOutOfRangeException || e is OverflowException)
                { throw new FormatException("Reaction line " + lineNumber + ": " + e.Message, e); }
            }
            if (rules.Count == 0 || (!allowEmpty && rules.Values.All(r => r.Count == 0)))
                throw new FormatException("No reactions were defined.");
            return rules;
        }
        private static int Number(string value) => int.Parse(value, CultureInfo.InvariantCulture);
        private static void Expect(string[] tokens, ref int i, string expected)
        {
            if (i >= tokens.Length || tokens[i++] != expected) throw new FormatException("Expected " + expected + ".");
        }
        private static string[] Tuple(string token, int count)
        {
            if (!token.StartsWith("(") || !token.EndsWith(")")) throw new FormatException("Expected a tuple: " + token);
            string[] parts = token.Substring(1, token.Length - 2).Split(',');
            if (count >= 0 && parts.Length != count) throw new FormatException("Wrong tuple length: " + token);
            return parts;
        }
        private static Requirement Rotate(Requirement r, int turns)
        { var p = GridMath.Rotate(r.x, r.y, turns); return new Requirement(p.x, p.y, r.type, r.matchFamily); }
        private static Offset Rotate(Offset o, int turns)
        { var p = GridMath.Rotate(o.x, o.y, turns); return new Offset(p.x, p.y, o.type, o.priority); }
    }
}
