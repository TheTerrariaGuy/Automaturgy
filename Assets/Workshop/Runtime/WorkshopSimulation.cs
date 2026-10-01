using System;
using System.Collections.Generic;
using System.Linq;
using Assets.Scripts;

namespace GridMage.Workshop
{
    public sealed class WorkshopSimulation
    {
        public BoardState Board { get; } = new BoardState(25, 25);
        public SpellQueue Queue { get; } = new SpellQueue();
        public bool Paused { get; private set; }
        public int TickCount { get; private set; }
        public float TickSeconds = 1f;
        public float Progress => elapsed / TickSeconds;
        private float elapsed;
        private IReadOnlyDictionary<int, List<Reaction>> rules = new Dictionary<int, List<Reaction>>();
        private readonly ReactionResolver resolver;
        public WorkshopSimulation() => resolver = new ReactionResolver(Board, family =>
            rules.TryGetValue(family, out var list) ? list : Array.Empty<Reaction>());

        public string Apply(string context, ReactionDocument wip)
        {
            // Compile everything before publishing; failed edits never partially replace active rules.
            var compiled = string.IsNullOrWhiteSpace(context) ? new Dictionary<int, List<Reaction>>() :
                WorkshopReactionCompiler.Parse(context, allowEmpty: true, sourceName: "Context").ToDictionary(p => p.Key, p => new List<Reaction>(p.Value));
            bool draft = wip.Inputs.Count == 0 || wip.Outputs.Count == 0;
            bool replaced = false;
            if (!draft)
            {
                var current = WorkshopReactionCompiler.Parse(wip.Source, sourceName: "WIP");
                foreach (var pair in current)
                {
                    if (!compiled.TryGetValue(pair.Key, out var list)) compiled[pair.Key] = list = new List<Reaction>();
                    if (wip.Id != 0) replaced |= list.RemoveAll(r => r.Id == wip.Id) > 0;
                    list.AddRange(pair.Value);
                }
            }
            rules = compiled;
            return draft ? "Context active. WIP needs an input and output before it can simulate." :
                replaced ? "Applied. WIP replaces its context ID and runs last on priority ties." : "Applied. Context + WIP active; WIP runs last on priority ties.";
        }
        public bool QueueTile(int row, int col, int type)
        {
            if (!WorkshopPalette.Colors.ContainsKey(type)) throw new FormatException("Choose a concrete tile ID from the workshop palette.");
            if (!Board.HasCell(row, col) || !ElementState.CanPlaceOn(Board.Get(row, col)) || Queue.Contains(row, col)) return false;
            Queue.Add(row, col, type, 0); return true;
        }
        private void Prune() => Queue.RemoveInvalid((r, c) => ElementState.CanPlaceOn(Board.Get(r, c)), (_, __) => { });
        public void Deploy()
        {
            Prune();
            foreach (var entry in Queue.Entries) Board.Set(entry.Key.row, entry.Key.col, entry.Value.type);
            Queue.Clear();
        }
        public void TogglePause() { Paused = !Paused; elapsed = 0; }
        public bool Step() { if (!Paused) return false; Tick(); return true; }
        public bool Advance(float deltaTime)
        {
            if (Paused) return false;
            elapsed += deltaTime;
            bool changed = false;
            while (elapsed >= Math.Max(.01f, TickSeconds)) { elapsed -= Math.Max(.01f, TickSeconds); Tick(); changed = true; }
            return changed;
        }
        private void Tick() { resolver.Resolve(_ => Prune()); TickCount++; }
        public void Clear() { Board.Replace(new int[Board.Rows, Board.Cols]); Queue.Clear(); TickCount = 0; elapsed = 0; }
    }
}
