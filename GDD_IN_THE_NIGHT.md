# IN THE NIGHT - Game Design Document

## 1. Project Overview

**Project Name:** IN THE NIGHT  
**Engine:** Unity (URP)  
**Current Build Identity in Project Settings:** `NetcodeDemo329D`  
**Current State:** Prototype / vertical slice under active development

`IN THE NIGHT` is a **multiplayer anomaly-observation horror game** where players enter a shared night location, memorize what is normal, investigate abnormalities, and submit a cooperative checklist before the round resolves.

The clearest current identity is:

- up to 4 players in one online room
- lobby creation / join flow
- round-based anomaly gameplay
- shared checklist submission
- phase-based map expansion
- horror / uncanny-night atmosphere

## 2. High Concept

Players enter a nighttime market-like environment and must **remember what is normal**, then identify what has changed once anomalies appear. The game is about observation, memory, communication, and tension from uncertainty rather than combat.

## 3. Core Fantasy

- "We are trapped in a strange place at night."
- "Something in the scene is wrong."
- "We must compare what we remember with what we see now."
- "We clear the game by noticing, discussing, and choosing correctly together."

## 4. Genre and Pillars

**Genre**

- Co-op horror
- Observation / anomaly detection
- Round-based multiplayer investigation

**Core pillars**

- Observation: players study environment details
- Memory: players must remember the normal state
- Communication: team agrees on anomaly categories
- Pressure: wrong answers push the team back to the start of the current phase
- Atmosphere: darkness, unease, and subtle reality shifts

## 5. Target Player Count and Platform

**Target player count**

- 1 to 4 players
- Best experience: 2 to 4 players

**Target platform**

- PC first

## 6. Core Game Loop

1. Load the gameplay scene.
2. Players spawn inside the Spawn Room.
3. Start the current round and progression phase.
4. Open available area doors for the active phase.
5. Open the Spawn Room door and let players memorize the normal map state.
6. Return players to the Spawn Room.
7. Lock the Spawn Room door.
8. Roll anomalies for the round.
9. Filter anomalies by the current progression phase.
10. Re-open the Spawn Room door.
11. Players explore the currently unlocked area.
12. Players open the checklist and choose anomaly categories.
13. All players submit.
14. The game evaluates one shared checklist result.
15. If correct, the game advances to the next round.
16. If wrong, the game returns to the start of the current phase.
17. Repeat until all 7 rounds are cleared.

## 7. Round and Progression Structure

The round system is now based on **7 total rounds**.

### Phase progression

- `Rounds 1-3` = `Phase 1`
- `Rounds 4-5` = `Phase 2`
- `Rounds 6-7` = `Phase 3`

### Map access progression

- `Phase 1` = center zone only
- `Phase 2` = center + left zone
- `Phase 3` = center + left + right zone

Unlocked space is **cumulative**. Older areas remain open when a new phase begins.

## 8. Win / Fail Structure

The core evaluation rule is now:

- `Correct`
- `Incorrect`

Score is **not part of the main progression loop anymore**.

### Correct result

- advance to the next round
- open more space if the next round begins a new phase

### Incorrect result

- do not reset the full game
- send players back to the **start of the current phase**
- use the Spawn Room checkpoint for that phase

## 9. Spawn Room Rules

The Spawn Room is now a gameplay control space, not just a spawn location.

### Spawn Room door behavior

- during memorize: Spawn Room door opens
- during anomaly roll / lockdown: Spawn Room door closes
- during investigation: Spawn Room door re-opens

This helps:

- control pacing
- stop players from leaving before anomalies are ready
- make phase transitions readable

## 10. Memorize and Investigation Timing

### Memorize phase

- no required time limit
- intended flow supports manual advance

### Investigation phase default tuning

- `Phase 1` = `120 seconds`
- `Phase 2` = `180 seconds`
- `Phase 3` = `240 seconds`

These values should stay editable from managers / inspector for gameplay testing.

## 11. Checklist Rules

### Shared resolution

- all players must submit
- the game evaluates a **single shared checklist result**

### Empty checklist rule

- players may submit with the checklist left empty
- this supports rounds where no anomaly is present

## 12. Anomaly System

The anomaly system is a core pillar of the project.

### Current anomaly categories

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

### Required anomaly data

Each anomaly point should have:

- `anomaly id`
- `anomaly type`
- `anomaly phase`
- `zone / room reference`

### Phase filtering rule

Anomalies are not chosen only by ID.  
They must also respect `anomaly phase`.

This prevents anomalies from activating inside areas that are still locked behind closed doors.

## 13. Multiplayer Structure

The current main room / anomaly gameplay flow uses **Photon PUN / Photon Cloud**.

### Photon path currently covers

- room creation
- room list
- password-protected rooms
- ready state
- shared checklist gameplay
- scene-local player spawning

### Important note

The project still contains a separate **Unity Netcode** branch for movement / train experiments, but that is not the main game direction right now.

For the current game identity, **Photon is the primary networking path**.

## 14. Player Features

### Current player presentation / gameplay features

- scene-local spawned player avatars
- player display names
- local / remote movement state sync
- camera-facing player name labels
- spawn-pad teleport reset support for phase restart flow

## 15. Map Structure

The currently agreed playable structure is based on a central market-like map with progressive opening.

### Layout reading

- Spawn Room at the top
- center market zone in the middle
- left-side shops
- right-side shops

### Progression meaning

- `Phase 1` teaches the core loop in the center
- `Phase 2` increases search space by opening the left side
- `Phase 3` increases search space again by opening the right side

## 16. UI / UX Structure

### Confirmed UI flow

- Main Menu
- Lobby
- Gameplay HUD / Checklist
- In-game end panel on victory

### Current end-game implementation direction

The project no longer depends on a separate ending scene for the main win flow.

Instead:

- `Canvas.prefab` contains a real editable `GameEndPanel`
- the panel shows when the game reaches `Victory`
- buttons:
  - `Back To Menu`
  - `Back To Lobby`

`Back To Lobby` must return to the lobby scene.

## 17. Current Technical Implementation Notes

### Implemented direction in code

- `GameRoundManager` now uses:
  - `Memorize`
  - `SpawnLockdown`
  - `Investigation`
  - `RoundTransition`
  - `Victory`
- phase progression is based on round number
- wrong answer returns to the start round of the current phase
- score is no longer used for progression
- anomaly phase filtering exists in `AnomalySpawnPoint`

### Door helpers added

- `PhaseDoorController`
- `PhaseAccessManager`

### UI helpers added

- `MemorizePhaseAdvanceUI`
- `GameEndPanelUI`

## 18. Bug / Risk Notes

### Bug status currently tracked

- room password flow was simplified to avoid exposing actual password text
- lobby player slots were resized for 4-player layout
- player name labels were fixed to face the camera correctly
- player prefab fallback behavior was improved so missing-prefab cases do not show a bright magenta placeholder

### Main technical risk

The project still contains both:

- Photon PUN
- Unity Netcode for GameObjects

This remains the biggest architecture risk if the project grows without a stricter boundary.

## 19. Current Strengths

- the core anomaly loop is clear
- phase-based map expansion now gives structure to progression
- shared checklist rules are defined
- round flow is more coherent than before
- in-game end panel and manual memorize advance support the intended loop

## 20. Current Weaknesses

- scene wiring for doors still needs validation
- some runtime references still rely on fallback behavior
- project structure still includes prototype-heavy content
- not all legacy systems are fully removed or isolated

## 21. Recommended MVP

### MVP goal

Ship one clean playable loop for 2 to 4 players.

### MVP content

- 1 main menu
- 1 lobby flow
- 1 playable map
- 7-round progression structure
- 3 progression phases
- phase-gated anomaly pool
- Spawn Room lockdown flow
- shared checklist submission
- in-game victory panel

### MVP exclusions

- no score-based meta progression
- no Firebase leaderboard
- no train as a core requirement
- no second networking architecture in the shipping path

## 22. Recommended Development Priorities

1. Validate phase doors in the real target scene.
2. Validate Spawn Room lockdown and re-open timing.
3. Ensure anomaly points are assigned the correct `anomalyPhase`.
4. Verify all 7 rounds and phase resets behave correctly in multiplayer.
5. Make sure player prefab resolution is robust in build, not only in editor.
6. Polish the editable `GameEndPanel` inside `Canvas.prefab`.

## 23. Final Product Statement

`IN THE NIGHT` is a **co-op nighttime anomaly horror game** focused on observation, memory, communication, and progressive map expansion across 7 rounds. The anomaly checklist loop is the main product identity. Older train-based ideas remain archived and should not drive current scope unless the team deliberately revives them later.

## 24. Notes About This Document

This GDD reflects the current agreed design and implementation direction as of **May 4, 2026**.

It is based on:

- current gameplay agreements
- current memory file
- current code implementation direction
- current prefab / UI structure
