# Elite Enemy Variants

## English

### Overview

Elite Enemy Variants randomly converts supported vanilla enemies into a single Enhanced variant. Every Enhanced enemy receives stronger common statistics plus a dedicated behavior built around that enemy's original gimmick. The default chance is 35%, with no more than three Enhanced enemies per level.

Only the host needs the mod. Unmodded participants experience the Enhanced enemies normally.

For questions, bug reports, or feedback, please use [GitHub Issues](https://github.com/CapacityDown/EliteEnemyVariants/issues). A GitHub account is required to submit an issue.

### Requirements

- BepInExPack 5.4.2305 or a later compatible version
- MenuLib 2.5.2 or a later compatible version
- REPOConfig 1.2.6 or a later compatible version

### Installation

Install with a compatible mod manager, or place `EliteEnemyVariants.dll` in the profile's `BepInEx/plugins` directory. Only the host needs the mod for gameplay effects.

### Multiplayer

- The host's settings determine which enemies become Enhanced.
- Vanilla participants are fully supported and do not need Elite Enemy Variants.
- Enhanced status lasts until the stage changes.
- Player damage and knockback are not directly increased. Strength comes from improved abilities and each enemy's dedicated behavior.

### Selection and identification

- Enemies present at the start of a level and enemies that appear later can both become Enhanced.
- The common chance is 35% by default. A per-enemy chance can override it.
- At most three Enhanced encounters are selected per level by default. Each Banger or Gnome group consumes one slot.
- New and existing configurations use a combined threat limit of 4 by default.
- `SelectionSeed = 0` creates a fresh selection for each stage. A positive value repeats the same selection pattern.
- Stage Flux settings and seeds are never used for this roll.
- An Enhanced enemy displays three vanilla green heal flashes when first approached within 12 m, then two flashes every 2.5 seconds by default. The pulse pauses briefly after real damage or healing so those effects remain readable.
- Red damage flashes remain reserved for actual damage and are not used as the Enhanced identifier.

### Enhanced common effects

| Effect | Default |
|---|---:|
| Health | `1.50x` |
| Normal movement | `1.15x` |
| Attack movement | `1.20x` |
| Body movement and turning | `1.15x` |
| Attack preparation speed | `1.35x` |
| Recovery speed | `1.30x` |
| Pursuit retention | `25%` |
| Orb value on defeat | `1.50x` |
| Identification | Two vanilla green flashes every `2.5 s` |

Gnome uses its dedicated 25 HP setting instead of the common health multiplier.

Orbs dropped by defeated Enhanced enemies are worth 1.5 times their usual value, including when common health and movement enhancements are disabled. Drop counts and drop conditions remain unchanged.

### Enemy catalog

| Display name | Enhanced behavior |
|---|---|
| Animal | Notices players and enters havoc faster, prioritizes runs through nearby clusters of valuables and loaded carts, and renews pursuit once after havoc. Moving the cargo can redirect its havoc. |
| Apex Predator | Transforms faster, hunts longer while transformed, and can transform one additional time. |
| Banger | Is selected once per group and consumes one level-cap slot. Uses a five-second fuse, shortened to 3.5 seconds near a visible target within 6 m. Explosion values remain vanilla. |
| Bella | Progresses tells and cooldowns faster and performs one additional charge. |
| Birthday Boy | Directly targets and immediately attacks the player who popped a balloon. |
| Bowtie | Progresses tell and recovery faster and performs one additional yell cycle. |
| Chef | Rolls at high speed while steering toward its target for up to six seconds, with one additional tumble. |
| Cleanup Crew | Uses head and melee actions sooner, adds one close-range melee follow-up, and circles toward the opposite side of its thrown head to pressure the player while keeping clear of the bomb. |
| Clown | Walks and tracks while beaming, can switch target once, and has a bounded chance to perform a 180-degree sweep. |
| Elsa | Becomes angry faster, chases longer in large form, and can hand off to one nearby target before shrinking. |
| Gambit | Begins escaping after securing a captured player, keeps choosing reachable routes, retries stalled movement, and completes roulette and its result. |
| Gnome | Has 25 HP, stronger jumps, immediate re-attacks, valuable priority, opposite-side formation, and at most two simultaneous attackers. |
| Headgrab | Searches for and attacks heads faster, adds one headless combo, and tries once to retrieve a head dropped when stunned. Gives up if a player grabs or carries the head away. |
| Headman | Pursues a player for at least ten seconds after noticing them and can extend that pursuit once. |
| Heart Hugger | Uses lure and chomp sooner and adds one aggro cycle. After burrowing, favors a nearby exit along the last-seen player's direction of travel, keeping its emergence and lure warnings. |
| Hidden | Captures faster, carries longer, and reroutes once after becoming stuck. During each capture, it can also take one alternate escape route when a visible rescuer blocks its path, without extending the carry time. |
| Huntsman | Remains completely blind, remembers sound during aim and fire, and investigates sounds within 45 m. Miss chance rises from 0% at 5 m or closer to 30% at 45 m; misses land 1.5–2.25 m beside the target. It does not gain burst or follow-up shots. |
| Loom | Avoids the same target consecutively. After changing targets, it teleports within 30 m of the new target only when currently farther than 30 m away, preferring positions ahead of the target's movement. Arrival points must remain at least 8 m from living players. |
| Mentalist | Before charging, it can spend up to two seconds repositioning to catch visible players and nearby valuables together. It then uses its faster psychic charge and one additional bounded psychic attack. |
| Oogly | Uses a lower regular flight band, periodically patrols near floor or table height with its gaze parallel to the floor, and retries one failed dive. |
| Peeper | Applies gaze damage faster and can hand off once to another visible target. |
| Reaper | Keeps attacking while searching the last place it saw a player, then checks one nearby corner or side of cover. Stops attacking if it cannot find the player. |
| Robe | Pursues longer, can re-engage once, and maintains at least 8 m/s toward its target even while unseen. |
| Rugrat | Selects the highest-value reachable destructible item, approaches it without jumping, and makes one extra throw attempt. While holding an item, can move sideways around cover to find a clear throwing angle. |
| Shadow Child | Approaches while unseen, keeps a stable target, and reliably attacks after sustained eye contact. |
| Spewer | Immediately attaches to a newly seen player and vomits continuously for the full attachment. |
| Tick | Notices sooner and drains faster. While fleeing after filling up, can change its escape route once if a visible pursuer blocks the way, without losing its fullness or reward. |
| Trudge | Preserves the full attack animation and renews pursuit once after attacking. |
| Upscream | Pursues the player it blew away for up to eight seconds and performs exactly one follow-up blow. |

### Elites menu

Open **Elites** from the lobby or pause menu. When RoleShuffle or Stage Flux is also installed, the button appears beneath **Roles** and **Events**.

- **Enhancement Guide** explains each enemy's Enhanced behavior.
- **Enemy Settings** lets the host turn enhancement selection on or off for each enemy. Changes apply to future draws; existing enemies and already selected encounters stay unchanged.
- **Tools** shows whether host settings are available and lets you refresh the display.
- **Report a Problem** creates a report you can copy or open, then guides you to GitHub Issues. Add reproduction steps and review the contents before posting. Some personal information is masked, and nothing is uploaded automatically.

Participants who install the mod can view this menu, but only the host can change enemy settings. Unmodded participants do not need the menu to experience Enhanced enemies. Enemy locations and encounter counts are not displayed, and no persistent HUD is added.

### Configuration

All settings are available through REPOConfig and use the host's values.

#### General

| Key | Default | Range / values | Effect |
|---|---:|---|---|
| `General.Enabled` | `true` | `true`, `false` | Master switch for selection and Enhanced behavior. |
| `General.VanillaEnemiesOnly` | `true` | `true`, `false` | Keeps selection limited to the 29 supported vanilla enemy types. |
| `General.ExcludeShop` | `true` | `true`, `false` | Disables selection in shop levels. |
| `General.ExcludeArena` | `true` | `true`, `false` | Disables selection in arena levels. |
| `General.DebugLog` | `false` | `true`, `false` | Enables verbose diagnostic logging. |

#### Selection, common statistics, behavior, and visual effect

| Key | Default | Range / values | Effect |
|---|---:|---|---|
| `Selection.EnhancedChancePercent` | `35` | `0`–`100` | Common Enhanced chance per eligible enemy. |
| `Selection.MaxEnhancedPerLevel` | `3` | `0`–`100` | Maximum Enhanced cap cost per level. `0` disables random enhancement. |
| `Selection.ThreatBudgetEnabled` | `true` | `true`, `false` | Also limits the combined threat cost of Enhanced encounters. |
| `Selection.ThreatBudget` | `4` | `0`–`100` | Combined threat-cost limit used when the threat budget is enabled. |
| `Selection.SelectionSeed` | `0` | Whole number | `0` creates a fresh selection for each stage; a positive value repeats the same selection pattern. |
| `Common.CommonStatsEnabled` | `true` | `true`, `false` | Enables common health and movement bonuses without changing dedicated enemy behavior or identification. |
| `Common.HealthMultiplier` | `1.50` | `0.1`–`10` | Common maximum-health multiplier. |
| `Common.NavMoveMultiplier` | `1.15` | `0.1`–`10` | Normal movement multiplier. |
| `Common.NavAttackMoveMultiplier` | `1.20` | `0.1`–`10` | Attack movement multiplier. |
| `Common.RigidbodyMoveMultiplier` | `1.15` | `0.1`–`10` | Body movement and turning multiplier. |
| `Behavior.WindupRate` | `1.35` | `0.1`–`10` | Attack preparation speed. |
| `Behavior.CooldownRate` | `1.30` | `0.1`–`10` | Recovery speed. |
| `Behavior.ChaseRetention` | `0.25` | `0`–`0.95` | Pursuit retention. |
| `Visual.EnableVanillaPulse` | `true` | `true`, `false` | Enables two synchronized vanilla green heal flashes. |
| `Visual.PulseCycleSeconds` | `2.50` | `0.5`–`30` | Time between identification cycles. |
| `Visual.PulseGapSeconds` | `0.22` | `0.05`–`1` | Gap between the two flashes in one cycle. |
| `Visual.DelayAfterHurtSeconds` | `0.80` | `0`–`10` | Identification delay after actual enemy damage. |
| `Visual.DelayAfterRealHealSeconds` | `0.80` | `0`–`10` | Identification delay after actual enemy healing. |

#### Per-enemy selection

Every enemy section listed below has the same two selection keys.

`Animal`, `Apex Predator`, `Banger`, `Bella`, `Birthday Boy`, `Bowtie`, `Chef`, `Cleanup Crew`, `Clown`, `Elsa`, `Gambit`, `Gnome`, `Headgrab`, `Headman`, `Heart Hugger`, `Hidden`, `Huntsman`, `Loom`, `Mentalist`, `Oogly`, `Peeper`, `Reaper`, `Robe`, `Rugrat`, `Shadow Child`, `Spewer`, `Tick`, `Trudge`, `Upscream`

| Key | Default | Range / values | Effect |
|---|---:|---|---|
| `<Display name>.Enabled` | `true` | `true`, `false` | Enables random enhancement for that enemy. |
| `<Display name>.ChancePercent` | `-1` | `-1`, or whole numbers `0`–`100` | `-1` uses the common chance; `0`–`100` overrides it. |

#### Dedicated enemy settings

| Key | Default | Range / values | Effect |
|---|---:|---|---|
| `Banger.FuseSeconds` | `5` | `0.5`–`30` | Standard Enhanced fuse duration. |
| `Banger.NearTargetFuseSeconds` | `3.5` | `0.5`–`30` | Fuse duration near a visible target. |
| `Banger.NearTargetDistance` | `6` | `0.5`–`50` | Near-target threshold in metres. |
| `Banger.FuseSyncIntervalSeconds` | `0.5` | `0.1`–`5` | Fuse update interval. |
| `Clown.WalkWhileAttacking` | `true` | `true`, `false` | Moves toward the target while beaming. |
| `Clown.TargetSwitch` | `true` | `true`, `false` | Allows one target switch per attack. |
| `Clown.TrackSpeed` | `15` | `1`–`300` | Beam tracking rotation speed. |
| `Clown.BeamRange` | `10` | `1`–`100` | Enhanced beam range in metres. |
| `Clown.BeamDurationMin` | `1` | `0.1`–`30` | Minimum beam duration in seconds. |
| `Clown.BeamDurationMax` | `2` | `0.1`–`30` | Maximum beam duration in seconds. |
| `Clown.SpinChancePercent` | `10` | `0`–`100` | Chance of the bounded 180-degree sweep. |
| `Clown.SpinSpeedDegPerSec` | `300` | `30`–`3000` | Sweep rotation speed. |
| `Gambit.RouletteStunImmune` | `true` | `true`, `false` | Ignores stun only while roulette is active. |
| `Gambit.RouletteDurationSeconds` | `5` | `1`–`30` | Enhanced roulette duration. |
| `Gnome.ZeroNoticeDelay` | `true` | `true`, `false` | Removes the Enhanced group's notice delay. |
| `Gnome.ZeroAttackCooldown` | `true` | `true`, `false` | Removes the Enhanced group's re-attack cooldown. |
| `Gnome.Health` | `25` | `1`–`10000` | Enhanced Gnome health. |
| `Gnome.ConcurrentAttackers` | `2` | `1`–`8` | Maximum simultaneous Enhanced Gnome attackers. |
| `Gnome.ValuablePriorityRadius` | `5` | `0`–`50` | Valuable-priority search radius in metres. |
| `Gnome.AttackOrbitSpeed` | `4` | `0.1`–`30` | Opposite-side formation speed. |
| `Gnome.JumpMultiplier` | `1.5` | `0.1`–`10` | Jump strength multiplier. |
| `Gnome.AttackRadius` | `1.1` | `0.1`–`10` | Attack formation radius in metres. |
| `Huntsman.SoundInvestigateRange` | `45` | `1`–`500` | Maximum sound investigation distance in metres. |
| `Oogly.LowPatrolHeight` | `1` | `0.5`–`4` | Low-patrol height in metres. |
| `Oogly.LowPatrolIntervalSeconds` | `10` | `3`–`60` | Interval between low-patrol attempts. |
| `Robe.UnseenMoveSpeed` | `8` | `1`–`25` | Minimum pursuit speed while unseen. |
| `Shadow Child.EyeContactSeconds` | `3` | `0.2`–`30` | Synchronized eye-contact time before attack. |
| `Shadow Child.ApproachRadius` | `50` | `1`–`200` | Maximum unseen approach radius in metres. |
| `Shadow Child.ApproachSpeed` | `4` | `0.1`–`30` | Unseen approach speed. |

### Compatibility

- Stage Flux and Role Shuffle can be used together with Elite Enemy Variants. Enhancement selection remains independent from Stage Flux settings.
- Enemies spawned by Stage Flux can become Enhanced. RoleShuffle Ninja keeps its stealth-related protection, while Vampire and Hunter rewards account for Enhanced enemies.
- If EnemySpeedUp is installed, shared movement boosts are not stacked. Dedicated enemy behavior remains available.
- Older single-enemy enhancement mods may overlap with this mod. Matching changes are disabled automatically where supported.

### Gameplay notes

- Enhanced variants intentionally make enemy-specific attacks more persistent and less predictable; the 35% chance and cap of three are the recommended starting values.
- The green pulse is an identifier only and does not heal the enemy.
- Unless listed above, enemy attacks and other basic rules remain unchanged.

## 日本語

### 概要

Elite Enemy Variants は、対応するバニラ敵を確率で単一の「Enhanced」個体へ変更します。Enhanced 個体には共通ステータス強化に加え、元の敵が持つ固有ギミックを活かした専用挙動が適用されます。既定の強化確率は35%、1レベルあたりの上限は3体です。

導入が必要なのはホストだけです。MOD未導入の参加者にもEnhanced個体の挙動が通常どおり反映されます。

ご質問、不具合報告、ご意見は[GitHub Issues](https://github.com/CapacityDown/EliteEnemyVariants/issues)からお送りください。投稿にはGitHubアカウントが必要です。

### 必須環境

- BepInExPack 5.4.2305 以降の互換バージョン
- MenuLib 2.5.2 以降の互換バージョン
- REPOConfig 1.2.6 以降の互換バージョン

### 導入方法

対応MODマネージャーでインストールするか、`EliteEnemyVariants.dll` をプロファイルの `BepInEx/plugins` フォルダーへ配置してください。ゲームプレイ効果はホストのみの導入で動作します。

### マルチプレイ

- 強化抽選と敵の挙動はホストが管理します。
- バニラ参加者に対応しており、Elite Enemy Variants の導入は不要です。
- 強化状態はステージが変わるまで維持されます。
- プレイヤーへのダメージ値や吹き飛ばし値は直接増やしません。共通能力と各敵の専用挙動によって強化します。

### Elitesメニュー

ロビーまたはポーズメニューの **Elites** から開きます。RoleShuffleやStage Fluxを同時導入している場合は、**Roles**・**Events** の下に配置されます。

- **Enhancement Guide**：各敵の強化挙動を確認できます。
- **Enemy Settings**：ホストが敵ごとに強化抽選のON/OFFを切り替えられます。変更は今後の抽選に適用され、既存の敵や抽選済みの遭遇は変更しません。
- **Tools**：ホスト設定の表示状態を確認し、表示データを再取得できます。
- **Report a Problem**：不具合レポートを作成・コピー・閲覧し、GitHub Issuesへ移動できます。再現手順を追記し、内容を確認してから投稿してください。個人情報は一部マスクされ、自動送信は行いません。

参加者もMODを導入すればメニューを閲覧できますが、敵設定を変更できるのはホストだけです。未導入の参加者にも強化挙動は適用されます。敵の位置や出現数は表示せず、常設HUDも追加しません。

### 抽選と識別

- レベル開始時からいる敵も、後から出現する敵もEnhanced個体になることがあります。
- 共通確率は既定で35%です。敵別確率で上書きできます。
- 既定では1レベルにつき最大3遭遇です。BangerとGnomeは群れごとに1枠を消費します。
- 新規・既存設定とも、脅威度合計4の上限を既定で使用します。
- `SelectionSeed = 0` はステージごとに新しいシードを生成します。正の値にするとテスト用に抽選を再現できます。
- Stage Flux の設定やシードは抽選に使用しません。
- Enhanced 個体は、プレイヤーが初めて12 m以内へ近づいたときにバニラの緑色回復エフェクトを3回表示し、その後は既定で2.5秒ごとに2回表示します。実際の被ダメージや回復の直後は識別表示を一時停止します。
- 赤いダメージ点滅は実際のダメージ表示として維持し、Enhanced の識別には使用しません。

### Enhanced 共通効果

| 効果 | 既定値 |
|---|---:|
| 体力 | `1.50倍` |
| 通常時の移動 | `1.15倍` |
| 攻撃時の移動 | `1.20倍` |
| 本体の移動・旋回 | `1.15倍` |
| 攻撃準備速度 | `1.35倍` |
| 復帰速度 | `1.30倍` |
| 追跡維持 | `25%` |
| 撃破時のオーブ金額 | `1.50倍` |
| 識別 | `2.5秒`ごとに緑色のバニラエフェクトを2回 |

Gnome は共通体力倍率ではなく、専用設定の25 HPを使用します。

Enhanced個体を倒した際に落とすオーブの金額は通常の1.5倍になります。共通の体力・移動強化をOFFにしていても適用されます。オーブの出現数と出現条件は変更しません。

### 敵一覧

| 表示名 | Enhanced 挙動 |
|---|---|
| Animal | プレイヤーへの気付きと暴走への移行が速くなり、近くの換金物が集まる場所や積載カートを優先して走り抜けます。荷物を動かすことで暴走先を誘導でき、暴走後には1度だけ追跡を更新します。 |
| Apex Predator | 変身が速くなり、変身中の追跡が長くなり、1度だけ追加で再変身します。 |
| Banger | 群れごとに1回抽選し、レベル上限を1枠消費します。導火線は5秒、視認中の標的が6 m以内にいる場合は3.5秒になります。爆発値はバニラのままです。 |
| Bella | 予兆とクールダウンの進行が速くなり、突進を1回追加します。 |
| Birthday Boy | 風船を割ったプレイヤーを直接標的にし、即座に攻撃します。 |
| Bowtie | 予兆と復帰が速くなり、叫びを1サイクル追加します。 |
| Chef | 最大6秒間、高速で転がりながら標的へ旋回追尾し、追加で1回転倒攻撃します。 |
| Cleanup Crew | 頭投げと近接攻撃の間隔が短くなり、近距離での追撃を1回追加します。頭投げ後は爆弾から距離を取りつつ反対側へ回り込み、プレイヤーを挟み込みます。 |
| Clown | ビーム中も歩行・追尾し、攻撃中に1度だけ標的を変更でき、一定確率で範囲を限定した180度薙ぎ払いを行います。 |
| Elsa | 怒り状態への移行が速くなり、大型時の追跡が長くなり、縮小前に近くの標的へ1度引き継ぎます。 |
| Gambit | 捕まえたプレイヤーを確保すると逃走を開始し、到達可能な経路を選び続け、停止時は再試行しながらルーレットと結果を最後まで完了します。 |
| Gnome | 25 HP、ジャンプ強化、即時再攻撃、換金物優先、挟み込み陣形、最大2体の同時攻撃を使用します。 |
| Headgrab | 頭の探索と攻撃が速くなり、頭なしコンボを1回追加します。気絶で落とした頭を復帰後に1度だけ取り戻そうとしますが、プレイヤーがつかんだり持ち去った場合は諦めます。 |
| Headman | プレイヤーを発見すると最低10秒追跡し、その追跡を1度だけ延長します。 |
| Heart Hugger | 誘引と噛みつきが速くなり、敵対サイクルを1回追加します。潜った後は、最後に見たプレイヤーの進行方向にある出口付近を優先して再出現します。出現と誘引の予兆は維持します。 |
| Hidden | 捕獲が速くなり、運搬が長くなり、停止した場合に1度だけ経路を引き直します。さらに、視認した救助者が進路を塞ぐと、捕獲1回につき1度だけ別経路へ逃げます。この回避で運搬時間は延長しません。 |
| Huntsman | 完全な盲目を維持したまま、照準・射撃中も音を記憶し、45 m以内の音を調査します。外す確率は5 m以下の0%から45 mの30%まで距離に応じて上昇し、外れ弾は標的の1.5～2.25 m横へ着弾します。連射や追撃射撃は追加しません。 |
| Loom | 同じ標的の連続選択を避けます。標的が変わり、現在地が新標的から30 mより遠い場合のみ、新標的の30 m以内へテレポートします。標的の進行方向を優先して先回りし、生存プレイヤーの8 m以内には出現しません。 |
| Mentalist | 念力を溜める前に、視認したプレイヤーと近くの換金物をまとめて巻き込める位置へ最長2秒移動します。その後、速い溜めと上限付きの追加念力攻撃を行います。 |
| Oogly | 通常の飛行高度が低くなり、視線を床と平行に保ちながら床・机付近を定期巡回し、失敗した急降下を1度再試行します。 |
| Peeper | 視線ダメージが速くなり、別の視認中標的へ1度だけ引き継ぎます。 |
| Reaper | 見失ったプレイヤーの最終目撃地点まで攻撃しながら探索し、さらに近くの角や遮蔽物の脇を1か所確認します。発見できなければ攻撃を解除します。 |
| Robe | 追跡時間が長くなり、1度だけ再追跡し、プレイヤーに見られていない状態でも標的方向へ最低8 m/sで移動します。 |
| Rugrat | 到達可能で最も価値の高い破壊可能アイテムを選び、接近中はジャンプせず、投擲を1回追加で試みます。アイテムを持った状態で遮蔽物の左右へ回り込み、投げられる角度を探します。 |
| Shadow Child | 見られていない間に接近し、標的を安定して追い、一定時間視線を合わせられると確実に攻撃します。 |
| Spewer | 新たなプレイヤーを視認すると即座に寄生し、寄生中は常に嘔吐を続けます。 |
| Tick | 気付きと吸収が速くなります。満腹で逃走中、見えている追手が進路をふさいだ場合は1度だけ逃走経路を変更します。満腹状態と報酬は維持します。 |
| Trudge | 攻撃アニメーションを最後まで維持し、攻撃後に1度だけ追跡を更新します。 |
| Upscream | 吹き飛ばしたプレイヤーを最大8秒追跡し、追撃の吹き飛ばしを正確に1回行います。 |

### 設定

すべての設定はREPOConfigから変更でき、ホスト側の値が使用されます。

#### 一般

| キー | 既定値 | 範囲 / 値 | 効果 |
|---|---:|---|---|
| `General.Enabled` | `true` | `true`, `false` | 抽選とEnhanced挙動の全体スイッチです。 |
| `General.VanillaEnemiesOnly` | `true` | `true`, `false` | 抽選を対応するバニラ敵29種に限定します。 |
| `General.ExcludeShop` | `true` | `true`, `false` | ショップレベルで抽選しません。 |
| `General.ExcludeArena` | `true` | `true`, `false` | アリーナレベルで抽選しません。 |
| `General.DebugLog` | `false` | `true`, `false` | 詳細な診断ログを有効にします。 |

#### 抽選・共通能力・挙動・視覚効果

| キー | 既定値 | 範囲 / 値 | 効果 |
|---|---:|---|---|
| `Selection.EnhancedChancePercent` | `35` | `0`～`100` | 対象敵1体ごとの共通Enhanced確率です。 |
| `Selection.MaxEnhancedPerLevel` | `3` | `0`～`100` | 1レベルのEnhanced上限コストです。`0`でランダム強化を停止します。 |
| `Selection.ThreatBudgetEnabled` | `true` | `true`, `false` | Enhanced遭遇の脅威度合計も制限します。 |
| `Selection.ThreatBudget` | `4` | `0`～`100` | 脅威度上限が有効な場合に使用する合計上限です。 |
| `Selection.SelectionSeed` | `0` | 整数 | `0`はステージごとに新しく抽選し、正の値は同じ抽選傾向を再現します。 |
| `Common.CommonStatsEnabled` | `true` | `true`, `false` | 敵固有挙動と識別表示を維持したまま、共通の体力・移動補正をON/OFFします。 |
| `Common.HealthMultiplier` | `1.50` | `0.1`～`10` | 共通の最大体力倍率です。 |
| `Common.NavMoveMultiplier` | `1.15` | `0.1`～`10` | 通常時の移動倍率です。 |
| `Common.NavAttackMoveMultiplier` | `1.20` | `0.1`～`10` | 攻撃時の移動倍率です。 |
| `Common.RigidbodyMoveMultiplier` | `1.15` | `0.1`～`10` | 本体の移動・旋回倍率です。 |
| `Behavior.WindupRate` | `1.35` | `0.1`～`10` | 攻撃準備速度です。 |
| `Behavior.CooldownRate` | `1.30` | `0.1`～`10` | 復帰速度です。 |
| `Behavior.ChaseRetention` | `0.25` | `0`～`0.95` | 追跡の維持率です。 |
| `Visual.EnableVanillaPulse` | `true` | `true`, `false` | 同期される緑色のバニラ回復エフェクト2回を有効にします。 |
| `Visual.PulseCycleSeconds` | `2.50` | `0.5`～`30` | 識別サイクルの間隔です。 |
| `Visual.PulseGapSeconds` | `0.22` | `0.05`～`1` | 1サイクル内の2回の点滅間隔です。 |
| `Visual.DelayAfterHurtSeconds` | `0.80` | `0`～`10` | 実際の敵ダメージ後に識別表示を待つ時間です。 |
| `Visual.DelayAfterRealHealSeconds` | `0.80` | `0`～`10` | 実際の敵回復後に識別表示を待つ時間です。 |

#### 敵別抽選

次の全敵セクションに共通する2つの抽選キーがあります。

`Animal`, `Apex Predator`, `Banger`, `Bella`, `Birthday Boy`, `Bowtie`, `Chef`, `Cleanup Crew`, `Clown`, `Elsa`, `Gambit`, `Gnome`, `Headgrab`, `Headman`, `Heart Hugger`, `Hidden`, `Huntsman`, `Loom`, `Mentalist`, `Oogly`, `Peeper`, `Reaper`, `Robe`, `Rugrat`, `Shadow Child`, `Spewer`, `Tick`, `Trudge`, `Upscream`

| キー | 既定値 | 範囲 / 値 | 効果 |
|---|---:|---|---|
| `<表示名>.Enabled` | `true` | `true`, `false` | その敵のランダム強化を有効にします。 |
| `<表示名>.ChancePercent` | `-1` | `-1`または整数`0`～`100` | `-1`は共通確率、`0`～`100`は敵別上書きです。 |

#### 敵別専用設定

| キー | 既定値 | 範囲 / 値 | 効果 |
|---|---:|---|---|
| `Banger.FuseSeconds` | `5` | `0.5`～`30` | 通常のEnhanced導火線時間です。 |
| `Banger.NearTargetFuseSeconds` | `3.5` | `0.5`～`30` | 視認中の標的が近い場合の導火線時間です。 |
| `Banger.NearTargetDistance` | `6` | `0.5`～`50` | 近距離判定（m）です。 |
| `Banger.FuseSyncIntervalSeconds` | `0.5` | `0.1`～`5` | 導火線の更新間隔です。 |
| `Clown.WalkWhileAttacking` | `true` | `true`, `false` | ビーム中も標的へ移動します。 |
| `Clown.TargetSwitch` | `true` | `true`, `false` | 攻撃中に1度だけ標的変更を許可します。 |
| `Clown.TrackSpeed` | `15` | `1`～`300` | ビーム追尾の回転速度です。 |
| `Clown.BeamRange` | `10` | `1`～`100` | Enhancedビームの射程（m）です。 |
| `Clown.BeamDurationMin` | `1` | `0.1`～`30` | ビーム最短時間（秒）です。 |
| `Clown.BeamDurationMax` | `2` | `0.1`～`30` | ビーム最長時間（秒）です。 |
| `Clown.SpinChancePercent` | `10` | `0`～`100` | 180度薙ぎ払いの発生確率です。 |
| `Clown.SpinSpeedDegPerSec` | `300` | `30`～`3000` | 薙ぎ払いの回転速度です。 |
| `Gambit.RouletteStunImmune` | `true` | `true`, `false` | ルーレット中だけスタンを無効化します。 |
| `Gambit.RouletteDurationSeconds` | `5` | `1`～`30` | Enhancedルーレットの時間です。 |
| `Gnome.ZeroNoticeDelay` | `true` | `true`, `false` | Enhanced群れの気付き待機を削除します。 |
| `Gnome.ZeroAttackCooldown` | `true` | `true`, `false` | Enhanced群れの再攻撃待機を削除します。 |
| `Gnome.Health` | `25` | `1`～`10000` | Enhanced Gnomeの体力です。 |
| `Gnome.ConcurrentAttackers` | `2` | `1`～`8` | 同時に攻撃するEnhanced Gnomeの最大数です。 |
| `Gnome.ValuablePriorityRadius` | `5` | `0`～`50` | 換金物を優先する探索半径（m）です。 |
| `Gnome.AttackOrbitSpeed` | `4` | `0.1`～`30` | 挟み込み陣形の移動速度です。 |
| `Gnome.JumpMultiplier` | `1.5` | `0.1`～`10` | ジャンプ倍率です。 |
| `Gnome.AttackRadius` | `1.1` | `0.1`～`10` | 攻撃陣形の半径（m）です。 |
| `Huntsman.SoundInvestigateRange` | `45` | `1`～`500` | 音を調査する最大距離（m）です。 |
| `Oogly.LowPatrolHeight` | `1` | `0.5`～`4` | 低空巡回の高さ（m）です。 |
| `Oogly.LowPatrolIntervalSeconds` | `10` | `3`～`60` | 低空巡回を試みる間隔です。 |
| `Robe.UnseenMoveSpeed` | `8` | `1`～`25` | 非視認時の最低追跡速度です。 |
| `Shadow Child.EyeContactSeconds` | `3` | `0.2`～`30` | 攻撃までに必要な同期視線時間です。 |
| `Shadow Child.ApproachRadius` | `50` | `1`～`200` | 非視認時に接近する最大半径（m）です。 |
| `Shadow Child.ApproachSpeed` | `4` | `0.1`～`30` | 非視認時の接近速度です。 |

### 互換性

- Stage Flux、Role Shuffleと同時に使用できます。強化抽選にStage Fluxの設定は使用しません。
- Stage Fluxが出現させた敵もEnhanced個体になることがあります。RoleShuffleのNinjaが持つ隠密上の保護を維持し、Vampire・Hunterの報酬ではEnhanced個体も考慮されます。
- EnemySpeedUp導入時は共通の移動強化を重複させず、敵別の挙動強化は維持します。
- 旧単体敵強化MODと内容が重なる場合、対応している重複部分は自動的に無効化します。

### ゲームプレイ上の注意

- Enhanced 個体は固有攻撃が継続しやすく予測しにくくなります。最初は既定の35%・上限3を推奨します。
- 緑色の点滅は識別表示だけで、敵を回復しません。
- 上記に記載していない攻撃や基本ルールは変更しません。
