using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace REPOJP.EliteEnemyVariants;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInDependency(RepoConfigGuid, BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency("nickklmao.menulib", BepInDependency.DependencyFlags.HardDependency)]
[BepInDependency(StageFluxCompatibility.PluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency(RoleShuffleCompatibility.PluginGuid, BepInDependency.DependencyFlags.SoftDependency)]
public sealed class EliteEnemyVariantsPlugin : BaseUnityPlugin
{
    public const string PluginGuid = "REPOJP.EliteEnemyVariants";
    public const string PluginName = "Elite Enemy Variants";
    public const string PluginVersion = "5.0.0";
    public const string RepoConfigGuid = "nickklmao.repoconfig";

    private Harmony? _harmony;
    private readonly List<Harmony> _adapterHarmonies = new();
    private GameObject? _runtimeObject;

    internal static EliteEnemyVariantsPlugin Instance { get; private set; } = null!;
    internal static ManualLogSource ModLogger { get; private set; } = null!;
    internal EliteEnemyConfig Settings { get; private set; } = null!;
    internal EliteRuntimeController Runtime { get; private set; } = null!;
    internal EliteMenuState MenuState { get; private set; } = null!;
    internal EliteBugReport BugReport { get; private set; } = null!;
    private Harmony? _menuHarmony;

    private void Awake()
    {
        Instance = this;
        ModLogger = Logger;
        Settings = new EliteEnemyConfig(Config);
        SelectionInvariantChecks.Run();
        CompatibilityGuard.Refresh(Logger);

        gameObject.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(gameObject);

        _runtimeObject = new GameObject("EliteEnemyVariants_Runtime");
        _runtimeObject.hideFlags = HideFlags.HideAndDontSave;
        _runtimeObject.transform.SetParent(transform, false);
        Runtime = _runtimeObject.AddComponent<EliteRuntimeController>();
        Runtime.Initialize(Settings);

        BugReport = new EliteBugReport();
        BepInEx.Logging.Logger.Listeners.Add(BugReport);
        MenuState = _runtimeObject.AddComponent<EliteMenuState>();
        MenuState.Initialize(Config, Settings);
        // Menu failures must not disable host-side enemy behavior.
        try
        {
            _runtimeObject.AddComponent<EliteMenu>().Initialize(MenuState);
            _menuHarmony = new Harmony(PluginGuid + ".Menu");
            _menuHarmony.PatchAll(typeof(EliteMenuScroll));
        }
        catch (Exception exception) { Logger.LogError("Elites menu initialization failed: " + exception); }

        _harmony = new Harmony(PluginGuid);
        try
        {
            _harmony.PatchAll(typeof(CommonEnemyPatches));
            _harmony.PatchAll(typeof(EnemyOrbRewardPatches));
            _harmony.PatchAll(typeof(LifecyclePatches));
            _harmony.PatchAll(typeof(TestCommandChatResponsePatch));
            ApplyAdapterPatch(EliteArchetype.Animal, typeof(AnimalElitePatches));
            ApplyAdapterPatch(EliteArchetype.BirthdayBoy, typeof(BirthdayBoyElitePatches));
            ApplyAdapterPatch(EliteArchetype.CleanupCrew, typeof(CleanupCrewElitePatches));
            ApplyAdapterPatch(EliteArchetype.Bowtie, typeof(BowtieElitePatches));
            ApplyAdapterPatch(EliteArchetype.Peeper, typeof(PeeperElitePatches));
            ApplyAdapterPatch(EliteArchetype.ApexPredator, typeof(ApexPredatorElitePatches));
            ApplyAdapterPatch(EliteArchetype.Elsa, typeof(ElsaElitePatches));
            ApplyAdapterPatch(EliteArchetype.Mentalist, typeof(MentalistElitePatches));
            ApplyAdapterPatch(EliteArchetype.Headman, typeof(HeadmanElitePatches));
            ApplyAdapterPatch(EliteArchetype.Headgrab, typeof(HeadgrabElitePatches));
            ApplyAdapterPatch(EliteArchetype.HeartHugger, typeof(HeartHuggerElitePatches));
            ApplyAdapterPatch(EliteArchetype.Hidden, typeof(HiddenElitePatches));
            ApplyAdapterPatch(EliteArchetype.Oogly, typeof(OoglyElitePatches));
            ApplyAdapterPatch(EliteArchetype.Robe, typeof(RobeElitePatches));
            ApplyAdapterPatch(EliteArchetype.Reaper, typeof(ReaperElitePatches));
            ApplyAdapterPatch(EliteArchetype.Loom, typeof(LoomElitePatches));
            ApplyAdapterPatch(EliteArchetype.Spewer, typeof(SpewerElitePatches));
            ApplyAdapterPatch(EliteArchetype.Trudge, typeof(TrudgeElitePatches));
            ApplyAdapterPatch(EliteArchetype.Tick, typeof(TickElitePatches));
            ApplyAdapterPatch(EliteArchetype.Bella, typeof(BellaElitePatches));
            ApplyAdapterPatch(EliteArchetype.Chef, typeof(ChefElitePatches));
            ApplyAdapterPatch(EliteArchetype.Upscream, typeof(UpscreamElitePatches));
            ApplyAdapterPatch(EliteArchetype.Rugrat, typeof(RugratElitePatches));
            ApplyAdapterPatch(EliteArchetype.Clown, typeof(ClownElitePatches));
            ApplyAdapterPatch(EliteArchetype.ShadowChild, typeof(ShadowChildElitePatches));
            ApplyAdapterPatch(EliteArchetype.Huntsman, typeof(HuntsmanElitePatches));
            ApplyAdapterPatch(EliteArchetype.Gambit, typeof(GambitElitePatches));
            ApplyAdapterPatch(EliteArchetype.Banger, typeof(BangerElitePatches));
            ApplyAdapterPatch(EliteArchetype.Gnome, typeof(GnomeElitePatches));
            ApplyAdapterPatch(EliteArchetype.Pony, typeof(PonyElitePatches));
            SceneManager.activeSceneChanged += ActiveSceneChanged;
            Logger.LogInfo($"{PluginName} v{PluginVersion} loaded. Host-only gameplay mode enabled.");
        }
        catch (Exception exception)
        {
            Logger.LogError($"Failed to apply Harmony patches: {exception}");
            _harmony.UnpatchSelf();
            Runtime.enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (BugReport != null) { BepInEx.Logging.Logger.Listeners.Remove(BugReport); BugReport.Dispose(); }
        _menuHarmony?.UnpatchSelf();
        SceneManager.activeSceneChanged -= ActiveSceneChanged;
        Runtime?.Shutdown();
        _harmony?.UnpatchSelf();
        foreach (Harmony harmony in _adapterHarmonies)
        {
            harmony.UnpatchSelf();
        }
        _adapterHarmonies.Clear();
        if (_runtimeObject != null)
        {
            Destroy(_runtimeObject);
        }
    }

    private static void ActiveSceneChanged(Scene previous, Scene current)
    {
        EliteRegistry.ClearStage();
    }

    private void ApplyAdapterPatch(EliteArchetype archetype, Type patchType)
    {
        if (!CompatibilityGuard.IsAdapterEnabled(archetype))
        {
            return;
        }

        Harmony harmony = new($"{PluginGuid}.{archetype}");
        try
        {
            AdapterPatchValidator.Validate(archetype);
            harmony.PatchAll(patchType);
            _adapterHarmonies.Add(harmony);
        }
        catch (Exception exception)
        {
            harmony.UnpatchSelf();
            CompatibilityGuard.DisableAdapter(archetype, exception, Logger);
        }
    }

}
