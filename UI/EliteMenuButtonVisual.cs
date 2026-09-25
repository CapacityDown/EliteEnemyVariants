using HarmonyLib;
using MenuLib.MonoBehaviors;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace REPOJP.EliteEnemyVariants;

// Decorations only: clicks and hover detection remain owned by the game's
// MenuButton, including its scroll-mask and menu-focus checks.
internal sealed class EliteMenuButtonVisual : MonoBehaviour
{
    private static readonly AccessTools.FieldRef<MenuButton, bool> NativeHover =
        AccessTools.FieldRefAccess<MenuButton, bool>("hovering");
    private REPOButton _button = null!;
    private TMP_Text _label = null!;
    private Color _textColor;
    private Image? _background;
    private readonly Image[] _edges = new Image[4];
    private bool _clickable;
    private bool _off;
    private bool _hovered;

    internal void Initialize(REPOButton button, TMP_Text label)
    {
        _button = button;
        _label = label;
        _textColor = label.color;
        enabled = false;
    }

    internal void Configure(bool control, bool clickable, bool off)
    {
        if (control && _background == null) CreateFrame();
        if (_background != null) _background.gameObject.SetActive(control);
        _clickable = clickable;
        _off = off;
        _hovered = false;
        enabled = control;
        if (control) Paint();
        else _label.color = _textColor;
    }

    private void LateUpdate()
    {
        bool hover = _clickable && _button.menuButton.enabled && NativeHover(_button.menuButton);
        if (hover == _hovered) return;
        _hovered = hover;
        Paint();
    }

    private void Paint()
    {
        Color border;
        if (!_clickable)
        {
            _background!.color = new Color(0.075f, 0.08f, 0.09f, 0.9f);
            border = new Color(0.35f, 0.37f, 0.4f, 0.75f);
            _label.color = new Color(0.5f, 0.52f, 0.55f, 1f);
        }
        else if (_off)
        {
            // Blue means OFF but still actionable; gray is reserved for
            // controls without permission/callbacks. Hover stays blue.
            _background!.color = _hovered ? new Color(0.065f, 0.16f, 0.24f, 0.96f)
                : new Color(0.035f, 0.075f, 0.11f, 0.94f);
            border = _hovered ? new Color(0.5f, 0.82f, 1f, 1f)
                : new Color(0.25f, 0.65f, 0.95f, 0.95f);
            _label.color = _hovered ? Color.white : new Color(0.72f, 0.85f, 0.97f, 1f);
        }
        else
        {
            _background!.color = _hovered ? new Color(0.32f, 0.16f, 0.045f, 0.96f)
                : new Color(0.16f, 0.075f, 0.025f, 0.94f);
            border = _hovered ? new Color(1f, 0.75f, 0.3f, 1f)
                : new Color(1f, 0.46f, 0.08f, 0.9f);
            _label.color = _hovered ? Color.white : new Color(1f, 0.88f, 0.7f, 1f);
        }
        foreach (Image edge in _edges) edge.color = border;
    }

    private void CreateFrame()
    {
        _background = Box("Button Background", _button.rectTransform, Vector2.zero, Vector2.one);
        _background.transform.SetAsFirstSibling();
        RectTransform rect = _background.rectTransform;
        _edges[0] = Box("Top Border", rect, new Vector2(0, 1), Vector2.one);
        _edges[1] = Box("Bottom Border", rect, Vector2.zero, new Vector2(1, 0));
        _edges[2] = Box("Left Border", rect, Vector2.zero, new Vector2(0, 1));
        _edges[3] = Box("Right Border", rect, new Vector2(1, 0), Vector2.one);
        _edges[0].rectTransform.sizeDelta = _edges[1].rectTransform.sizeDelta = new Vector2(0, 1.5f);
        _edges[2].rectTransform.sizeDelta = _edges[3].rectTransform.sizeDelta = new Vector2(1.5f, 0);
        _edges[0].rectTransform.pivot = Vector2.one;
        _edges[1].rectTransform.pivot = Vector2.zero;
        _edges[2].rectTransform.pivot = Vector2.zero;
        _edges[3].rectTransform.pivot = Vector2.one;
    }

    private static Image Box(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject obj = new(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.raycastTarget = false;
        RectTransform rect = image.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return image;
    }
}
