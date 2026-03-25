# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

AMNS_Cycle is a Unity 6 (6000.0.66f2) multiplayer cycling game. Players ride physical cycles whose rotation data is sent via OSC protocol, and the game visualizes combined progress toward a target distance with gift milestone unlocks.

## Build & Run

- **Unity Version:** 6000.0.66f2 — open the project in Unity Hub
- **Render Pipeline:** Universal Render Pipeline (URP) with 2D renderer
- **No custom build scripts** — use Unity's standard Build Settings (File > Build Settings)
- **Testing:** Unity Test Framework is included (`com.unity.test-framework`) but no tests are written yet

## Architecture

Three singleton MonoBehaviours in `Assets/Scripts/` form the entire custom codebase:

### OSCManager
Handles network communication via the extOSC library (localhost, receive port 8000, send port 9000):
- `/start-game` — receives player names array, triggers countdown
- `/game-data` — receives rotation counts for 6 players each frame
- `/serial-value` — sends `s` (start) / `f` (finish) signals
- Debug: press **S** to simulate start, **F** to simulate finish

### GameManager
Main game controller and UI orchestrator:
- Manages round lifecycle: countdown → active round (default 30s) → end
- Tracks cumulative distance across rounds toward target (default 100km)
- Drives cyclist animation and road progression based on combined player distance
- Gift reveal system at distance milestones (shake, confetti, typing text effects)
- **Activity pacing system** — dynamically adjusts wheel circumference (0.5x–2.0x multiplier) based on elapsed time vs. expected progress across sessions

### CycleDataManager
Per-player metrics tracker:
- Calculates distance from rotation count × wheel circumference (2.1m)
- Maintains speed (smoothed, 0.5s time constant), top speed, and average speed per player

## Key Dependencies

- **extOSC** (v1.19.7) — OSC protocol implementation, lives in `Assets/extOSC/` (194 files, do not modify)
- **Lana Studio Hyper Casual FX** — particle effect prefabs in `Assets/Lana Studio/`
- **Salesforce Sans fonts** in `Assets/Fonts/SF fonts/`

## Version Control Notes

- `Assets/Graphics/` is gitignored — sprite assets are not tracked
- `.csproj` and `.sln` files are gitignored — Unity regenerates them
- Standard Unity ignores: `Library/`, `Temp/`, `Obj/`, `Build/`, `Logs/`, `UserSettings/`
