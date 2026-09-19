# Time Echo Progression v1.0.0

This is the scene-flow package for **TimeEcho v1.0.8 InstantBoost GameTuning**. It is separate on purpose: it does not overwrite the boost, rewind, vitality, camera, or collectible scripts.

## What it handles

- Generated placeholder main menu with New Game, Continue, and Quit
- Menu-to-game transition
- Optional level-intro cutscene on every map
- Exit trigger and asynchronous next-map transition
- Optional Game Master guidance subtitle, voice clip, and sound effect on every sequence
- Automatic detection of `PlayerVitality.Died`
- Optional death cutscene followed by a separate death/restart transition
- Six death destinations, including a configurable chance of returning to the previous map
- Final winning transition and placeholder win screen
- Saved current level, highest unlocked level, death count, and win state
- Per-level overrides for intro, next-map, death, restart, and death policy
- Runtime events for connecting cameras, animation, Timeline adapters, particles, and custom UI

## Install

1. Keep the accepted **TimeEcho v1.0.8** foundation in the project.
2. Copy this package's `Assets/TimeEchoProgression` folder into your Unity project's `Assets` folder.
3. Let Unity compile. The installer creates:
   - `Assets/TimeEchoProgression/Resources/TimeEchoFlowConfig.asset`
   - `Assets/TimeEchoProgression/Generated/Prefabs/LevelExitTrigger.prefab`
   - `Assets/TimeEchoProgression/Generated/Prefabs/GameMasterSequenceTrigger.prefab`
   - `Assets/TimeEchoProgression/Generated/Prefabs/CustomMenuActions.prefab`
4. Open `TimeEchoFlowConfig.asset`. Replace `MainMenu`, `Win`, and `Level_01` to `Level_20` with the exact names of your scenes.
5. Add every configured scene to **File > Build Settings** and enable it.
6. Run **Tools > Time Echo > Progression > Validate Configuration**.
7. Put `LevelExitTrigger.prefab` at the end of every gameplay map. Scale its collider to cover the exit.

You do not need to place a manager in any scene. The config in `Resources` creates one persistent manager automatically.

## Important scene rule

The order of the Levels list is the real progression order. The exit in level 4 loads entry 5 unless that level or trigger has a destination override. This means you can reorder the campaign without editing scripts.

## Death behavior

The default is `ChancePreviousOtherwiseRestart` with `Previous Level Chance = 0.35`.

- 35% of deaths load the previous map.
- 65% restart the current map.
- On the first map, it restarts because no previous map exists.
- Highest unlocked progress is preserved, so Continue does not permanently punish the player.
- Enable `Erase Forward Progress When Sent Back` only if you intentionally want a harsher game.

Other policies are Restart Current, Previous Level, First Level, Main Menu, and Specific Scene. The global rule is under **Death**. Any level can override it.

## Optional sequences

Every sequence has an `Enabled` checkbox. Disable any of these independently:

- Menu To Game
- Level Intro
- Next Level
- Death (the cutscene before choosing a destination)
- Restart (the loading transition after death)
- Winning
- Return To Menu

Each sequence supports:

- None, Subtitle Only, Cutscene, Fade, or Glitch presentation
- Game Master guidance text
- Game Master voice clip placeholder
- Sound-effect placeholder
- Unscaled delay, fade-in, minimum hold, and fade-out timing
- Waiting for voice playback to finish
- Input locking, optional skipping, cover color, dim amount, glitch intensity, and letterbox

To customize only one level, expand its Intro, Next Level, Death, Restart, or Death Rule override and enable `Use Override`.

## Custom art and cutscenes

The generated visuals are deliberately plain. Add `FlowEventRelay` to a scene object and connect its UnityEvents to your Animator, camera controller, particles, or Timeline adapter. It exposes death, level entry, win, next-level transition, death reload, restart, and generic transition events.

For an extra Game Master speech inside a map, create a sequence asset through **Assets > Create > Time Echo > Progression > Sequence**, assign it to `GameMasterSequenceTrigger.prefab`, and place the trigger in the map.

## Custom menu

The generated menu is optional. Disable `Show Generated Main Menu` in the config, place `CustomMenuActions.prefab` in your own menu scene, and wire your UI Buttons to `TimeEchoMenuActions.NewGame`, `ContinueGame`, `MainMenu`, `RestartLevel`, or `QuitGame`.

## Integration with v1.0.8

- `PresentationDirector` locks movement and boost input during sequences.
- `TimeDirector` is forced back to Flowing before scene changes, preventing a scene transition from inheriting rewind/stasis time scale.
- A newly loaded scene gets its own rewind timeline, so the player cannot rewind into an unloaded map.
- `PlayerVitality.Died` starts the death loop automatically.
- `GameSession` can optionally block the exit until all registered collectibles are collected.

If your player does not use `PlayerVitality`, call `TimeEchoFlowManager.Instance.ReportPlayerDeath()` from its death logic. If automatic discovery is disabled, add `PlayerDeathReporter` to the player.
