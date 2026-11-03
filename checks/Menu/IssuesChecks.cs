using MenuLib;
using MenuLib.MonoBehaviors;
using TMPro;
using UnityEngine;

namespace REPOJP.EliteEnemyVariants;

// Compile the production dialog partial against deterministic native UI stubs.
internal sealed partial class EliteMenu
{
    private REPOPopupPage? _page;
    private View _view;
    private EliteLanguage _language = EliteLanguage.Japanese;
    private string _message = "", _signature = "unchanged";
    private enum View { Report, Tools }
    private string T(string key) => "localized:" + key;

    internal static void CheckIssues(Action<bool, string> check)
    {
        EliteMenu New()
        {
            Time.frameCount++;
            return new() { _page = new(), _view = View.Report };
        }
        void Show(EliteMenu menu) => menu.ConfirmOpenIssues(menu._page!);
        var menu = New();
        var source = menu._page;
        Show(menu);
        MenuPage popup = MenuManager.instance.Current;
        check(MenuAPI.Opens == 1 && Application.Opened.Count == 0, "confirmation never opens URL automatically");
        check(MenuAPI.Header == "localized:ExternalSite" && MenuAPI.Color == new Color(1f, 0.65f, 0f), "Roles native confirmation heading/color");
        check(MenuAPI.Body == "localized:ConfirmIssues\n\n" + EliteBugReport.IssuesUrl, "confirmation shows exact destination");
        check(popup.Dialog.option1Button.buttonTextString == "localized:Browser" && popup.Dialog.option2Button.buttonTextString == "localized:Cancel", "localized native choice labels");
        check(popup.GetComponentsInChildren<TextMeshProUGUI>(true).All(t => t.Shaped && t.font == EliteGuideFont.Font), "Roles fonts and shaping applied to all popup labels");
        check(popup.Dialog.bodyTextMesh.textWrappingMode == TextWrappingModes.Normal && popup.Dialog.bodyTextMesh.enableAutoSizing && popup.Dialog.bodyTextMesh.fontSizeMin == 12 && popup.Dialog.bodyTextMesh.fontSizeMax == 18, "Roles native body sizing");
        Show(menu);
        check(MenuAPI.Opens == 1 && menu.IssuesInputBlocked, "repeated click cannot stack dialogs");
        MenuAPI.Cancel(); MenuAPI.Confirm();
        check(Application.Opened.Count == 0 && menu._page == source && menu._view == View.Report, "cancel keeps report and rejects later confirm");
        popup.Destroyed = true;
        check(menu.IssuesInputBlocked, "Escape does not propagate during dismissal frame");
        Time.frameCount++;
        check(!menu.IssuesInputBlocked, "underlying page interactive next frame after destruction");
        Show(menu); MenuAPI.Confirm(); MenuAPI.Confirm(); MenuAPI.Cancel();
        check(Application.Opened.SequenceEqual(new[] { EliteBugReport.IssuesUrl }), "explicit confirm opens exactly once");
        Time.frameCount++;
        check(menu.IssuesInputBlocked, "closing animation retains modal guard until destruction");
        MenuManager.instance.Current.Destroyed = true;
        check(!menu.IssuesInputBlocked, "destroyed Unity object no longer blocks");

        menu = New(); Show(menu);
        popup = MenuManager.instance.Current;
        menu._page = null; menu.CloseIssuesDialog(); MenuAPI.Confirm();
        check(popup.State == MenuPage.PageState.Closing && menu._issuesDialog == null, "source close cleans native popup");
        check(Application.Opened.Count == 1, "closed source cannot authorize delayed confirm");
        menu = New(); Show(menu); menu._page = new(); MenuAPI.Confirm();
        check(Application.Opened.Count == 1, "reopened source cannot authorize stale confirm");
        menu = New(); Show(menu); menu._view = View.Tools; MenuAPI.Confirm();
        check(Application.Opened.Count == 1, "changed view rejects stale confirm");
        menu = New(); Show(menu); menu._page!.Destroyed = true; MenuAPI.Confirm();
        check(Application.Opened.Count == 1, "destroyed source rejects confirm");
        menu = New(); menu._view = View.Tools; int opens = MenuAPI.Opens; Show(menu);
        check(MenuAPI.Opens == opens, "report-only entry point");
        menu = New(); Show(menu); Application.Fail = true; MenuAPI.Confirm(); MenuAPI.Confirm(); Application.Fail = false;
        check(menu._message == "Failed" && menu._signature == "" && EliteEnemyVariantsPlugin.ModLogger.Errors == 1, "browser failure logged once and report refresh scheduled");
        check(Application.Opened.Count == 1, "failure does not retry or send data");
    }
}
