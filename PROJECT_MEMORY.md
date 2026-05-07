# PROJECT_MEMORY

## Purpose

This file is the current project memory for `IN THE NIGHT`. Use it as the first reference before changing gameplay, scene flow, anomaly rules, multiplayer flow, UI, or MVP scope.

Last updated: 2026-05-07

## Current Game Identity

- Project name: `IN THE NIGHT`
- Engine: Unity / URP
- Current state: prototype / vertical slice under active development
- Main genre: co-op horror, anomaly detection, observation and memory game
- Target platform: PC first
- Target player count: 1-4 players
- Best experience: 2-4 players

`IN THE NIGHT` is a multiplayer horror game where players enter a night-time location, memorize the normal state of the map, investigate what changed, and submit one shared checklist answer as a team.

The current product identity is the anomaly checklist loop. Older train-based ideas and Unity Netcode experiments exist in the project, but they are not the main gameplay direction right now.

## Latest UX / Flow Updates

- Victory flow now stays in the gameplay scene and ends through the in-canvas `GameEndPanel`.
- `Memorize` manual advance now has a keyboard fallback through `Enter` in addition to the button.
- `Investigation` now shows a late warning message before time runs out:
  - `ถ้าไม่ submit ระบบจะใช้ checklist ปัจจุบันทันที`
- Phase unlock now has a centered popup with a padlock image and unlock message.
- `GameStatusUI` now contains audio clip slots for:
  - investigation warning
  - phase unlock
- `PhotonScenePlayerAvatar` now supports walk/run footstep audio.
- Added `LoopBgmSfx` for looping BGM with multiple tracks and crossfade transitions.

## High Concept

Players are trapped in a strange place at night. They must study the environment while it is normal, return to a safe spawn room, wait while the game rolls anomalies, then explore again to find what has changed.

The game is interesting because the pressure comes from memory, uncertainty, communication, and the fear that small details may be wrong. It is not combat-focused.

## Core Pillars

- Observation: players study object placement, lighting, sound, pictures, and environmental details.
- Memory: players must remember the normal state before anomalies appear.
- Communication: players discuss what changed and agree on checklist categories.
- Pressure: wrong answers push the team back to the start of the current phase.
- Atmosphere: dark night-time spaces, subtle changes, and uncanny events create tension.

## Current Core Loop

1. Load the gameplay scene.
2. Players spawn in the Spawn Room.
3. The current round begins.
4. The active progression phase determines which map areas are open.
5. The Spawn Room door opens during the Memorize phase.
6. Players explore the open area and memorize the normal map state.
7. Players manually start investigation, unless a memorize timer is enabled.
8. The game enters Spawn Lockdown.
9. Players are returned to their spawn pads.
10. The Spawn Room door closes.
11. The game resets anomaly points to normal, then rolls anomalies for the current phase.
12. The Spawn Room door re-opens for Investigation.
13. Players explore the unlocked map area.
14. Players open the checklist and select anomaly categories.
15. All players submit.
16. The game evaluates one shared checklist result.
17. If correct, the game advances to the next round.
18. If wrong, the game returns to the first round of the current phase.
19. Clearing round 7 triggers Victory and shows the in-game end panel.

## Round And Phase Rules

The game currently uses 7 total rounds.

- Rounds 1-3: Phase 1
- Rounds 4-5: Phase 2
- Rounds 6-7: Phase 3

Phase access is cumulative.

- Phase 1: center zone only
- Phase 2: center zone + left zone
- Phase 3: center zone + left zone + right zone

When a new phase begins, older zones stay open.

## Win, Fail, And Reset Rules

The core progression uses only Correct or Wrong.

- Correct answer: advance to the next round.
- Correct answer on round 7: enter Victory.
- Wrong answer: return to the first round of the current phase.
- Wrong answer in Phase 1: return to round 1.
- Wrong answer in Phase 2: return to round 4.
- Wrong answer in Phase 3: return to round 6.

Score is not part of the main progression loop. Some code still keeps `currentScore` for compatibility/UI history, but the active game rule is round and phase progression.

## Game Phases In Code

`GameRoundManager.GamePhase` currently contains:

- `Memorize`
- `SpawnLockdown`
- `Investigation`
- `RoundTransition`
- `Victory`

Current flow:

- `Memorize`: players inspect the normal state.
- `SpawnLockdown`: players are held in Spawn while anomalies are prepared.
- `Investigation`: players search and submit checklist choices.
- `RoundTransition`: short delay after the result.
- `Victory`: final win state after clearing all rounds.

## Timing Rules

Memorize phase:

- Default direction: no required time limit.
- Manual advance is supported through `MemorizePhaseAdvanceUI`.
- `GameRoundManager` still supports an optional memorize timer through inspector settings.

Investigation phase default durations:

- Phase 1: 120 seconds
- Phase 2: 180 seconds
- Phase 3: 240 seconds

These values are editable in `GameRoundManager`.

## Spawn Room Rules

The Spawn Room is a gameplay control space.

- During `Memorize`, the Spawn Room door opens.
- During `SpawnLockdown`, the Spawn Room door closes.
- During `Investigation`, the Spawn Room door opens.
- During phase resets, the local player can be teleported back to the assigned spawn pad.

The goal is to control pacing and prevent players from leaving while anomalies are being rolled.

## Door And Map Access System

Current helper scripts:

- `PhaseDoorController`
- `PhaseAccessManager`

`PhaseAccessManager` watches `GameRoundManager` state and controls:

- Spawn Room door open/closed by gameplay phase.
- Phase 2 doors when progression phase is 2 or higher.
- Phase 3 doors when progression phase is 3 or higher.
- Optional phase-change audio cues.

Scene wiring still needs validation in the final target gameplay scene.

## Anomaly System

The anomaly system is the main gameplay mechanic.

Current anomaly categories:

- `MissingObject`
- `MovedObject`
- `ExtraObject`
- `ChangedObject`
- `StrangeLight`
- `StrangeSound`
- `PictureChanged`
- `MultiplyingObject`
- `Creature`
- `Other`

Each anomaly point should have:

- anomaly id
- anomaly name
- one or more assigned anomaly types
- anomaly phase
- optional normal prefab
- optional anomaly prefab
- optional moved-object offset
- optional changed-object scale/color override
- optional normal/anomaly spatial audio

Anomaly phase rule:

- Anomaly points have `anomalyPhase` from 1 to 3.
- An anomaly can spawn only when `anomalyPhase <= currentProgressionPhase`.
- This prevents anomalies from appearing in locked areas.

Current spawn behavior:

- `GameRoundManager` collects eligible `AnomalySpawnPoint` objects.
- It resets points to normal before rolling.
- It can spawn a random number of anomalies based on phase settings.
- It supports empty-room rounds through `emptyRoomChance`.
- Spawned anomaly state is synchronized through Photon room custom properties.

## Checklist System

Current script: `ChecklistManager`

The checklist is shared across the room.

- Checklist items are built from the `AnomalyType` enum, excluding `None`.
- Selections are stored as a synced bit mask.
- Players may submit an empty checklist.
- Empty checklist is correct when no anomaly type exists in the current round.
- The game evaluates one shared result, not separate individual answers.
- All players must submit before the round resolves.
- If the investigation timer expires, the game resolves automatically.

Current result values:

- `Playing`
- `Win`
- `Lose`

In the active gameplay loop, checklist `Win` means round answer is correct, and checklist `Lose` means round answer is wrong.

## Multiplayer Direction

The current main multiplayer direction is Photon PUN / Photon Cloud.

Photon currently covers:

- connecting to Photon
- joining the shared lobby
- creating rooms
- listing rooms
- password-protected rooms
- max 4 players per room
- local player names
- ready/start flow
- synchronized room properties
- synchronized checklist selections
- synchronized round/phase state
- scene-local player spawning
- local and remote player movement state

Important architecture note:

- Photon is the main shipping path for the current anomaly gameplay.
- Unity Netcode scripts and train experiments still exist in the project, but they should be treated as prototype/secondary systems unless the team deliberately revives them.

## Lobby Flow

Current lobby flow is handled mainly by `LobbyTestPhotonController`.

Supported behavior:

- Create room
- Set lobby name
- Set leader/player name
- Optional password
- Join listed room
- Password prompt for protected rooms
- Ready/start button
- Master Client starts the gameplay scene when all members are ready
- Room UI supports 4 player slots

Known password-flow direction:

- Public room list should only show whether a password is required/open.
- Password handling has been simplified to avoid exposing password text in the wrong UI places.

## Player Gameplay

Current player features:

- Scene-local spawned player avatars.
- Player display names.
- Camera-facing name labels for remote players.
- Local player camera creation.
- Local and remote movement state sync.
- Ground-based movement only.
- Walk/sprint style movement support through current movement values.
- Current sprint input is `LeftShift`.
- Gravity/fall support.
- Jump input and jump animation are currently removed from the main player control direction.
- Spawn-pad teleport reset support for phase resets.

Current player movement should be treated as grounded exploration, not platforming.

## UI And UX

Confirmed UI flow:

- Main Menu
- Lobby
- Gameplay HUD
- Checklist window
- Memorize advance button
- Game status text
- In-game victory panel

Current UI helpers:

- `GameStatusUI`
- `ChecklistUI`
- `ChecklistWindowController`
- `ChecklistNetworkWindowController`
- `ChecklistWindowToggle`
- `ChecklistwindowVisibility`
- `MemorizePhaseAdvanceUI`
- `GameEndPanelUI`
- `GameplayPauseMenu`

Current end-game direction:

- The game does not need a separate ending scene for the main win flow.
- `GameEndPanelUI` shows when `GameRoundManager` reaches `Victory`.
- The panel has:
  - `Back To Menu`
  - `Back To Lobby`
- Returning to menu/lobby leaves the Photon room first when needed.

## Audio Direction

Audio should support both atmosphere and gameplay readability.

Existing/recommended categories:

- Base night ambience
- Phase ambience layers
- Memorize start cue
- Spawn lockdown cue
- Investigation start cue
- Phase unlock cue
- Checklist open/close/toggle/submit sounds
- Waiting-for-teammates cue
- Correct/wrong/victory sounds
- Door open/close sounds
- Anomaly-specific sounds

Current implementation hooks:

- `SoundManager`
- `SoundCategoryEmitter`
- `SoundCategory`
- `PhaseAccessManager` phase-change audio
- `AnomalySpawnPoint` normal/anomaly spatial audio
- `FootstepReceiver`
- `SoundManager` now owns BGM playback and crossfade transitions

Smallest high-impact audio pass:

1. Base ambience
2. Spawn Room door open/close
3. Memorize -> lockdown -> investigation cues
4. Checklist submit / correct / wrong
5. Phase unlock cue
6. Anomaly-specific sounds

## Current Implementation Notes

Recently implemented or confirmed:

- 7-round progression in `GameRoundManager`.
- Phase progression by round number.
- Wrong answer returns to the start round of the current phase.
- Spawn Lockdown phase between Memorize and Investigation.
- Score removed from active progression rules.
- Phase-filtered anomaly spawning.
- Empty-room anomaly rolls.
- Shared checklist resolution.
- Local spawn-pad teleport hooks.
- Door helpers for phase and spawn access.
- In-game victory panel.
- Manual memorize advance button.
- Photon player prefab fallback improvements.
- Player name labels face the camera correctly.
- Jump removed from current player movement direction.
- `SoundManager` merged the old `LoopBgmSfx` role and now handles multi-track BGM with crossfade.
- `PhaseAccessManager` now asks `SoundManager` to switch music by progression phase (`phase1/phase2/phase3`).
- Legacy `LoopBgmSfx` object in `Manager.prefab` is disabled to avoid duplicate BGM playback.

## Current Strengths

- Core anomaly loop is clear.
- Round and phase progression now has a stable structure.
- Map expansion creates rising difficulty.
- Shared checklist gives the game a strong co-op identity.
- Photon lobby and room flow supports the intended 1-4 player structure.
- The game can support no-anomaly rounds through empty checklist logic.

## Current Risks

- The project still contains both Photon and Unity Netcode code paths.
- Scene references for phase doors and Spawn Room door must be validated in the real gameplay scene.
- Some runtime references use fallback behavior and should be wired explicitly in prefabs/scenes.
- Prototype and imported asset content still makes the project structure noisy.
- Player prefab loading should be validated in build, not only in editor.
- UI should be tested with 4 players and different screen sizes.

## Recommended MVP

MVP goal:

Ship one clean playable 2-4 player anomaly loop.

MVP content:

- One main menu
- One lobby flow
- One playable map
- 7-round progression
- 3 progression phases
- Phase-gated map access
- Phase-gated anomaly pool
- Spawn Room lockdown flow
- Shared checklist submission
- Empty checklist support
- In-game victory panel

MVP exclusions:

- No score-based meta progression
- No Firebase leaderboard
- No train as a core requirement
- No second networking architecture in the shipping path

## Next Development Priorities

1. Validate phase doors in the actual gameplay scene.
2. Validate Spawn Room door open/close timing.
3. Assign all anomaly points to the correct anomaly phase.
4. Verify all 7 rounds in multiplayer.
5. Verify wrong-answer phase reset in multiplayer.
6. Verify empty-room rounds and empty checklist correctness.
7. Validate player prefab spawning in a build.
8. Polish Checklist UI readability and feedback.
9. Polish GameEndPanel UI and navigation.
10. Add or wire key audio cues for the core loop.

## Deferred Ideas

Firebase leaderboard:

- Previously considered.
- Not part of the current MVP.
- Keep as a future idea only.

Train-based anomaly game:

- Previously explored.
- Not aligned with the current main direction.
- Treat as archived/prototype content unless the team explicitly revives it.

## Update Rule

Update this file whenever any of these change:

- core gameplay loop
- round count
- phase progression
- map access rules
- anomaly categories or spawn rules
- checklist evaluation rules
- win/fail rules
- checkpoint/reset behavior
- multiplayer architecture
- MVP scope
- major UI flow
- major audio direction
