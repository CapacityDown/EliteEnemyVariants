using UnityEngine;

namespace REPOJP.EliteEnemyVariants;

internal static class EnemyDisplayNameResolver
{
    internal static bool TryResolve(Enemy enemy, out EliteArchetype archetype)
    {
        if (enemy.GetComponent<EnemyAnimal>() != null)
        {
            archetype = EliteArchetype.Animal;
            return true;
        }
        if (enemy.GetComponent<EnemyBirthdayBoy>() != null)
        {
            archetype = EliteArchetype.BirthdayBoy;
            return true;
        }
        if (enemy.GetComponent<EnemyBombThrower>() != null)
        {
            archetype = EliteArchetype.CleanupCrew;
            return true;
        }
        if (enemy.GetComponent<EnemyBowtie>() != null)
        {
            archetype = EliteArchetype.Bowtie;
            return true;
        }
        if (enemy.GetComponent<EnemyCeilingEye>() != null)
        {
            archetype = EliteArchetype.Peeper;
            return true;
        }
        if (enemy.GetComponent<EnemyDuck>() != null)
        {
            archetype = EliteArchetype.ApexPredator;
            return true;
        }
        if (enemy.GetComponent<EnemyElsa>() != null)
        {
            archetype = EliteArchetype.Elsa;
            return true;
        }
        if (enemy.GetComponent<EnemyFloater>() != null)
        {
            archetype = EliteArchetype.Mentalist;
            return true;
        }
        if (enemy.GetComponentInChildren<EnemyHeadController>(true) != null)
        {
            archetype = EliteArchetype.Headman;
            return true;
        }
        if (enemy.GetComponent<EnemyHeadGrabber>() != null)
        {
            archetype = EliteArchetype.Headgrab;
            return true;
        }
        if (enemy.GetComponent<EnemyHeartHugger>() != null)
        {
            archetype = EliteArchetype.HeartHugger;
            return true;
        }
        if (enemy.GetComponent<EnemyHidden>() != null)
        {
            archetype = EliteArchetype.Hidden;
            return true;
        }
        if (enemy.GetComponent<EnemyOogly>() != null)
        {
            archetype = EliteArchetype.Oogly;
            return true;
        }
        if (enemy.GetComponent<EnemyRobe>() != null)
        {
            archetype = EliteArchetype.Robe;
            return true;
        }
        if (enemy.GetComponent<EnemyRunner>() != null)
        {
            archetype = EliteArchetype.Reaper;
            return true;
        }
        if (enemy.GetComponent<EnemyShadow>() != null)
        {
            archetype = EliteArchetype.Loom;
            return true;
        }
        if (enemy.GetComponent<EnemySlowMouth>() != null)
        {
            archetype = EliteArchetype.Spewer;
            return true;
        }
        if (enemy.GetComponent<EnemySlowWalker>() != null)
        {
            archetype = EliteArchetype.Trudge;
            return true;
        }
        if (enemy.GetComponent<EnemyTick>() != null)
        {
            archetype = EliteArchetype.Tick;
            return true;
        }
        if (enemy.GetComponent<EnemyTricycle>() != null)
        {
            archetype = EliteArchetype.Bella;
            return true;
        }
        if (enemy.GetComponent<EnemyTumbler>() != null)
        {
            archetype = EliteArchetype.Chef;
            return true;
        }
        if (enemy.GetComponent<EnemyUpscream>() != null)
        {
            archetype = EliteArchetype.Upscream;
            return true;
        }
        if (enemy.GetComponent<EnemyValuableThrower>() != null)
        {
            archetype = EliteArchetype.Rugrat;
            return true;
        }
        if (enemy.GetComponent<EnemyBeamer>() != null)
        {
            archetype = EliteArchetype.Clown;
            return true;
        }
        if (enemy.GetComponent<EnemyThinMan>() != null)
        {
            archetype = EliteArchetype.ShadowChild;
            return true;
        }
        if (enemy.GetComponent<EnemyHunter>() != null)
        {
            archetype = EliteArchetype.Huntsman;
            return true;
        }
        if (enemy.GetComponent<EnemySpinny>() != null)
        {
            archetype = EliteArchetype.Gambit;
            return true;
        }
        if (enemy.GetComponent<EnemyBang>() != null)
        {
            archetype = EliteArchetype.Banger;
            return true;
        }
        if (enemy.GetComponent<EnemyGnome>() != null)
        {
            archetype = EliteArchetype.Gnome;
            return true;
        }

        if (enemy.GetComponent<EnemySnatcher>() != null)
        {
            archetype = EliteArchetype.Pony;
            return true;
        }

        archetype = default;
        return false;
    }

    internal static string Get(EliteArchetype archetype) => archetype switch
    {
        EliteArchetype.Animal => "Animal",
        EliteArchetype.BirthdayBoy => "Birthday Boy",
        EliteArchetype.CleanupCrew => "Cleanup Crew",
        EliteArchetype.Bowtie => "Bowtie",
        EliteArchetype.Peeper => "Peeper",
        EliteArchetype.ApexPredator => "Apex Predator",
        EliteArchetype.Elsa => "Elsa",
        EliteArchetype.Mentalist => "Mentalist",
        EliteArchetype.Headman => "Headman",
        EliteArchetype.Headgrab => "Headgrab",
        EliteArchetype.HeartHugger => "Heart Hugger",
        EliteArchetype.Hidden => "Hidden",
        EliteArchetype.Oogly => "Oogly",
        EliteArchetype.Robe => "Robe",
        EliteArchetype.Reaper => "Reaper",
        EliteArchetype.Loom => "Loom",
        EliteArchetype.Spewer => "Spewer",
        EliteArchetype.Trudge => "Trudge",
        EliteArchetype.Tick => "Tick",
        EliteArchetype.Bella => "Bella",
        EliteArchetype.Chef => "Chef",
        EliteArchetype.Upscream => "Upscream",
        EliteArchetype.Rugrat => "Rugrat",
        EliteArchetype.Clown => "Clown",
        EliteArchetype.ShadowChild => "Shadow Child",
        EliteArchetype.Huntsman => "Huntsman",
        EliteArchetype.Gambit => "Gambit",
        EliteArchetype.Banger => "Banger",
        EliteArchetype.Gnome => "Gnome",
        EliteArchetype.Pony => "Pony",
        _ => "Unknown"
    };
}
