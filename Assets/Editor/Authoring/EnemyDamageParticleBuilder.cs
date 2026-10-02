using System;
using UnityEditor;
using UnityEngine;

public static class EnemyDamageParticleBuilder
{
    [MenuItem("Tools/Automaturgy/Particles/Rebuild enemy damage effects")]
    public static void Build()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<ParticleCatalog>("Assets/Rendering/Particles/ParticleCatalog.asset");
        var entries = new ParticleCatalog.DamageEntry[4];
        string[] names = { "Fire", "Water", "Electricity", "Lava" };
        for (int i = 0; i < names.Length; i++)
        {
            var root = new GameObject("Damage_" + names[i]);
            root.SetActive(false);
            root.layer = LayerMask.NameToLayer("PixelVFX");
            var container = root.AddComponent<ParticleSystem>();
            var main = container.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 1f;
            main.startLifetime = 0f;
            main.startSpeed = 0f;
            main.maxParticles = 0;
            var emission = container.emission;
            emission.enabled = false;
            var shape = container.shape;
            shape.enabled = false;
            container.GetComponent<ParticleSystemRenderer>().enabled = false;
            switch (i)
            {
                case 0:
                    Part(root, "Flame tongues", "Flame", 9, .20f, .36f, .25f, .45f,
                        new Color(1, .25f, .03f), new Color(1, .85f, .2f), .6f, .8f, 1.6f, 0f, .2f, 0);
                    Part(root, "Rising embers", "Chip", 8, .055f, .10f, .35f, .65f,
                        new Color(1, .5f, .06f), new Color(1, .95f, .4f), .9f, .8f, 2f, -.6f, 3.14f, 1);
                    break;
                case 1:
                    Part(root, "Blue splash", "Droplet", 12, .10f, .20f, .3f, .55f,
                        new Color(.12f, .55f, 1), new Color(.7f, 1, 1), 1.5f, .5f, 1.8f, -3.5f, .7f, 1);
                    Part(root, "Impact ripple", "Ripple", 1, .6f, .6f, .3f, .3f,
                        new Color(.25f, .8f, 1), new Color(.8f, 1, 1), 0, 0, 0, 0, 0, 0);
                    break;
                case 2:
                    Part(root, "Violet shock arcs", "Bolt", 6, .32f, .55f, .15f, .28f,
                        new Color(.55f, .25f, 1), new Color(.95f, .85f, 1), .4f, -.3f, .3f, 0, 3.14f, 0);
                    Part(root, "Electric sparks", "Chip", 9, .06f, .12f, .2f, .4f,
                        new Color(.7f, .55f, 1), Color.white, 2, -1.6f, 1.6f, 0, 3.14f, 1);
                    break;
                default:
                    Part(root, "Molten chips", "Chip", 9, .12f, .23f, .35f, .6f,
                        new Color(.9f, .15f, .015f), new Color(1, .7f, .08f), 1.1f, .8f, 1.9f, -3.8f, 3.14f, 1);
                    Part(root, "Hot smoke", "Puff", 5, .20f, .36f, .4f, .75f,
                        new Color(.2f, .08f, .05f, .65f), new Color(.45f, .19f, .09f, .8f), .25f, .4f, .9f, 0, 3.14f, 0);
                    Part(root, "Lava impact", "Ripple", 1, .5f, .5f, .22f, .22f,
                        new Color(1, .3f, .015f), new Color(1, .7f, .08f), 0, 0, 0, 0, 0, 2);
                    break;
            }
            ParticlePrefabAuthoring.Bake(container, damage: true);
            var saved = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Rendering/Particles/Prefabs/" + root.name + ".prefab");
            entries[i] = new ParticleCatalog.DamageEntry { element = i + 1, prefab = saved.GetComponent<ParticleSystem>() };
            UnityEngine.Object.DestroyImmediate(root);
        }
        catalog.damage = entries;
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Debug.Log("Built four enemy damage effects and updated the particle catalog.");
    }

    private static void Part(GameObject root, string name, string mesh, int count, float sizeMin, float sizeMax,
        float lifeMin, float lifeMax, Color low, Color high, float spread, float riseMin, float riseMax, float gravity, float rotation, int order)
    {
        var child = new GameObject(name);
        child.layer = root.layer;
        child.transform.SetParent(root.transform, false);
        var system = child.AddComponent<ParticleSystem>();
        var main = system.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 1;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
        main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
        main.startSpeed = 0;
        main.startColor = new ParticleSystem.MinMaxGradient(low, high);
        main.startRotation = new ParticleSystem.MinMaxCurve(-rotation, rotation);
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = count;
        var shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Rectangle;
        shape.scale = count == 1 ? Vector3.zero : new Vector3(.38f, .45f, 0);
        var emission = system.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0, (short)count) });
        var velocity = system.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;
        velocity.x = new ParticleSystem.MinMaxCurve(-spread, spread);
        velocity.y = new ParticleSystem.MinMaxCurve(riseMin, riseMax);
        velocity.z = new ParticleSystem.MinMaxCurve(0, 0);
        var force = system.forceOverLifetime;
        force.enabled = gravity != 0;
        force.space = ParticleSystemSimulationSpace.Local;
        force.y = gravity;
        var size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, count == 1 ? 1.8f : .35f));
        var color = system.colorOverLifetime;
        color.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, .2f), new GradientAlphaKey(0, 1) });
        color.color = fade;
        var renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.alignment = ParticleSystemRenderSpace.Local;
        renderer.mesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Rendering/Particles/Meshes/" + mesh + ".asset");
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Rendering/Particles/Materials/Element_Unlit.mat");
        renderer.sortingOrder = order;
    }

    public static void BuildAndCheck()
    {
        try { Build(); WorldRenderingChecks.Run(); }
        catch (Exception exception) { Debug.LogException(exception); EditorApplication.Exit(1); }
    }
}
