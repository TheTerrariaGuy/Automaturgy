using System;
using System.Linq;
using Assets.Scripts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace GridMage.Workshop
{
    public sealed class ReactionWorkshopTab : WorkshopTab
    {
        public override string Title => "Reactions";
        [SerializeField] private TextAsset defaultContext;
        public bool saveDraft = true;
        public WorkshopSimulation Simulation { get; private set; }
        public ReactionDocument Document { get; private set; }
        public InputField WipField { get; private set; }
        public InputField ContextField { get; private set; }
        private InputField[] brushes;
        private Button[] brushButtons;
        private WorkshopGridView inputGrid, outputGrid, playground;
        private Text status, hover, metadata, clockLabel, selectionLabel;
        private Button pauseButton, stepButton, sourceButton;
        private RectTransform popup, sourcePopup;
        private Text sourceError;
        private InputField cellText;
        private Text cellError;
        private bool editingInput, autoSaveAllowed = true;
        private int editX, editY, selected;
        private string appliedContext;
        private float saveAt = -1;
        public void Configure(TextAsset source) => defaultContext = source;

        protected override void Build(RectTransform panel)
        {
            Simulation = new WorkshopSimulation(); Document = ReactionDocument.Parse(ReactionDocument.NewReaction);
            appliedContext = defaultContext != null ? defaultContext.text : "";
            WorkshopUI.Label(panel, "WIP REACTION", 24, 0, 930, 28, 20);
            WorkshopUI.Label(panel, "CONTEXT  /  Spells.txt", 1000, 0, 575, 28, 20);
            WipField = WorkshopUI.Input(panel, "WIP", Document.Source, 24, 34, 942, 165, true);
            ContextField = WorkshopUI.Input(panel, "Context", appliedContext, 1000, 34, 576, 165, true);
            WorkshopUI.Button(panel, "Apply WIP", 24, 212, 140, ApplyWip);
            WorkshopUI.Button(panel, "Copy WIP", 174, 212, 135, () => { GUIUtility.systemCopyBuffer = WipField.text; SetStatus("WIP copied."); });
            WorkshopUI.Button(panel, "Save draft", 319, 212, 135, () => Save(true));
            WorkshopUI.Button(panel, "Apply context", 1000, 212, 175, ApplyContext);
            WorkshopUI.Button(panel, "Restore Spells.txt", 1185, 212, 185, () =>
            {
                ContextField.text = defaultContext != null ? defaultContext.text : "";
                ApplyContext();
            });
            WorkshopUI.Button(panel, "Copy context", 1380, 212, 196, () => GUIUtility.systemCopyBuffer = ContextField.text);
            sourceButton = WorkshopUI.Button(panel, "Source: FIRE", 24, 260, 235, () =>
            {
                sourceError.text = "The source family determines which tile starts the reaction at (0,0). Brushes only change the painted cells.";
                sourcePopup.gameObject.SetActive(true);
            });
            metadata = WorkshopUI.Label(panel, "", 275, 257, 691, 45, 16);
            WorkshopUI.Label(panel, "INPUT  /  I", 24, 305, 450, 25, 19);
            WorkshopUI.Label(panel, "OUTPUT  /  O", 516, 305, 450, 25, 19);
            WorkshopUI.Label(panel, "PLAYGROUND  /  25 x 25", 1000, 263, 570, 28, 19);
            inputGrid = Grid(panel, "Input grid", 24, 340, 450, 15);
            outputGrid = Grid(panel, "Output grid", 516, 340, 450, 15);
            playground = Grid(panel, "Playground", 1000, 305, 575, 25);
            inputGrid.Clicked = (x, y, erase, shift) => Edit(true, x, y, erase, shift);
            outputGrid.Clicked = (x, y, erase, shift) => Edit(false, x, y, erase, shift);
            playground.Clicked = PaintPlayground;
            hover = WorkshopUI.Label(panel, "", 24, 797, 942, 30, 15);
            inputGrid.Hovered = value => hover.text = value;
            outputGrid.Hovered = value => hover.text = value;
            playground.Hovered = value => hover.text = "Playground " + value;
            WorkshopUI.Label(panel, "Origin outlined in mint. X right, Y up. Right-click erases; Shift-click edits tuples.", 24, 827, 942, 26, 15);
            brushes = new InputField[9]; brushButtons = new Button[9];
            for (int i = 0; i < 9; i++)
            {
                int index = i;
                float x = 24 + i * 105;
                brushButtons[i] = WorkshopUI.Button(panel, (i + 1).ToString(), x, 862, 95, () => SelectBrush(index));
                brushes[i] = WorkshopUI.Input(panel, "Brush " + (i + 1), i < 7 ? ((i + 1) * 100).ToString() : "", x, 902, 95, 34);
                brushes[i].onValueChanged.AddListener(_ => { DirtyDraft(); UpdateBrushes(); });
            }
            pauseButton = WorkshopUI.Button(panel, "Pause [P]", 1000, 892, 175, TogglePause);
            stepButton = WorkshopUI.Button(panel, "Step [A]", 1185, 892, 175, Step);
            WorkshopUI.Button(panel, "Deploy [Space]", 1370, 892, 205, Deploy);
            WorkshopUI.Button(panel, "Clear playground", 1000, 939, 185, () => { Simulation.Clear(); RefreshPlayground(); });
            clockLabel = WorkshopUI.Label(panel, "", 1200, 939, 376, 35, 15);
            selectionLabel = WorkshopUI.Label(panel, "", 24, 943, 942, 30, 15);
            status = WorkshopUI.Label(panel, "", 480, 206, 486, 48, 14);
            WipField.onValueChanged.AddListener(_ => { DirtyDraft(); SetStatus("WIP has typed changes. Apply WIP before painting."); });
            ContextField.onValueChanged.AddListener(_ => { DirtyDraft(); SetStatus("Context has typed changes. Apply context to activate them."); });
            BuildPopup(panel);
            BuildSourcePopup(panel);
            try { SetStatus(Simulation.Apply(appliedContext, Document)); }
            catch (Exception e) { SetStatus(e.Message, true); }
            if (saveDraft)
            {
                try
                {
                    var draft = WorkshopDraftStore.Load();
                    if (draft != null)
                    {
                        WipField.SetTextWithoutNotify(draft.wip); ContextField.SetTextWithoutNotify(draft.context);
                        for (int i = 0; i < 9; i++) brushes[i].SetTextWithoutNotify(draft.brushes[i] ?? "");
                        selected = Mathf.Clamp(draft.selectedBrush, 0, 8);
                        try
                        {
                            var doc = ReactionDocument.Parse(draft.wip);
                            string message = Simulation.Apply(draft.context, doc);
                            Document = doc; appliedContext = draft.context; SetStatus("Draft restored. " + message);
                        }
                        catch (Exception e) { SetStatus("Draft text restored; last valid/default rules remain active. " + e.Message, true); }
                    }
                }
                catch (Exception e) { autoSaveAllowed = false; SetStatus(e.Message, true); }
            }
            RefreshEditor(); RefreshPlayground(); UpdateBrushes(); UpdateClock();
        }

        private WorkshopGridView Grid(Transform parent, string name, float x, float y, float size, int cells)
        {
            var grid = WorkshopUI.Rect(parent, name, x, y, size, size).gameObject.AddComponent<WorkshopGridView>();
            grid.Initialize(cells); return grid;
        }
        public void ApplyWip()
        {
            try
            {
                var doc = ReactionDocument.Parse(WipField.text);
                string message = Simulation.Apply(appliedContext, doc);
                Document = doc; RefreshEditor(); SetStatus(message); DirtyDraft();
            }
            catch (Exception e) { SetStatus(e.Message, true); }
        }
        public void ApplyContext()
        {
            try
            {
                string message = Simulation.Apply(ContextField.text, Document);
                appliedContext = ContextField.text; SetStatus(message); DirtyDraft();
            }
            catch (Exception e) { SetStatus(e.Message, true); }
        }
        private void Publish(ReactionDocument next)
        {
            string message = Simulation.Apply(appliedContext, next);
            Document = next; WipField.SetTextWithoutNotify(next.Source);
            RefreshEditor(); SetStatus(next.EditNotice ?? message); DirtyDraft();
        }
        private void Edit(bool input, int x, int y, bool erase, bool shift)
        {
            if (popup.gameObject.activeSelf || sourcePopup.gameObject.activeSelf) return;
            if (WipField.text != Document.Source) { SetStatus("Apply typed WIP changes before painting; your text has been preserved.", true); return; }
            try
            {
                if (shift && !erase)
                {
                    var entries = Document.At(input, x, y).ToArray();
                    if (entries.Length == 0) return;
                    editingInput = input; editX = x; editY = y;
                    cellText.SetTextWithoutNotify(string.Join("\n", entries.Select(e => e.Tuple)));
                    cellError.text = input ? "Input tuples: (x,y,state). Dot expressions are supported." : "Output tuples: (x,y,state[,priority]). No dots; captured variables are allowed.";
                    popup.gameObject.SetActive(true); cellText.ActivateInputField(); return;
                }
                string brush = brushes[selected].text.Trim();
                if (!erase && string.IsNullOrEmpty(brush)) { SetStatus("This brush is unbound. Enter a tile ID below its key."); return; }
                Publish(Document.Paint(input, x, y, brush, erase));
            }
            catch (Exception e) { SetStatus(e.Message, true); }
        }
        private void PaintPlayground(int x, int y, bool erase, bool shift)
        {
            if (popup.gameObject.activeSelf || sourcePopup.gameObject.activeSelf) return;
            try
            {
                if (erase) Simulation.Queue.Remove(y + 12, x + 12);
                else
                {
                    if (!int.TryParse(brushes[selected].text.Trim(), out int id)) throw new FormatException("Playground brushes require one concrete tile ID, without dots or sets.");
                    if (!Simulation.QueueTile(y + 12, x + 12, id)) SetStatus("Queue placement needs an empty/spent cell with no queued tile.");
                }
                RefreshPlayground();
            }
            catch (Exception e) { SetStatus(e.Message, true); }
        }
        private void RefreshEditor()
        {
            inputGrid.Refresh((x, y) => EditorCell(true, x, y)); outputGrid.Refresh((x, y) => EditorCell(false, x, y));
            sourceButton.GetComponentInChildren<Text>().text = "Source: " + Document.Family + "...";
            metadata.text = (Document.Id == 0 ? "No reaction ID" : "R " + Document.Id) +
                "  |  15 x 15, offsets -7 to +7\n" + (Document.HiddenEntries == 0 ? "Family at origin is implicit. Paint 0 to require empty / clear an output." :
                Document.HiddenEntries + " tuple(s) have dynamic/out-of-view coordinates; preserved in WIP.");
        }
        private WorkshopGridView.Cell EditorCell(bool input, int x, int y)
        {
            var entries = Document.At(input, x, y).ToArray();
            if (entries.Length == 0) return new WorkshopGridView.Cell { Color = WorkshopPalette.Empty, Detail = "Unspecified" };
            string expression = entries[0].Fields[2];
            Color color = WorkshopUI.Surface;
            try { color = WorkshopPalette.ColorFor(SpellSetExpression.Compile(expression, 0, int.MaxValue, WorkshopPalette.Domain).First()); }
            catch (FormatException) { /* Captured output remains symbolic until a playground match. */ }
            return new WorkshopGridView.Cell { Color = color, Label = string.Join("\n", entries.Select(e => e.GridLabel)),
                Detail = string.Join("\n\n", entries.Select(e => e.HoverDetail)) };
        }
        private void RefreshPlayground() => playground.Refresh((x, y) =>
        {
            int row = y + 12, col = x + 12;
            bool queued = Simulation.Queue.Entries.TryGetValue((row, col), out var spell);
            int type = queued ? spell.type : Simulation.Board.Get(row, col);
            return new WorkshopGridView.Cell { Color = WorkshopPalette.ColorFor(type), Label = type == 0 ? "" : type.ToString(), Queued = queued,
                Detail = queued ? "Queued " + type + " (Space to deploy)" : type == 0 ? "Empty" : "Tile " + type };
        });
        private void BuildPopup(Transform parent)
        {
            popup = WorkshopUI.Panel(parent, "Cell editor overlay", 0, -65, 1600, 1050, new Color(0, 0, 0, .82f)).rectTransform;
            var box = WorkshopUI.Panel(popup, "Cell editor", 370, 300, 860, 420, WorkshopUI.Surface);
            WorkshopUI.Label(box.transform, "EDIT CELL", 25, 15, 810, 35, 24);
            WorkshopUI.Label(box.transform, "Edit state in each tuple. Multiple rows preserve multiple writes and their order.", 25, 60, 810, 40, 17);
            cellText = WorkshopUI.Input(box.transform, "Cell tuples", "", 25, 112, 810, 170, true);
            cellError = WorkshopUI.Label(box.transform, "", 25, 286, 810, 70, 16);
            WorkshopUI.Button(box.transform, "Apply cell", 25, 370, 170, () =>
            {
                try { Publish(Document.EditCell(editingInput, editX, editY, cellText.text)); ClosePopup(); }
                catch (Exception e) { cellError.text = e.Message; }
            });
            WorkshopUI.Button(box.transform, "Cancel [Esc]", 210, 370, 170, ClosePopup);
            popup.gameObject.SetActive(false);
        }
        private void ClosePopup() { popup.gameObject.SetActive(false); EventSystem.current?.SetSelectedGameObject(null); }
        private void BuildSourcePopup(Transform parent)
        {
            sourcePopup = WorkshopUI.Panel(parent, "Source family overlay", 0, -65, 1600, 1050, new Color(0, 0, 0, .82f)).rectTransform;
            var box = WorkshopUI.Panel(sourcePopup, "Source family", 370, 325, 860, 355, WorkshopUI.Surface);
            WorkshopUI.Label(box.transform, "REACTION SOURCE", 25, 15, 810, 35, 24);
            sourceError = WorkshopUI.Label(box.transform, "", 25, 60, 810, 80, 17);
            for (int i = 0; i < ReactionDocument.Families.Length; i++)
            {
                string family = ReactionDocument.Families[i];
                WorkshopUI.Button(box.transform, family + " (" + ((i + 1) * 100) + ")", 25 + i % 4 * 205, 155 + i / 4 * 48, 195, () =>
                {
                    try { Publish(ReactionDocument.Parse(WipField.text).WithFamily(family)); CloseSourcePopup(); }
                    catch (Exception e) { sourceError.text = e.Message; }
                });
            }
            WorkshopUI.Button(box.transform, "Cancel [Esc]", 25, 285, 170, CloseSourcePopup);
            sourcePopup.gameObject.SetActive(false);
        }
        private void CloseSourcePopup() { sourcePopup.gameObject.SetActive(false); EventSystem.current?.SetSelectedGameObject(null); }
        private void SelectBrush(int index) { selected = index; UpdateBrushes(); DirtyDraft(); }
        private void UpdateBrushes()
        {
            if (brushes == null || selectionLabel == null) return;
            for (int i = 0; i < brushes.Length; i++) brushButtons[i].GetComponent<Image>().color = i == selected ? new Color32(42, 95, 85, 255) : WorkshopUI.Surface;
            string brush = brushes[selected].text.Trim();
            selectionLabel.text = "Brush " + (selected + 1) + ": " + (brush.Length == 0 ? "unbound" : brush) + "  |  1-9 select. Dot patterns paint INPUT only. All 84 colors can simulate.";
        }
        public void TogglePause() { Simulation.TogglePause(); UpdateClock(); }
        public void Step() { Simulation.Step(); RefreshPlayground(); UpdateClock(); }
        public void Deploy() { Simulation.Deploy(); RefreshPlayground(); }
        private void UpdateClock()
        {
            pauseButton.GetComponentInChildren<Text>().text = Simulation.Paused ? "Resume [P]" : "Pause [P]";
            stepButton.interactable = Simulation.Paused;
            clockLabel.text = (Simulation.Paused ? "PAUSED" : "RUNNING") + "  /  Tick " + Simulation.TickCount + "  /  1 sec";
        }
        private void Update()
        {
            if (Simulation == null) return;
            if (Simulation.Advance(Time.unscaledDeltaTime)) RefreshPlayground();
            UpdateClock();
            if (saveAt >= 0 && Time.unscaledTime >= saveAt) Save(false);
            var k = Keyboard.current; if (k == null) return;
            if (popup.gameObject.activeSelf) { if (k.escapeKey.wasPressedThisFrame) ClosePopup(); return; }
            if (sourcePopup.gameObject.activeSelf) { if (k.escapeKey.wasPressedThisFrame) CloseSourcePopup(); return; }
            var selectedObject = EventSystem.current?.currentSelectedGameObject;
            if (selectedObject != null && selectedObject.GetComponent<InputField>()?.isFocused == true) return;
            for (int i = 0; i < 9; i++) if (k[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) SelectBrush(i);
            if (k.pKey.wasPressedThisFrame) TogglePause();
            if (k.aKey.wasPressedThisFrame && Simulation.Paused) Step();
            if (k.spaceKey.wasPressedThisFrame) Deploy();
        }
        private void SetStatus(string message, bool error = false)
        { status.text = message; status.color = error ? new Color32(255, 170, 138, 255) : WorkshopUI.Accent; }
        private void DirtyDraft() { if (autoSaveAllowed && saveDraft) saveAt = Time.unscaledTime + .7f; }
        private void Save(bool explicitSave)
        {
            saveAt = -1;
            if (!saveDraft || (!explicitSave && !autoSaveAllowed) || WipField == null) return;
            try
            {
                WorkshopDraftStore.Save(new WorkshopDraftStore.Draft { wip = WipField.text, context = ContextField.text,
                    brushes = brushes.Select(b => b.text).ToArray(), selectedBrush = selected });
                autoSaveAllowed = true; if (explicitSave) SetStatus("Draft saved locally. Source Spells.txt is unchanged.");
            }
            catch (Exception e) { autoSaveAllowed = false; SetStatus("Could not save draft: " + e.Message, true); }
        }
        public override void SetActive(bool active)
        {
            if (!active && popup != null) ClosePopup();
            if (!active && sourcePopup != null) CloseSourcePopup();
            if (!active && saveAt >= 0) Save(false);
            base.SetActive(active);
        }
        private void OnApplicationQuit() { if (saveAt >= 0) Save(false); }
        private void OnApplicationFocus(bool focused) { if (!focused && saveAt >= 0) Save(false); }
    }
}
