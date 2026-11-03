using System;
using HarmonyLib;
using MenuLib;
using MenuLib.MonoBehaviors;
using TMPro;
using UnityEngine;

namespace REPOJP.EliteEnemyVariants;

internal sealed partial class EliteMenu
{
    private MenuPage? _issuesDialog;
    private int _issuesDialogDismissedFrame = -1;
    private bool IssuesInputBlocked => _issuesDialog != null || _issuesDialogDismissedFrame == Time.frameCount;

    private void ConfirmOpenIssues(REPOPopupPage page)
    {
        if (page == null || _page != page || _view != View.Report || IssuesInputBlocked) return;
        bool decided = false;
        void Decide(bool open)
        {
            if (decided) return;
            decided = true;
            _issuesDialogDismissedFrame = Time.frameCount;
            if (!open || page == null || _page != page || _view != View.Report) return;
            try { Application.OpenURL(EliteBugReport.IssuesUrl); }
            catch (Exception exception)
            {
                _message = "Failed";
                _signature = "";
                EliteEnemyVariantsPlugin.ModLogger.LogError(exception);
            }
        }
        MenuAPI.OpenPopup(T("ExternalSite"), new Color(1f, 0.65f, 0f),
            T("ConfirmIssues") + "\n\n" + EliteBugReport.IssuesUrl,
            () => Decide(true), () => Decide(false));
        // Like Roles, use the newly current page rather than a global singleton.
        // Keep it tracked until Unity destroys the closing page.
        _issuesDialog = AccessTools.Field(typeof(MenuManager), "currentMenuPage").GetValue(MenuManager.instance) as MenuPage;
        if (_issuesDialog == null) return;
        var dialog = _issuesDialog.GetComponent<MenuPageTwoOptions>();
        if (dialog == null) { _issuesDialog = null; return; }
        foreach (TextMeshProUGUI label in _issuesDialog.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            label.font = EliteGuideFont.ForLanguage(label.font, _language) ?? label.font;
            EliteTextLayout.Apply(label, _language);
        }
        dialog.option1Button.buttonTextString = T("Browser");
        dialog.option2Button.buttonTextString = T("Cancel");
        dialog.bodyTextMesh.textWrappingMode = TextWrappingModes.Normal;
        dialog.bodyTextMesh.enableAutoSizing = true;
        dialog.bodyTextMesh.fontSizeMin = 12;
        dialog.bodyTextMesh.fontSizeMax = 18;
    }

    private void CloseIssuesDialog()
    {
        if (_issuesDialog != null) _issuesDialog.PageStateSet(MenuPage.PageState.Closing);
        _issuesDialog = null;
    }
}
