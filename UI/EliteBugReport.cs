using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace REPOJP.EliteEnemyVariants;

internal sealed class EliteBugReport : ILogListener
{
    internal const string IssuesUrl = "https://github.com/CapacityDown/EliteEnemyVariants/issues/new";
    private const int MaxLogs = 500, MaxLogLength = 2048;
    private readonly object _gate = new();
    private readonly Queue<string> _logs = new();
    private readonly HashSet<string> _privateValues = new(StringComparer.Ordinal);
    internal string LatestText { get; private set; } = "";
    internal string LatestPath { get; private set; } = "";
    internal void RememberPlayers()
    {
        lock (_gate)
        {
            if (PhotonNetwork.CurrentRoom != null) _privateValues.Add(PhotonNetwork.CurrentRoom.Name);
            foreach (var player in PhotonNetwork.PlayerList)
            {
                if (!string.IsNullOrWhiteSpace(player.NickName)) _privateValues.Add(player.NickName);
                if (!string.IsNullOrWhiteSpace(player.UserId)) _privateValues.Add(player.UserId);
            }
            if (GameDirector.instance != null) foreach (PlayerAvatar player in SemiFunc.PlayerGetList())
            {
                if (player == null) continue;
                string name = SemiFunc.PlayerGetName(player);
                if (!string.IsNullOrWhiteSpace(name)) _privateValues.Add(name);
                string steamId = SemiFunc.PlayerGetSteamID(player);
                if (!string.IsNullOrWhiteSpace(steamId)) _privateValues.Add(steamId);
            }
            // Discard the corresponding logs before forgetting identities.
            if (_privateValues.Count > 1000) { _logs.Clear(); _privateValues.Clear(); }
        }
    }
    public void LogEvent(object sender, LogEventArgs args)
    {
        try
        {
            string body = args.Data?.ToString() ?? "";
            if (args.Source.SourceName != EliteEnemyVariantsPlugin.PluginName && args.Source.SourceName != EliteEnemyVariantsPlugin.PluginGuid &&
                !((args.Level & (LogLevel.Error | LogLevel.Fatal)) != 0 && body.Contains("REPOJP.EliteEnemyVariants"))) return;
            string line = $"[{DateTime.UtcNow:O}] [{args.Level}] {body}";
            lock (_gate)
            {
                _logs.Enqueue(line.Substring(0, Math.Min(MaxLogLength, line.Length)));
                while (_logs.Count > MaxLogs) _logs.Dequeue();
            }
        }
        catch { /* Logging must not throw or recursively log. */ }
    }
    internal void Create(EliteMenuState state)
    {
        RememberPlayers();
        Dictionary<string, string> versions = new();
        string VersionField(string value)
        { string key = "EEVVERSION" + Guid.NewGuid().ToString("N"); versions[key] = value; return key; }
        StringBuilder report = new("# EliteEnemyVariants bug report\n\n## What happened? / 発生した問題\n\n## Steps to reproduce / 再現手順\n1. \n\n## Expected / actual result / 期待した結果と実際の結果\n\n## Environment\n");
        report.AppendLine("EliteEnemyVariants: " + EliteEnemyVariantsPlugin.PluginVersion);
        report.AppendLine("Game: " + VersionField(Application.version) + "\nUnity: " + VersionField(Application.unityVersion));
        report.AppendLine("OS: " + SystemInfo.operatingSystem + $"\nResolution: {Screen.width} x {Screen.height}");
        report.AppendLine("Scene: " + SceneManager.GetActiveScene().name + "\nDisplay: " + state.Status);
        report.AppendLine("\n## Installed mods");
        int start = report.Length;
        foreach (var plugin in Chainloader.PluginInfos.Values.OrderBy(p => p.Metadata.GUID))
        {
            report.AppendLine(plugin.Metadata.GUID + " " + VersionField(plugin.Metadata.Version.ToString()));
            if (report.Length - start > 10000) { report.AppendLine("[TRUNCATED]"); break; }
        }
        report.AppendLine("\n## Local EliteEnemyVariants settings");
        start = report.Length;
        foreach (var entry in EliteEnemyVariantsPlugin.Instance.Config.OrderBy(e => e.Key.Section).ThenBy(e => e.Key.Key))
        {
            string value = entry.Value.GetSerializedValue();
            report.AppendLine(entry.Key.Section + "." + entry.Key.Key + " = " + value.Substring(0, Math.Min(512, value.Length)));
            if (report.Length - start > 30000) { report.AppendLine("[TRUNCATED]"); break; }
        }
        string[] privateValues, logs;
        lock (_gate) { privateValues = _privateValues.ToArray(); logs = _logs.Reverse().ToArray(); }
        string cleaned = EliteReportRedactor.Clean(report.ToString(), privateValues);
        foreach (var version in versions) cleaned = cleaned.Replace(version.Key, version.Value);
        StringBuilder complete = new(cleaned);
        complete.AppendLine("\n## Recent EliteEnemyVariants messages (up to 500, newest first)");
        foreach (string line in logs) complete.AppendLine(EliteReportRedactor.Clean(line, privateValues, MaxLogLength));
        string text = complete.ToString();
        string directory = Path.Combine(Paths.BepInExRootPath, "EliteEnemyVariantsReports");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"EliteEnemyVariants-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.md");
        File.WriteAllText(path, text, new UTF8Encoding(false));
        LatestText = text; LatestPath = path;
    }
    public void Dispose() { lock (_gate) { _logs.Clear(); _privateValues.Clear(); } }
}
