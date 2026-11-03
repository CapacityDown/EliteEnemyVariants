## 5.0.0
- Added support for the Sunstone Update.
- Added Pony enhancements: armed-player priority, tongue flanking, obstacle removal, ceiling interception and ceiling patrols before spotting a player. Can grab, lift and fling reachable players from the ceiling, releasing them if there is no clear throwing space.
- Updated the required menu and configuration mods.
- Added the Elites menu with enemy enhancement descriptions, host-controlled per-enemy switches and display refresh. Uses the Roles menu layout and appears below Roles and Events when RoleShuffle or Stage Flux is installed.
- Added support for all 21 game languages in Elites, following the game's language setting automatically.
- Added bug-report tools to Elites and GitHub Issues guidance for questions, bug reports and feedback.
- Increased the value of orbs dropped by defeated Enhanced enemies to 1.5 times their usual value.
- Changed Animal to prioritize nearby clusters of valuables and loaded carts during havoc.
- Added an alternate escape route for Hidden when a visible rescuer blocks its path, limited to once per capture without extending its carry time.
- Added repositioning for Mentalist before charging to catch players and valuables together.
- Changed Loom to favor teleport destinations ahead of its new target while keeping distance from living players.
- Added movement around cover for Rugrat while holding an item to find a throwing angle.
- Added one attempt for Headgrab to recover a head dropped when stunned, giving up when a player rescues it.
- Added one escape-route change for Tick when a visible pursuer blocks its path after it fills up.
- Changed Heart Hugger to favor exits along the last-seen player's direction when reappearing after burrowing.
- Added flanking for Cleanup Crew opposite its thrown head while keeping clear of the bomb.
- Changed Reaper to check one nearby corner after searching a player's last-seen position before ending its attack.

## 4.0.2
- Added support for enhancing enemies spawned by Stage Flux.
- Improved compatibility with RoleShuffle: Ninja retains its stealth protection, while Vampire and Hunter rewards account for Enhanced enemies.

## 4.0.1
- Changed per-enemy chance settings to accept `-1` or whole percentages from 0 to 100.
- Added Threat Budget with a default limit of 4 while retaining the maximum Enhanced encounter count.
- Added an option to enable or disable common health and movement enhancements independently from dedicated behavior and identification.
- Improved Enhanced selection to reduce the influence of enemy appearance order.
- Changed Banger and Gnome selection to count each group as one encounter toward the level limit.
- Added a stronger green identification pulse when a player first approaches an Enhanced enemy.
- Improved Enhanced enemy stability while preserving common bonuses and visual identification.
- Reduced overlapping effects when supported single-enemy enhancement mods are installed.
- Reorganized REPOConfig settings into consistent global groups and alphabetical enemy sections.

## 4.0.0
- First Release
