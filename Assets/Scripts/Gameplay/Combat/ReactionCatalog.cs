using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Assets.Scripts
{
    public sealed class ReactionPackage
    {
        public int Id { get; }
        public int ElementType => Id / 100 * 100;
        public bool EnablesPlacement => Id % 100 == 0;
        public IReadOnlyList<Reaction> Reactions { get; }
        public ReactionPackage(int id, IEnumerable<Reaction> reactions)
        { Id = id; Reactions = Array.AsReadOnly(reactions.ToArray()); }
    }

    /// <summary>Authoritative authored packages. IDs are persistent; source order breaks combat ties.</summary>
    public static class ReactionCatalog
    {
        public const string Definitions = @"PACKAGE 100 FIRE
R 1000 V Fire_Fire_Cardinal_Spread I (1,0,100) O (1,0,0,0) (1,0,101,51) (1,1,104,50) (1,-1,104,50) (2,0,104,50) D (0,1,2,3) E
R 1001 V Fire_Fire_Diagonal_Spread I (1,1,100) O (1,1,0,0) (2,1,104,54) (1,2,104,54) (0,1,101,55) (1,0,101,55) D (0,1,2,3) E
R 1002 I (0,0,101) O (0,0,102,-1) D (0) E
R 1003 I (0,0,102) O (0,0,103,-1) D (0) E
R 1004 I (0,0,103) O (0,0,104,-1) D (0) E
R 1005 I (0,0,104) O (0,0,105,-1) D (0) E
R 1006 I (0,0,105) O (0,0,106,-1) D (0) E
R 1007 I (0,0,106) O (0,0,107,-1) D (0) E
R 1008 V Fire_Burnout I (0,0,107) O (0,0,0,-1) D (0) E
PACKAGE 101 FIRE
R 1009 V Fire_Water_Cardinal_Geyser I (1,0,200*) O (0,0,0,0) (1,0,210,51) (2,0,210,51) (3,0,210,51) (4,0,210,51) (5,0,210,51) D (0,1,2,3) E
R 1010 V Fire_Electricity_Cardinal_PlasmaFork I (1,0,300*) O (0,0,0,0) (1,0,310,52) (2,1,310,52) (2,-1,310,52) D (0,1,2,3) E
R 1011 V Fire_Stone_Cardinal_LavaFlow I (1,0,400*) O (0,0,0,0) (1,0,410,53) (1,1,410,53) (1,-1,410,53) D (0,1,2,3) E
R 1012 V Fire_Water_Overlap_SteamRing I (0,0,200*) O (0,0,0,0) (2,0,210,55) (1,1,210,55) (0,2,210,55) (-1,1,210,55) (1,-1,210,55) (-2,0,210,55) (-1,-1,210,55) (0,-2,210,55) D (0,1,2,3) E
R 1013 V Fire_Electricity_Overlap_PlasmaCross I (0,0,300*) O (0,0,0,0) (3,0,310,56) (4,0,311,56) (0,3,310,56) (0,4,311,56) (-3,0,310,56) (-4,0,311,56) (0,-3,310,56) (0,-4,311,56) D (0,1,2,3) E
R 1014 V Fire_Stone_Overlap_LavaEruption I (0,0,400*) O (0,0,0,0) (0,0,410,56) D (0,1,2,3) E
PACKAGE 200 WATER
R 2000 V Water_Water_Cardinal_Surge I (1,0,200) O (1,0,0,0) (1,0,201,51) (2,0,201,51) (3,0,201,51) (4,0,201,51) D (0,1,2,3) E
PACKAGE 300 ELECTRICITY
R 3000 V Electricity_Electricity_Cardinal_Chain I (1,0,300) O (1,0,0,0) (2,1,301,56) (3,2,302,55) (4,3,302,55) (2,-1,301,56) (3,-2,302,55) (4,-3,302,55) D (0,1,2,3) E
R 3001 V Electricity_Electricity_Diagonal_Chain I (1,1,300) O (1,1,0,0) (1,2,301,56) (1,3,302,55) (2,1,301,56) (3,1,302,55) D (0,1,2,3) E
R 3002 I (0,0,301) O (0,0,302,-1) D (0) E
R 3003 V Electricity_Discharge I (0,0,302) O (0,0,0,-1) D (0) E
PACKAGE 301 ELECTRICITY
R 3004 V Electricity_Water_Cardinal_Conduction I (1,0,200*) O (0,0,0,0) (1,0,211,51) (2,0,301,53) (3,0,301,53) D (0,1,2,3) E
R 3005 V Electricity_Water_Diagonal_Conduction I (1,1,200*) O (0,0,0,0) (1,1,211,51) (2,2,301,53) (3,3,301,53) D (0,1,2,3) E
PACKAGE 400 STONE";
        public static readonly IReadOnlyList<ReactionPackage> Packages = ParsePackages(Definitions);
        public static bool Contains(int id) => Packages.Any(p => p.Id == id);

        public static IReadOnlyList<ReactionPackage> ParsePackages(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new FormatException("Reaction catalog is empty.");
            var result = new List<ReactionPackage>();
            var packageIds = new HashSet<int>();
            var reactionIds = new HashSet<int>();
            var body = new StringBuilder();
            int packageId = 0;
            void Finish()
            {
                if (packageId == 0) return;
                var parsed = ReactionParser.Parse(body.ToString(), allowEmpty: true, requireIds: true);
                var rules = parsed.Values.SelectMany(r => r).ToArray();
                foreach (int id in rules.Select(r => r.Id).Distinct())
                    if (!reactionIds.Add(id)) throw new FormatException("Duplicate reaction ID: " + id);
                result.Add(new ReactionPackage(packageId, rules));
                body.Clear();
            }
            int lineNumber = 0;
            foreach (string raw in text.Split('\n'))
            {
                lineNumber++;
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#") || line == "START" || line == "END") continue;
                try
                {
                    if (line.StartsWith("PACKAGE ", StringComparison.Ordinal))
                    {
                        Finish();
                        string[] parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length != 3 || !int.TryParse(parts[1], out packageId) ||
                            packageId < 100 || packageId >= 500 || !packageIds.Add(packageId))
                            throw new FormatException("Expected a unique PACKAGE ID ELEMENT (100-499).");
                        string expected = (packageId / 100) switch
                        { 1 => "FIRE", 2 => "WATER", 3 => "ELECTRICITY", 4 => "STONE", _ => "" };
                        if (parts[2] != expected) throw new FormatException("Package element does not match its ID.");
                        body.AppendLine(expected);
                    }
                    else
                    {
                        if (packageId == 0 || !line.StartsWith("R ", StringComparison.Ordinal))
                            throw new FormatException("Expected PACKAGE or R reaction ID.");
                        body.AppendLine(line);
                    }
                }
                catch (FormatException e) { throw new FormatException("Catalog line " + lineNumber + ": " + e.Message, e); }
            }
            Finish();
            if (result.Count == 0) throw new FormatException("No reaction packages were defined.");
            return result.AsReadOnly();
        }
    }

    /// <summary>An immutable loadout snapshot; repeated grants activate a package only once.</summary>
    public sealed class RunLoadout
    {
        private readonly Dictionary<int, IReadOnlyList<Reaction>> byElement = new();
        private readonly HashSet<int> placements = new();
        public IReadOnlyList<int> PackageIds { get; }
        public IReadOnlyList<Reaction> Reactions { get; }
        public int FirstElement => placements.OrderBy(t => t).FirstOrDefault();
        public bool CanPlace(int type) => placements.Contains(type);
        public IReadOnlyList<Reaction> GetReactions(int type) =>
            byElement.TryGetValue(type, out var rules) ? rules : Array.Empty<Reaction>();

        public RunLoadout(IEnumerable<int> ids)
        {
            var selected = new HashSet<int>(ids);
            foreach (int id in selected)
                if (!ReactionCatalog.Contains(id)) throw new ArgumentException("Unknown reaction package: " + id);
            var packages = ReactionCatalog.Packages.Where(p => selected.Contains(p.Id)).ToArray();
            PackageIds = Array.AsReadOnly(packages.Select(p => p.Id).ToArray());
            Reactions = Array.AsReadOnly(packages.SelectMany(p => p.Reactions).ToArray());
            foreach (var group in packages.GroupBy(p => p.ElementType))
                byElement.Add(group.Key, Array.AsReadOnly(group.SelectMany(p => p.Reactions).ToArray()));
            foreach (var package in packages)
                if (package.EnablesPlacement) placements.Add(package.ElementType);
        }
    }
}
