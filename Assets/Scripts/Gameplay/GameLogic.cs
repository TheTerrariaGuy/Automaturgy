using System;
using UnityEngine;
using Assets.Scripts.Inventory;

namespace Assets.Scripts
{
    [DefaultExecutionOrder(-100)]
    public class GameLogic : MonoBehaviour
    {
        public static GameLogic INSTANCE;

        public BoardState Board { get; private set; }
        public int[,] grid => Board?.Cells;
        [NonSerialized] public Tile[,] tilesGrid;
        public bool[,] cellExists => Board?.Exists;
        public float[,] elevationGrid => Board?.Elevations;
        [SerializeField] private TilemapLevel level;
        public TilemapLevel Level => level;
        public TilemapLevel.Layout LevelLayout { get; private set; }
        public Vector3 GridTranslation { get; private set; }
        [SerializeField] private GameObject tile;
        [SerializeField] private int rows, cols;
        [SerializeField] public Transform gridParent;
        [SerializeField] public float spacing, offset;
        [SerializeField] private float clock;
        [SerializeField] private GameObject clockHand;
        [SerializeField] public float maxMana, manaRegen;
        [SerializeField, Min(0f)] private float placementManaRegenMultiplier = 0.5f;
        [NonSerialized] public bool[,] castableGrid;
        [NonSerialized] public bool[,] moveableGrid;
        [SerializeField] private CastableOutline castableOutline;
        [SerializeField] private CastableOutline moveableOutline;
        private int currentSelection;
        public float time;
        public float currMana;
        private const float SubmitQueueManaCost = 5f;
        private readonly SpellQueue spellQueue = new();
        private ReactionResolver resolver;
        public RunLoadout Loadout { get; private set; }
        private readonly TilePresentation presentation = new();
        private float reservedMana => spellQueue.ReservedMana;
        public event Action LevelInitialized;
        public event Action<int> SelectionChanged;
        public bool IsPlacementMode => spellQueue.HasSpells;
        public float AvailableMana => Mathf.Max(0f, currMana - reservedMana);
        public int CurrentSelection => currentSelection;

        private void Awake()
        {
            if (INSTANCE == null)
            {
                INSTANCE = this;
                try { Loadout = InventorySession.GetRun(); }
                catch (Exception e)
                {
                    Debug.LogError("Cannot load spell inventory: " + e.Message);
                    Loadout = new RunLoadout(Array.Empty<int>());
                }
                currentSelection = Loadout.FirstElement;
            } else
            {
                Destroy(this);
                return;
            }

        }

        private void Start() => InitializeGrid();

        public void InitializeGrid()
        {
            // Validate before disposing the previous board.
            TilemapLevel.Layout layout = level != null ? level.Read(gridParent) : null;
            ParticleVFX.INSTANCE?.ClearBursts();
            if (tilesGrid != null)
                foreach (Tile oldTile in tilesGrid)
                {
                    if (oldTile == null) continue;
                    oldTile.ReleaseParticles();
                    oldTile.gameObject.SetActive(false);
                    Destroy(oldTile.gameObject);
                }
            spellQueue.Clear();
            LevelLayout = layout;
            GridTranslation = Vector3.zero;
            if (layout != null)
            {
                rows = layout.exists.GetLength(0);
                cols = layout.exists.GetLength(1);
                spacing = layout.spacing;
                GridTranslation = layout.origin - new Vector3(offset, offset, 0f);
            }
            Board = new BoardState(rows, cols, layout?.exists, layout?.elevations,
                layout?.walkable, layout?.blocksSight, layout?.allowsSpells);
            resolver = new ReactionResolver(Board, Loadout.GetReactions);
            castableGrid = new bool[rows, cols];
            moveableGrid = new bool[rows, cols];
            tilesGrid = new Tile[rows, cols];
            for (int i = 0; i < grid.GetLength(0); i++)
            {
                for (int j = 0; j < grid.GetLength(1); j++)
                {
                    if (!cellExists[i, j]) continue;
                    GameObject t = Instantiate(tile, new Vector3(0, 0, 0), Quaternion.identity);
                    Tile tileScript = t.GetComponent<Tile>();
                    tilesGrid[i, j] = tileScript;
                    tileScript.Init(this, i, j);
                }
            }
            if (layout != null) level.HideMarkers();
            if (PlayerHandler.INSTANCE != null) PlayerHandler.INSTANCE.ResetForLevel();
            if (MobHandler.INSTANCE != null) MobHandler.INSTANCE.ResetForLevel();
            presentation.Reset(tilesGrid);
            UpdateTiles();
            MakeCastable();
            LevelInitialized?.Invoke();
        }

        public bool HasCell(int r, int c) => Board != null && Board.HasCell(r, c);
        public bool AllowsSpells(int r, int c) => Board != null && Board.AllowsSpells(r, c);
        public bool CanWalk(int r, int c) => Board != null && Board.CanWalk(r, c);
        public bool BlocksSight(int r, int c) => Board == null || Board.BlocksSight(r, c);
        public bool CanStep(int fromR, int fromC, int toR, int toC) =>
            Board != null && Board.CanStep(fromR, fromC, toR, toC);

        public bool Castable(int r, int c) =>
            AllowsSpells(r, c) && GridMath.IsInBounds(castableGrid, r, c) && castableGrid[r, c];

        public bool CanBlinkTo(int r, int c) =>
            CanWalk(r, c) && GridMath.IsInBounds(moveableGrid, r, c) && moveableGrid[r, c];

        public void MakeCastable()
        {
            if (castableGrid != null)
            {
                Array.Clear(castableGrid, 0, castableGrid.Length);
                Array.Clear(moveableGrid, 0, moveableGrid.Length);
                PlayerHandler player = PlayerHandler.INSTANCE;
                if (player != null && HasCell(player.r, player.c))
                {
                    int range = Mathf.Max(0, player.castRange);
                    int blinkRange = Mathf.Max(0, player.blinkRange);
                    int[,] walls = GridHelper.INSTANCE.ExtractWalls();
                    for (int row = Mathf.Max(0, player.r - range); row <= Mathf.Min(rows - 1, player.r + range); row++)
                        for (int col = Mathf.Max(0, player.c - range); col <= Mathf.Min(cols - 1, player.c + range); col++)
                        {
                            bool visible = HasCell(row, col) && GridMath.CanReach(walls, player.r, player.c, row, col, range);
                            int distance = GridMath.SquareDistance(row, col, player.r, player.c);
                            castableGrid[row, col] = AllowsSpells(row, col) && visible;
                            moveableGrid[row, col] = distance > 0 && distance <= blinkRange && CanWalk(row, col) &&
                                visible && GridHelper.INSTANCE.TestElevationLine(player.r, player.c, row, col);
                        }
                }
            }
            castableOutline?.Rebuild(this);
            moveableOutline?.Rebuild(this);
        }

        void Update()
        {
            if (grid == null) return;
            time += Time.deltaTime;
            currMana += Time.deltaTime * manaRegen * (IsPlacementMode ? placementManaRegenMultiplier : 1f);
            // Queued spells hold their cost separately so spendable mana can still regenerate.
            currMana = Mathf.Min(currMana, maxMana + reservedMana);
            if (clock <= 0f) return;
            while (time >= clock)
            {
                time -= clock;
                TickCombat();
            }
            float angle = time / clock * 360f;
            if (clockHand != null) clockHand.transform.localRotation = Quaternion.Euler(0f, 0f, -angle);
        }

        public void UpdateTiles() => presentation.Refresh(tilesGrid, false);
        public void UpdateTile(int r, int c)
        {
            if (HasCell(r, c)) presentation.RefreshCell(tilesGrid, r, c);
        }

        public void UpdateSelection(int newSelection)
        {
            if (Loadout == null || !Loadout.CanPlace(newSelection)) return;
            if (currentSelection == newSelection) return;
            currentSelection = newSelection;
            SelectionChanged?.Invoke(newSelection);
        }

        public void Clicked(Tile t)
        {
            MakeMove(t.row, t.col, currentSelection);
        }

        public bool TryCastBlink(Tile target)
        {
            PlayerHandler player = PlayerHandler.INSTANCE;
            if (player == null || !player.isActiveAndEnabled || target == null ||
                !HasCell(target.row, target.col) ||
                tilesGrid[target.row, target.col] != target ||
                AvailableMana < player.BlinkManaCost) return false;
            if (!player.BlinkTo(target)) return false;
            currMana -= player.BlinkManaCost;
            return true;
        }


        // type: 100, 200 ,300, 400
        public void MakeMove(int r, int c, int type)
        {
            if (!CanQueueSpell(r, c, type)) return;
            ElementDefinitions.TryManaCost(type, out float manaCost);
            spellQueue.Add(r, c, type, manaCost);
            tilesGrid[r, c].ShowQueuedSpell(type);
        }

        public bool CanQueueSpell(int r, int c, int type)
        {
            return Loadout != null && Loadout.CanPlace(type) && Castable(r, c) && CanCastAt(r, c) && !spellQueue.Contains(r, c) &&
                ElementDefinitions.TryManaCost(type, out float manaCost) &&
                AvailableMana >= manaCost;
        }

        private bool CanCastAt(int r, int c)
        {
            // Queued spells only recheck terrain, not the player's current range or sight.
            return AllowsSpells(r, c) &&
                ElementState.CanPlaceOn(grid[r, c]);
        }

        public void RemoveQueuedSpell(int r, int c)
        {
            if (!spellQueue.Remove(r, c)) return;
            currMana = Mathf.Min(currMana, maxMana + reservedMana);
            tilesGrid[r, c].ClearQueuedSpell();
        }

        private void RemoveInvalidQueuedSpells()
        {
            spellQueue.RemoveInvalid(CanCastAt, (r, c) => tilesGrid[r, c].ClearQueuedSpell());
            currMana = Mathf.Min(currMana, maxMana + reservedMana);
        }

        public void SubmitQueuedSpells()
        {
            RemoveInvalidQueuedSpells();
            if (!IsPlacementMode || AvailableMana < SubmitQueueManaCost) return;
            currMana -= reservedMana + SubmitQueueManaCost;
            foreach (var entry in spellQueue.Entries)
            {
                var cell = entry.Key;
                Board.Set(cell.row, cell.col, entry.Value.type);
                tilesGrid[cell.row, cell.col].ClearQueuedSpell();
            }
            spellQueue.Clear();
            UpdateTiles();
            MakeCastable();
        }

        public void TickCombat()
        {
            var visuals = resolver.Resolve(_ => RemoveInvalidQueuedSpells());
            UpdateTiles();
            foreach (var visual in visuals) ParticleVFX.INSTANCE?.Play(visual, clock);
            MobHandler.INSTANCE?.HandleUpdate();
            MakeCastable();
        }

        private void OnDestroy() { if (INSTANCE == this) INSTANCE = null; }
    }
}
