using System.Collections.Generic;
using System.Linq;

namespace Assets.Scripts
{
    public sealed class Reaction
    {
        public int Id { get; }
        public IReadOnlyCollection<Requirement> Requirements { get; }
        public IReadOnlyCollection<Offset> Outputs { get; }
        public string Effect { get; }
        public int Direction { get; }
    
        public Reaction(
            IEnumerable<Requirement> requirements,
            IEnumerable<Offset> outputs, string effect = null, int direction = 0, int id = 0)
        {
            Id = id;
            Requirements = requirements.Distinct().ToArray();
            Outputs = outputs.Distinct().ToArray();
            Effect = effect;
            Direction = direction;
        }
    }
    
    public record Requirement
    {
        public int x { get; }
        public int y { get; }
        public int type { get; }
        public bool matchFamily { get; }
    
        public Requirement(int x, int y, int type, bool matchFamily)
        {
            this.x = x;
            this.y = y;
            this.type = type;
            this.matchFamily = matchFamily;
        }
    
        public bool Matches(int actualType)
        {
            if (!matchFamily)
            {
                return actualType == type;
            }
    
            return actualType >= 100 &&
                   actualType % 100 < 10 &&
                   actualType / 100 == type / 100;
        }
    }
    
    public record Offset
    {
        public int x { get; }
        public int y { get; }
        public int type { get; }
        public int priority { get; }
    
        public Offset(int x, int y, int type, int priority = 0)
        {
            this.x = x;
            this.y = y;
            this.type = type;
            this.priority = priority;
        }
    }
    
}
