namespace REPOJP.EliteEnemyVariants;

internal static class EnemyAdapterRuntime
{
    internal static bool HasRuntimeTick(EliteArchetype archetype) => archetype is
        EliteArchetype.Clown or
        EliteArchetype.ShadowChild or
        EliteArchetype.Huntsman or
        EliteArchetype.Gambit or
        EliteArchetype.Banger or
        EliteArchetype.Gnome;

    internal static void OnSpawn(EliteState state)
    {
        switch (state.Archetype)
        {
            case EliteArchetype.Animal: AnimalElitePatches.OnSpawn(state); break;
            case EliteArchetype.BirthdayBoy: BirthdayBoyElitePatches.OnSpawn(state); break;
            case EliteArchetype.CleanupCrew: CleanupCrewElitePatches.OnSpawn(state); break;
            case EliteArchetype.Bowtie: BowtieElitePatches.OnSpawn(state); break;
            case EliteArchetype.Peeper: PeeperElitePatches.OnSpawn(state); break;
            case EliteArchetype.ApexPredator: ApexPredatorElitePatches.OnSpawn(state); break;
            case EliteArchetype.Elsa: ElsaElitePatches.OnSpawn(state); break;
            case EliteArchetype.Mentalist: MentalistElitePatches.OnSpawn(state); break;
            case EliteArchetype.Headman: HeadmanElitePatches.OnSpawn(state); break;
            case EliteArchetype.Headgrab: HeadgrabElitePatches.OnSpawn(state); break;
            case EliteArchetype.HeartHugger: HeartHuggerElitePatches.OnSpawn(state); break;
            case EliteArchetype.Hidden: HiddenElitePatches.OnSpawn(state); break;
            case EliteArchetype.Oogly: OoglyElitePatches.OnSpawn(state); break;
            case EliteArchetype.Robe: RobeElitePatches.OnSpawn(state); break;
            case EliteArchetype.Reaper: ReaperElitePatches.OnSpawn(state); break;
            case EliteArchetype.Loom: LoomElitePatches.OnSpawn(state); break;
            case EliteArchetype.Spewer: SpewerElitePatches.OnSpawn(state); break;
            case EliteArchetype.Trudge: TrudgeElitePatches.OnSpawn(state); break;
            case EliteArchetype.Tick: TickElitePatches.OnSpawn(state); break;
            case EliteArchetype.Bella: BellaElitePatches.OnSpawn(state); break;
            case EliteArchetype.Chef: ChefElitePatches.OnSpawn(state); break;
            case EliteArchetype.Upscream: UpscreamElitePatches.OnSpawn(state); break;
            case EliteArchetype.Rugrat: RugratElitePatches.OnSpawn(state); break;
            case EliteArchetype.Clown:
                ClownElitePatches.OnSpawn(state);
                break;
            case EliteArchetype.ShadowChild:
                ShadowChildElitePatches.OnSpawn(state);
                break;
            case EliteArchetype.Huntsman:
                HuntsmanElitePatches.OnSpawn(state);
                break;
            case EliteArchetype.Gambit:
                GambitElitePatches.OnSpawn(state);
                break;
            case EliteArchetype.Banger:
                BangerElitePatches.OnSpawn(state);
                break;
            case EliteArchetype.Gnome:
                GnomeElitePatches.OnSpawn(state);
                break;
        }
    }

    internal static void Tick(EliteState state)
    {
        // The other 23 adapters are driven by postfixes on their own AI Update
        // methods, after vanilla has initialized and advanced its state timers.
        switch (state.Archetype)
        {
            case EliteArchetype.Clown:
                ClownElitePatches.Tick(state);
                break;
            case EliteArchetype.ShadowChild:
                ShadowChildElitePatches.Tick(state);
                break;
            case EliteArchetype.Huntsman:
                HuntsmanElitePatches.Tick(state);
                break;
            case EliteArchetype.Gambit:
                GambitElitePatches.Tick(state);
                break;
            case EliteArchetype.Banger:
                BangerElitePatches.Tick(state);
                break;
            case EliteArchetype.Gnome:
                GnomeElitePatches.Tick(state);
                break;
        }
    }

    internal static void Cleanup(EliteState state)
    {
        if (state.Archetype == EliteArchetype.Gnome)
        {
            GnomeElitePatches.Cleanup(state);
            return;
        }
        state.AdapterState = null;
    }
}
