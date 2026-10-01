using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Assets.Scripts
{
    /// <summary>Stable gameplay IDs and shared stage properties. Artistic assets live in presentation catalogs.</summary>
    public static class ElementDefinitions
    {
        public readonly struct Stage
        {
            public readonly byte Alpha;
            public readonly float Damage, ManaCost;
            public readonly string DecayEffect;
            public readonly int ReactionPriority;
            public Stage(byte alpha, float damage = 0, float manaCost = 0, string decayEffect = null, int reactionPriority = 50)
            { Alpha = alpha; Damage = damage; ManaCost = manaCost; DecayEffect = decayEffect; ReactionPriority = reactionPriority; }
        }
        public static readonly IReadOnlyDictionary<int, Stage> Stages = Build();
        private static IReadOnlyDictionary<int, Stage> Build()
        {
            var stages = new Dictionary<int, Stage> { [0] = new Stage(255, reactionPriority: 0) };
            byte[] fadeAlpha = { 255, 240, 227, 198, 170, 142, 113, 85 };
            int[] firePriorities = { 50, 51, -1, -1, 50, -1, -1, -1 };
            for (int i = 0; i < fadeAlpha.Length; i++)
            {
                stages[100 + i] = new Stage(fadeAlpha[i], 10, i == 0 ? 8 : 0, reactionPriority: firePriorities[i]);
                stages[200 + i] = new Stage(fadeAlpha[i], i < 2 ? 4 : 0, i == 0 ? 6 : 0, reactionPriority: i == 1 ? 51 : 50);
            }
            stages[110] = new Stage(255, 0, 0, "Fire_Flare_Decay");
            stages[111] = new Stage(170);
            stages[210] = new Stage(180, 16, 0, "Water_Steam_Decay", reactionPriority: 51);
            stages[211] = new Stage(120, 8, reactionPriority: 51);
            stages[300] = new Stage(255, 1, 10);
            stages[301] = new Stage(240, 1, reactionPriority: 56);
            stages[302] = new Stage(200, 1, reactionPriority: 55);
            stages[310] = new Stage(255, 4, 0, "Electricity_Charge_Decay", reactionPriority: 52);
            stages[311] = new Stage(170, 2, reactionPriority: 56);
            stages[400] = new Stage(255, 0, 2);
            stages[401] = new Stage(170);
            stages[410] = new Stage(255, 20, 0, "Lava_Cooling_Decay", reactionPriority: 53);
            stages[411] = new Stage(170, 16);
            return new ReadOnlyDictionary<int, Stage>(stages);
        }
        public static byte Alpha(int type) => Stages.TryGetValue(type, out var stage) ? stage.Alpha : (byte)255;
        public static float Damage(int type) => Stages.TryGetValue(type, out var stage) ? stage.Damage : 0;
        public static bool TryManaCost(int type, out float cost)
        {
            cost = Stages.TryGetValue(type, out var stage) ? stage.ManaCost : 0;
            return cost > 0;
        }
        public static string DecayEffect(int type) => Stages.TryGetValue(type, out var stage) ? stage.DecayEffect : null;
        public static int ReactionPriority(int type) => Stages.TryGetValue(type, out var stage)
            ? stage.ReactionPriority : throw new ArgumentException("Unknown output tile type: " + type);
    }
}
