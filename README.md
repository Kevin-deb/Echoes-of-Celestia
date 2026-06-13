# Echoes of Celestia

**Echoes of Celestia** is a Unity-based 3D exploration game about uncovering the forbidden history of a lost civilisation called Celestia.

The current playable vertical slice focuses on the **Space Civilisation Period**. It includes a moon-surface 3D Hub, interactive vehicles, enemy drones, readable lore records, main-story route guidance, player health / respawn logic, a connected 2D aerial shooter mini-game, and an embedded **Pixel Depths** dungeon roguelike.

## How to Play (Recommended)

The easiest way to evaluate the game is to download the prebuilt Windows version — **no Unity install and no third-party asset packs required**:

1. Open the [**Releases**](https://github.com/Kevin-deb/Echoes-of-Celestia/releases) page.
2. Download the latest `EchoesOfCelestia-Windows.zip`.
3. Extract it and run `Echoes of Celestia.exe`.

> The repository source below is provided for **code review**. Opening/building it directly in Unity additionally requires the third-party asset packs described in *Third-Party Assets*.

## Requirements

- Unity Hub
- The Unity Editor version recorded in `ProjectSettings/ProjectVersion.txt`

## Opening the Project

1. Clone this repository.
2. Open Unity Hub.
3. Choose **Add** and select the repository root folder.
4. Unity will generate local cache folders such as `Library/`, `Temp/`, and `Logs/`. These folders are intentionally ignored by Git and should not be committed.

> **Important:** This repository excludes two paid Unity Asset Store packs (see *Third-Party Assets*). Without them the project **will not compile** — the Pixel Depths mini-game references `PixelFantasy` scripts directly — and the Hub scene will show missing objects. To simply play the game, download the prebuilt **Release** above instead of opening the project in Unity.

## Repository Contents

This repository contains the version-controlled Unity project files, including:

- `Assets/`
- `Packages/`
- `ProjectSettings/`
- `Tools/`
- Project documentation and planning files

Local generated folders such as `Library/`, `Temp/`, `Logs/`, and other machine-specific Unity cache files are excluded.

## Third-Party Assets

Some third-party asset packs are **not included** in this GitHub repository because of file size and licensing restrictions.

Not included:

- `Assets/_Creepy_Cat` — 3D Scifi Kit Vol 3
- `Assets/PixelFantasy`

**These packs are required to open and run the project in Unity:**

- The **Pixel Depths** dungeon mini-game has a direct *code* dependency on `PixelFantasy`. Without it the project fails to compile and Unity cannot enter Play Mode.
- The Space **Hub** scene is built from `_Creepy_Cat` prefabs. Without it the Hub shows missing / invisible objects.

If you own these packs, import them into `Assets/` using the same folder names (`Assets/_Creepy_Cat`, `Assets/PixelFantasy`) to restore full functionality. Otherwise, play the prebuilt **Release** build, which legally embeds the compiled assets and requires neither Unity nor the packs.

## Current Development Status

The current milestone is tracked in GitHub Projects:

- **Project:** Echoes of Celestia - Master Development Kanban
- **Milestone:** Space Civilisation Vertical Slice

Implemented systems include:

- Space Hub scene reconstruction
- Third-person Player controller
- Vehicle interaction system
- Enemy drone combat
- Player health, death, and respawn
- Lore interaction system
- Main Story quest UI and golden route guidance
- 2D aerial shooter integration
- Seven-step main-story flow (gameplay trials interleaved with lore volumes)
- Pixel Depths dungeon mini-game (embedded roguelike reachable from the Hub)

See `KANBAN.md` and the GitHub Project board for detailed progress.

## Documentation

- `Echoes_of_Celestia_Game_Concept_and_Design.md` — game concept and design document
- `KANBAN.md` — local Markdown mirror of the GitHub Kanban board

## Notes

This project is currently developed as an educational game development module submission. Some models are placeholders and would need to be replaced or relicensed for a commercial release.