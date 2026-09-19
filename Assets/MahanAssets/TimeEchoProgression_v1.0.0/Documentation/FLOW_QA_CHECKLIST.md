# Flow QA checklist

## Configuration

- Run `Tools > Time Echo > Progression > Validate Configuration` with zero errors.
- Confirm all 20 gameplay scenes are enabled in Build Settings.
- Confirm every scene name matches case exactly.
- Confirm the Levels list is in the intended campaign order.

## Main loop

- Launch from the menu scene. New Game loads level 1.
- Continue is disabled before the first New Game.
- Return to the menu after entering a level. Continue loads the last entered level.
- Trigger each level exit twice quickly. Only one transition starts.
- Confirm the final level uses the win transition and opens the win screen or win scene.

## Death

- Kill the player during ordinary movement, boost, and rewind.
- Confirm rewind/stasis releases and time scale returns to normal.
- Confirm the death cutscene plays once.
- Confirm the restart/loading sequence is separate from the death cutscene.
- Test every Death Destination Mode.
- Set previous chance to 0: it must always restart.
- Set previous chance to 1: it must go back whenever a previous level exists.
- Die on level 1 with Previous Level selected: the configured first-level fallback must run.
- Confirm highest unlocked progress remains unless erase-forward-progress is enabled.

## Optional paths

- Disable each global sequence one at a time. Loading must still complete.
- Disable automatic vitality watching and test `PlayerDeathReporter` or the manual API.
- Remove the Win scene name. The in-place win overlay must still appear.
- Enable Require All Collectibles on an exit and test both incomplete and complete states.
- Test a per-level next-scene override.
- Test a trigger-level destination override.
- Test a per-level death rule and sequence override.

## UI and presentation

- Test mouse interaction using the project's chosen input system.
- Test 16:9, ultrawide, and 4:3 Game views.
- Test skip enabled and disabled.
- Test a voice clip longer than Minimum Hold with Wait For Voice enabled.
- Confirm custom FlowEventRelay callbacks fire once.

## Build

- Make a development build. Scene loading can behave differently from Play Mode when scenes are absent from Build Settings.
- Test New Game, Continue, death, next map, final win, return to menu, and quit in the build.
