# Echoes of Celestia — Development Kanban

> Purpose: keep the GitHub repository aligned with the actual development status of *Echoes of Celestia*.  
> Scope: current milestone focuses on the **Space Civilisation Period** vertical slice.

## Board Structure

Recommended GitHub Project columns:

1. **Backlog** — ideas and future work not yet scheduled.
2. **Ready** — clearly scoped tasks ready to start.
3. **In Progress** — currently being implemented.
4. **Review / Test** — implemented, needs playtesting or polish.
5. **Done** — completed and verified in the current vertical slice.

Recommended labels:

- `feature`
- `bug`
- `design`
- `narrative`
- `ui/ux`
- `combat`
- `vehicle`
- `hub`
- `2d-game`
- `tooling`
- `documentation`
- `polish`
- `priority: high`
- `priority: medium`
- `priority: low`

---

## Milestone: Space Civilisation Vertical Slice

Goal: deliver one polished playable chapter showing the core game structure:

```text
3D Hub Exploration → Lore / Vehicle / Enemy Interaction → Portal → 2D Mini-game
```

### Definition of Done

- Player can explore the Space Hub on foot.
- Player can enter and exit vehicles.
- Player can read main-story and side-story lore from world objects.
- Main-story task UI can guide the player to the next unread lore object.
- Enemy drones can attack the player.
- Player death and respawn flow works.
- The portal to the 2D aerial shooter is functional.
- Scene persistence / restoration tools do not overwrite progress.

---

# Kanban

## Done

### 1. Space Hub Scene Reconstruction

**Status:** Done  
**Labels:** `feature`, `hub`, `tooling`

The Space Hub has been rebuilt as the 3D entry scene for the Space Civilisation Period.

Completed work:

- Replaced the old placeholder white plane scene with a moon-surface sci-fi environment.
- Used `Assets/_Creepy_Cat/_3D Scifi Kit Vol 3/Scenes/Example_Alone_On_Moon.unity` as a model reference/base.
- Preserved the Player model and the space station portal from the previous Hub.
- Kept the Creepy Cat asset pack unmodified.
- Added `SpaceHubSceneGuardian` to protect `Assets/Scenes/Space/Hub.unity` from accidental reverts.

Acceptance criteria:

- Hub opens in Unity with moon terrain and sci-fi props visible in Scene view.
- Hub does not revert to the old placeholder scene after reopening Unity.
- Player and portal remain present after scene restoration.

---

### 2. Third-Person Player Controller

**Status:** Done  
**Labels:** `feature`, `hub`, `player`

Completed work:

- Implemented `HubSimpleThirdPerson`.
- WASD movement relative to camera direction.
- Mouse orbit camera.
- Jumping.
- Sprinting with stamina logic running silently in the background.
- Removed visible stamina bar due to unsatisfactory UX.

Acceptance criteria:

- Player can move, jump, and sprint.
- Sprint is faster than normal movement.
- Stamina still limits sprinting even though no stamina UI is displayed.

---

### 3. Vehicle Interaction System

**Status:** Done  
**Labels:** `feature`, `vehicle`, `hub`

Completed work:

- Implemented `SpaceVehicleSeat`.
- Press `F` to enter a vehicle.
- Press `V` to exit a vehicle.
- Player model hides while inside a vehicle and reappears on exit.
- Camera switches to third-person vehicle follow view.
- Ground vehicle supports WASD driving.
- Aircraft supports WASD flying and Space-to-ascend.
- Aircraft descends naturally after player exits mid-air.
- Vehicle route guidance now starts from the active vehicle position when driving.

Playable vehicles currently used:

- `P_Rover_Mark_01`
- `P_Drop_Ship_Mark_01`
- `P_Spaceship_Constellation_Mark_1_Fake`

Acceptance criteria:

- Enter / exit works reliably.
- Player is not duplicated after exit.
- Vehicles do not visibly sink into the terrain.
- Aircraft lands after being abandoned in the air.

---

### 4. Enemy Drone Combat

**Status:** Done  
**Labels:** `feature`, `combat`, `hub`

Completed work:

- Implemented `PrimaryEnemy`.
- Added enemy health.
- Added red instant-raycast laser attack.
- Enemy automatically aims at the Player.
- If Player is driving a vehicle, enemy targets the vehicle instead.
- Vehicle armour blocks player damage while inside.
- Enemy drones use existing Creepy Cat drone models.

Current enemies:

- `P_Oblivion_Drone_01`
- `P_Oblivion_Drone_01 (1)`

Acceptance criteria:

- Enemy detects Player within range.
- Enemy fires visible red laser.
- Player takes damage when on foot.
- Player does not take damage while inside a vehicle.

---

### 5. Player Health, Death, and Respawn

**Status:** Done  
**Labels:** `feature`, `combat`, `player`, `ui/ux`

Completed work:

- Implemented `HubCombatTarget`.
- Player has health.
- Player model turns greyscale when defeated.
- 10-second respawn countdown appears in English.
- Player respawns at initial spawn position.
- Health resets on respawn.

Acceptance criteria:

- Player is defeated after repeated enemy hits.
- Countdown displays correctly.
- Player regains control after respawn.

---

### 6. Lore Interaction System

**Status:** Done  
**Labels:** `feature`, `narrative`, `ui/ux`, `hub`

Completed work:

- Implemented `LoreInteractable`.
- Implemented `LoreReadingUI`.
- Press `F` near a lore object to open a reading window.
- Window supports previous / next page navigation.
- Window supports close button and Escape key.
- Lore is split into main-story and side-story entries.
- English prompts are shown during Play.

Main-story chapters:

1. `The Meridian Age`
2. `The Signal from Aetherion`
3. `The Great Fracture`
4. `The Last Silence`

Side stories:

- `The Last Cartographer`
- `Letters to Lia`
- `The Elysium Files`
- `The Ember Compact`

Acceptance criteria:

- Player sees an English interaction prompt near lore objects.
- Pressing `F` opens the correct lore entry.
- Player can page through text and close the window.

---

### 7. Main Story Quest UI and Route Guidance

**Status:** Done  
**Labels:** `feature`, `narrative`, `ui/ux`, `hub`

Completed work:

- Implemented `MainStoryProgress`.
- Implemented `MainStoryQuestUI`.
- Implemented `MainStoryPathGuide`.
- Added top-left gold `◆ Main Story [J]` quest icon.
- Added progress overview window.
- Added node-based chapter progress display.
- Added `Start` and `Quit` story mode buttons.
- Added gold ground-dot route guidance toward the next unread chapter.
- Main-story progress resets every Play session.
- Route starts from Player position on foot and vehicle position while driving.

Acceptance criteria:

- Pressing `J` opens the main-story overview.
- Holding `Alt` allows clicking the `Main Story` icon with the mouse.
- `Start` enables golden route guidance.
- Reading a chapter updates progress and retargets the next unread chapter.
- `Quit` disables route guidance.

---

### 8. 2D Aerial Shooter Integration

**Status:** Done  
**Labels:** `feature`, `2d-game`, `ui/ux`

Completed work:

- Connected Hub portal to `Level1`.
- Implemented enhancement bootstrap for the 2D aerial shooter.
- Added camera follow, starfield, score popups, difficulty scaling, pause menu, and hit feedback without modifying the original 2D scene scripts directly.

Acceptance criteria:

- Player can enter the aerial shooter from the Hub portal.
- 2D game enhancements are injected only in the relevant scene.
- Cursor behaviour is appropriate for the 2D scene.

---

### 9. Game Concept and Design Documentation

**Status:** Done  
**Labels:** `documentation`, `design`

Completed work:

- Wrote `Game Concept and Design` document.
- Clarified scope, tools, assets, legal considerations, accessibility, and development plan.
- Clarified that 3D Scifi Kit Vol 3 provides model assets only; scene layout, lighting, and scripts are original work.
- Clarified that Player character and space station were generated via Hi3D.

Acceptance criteria:

- Document explains the idea, scope, planning, tools, assets, and risks.
- Document is suitable for module assessment submission.

---

### 21. Pixel Depths Dungeon Mini-Game

**Status:** Done  
**Labels:** `feature`, `combat`, `hub`

Completed work:

- Embedded a self-contained pixel-art dungeon roguelike (**Pixel Depths**) reachable from the Hub as a main-story trial.
- Runtime portal injection on an existing Hub station object (no manual `Hub.unity` edits).
- Procedural dungeon generator, melee / ranged combat, enemies, loot, traps, and meta-progress (best floor / gems).
- Player and monster visuals built from the PixelFantasy 4-directional character system.
- Bootstrap saves/restores `Physics2D.gravity`, target frame rate, and cursor state on entry/exit.
- Headless smoke test available via the PixelDungeon editor tools.

Acceptance criteria:

- Player can enter Pixel Depths from the Hub and return.
- Dungeon generates, combat works, and progress is recorded.
- Exiting restores the 3D Hub state cleanly.

---

### 22. Seven-Step Main Story Flow

**Status:** Done  
**Labels:** `feature`, `narrative`, `hub`

Completed work:

- Expanded the main story into a seven-step flow that interleaves gameplay trials with lore volumes:
  `Sky Assault trial → Volume I → Sentinels trial → Volume II → Pixel Depths trial → Volume III → Volume IV`.
- Story state (volume reads and trial flags) resets each Play session for consistent demos; mini-game records persist.
- Route guidance and quest UI updated to drive the player through the expanded sequence.

Acceptance criteria:

- Story advances only when the matching trial / lore step is completed.
- Quest UI reflects the current step.
- A demo always starts from a clean story state.

---

## Review / Test

### 10. Full Hub Playtest Pass

**Status:** Review / Test  
**Labels:** `testing`, `hub`, `priority: high`

Test the complete Hub loop:

- Spawn into Hub.
- Start main-story mode.
- Follow gold dots to each main-story object.
- Read all four chapters.
- Enter and exit vehicles.
- Trigger enemy combat.
- Die and respawn.
- Enter the 2D mini-game portal.

Acceptance criteria:

- No blocking bugs.
- No cursor-lock dead ends.
- No duplicate UI canvases.
- No invisible / floating guidance dots.
- No regressions in vehicle controls.

---

### 11. Scene Persistence Regression Test

**Status:** Review / Test  
**Labels:** `testing`, `tooling`, `priority: high`

Validate the scene guardian workflow after recent lore and quest-system additions.

Checklist:

- Reopen Unity.
- Confirm `Hub.unity` still contains moon terrain.
- Confirm lore objects still contain `LoreInteractable`.
- Confirm `Tools/Hub.unity.merged` is updated after Editor setup scripts run.
- Confirm Player, portal, vehicles, enemies, and quest UI still function.

Acceptance criteria:

- Hub scene does not revert.
- Lore and main-story bindings survive Editor restart.

---

## In Progress

### 12. Final Gameplay Polish

**Status:** In Progress  
**Labels:** `polish`, `ui/ux`, `priority: medium`

Current polish focus:

- Improve visual clarity of interaction prompts.
- Tune route guidance dot spacing and visibility.
- Tune enemy laser timing and damage if needed.
- Tune vehicle camera distance and ground alignment.

Acceptance criteria:

- Hub feels readable and playable without developer explanation.
- UI prompts do not overlap or fight for attention.
- Player can understand what to do next.

---

## Ready

### 13. Health UI Overlay

**Status:** Ready  
**Labels:** `feature`, `ui/ux`, `combat`

Add a minimal health display for the Player.

Suggested design:

- Small red health bar or numeric HP near bottom-left.
- Hidden while reading lore or inside main-story overview.
- Remains visible during combat.

Acceptance criteria:

- Player can understand how close they are to defeat.
- Does not clutter exploration UI.

---

### 14. Player Attack / Enemy Defeat Loop

**Status:** Ready  
**Labels:** `feature`, `combat`, `priority: medium`

Currently enemies can attack the Player, but Player does not yet have a dedicated attack mechanic.

Suggested implementation:

- Add simple ranged attack or interactable countermeasure.
- Allow damaging `PrimaryEnemy`.
- Add enemy destruction visual feedback.

Acceptance criteria:

- Player can defeat drones intentionally.
- Enemy death is visible and satisfying.
- Combat loop feels fair.

---

### 15. Narrative Placement Review

**Status:** Ready  
**Labels:** `narrative`, `hub`, `polish`

Review whether each lore object location matches the story content.

Checklist:

- Volume I should feel like an origin / historical archive.
- Volume II should feel like research / signal analysis.
- Volume III should feel linked to conflict / war.
- Volume IV should feel like a final dispatch / silence.
- Side stories should feel personal and hidden.

Acceptance criteria:

- Lore placement feels intentional.
- Player can infer the tone of the entry from the object and environment.

---

## Backlog

### 16. Space Civilisation Story Dialogue System

**Status:** Backlog  
**Labels:** `feature`, `narrative`

Future narrative module beyond readable records.

Ideas:

- Triggered dialogue after reading each main-story chapter.
- Companion / narrator voice text.
- Chapter completion popup.
- Optional lore recap in main-story overview.

Acceptance criteria:

- Narrative becomes more directed without removing exploration freedom.

---

### 17. Chapter 2: Medieval / Ancient Civilisation Period

**Status:** Backlog  
**Labels:** `design`, `feature`, `priority: low`

Build the next historical period after the Space Civilisation vertical slice is stable.

Possible direction:

- Different 3D hub aesthetic.
- Different 2D mini-game genre.
- New historical mystery that connects back to Celestia's collapse.

Acceptance criteria:

- Chapter 2 reuses the same overall structure but feels mechanically and visually distinct.

---

### 18. Accessibility Pass

**Status:** Backlog  
**Labels:** `accessibility`, `ui/ux`

Potential improvements:

- Remappable controls.
- Larger text option for lore windows.
- Colourblind-safe route guidance alternative.
- Subtitle / narration support if voiceover is added.

Acceptance criteria:

- Game remains playable for a wider range of players.

---

### 19. Audio Pass

**Status:** Backlog  
**Labels:** `audio`, `polish`

Potential work:

- Ambient moon-base hum.
- Vehicle engine loops.
- Drone alert sound.
- UI open / close sound.
- Lore discovery chime.

Acceptance criteria:

- Audio reinforces atmosphere without overwhelming the player.

---

## Suggested GitHub Issue Breakdown

If converted to GitHub Issues, create one issue per Kanban card above, with:

- Title matching the card title.
- Labels from the label list.
- Body containing the task description and acceptance criteria.
- Milestone: `Space Civilisation Vertical Slice`.

Suggested project views:

- **Board by Status**: Backlog / Ready / In Progress / Review / Done.
- **Table by Label**: grouped by `feature`, `bug`, `narrative`, `ui/ux`, etc.
- **Roadmap by Milestone**: Space Civilisation → Chapter 2 → Polish.

---

## Current Snapshot

| Area | State |
|------|-------|
| Space Hub | Playable |
| Player Controller | Playable |
| Vehicles | Playable |
| Enemy Combat | Playable |
| Player Death / Respawn | Playable |
| Lore Reading | Playable |
| Main Story Quest Guidance | Playable |
| 2D Aerial Shooter | Playable |
| Pixel Depths Dungeon | Playable |
| Seven-Step Main Story Flow | Playable |
| Health UI | Not started |
| Player Attack | Not started |
| Additional Historical Periods | Not started |

