using static CheckSupport;
using System;
using System.IO;
using System.Linq;
using Assets.Scripts;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Checks production rendering in a temporary rectangular scene, restoring the original scene afterward.</summary>
[InitializeOnLoad]
public static class WorldRenderingChecks
{
    private const string Running = "GridMage.WorldRenderingChecks";
    private const string PendingPlay = "GridMage.WorldRenderingChecks.PendingPlay";
    private static int phase, nextFrame;
    private static double started;
    private static Transform redAnchor, blueAnchor, redVisual, blueVisual;
    private static Vector3 originalPosition;
    private static float originalSize;
    private static bool failed;
    private static string Output => Path.GetFullPath("Temp/WorldRenderingChecks");

    static WorldRenderingChecks()
    {
        if (SessionState.GetBool(Running, false)) Register();
    }

    [MenuItem("Tools/Automaturgy/Validation/Check world rendering")]
    public static void Run()
    {
        phase = nextFrame = 0;
        failed = false;
        ValidationFixture.Open();
        Directory.CreateDirectory(Output);
        File.WriteAllText(Path.Combine(Output, "Results.txt"), "RUNNING");
        // A fresh CLI import can still have a domain reload queued. Let startup settle
        // before entering play, so it cannot reset gameplay singletons mid-frame.
        SessionState.SetBool(PendingPlay, true);
        SessionState.SetBool(Running, true);
        Register();
    }

    private static void Register()
    {
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update -= Update;
        EditorApplication.update += Update;
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
    }

    private static void OnLog(string message, string trace, LogType type)
    {
        if (EditorApplication.isPlaying && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) failed = true;
    }


    private static void Update()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 180) throw new Exception("Rendering checks timed out.");
            if (SessionState.GetBool(PendingPlay, false))
            {
                if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup - started < 5) return;
                SessionState.SetBool(PendingPlay, false);
                EditorApplication.isPlaying = true;
                return;
            }
            if (!EditorApplication.isPlaying || Time.frameCount < Math.Max(20, nextFrame)) return;
            if (failed) throw new Exception("Unity reported an error during the rendering checks; inspect the log.");
            if (phase == 1 && !EnemyDamageChecks.Ready) return;
            switch (phase++)
            {
                case 0:
                    Directory.CreateDirectory(Output);
                    ValidateScene();
                    ValidateParticles();
                    ValidateTileStages();
                    ValidateGeysers();
                    EnemyDamageChecks.Begin();
                    SetTile(5, 6, 400);
                    SetTile(6, 6, 100);
                    SetTile(5, 7, 210);
                    SetTile(6, 7, 300);
                    GameLogic.INSTANCE.enabled = false;
                    foreach (var mob in Object.FindObjectsByType<MobScript>()) mob.enabled = false;
                    nextFrame = Time.frameCount + 30;
                    break;
                case 1:
                    EnemyDamageChecks.Finish(Output);
                    Capture("World.png");
                    CapturePresentation();
                    SetupOcclusion();
                    nextFrame = Time.frameCount + 10;
                    break;
                case 2:
                    AssertCenter(Color.blue, "A sprite in front must occlude the particle system behind it.");
                    Capture("ParticleBehindSprite.png");
                    MoveAnchor(redAnchor, redVisual, -.5f);
                    MoveAnchor(blueAnchor, blueVisual, .5f);
                    nextFrame = Time.frameCount + 10;
                    break;
                case 3:
                    AssertCenter(Color.red, "A particle system in front must occlude the sprite behind it.");
                    Capture("ParticleInFrontOfSprite.png");
                    Camera.main.transform.position = originalPosition;
                    Camera.main.orthographicSize = originalSize * .8f;
                    Camera.main.aspect = 1f;
                    nextFrame = Time.frameCount + 10;
                    break;
                case 4:
                    var tile = GameLogic.INSTANCE.tilesGrid[6, 6];
                    var screen = Camera.main.WorldToScreenPoint(tile.transform.position);
                    Require(Object.FindAnyObjectByType<GridPointer>().Pick(screen) == tile, "Tile picking must match the displayed camera after zoom/resize.");
                    GameLogic.INSTANCE.InitializeGrid();
                    nextFrame = Time.frameCount + 10;
                    break;
                default:
                    Require(GameLogic.INSTANCE.tilesGrid[6, 6].SurfaceRenderer.GetComponentInParent<SortingGroup>(true).sortingLayerName == WorldSorting.Ground,
                        "Reset tiles must restore ground sorting.");
                    Require(GameLogic.INSTANCE.gridParent.GetComponentsInChildren<ParticleSystem>(true).Length == 0,
                        "Reset grass tiles must not create particle effects.");
                    File.WriteAllText(Path.Combine(Output, "Results.txt"),
                        "PASS: scene wiring, grass without particles, ground/wall transitions, all catalog anchors, stage continuity, pool reuse, rotated multi-cell reactions, " +
                        "upright geysers in every cast direction and pooled reuse, enemy damage effects for every element, particle/sprite occlusion in both Y orders, camera zoom/aspect changes, screen-space tile picking and grid reset.\n");
                    Debug.Log("WORLD RENDERING CHECKS PASSED: " + Output);
                    Stop(0);
                    break;
            }
        }
        catch (Exception exception)
        {
            File.WriteAllText(Path.Combine(Output, "Results.txt"), "FAIL: " + exception);
            Debug.LogException(exception);
            Stop(1);
        }
    }

    private static void ValidateScene()
    {
        ValidateElementSprites();
        Require(Camera.main.GetComponent<ParticlePixelation>() != null, "Camera needs shared particle pixel settings.");
        Require(Camera.main.targetTexture == null, "World must render directly at display resolution.");
        int world = LayerMask.GetMask("Default", "PixelVFX", "UI");
        Require((Camera.main.cullingMask & world) == world, "One camera must render sprites, particles and HUD.");
        Require(Object.FindObjectsByType<Camera>().Length == 1, "The old compositor camera must be removed.");
        var clock = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).First(t => t.name == "Clock");
        Require(clock.GetComponentsInChildren<Transform>(true).All(t => t.gameObject.layer == LayerMask.NameToLayer("UI")),
            "Inactive HUD elements must also be migrated so enabling them does not put them in the world render.");
        Require(Object.FindAnyObjectByType<PlayerHandler>().GetComponentInChildren<SortingGroup>().sortingLayerName == WorldSorting.World,
            "Player needs a feet anchor in the World sorting layer.");
        SetTile(5, 6, 400);
        var tile = GameLogic.INSTANCE.tilesGrid[5, 6];
        var group = tile.SurfaceRenderer.GetComponentInParent<SortingGroup>(true);
        Require(group.sortingLayerName == WorldSorting.World, "Walls must use a world anchor.");
        Require(Mathf.Abs(group.transform.position.y - tile.transform.TransformPoint(new Vector3(0, -.5f, 0)).y) < .001f,
            "Wall anchor must remain at its ground contact, independent of the raised artwork.");
        SetTile(5, 6, 0);
        Require(group.sortingLayerName == WorldSorting.Ground, "Changing a wall to ground must change its sorting layer.");
        Require(tile.GetComponentInChildren<ParticleSystem>(true) == null, "Grass tiles must have no particle effect.");
        SetTile(6, 6, 100);
        var flame = GameLogic.INSTANCE.tilesGrid[6, 6].GetComponentInChildren<ParticleSystem>();
        SetTile(6, 6, 101);
        Require(flame == GameLogic.INSTANCE.tilesGrid[6, 6].GetComponentInChildren<ParticleSystem>(), "Fading stages must preserve the existing effect.");
        SetTile(6, 6, 0);
        Require(GameLogic.INSTANCE.tilesGrid[6, 6].GetComponentInChildren<ParticleSystem>(true) == null,
            "Returning fire to grass must release its particle effect.");
        SetTile(6, 6, 100);
        Require(flame == GameLogic.INSTANCE.tilesGrid[6, 6].GetComponentInChildren<ParticleSystem>(), "Particle pooling must survive anchor creation.");
    }

    private static void ValidateElementSprites()
    {
        var game = GameLogic.INSTANCE;
        var textures = TextureHandler.INSTANCE;
        var tile = game.tilesGrid[3, 3];
        var overlay = (SpriteRenderer)new SerializedObject(tile).FindProperty("overlay").objectReferenceValue;


        foreach (int type in new[] { 100, 200, 300, 400 })
        {
            string sheet = type == 100 ? "Fire" : type == 200 ? "Water" : type == 300 ? "Lightning" : "RockWall";
            string isolated = sheet + (type == 200 ? "_0" : type == 400 ? "_34" : "_12");
            Require(textures.GetPreviewSprite(type)?.name == isolated, $"Wrong isolated preview for {type}.");
            SetTile(3, 3, type);
            Require(tile.SurfaceRenderer.sprite.name == isolated, "An isolated placed tile must use its blob.");
            SetTile(3, 4, type + 1);
            SetTile(4, 3, type);
            SetTile(4, 4, type + 1);
            string squareCorner = sheet + (type == 200 ? "_10" : type == 400 ? "_0" : "_1");
            Require(tile.SurfaceRenderer.sprite.name == squareCorner, $"Wrong 2x2 corner for {type}.");
            var colliderBounds = tile.GetComponent<Collider2D>().bounds;
            tile.ShowQueuedSpell(type);
            Require(overlay.sprite.name == isolated && Mathf.Abs(overlay.color.a - .75f) < .001f,
                "Queued previews must stay isolated and use queued opacity.");
            Require(Vector3.Distance(overlay.bounds.size, tile.SurfaceRenderer.bounds.size) < .001f,
                "Preview must fit one tile despite sprite import scale.");
            Require(tile.GetComponent<Collider2D>().bounds == colliderBounds, "Artwork must not resize the tile collider.");
            tile.ClearQueuedSpell();
            Require(overlay.color.a == 0f, "Clearing a queue must hide its preview.");

            SetTile(4, 4, 0);
            Require(tile.SurfaceRenderer.sprite.name == sheet + (type == 200 ? "_10" : type == 400 ? "_4" : "_1"),
                "Removing a diagonal must refresh stone while water keeps its thick L and fire/lightning corners remain unchanged.");
            // Refresh against a replaced gameplay grid while Tile.type is still stale.
            game.grid[3, 3] = type + 1;
            game.UpdateTile(4, 3);
            Require(((Color32)tile.SurfaceRenderer.color).Equals(new Color32(255, 255, 255, ElementDefinitions.Alpha(type + 1))),
                "Neighbor refresh must apply the current grid stage alpha.");
            for (int r = 3; r <= 4; r++)
                for (int c = 3; c <= 4; c++) SetTile(r, c, 0);
        }
        float mana = game.currMana;
        int selection = game.CurrentSelection;
        game.currMana = game.maxMana;
        var pointer = Object.FindAnyObjectByType<GridPointer>();
        foreach (bool teleport in new[] { false, true, false })
        {
            pointer.SetHovered(tile, teleport);
            var hover = game.gridParent.Find("Hover").GetComponent<SpriteRenderer>();
            bool inRange = PlayerHandler.INSTANCE.IsInBlinkRange(tile) && game.Castable(tile.row, tile.col);
            var expected = (Sprite)new SerializedObject(pointer).FindProperty(teleport && inRange ? "teleportSprite" : "borderSprite").objectReferenceValue;
            Require(expected != null && hover.sprite == expected && Mathf.Abs(hover.color.a - new SerializedObject(pointer).FindProperty("hoverAlpha").floatValue) < .001f,
                "Hover must switch between border and teleport sprites at the configured opacity.");
        }
        pointer.SetHovered(null);
        game.UpdateSelection(selection);
        game.currMana = mana;
        var selector = Object.FindAnyObjectByType<Selector>();
        Require(selector.GetComponentsInChildren<SpriteRenderer>().Single(r => r.enabled).sprite == textures.GetPreviewSprite(selection),
            "Selector must display its selected element's isolated sprite.");
        Debug.Log("ELEMENT SPRITE CHECKS PASSED: scene mappings, 2x2 rules, previews, white stage alpha, neighbor refresh, and collider sizes.");
    }

    private static void ValidateParticles()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<ParticleCatalog>("Assets/Rendering/Particles/ParticleCatalog.asset");
        foreach (var prefab in catalog.tiles.Select(t => t.prefab).Concat(catalog.reactions.Select(r => r.prefab))
            .Concat(catalog.damage.Select(d => d.prefab)).Distinct())
        {
            var root = Object.Instantiate(prefab);
            root.gameObject.SetActive(false);
            var systems = root.GetComponentsInChildren<ParticleSystem>(true);
            var pattern = root.GetComponent<ParticlePattern>();
            foreach (var system in systems.Where(s => s != root))
                Require(system.GetComponentInParent<SortingGroup>(true)?.sortingLayerName == WorldSorting.World,
                    prefab.name + " contains an unanchored particle renderer.");
            if (pattern != null)
                for (int direction = 0; direction < 4; direction++)
                {
                    root.transform.SetPositionAndRotation(new Vector3(3, -2, 0), Quaternion.Euler(0, 0, -90 * direction));
                    root.transform.localScale = Vector3.one * 1.25f;
                    foreach (var part in pattern.parts)
                    {
                        if (part.link)
                            Require(Mathf.Max(Mathf.Abs(part.to.x - part.from.x), Mathf.Abs(part.to.y - part.from.y)) <= 1,
                                prefab.name + " has a long unsplit link; author separate one-cell links before using Y sorting.");
                        Vector2 cell = part.link ? ((Vector2)part.from + part.to) * .5f : part.to;
                        Vector3 expected = root.transform.TransformPoint(new Vector3(cell.x, -cell.y, 0));
                        var actual = part.system.GetComponentInParent<SortingGroup>(true).transform.position;
                        Require(Vector3.Distance(expected, actual) < .001f, prefab.name + " sorts a rotated part at the wrong ground position.");
                    }
                }
            Object.Destroy(root.gameObject);
        }
    }

    private static void ValidateTileStages()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<ParticleCatalog>("Assets/Rendering/Particles/ParticleCatalog.asset");
        Require(catalog.tiles.Length == 27 && catalog.tiles.Select(t => t.prefab).Distinct().Count() == 7,
            "All 27 tile stages must share seven family prefabs.");
        foreach (var family in catalog.tiles.GroupBy(t => t.prefab))
        {
            SetTile(6, 6, family.Last().type);
            var root = GameLogic.INSTANCE.tilesGrid[6, 6].GetComponentInChildren<ParticleSystem>();
            SetTile(6, 6, 0);
            foreach (var stage in family.Concat(family.Reverse()))
            {
                SetTile(6, 6, stage.type);
                var current = GameLogic.INSTANCE.tilesGrid[6, 6].GetComponentInChildren<ParticleSystem>();
                Require(current == root, "Starting at a faded stage must reuse the same family pool and preserve stage continuity.");
                Require(current.transform.localScale == stage.scale, "Tile stage scale was not restored.");
                var systems = current.GetComponentsInChildren<ParticleSystem>().Where(p => p != current).ToDictionary(p => p.name);
                Require(systems.Count == stage.parts.Length, "Tile stages must describe every named emitter.");
                foreach (var part in stage.parts)
                {
                    var system = systems[part.emitter];
                    Require(system.main.maxParticles == part.maxParticles &&
                        system.main.startColor.Evaluate(.5f, .5f) == part.startColor.Evaluate(.5f, .5f) &&
                        system.colorOverLifetime.color.Evaluate(.5f, .5f) == part.colorOverLifetime.Evaluate(.5f, .5f) &&
                        Mathf.Approximately(system.emission.rateOverTime.Evaluate(.5f, .5f), part.rateOverTime.Evaluate(.5f, .5f)),
                        "Pooled stage settings were not restored for " + stage.type + "/" + part.emitter +
                        $": count {system.main.maxParticles}/{part.maxParticles}, start {system.main.startColor.Evaluate(.5f, .5f):F6}/{part.startColor.Evaluate(.5f, .5f):F6}," +
                        $" lifetime {system.colorOverLifetime.color.Evaluate(.5f, .5f):F6}/{part.colorOverLifetime.Evaluate(.5f, .5f):F6}," +
                        $" rate {system.emission.rateOverTime.Evaluate(.5f, .5f)}/{part.rateOverTime.Evaluate(.5f, .5f)}");
                }
            }
            SetTile(6, 6, 0);
        }
    }

    private static void ValidateGeysers()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<ParticleCatalog>("Assets/Rendering/Particles/ParticleCatalog.asset");
        foreach (string id in new[] { "Fire_Water_Cardinal_Geyser", "Fire_Water_Overlap_SteamRing" })
        {
            var prefab = catalog.reactions.Single(entry => entry.id == id).prefab;
            var authored = prefab.GetComponent<ParticlePattern>();
            Require(authored.keepEmittersUpright, id + " must preserve upward emission.");
            ParticlePattern previous = null;
            foreach (int direction in new[] { 0, 1, 2, 3, 0 })
            {
                var burst = new ReactionVisual(id, 7, 7, direction);
                foreach (var part in authored.parts) burst.cells.Add(RotateCell(part.to, direction) + new Vector2Int(7, 7));
                ParticleVFX.INSTANCE.Play(burst, 1f);
                var live = GameLogic.INSTANCE.gridParent.GetComponentsInChildren<ParticlePattern>().Single();
                if (previous != null) Require(previous == live, "Geyser orientation must also work when reusing a pooled effect.");
                foreach (var part in live.parts)
                {
                    Require(Vector3.Dot(part.system.transform.up, Vector3.up) > .999f, id + " tilted an emitter with the cast direction.");
                    Vector2Int cell = RotateCell(part.to, direction) + new Vector2Int(7, 7);
                    Vector3 expected = GameLogic.INSTANCE.tilesGrid[cell.y, cell.x].transform.position;
                    Require(Vector2.Distance(part.system.transform.position, expected) < .001f, id + " moved a jet off its rotated destination.");
                    if (!part.system.name.StartsWith("Geyser jet")) continue;
                    part.system.Simulate(part.system.main.startDelay.constantMax + .15f, false, true);
                    var particles = new ParticleSystem.Particle[part.system.main.maxParticles];
                    int count = part.system.GetParticles(particles);
                    Require(count > 0, "The geyser check must sample a live jet particle.");
                    Vector3 velocity = particles[0].totalVelocity;
                    if (part.system.main.simulationSpace == ParticleSystemSimulationSpace.Local)
                        velocity = part.system.transform.TransformVector(velocity);
                    Require(velocity.y > 0f, id + " jet particles must travel upward in world space.");
                }
                previous = live;
                ParticleVFX.INSTANCE.ClearBursts();
            }
        }
    }

    private static Vector2Int RotateCell(Vector2Int cell, int direction)
    {
        for (int i = 0; i < direction; i++) cell = new Vector2Int(-cell.y, cell.x);
        return cell;
    }


    private static void SetupOcclusion()
    {
        originalPosition = Camera.main.transform.position;
        originalSize = Camera.main.orthographicSize;
        Camera.main.transform.position = new Vector3(100, 100, -10);
        Camera.main.orthographicSize = 2;
        var material = new Material(Shader.Find("Grid Mage/Element Particle"));
        var parent = new GameObject("Occlusion checks").transform;
        parent.position = new Vector3(100, 100, 0);
        var red = WorldSorting.CreateGroup(parent, new Vector3(0, .5f, 0), "Particle ground anchor");
        redAnchor = red.transform;
        var particles = new GameObject("Red particle").AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particles.transform.position = parent.position;
        var main = particles.main;
        main.startSpeed = 0;
        main.startLifetime = 100;
        main.startSize = 2;
        main.startColor = Color.red;
        var emission = particles.emission;
        emission.enabled = false;
        var shape = particles.shape;
        shape.enabled = false;
        particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        WorldSorting.Include(red, particles.GetComponent<ParticleSystemRenderer>(), 0);
        particles.Emit(1);
        particles.Pause();
        redVisual = particles.transform;
        var blue = WorldSorting.CreateGroup(parent, new Vector3(0, -.5f, 0), "Sprite ground anchor");
        blueAnchor = blue.transform;
        var sprite = new GameObject("Blue sprite").AddComponent<SpriteRenderer>();
        sprite.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), Vector2.one * .5f, 1);
        sprite.color = Color.blue;
        sprite.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
        sprite.transform.position = parent.position;
        WorldSorting.Include(blue, sprite, 0);
        blueVisual = sprite.transform;
    }

    private static void MoveAnchor(Transform anchor, Transform visual, float y)
    {
        Vector3 position = visual.position;
        anchor.localPosition = new Vector3(0, y, 0);
        visual.position = position;
    }

    private static Texture2D ReadWorld()
    {
        return CheckSupport.RenderCamera(Camera.main, Mathf.RoundToInt(800 * Camera.main.aspect), 800);
    }

    private static void AssertCenter(Color expected, string message)
    {
        var texture = ReadWorld();
        Color actual = texture.GetPixel(texture.width / 2, texture.height / 2);
        Object.Destroy(texture);
        Require(Vector3.Distance(new Vector3(actual.r, actual.g, actual.b), new Vector3(expected.r, expected.g, expected.b)) < .1f,
            message + " Actual pixel: " + actual);
    }

    private static void Capture(string name)
    {
        var texture = ReadWorld();
        File.WriteAllBytes(Path.Combine(Output, name), texture.EncodeToPNG());
        Object.Destroy(texture);
    }

    private static void CapturePresentation()
    {
        var view = Camera.main;
        Canvas.ForceUpdateCanvases();
        var pixels = CheckSupport.RenderCamera(view, Mathf.RoundToInt(800 * view.aspect), 800);
        try
        {
        var button = GameObject.Find("Button");
        Vector3 uv = view.WorldToViewportPoint(button.transform.position);
        Color hud = pixels.GetPixel(Mathf.RoundToInt(uv.x * pixels.width), Mathf.RoundToInt(uv.y * pixels.height));
        Require(Vector3.Distance(new Vector3(hud.r, hud.g, hud.b), new Vector3(view.backgroundColor.r, view.backgroundColor.g, view.backgroundColor.b)) > .2f,
            "HUD sprites must be visible over the world.");
        File.WriteAllBytes(Path.Combine(Output, "WorldWithHUD.png"), pixels.EncodeToPNG());
        }
        finally { Object.Destroy(pixels); }
    }

    private static void Stop(int code)
    {
        SessionState.SetBool(Running, false);
        SessionState.SetBool(PendingPlay, false);
        EditorApplication.update -= Update;
        Application.logMessageReceived -= OnLog;
        if (Application.isBatchMode) EditorApplication.Exit(code);
        else EditorApplication.isPlaying = false;
    }
}
