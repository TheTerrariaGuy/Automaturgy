using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace GridMage.Workshop.Editor
{
    /// <summary>Runs against the actual workshop UI in Play mode; leaves no saved test draft.</summary>
    public static class WorkshopPlayChecks
    {
        [UnityEditor.MenuItem("Tools/Automaturgy/Workshop/Run Play-mode checks")]
        public static void RunMenu() => Debug.Log(Run());

        public static string Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Open Workshop and enter Play mode first.");
            var tab = UnityEngine.Object.FindAnyObjectByType<ReactionWorkshopTab>();
            if (tab == null || tab.Simulation == null) throw new InvalidOperationException("Workshop is not initialized.");
            int checks = 0;
            void Require(bool condition, string message) { checks++; if (!condition) throw new InvalidOperationException(message); }
            var fields = tab.Panel.GetComponentsInChildren<InputField>();
            var brushes = Enumerable.Range(1, 9).Select(i => fields.Single(f => f.name == "Brush " + i)).ToArray();
            var grids = tab.Panel.GetComponentsInChildren<WorkshopGridView>();
            var input = grids.Single(g => g.name == "Input grid");
            var output = grids.Single(g => g.name == "Output grid");
            var playground = grids.Single(g => g.name == "Playground");
            string originalWip = tab.WipField.text, originalContext = tab.ContextField.text;
            string[] originalBrushes = brushes.Select(b => b.text).ToArray();
            bool saving = tab.saveDraft, paused = tab.Simulation.Paused;
            var originalKeyboard = Keyboard.current;
            Keyboard keyboard = null;
            tab.saveDraft = false;
            try
            {
                keyboard = InputSystem.AddDevice<Keyboard>("Workshop validation keyboard"); keyboard.MakeCurrent();
                if (!tab.Simulation.Paused) tab.TogglePause();
                tab.ContextField.text = ""; tab.ApplyContext();
                tab.WipField.text = ReactionDocument.NewReaction; tab.ApplyWip();
                Require(grids.Length == 3 && input.Size == 15 && output.Size == 15 && playground.Size == 25, "Three grids have requested dimensions.");
                Require(grids.All(g => g.canvasRenderer != null), "All grids have renderers.");
                Require(tab.ContextField.textComponent.text.Length == 0, "Context textbox is editable.");
                tab.WipField.text = "FIRE\nI (0,0,500) (1,0,500) O (2,0,508) (1,1,501) (1,-1,501) D (0,1,2,3) E";
                tab.ApplyWip();
                Require(tab.Document.Source == ReactionDocument.NewReaction, "An impossible origin mismatch does not replace the active document.");
                tab.Panel.GetComponentsInChildren<Button>().Single(b => b.name == "Source: FIRE").onClick.Invoke();
                var sourcePopup = tab.Panel.Find("Source family overlay").gameObject;
                Require(sourcePopup.activeSelf, "Source family picker opens.");
                sourcePopup.GetComponentsInChildren<Button>().Single(b => b.name == "WIND (500)").onClick.Invoke();
                Require(!sourcePopup.activeSelf && tab.Document.Family == "WIND" && tab.WipField.text.StartsWith("WIND\n"),
                    "Selecting Wind corrects the pending reaction text and applies it.");
                tab.Simulation.Clear(); tab.Simulation.QueueTile(12, 12, 500); tab.Simulation.QueueTile(12, 13, 500); tab.Deploy(); tab.Step();
                Require(tab.Simulation.Board.Get(12, 14) == 508 && tab.Simulation.Board.Get(13, 13) == 501 && tab.Simulation.Board.Get(11, 13) == 501,
                    "Wind selected through the UI runs the user's pattern in the playground.");
                tab.Simulation.Clear(); tab.WipField.text = ReactionDocument.NewReaction; tab.ApplyWip();
                tab.WipField.text = "FIRE\nI (1,0,...-4..) (2,0,50.) O (0,1,a) D (0) E"; tab.ApplyWip();
                Require(input.transform.GetChild(7 * 15 + 8).GetComponent<Text>().text == "a" &&
                    input.transform.GetChild(7 * 15 + 9).GetComponent<Text>().text == "b", "Grid shows capture letters instead of formulas.");
                var hoverData = new PointerEventData(EventSystem.current) {
                    position = RectTransformUtility.WorldToScreenPoint(input.canvas.worldCamera, input.transform.TransformPoint(new Vector3(8.5f * 30, -7.5f * 30))),
                    pointerCurrentRaycast = new RaycastResult { module = input.canvas.GetComponent<GraphicRaycaster>() }
                };
                input.OnPointerEnter(hoverData);
                var tooltip = input.transform.Find("Cell tooltip");
                Require(tooltip.gameObject.activeSelf && tooltip.GetComponentInChildren<Text>().text.Contains("a = ...-4.."),
                    "Hover without clicking shows the captured formula in a tooltip.");
                input.OnPointerExit(hoverData); Require(!tooltip.gameObject.activeSelf, "Tooltip hides on pointer exit.");
                brushes[8].text = "a"; Press(Key.Digit9); Click(output, 3, 0, false);
                Require(tab.Document.At(false, 3, 0).Single().Fields[2] == "a" &&
                    output.transform.GetChild(7 * 15 + 10).GetComponent<Text>().text == "a",
                    "Variable brush paints output through the UI and displays the variable name.");
                brushes[8].text = "b"; Click(output, 4, 0, false);
                Click(input, 1, 0, true);
                Require(!tab.Document.At(true, 1, 0).Any() && !tab.Document.At(false, 3, 0).Any() &&
                    !tab.Document.At(false, 0, 1).Any(), "Right-click deletes a variable cell and its dependent outputs.");
                Require(tab.Document.At(true, 2, 0).Single().GridLabel == "a" &&
                    tab.Document.At(false, 4, 0).Single().Fields[2] == "a" && tab.WipField.text == tab.Document.Source,
                    "Right-click updates surviving labels, output bindings, and WIP text together.");
                tab.WipField.text = ReactionDocument.NewReaction; tab.ApplyWip();
                for (int i = 0; i < 9; i++)
                {
                    Press((Key)((int)Key.Digit1 + i));
                    Require((int)typeof(ReactionWorkshopTab).GetField("selected", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tab) == i,
                        "Digit " + (i + 1) + " selects its brush.");
                }
                brushes[0].text = "100"; Press(Key.Digit1);
                Click(input, 1, 0, false);
                Require(tab.Document.At(true, 1, 0).Single().Fields[2] == "100" && tab.WipField.text.Contains("(1,0,100)"), "Pointer paints input and immediately updates text.");
                brushes[0].text = "20.";
                Click(input, 1, 0, false);
                Require(tab.Document.At(true, 1, 0).Single().Fields[2] == "20.", "Wildcard brush paints input.");
                Click(output, 1, 0, false);
                Require(tab.Document.Outputs.Count == 0, "Wildcard cannot paint output.");
                brushes[0].text = "608"; Click(output, 1, 0, false);
                Require(tab.Document.At(false, 1, 0).Single().Fields[2] == "608", "Extended color paints output.");
                SetKeys(Key.LeftShift); Click(input, 1, 0, false); SetKeys();
                var popup = tab.Panel.Find("Cell editor overlay").gameObject;
                Require(popup.activeSelf, "Shift-click opens populated editor cell.");
                var cell = popup.GetComponentsInChildren<InputField>().Single();
                cell.text = "(1,0,70.)";
                popup.GetComponentsInChildren<Button>().Single(b => b.name == "Apply cell").onClick.Invoke();
                Require(!popup.activeSelf && tab.Document.At(true, 1, 0).Single().Fields[2] == "70.", "Cell popup applies state expression.");
                SetKeys(Key.LeftShift); Click(input, 2, 0, false); SetKeys();
                Require(!popup.activeSelf, "Shift-click on empty editor cell does not open popup.");
                string before = tab.Document.Source;
                tab.WipField.text = "unfinished text"; Click(input, 0, 0, false);
                Require(tab.WipField.text == "unfinished text" && tab.Document.Source == before, "Painting preserves unapplied typed edits.");
                tab.WipField.text = before; tab.ApplyWip();
                tab.Simulation.Clear(); brushes[0].text = "700";
                Click(playground, 0, 0, false);
                Require(tab.Simulation.Queue.Contains(12, 12) && tab.Simulation.Board.Get(12, 12) == 0, "Playground click queues without immediate painting.");
                Click(playground, 0, 0, true);
                Require(!tab.Simulation.Queue.HasSpells, "Right-click removes only queued tile.");
                Click(playground, 0, 0, false);
                Press(Key.Space);
                Require(tab.Simulation.Board.Get(12, 12) == 700 && !tab.Simulation.Queue.HasSpells && tab.Simulation.TickCount == 0, "Space deploys while paused without ticking.");
                Click(playground, 0, 0, true);
                Require(tab.Simulation.Board.Get(12, 12) == 700, "Right-click does not erase deployed tile.");
                int count = tab.Simulation.TickCount;
                Press(Key.A); Press(Key.A);
                Require(tab.Simulation.TickCount == count + 2, "A advances one tick per press, with no cooldown.");
                tab.WipField.ActivateInputField(); tab.WipField.SendMessage("LateUpdate");
                Require(tab.WipField.isFocused, "WIP field receives text focus.");
                count = tab.Simulation.TickCount;
                Press(Key.P); Press(Key.A); Press(Key.Digit9); Press(Key.Space);
                Require(tab.Simulation.Paused && tab.Simulation.TickCount == count &&
                    (int)typeof(ReactionWorkshopTab).GetField("selected", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(tab) == 0,
                    "Typing suppresses pause, step, selection, and deployment hotkeys.");
                tab.WipField.DeactivateInputField(); EventSystem.current.SetSelectedGameObject(null);
                Press(Key.P); Require(!tab.Simulation.Paused, "P resumes clock.");
                Press(Key.P); Require(tab.Simulation.Paused, "P pauses clock.");
                Click(input, 1, 0, true);
                Require(!tab.Document.At(true, 1, 0).Any(), "Right-click immediately erases editor entry.");
                Require(UnityEngine.Object.FindObjectsByType<global::Tile>().Length == 0,
                    "Workshop loads no gameplay tile components.");
                Require(UnityEngine.Object.FindObjectsByType<ParticleSystem>().Length == 0,
                    "Workshop loads no particles.");
                return "PASS: " + checks + " workshop Play-mode UI/input checks.";

                void SetKeys(params Key[] keys) { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.Update(); }
                void Press(Key key) { SetKeys(key); tab.SendMessage("Update"); SetKeys(); }
                void Click(WorkshopGridView grid, int x, int y, bool right)
                {
                    float cellWidth = grid.rectTransform.rect.width / grid.Size;
                    var local = new Vector3((x + grid.Size / 2 + .5f) * cellWidth, -(grid.Size / 2 - y + .5f) * cellWidth);
                    var canvas = grid.canvas;
                    var raycast = new RaycastResult { module = canvas.GetComponent<GraphicRaycaster>() };
                    var data = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, grid.transform.TransformPoint(local)),
                        button = right ? PointerEventData.InputButton.Right : PointerEventData.InputButton.Left, pointerPressRaycast = raycast };
                    grid.OnPointerDown(data);
                }
            }
            finally
            {
                if (keyboard != null) InputSystem.RemoveDevice(keyboard);
                originalKeyboard?.MakeCurrent();
                tab.WipField.text = originalWip; tab.ContextField.text = originalContext;
                tab.ApplyWip(); tab.ApplyContext();
                for (int i = 0; i < 9; i++) brushes[i].text = originalBrushes[i];
                tab.Simulation.Clear(); tab.Deploy();
                if (tab.Simulation.Paused != paused) tab.TogglePause();
                tab.saveDraft = saving;
            }
        }
    }
}
