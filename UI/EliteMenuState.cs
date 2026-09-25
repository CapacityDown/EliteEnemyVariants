using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

namespace REPOJP.EliteEnemyVariants;

// These optional room properties describe settings only. They never select or count enemies.
internal sealed class EliteMenuState : MonoBehaviourPunCallbacks
{
    private const string Key = "EEV.Menu", RequestKey = "EEV.Menu.Request";
    private ConfigFile _file = null!;
    private EliteEnemyConfig _config = null!;
    private object? _room;
    private string _published = "";
    private readonly Dictionary<int, int> _requests = new();
    private float _nextPoll, _lastPublish = -10f, _lastRequest = -10f;
    private int _revision;
    private bool _warned, _force;
    internal static bool CanEdit => GameManager.instance != null && EliteMenuPolicy.CanEdit(true,
        SemiFunc.IsMultiplayer(), PhotonNetwork.InRoom, PhotonNetwork.IsMasterClient);
    internal void Initialize(ConfigFile file, EliteEnemyConfig config) { _file = file; _config = config; }
    private void Update()
    {
        if (Time.unscaledTime < _nextPoll) return;
        _nextPoll = Time.unscaledTime + 0.5f;
        try
        {
            EliteEnemyVariantsPlugin.Instance.BugReport.RememberPlayers();
            if (PhotonNetwork.InRoom && PhotonNetwork.IsMasterClient)
                foreach (Player player in PhotonNetwork.PlayerList)
                    if (player.CustomProperties[RequestKey] is int stamp &&
                        (!_requests.TryGetValue(player.ActorNumber, out int previous) || previous != stamp))
                    { _requests[player.ActorNumber] = stamp; _force = true; }
            Publish();
        }
        catch (Exception exception)
        { if (!_warned) EliteEnemyVariantsPlugin.ModLogger.LogWarning("Elites display unavailable: " + exception.Message); _warned = true; }
    }
    internal bool TryRead(out long mask, out bool active)
    {
        mask = 0; active = false;
        if (CanEdit)
        {
            foreach (EliteArchetype enemy in EliteGuideCatalog.Enemies)
                if (_config.ChanceFor(enemy).Enabled.Value) mask |= 1L << (int)enemy;
            active = _config.Enabled.Value;
            return true;
        }
        return PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null &&
            EliteMenuPolicy.TryDecode(PhotonNetwork.CurrentRoom.CustomProperties[Key] as string,
                PhotonNetwork.MasterClient?.ActorNumber ?? 0, out mask, out active);
    }
    private void Publish()
    {
        if (!CanEdit || !PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null ||
            Time.unscaledTime - _lastPublish < 2f || !TryRead(out long mask, out bool active)) return;
        string state = mask + ":" + active;
        if (!_force && ReferenceEquals(_room, PhotonNetwork.CurrentRoom) && _published == state) return;
        int revision = _revision == int.MaxValue ? 0 : _revision + 1;
        if (!PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable
            { [Key] = EliteMenuPolicy.Encode(PhotonNetwork.LocalPlayer.ActorNumber, mask, active, revision) })) return;
        _revision = revision; _room = PhotonNetwork.CurrentRoom; _published = state; _force = false; _lastPublish = Time.unscaledTime;
    }
    internal bool SetEnabled(EliteArchetype enemy, bool enabled)
    {
        if (!CanEdit || !Enum.IsDefined(typeof(EliteArchetype), enemy)) return false;
        ConfigEntry<bool> entry = _config.ChanceFor(enemy).Enabled;
        bool old = entry.Value, automatic = _file.SaveOnConfigSet;
        _file.SaveOnConfigSet = false;
        try { entry.Value = enabled; _file.Save(); }
        catch { entry.Value = old; throw; }
        finally { _file.SaveOnConfigSet = automatic; }
        _force = true;
        return true;
    }
    internal string Status => CanEdit ? (PhotonNetwork.InRoom ? "Host - sharing enhancement settings." : "Single player - local enhancement settings.") :
        TryRead(out _, out _) ? "Connected - host enhancement settings received." :
        "Host settings unavailable. The host may not support this menu. Local values are not shown as host settings.";
    internal void RefreshDisplay()
    {
        if (CanEdit) { _force = true; _nextPoll = 0; return; }
        if (!PhotonNetwork.InRoom || Time.unscaledTime - _lastRequest < 2f) return;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [RequestKey] = PhotonNetwork.ServerTimestamp });
        _lastRequest = Time.unscaledTime;
    }
    private void ResetDisplay()
    { _room = null; _published = ""; _requests.Clear(); _warned = false; _force = true; _nextPoll = 0; _lastPublish = -10f; }
    public override void OnJoinedRoom() => ResetDisplay();
    public override void OnLeftRoom() => ResetDisplay();
    public override void OnMasterClientSwitched(Player player) => ResetDisplay();
}
