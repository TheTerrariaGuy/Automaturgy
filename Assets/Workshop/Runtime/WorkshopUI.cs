using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GridMage.Workshop
{
    public static class WorkshopUI
    {
        public static readonly Color Surface = new Color32(29, 38, 53, 255);
        public static readonly Color Accent = new Color32(103, 226, 196, 255);
        public static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        public static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
            return rect;
        }
        public static Text Label(Transform parent, string caption, float x, float y, float w, float h, int size = 18)
        {
            var text = Rect(parent, caption.Length < 40 ? caption : "Text", x, y, w, h).gameObject.AddComponent<Text>();
            text.font = Font; text.fontSize = size; text.text = caption; text.color = new Color32(224, 233, 244, 255);
            text.raycastTarget = false; text.supportRichText = false;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            return text;
        }
        public static Image Panel(Transform parent, string name, float x, float y, float w, float h, Color color)
        { var image = Rect(parent, name, x, y, w, h).gameObject.AddComponent<Image>(); image.color = color; return image; }
        public static Button Button(Transform parent, string caption, float x, float y, float w, UnityAction clicked)
        {
            var image = Panel(parent, caption, x, y, w, 36, Surface);
            var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(.65f, .9f, .85f); button.colors = colors;
            var label = Label(image.transform, caption, 4, 0, w - 8, 36, 16); label.alignment = TextAnchor.MiddleCenter;
            button.onClick.AddListener(clicked);
            var navigation = button.navigation; navigation.mode = Navigation.Mode.None; button.navigation = navigation;
            return button;
        }
        public static InputField Input(Transform parent, string name, string value, float x, float y, float w, float h, bool multiline = false)
        {
            var image = Panel(parent, name, x, y, w, h, new Color32(18, 25, 37, 255));
            var input = image.gameObject.AddComponent<InputField>(); input.targetGraphic = image;
            var text = Label(image.transform, "", 9, 5, w - 18, h - 10, 16);
            text.alignment = multiline ? TextAnchor.UpperLeft : TextAnchor.MiddleLeft;
            text.horizontalOverflow = multiline ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            image.gameObject.AddComponent<RectMask2D>();
            input.textComponent = text; input.lineType = multiline ? InputField.LineType.MultiLineNewline : InputField.LineType.SingleLine;
            input.customCaretColor = true; input.caretColor = Accent;
            input.selectionColor = new Color(.2f, .65f, .6f, .5f); input.text = value;
            return input;
        }
    }
}
