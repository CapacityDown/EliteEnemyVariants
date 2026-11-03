using System;
using System.Collections.Generic;
using System.Linq;

namespace REPOJP.EliteEnemyVariants;

internal static class EliteGuideCatalog
{
    internal static readonly IReadOnlyList<EliteArchetype> Enemies =
        Enum.GetValues(typeof(EliteArchetype)).Cast<EliteArchetype>()
            .OrderBy(EnemyDisplayNameResolver.Get, StringComparer.Ordinal).ToArray();
    internal static string Description(EliteArchetype enemy) => enemy switch
    {
        EliteArchetype.Animal => "Notices players and enters havoc faster, prioritizes runs through nearby clusters of valuables and loaded carts, and renews pursuit once after havoc. Moving the cargo can redirect its havoc.",
        EliteArchetype.ApexPredator => "Transforms faster, hunts longer while transformed, and can transform one additional time.",
        EliteArchetype.Banger => "Is selected once per group and consumes one level-cap slot. Uses a five-second fuse, shortened to 3.5 seconds near a visible target within 6 m. Explosion values remain vanilla.",
        EliteArchetype.Bella => "Progresses tells and cooldowns faster and performs one additional charge.",
        EliteArchetype.BirthdayBoy => "Directly targets and immediately attacks the player who popped a balloon.",
        EliteArchetype.Bowtie => "Progresses tell and recovery faster and performs one additional yell cycle.",
        EliteArchetype.Chef => "Rolls at high speed while steering toward its target for up to six seconds, with one additional tumble.",
        EliteArchetype.CleanupCrew => "Uses head and melee actions sooner, adds one close-range melee follow-up, and circles toward the opposite side of its thrown head to pressure the player while keeping clear of the bomb.",
        EliteArchetype.Clown => "Walks and tracks while beaming, can switch target once, and has a bounded chance to perform a 180-degree sweep.",
        EliteArchetype.Elsa => "Becomes angry faster, chases longer in large form, and can hand off to one nearby target before shrinking.",
        EliteArchetype.Gambit => "Begins escaping after securing a captured player, keeps choosing reachable routes, retries stalled movement, and completes roulette and its result.",
        EliteArchetype.Gnome => "Has 25 HP, stronger jumps, immediate re-attacks, valuable priority, opposite-side formation, and at most two simultaneous attackers.",
        EliteArchetype.Headgrab => "Searches for and attacks heads faster, adds one headless combo, and tries once to retrieve a head dropped when stunned. Gives up if a player grabs or carries the head away.",
        EliteArchetype.Headman => "Pursues a player for at least ten seconds after noticing them and can extend that pursuit once.",
        EliteArchetype.HeartHugger => "Uses lure and chomp sooner and adds one aggro cycle. After burrowing, favors a nearby exit along the last-seen player's direction of travel, keeping its emergence and lure warnings.",
        EliteArchetype.Hidden => "Captures faster, carries longer, and reroutes once after becoming stuck. During each capture, it can also take one alternate escape route when a visible rescuer blocks its path, without extending the carry time.",
        EliteArchetype.Huntsman => "Remains completely blind, remembers sound during aim and fire, and investigates sounds within 45 m. Miss chance rises from 0% at 5 m or closer to 30% at 45 m; misses land 1.5–2.25 m beside the target. It does not gain burst or follow-up shots.",
        EliteArchetype.Loom => "Avoids the same target consecutively. After changing targets, it teleports within 30 m of the new target only when currently farther than 30 m away, preferring positions ahead of the target's movement. Arrival points must remain at least 8 m from living players.",
        EliteArchetype.Mentalist => "Before charging, it can spend up to two seconds repositioning to catch visible players and nearby valuables together. It then uses its faster psychic charge and one additional bounded psychic attack.",
        EliteArchetype.Oogly => "Uses a lower regular flight band, periodically patrols near floor or table height with its gaze parallel to the floor, and retries one failed dive.",
        EliteArchetype.Peeper => "Applies gaze damage faster and can hand off once to another visible target.",
        EliteArchetype.Pony => "Favors visible armed players, circles up to 2.5 m for a clear tongue attack, and can remove one blocking object per pursuit. Can move ahead of a visible moving target on the ceiling before dropping, and sometimes patrols the ceiling before finding anyone. Attack warnings and recovery are preserved. From the ceiling, prioritizes grabbing, lifting and flinging a reachable player; releases without a fling when space is blocked.",
        EliteArchetype.Reaper => "Keeps attacking while searching the last place it saw a player, then checks one nearby corner or side of cover. Stops attacking if it cannot find the player.",
        EliteArchetype.Robe => "Pursues longer, can re-engage once, and maintains at least 8 m/s toward its target even while unseen.",
        EliteArchetype.Rugrat => "Selects the highest-value reachable destructible item, approaches it without jumping, and makes one extra throw attempt. While holding an item, can move sideways around cover to find a clear throwing angle.",
        EliteArchetype.ShadowChild => "Approaches while unseen, keeps a stable target, and reliably attacks after sustained eye contact.",
        EliteArchetype.Spewer => "Immediately attaches to a newly seen player and vomits continuously for the full attachment.",
        EliteArchetype.Tick => "Notices sooner and drains faster. While fleeing after filling up, can change its escape route once if a visible pursuer blocks the way, without losing its fullness or reward.",
        EliteArchetype.Trudge => "Preserves the full attack animation and renews pursuit once after attacking.",
        EliteArchetype.Upscream => "Pursues the player it blew away for up to eight seconds and performs exactly one follow-up blow.",
        _ => string.Empty
    };
}
