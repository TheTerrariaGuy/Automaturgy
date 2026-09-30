using System;
using System.Collections.Generic;
using Assets.Scripts;
using UnityEngine;
using Assets.Scripts.Inventory;

/// <summary>Legacy scene adapter. Catalog, parsing, and active rules are owned outside this component.</summary>
public sealed class Indexing : MonoBehaviour
{
    public static Indexing INSTANCE { get; private set; }

    private void Awake()
    {
        if (INSTANCE != null && INSTANCE != this) { Destroy(this); return; }
        INSTANCE = this;
    }
    public IReadOnlyList<Reaction> GetReactions(int type) =>
        InventorySession.GetRun().GetReactions(type);
    private void OnDestroy() { if (INSTANCE == this) INSTANCE = null; }
}
