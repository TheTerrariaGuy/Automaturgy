using System;
using System.Collections.Generic;
using System.Linq;

namespace Assets.Scripts
{
    /// <summary>Compiled once at startup; loadouts select immutable rules by individual reaction ID.</summary>
    public static class ReactionCatalog
    {
        public const string DefaultResourcePath = "Spells";
        private static IReadOnlyList<Reaction> reactions;
        private static IReadOnlyList<int> reactionIds;
        private static HashSet<int> ids;
        private static UnityEngine.TextAsset source;
        public static bool IsInitialized => reactions != null;
        public static int CompilationCount { get; private set; }
        public static IReadOnlyList<Reaction> Reactions { get { EnsureInitialized(); return reactions; } }
        public static IReadOnlyList<int> ReactionIds { get { EnsureInitialized(); return reactionIds; } }
        public static bool Contains(int id) { EnsureInitialized(); return ids.Contains(id); }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        { reactions = null; reactionIds = null; ids = null; source = null; CompilationCount = 0; }

        public static void Initialize(UnityEngine.TextAsset spellSource)
        {
            if (spellSource == null) throw new ArgumentNullException(nameof(spellSource), "Assign a spell text asset to Bootstrap.");
            if (IsInitialized)
            {
                if (source != spellSource) throw new InvalidOperationException("A different spell source is already compiled for this session.");
                return;
            }
            // Publish only a complete, validated compilation.
            var compiled = ReactionParser.Parse(spellSource.text, requireIds: true, sourceName: spellSource.name + ".txt")
                .Values.SelectMany(r => r).ToArray();
            var compiledIds = compiled.Select(r => r.Id).Distinct().ToArray();
            reactionIds = Array.AsReadOnly(compiledIds);
            ids = new HashSet<int>(compiledIds);
            source = spellSource;
            reactions = Array.AsReadOnly(compiled);
            CompilationCount++;
        }

        // Supports validation and opening Inventory/gameplay directly in the Editor.
        public static void EnsureInitialized()
        {
            if (!IsInitialized) Initialize(UnityEngine.Resources.Load<UnityEngine.TextAsset>(DefaultResourcePath));
        }
    }
    /// <summary>An immutable snapshot of individual reaction grants and element placement permissions.</summary>
    public sealed class RunLoadout
    {
        private readonly Dictionary<int, IReadOnlyList<Reaction>> byElement = new();
        private readonly HashSet<int> placements;
        public IReadOnlyList<int> ReactionIds { get; }
        public IReadOnlyList<int> PlacementElements { get; }
        public IReadOnlyList<Reaction> Reactions { get; }
        public int FirstElement => PlacementElements.FirstOrDefault();
        public bool CanPlace(int type) => placements.Contains(type);
        public static bool IsPlacementElement(int type) => type >= 100 && type <= 400 && type % 100 == 0;
        public IReadOnlyList<Reaction> GetReactions(int type) =>
            byElement.TryGetValue(type, out var rules) ? rules : Array.Empty<Reaction>();

        public RunLoadout(IEnumerable<int> reactionIds, IEnumerable<int> placementElements = null)
        {
            var selected = new HashSet<int>(reactionIds ?? throw new ArgumentNullException(nameof(reactionIds)));
            foreach (int id in selected)
                if (!ReactionCatalog.Contains(id)) throw new ArgumentException("Unknown reaction: " + id);
            placements = new HashSet<int>(placementElements ?? Array.Empty<int>());
            foreach (int type in placements)
                if (!IsPlacementElement(type)) throw new ArgumentException("Invalid placement element: " + type);
            PlacementElements = Array.AsReadOnly(placements.OrderBy(type => type).ToArray());
            // Filter the catalog rather than iterating grants so item layout and duplicates cannot change priority.
            Reactions = Array.AsReadOnly(ReactionCatalog.Reactions.Where(r => selected.Contains(r.Id)).ToArray());
            ReactionIds = Array.AsReadOnly(Reactions.Select(r => r.Id).Distinct().ToArray());
            foreach (var group in Reactions.GroupBy(r => r.Id / 1000 * 100))
                byElement.Add(group.Key, Array.AsReadOnly(group.ToArray()));
        }
    }
}
