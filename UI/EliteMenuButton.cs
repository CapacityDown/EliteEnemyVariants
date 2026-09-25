using System;
using MenuLib.MonoBehaviors;
using UnityEngine;

namespace REPOJP.EliteEnemyVariants;

internal sealed class EliteMenuButton : MonoBehaviour
{
    private REPOButton _button = null!;
    private Transform _parent = null!;
    private float _nextProbe;
    private readonly Vector3[] _corners = new Vector3[4];
    internal void Initialize(REPOButton button, Transform parent) { _button = button; _parent = parent; Place(); }
    private void LateUpdate()
    {
        if (Time.unscaledTime < _nextProbe) return;
        _nextProbe = Time.unscaledTime + 0.25f;
        Place();
    }
    private void Place()
    {
        if (_button == null || _parent is not RectTransform parent) return;
        float rolesBottom = 0, eventsBottom = 0;
        foreach (REPOButton candidate in _parent.GetComponentsInChildren<REPOButton>(true))
        {
            if (candidate == _button || candidate.transform.parent != _button.transform.parent ||
                !candidate.gameObject.activeSelf || candidate.labelTMP == null) continue;
            string name = candidate.labelTMP.text.Trim();
            bool roles = name.Equals("Roles", StringComparison.OrdinalIgnoreCase);
            bool events = name.Equals("Events", StringComparison.OrdinalIgnoreCase);
            if (!roles && !events) continue;
            candidate.rectTransform.GetWorldCorners(_corners);
            float bottom = parent.rect.yMax - parent.InverseTransformPoint(_corners[0]).y;
            if (roles) rolesBottom = Mathf.Max(rolesBottom, bottom);
            if (events) eventsBottom = Mathf.Max(eventsBottom, bottom);
        }
        Vector2 size = _button.labelTMP.GetPreferredValues("Elites");
        size.y = Mathf.Max(26f, size.y);
        _button.overrideButtonSize = size;
        _button.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Right, 36f, size.x);
        _button.rectTransform.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top,
            EliteMenuPolicy.ButtonTop(rolesBottom, eventsBottom), size.y);
    }
}
