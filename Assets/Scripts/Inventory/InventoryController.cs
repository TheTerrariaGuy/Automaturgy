using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Assets.Scripts.Inventory
{
    public sealed class InventoryController : MonoBehaviour
    {
        public Camera viewCamera;
        public InventoryGridView storage, active;
        public Text details, status, activeSummary;
        public Button enterButton, recoverButton, saveButton;
        public InventoryState State { get; private set; }
        public ItemPlacement Dragging { get; private set; }
        private Vector2Int grabOffset;
        private bool entering;
        private string loadError;

        private void Start()
        {
            enterButton.onClick.AddListener(EnterGame);
            recoverButton.onClick.AddListener(Recover);
            saveButton.onClick.AddListener(RetrySave);
            try
            {
                State = InventorySession.Load();
                State.Changed += Refresh;
                InventorySession.SaveStatusChanged += Refresh;
            }
            catch (Exception e) { loadError = e.Message; Debug.LogError(e); }
            storage.DrawBackground(); active.DrawBackground();
            Refresh();
        }

        private void Update()
        {
            viewCamera.orthographicSize = Mathf.Max(6.4f, 10.2f / Mathf.Max(.1f, viewCamera.aspect));
            if (State == null || entering) return;
            var mouse = Mouse.current;
            if (mouse == null) { CancelDrag(); return; }
            if (Keyboard.current?.escapeKey.wasPressedThisFrame == true || mouse.rightButton.wasPressedThisFrame)
            { CancelDrag(); return; }
            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            Vector2 screen = mouse.position.ReadValue();
            InventoryGridView view = null;
            Vector2Int cell = default;
            foreach (var candidate in new[] { storage, active })
            {
                var picked = candidate.Pick(viewCamera, screen);
                if (!overUI && viewCamera.pixelRect.Contains(screen) && candidate.Contains(picked))
                { view = candidate; cell = picked; break; }
            }
            if (Dragging == null && mouse.leftButton.wasPressedThisFrame && view != null)
                BeginDrag(view.grid, cell);
            storage.ghost.ClearAllTiles(); active.ghost.ClearAllTiles();
            if (Dragging != null)
            {
                if (view != null)
                {
                    var origin = cell - grabOffset;
                    view.Preview(State.Definition(Dragging), origin, State.CanPlace(Dragging, view.grid, origin.x, origin.y));
                }
                if (mouse.leftButton.wasReleasedThisFrame)
                {
                    if (view != null) Drop(view.grid, cell);
                    else CancelDrag();
                }
            }
            else
            {
                var item = view != null ? State.At(view.grid, cell.x, cell.y) : null;
                details.text = item == null ? "Drag blocks between grids. Only blocks on the right are active.\nRight-click or Esc cancels a drag." : Describe(State.Definition(item));
            }
        }

        public bool BeginDrag(InventoryGrid grid, Vector2Int cell)
        {
            if (State == null || Dragging != null || entering) return false;
            Dragging = State.At(grid, cell.x, cell.y);
            if (Dragging == null) return false;
            grabOffset = cell - new Vector2Int(Dragging.x, Dragging.y);
            details.text = Describe(State.Definition(Dragging));
            Refresh(); return true;
        }
        public bool Drop(InventoryGrid grid, Vector2Int grabbedCell)
        {
            if (Dragging == null) return false;
            var origin = grabbedCell - grabOffset;
            bool moved = State.Move(Dragging, grid, origin.x, origin.y);
            CancelDrag();
            return moved;
        }
        public void CancelDrag()
        {
            if (Dragging == null) return;
            Dragging = null;
            storage.ghost.ClearAllTiles(); active.ghost.ClearAllTiles(); Refresh();
        }
        private string Describe(InventoryItemDefinition definition) =>
            definition.displayName + "  /  " + definition.Width + " x " + definition.Height + "\n" +
            definition.reactionIds.Distinct().Count() + " reactions. " +
            (definition.placementElement != 0 ? "Enables element placement." : "Does not unlock placement.");

        public void Refresh()
        {
            enterButton.interactable = State != null && Dragging == null && !entering;
            recoverButton.interactable = State != null && State.Recovery.Count > 0 && Dragging == null;
            saveButton.interactable = State != null && Dragging == null;
            if (State == null) { status.text = loadError; return; }
            storage.Draw(State, Dragging); active.Draw(State, Dragging);
            var loadout = State.BuildLoadout();
            string[] names = { "Fire", "Water", "Electricity", "Stone" };
            string enabled = string.Join(", ", Enumerable.Range(1, 4).Where(i => loadout.CanPlace(i * 100)).Select(i => names[i - 1]));
            activeSummary.text = "Placement: " + (enabled.Length == 0 ? "none — add a fundamental block" : enabled) +
                "\n" + loadout.ReactionIds.Count + " active reactions";
            status.text = InventorySession.Notice ?? (State.Recovery.Count > 0 ? State.Recovery.Count + " items awaiting recovery. Free space and choose Recover." : "Changes save automatically.");
        }
        public void EnterGame()
        {
            if (State == null || Dragging != null || entering) return;
            try
            {
                if (!Application.CanStreamedLevelBeLoaded(InventorySession.GameplayScene))
                    throw new InvalidOperationException("The gameplay scene is missing from build settings.");
                InventorySession.PrepareRun();
                entering = true; Refresh();
                SceneManager.LoadSceneAsync(InventorySession.GameplayScene);
            }
            catch (Exception e) { entering = false; Refresh(); status.text = e.Message; }
        }
        private void Recover() { State?.RecoverAvailable(); Refresh(); }
        private void RetrySave() { InventorySession.Save(); Refresh(); }
        private void OnApplicationFocus(bool focused) { if (!focused) CancelDrag(); }
        private void OnDisable() { CancelDrag(); }
        private void OnDestroy()
        {
            if (State != null) State.Changed -= Refresh;
            InventorySession.SaveStatusChanged -= Refresh;
        }
    }
}
