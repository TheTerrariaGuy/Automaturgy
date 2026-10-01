using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GridMage.Workshop
{
    /// <summary>One mesh for colored cells, with lightweight labels; no gameplay tiles or VFX.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class WorkshopGridView : MaskableGraphic, IPointerDownHandler, IDragHandler, IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
    {
        public struct Cell
        {
            public Color Color;
            public string Label, Detail;
            public bool Queued;
        }
        public int Size { get; private set; }
        private Cell[] cells;
        private Text[] labels;
        private RectTransform tooltip;
        private Text tooltipText;
        private int hoveredIndex = -1;
        private Vector2Int lastCell = new Vector2Int(int.MinValue, int.MinValue);
        public Action<int, int, bool, bool> Clicked;
        public Action<string> Hovered;
        public void Initialize(int size)
        {
            Size = size; cells = new Cell[size * size]; labels = new Text[cells.Length];
            float step = rectTransform.rect.width / size;
            for (int r = 0; r < size; r++) for (int c = 0; c < size; c++)
            {
                var label = WorkshopUI.Label(transform, "", c * step + 1, r * step + 1, step - 2, step - 2, size == 15 ? 12 : 10);
                label.alignment = TextAnchor.MiddleCenter; label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 8; label.resizeTextMaxSize = size == 15 ? 12 : 10;
                labels[r * size + c] = label;
            }
            raycastTarget = true;
            float tooltipWidth = Mathf.Min(420, rectTransform.rect.width - 12);
            var background = WorkshopUI.Panel(transform, "Cell tooltip", 0, 0, tooltipWidth, 60, new Color32(8, 14, 23, 250));
            background.raycastTarget = false; tooltip = background.rectTransform;
            tooltipText = WorkshopUI.Label(tooltip, "", 10, 8, tooltipWidth - 20, 44, 16);
            tooltipText.alignment = TextAnchor.UpperLeft;
            tooltip.gameObject.SetActive(false);
        }
        public void Refresh(Func<int, int, Cell> read)
        {
            for (int r = 0; r < Size; r++) for (int c = 0; c < Size; c++)
            {
                int i = r * Size + c;
                cells[i] = read(c - Size / 2, Size / 2 - r);
                labels[i].text = cells[i].Label ?? "";
                labels[i].color = WorkshopPalette.Ink(cells[i].Color);
            }
            SetVerticesDirty();
            UpdateTooltip();
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); if (cells == null) return;
            Rect rect = rectTransform.rect;
            float w = rect.width / Size, h = rect.height / Size;
            for (int r = 0; r < Size; r++) for (int c = 0; c < Size; c++)
            {
                var cell = cells[r * Size + c];
                var bounds = new Rect(rect.xMin + c * w, rect.yMax - (r + 1) * h, w, h);
                bool origin = r == Size / 2 && c == Size / 2;
                Quad(bounds, cell.Queued ? new Color32(255, 218, 91, 255) : origin ? WorkshopUI.Accent : new Color32(46, 57, 72, 255));
                float inset = cell.Queued || origin ? 2 : 1;
                Quad(new Rect(bounds.x + inset, bounds.y + inset, w - 2 * inset, h - 2 * inset), cell.Color);
            }
            void Quad(Rect box, Color color)
            {
                int start = vh.currentVertCount;
                vh.AddVert(new Vector3(box.xMin, box.yMin), color, Vector2.zero);
                vh.AddVert(new Vector3(box.xMin, box.yMax), color, Vector2.zero);
                vh.AddVert(new Vector3(box.xMax, box.yMax), color, Vector2.zero);
                vh.AddVert(new Vector3(box.xMax, box.yMin), color, Vector2.zero);
                vh.AddTriangle(start, start + 1, start + 2); vh.AddTriangle(start + 2, start + 3, start);
            }
        }
        public void OnPointerDown(PointerEventData e)
        {
            lastCell = new Vector2Int(int.MinValue, int.MinValue);
            EventSystem.current?.SetSelectedGameObject(null);
            Dispatch(e, true);
        }
        public void OnDrag(PointerEventData e) => Dispatch(e, false);
        private void Dispatch(PointerEventData e, bool first)
        {
            if (e.button != PointerEventData.InputButton.Left && e.button != PointerEventData.InputButton.Right) return;
            if (!Pick(e, out var cell) || cell == lastCell) return;
            lastCell = cell;
            var k = Keyboard.current;
            bool shift = k != null && (k.leftShiftKey.isPressed || k.rightShiftKey.isPressed);
            if (shift && !first) return;
            Clicked?.Invoke(cell.x, cell.y, e.button == PointerEventData.InputButton.Right, shift);
        }
        private bool Pick(PointerEventData e, out Vector2Int cell)
        {
            cell = default;
            if (Size == 0 || !RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, e.position, e.enterEventCamera ?? e.pressEventCamera, out var p)) return false;
            var rect = rectTransform.rect;
            int col = Mathf.FloorToInt((p.x - rect.xMin) / rect.width * Size);
            int row = Mathf.FloorToInt((rect.yMax - p.y) / rect.height * Size);
            if (col < 0 || row < 0 || col >= Size || row >= Size) return false;
            cell = new Vector2Int(col - Size / 2, Size / 2 - row); return true;
        }
        public void OnPointerEnter(PointerEventData e) => OnPointerMove(e);
        public void OnPointerMove(PointerEventData e)
        {
            if (!Pick(e, out var p)) return;
            hoveredIndex = (Size / 2 - p.y) * Size + p.x + Size / 2;
            UpdateTooltip();
        }
        private void UpdateTooltip()
        {
            if (tooltip == null || hoveredIndex < 0) return;
            var cell = cells[hoveredIndex];
            int col = hoveredIndex % Size, row = hoveredIndex / Size;
            string detail = "(" + (col - Size / 2) + ", " + (Size / 2 - row) + ")  " + (cell.Detail ?? cell.Label ?? "Unspecified");
            Hovered?.Invoke(detail.Replace('\n', ' '));
            // Unspecified cells need no floating tooltip; their coordinates remain in the hover bar.
            tooltip.gameObject.SetActive(!string.IsNullOrEmpty(cell.Label));
            tooltipText.text = detail;
            float height = Mathf.Min(rectTransform.rect.height - 12, Mathf.Max(52, tooltipText.preferredHeight + 16));
            tooltip.sizeDelta = new Vector2(tooltip.sizeDelta.x, height);
            tooltipText.rectTransform.sizeDelta = new Vector2(tooltipText.rectTransform.sizeDelta.x, height - 16);
            float cellSize = rectTransform.rect.width / Size;
            tooltip.anchoredPosition = new Vector2(Mathf.Clamp((col + 1) * cellSize, 6, rectTransform.rect.width - tooltip.sizeDelta.x - 6),
                -Mathf.Clamp((row + 1) * cellSize, 6, rectTransform.rect.height - height - 6));
            tooltip.SetAsLastSibling();
        }
        public void OnPointerExit(PointerEventData e)
        {
            hoveredIndex = -1; if (tooltip != null) tooltip.gameObject.SetActive(false); Hovered?.Invoke("");
        }
        protected override void OnDisable() { base.OnDisable(); OnPointerExit(null); }
    }
}
