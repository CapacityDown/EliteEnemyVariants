using System;
using System.Collections.Generic;
using MenuLib;
using MenuLib.MonoBehaviors;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace REPOJP.EliteEnemyVariants;

internal sealed class EliteMenu : MonoBehaviour
{
    private static EliteMenu? _instance;
    private EliteMenuState _settings = null!;
    private REPOPopupPage? _page;
    private readonly List<Row> _rows = new();
    private readonly Dictionary<View, REPOButton> _navigation = new();
    private View _view;
    private string _signature = "", _message = "";
    private float _nextRefresh;
    internal static bool OwnsScrollBox(MenuScrollBox box) => _instance != null && _instance._page != null && _instance._page.menuScrollBox == box;
    internal void Initialize(EliteMenuState settings)
    {
        _settings = settings; _instance = this;
        MenuAPI.AddElementToEscapeMenu(AddButton);
        MenuAPI.AddElementToLobbyMenu(AddButton);
    }
    private void AddButton(Transform parent)
    {
        REPOButton button = MenuAPI.CreateREPOButton("Elites", Open, parent, Vector2.zero);
        button.gameObject.AddComponent<EliteMenuButton>().Initialize(button, parent);
    }
    private void Open()
    {
        if (_page != null) return;
        REPOPopupPage page = MenuAPI.CreateREPOPopupPage("Elites", REPOPopupPage.PresetSide.Right,
            shouldCachePage: false, pageDimmerVisibility: true, spacing: 2f);
        _page = page; _rows.Clear(); _navigation.Clear(); _message = "";
        var padding = page.maskPadding; padding.top = 24f; padding.bottom = 0; page.maskPadding = padding;
        page.AddElement(parent =>
        {
            Navigation(View.Guide, "ENHANCEMENT GUIDE", parent, 268f);
            Navigation(View.Settings, "ENEMY SETTINGS", parent, 230f);
            Navigation(View.Tools, "TOOLS", parent, 192f);
            REPOLabel version = MenuAPI.CreateREPOLabel("EliteEnemyVariants v" + EliteEnemyVariantsPlugin.PluginVersion, parent, new Vector2(108, 72));
            version.rectTransform.sizeDelta = new Vector2(190, 50);
            version.labelTMP.rectTransform.sizeDelta = new Vector2(190, 50);
            version.labelTMP.fontSize = 15; version.labelTMP.textWrappingMode = TextWrappingModes.Normal;
            version.labelTMP.alignment = TextAlignmentOptions.Left;
            MenuAPI.CreateREPOButton("Back", () => { Forget(); page.ClosePage(true); }, parent, new Vector2(66, 18));
        });
        page.onEscapePressed += () =>
        {
            if (_page == null || _page != page) return false;
            if (_view == View.Issues) { Switch(View.Report); return false; }
            Forget(); return true;
        };
        _view = View.Guide;
        Refresh();
        page.OpenPage(openOnTop: false);
    }
    private void Navigation(View view, string title, Transform parent, float y)
    {
        REPOButton button = MenuAPI.CreateREPOButton(title, () => Switch(view), parent, new Vector2(108, y));
        button.overrideButtonSize = new Vector2(190, 32);
        button.labelTMP.alignment = TextAlignmentOptions.Left; button.labelTMP.fontSize = 18;
        _navigation.Add(view, button);
    }
    private void Switch(View view)
    {
        if (_page == null) return;
        _view = view; _message = "";
        _page.scrollView.SetScrollPosition(0);
        Refresh();
    }
    private void Update()
    {
        if (_page == null || Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + 0.5f;
        if (Signature() != _signature) Refresh();
    }
    private string Signature()
    {
        bool available = _settings.TryRead(out long mask, out bool active);
        return $"{_view}:{EliteMenuState.CanEdit}:{available}:{mask}:{active}:{_settings.Status}";
    }
    private void Refresh()
    {
        if (_page == null) return;
        List<Entry> entries = new();
        void Text(string text, bool title = false) => entries.Add(new Entry(text, title: title));
        void Button(string text, Action action, bool allowed = true) => entries.Add(new Entry(text, allowed ? action : null, true));
        bool editable = EliteMenuState.CanEdit;
        bool available = _settings.TryRead(out long mask, out bool active);
        if (_message.Length > 0) Text(_message);
        switch (_view)
        {
            case View.Guide:
                Text("Enhanced enemies keep their own signature behavior and gain new ways to pressure players. The descriptions below use default settings; detailed values can be changed in REPOConfig.");
                Text("Green flashes identify Enhanced enemies. Their defeated-enemy orbs are worth 1.5 times the usual value. No enemy locations or encounter counts are revealed here.");
                foreach (EliteArchetype enemy in EliteGuideCatalog.Enemies)
                { Text(EnemyDisplayNameResolver.Get(enemy), true); Text(EliteGuideCatalog.Description(enemy)); }
                break;
            case View.Settings:
                Text("Changes affect future enhancement draws. Existing enemies and already selected encounters remain unchanged. Chances and detailed settings are available in REPOConfig.");
                Text(editable ? "You are editing the host's enhancement selection." : "Only the host can change enhancement settings.");
                if (!available) { Text(_settings.Status); break; }
                if (!active) Text("EliteEnemyVariants is disabled in General settings. These switches are saved for when it is enabled.");
                foreach (EliteArchetype enemy in EliteGuideCatalog.Enemies)
                {
                    EliteArchetype captured = enemy;
                    bool on = (mask & (1L << (int)enemy)) != 0;
                    Button($"[{(on ? "ON" : "OFF")}] {EnemyDisplayNameResolver.Get(enemy)}", () =>
                    {
                        // Recheck current authority and config at click time, not only when rendering.
                        if (EliteMenuState.CanEdit && _settings.TryRead(out long current, out _))
                            _settings.SetEnabled(captured, (current & (1L << (int)captured)) == 0);
                    }, editable);
                }
                break;
            case View.Tools:
                Text("DISPLAY STATUS", true); Text(_settings.Status);
                Button("REFRESH DISPLAY DATA", () => { _settings.RefreshDisplay(); _message = "Display refresh requested. No enemies are changed or reselected."; });
                Text("Elites has no persistent HUD. There are no enemy counters or positions to edit.");
                Button("REPORT A PROBLEM", () => Switch(View.Report));
                break;
            case View.Report:
                Text("Create a report, review it, and paste it into GitHub Issues. Personal information is partly masked. Nothing is uploaded automatically.");
                Button("COPY REPORT", () =>
                {
                    EliteBugReport report = EliteEnemyVariantsPlugin.Instance.BugReport;
                    report.Create(_settings);
                    GUIUtility.systemCopyBuffer = report.LatestText;
                    _message = "Report copied and saved to BepInEx/EliteEnemyVariantsReports. Add reproduction steps before sharing.";
                });
                Button("OPEN SAVED REPORT", () =>
                {
                    EliteBugReport report = EliteEnemyVariantsPlugin.Instance.BugReport;
                    report.Create(_settings);
                    Application.OpenURL(new Uri(report.LatestPath).AbsoluteUri);
                    _message = "Report saved. Review it before sharing.";
                });
                Button("OPEN GITHUB ISSUES", () => Switch(View.Issues));
                break;
            case View.Issues:
                Text("Open EliteEnemyVariants Issues on GitHub in your browser? A GitHub account is required to submit a report. Nothing will be sent automatically.");
                Text(EliteBugReport.IssuesUrl);
                Button("OPEN IN BROWSER", () => { Application.OpenURL(EliteBugReport.IssuesUrl); Switch(View.Report); });
                Button("CANCEL", () => Switch(View.Report));
                break;
        }
        _page.headerTMP.text = _view switch { View.Guide => "Enhancement Guide", View.Settings => "Enemy Settings", View.Tools => "Tools", View.Report => "Bug Report", _ => "GitHub Issues" };
        foreach (var pair in _navigation)
        {
            bool selected = pair.Key == _view || pair.Key == View.Tools && _view is View.Report or View.Issues;
            pair.Value.labelTMP.color = selected ? new Color(0.5f, 1f, 0.6f) : Color.white;
        }
        Apply(entries);
        _signature = Signature();
    }
    private void Apply(List<Entry> entries)
    {
        REPOPopupPage page = _page!;
        float width = Mathf.Clamp(page.maskRectTransform.rect.width - 20f, 260f, 520f);
        while (_rows.Count < entries.Count) _rows.Add(CreateRow(page));
        for (int i = 0; i < _rows.Count; i++)
        {
            Row row = _rows[i];
            row.Element.visibility = i < entries.Count;
            if (i >= entries.Count) continue;
            Entry entry = entries[i];
            TMP_Text text = row.Label.labelTMP;
            text.text = entry.Text; text.richText = false; text.fontSize = entry.Control || entry.Title ? 20 : 18;
            text.fontStyle = entry.Title ? FontStyles.Bold : FontStyles.Normal;
            text.textWrappingMode = TextWrappingModes.Normal; text.enableAutoSizing = false; text.overflowMode = TextOverflowModes.Truncate;
            float height = Mathf.Max(entry.Control ? 40f : 30f, text.GetPreferredValues(entry.Text, width - 22f, 0).y + 14f);
            text.alignment = TextAlignmentOptions.Left;
            text.color = entry.Control && entry.Click == null ? new Color(0.5f, 0.52f, 0.55f) : Color.white;
            row.Button.overrideButtonSize = new Vector2(width, height);
            row.Button.rectTransform.sizeDelta = row.Focus.sizeDelta = new Vector2(width, height);
            row.Label.rectTransform.anchoredPosition = new Vector2(10, 0);
            row.Label.rectTransform.sizeDelta = text.rectTransform.sizeDelta = new Vector2(width - 22f, height);
            row.Visual.Configure(entry.Control, entry.Click != null, entry.Text.StartsWith("[OFF]", StringComparison.Ordinal));
            row.Button.menuButton.enabled = entry.Click != null;
            row.Button.onClick = entry.Click == null ? null : () =>
            {
                if (_page != page) return;
                try { entry.Click(); }
                catch (Exception exception) { _message = "Operation failed. Check the EliteEnemyVariants log."; EliteEnemyVariantsPlugin.ModLogger.LogError(exception); }
                if (_page == page) Refresh();
            };
        }
        page.scrollView.UpdateElements();
    }
    private static Row CreateRow(REPOPopupPage page)
    {
        REPOButton button = null!; REPOLabel label = null!;
        page.AddElementToScrollView(parent =>
        {
            button = MenuAPI.CreateREPOButton("", () => { }, parent, Vector2.zero);
            label = MenuAPI.CreateREPOLabel("", button.rectTransform, Vector2.zero);
            return button.rectTransform;
        }, topPadding: 4f, bottomPadding: 4f);
        button.labelTMP.gameObject.SetActive(false);
        button.menuButton.customHoverArea = true; button.menuButton.resizeButton = false;
        label.labelTMP.raycastTarget = false;
        BottomLeft(label.rectTransform); BottomLeft(label.labelTMP.rectTransform);
        GameObject focus = new("Elite Focus", typeof(RectTransform), typeof(MenuSelectableElement));
        focus.transform.SetParent(button.rectTransform, false);
        RectTransform focusRect = (RectTransform)focus.transform; BottomLeft(focusRect);
        button.menuButton.rectTransformSelection = focusRect;
        EliteMenuButtonVisual visual = button.gameObject.AddComponent<EliteMenuButtonVisual>();
        visual.Initialize(button, label.labelTMP);
        return new Row(button, label, button.GetComponent<REPOScrollViewElement>(), visual, focusRect);
    }
    private static void BottomLeft(RectTransform rect)
    { rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero; rect.anchoredPosition = Vector2.zero; }
    private void Forget() { _page = null; _rows.Clear(); _navigation.Clear(); }
    private void OnDestroy() { if (_page != null) _page.ClosePage(true); if (_instance == this) _instance = null; }
    private enum View { Guide, Settings, Tools, Report, Issues }
    private sealed class Entry(string text, Action? click = null, bool control = false, bool title = false)
    { internal readonly string Text = text; internal readonly Action? Click = click; internal readonly bool Control = control, Title = title; }
    private sealed class Row(REPOButton button, REPOLabel label, REPOScrollViewElement element, EliteMenuButtonVisual visual, RectTransform focus)
    { internal readonly REPOButton Button = button; internal readonly REPOLabel Label = label; internal readonly REPOScrollViewElement Element = element; internal readonly EliteMenuButtonVisual Visual = visual; internal readonly RectTransform Focus = focus; }
}
