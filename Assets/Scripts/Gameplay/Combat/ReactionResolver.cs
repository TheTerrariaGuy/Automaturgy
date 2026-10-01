using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts
{
    public enum ResolutionPhase { Fading, Reactions, Overlap }

    /// <summary>Snapshot-based combat. Later priority/insertion writes win, including visual ownership.</summary>
    public sealed class ReactionResolver
    {
        private readonly BoardState board;
        private readonly Func<int, IReadOnlyList<Reaction>> reactions;
        private readonly List<Change> changes = new();
        private readonly List<ReactionVisual> visuals = new();
        private readonly Dictionary<Vector2Int, ReactionVisual> visualOwners = new();
        private readonly int[] captures = new int[ReactionParser.MaxVariables];
        private readonly int[] captureCounts = new int[ReactionParser.MaxVariables];
        private readonly HashSet<Offset> outputSeen = new();
        public ReactionResolver(BoardState board, Func<int, IReadOnlyList<Reaction>> reactions)
        { this.board = board; this.reactions = reactions; }

        public IReadOnlyList<ReactionVisual> Resolve(Action<ResolutionPhase> phaseCompleted = null)
        {
            visuals.Clear();
            visualOwners.Clear();
            board.Replace(HandleFading(board.Cells));
            phaseCompleted?.Invoke(ResolutionPhase.Fading);
            // Each phase clones its input. This immutable reference is the overlap snapshot.
            int[,] beforeReactions = board.Cells;
            board.Replace(ResolveReactions(board.Cells));
            phaseCompleted?.Invoke(ResolutionPhase.Reactions);
            board.Replace(ResolveReactions(board.Cells, beforeReactions));
            phaseCompleted?.Invoke(ResolutionPhase.Overlap);
            return visuals;
        }

        private int[,] HandleFading(int[,] before)
        {
            int[,] next = (int[,])before.Clone();
            for (int r = 0; r < board.Rows; r++) for (int c = 0; c < board.Cols; c++)
                if (board.HasCell(r, c) && ElementState.IsSpentEffect(before[r, c])) next[r, c] = 0;
            for (int r = 0; r < board.Rows; r++) for (int c = 0; c < board.Cols; c++)
            {
                int type = before[r, c];
                if (!board.AllowsSpells(r, c) || !ElementState.IsFading(type)) continue;
                var visual = NewVisual(ElementDefinitions.DecayEffect(type), r, c);
                foreach (var delta in GridMath.Cardinal)
                {
                    int toR = r + delta.row, toC = c + delta.col;
                    if (!board.AllowsSpells(toR, toC) || !board.ElevationLine(r, c, toR, toC)) continue;
                    int target = before[toR, toC];
                    if (ElementState.Stage(target) > 10 || target < 100 ||
                        (type == 210 && ElementState.Family(target) == 1) ||
                        (type == 310 && ElementState.Family(target) == 2))
                    { next[toR, toC] = type + 1; TrackVisual(toR, toC, visual); }
                }
                next[r, c] = 0;
                TrackVisual(r, c, visual);
            }
            return next;
        }

        private int[,] ResolveReactions(int[,] input, int[,] before = null)
        {
            int[,] next = (int[,])input.Clone();
            changes.Clear();
            int insertionOrder = 0;
            for (int r = 0; r < board.Rows; r++) for (int c = 0; c < board.Cols; c++)
            {
                if (!board.AllowsSpells(r, c)) continue;
                int type = input[r, c];
                int family;
                if (before == null)
                {
                    if (!ElementState.IsReactive(type)) continue;
                    family = ElementState.BaseType(type);
                }
                else
                {
                    if (ElementState.Stage(type) >= 10 || ElementState.Stage(before[r, c]) >= 10 ||
                        ElementState.Family(type) == ElementState.Family(before[r, c])) continue;
                    family = ElementState.BaseType(before[r, c]);
                }
                foreach (var reaction in reactions(family))
                {
                    Array.Clear(captureCounts, 0, captureCounts.Length);
                    if (before == null ? CheckReq(input, reaction.Requirements, r, c) : IsOriginReaction(reaction, type))
                    {
                        for (int variable = 0; variable < reaction.VariableCount; variable++)
                            if (captureCounts[variable] > 1)
                                Debug.LogWarning($"Reaction {reaction.Id} at ({c},{r}), direction {reaction.Direction}: variable {(char)('a' + variable)} matched {captureCounts[variable]} detections; using last detected value {captures[variable]}.");
                        QueueChanges(changes, reaction, r, c, ref insertionOrder);
                    }
                }
            }
            ApplyQueuedChanges(changes, next);
            return next;
        }

        private bool CheckReq(int[,] g, IReadOnlyCollection<Requirement> req, int r, int c) 
        {
            foreach (Requirement requirement in req)
            {
                int targetRow = r + requirement.y;
                int targetCol = c + requirement.x;

                if (!board.HasCell(targetRow, targetCol) ||
                    !board.ElevationLine(r, c, targetRow, targetCol) ||
                    !requirement.Matches(g[targetRow, targetCol]))
                {
                    return false;
                } 
                Capture(requirement, g[targetRow, targetCol]);
            }
            return true;
        }

        private bool IsOriginReaction(Reaction reaction, int type)
        {
            if (reaction.Requirements.Count != 1)
            {
                return false;
            }

            foreach (Requirement requirement in reaction.Requirements)
            {
                if (requirement.x != 0 || requirement.y != 0 || !requirement.Matches(type)) return false;
                Capture(requirement, type);
                return true;
            }

            return false;
        }

        private void Capture(Requirement requirement, int type)
        {
            Set(requirement.XVariable, requirement.CaptureX);
            Set(requirement.YVariable, requirement.CaptureY);
            Set(requirement.TypeVariable, type);
            void Set(int variable, int value)
            {
                if (variable < 0) return;
                captures[variable] = value;
                captureCounts[variable]++;
            }
        }

        private void QueueChanges(
            List<Change> changeQueue,
            Reaction reaction,
            int originRow,
            int originCol,
            ref int insertionOrder)
        {
            var visual = NewVisual(reaction.Effect, originRow, originCol, reaction.Direction);
            outputSeen.Clear();
            foreach (var mapping in reaction.OutputMappings)
            foreach (Offset output in mapping.Resolve(captures))
            {
                if (!outputSeen.Add(output)) continue;
                int targetRow = originRow + output.y;
                int targetCol = originCol + output.x;

                if (!board.AllowsSpells(targetRow, targetCol))
                {
                    continue;
                }

                changeQueue.Add(new Change(
                    targetRow,
                    targetCol,
                    output.type,
                    output.priority,
                    insertionOrder++,
                    originRow,
                    originCol, visual));
            }
        }

        private void ApplyQueuedChanges(List<Change> changeQueue, int[,] grid)
        {
            int[,] walls = board.SightWalls(grid);
            changeQueue.Sort();
            foreach (Change change in changeQueue)
            {
                if (!board.AllowsSpells(change.Row, change.Col)) continue;
                if (!board.ElevationLine(change.SourceRow, change.SourceCol, change.Row, change.Col)) continue;
                if (!GridMath.HasLineOfSight(walls,
                    change.SourceRow, change.SourceCol, change.Row, change.Col, change.Type == 410)) continue;
                grid[change.Row, change.Col] = change.Type;
                TrackVisual(change.Row, change.Col, change.Visual);
            }
        }

        private ReactionVisual NewVisual(string id, int row, int col, int direction = 0)
        {
            if (id == null) return null;
            var visual = new ReactionVisual(id, row, col, direction);
            visuals.Add(visual);
            return visual;
        }

        private void TrackVisual(int row, int col, ReactionVisual visual)
        {
            var cell = new Vector2Int(col, row);
            if (visualOwners.Remove(cell, out var previous)) previous.cells.Remove(cell);
            if (visual == null) return;
            visualOwners[cell] = visual;
            visual.cells.Add(cell);
        }

        private sealed class Change : IComparable<Change>
        {
            public int Row { get; }
            public int Col { get; }
            public int SourceRow { get; }
            public int SourceCol { get; }
            public int Type { get; }
            public int Priority { get; }
            public ReactionVisual Visual { get; }
            private int InsertionOrder { get; }

            public Change(int row, int col, int type, int priority, int insertionOrder, int sourceRow, int sourceCol,
                ReactionVisual visual = null)
            {
                Row = row;
                Col = col;
                SourceRow = sourceRow;
                SourceCol = sourceCol;
                Type = type;
                Priority = priority;
                InsertionOrder = insertionOrder;
                Visual = visual;
            }

            public int CompareTo(Change other)
            {
                if (other == null)
                {
                    return 1;
                }

                int priorityComparison = Priority.CompareTo(other.Priority);
                return priorityComparison != 0
                    ? priorityComparison
                    : InsertionOrder.CompareTo(other.InsertionOrder); // ultra deterministic
            }
        }
    }
}
