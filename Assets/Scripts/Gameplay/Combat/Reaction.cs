using System;
using System.Collections.Generic;
using System.Linq;

namespace Assets.Scripts
{
    public sealed class Reaction
    {
        public int Id { get; }
        public IReadOnlyCollection<Requirement> Requirements { get; }
        public IReadOnlyCollection<Offset> Outputs { get; }
        public IReadOnlyList<ReactionOutput> OutputMappings { get; }
        public int VariableCount { get; }
        public string Effect { get; }
        public int Direction { get; }
    
        public Reaction(
            IEnumerable<Requirement> requirements,
            IEnumerable<Offset> outputs, string effect = null, int direction = 0, int id = 0)
            : this(requirements, new[] { new ReactionOutput(outputs) }, 0, effect, direction, id) { }

        internal Reaction(IEnumerable<Requirement> requirements, IEnumerable<ReactionOutput> outputs,
            int variableCount, string effect = null, int direction = 0, int id = 0)
        {
            Id = id;
            Requirements = Array.AsReadOnly(requirements.Distinct().ToArray());
            OutputMappings = Array.AsReadOnly(outputs.ToArray());
            // All possible baked outputs for inspection; combat selects the matching rows.
            Outputs = Array.AsReadOnly(OutputMappings.SelectMany(o => o.AllOutputs).Distinct().ToArray());
            VariableCount = variableCount;
            Effect = effect;
            Direction = direction;
        }
    }
    
    public sealed class Requirement : IEquatable<Requirement>
    {
        public int x { get; }
        public int y { get; }
        public IReadOnlyList<int> Types { get; }
        public int XVariable { get; }
        public int YVariable { get; }
        public int TypeVariable { get; }
        public int CaptureX { get; }
        public int CaptureY { get; }
        private readonly HashSet<int> accepted;
    
        public Requirement(int x, int y, int type) : this(x, y, new[] { type }) { }
        public Requirement(int x, int y, IEnumerable<int> types, int xVariable = -1, int yVariable = -1,
            int typeVariable = -1, int? captureX = null, int? captureY = null)
        {
            this.x = x;
            this.y = y;
            accepted = new HashSet<int>(types);
            if (accepted.Count == 0) throw new ArgumentException("A requirement needs at least one tile type.");
            Types = Array.AsReadOnly(accepted.OrderBy(t => t).ToArray());
            XVariable = xVariable; YVariable = yVariable; TypeVariable = typeVariable;
            CaptureX = captureX ?? x; CaptureY = captureY ?? y;
        }
    
        public bool Matches(int actualType) => accepted.Contains(actualType);
        public bool Equals(Requirement other) => other != null && x == other.x && y == other.y && Types.SequenceEqual(other.Types) &&
            XVariable == other.XVariable && YVariable == other.YVariable && TypeVariable == other.TypeVariable &&
            CaptureX == other.CaptureX && CaptureY == other.CaptureY;
        public override bool Equals(object obj) => obj is Requirement other && Equals(other);
        public override int GetHashCode()
        {
            var hash = new HashCode(); hash.Add(x); hash.Add(y);
            hash.Add(XVariable); hash.Add(YVariable); hash.Add(TypeVariable); hash.Add(CaptureX); hash.Add(CaptureY);
            foreach (int type in Types) hash.Add(type);
            return hash.ToHashCode();
        }
    }
    
    public record Offset
    {
        public int x { get; }
        public int y { get; }
        public int type { get; }
        public int priority { get; }
    
        public Offset(int x, int y, int type, int? priority = null)
        {
            this.x = x;
            this.y = y;
            this.type = type;
            this.priority = priority ?? ElementDefinitions.ReactionPriority(type);
        }
    }
    
}
