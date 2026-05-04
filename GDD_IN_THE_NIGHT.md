# IN THE NIGHT - Game Design Document (Working Draft)

## 1. Project Overview

**Project Name:** IN THE NIGHT  
**Engine:** Unity (URP)  
**Current Build Identity in Project Settings:** `NetcodeDemo329D`  
**Current State:** Prototype / vertical slice under active assembly from multiple scenes and subsystems

`IN THE NIGHT` appears to be a **multiplayer anomaly-observation horror game** where players enter a shared space at night, memorize the environment, investigate abnormalities, and submit a cooperative checklist before the round resolves.

From the current project state, the strongest playable identity is:

- Multiplayer session with up to 4 players
- Lobby creation / joining
- Round-based anomaly gameplay
- Shared checklist submission
- Horror / uncanny-night presentation
- Experimental player exploration and rail-train traversal

## 2. High Concept

Players enter a nighttime location and must **remember what is normal**, then identify what has changed once anomalies begin to appear. The game rewards observation, communication, and team coordination. The emotional target is tension from uncertainty, not direct combat.

## 3. Core Fantasy

- "We are trapped in a strange place at night."
- "Something in the scene is wrong."
- "We must compare what we remember with what we see now."
- "We survive by noticing, discussing, and choosing correctly together."

## 4. Genre and Pillars

**Genre**

- Co-op horror
- Observation / anomaly detection
- Round-based social deduction-lite

**Core pillars**

- Observation: players study environment details
- Memory: players must remember normal state
- Communication: team agrees on anomaly categories
- Pressure: wrong answers reset progress
- Atmosphere: darkness, unease, uncanny changes

## 5. Target Player Count and Platform

**Target player count**

- 1 to 4 players
- Best experience: 2 to 4 players

**Likely target platform**

- PC first

## 6. Current Gameplay Loop Found in Project

Based on the existing `Kan/Anomaly` gameplay scripts, the implemented loop is roughly:

1. Players join a lobby.
2. Host starts the match when players are ready.
3. Round begins in **Memorize Phase**.
4. Scene is shown in its normal state for a limited time.
5. Game enters **Investigation Phase**.
6. Anomalies are rolled/spawned.
7. Players inspect the scene and use a checklist UI.
8. All players submit.
9. Game compares submitted checklist against actual anomaly types.
10. Correct submission increases score.
11. Wrong submission resets score and round state.
12. Reaching target score leads to victory.

## 7. Current Round Structure

The current `GameRoundManager` supports these phases:

- `Memorize`
- `Investigation`
- `RoundTransition`
- `Victory`

Current round rules found in code:

- Memorize duration: `8` seconds
- Score needed to win: `3`
- Wrong answer resets score to `0`
- Correct answer advances toward victory
- All players must submit before resolution

## 8. Current Anomaly System Found in Project

The current anomaly framework is more advanced than a simple random swap. Each anomaly point can:

- stay normal
- hide an object
- move an object
- spawn an extra object
- replace/change an object
- represent strange light / sound / creature types

**Current anomaly categories in code**

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

Each anomaly point can:

- store ID and name
- sync state through Photon room properties
- show normal state from scene object or prefab
- preview anomaly in editor

## 9. Current Multiplayer Structure

The project currently contains **two multiplayer directions**:

**Photon / PUN path**

- Lobby creation and room list
- Password-protected rooms
- Ready state
- Scene sync
- Anomaly checklist gameplay
- Photon-based player spawning in scene

**Unity Netcode path**

- Networked player movement
- Networked character animation
- Train enter / exit seats
- Train driving on rail path

This strongly suggests the project is being built from multiple prototypes that have not been unified yet.

## 10. Current Player Features Found

### Photon gameplay branch

- Scene-local spawned player avatars
- Display names per player
- Shared room state
- Shared checklist resolution

### Netcode gameplay branch

- First/third-person style movement
- Mouse look
- Jump / sprint
- Character animation sync
- Footstep sound playback
- Interact with rail train
- Seat assignment and driver role

## 11. Current Scene Structure

The project contains many asset/demo scenes, but the most meaningful gameplay path appears to be:

- `Assets/Scenes/Kan/Menu.unity`
- `Assets/Scenes/Kan/Lobby.unity` or `Lobby Test.unity`
- `Assets/Scenes/Kan/Anomaly Test System Belike.unity`
- `Assets/Scenes/Kan/Map.unity`

Other folders such as `bass`, `Pond`, `Phukao`, `Nemo & Kratae`, and imported packs appear to contain:

- UI prototypes
- environment experiments
- test levels
- marketplace asset demo scenes
- rail / player test scenes

## 12. Best Reading of the Intended Game

The clearest current design direction is:

> A 4-player co-op horror anomaly game where players enter a nighttime environment, memorize normal objects, investigate changes, and submit a shared anomaly checklist to clear rounds.

The train system may be one of two things:

- a traversal feature inside the main game
- a separate prototype that may later become part of the main loop

Because the current anomaly loop is more complete than the train loop from a game-rules perspective, anomaly detection should be treated as the **primary game identity** for now.

## 13. Recommended Final Design Direction

To make the project feel unified, the recommended direction is:

**Primary fantasy**

- Players are investigators trapped in a night world where reality shifts subtly.

**Primary loop**

- Memorize
- Explore
- Detect
- Discuss
- Submit
- Survive consecutive rounds

**Secondary feature**

- Vehicle/train traversal between anomaly zones, if the team wants a larger map structure

## 14. Proposed Full GDD Direction

### 14.1 Game Premise

In a strange night-bound location, reality distorts in small but disturbing ways. Players must work together to detect anomalies before making a final submission. Wrong calls reset momentum. Perfect observation leads to escape.

### 14.2 Player Goal

- Identify which anomaly categories are present in the current round
- Submit the correct checklist as a team
- Clear enough rounds in a row to win

### 14.3 Failure State

- Wrong checklist submission resets progression
- Optional future expansion: limited lives, sanity, timer failure, creature threat

### 14.4 Win State

- Reach target score / cleared rounds
- Transition to victory or escape scene

## 15. Core Mechanics

### 15.1 Memorization

- Players are shown the scene in normal condition
- Short window to inspect layout, props, lights, and visual cues

### 15.2 Investigation

- Anomalies are introduced
- Players move around scene to verify abnormalities
- Team discusses what category exists

### 15.3 Checklist Submission

- Checklist contains anomaly categories
- Players mark categories they believe are present
- Match resolves only when all active players submit

### 15.4 Round Progression

- Correct answer: gain score
- Wrong answer: reset score and return to memorize flow

### 15.5 Multiplayer Coordination

- Host controls round authority
- All clients share round state
- Players can enter with names and ready up in lobby

## 16. Proposed Expanded Mechanics

These are not fully implemented, but fit the project well:

- Voice-chat-oriented clue sharing
- Personal notes / photo capture
- Limited flashlight battery
- Sanity or fear meter
- Fake anomalies meant to bait over-reporting
- Role perks such as Observer / Driver / Archivist / Scout
- Multiple maps with unique anomaly pools

## 17. Map and World Structure

### Recommended structure

- Small-to-medium handcrafted maps
- Dense prop placement
- Reusable anomaly points
- Strong landmarking for memorization

### Suggested map themes

- Night train station
- Empty shopping mall
- Alley district
- Apartment corridor
- Cafe interior after hours

The current project already contains many environment assets that could support this approach.

## 18. Train System in the GDD

The train system should be framed as one of these options:

**Option A: Keep as a core traversal mechanic**

- Players board a train to move between anomaly zones
- One player drives, others observe
- Train ride itself becomes a tension phase

**Option B: Keep as a separate prototype**

- Main game focuses only on room/scene anomaly detection
- Train system is not part of MVP

For a clean MVP, **Option B is safer** unless the team specifically wants the train fantasy as a headline feature.

## 19. Art Direction

Current asset mix suggests stylized-to-semi-stylized 3D environments. To unify the project, recommended art direction is:

- Nighttime lighting
- Warm practical lights against dark spaces
- Slightly uncanny realism
- Clean silhouettes for props players must memorize
- Readable environmental storytelling

## 20. Audio Direction

Audio should do heavy emotional work:

- ambient night tone
- distant mechanical hum
- subtle environmental loops
- anomaly-specific sound cues
- UI confirmation and error sounds
- stronger tension during investigation phase

## 21. UI / UX Direction

Current UI systems indicate:

- main menu
- lobby list
- room setup
- ready/start flow
- checklist window
- game status text

Recommended UX principles:

- fast room creation
- clear ready states
- simple readable checklist
- obvious phase transitions
- strong feedback for submit / waiting / result

## 22. Technical Summary

### Technologies currently present

- Unity URP
- Photon PUN
- Unity Netcode for GameObjects
- Cinemachine
- Input System
- Starter Assets

### Major technical issue

The project currently mixes **Photon** and **Unity Netcode** in the same game space. This is the single biggest architecture risk.

### Recommendation

Choose one networking stack for the main product:

- If anomaly/lobby flow is the main game: prefer **Photon path**
- If train/movement prototype is the future core and dedicated authority matters more: prefer **Unity Netcode path**

For the project as it stands, **Photon is closer to the actual game loop**.

## 23. Current Strengths

- Strong core concept is already visible
- Lobby and round flow exist
- Shared checklist system exists
- Anomaly taxonomy already designed
- Victory progression exists
- Many environment assets are available

## 24. Current Weaknesses

- Multiplayer architecture is split across two systems
- Scene structure is messy and prototype-heavy
- Project identity is partially hidden by imported demo content
- Build settings do not yet reflect one clean shipping flow
- Train prototype and anomaly prototype are not fully unified
- Naming and folder organization are inconsistent

## 25. Recommended MVP

### MVP goal

Ship one clean playable loop for 2 to 4 players.

### MVP content

- 1 main menu
- 1 lobby flow
- 1 playable map
- 5 to 8 anomaly points
- 5 anomaly categories
- 3 successful rounds to win
- simple victory screen

### MVP exclusions

- no train unless essential
- no multiple networking solutions
- no extra prototype scenes in shipping path
- no unnecessary imported demo logic

## 26. Production Recommendations

### Short-term

- choose one networking stack
- define one canonical scene flow
- lock one map as the vertical slice
- move active gameplay scripts into one clean folder

### Mid-term

- unify player controller
- unify checklist, round, and UI states
- add game feel polish
- add anomaly content variety

### Long-term

- more maps
- progression/meta layer
- more advanced anomalies
- stronger horror pacing systems

## 27. Suggested Canonical Scene Flow

Recommended shipping flow:

1. `Main Menu`
2. `Lobby`
3. `Map / Match`
4. `Victory / End Screen`

## 28. Final Product Statement

`IN THE NIGHT` should be positioned as a **co-op nighttime anomaly horror game** focused on observation, memory, and communication. The anomaly checklist loop is already the clearest and most complete identity in the codebase, so the project should center around that system first and treat other prototypes, especially the train system, as optional expansion unless the team explicitly decides otherwise.

## 29. Notes About This Document

This GDD is based on the current contents of the repository as of **May 1, 2026** and reflects:

- implemented systems found in code
- scene structure found in the project
- likely intended design direction inferred from those systems

Some parts are confirmed by code, while some are editorial recommendations to turn the current prototype into a coherent game.
