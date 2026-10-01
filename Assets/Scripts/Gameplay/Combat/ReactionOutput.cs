using System;
using System.Collections.Generic;
using System.Linq;

namespace Assets.Scripts
{
    /// <summary>A baked output tuple: captured values select concrete writes without evaluating expressions.</summary>
    public sealed class ReactionOutput
    {
        private readonly int[] variables;
        private readonly Dictionary<int, int>[] indices;
        private readonly IReadOnlyList<Offset>[] rows;
        public IReadOnlyList<Offset> AllOutputs { get; }

        public ReactionOutput(IEnumerable<Offset> outputs)
            : this(Array.Empty<int>(), Array.Empty<int[]>(), new[] { outputs }) { }

        internal ReactionOutput(int[] variables, int[][] domains, IEnumerable<IEnumerable<Offset>> outputs)
        {
            this.variables = (int[])variables.Clone();
            indices = domains.Select(domain => domain.Select((value, index) => (value, index))
                .ToDictionary(p => p.value, p => p.index)).ToArray();
            rows = outputs.Select(row => (IReadOnlyList<Offset>)Array.AsReadOnly(row.Distinct().ToArray())).ToArray();
            AllOutputs = Array.AsReadOnly(rows.SelectMany(row => row).Distinct().ToArray());
        }

        public IReadOnlyList<Offset> Resolve(int[] captures)
        {
            int row = 0;
            for (int i = 0; i < variables.Length; i++)
                row = row * indices[i].Count + indices[i][captures[variables[i]]];
            return rows[row];
        }

        internal ReactionOutput Rotate(int turns)
        {
            var domains = indices.Select(index => index.OrderBy(p => p.Value).Select(p => p.Key).ToArray()).ToArray();
            return new ReactionOutput(variables, domains, rows.Select(row => row.Select(output =>
            {
                var p = GridMath.Rotate(output.x, output.y, turns);
                return new Offset(p.x, p.y, output.type, output.priority);
            })));
        }
    }
}
