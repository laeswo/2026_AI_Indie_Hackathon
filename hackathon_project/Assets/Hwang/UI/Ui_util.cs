using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem.UI;

// uGUI 를 코드로 조립할 때 쓰는 공용 도구. Start_menu 와 Hud 가 같이 쓴다.
// 텍스트는 legacy Text + LegacyRuntime.ttf. TMP 는 리소스 임포트 대화상자가 떠서 안 쓴다.
public static class Ui_util
{

    static Font font;

    // Unity 기본 폰트. 한글은 OS 폰트로 대체돼서 에디터·윈도우 빌드에서 보인다.
    public static Font Font()
    {
        if (font == null) {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        return font;
    }

    // 버튼이 클릭을 받으려면 EventSystem 이 하나 있어야 한다. 새 Input System 을 쓰므로 그쪽 모듈을 붙인다.
    public static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() != null) {
            return;
        }

        GameObject holder = new GameObject("EventSystem");
        holder.AddComponent<UnityEngine.EventSystems.EventSystem>();
        holder.AddComponent<InputSystemUIInputModule>();
    }

    // 화면에 바로 그리는 Canvas. 해상도가 달라도 1920x1080 기준으로 같은 비율로 보이게 한다.
    public static Canvas MakeCanvas(string name, int sorting_order)
    {
        return MakeCanvas(name, sorting_order, true);
    }

    // block_raycast 가 false 면 Raycaster 를 안 붙인다. 플래시처럼 덮기만 하고 클릭은 통과시켜야 하는 Canvas 용.
    public static Canvas MakeCanvas(string name, int sorting_order, bool block_raycast)
    {
        GameObject holder = new GameObject(name);

        Canvas canvas = holder.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sorting_order;

        CanvasScaler scaler = holder.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (block_raycast) {
            holder.AddComponent<GraphicRaycaster>();
        }
        return canvas;
    }

    public static Image MakeImage(RectTransform parent, string name, Color color)
    {
        GameObject holder = new GameObject(name);
        holder.transform.SetParent(parent, false);

        Image image = holder.AddComponent<Image>();
        image.color = color;
        return image;
    }

    public static Text MakeText(RectTransform parent, string name, string content, int size, Color color, FontStyle style)
    {
        return MakeText(parent, name, content, size, color, style, TextAnchor.MiddleCenter);
    }

    public static Text MakeText(RectTransform parent, string name, string content, int size, Color color, FontStyle style, TextAnchor alignment)
    {
        GameObject holder = new GameObject(name);
        holder.transform.SetParent(parent, false);

        Text text = holder.AddComponent<Text>();
        text.font = Font();
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    // 글자 뒤에 그림자를 깐다. 예전에 "그림자 한 번, 본체 한 번" 그리던 것과 같은 느낌.
    public static Shadow AddShadow(Text text)
    {
        Shadow shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.7f);
        shadow.effectDistance = new Vector2(2f, -2f);
        return shadow;
    }

    public static Button MakeButton(RectTransform parent, string name, string label, Vector2 position, Vector2 size, int font_size,
        Color color, Color label_color, UnityEngine.Events.UnityAction on_click)
    {
        Image image = MakeImage(parent, name, color);
        Place(image.rectTransform, position, size);

        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        // 마우스를 올리면 밝아지게.
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.3f, 1.3f, 1.3f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        button.colors = colors;

        button.onClick.AddListener(on_click);

        Text text = MakeText(image.rectTransform, "Label", label, font_size, label_color, FontStyle.Bold);
        Stretch(text.rectTransform);

        return button;
    }

    // 부모를 꽉 채운다.
    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // 화면 가운데 기준으로 놓는다.
    public static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
    }

    // 앵커·피벗을 정해서 놓는다. 좌상단이면 anchor (0,1) pivot (0,1) 에 position (x, -y).
    public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    // 가로는 부모의 비율(anchor_x_min~max)로 늘리고, 세로는 위 기준 고정 높이로 놓는다.
    // 드래곤 HP 바처럼 화면 폭에 비례하는 것에 쓴다.
    public static void PlaceHorizontalStretch(RectTransform rect, float anchor_x_min, float anchor_x_max, float top, float height)
    {
        rect.anchorMin = new Vector2(anchor_x_min, 1f);
        rect.anchorMax = new Vector2(anchor_x_max, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.offsetMin = new Vector2(0f, -top - height);
        rect.offsetMax = new Vector2(0f, -top);
    }
}
