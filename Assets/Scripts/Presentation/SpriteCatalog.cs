using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Automaturgy/Sprite Catalog")]
public sealed class SpriteCatalog : ScriptableObject
{
    [Serializable] public struct Entry { public int key; public Sprite value; }
    [SerializeField] private Entry[] sprites = Array.Empty<Entry>();
    private Dictionary<int, Sprite> lookup;
    public IReadOnlyList<Entry> Entries => sprites;
    public bool TryGet(int key, out Sprite sprite)
    {
        if (lookup == null) Validate();
        return lookup.TryGetValue(key, out sprite);
    }
    public void Validate()
    {
        var next = new Dictionary<int, Sprite>();
        foreach (var entry in sprites)
        {
            if (entry.value == null) throw new InvalidOperationException("Missing sprite at key " + entry.key);
            if (!next.TryAdd(entry.key, entry.value)) throw new InvalidOperationException("Duplicate sprite key " + entry.key);
        }
        foreach (int type in new[] { 100, 200, 300, 400 })
            if (!next.ContainsKey(type * 100 + 2)) throw new InvalidOperationException("Missing isolated shape for " + type);
        lookup = next;
    }
    private void OnValidate() => lookup = null;
}
