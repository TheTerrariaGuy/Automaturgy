using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Automaturgy/Particle Catalog")]
public class ParticleCatalog : ScriptableObject
{
    [Serializable] public struct TileEntry
    {
        public int type;
        public ParticleSystem prefab;
        public Vector3 scale;
        public StagePart[] parts;
    }
    [Serializable] public struct StagePart
    {
        public string emitter;
        public ParticleSystem.MinMaxGradient startColor, colorOverLifetime;
        public ParticleSystem.MinMaxCurve rateOverTime;
        public int maxParticles;

        public void Apply(ParticleSystem system)
        {
            var main = system.main;
            main.startColor = startColor;
            main.maxParticles = maxParticles;
            var color = system.colorOverLifetime;
            color.color = colorOverLifetime;
            var emission = system.emission;
            emission.rateOverTime = rateOverTime;
        }
    }
    [Serializable] public struct ReactionEntry { public string id; public ParticleSystem prefab; }
    [Serializable] public struct DamageEntry { public int element; public ParticleSystem prefab; }
    public TileEntry[] tiles;
    public ReactionEntry[] reactions;
    [Tooltip("Element families: 1 fire, 2 water, 3 electricity, 4 lava (stone family).")]
    public DamageEntry[] damage = Array.Empty<DamageEntry>();
}
