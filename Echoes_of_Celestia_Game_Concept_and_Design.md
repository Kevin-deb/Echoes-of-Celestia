# Game Concept and Design
## *Echoes of Celestia*

---

## 1. Game Concept Overview

**Echoes of Celestia** is a single-player narrative exploration game built in Unity (2022.3 LTS). The player enters an illusionary realm where the ruins of a lost civilisation, *Celestia*, lie frozen in time. Each historical period of Celestia is represented as a self-contained chapter, combining a 3D exploratory hub scene with a 2D mini-game that reflects the art style and mechanics of that era.

The central dramatic question is: *how did a great civilisation rise, and why did it secretly fall?* Progress through the game gradually reveals a suppressed history that no one was supposed to find.

The game is structured around a core loop:

```
Enter 3D Hub → Explore environment → Find the portal → Play 2D mini-game → Unlock next chapter
```

The first chapter, and the one developed for this module, is the **Space Civilisation Period** — a moon-surface sci-fi setting.

---

## 2. Player Experience Goals

| Goal | How it is delivered |
|------|---------------------|
| Curiosity and discovery | An atmospheric 3D hub with vehicles to drive, enemies to avoid, and a portal to find |
| Narrative tension | Locked doors that open only when earlier levels are completed; enemy drones that enforce a sense of danger |
| Mechanical variety | 3D third-person exploration switches to a 2D aerial shooter mini-game |
| Progression and reward | A persistent `GameSession` tracks gold and unlocked level count across scenes using `DontDestroyOnLoad` and `PlayerPrefs` |

---

## 3. Game Design Principles Applied

### 3.1 Clear Core Mechanic
The central mechanic of the hub is **contextual interaction**: the player presses **F** to interact with vehicles and portals, and **V** to exit vehicles. This single interaction key is used consistently so the player never needs to learn multiple control schemes. The door system extends this naturally — approaching a portal and pressing **F** transitions the player into the mini-game.

### 3.2 Risk and Reward
Two enemy drones (`P_Oblivion_Drone_01`) patrol the hub. They detect the player within 70 metres, aim automatically, and fire a red instant-raycast laser every 1.4 seconds. Each hit deals 12 damage against a player pool of 100 HP.

- **Risk**: If the player ignores the drones while exploring, they will be defeated.
- **Reward (mitigation)**: Entering a vehicle provides full damage immunity — the laser cannot penetrate vehicle armour. The player trades mobility precision for safety.
- **Consequence without punishment**: Death causes a 10-second greyscale respawn rather than a game-over screen, keeping the experience accessible while maintaining stakes.

### 3.3 Layered Controls
The control scheme expands progressively:

| Context | Controls |
|---------|----------|
| On foot | WASD movement, mouse camera, Space to jump |
| Ground vehicle (Lunar Rover) | WASD to drive; vehicle bounds-snapped to terrain surface |
| Aircraft (Constellation) | WASD to fly; hold Space to ascend, release to descend; gravity-assisted landing when exiting mid-air |

### 3.4 Spatial Navigation
This hub draws on sample scenes and real-world lunar landscapes to provide a lunar terrain featuring natural landmarks. The player spawns near the space station portal, encouraging purposeful movement rather than random wandering.

---

## 4. Creativity and Originality

The structural idea — using multiple historical periods as distinct chapters, each with its own 3D hub and corresponding 2D game — is the original design proposition. It allows:

- **Artistic coherence per chapter**: the Space Period hub is sci-fi; a future Medieval Period hub would have a different aesthetic and mini-game genre entirely.
- **Narrative layering**: the 3D environment provides atmosphere and lore hints; the 2D game provides action and challenge.
- **Controlled scope**: each period can be developed, tested, and shipped independently.

The vehicle interaction system (`SpaceVehicleSeat.cs`) was written from scratch and is not based on any tutorial. It uses renderer-bounds-based proximity detection (rather than simple transform distance), which correctly handles large asymmetric vehicles regardless of pivot placement. Renderer snapshots record each material's enabled state on entry and restore exactly that state on exit, preventing conflicts with the runtime GLB model loader (`HubKleeModelRuntimeFit.cs`).

The aerial vehicle physics include a soft fall model when exiting mid-air: the ship accumulates gravitational velocity up to a terminal speed cap, then settles on the terrain surface, which makes the world feel physically consistent even though no Rigidbody is used.

---

## 5. Scope

This module submission covers **Chapter 1: Space Civilisation Period** only. It is intentionally scoped to one polished, playable chapter rather than prototyping all periods.

### What is built and functional

| System | Status |
|--------|--------|
| 3D hub scene (moon terrain, props, lighting) | Complete |
| Third-person character controller with stamina | Complete |
| Vehicle interaction — ground rover (WASD, terrain snap) | Complete |
| Vehicle interaction — aircraft (WASD + Space lift, mid-air exit gravity) | Complete |
| Enemy drones — health, auto-aim, laser attack, death | Complete |
| Player health, greyscale death, 10-second respawn at spawn point | Complete |
| Portal door with level-lock system | Complete |
| 2D aerial shooter mini-game (Level1 scene) | Complete |
| Cross-scene session persistence (gold, unlock count) | Complete |
| Scene integrity guardian (auto-restore from known-good backup) | Complete |

### In progress

| System | Status |
|--------|--------|
| Narrative / story module for Space Civilisation Period | Not yet started — story content has not been written; narrative scripting and in-game dialogue system are planned for a later phase |

### Deliberately out of scope for this submission

- Additional civilisation periods (Medieval, Ancient)
- In-game UI health bar (health tracked internally; visual feedback via greyscale)
- Multiplayer
- Mobile platform

---

## 6. Tools, Assets, and Resources

### Engine and Language
- **Unity 2022.3.62f1 LTS** — stable long-term support release; chosen for editor stability and broad community documentation
- **C# / .NET Standard 2.1** — all gameplay scripts written from scratch

### Asset Pack
- **3D Scifi Kit Vol 3** by Creepy Cat (Unity Asset Store)
  - Used **exclusively as a source of 3D model assets**: sci-fi props, vehicle meshes (rover, drop-ship, constellation), and the drone enemy model. The pack also supplies laser visual effect prefabs and materials.
  - All **scene layout, lighting, and interactive behaviour are original work**: the hub scene was built by placing and positioning models manually, lighting was configured independently, and every interaction script (vehicle controls, enemy AI, door triggers, etc.) was written from scratch.
  - The asset pack is a commercial Unity Asset Store product. Its licence permits use in personal and commercial projects but does not permit redistribution of the raw assets. The pack files are kept in `Assets/_Creepy_Cat/` and are **never modified**.

### 3D Models Generated via Hi3D
- The **player character (Klee)** and the **space station model** (which serves as the visual entrance to the aerial shooter mini-game) were generated using the [Hi3D](https://hi3d.ai) AI 3D generation platform and exported as `.glb` files.
- The player character is loaded at runtime through a custom GLB parser (`RuntimeGlbMeshBuilder.cs`, `HubKleeModelRuntimeFit.cs`) that reads mesh data, reconstructs UV-mapped geometry, and applies a runtime material — without relying on any third-party importer package.
- These models are used for educational and demonstration purposes. For a commercial release they would be replaced with original or properly licensed assets.

### Unity Packages Used

| Package | Purpose |
|---------|---------|
| `com.unity.feature.2d` | 2D physics and sprite rendering for the aerial shooter mini-game |
| `com.unity.inputsystem` | Input handling |
| `com.unity.textmeshpro` | UI text rendering |
| `com.unity.ugui` | Canvas-based UI (prompts, respawn countdown, mini-game HUD) |
| `com.unity.modules.physics` | 3D raycasting (laser hit detection, camera collision, terrain snapping) |
| `com.unity.modules.terrain` | Moon terrain rendering |

---

## 7. Legal, Ethical, Social, and Accessibility Considerations

### Legal
- The 3D Scifi Kit Vol 3 is used under its standard Unity Asset Store licence, which permits use in projects and submissions. Only the 3D model assets are used from the pack; all scene composition, lighting, and scripts are original work. The pack is not redistributed.
- The player character and space station models were generated via Hi3D and are used for educational purposes only. Their presence is acknowledged as a placeholder pending original or properly licensed assets.
- All custom scripts are original work.

### Ethical
- The game involves combat (laser fire, enemy drones) but no gore, realistic violence, or distressing content. The aesthetic is stylised sci-fi.
- Death is represented as a greyscale colour filter and a brief countdown, not as graphic imagery.

### Social
- The game is single-player with no online features, avoiding data collection, social pressure mechanics, or monetisation systems.

### Accessibility
- Controls use standard WASD + mouse, which is the de facto PC gaming convention.
- On-screen contextual prompts tell the player which key to press at all times (e.g. "Press F to enter Lunar Rover", "WASD to drive, V to exit").
- The respawn system ensures that defeat is never permanent — the player always returns to the start after 10 seconds, keeping the experience low-frustration.
- Known limitation: no remappable controls, no colourblind mode, no font size adjustment. These would be addressed in a production iteration.

### Security
- No network connectivity, no user account system, no external data transmission. Save data uses Unity `PlayerPrefs` (local registry/file), storing only integers (gold, unlock count). No personal data is handled.

---

## 8. Development Plan

### Phase 1 — Foundation (Completed)
- Set up Unity project, configure Build Settings, establish scene structure (`Hub`, `Level1`, `TD_Level1`, `MainMenu`)
- Implement `GameSession` singleton for cross-scene persistence
- Build basic third-person character controller (`HubSimpleThirdPerson`)

### Phase 2 — Hub Scene Construction (Completed)
- Merge `Example_Alone_On_Moon.unity` (asset pack) with game-specific objects (player spawn, portal, UI) using a PowerShell YAML merge script
- Integrate existing portal and door system from earlier scene version
- Implement `SpaceHubSceneGuardian` Editor script to protect the merged scene file from accidental overwrite by Unity's asset pipeline

### Phase 3 — Vehicle Interaction (Completed)
- Design and implement `SpaceVehicleSeat` component: proximity detection via renderer bounds, F/V key entry/exit, player hide/show via renderer state snapshot, camera follow, ground snap and aircraft gravity
- Write `SpaceHubVehicleSetupEditor` to auto-install components on scene open
- Tune vehicle parameters (speed, camera distance, directional orientation correction)

### Phase 4 — Enemy System (Completed)
- Implement `PrimaryEnemy`: HP, auto-aim turret rotation, instant-raycast red laser (LineRenderer), cooldown, death
- Implement `HubCombatTarget`: player HP, vehicle armour immunity, greyscale death, 10-second respawn countdown UI
- Add `[RuntimeInitializeOnLoadMethod]` bootstrap to guarantee component presence regardless of Editor setup state

### Phase 5 — 2D Mini-game Integration (Completed)
- Connect hub portal to `Level1` (aerial shooter scene) via `HubDoorInteractable`
- Implement `PlaneGameBootstrap` injection system: enhances the 2D game without modifying its original scripts — adds camera follow, starfield, wave difficulty scaling, pause menu, score popups, and hit-flash feedback

### Phase 6 — Polish and Testing (Ongoing)
- Fix compiler warnings (unused fields, invalid type checks)
- Tune player model material brightness (emission value added to GLB material)
- Resolve scene-revert bugs via guardian script and stable merged backup
- Final playtesting for feel, difficulty balance, and respawn correctness

### Planned but not yet started
- **Narrative module for Space Civilisation Period** — story content, in-game dialogue, and lore delivery system; story writing is a prerequisite before implementation can begin
- Chapter 2 (Medieval Period) — new 3D hub and 2D mini-game
- Main menu polish and chapter select screen
- Ambient audio for hub scene
- In-game health bar UI overlay

---

## 9. Risk Assessment

| Risk | Likelihood | Impact | Mitigation |
|------|-----------|--------|------------|
| Asset pack render pipeline mismatch (Built-in vs URP) | Low — already resolved | Medium | Project uses Built-in RP throughout; asset pack was verified compatible |
| Scene file corruption or revert by Unity pipeline | Was encountered | High | `SpaceHubSceneGuardian` monitors scene on every Editor load and auto-restores from `Tools/Hub.unity.merged` |
| Component missing at runtime (Editor-only setup scripts) | Was encountered | High | All critical components now have `[RuntimeInitializeOnLoadMethod]` fallback installation |
| Scope creep across multiple periods | Medium | High | Development is gated by chapter: Chapter 2 is not started until Chapter 1 is shippable |
| GLB character model licence | Low for educational use | Medium (commercial) | Model is acknowledged as placeholder; would be replaced with original asset for release |

---

## 10. Summary

*Echoes of Celestia* is a focused, completable concept built around one strong structural idea: each historical period of a lost civilisation is its own mini-game, connected through an atmospheric 3D hub. The first chapter is fully playable, with a coherent control scheme, enemy combat, vehicle interaction, and a portal into a 2D shooter.

The scope is realistic — one polished chapter rather than a vague promise of many. The tools are appropriate to the task. The assets are legally sourced and unmodified. The known limitations (colourblind support, audio, additional chapters) are clearly identified rather than ignored.

The game demonstrates that a small, well-reasoned idea with a clear player loop and a realistic plan is more valuable than an ambitious design with no achievable path to delivery.
