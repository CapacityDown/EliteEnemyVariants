using System;
using System.Collections.Generic;
using System.Text;
using MenuLib;
using MenuLib.MonoBehaviors;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace REPOJP.EliteEnemyVariants;

internal sealed partial class EliteMenu : MonoBehaviour
{
    private static EliteMenu? _instance;
    private EliteMenuState _settings = null!;
    private REPOPopupPage? _page;
    private readonly List<Row> _rows = new();
    private readonly Dictionary<View, REPOButton> _navigation = new();
    private REPOButton? _back;
    private EliteLanguage _language;
    private string T(string key) => EliteText.Get(key, _language);
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
            shouldCachePage: false, pageDimmerVisibility: true, spacing: 1.5f);
        _page = page; _rows.Clear(); _navigation.Clear(); _message = "";
        var padding = page.maskPadding; padding.top = 24f; padding.bottom = 0; page.maskPadding = padding;
        page.AddElement(parent =>
        {
            Navigation(View.Guide, "", parent, 272f);
            Navigation(View.Settings, "", parent, 240f);
            Navigation(View.Tools, "", parent, 208f);
            REPOLabel version = MenuAPI.CreateREPOLabel(EliteBuild.Label, parent, new Vector2(108, 54));
            version.rectTransform.sizeDelta = new Vector2(190, 24);
            version.labelTMP.rectTransform.sizeDelta = new Vector2(190, 24);
            version.labelTMP.fontSize = 16; version.labelTMP.textWrappingMode = TextWrappingModes.NoWrap;
            version.labelTMP.enableAutoSizing = true; version.labelTMP.fontSizeMin = 10; version.labelTMP.fontSizeMax = 16;
            version.labelTMP.color = new Color(0.72f, 0.72f, 0.72f, 0.9f);
            version.labelTMP.alignment = TextAlignmentOptions.Left;
            _back = MenuAPI.CreateREPOButton("Back", () => { Forget(); page.ClosePage(true); }, parent, new Vector2(66, 18));
        });
        page.onEscapePressed += () =>
        {
            if (_page == null || _page != page) return false;
            // MenuLib sends Escape to inactive pages as well as the native popup.
            if (IssuesInputBlocked) return false;
            Forget(); return true;
        };
        _view = View.Guide;
        try { Refresh(); page.OpenPage(openOnTop: false); }
        catch (Exception exception) { Forget(); page.ClosePage(true); EliteEnemyVariantsPlugin.ModLogger.LogError(exception); }
    }
    private void Navigation(View view, string title, Transform parent, float y)
    {
        REPOButton button = MenuAPI.CreateREPOButton(title, () => Switch(view), parent, new Vector2(108, y));
        button.overrideButtonSize = new Vector2(190, 32);
        button.labelTMP.alignment = TextAlignmentOptions.Left; button.labelTMP.fontSize = 20;
        _navigation.Add(view, button);
    }
    private void Switch(View view)
    {
        if (_page == null || IssuesInputBlocked) return;
        _view = view; _message = "";
        _page.scrollView.SetScrollPosition(0);
        Refresh();
    }
    private void Update()
    {
        if (_page == null) { CloseIssuesDialog(); return; }
        if (Time.unscaledTime < _nextRefresh || IssuesInputBlocked) return;
        _nextRefresh = Time.unscaledTime + 0.25f;
        if (Signature() != _signature) Refresh();
    }
    private string Signature()
    {
        bool available = _settings.TryRead(out long mask, out bool active);
        return $"{EliteGameLocale.Code}:{_page?.maskRectTransform.rect.width}:{_view}:{EliteMenuState.CanEdit}:{available}:{mask}:{active}:{_settings.Status}";
    }
    private void Refresh()
    {
        if (_page == null) return;
        _language = EliteGameLocale.Language;
        List<Entry> entries = new();
        void Text(string key, bool title = false)
        {
            string text = T(key);
            if (title) entries.Add(new Entry(text, title: true));
            else foreach (string line in Wrap(text)) entries.Add(new Entry(line));
        }
        void Button(string key, Action action, bool allowed = true, bool off = false, bool literal = false) =>
            entries.Add(new Entry(literal ? key : T(key), allowed ? action : null, true, off: off));
        bool editable = EliteMenuState.CanEdit;
        bool available = _settings.TryRead(out long mask, out bool active);
        if (_message.Length > 0) Text(_message);
        switch (_view)
        {
            case View.Guide:
                Text("GuideIntro"); Text("Markers");
                foreach (EliteArchetype enemy in EliteGuideCatalog.Enemies)
                { entries.Add(new Entry("", space: true)); Text(EnemyDisplayNameResolver.Get(enemy), true); Text("Enemy." + enemy); }
                break;
            case View.Settings:
                Text("SelectionHelp"); Text(editable ? "HostEdit" : "GuestRead");
                if (!available) { Text(StatusKey()); break; }
                if (!active) Text("Disabled");
                foreach (EliteArchetype enemy in EliteGuideCatalog.Enemies)
                {
                    EliteArchetype captured = enemy;
                    bool on = (mask & (1L << (int)enemy)) != 0;
                    Button($"[{T(on ? "On" : "Off")}] {EnemyDisplayNameResolver.Get(enemy)}", () =>
                    {
                        // Recheck current authority and config at click time, not only when rendering.
                        if (EliteMenuState.CanEdit && _settings.TryRead(out long current, out _))
                            _settings.SetEnabled(captured, (current & (1L << (int)captured)) == 0);
                    }, editable, !on, true);
                }
                break;
            case View.Tools:
                Text("Status", true); Text(StatusKey());
                Button("Refresh", () => { _settings.RefreshDisplay(); _message = "RefreshDone"; });
                Text("NoHud");
                Button("ReportButton", () => Switch(View.Report));
                break;
            case View.Report:
                Text("ReportHelp");
                Button("Copy", () =>
                {
                    EliteBugReport report = EliteEnemyVariantsPlugin.Instance.BugReport;
                    report.Create(_settings);
                    GUIUtility.systemCopyBuffer = report.LatestText;
                    _message = "CopyDone";
                });
                Button("OpenReport", () =>
                {
                    EliteBugReport report = EliteEnemyVariantsPlugin.Instance.BugReport;
                    report.Create(_settings);
                    Application.OpenURL(new Uri(report.LatestPath).AbsoluteUri);
                    _message = "Saved";
                });
                REPOPopupPage reportPage = _page;
                Button("OpenIssues", () => ConfirmOpenIssues(reportPage));
                break;
        }
        Style(_page.headerTMP, 32, 18);
        _page.headerTMP.text = T(_view.ToString());
        if (_back != null) { _back.labelTMP.text = T("Back"); Style(_back.labelTMP, 20, 12); }
        foreach (var pair in _navigation)
        {
            bool selected = pair.Key == _view || pair.Key == View.Tools && _view == View.Report;
            pair.Value.labelTMP.text = (selected ? "> " : "  ") + T(pair.Key.ToString());
            pair.Value.labelTMP.color = Color.white;
            Style(pair.Value.labelTMP, 20, 12);
        }
        Apply(entries);
        _signature = Signature();
    }
    private void Apply(List<Entry> entries)
    {
        REPOPopupPage page = _page!;
        View view = _view;
        float width = Mathf.Clamp(page.maskRectTransform.rect.width - 20f, 260f, 520f);
        while (_rows.Count < entries.Count) _rows.Add(CreateRow(page));
        for (int i = 0; i < _rows.Count; i++)
        {
            Row row = _rows[i];
            row.Element.visibility = i < entries.Count;
            if (i >= entries.Count) { row.Separator.gameObject.SetActive(false); continue; }
            Entry entry = entries[i];
            TMP_Text text = row.Label.labelTMP;
            text.text = entry.Text; text.richText = false; text.fontSize = entry.Control || entry.Title ? 20 : 18;
            text.font = EliteGuideFont.ForLanguage(row.DefaultFont, _language);
            EliteTextLayout.Apply(text, _language);
            text.fontStyle = entry.Title ? FontStyles.Bold : FontStyles.Normal;
            text.textWrappingMode = TextWrappingModes.NoWrap; text.enableAutoSizing = entry.Control || entry.Title;
            text.fontSizeMin = 12; text.fontSizeMax = entry.Control || entry.Title ? 20 : 18;
            text.overflowMode = TextOverflowModes.Overflow;
            float height = entry.Space ? 10 : entry.Control ? 32 : entry.Title ? 30 : 25;
            text.alignment = _language == EliteLanguage.Arabic ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
            text.color = entry.Control && entry.Click == null ? new Color(0.5f, 0.52f, 0.55f) : Color.white;
            row.Button.overrideButtonSize = new Vector2(width, height);
            row.Button.rectTransform.sizeDelta = new Vector2(width, height);
            float focusHeight = entry.Control ? height : Mathf.Max(12, height * 0.5f);
            row.Focus.sizeDelta = new Vector2(width, focusHeight);
            row.Focus.anchoredPosition = new Vector2(0, (height - focusHeight) * 0.5f);
            float padding = entry.Control ? 12 : 0;
            row.Label.rectTransform.anchoredPosition = new Vector2(padding, 0);
            row.Label.rectTransform.sizeDelta = text.rectTransform.sizeDelta = new Vector2(width - padding * 2, height);
            row.Visual.Configure(entry.Control, entry.Click != null, entry.Off);
            row.Separator.gameObject.SetActive(_view != View.Guide && i > 0 && entry.Control && !entries[i - 1].Control);
            row.Separator.rectTransform.sizeDelta = new Vector2(width, 1);
            row.Separator.rectTransform.anchoredPosition = new Vector2(0, height + 2);
            row.Button.menuButton.enabled = entry.Click != null;
            row.Button.onClick = entry.Click == null ? null : () =>
            {
                if (_page != page || _view != view || IssuesInputBlocked) return;
                try { entry.Click(); }
                catch (Exception exception) { _message = "Failed"; EliteEnemyVariantsPlugin.ModLogger.LogError(exception); }
                if (_page == page && _view == view && !IssuesInputBlocked) Refresh();
            };
        }
        page.scrollView.UpdateElements();
    }
    private Row CreateRow(REPOPopupPage page)
    {
        REPOButton button = null!; REPOLabel label = null!;
        page.AddElementToScrollView(parent =>
        {
            button = MenuAPI.CreateREPOButton("", () => { }, parent, Vector2.zero);
            label = MenuAPI.CreateREPOLabel("", button.rectTransform, Vector2.zero);
            return button.rectTransform;
        }, topPadding: _rows.Count == 0 ? 20f : 3f, bottomPadding: 3f);
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
        GameObject divider = new("Section Divider", typeof(RectTransform), typeof(Image));
        divider.transform.SetParent(button.rectTransform, false);
        Image separator = divider.GetComponent<Image>(); separator.raycastTarget = false;
        separator.color = new Color(0.65f, 0.4f, 0.16f, 0.6f); BottomLeft(separator.rectTransform);
        divider.SetActive(false);
        return new Row(button, label, button.GetComponent<REPOScrollViewElement>(), visual, focusRect, separator);
    }
    private static void BottomLeft(RectTransform rect)
    { rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero; rect.anchoredPosition = Vector2.zero; }
    private void Style(TMP_Text text, float max, float min)
    {
        text.font = EliteGuideFont.ForLanguage(text.font, _language); EliteTextLayout.Apply(text, _language);
        text.alignment = _language == EliteLanguage.Arabic ? TextAlignmentOptions.Right : TextAlignmentOptions.Left;
        text.textWrappingMode = TextWrappingModes.NoWrap; text.enableAutoSizing = true;
        text.fontSizeMax = max; text.fontSizeMin = min;
    }
    private string StatusKey() => _settings.Status switch
    {
        "Host - sharing enhancement settings." => "StatusHost",
        "Single player - local enhancement settings." => "StatusSolo",
        "Connected - host enhancement settings received." => "StatusGuest",
        _ => "StatusUnavailable"
    };
    private IReadOnlyList<string> Wrap(string text)
    {
        if (_rows.Count == 0) _rows.Add(CreateRow(_page!));
        TMP_Text measure = _rows[0].Label.labelTMP;
        measure.font = EliteGuideFont.ForLanguage(_rows[0].DefaultFont, _language);
        EliteTextLayout.Apply(measure, _language);
        measure.fontSize = 18; measure.fontStyle = FontStyles.Normal; measure.enableAutoSizing = false;
        measure.textWrappingMode = TextWrappingModes.NoWrap;
        float width = Mathf.Clamp(_page!.maskRectTransform.rect.width - 20f, 260f, 520f);
        List<string> lines = new(); StringBuilder line = new();
        foreach (string token in EliteTextLayout.Tokens(text))
        {
            if (token == "\n") { if (line.Length > 0) lines.Add(line.ToString().TrimEnd()); line.Clear(); continue; }
            if (line.Length == 0 && string.IsNullOrWhiteSpace(token)) continue;
            string candidate = line + token;
            if (line.Length > 0 && measure.GetPreferredValues(candidate).x > width)
            { lines.Add(line.ToString().TrimEnd()); line.Clear(); }
            if (line.Length > 0 || !string.IsNullOrWhiteSpace(token)) line.Append(token);
        }
        if (line.Length > 0) lines.Add(line.ToString().TrimEnd());
        return lines;
    }
    private void Forget() { _page = null; CloseIssuesDialog(); _back = null; _rows.Clear(); _navigation.Clear(); }
    private void OnDestroy()
    {
        REPOPopupPage? page = _page;
        Forget();
        if (page != null) page.ClosePage(true);
        if (_instance == this) _instance = null;
    }
    private enum View { Guide, Settings, Tools, Report }
    private sealed class Entry(string text, Action? click = null, bool control = false, bool title = false, bool off = false, bool space = false)
    { internal readonly string Text = text; internal readonly Action? Click = click; internal readonly bool Control = control, Title = title, Off = off, Space = space; }
    private sealed class Row(REPOButton button, REPOLabel label, REPOScrollViewElement element, EliteMenuButtonVisual visual, RectTransform focus, Image separator)
    { internal readonly REPOButton Button = button; internal readonly REPOLabel Label = label; internal readonly REPOScrollViewElement Element = element; internal readonly EliteMenuButtonVisual Visual = visual; internal readonly RectTransform Focus = focus; internal readonly Image Separator = separator; internal readonly TMP_FontAsset DefaultFont = label.labelTMP.font; }
}
