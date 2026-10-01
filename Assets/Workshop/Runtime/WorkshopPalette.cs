using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GridMage.Workshop
{
    public static class WorkshopPalette
    {
        public static readonly Color Empty = new Color32(27, 33, 45, 255);
        public static readonly IReadOnlyDictionary<int, Color32> Colors = Build();
        public static IEnumerable<int> Domain => Colors.Keys.Concat(new[] { 0 });
        public static Color ColorFor(int id) => Colors.TryGetValue(id, out var color) ? color : Empty;
        public static Color Ink(Color background) => background.grayscale > .53f ? new Color32(12, 17, 26, 255) : Color.white;

        private static IReadOnlyDictionary<int, Color32> Build()
        {
            int[][] rows = {
                new[] {120,12,10, 205,24,18, 218,48,38, 224,69,58, 229,91,80, 232,113,103, 234,137,129, 235,162,155, 255,128,20, 255,45,105, 255,170,115, 255,207,174},
                new[] {5,45,125, 15,92,220, 36,108,224, 57,122,225, 79,138,226, 101,153,226, 125,168,225, 151,184,224, 0,205,215, 45,60,210, 238,145,120, 248,193,178},
                new[] {70,10,125, 135,25,220, 148,46,222, 158,67,222, 169,88,222, 180,110,221, 191,133,220, 202,158,220, 245,225,25, 225,40,225, 255,162,205, 255,205,229},
                new[] {55,58,62, 88,92,98, 102,106,112, 116,120,126, 131,134,140, 146,149,154, 162,164,169, 180,181,185, 125,92,55, 72,87,105, 220,55,12, 255,135,28},
                new[] {10,95,48, 18,180,78, 39,190,94, 61,197,110, 83,202,126, 106,206,143, 130,209,160, 155,211,179, 25,215,190, 175,220,35, 255,193,132, 255,220,178},
                new[] {175,135,10, 245,205,35, 247,212,58, 248,219,82, 249,225,107, 249,231,132, 249,236,158, 249,240,184, 255,165,35, 255,245,150, 255,172,125, 255,215,182},
                new[] {18,5,45, 35,10,82, 43,15,88, 49,20,93, 55,25,98, 61,30,103, 67,35,108, 73,40,113, 8,48,105, 105,8,75, 112,35,30, 125,52,42}
            };
            var colors = new Dictionary<int, Color32>();
            for (int f = 0; f < rows.Length; f++)
                for (int s = 0; s < 12; s++)
                    colors[(f + 1) * 100 + s] = new Color32((byte)rows[f][s * 3], (byte)rows[f][s * 3 + 1], (byte)rows[f][s * 3 + 2], 255);
            return colors;
        }
    }
}
