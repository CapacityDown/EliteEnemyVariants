using System;
using HarmonyLib;

namespace REPOJP.EliteEnemyVariants;

internal static class AdapterPatchValidator
{
    internal static void Validate(EliteArchetype archetype)
    {
        switch (archetype)
        {
            case EliteArchetype.Animal:
                Fields(typeof(EnemyAnimal), "currentState", "stateTimer", "havocTimer", "playerTarget", "agentDestination");
                Methods(typeof(EnemyAnimal), "UpdateState");
                break;
            case EliteArchetype.BirthdayBoy:
                Fields(typeof(EnemyBirthdayBoy), "currentState", "stateTimer", "secondStateTimer", "photonView", "balloons");
                Methods(typeof(EnemyBirthdayBoy), "CheckForPoppedBalloons", "UpdatePlayerTarget", "UpdateState");
                break;
            case EliteArchetype.CleanupCrew:
                Fields(typeof(EnemyBombThrower), "currentState", "stateTimer", "headCooldown", "headAttackCooldown", "meleeCooldown", "playerTarget");
                Methods(typeof(EnemyBombThrower), "UpdateState");
                break;
            case EliteArchetype.Bowtie:
                Fields(typeof(EnemyBowtie), "currentState", "stateTimer", "attacks");
                Methods(typeof(EnemyBowtie), "UpdateState");
                break;
            case EliteArchetype.Peeper:
                Fields(typeof(EnemyCeilingEye), "currentState", "eyeDamageWaitTimer", "eyeDamageTimer", "targetPlayer", "photonView");
                Methods(typeof(EnemyCeilingEye), "UpdateState", "TargetPlayerRPC");
                break;
            case EliteArchetype.ApexPredator:
                Fields(typeof(EnemyDuck), "currentState", "stateTimer", "chaseTimer", "playerTarget");
                Methods(typeof(EnemyDuck), "UpdateState");
                break;
            case EliteArchetype.Elsa:
                Fields(typeof(EnemyElsa), "currentState", "stateTimer", "angryTimer", "chaseTimer", "playerTarget", "photonView");
                Methods(typeof(EnemyElsa), "UpdateState", "UpdatePlayerTargetRPC");
                break;
            case EliteArchetype.Mentalist:
                Fields(typeof(EnemyFloater), "currentState", "stateTimer", "attackCount", "targetPlayer");
                Methods(typeof(EnemyFloater), "UpdateState", "StateGoToPlayer");
                break;
            case EliteArchetype.Headman:
                Fields(typeof(Enemy), nameof(Enemy.CurrentState), "DisableChaseTimer");
                Fields(typeof(EnemyStateChase), "StateTimer");
                break;
            case EliteArchetype.Headgrab:
                Fields(typeof(EnemyHeadGrabber), "currentState", "stateTimer", "attackCooldown", "headTargetCooldown", "attacksInRow", "headTargetActive");
                break;
            case EliteArchetype.HeartHugger:
                Fields(typeof(EnemyHeartHugger), "currentState", "stateTimer", "attacksInARow");
                Methods(typeof(EnemyHeartHugger), "StateSet", "LeaveStart");
                break;
            case EliteArchetype.Hidden:
                Fields(typeof(EnemyHidden), "currentState", "stateTimer", "maxMoveTimer", "agentDestination", "playerTarget");
                break;
            case EliteArchetype.Oogly:
                Fields(typeof(EnemyOogly), "currentState", "stateTimer", "attackCooldownTimer", "targetPlayer", "roamDestination", "currentRoamTarget", "headWorldTargetRotation");
                Methods(typeof(EnemyOogly), "StateSet", "InitializeFlightSystem", "SetHeadTargetRotation");
                break;
            case EliteArchetype.Robe:
                Fields(typeof(EnemyRobe), "currentState", "idleBreakTimer", "chaseTime", "targetPlayer", "isOnScreen");
                Methods(typeof(EnemyRobe), "UpdateState", "MoveTowardPlayer");
                break;
            case EliteArchetype.Reaper:
                Fields(typeof(EnemyRunner), "currentState", "stateTimer", "targetPlayer", "stateImpulse", "targetPosition", "agentSpeedCurrent");
                Methods(typeof(EnemyRunner), "UpdateState", nameof(EnemyRunner.StateAttackPlayer), nameof(EnemyRunner.OnVision), "RotationLogic");
                break;
            case EliteArchetype.Loom:
                Fields(typeof(PlayerAvatar), "rbVelocity");
                Fields(typeof(EnemyShadow), "currentState", "stateTimer", "playerTarget");
                Methods(typeof(EnemyShadow), "UpdatePlayerTarget", "StateChooseTarget");
                break;
            case EliteArchetype.Spewer:
                Fields(typeof(EnemySlowMouth), "currentState", "stateTimer", "idlePukeCooldown", "possessCooldown", "attachedTimer");
                Methods(typeof(EnemySlowMouth), "OnVision", "UpdatePlayerTarget");
                break;
            case EliteArchetype.Trudge:
                Fields(typeof(EnemySlowWalker), "currentState", "stateTimer", "targetPlayer");
                Methods(typeof(EnemySlowWalker), "UpdateState");
                break;
            case EliteArchetype.Tick:
                Fields(typeof(EnemyTick), "currentState", "stateTimer", "suckTimer");
                break;
            case EliteArchetype.Bella:
                Fields(typeof(EnemyTricycle), "currentState", "stateTimer", "attackCooldown");
                Methods(typeof(EnemyTricycle), "SetState");
                break;
            case EliteArchetype.Chef:
                Fields(typeof(EnemyTumbler), "currentState", "stateTimer", "roamWaitTimer", "targetPlayer");
                Methods(typeof(EnemyTumbler), "UpdateState");
                break;
            case EliteArchetype.Upscream:
                Fields(typeof(EnemyUpscream), "currentState", "stateTimer", "roamWaitTimer", "attacks", "targetPlayer");
                Methods(typeof(EnemyUpscream), "UpdateState");
                break;
            case EliteArchetype.Rugrat:
                Fields(typeof(EnemyValuableThrower), "currentState", "stateTimer", "attacks", "playerTarget", "valuableTarget");
                Methods(typeof(EnemyValuableThrower), "UpdateState", "StateGetValuable", "StateGoToTarget");
                Methods(typeof(EnemyJump), nameof(EnemyJump.StuckTrigger));
                Methods(typeof(SemiFunc), nameof(SemiFunc.EnemyCartJump));
                break;
            case EliteArchetype.Clown:
                Fields(typeof(EnemyBeamer), "aimHorizontalTarget", "aimHorizontalResult", "laserRange", "aimVerticalTarget");
                Methods(typeof(EnemyBeamer), "RotationLogic", "VerticalAimLogic");
                break;
            case EliteArchetype.ShadowChild:
                Fields(typeof(EnemyThinMan), "enemy", "playerTarget", "teleportPosition");
                Methods(typeof(EnemyThinMan), "UpdateState", "SetTarget");
                break;
            case EliteArchetype.Huntsman:
                Fields(typeof(EnemyHunter), "investigatePoint", "investigateAimHorizontal", "investigateAimVertical");
                Methods(typeof(EnemyHunter), "AimLogic", "ShootRPC");
                Methods(typeof(EnemyDirector), nameof(EnemyDirector.SetInvestigate));
                break;
            case EliteArchetype.Gambit:
                Fields(typeof(EnemySpinny), "reachedPoint");
                Methods(typeof(EnemySpinny), "UpdateState", "JumpIfStuck", nameof(EnemySpinny.RouletteGoingOn));
                Methods(typeof(EnemyStateStunned), nameof(EnemyStateStunned.Set));
                break;
            case EliteArchetype.Banger:
                Methods(typeof(EnemyBang), "FuseLogic", "UpdateState", "ExplodeRPC");
                break;
            case EliteArchetype.Gnome:
                Fields(typeof(EnemyGnome), "attackAngle");
                Fields(typeof(EnemyGnomeDirector), "stateImpulse", "playerTarget", "valuableTarget");
                Methods(typeof(EnemyGnome), "UpdateState", "StateNoticeDelay", "AttackPositionLogic", "StateAttackDone");
                Methods(typeof(EnemyGnomeDirector), "UpdateState", "CanAttack", "StateAttackSet");
                break;
            case EliteArchetype.Pony:
                Fields(typeof(EnemySnatcher), "playerTarget", "currentState", "stateImpulse", "agentDestination",
                    "tongueRepositionStartPosition", "tongueRepositionPlayerPosition", "tongueRepositionDestinationTimer",
                    "attackPauseTimer", "mainAttackCooldownTimer", "tongueAttackRecoveryTimer", "ceilingCooldownTimer");
                Methods(typeof(EnemySnatcher), "Update", "StateNotice", "FindClosestVisiblePlayerTarget",
                    "TongueRepositionDestinationPick", "TongueAttackCheck", "StateChase", "CeilingStateGround",
                    "CeilingStateWatchdog", "SetStateGrabPhysObject", "PlayerVisible", "PlayerAttackCooldownActive",
                    "SetPlayerTarget", "CeilingJumpTry", "CeilingWanderPathClear", "CeilingScareLanding", "CeilingDrop");
                break;
        }

        Type? dedicatedUpdateType = archetype switch
        {
            EliteArchetype.Animal => typeof(EnemyAnimal),
            EliteArchetype.BirthdayBoy => typeof(EnemyBirthdayBoy),
            EliteArchetype.CleanupCrew => typeof(EnemyBombThrower),
            EliteArchetype.Bowtie => typeof(EnemyBowtie),
            EliteArchetype.Peeper => typeof(EnemyCeilingEye),
            EliteArchetype.ApexPredator => typeof(EnemyDuck),
            EliteArchetype.Elsa => typeof(EnemyElsa),
            EliteArchetype.Mentalist => typeof(EnemyFloater),
            EliteArchetype.Headman => typeof(Enemy),
            EliteArchetype.Headgrab => typeof(EnemyHeadGrabber),
            EliteArchetype.HeartHugger => typeof(EnemyHeartHugger),
            EliteArchetype.Hidden => typeof(EnemyHidden),
            EliteArchetype.Oogly => typeof(EnemyOogly),
            EliteArchetype.Robe => typeof(EnemyRobe),
            EliteArchetype.Reaper => typeof(EnemyRunner),
            EliteArchetype.Loom => typeof(EnemyShadow),
            EliteArchetype.Spewer => typeof(EnemySlowMouth),
            EliteArchetype.Trudge => typeof(EnemySlowWalker),
            EliteArchetype.Tick => typeof(EnemyTick),
            EliteArchetype.Bella => typeof(EnemyTricycle),
            EliteArchetype.Chef => typeof(EnemyTumbler),
            EliteArchetype.Upscream => typeof(EnemyUpscream),
            EliteArchetype.Rugrat => typeof(EnemyValuableThrower),
            _ => null
        };
        if (dedicatedUpdateType != null)
        {
            Methods(dedicatedUpdateType, "Update");
        }
    }

    private static void Fields(Type type, params string[] names)
    {
        foreach (string name in names)
        {
            if (AccessTools.Field(type, name) == null)
            {
                throw new MissingFieldException(type.FullName, name);
            }
        }
    }

    private static void Methods(Type type, params string[] names)
    {
        foreach (string name in names)
        {
            if (AccessTools.Method(type, name) == null)
            {
                throw new MissingMethodException(type.FullName, name);
            }
        }
    }
}
