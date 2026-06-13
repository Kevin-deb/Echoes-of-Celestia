# Echoes of Celestia

**Echoes of Celestia** is a Unity-based 3D exploration game about uncovering the forbidden history of a lost civilisation called Celestia.

The current playable vertical slice focuses on the **Space Civilisation Period**. It includes a moon-surface 3D Hub, interactive vehicles, enemy drones, readable lore records, main-story route guidance, player health / respawn logic, and a connected 2D aerial shooter mini-game.

## Requirements

- Unity Hub
- The Unity Editor version recorded in `ProjectSettings/ProjectVersion.txt`

## Opening the Project

1. Clone this repository.
2. Open Unity Hub.
3. Choose **Add** and select the repository root folder.
4. Unity will generate local cache folders such as `Library/`, `Temp/`, and `Logs/`. These folders are intentionally ignored by Git and should not be committed.

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

If you clone this repository and find missing references in the Hub scene or future development content, import the corresponding asset packs locally using the same folder structure as the original working project.

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

See `KANBAN.md` and the GitHub Project board for detailed progress.

## Documentation

- `Echoes_of_Celestia_Game_Concept_and_Design.md` — game concept and design document
- `KANBAN.md` — local Markdown mirror of the GitHub Kanban board

## Notes

This project is currently developed as an educational game development module submission. Some models are placeholders and would need to be replaced or relicensed for a commercial release.