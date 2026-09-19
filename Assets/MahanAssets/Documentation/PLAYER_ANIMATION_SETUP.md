# Time Echo player animation: working setup

This package adds **TimeEchoAnimatorSetup.cs** and modifies **TimeEchoFoundationBuilder.cs**. It uses the existing `PlayerAnimationDriver.cs` and does NOT change movement, boost, Stasis, energy, rewind or audio.

## Installation for an existing project

1. Leave Play Mode. Back up / commit your Unity project.
2. Merge this package's `Assets/TimeEchoGame` directory into the matching existing directory. If you relocated the TimeEcho scripts under `Assets/MahanAssets`, update the *existing* `TimeEchoFoundationBuilder.cs` and put `TimeEchoAnimatorSetup.cs` alongside it in its Editor directory, **not** a second copy of the runtime scripts.
3. Wait until Unity finishes compiling.
4. Select the existing **Player** in the Hierarchy (the object holding `PlayerMotor2D` and `SpriteRenderer`).
5. Open `Tools > Time Echo > Animation > Setup Player Animator`.
6. Optionally drag in your sprite folder. Click `Generate clips + Animator and attach to Player`.
7. Save your scene or prefab. The Animator Controller and `.anim` clips are created under `Assets/TimeEchoGame/Generated/Animation/`.

If you also want the shared **Gameplay Core** base prefab to gain an Animator, run `Tools > Time Echo > Prefabs > Create or Update Shared Prefabs`. This refreshes the *base* prefab (may affect inherited properties); it does **not** replace a Player in your existing scene or directly rewire arbitrary standalone Player prefabs. Use the setup window for an existing Player, especially if you have already customized its appearance. Do not run `Create Playable Foundation` on a demo you do not intend to replace.

## Sprite folder layout

Use any parent path below `Assets`. Each state is a directly nested folder (case-insensitive):

```
Assets/MyGame/PlayerSprites/
  Idle/    (frame_0.png, frame_1.png, ... OR a sliced sprite sheet)
  Run/
  Jump/
  Fall/
  Boost/
  Death/
  Rewind/
```

The setup tool finds imported Sprite sub-assets as well as individual sprite PNGs, sorts frame names numerically where possible (`run_2` before `run_10`), and writes SpriteRenderer sprite curves to the clips. For a sheet, set **Texture Type = Sprite (2D and UI)**, **Sprite Mode = Multiple**, slice in Sprite Editor and Apply **before** running setup. Keep pivots/pixels-per-unit consistent across frames.

If a folder is absent or empty, a *new* clip uses the current player sprite as a static placeholder. Existing clips are not erased for absent folders. Re-running with a populated folder **replaces that clip's Sprite curve** and may replace manually edited Sprite frames; it leaves the Controller graph alone. Do not run the sprite import again if you have made manual clip-frame edits and want to preserve them.

By default, Idle/Run/Rewind loop. Jump/Fall/Boost/Death do not. You may tune Samples, looping, and transitions in Unity's Animation and Animator windows after generation. Rewind is a separate visual clip, **not** actual backwards playback of past animation frames.

## What gets connected

The generated Animator sits on the **same Player GameObject as SpriteRenderer** (the animation curve path is empty). `PlayerAnimationDriver` is assigned Animator, SpriteRenderer, motor, vitality and current TimeDirector. Root motion is disabled so the animation won't drive physics. The controller has parameters `Speed` (float), `VerticalSpeed` (float), `Grounded` (bool), `Dead` (bool), `Rewinding` (bool), `Launch` (trigger), plus seven states: Idle, Run, Jump, Fall, Boost, Death, Rewind. The existing driver fires the `Launch` trigger.

Stasis holds the current animation frame, as before. The sprite renderer's color is set to white when your imported sprite frames are applied to the selected Player (so the old yellow placeholder tint won't distort artwork).

## What is NOT included

Your actual sprite art has not been provided in this conversation, so the archive contains a generator and placeholders rather than completed clips with your characters' frames. Assign your sprite folder in the window to generate them. Unity Play Mode validation is required in your project.
