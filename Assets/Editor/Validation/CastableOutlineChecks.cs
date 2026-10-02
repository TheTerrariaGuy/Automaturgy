using static CheckSupport;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using Assets.Scripts;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class CastableOutlineChecks
{
    private const string Output = "Temp/CastableOutlineChecks";
    private static readonly CheckSupport.Runner runner = new();
    [MenuItem("Tools/Automaturgy/Rendering/Check castable outline")]
    public static void Run() => runner.Start(RunChecks(), "Temp/CastableOutlineChecks");

    private static IEnumerator RunChecks()
    {
        CheckGeometry();
        var rendering = CheckRendering();
        try { while (rendering.MoveNext()) yield return rendering.Current; }
        finally { (rendering as IDisposable)?.Dispose(); }
    }

    private static void CheckGeometry()
    {
        var mesh = new Mesh();
        try
        {
            for (int mask = 0; mask < 512; mask++)
            {
                var grid = new bool[3, 3];
                for (int i = 0; i < 9; i++) grid[i / 3, i % 3] = (mask & (1 << i)) != 0;
                CastableOutline.BuildMesh(mesh, grid, 1, 0, Color.white, .0625f, .25f, 1);
                var vertices = mesh.vertices;
                var triangles = mesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector2 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                    Require(Cross(b - a, c - a) > 0, "Folded or degenerate triangle, mask " + mask);
                    Vector2 center = (a + b + c) / 3;
                    int row = Mathf.FloorToInt(.5f - center.y), col = Mathf.FloorToInt(center.x + .5f);
                    Require(row >= 0 && row < 3 && col >= 0 && col < 3 && grid[row, col], "Band outside castable cells, mask " + mask);
                }
                // Rectangle, concavity, hole, disconnected islands, diagonal touch, single-cell corridors.
                if (new[] { 511, 15, 495, 257, 17, 186 }.Contains(mask))
                    for (int y = 0; y < 39; y++)
                        for (int x = 0; x < 39; x++)
                        {
                            Vector2 p = new Vector2(-.5f + (x + .371f) / 13, .5f - (y + .619f) / 13);
                            int coverage = 0;
                            for (int i = 0; i < triangles.Length; i += 3)
                            {
                                Vector2 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                                if (Cross(b - a, p - a) > 0 && Cross(c - b, p - b) > 0 && Cross(a - c, p - c) > 0) coverage++;
                            }
                            float distance = float.PositiveInfinity;
                            for (int row = -1; row <= 3; row++)
                                for (int col = -1; col <= 3; col++)
                                {
                                    if (row >= 0 && row < 3 && col >= 0 && col < 3 && grid[row, col]) continue;
                                    float dx = Mathf.Max(0, Mathf.Abs(p.x - col) - .5f);
                                    float dy = Mathf.Max(0, Mathf.Abs(p.y + row) - .5f);
                                    distance = Mathf.Min(distance, Mathf.Max(dx, dy));
                                }
                            Require(coverage == (distance > 0 && distance < .3125f ? 1 : 0), "Gap or overlap, mask " + mask + " at " + p);
                        }
            }
            CastableOutline.BuildMesh(mesh, new[,] { { true } }, 2, 7, Color.white, 1, 1, 0);
            Require(mesh.bounds.min == new Vector3(6, 6, 0) && mesh.bounds.max == new Vector3(8, 8, 0), "Spacing and offset alignment.");
            CastableOutline.BuildMesh(mesh, null, 1, 0, Color.white, .1f, .2f, 1);
            Require(mesh.vertexCount == 0, "Null grid must clear old geometry.");
        }
        finally { Object.DestroyImmediate(mesh); }
    }

    private static IEnumerator CheckRendering()
    {
        var root = new GameObject("Temporary outline checks");
        root.transform.position = new Vector3(1000, 1000, 0);
        var material = new Material(Shader.Find("Grid Mage/Element Particle"));
        var spriteMaterial = new Material(Shader.Find("Sprites/Default"));
        Sprite sprite = null;
        float savedPixelSize = Shader.GetGlobalFloat("_ParticlePixelWorldSize");
        Mesh mesh = null;
        try
        {
            var outline = new GameObject("Outline").AddComponent<CastableOutline>();
            outline.transform.SetParent(root.transform, false);
            var data = new SerializedObject(outline);
            data.FindProperty("highlightColor").colorValue = Color.red;
            data.ApplyModifiedPropertiesWithoutUndo();
            var grid = new bool[3, 3];
            for (int r = 0; r < 3; r++) for (int c = 0; c < 3; c++) grid[r, c] = true;
            outline.Rebuild(grid, 1, 0);
            mesh = outline.GetComponent<MeshFilter>().sharedMesh;
            var renderer = outline.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.sortingLayerName = WorldSorting.Foreground;
            renderer.sortingOrder = -1;
            outline.enabled = false;
            Require(!renderer.enabled, "Disabled outline must hide.");
            outline.enabled = true;
            Require(renderer.enabled && outline.GetComponent<MeshFilter>().sharedMesh == mesh, "Enable must reuse and restore mesh.");
            outline.Rebuild(new bool[3, 3], 1, 0);
            Require(!renderer.enabled && mesh.vertexCount == 0, "Empty grid must hide.");
            outline.Rebuild(grid, 1, 0);
            var camera = new GameObject("Check camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform, false);
            camera.transform.localPosition = new Vector3(1, -1, -10);
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 2;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowHDR = camera.allowMSAA = false;
            var settings = camera.gameObject.AddComponent<ParticlePixelation>();
            settings.enabled = false;
            yield return null;
            var image = RenderSample(camera, "Fade");
            try
            {
                float border = image.GetPixel(42, 160).r, near = image.GetPixel(50, 160).r, far = image.GetPixel(60, 160).r;
                Require(border > near && near > far && far > .001f, "Highlight must fade inward.");
                Require(image.GetPixel(160, 160).r < .01f && image.GetPixel(39, 160).r < .01f, "Center and exterior must stay clear.");
                Require(image.GetPixels().Max(p => p.r) <= border + .015f, "Joined corners and triangle edges must not double-blend.");
            }
            finally { Object.DestroyImmediate(image); }
            settings.enabled = true;
            settings.PixelSize = 4;
            yield return null;
            image = RenderSample(camera, "Pixelated");
            try
            {
                Require(image.GetPixels().Any(p => p.r > .1f), "Pixelated outline must remain visible.");
                for (int y = 0; y < 320; y += 20)
                    for (int x = 0; x < 320; x += 20)
                        for (int dy = 0; dy < 20; dy++)
                            for (int dx = 0; dx < 20; dx++)
                                Require(Mathf.Abs(image.GetPixel(x, y).r - image.GetPixel(x + dx, y + dy).r) < .01f, "Pixel cell must have uniform alpha, including mesh seams.");
            }
            finally { Object.DestroyImmediate(image); }
            var overlay = new GameObject("Indicator").AddComponent<SpriteRenderer>();
            overlay.transform.SetParent(root.transform, false);
            overlay.transform.localPosition = new Vector3(1, -1, 0);
            overlay.transform.localScale = Vector3.one * 4;
            sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), Vector2.one * .5f, Texture2D.whiteTexture.width);
            overlay.sprite = sprite;
            overlay.sharedMaterial = spriteMaterial;
            overlay.color = Color.blue;
            overlay.sortingLayerName = WorldSorting.Foreground;
            foreach (int order in new[] { -2, 0, 1 })
            {
                overlay.sortingOrder = order;
                yield return null;
                image = RenderSample(camera, "Sorting" + order);
                try
                {
                    Color pixel = image.GetPixel(50, 160);
                    Require(order < -1 ? pixel.r > .1f && pixel.b > .1f : pixel.r < .01f && pixel.b > .9f, "Outline must be behind queue/hover and above lower orders.");
                }
                finally { Object.DestroyImmediate(image); }
            }
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(spriteMaterial);
            if (sprite != null) Object.DestroyImmediate(sprite);
            Shader.SetGlobalFloat("_ParticlePixelWorldSize", savedPixelSize);
        }
        Require(mesh == null, "Destroying the component must dispose its mesh.");
    }

    // Run in a fresh Play session, then stop Play to discard the test board.
    public static string CheckLifecycle()
    {
        Require(Application.isPlaying, "Enter Play mode first.");
        var game = GameLogic.INSTANCE;
        var player = PlayerHandler.INSTANCE;
        var outline = (CastableOutline)new SerializedObject(game).FindProperty("castableOutline").objectReferenceValue;
        Require(outline != null && outline.gameObject.scene == game.gameObject.scene, "Scene outline must be connected.");
        var mesh = outline.GetComponent<MeshFilter>().sharedMesh;
        Require(mesh.vertexCount > 0, "Player initialization must build the outline.");
        var speed = typeof(PlayerHandler).GetField("blinkSpeed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        float savedSpeed = (float)speed.GetValue(player), savedTimeScale = Time.timeScale;
        try
        {
            Time.timeScale = 0;
            player.r = player.c = 5;
            game.InitializeGrid();
            player.transform.position = game.tilesGrid[5, 5].transform.position;
            Require(outline.GetComponent<MeshFilter>().sharedMesh == mesh && mesh.vertexCount > 0, "Grid reset must reuse the mesh.");
            CheckPlayerAlignment();
            Vector3 center = outline.transform.TransformPoint(mesh.bounds.center);
            speed.SetValue(player, 0f);
            Require(player.BlinkTo(game.tilesGrid[5, 6]), "Test blink must succeed.");
            Vector3 movement = outline.transform.TransformPoint(mesh.bounds.center) - center;
            Require(Vector3.Distance(movement, game.gridParent.TransformVector(Vector3.right * game.spacing)) < .001f,
                "Blink arrival must move the outline by one world-space cell.");
            CheckPlayerAlignment();
            player.r = -1;
            game.MakeCastable();
            Require(mesh.vertexCount == 0 && !outline.GetComponent<MeshRenderer>().enabled, "Invalid player must clear the outline.");
            player.r = 5;
            game.MakeCastable();
            Require(mesh.vertexCount > 0, "Valid player must restore the outline.");
        }
        finally { speed.SetValue(player, savedSpeed); Time.timeScale = savedTimeScale; }
        return "PASS: scene reference, player init, grid reset, post-Blink rebuild, clearing and mesh reuse.";
    }

    public static void CheckPlayerAlignment()
    {
        var game = GameLogic.INSTANCE;
        var player = PlayerHandler.INSTANCE;
        var data = new SerializedObject(game);
        var expected = new Mesh();
        try
        {
            foreach (string field in new[] { "castableOutline", "moveableOutline" })
            {
                var outline = (CastableOutline)data.FindProperty(field).objectReferenceValue;
                Require(outline.transform.parent == player.transform, "Both range meshes must be children of Player.");
                var settings = new SerializedObject(outline);
                CastableOutline.BuildMesh(expected, field == "castableOutline" ? game.castableGrid : game.moveableGrid,
                    game.spacing, game.offset, Color.white, settings.FindProperty("borderWidth").floatValue,
                    settings.FindProperty("fadeWidth").floatValue, settings.FindProperty("falloff").floatValue,
                    settings.FindProperty("inset").floatValue);
                var actual = outline.GetComponent<MeshFilter>().sharedMesh.vertices;
                var boardVertices = expected.vertices;
                Require(actual.Length == boardVertices.Length, "Range geometry must preserve the board boundary.");
                for (int i = 0; i < actual.Length; i++)
                    Require(Vector3.Distance(outline.transform.TransformPoint(actual[i]),
                        game.gridParent.TransformPoint(boardVertices[i] + game.GridTranslation)) < .001f,
                        "Player-local range vertices must align with board cells at rest.");
            }
        }
        finally { Object.DestroyImmediate(expected); }
    }

    private static Texture2D RenderSample(Camera camera, string name)
    {
        var image = CheckSupport.RenderCamera(camera, 320, 320);
        File.WriteAllBytes(Output + "/" + name + ".png", image.EncodeToPNG());
        return image;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
}
