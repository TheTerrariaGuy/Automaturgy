using static CheckSupport;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>GPU readback checks for cell coverage, alpha seams, native sprites and normal sorting.</summary>
public static class ParticlePixelationChecks
{
    private static readonly CheckSupport.Runner runner = new();
    [MenuItem("Tools/Automaturgy/Rendering/Check particle pixelation")]
    public static void Run() => runner.Start(RunChecks(), "Temp/ParticlePixelationChecks");

    private static IEnumerator RunChecks()
    {
        var root = new GameObject("Temporary pixelation checks");
        root.transform.position = new Vector3(1000, 1000, 0);
        var material = new Material(Shader.Find("Grid Mage/Element Particle"));
        var spriteMaterial = new Material(Shader.Find("Sprites/Default"));
        var mesh = new Mesh { name = "Two triangle alpha seam check" };
        mesh.vertices = new[] { new Vector3(-.7f, -.7f), new Vector3(.7f, -.7f), new Vector3(.7f, .7f), new Vector3(-.7f, .7f) };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        // Mesh particles consume packed vertex colors, matching the authored effect meshes.
        mesh.colors32 = Enumerable.Repeat(new Color32(255, 255, 255, 255), 4).ToArray();
        mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        Sprite spriteAsset = null;
        float savedSize = Shader.GetGlobalFloat("_ParticlePixelWorldSize");
        try
        {
            Require(material.shader.isSupported, "Particle shader must be supported by the graphics device.");
            Require(!ShaderUtil.ShaderHasError(material.shader), "Particle shader must compile.");
            var camera = new GameObject("Check camera").AddComponent<Camera>();
            camera.transform.SetParent(root.transform, false);
            camera.transform.localPosition = new Vector3(0, 0, -10);
            camera.enabled = false;
            camera.orthographic = true;
            camera.orthographicSize = 2;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.allowHDR = camera.allowMSAA = false;
            camera.transparencySortMode = TransparencySortMode.CustomAxis;
            camera.transparencySortAxis = Vector3.up;
            var settings = camera.gameObject.AddComponent<ParticlePixelation>();

            var particles = new GameObject("Check particle").AddComponent<ParticleSystem>();
            particles.transform.SetParent(root.transform, false);
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.startSpeed = 0;
            main.startSize = 1;
            main.startLifetime = 100;
            main.startColor = new Color(1, 0, 0, .5f);
            var emission = particles.emission; emission.enabled = false;
            var shape = particles.shape; shape.enabled = false;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.alignment = ParticleSystemRenderSpace.Local;
            renderer.mesh = mesh;
            renderer.sharedMaterial = material;
            var particleGroup = WorldSorting.CreateGroup(root.transform, new Vector3(0, -.5f, 0), "Particle anchor");
            WorldSorting.Include(particleGroup, renderer, 0);
            particles.Emit(1);
            particles.Simulate(.01f, false, false);
            particles.Pause();

            var sprite = new GameObject("Native sprite").AddComponent<SpriteRenderer>();
            spriteAsset = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), Vector2.one * .5f, 1);
            sprite.sprite = spriteAsset;
            sprite.sharedMaterial = spriteMaterial;
            sprite.color = Color.blue;
            sprite.transform.position = root.transform.position;
            var spriteGroup = WorldSorting.CreateGroup(root.transform, new Vector3(0, .5f, 0), "Sprite anchor");
            WorldSorting.Include(spriteGroup, sprite, 0);
            sprite.enabled = false;
            SortingGroup.UpdateAllSortingGroups();
            yield return null; // Let Unity register new renderers and sorting groups before culling.

            string output = Path.GetFullPath("Temp/ParticlePixelationChecks");
            Directory.CreateDirectory(output);
            int cases = 0;
            foreach (var resolution in new[] { new Vector2Int(240, 180), new Vector2Int(480, 360), new Vector2Int(319, 241) })
            foreach (int size in new[] { 1, 4, 6, 9 })
            foreach (float zoom in new[] { 2f, 1.7f })
            foreach (float rotation in new[] { 0f, 27f })
            {
                settings.PixelSize = size;
                camera.orthographicSize = zoom;
                camera.transform.localPosition = new Vector3(rotation == 0 ? 0 : .073f, .031f, -10);
                particles.transform.rotation = Quaternion.Euler(0, 0, rotation);
                particles.Simulate(0, false, false);
                yield return null;
                var image = CheckSupport.RenderCamera(camera, resolution.x, resolution.y);
                try
                {
                    float screenCellSize = Mathf.Max(1f, size / 16f * resolution.y / (2f * zoom));
                    Require(CountMixedCells(image, screenCellSize) == 0, $"Partial game-pixel cells at {resolution}, size {size}, zoom {zoom}, rotation {rotation}.");
                    var filled = image.GetPixels32().Where(p => p.r > 10).ToArray();
                    Require(filled.Length > 100, $"Particle must be visible: {filled.Length} pixels, {particles.particleCount} particles, bounds {renderer.bounds}.");
                    Require(filled.Max(p => p.r) - filled.Min(p => p.r) <= 2, "Shared triangle edges must not double-blend alpha.");
                    Require(image.GetPixel(image.width / 2, image.height / 2).r > .1f, "Internal diagonal must have no holes.");
                    if (size == 6 && rotation == 27 && resolution.x == 319 && zoom == 1.7f)
                        File.WriteAllBytes(Path.Combine(output, "Particle6px.png"), image.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(image); }
                cases++;
            }

            settings.PixelSize = 6;
            settings.enabled = false;
            yield return null;
            var unpixelated = CheckSupport.RenderCamera(camera, 319, 241);
            float cellSize = 6f / 16f * 241 / (2f * camera.orthographicSize);
            try { Require(CountMixedCells(unpixelated, cellSize) > 0, "Disabling pixelation must restore native particle edges."); }
            finally { Object.DestroyImmediate(unpixelated); settings.enabled = true; }
            renderer.enabled = false;
            sprite.enabled = true;
            sprite.transform.rotation = Quaternion.Euler(0, 0, 27);
            yield return null;
            var native = CheckSupport.RenderCamera(camera, 319, 241);
            try
            {
                Require(CountMixedCells(native, cellSize) > 0, "Sprite edges must retain native screen resolution.");
                File.WriteAllBytes(Path.Combine(output, "NativeSprite.png"), native.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(native); }

            renderer.enabled = true;
            foreach (bool particleInFront in new[] { true, false })
            {
                Vector3 particlePosition = particles.transform.position, spritePosition = sprite.transform.position;
                particleGroup.transform.localPosition = new Vector3(0, particleInFront ? -.5f : .5f, 0);
                spriteGroup.transform.localPosition = new Vector3(0, particleInFront ? .5f : -.5f, 0);
                particles.transform.position = particlePosition;
                sprite.transform.position = spritePosition;
                SortingGroup.UpdateAllSortingGroups();
                yield return null;
                var image = CheckSupport.RenderCamera(camera, 319, 241);
                try
                {
                    Color center = image.GetPixel(image.width / 2, image.height / 2);
                    Require(particleInFront ? center.r > .1f && center.b > .1f : center.r < .02f && center.b > .9f,
                        "Normal sprite/particle sorting failed, particleInFront=" + particleInFront + ", pixel=" + center);
                    File.WriteAllBytes(Path.Combine(output, particleInFront ? "ParticleInFront.png" : "SpriteInFront.png"), image.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(image); }
            }
            File.WriteAllText(Path.Combine(output, "Results.txt"), $"PASS: {cases} game-pixel grid/alpha cases across resolutions and zooms; disabled pixelation; native sprite edges; sprite/particle Y sorting in both orders.\n");
            Debug.Log("PARTICLE PIXELATION CHECKS PASSED: " + output);
        }
        finally
        {
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(material);
            Object.DestroyImmediate(spriteMaterial);
            Object.DestroyImmediate(mesh);
            if (spriteAsset != null) Object.DestroyImmediate(spriteAsset);
            Shader.SetGlobalFloat("_ParticlePixelWorldSize", savedSize);
        }
    }

    private static int CountMixedCells(Texture2D image, float size)
    {
        var pixels = image.GetPixels32();
        int mixed = 0;
        // URP's final resolve flips its intermediate D3D target into texture orientation.
        for (int cy = 0; cy <= Mathf.FloorToInt((image.height - .5f) / size); cy++)
        for (int cx = 0; cx <= Mathf.FloorToInt((image.width - .5f) / size); cx++)
        {
            // Group raster pixel centers by their fractional game-pixel cell.
            int x = Mathf.Max(0, Mathf.CeilToInt(cx * size - .5f));
            int y = Mathf.Max(0, Mathf.CeilToInt(cy * size - .5f));
            int endX = Mathf.Min(image.width, Mathf.CeilToInt((cx + 1) * size - .5f));
            int endY = Mathf.Min(image.height, Mathf.CeilToInt((cy + 1) * size - .5f));
            Color32 reference = pixels[y * image.width + x];
            bool differs = false;
            for (int py = y; py < endY; py++)
            for (int px = x; px < endX; px++)
            {
                Color32 actual = pixels[py * image.width + px];
                differs |= Math.Abs(actual.r - reference.r) > 2 || Math.Abs(actual.b - reference.b) > 2;
            }
            if (differs) mixed++;
        }
        return mixed;
    }

}
