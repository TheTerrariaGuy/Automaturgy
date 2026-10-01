using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Assets.Scripts
{
    /// <summary>Compiles left-to-right unions/differences of signed integers and digit patterns.</summary>
    public static class SpellSetExpression
    {
        public const int MaxValues = 1024, MaxCoordinate = 1024;

        public static int[] Compile(string expression, int minimum, int maximum, IEnumerable<int> domain = null,
            IReadOnlyDictionary<char, int> variables = null, bool allowEmpty = false)
        {
            if (string.IsNullOrWhiteSpace(expression)) throw new FormatException("Empty set expression.");
            if (expression.Length > 4096) throw new FormatException("Set expression is too long.");
            var allowed = domain == null ? null : new HashSet<int>(domain);
            var values = new SortedSet<int>();
            int cursor = 0;
            bool first = true;
            while (cursor < expression.Length)
            {
                SkipSpace();
                char operation = '+';
                if (!first)
                {
                    if (cursor >= expression.Length || (expression[cursor] != '+' && expression[cursor] != '-'))
                        throw new FormatException("Expected + (union) or - (difference).");
                    operation = expression[cursor++];
                    SkipSpace();
                }
                int sign = 1;
                if (cursor < expression.Length && (expression[cursor] == '+' || expression[cursor] == '-'))
                    sign = expression[cursor++] == '-' ? -1 : 1;
                SkipSpace();
                if (cursor < expression.Length && char.IsLetter(expression[cursor]))
                {
                    char variable = expression[cursor++];
                    if (variable < 'a' || variable > 'x' || variables == null || !variables.TryGetValue(variable, out int captured))
                        throw new FormatException("Unknown or unavailable variable: " + variable);
                    long resolved = (long)sign * captured;
                    if (resolved < minimum || resolved > maximum || (allowed != null && !allowed.Contains((int)resolved)))
                        throw new FormatException("Variable " + variable + " resolves outside this field's domain: " + resolved);
                    if (operation == '+') values.Add((int)resolved); else values.Remove((int)resolved);
                    if (values.Count > MaxValues) throw new FormatException("Set expands beyond " + MaxValues + " values.");
                    first = false;
                    SkipSpace();
                    continue;
                }
                int start = cursor;
                while (cursor < expression.Length && ((expression[cursor] >= '0' && expression[cursor] <= '9') || expression[cursor] == '.')) cursor++;
                if (cursor == start) throw new FormatException("Expected an integer or digit pattern.");
                string pattern = expression.Substring(start, cursor - start);
                if (pattern.Length > 10) throw new FormatException("Integer or digit pattern is too wide.");
                if (pattern.Length > 1 && pattern[0] == '0') throw new FormatException("Leading zeroes are not supported.");
                var term = new List<int>();
                if (!pattern.Contains('.'))
                {
                    if (!long.TryParse(pattern, NumberStyles.None, CultureInfo.InvariantCulture, out long magnitude))
                        throw new FormatException("Invalid integer.");
                    long value = sign * magnitude;
                    if (value < minimum || value > maximum || (allowed != null && !allowed.Contains((int)value)))
                        throw new FormatException("Value outside this field's domain: " + value);
                    term.Add((int)value);
                }
                else if (allowed != null)
                {
                    foreach (int value in allowed.OrderBy(v => v))
                        if (value >= minimum && value <= maximum && Matches(value, sign, pattern)) term.Add(value);
                }
                else
                {
                    Expand(0, 0);
                    void Expand(int digit, long magnitude)
                    {
                        if (digit == pattern.Length)
                        {
                            long value = sign * magnitude;
                            if (value >= minimum && value <= maximum) term.Add((int)value);
                            if (term.Count > MaxValues) throw new FormatException("Set expands beyond " + MaxValues + " values.");
                            return;
                        }
                        int low = pattern[digit] == '.' ? 0 : pattern[digit] - '0';
                        int high = pattern[digit] == '.' ? 9 : low;
                        for (int number = low; number <= high; number++)
                        {
                            if (digit == 0 && number == 0 && pattern.Length > 1) continue;
                            long next = magnitude * 10 + number;
                            long scale = 1;
                            for (int remaining = digit + 1; remaining < pattern.Length; remaining++) scale *= 10;
                            long smallest = next * scale, largest = smallest + scale - 1;
                            long lower = sign > 0 ? smallest : -largest, upper = sign > 0 ? largest : -smallest;
                            if (lower > maximum || upper < minimum) continue;
                            Expand(digit + 1, next);
                        }
                    }
                }
                if (term.Count == 0) throw new FormatException("Pattern matches no values in this field: " + pattern);
                if (operation == '+') values.UnionWith(term); else values.ExceptWith(term);
                if (values.Count > MaxValues) throw new FormatException("Set expands beyond " + MaxValues + " values.");
                first = false;
                SkipSpace();
            }
            if (values.Count == 0 && !allowEmpty) throw new FormatException("Set expression evaluates to an empty set.");
            return values.ToArray();

            void SkipSpace() { while (cursor < expression.Length && char.IsWhiteSpace(expression[cursor])) cursor++; }
        }

        private static bool Matches(int value, int sign, string pattern)
        {
            if ((value < 0 && sign > 0) || (value > 0 && sign < 0)) return false;
            string digits = Math.Abs((long)value).ToString(CultureInfo.InvariantCulture);
            if (digits.Length != pattern.Length) return false;
            for (int i = 0; i < digits.Length; i++)
                if (pattern[i] != '.' && pattern[i] != digits[i]) return false;
            return true;
        }
    }
}
