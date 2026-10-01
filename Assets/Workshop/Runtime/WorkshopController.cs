using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace GridMage.Workshop
{
    public sealed class WorkshopController : MonoBehaviour
    {
        [SerializeField] private Camera uiCamera;
        [SerializeField] private List<WorkshopTab> tabs = new List<WorkshopTab>();
        private readonly List<Button> buttons = new List<Button>();
        public void Configure(Camera camera, WorkshopTab firstTab) { uiCamera = camera; tabs.Add(firstTab); }
        private void Awake()
        {
            var canvas = new GameObject("Workshop UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = uiCamera; canvas.planeDistance = 1;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600, 1050); scaler.matchWidthOrHeight = .5f;
            // Fit the complete authoring surface at narrower/wider Game View aspect ratios.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var root = WorkshopUI.Rect(canvas.transform, "Workspace", 0, 0, 1600, 1050);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f); root.anchoredPosition = Vector2.zero;
            WorkshopUI.Panel(root, "Background", 0, 0, 1600, 1050, new Color32(13, 19, 29, 255));
            WorkshopUI.Label(root, "GRID MAGE  /  WORKSHOP", 24, 10, 520, 35, 24);
            for (int i = 0; i < tabs.Count; i++)
            {
                int index = i;
                buttons.Add(WorkshopUI.Button(root, tabs[i].Title, 1000 + i * 155, 12, 145, () => Select(index)));
                tabs[i].Initialize(WorkshopUI.Rect(root, tabs[i].Title + " panel", 0, 65, 1600, 980));
            }
            if (EventSystem.current == null)
            {
                var events = new GameObject("Workshop EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                events.transform.SetParent(transform, false);
            }
            Select(0);
        }
        public void Select(int index)
        {
            for (int i = 0; i < tabs.Count; i++)
            {
                tabs[i].SetActive(i == index);
                buttons[i].GetComponent<Image>().color = i == index ? new Color32(42, 95, 85, 255) : WorkshopUI.Surface;
            }
            EventSystem.current?.SetSelectedGameObject(null);
        }
    }
}
