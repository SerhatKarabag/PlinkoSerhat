# Plinko

This repository contains a Unity-based Plinko Game that focuses on a production-style game loop: simulated server communication, reward verification with anti-cheat checks, session persistence, and performance-minded runtime systems. The goal is to feel like a client-server game without requiring a real backend.

## What’s Included

- **Full gameplay loop** driven by a state machine (`Initializing`, `Playing`, `LevelTransition`, `RunEnding`, `RunFinished`, `Paused`, `Error`).
- **Mock backend** that simulates latency, error rates, wallet sync, and reward validation.
- **Reward batching** with retries and optimistic UI updates.
- **Anti-cheat validation** that flags implausible outcomes and abnormal statistics.
- **Persistent player data** (wallet, run summary, reward history) using `PlayerPrefs`.
- **Performance safeguards** including pooling, FPS monitoring, and physics quality scaling.

## Gameplay Flow (High Level)

1. `GameBootstrapper` creates services (mock server, session manager, reward batch manager) and injects them into `GameManager`.
2. `GameManager` initializes the board and ball systems, then transitions through game states.
3. Dropped balls are tracked by `BallManager` and scored by `PlinkoBoard`, which emits reward events.
4. Rewards are queued by `RewardBatchManager` and validated by the mock server (including anti-cheat).
5. UI updates are driven by events, with wallet totals split into verified vs. pending rewards.

## Architecture Overview

### Core Orchestration
- **`GameManager`** is the central controller: it owns the state machine, starts sessions, updates systems, and wires UI/physics/services together.
- **`GameEventWiring`** subscribes to gameplay, session, and reward events to keep UI and data in sync.

### State Machine
`GameStateMachine` registers the main states and drives transitions. This keeps session flow, level transitions, pause/resume, and run ending logic isolated and testable.

### Services
- **`MockServerService`** simulates a backend with latency and errors, validates reward batches, updates wallet totals, and persists mock server state to `PlayerPrefs`.
- **`SessionManager`** owns session lifecycle and timer updates, and restores session state when possible.
- **`RewardBatchManager`** accumulates rewards, sends batches, retries failed batches, and reconciles optimistic vs. verified balances.

### Anti-Cheat
`AntiCheatValidator` analyzes reward entries for implausible trajectories, suspicious statistics, and rate limits. It can reject suspicious batches or flag them for review.

### Data & Persistence
- **`PlayerData`** stores player wallet, run summaries, and reward history, with throttled saves.
- **`GameConfig` / `UIConfig`** are ScriptableObjects that drive rules, physics tuning, batching thresholds, and UI update cadence.

### Physics & Performance
- **`PlinkoBoard`** generates the peg layout and buckets dynamically.
- **`BallManager`** spawns and recycles balls with pooling.
- **`FPSMonitor`** reduces physics quality when FPS drops and restores it on recovery.
- **`PhysicsOptimizer`** centralizes physics tuning (iterations, fixed delta time, materials).

## Running the Project

1. Open the project in Unity.
2. Open `Assets/Scenes/MainScene.unity`.
3. Press **Play**.

## Configuration

Main configuration assets are under `Assets/Resources/Config/`:

- **`GameConfig.asset`**: rules, levels, physics values, batching thresholds, anti-cheat thresholds, session length.
- **`UIConfig.asset`**: UI update interval, pooling sizes, and string builder settings.

## Folder Structure (Key Areas)

- `Assets/Scripts/Core` — game bootstrap, state machine, and event wiring.
- `Assets/Scripts/Services` — mock server, session manager, reward batching, anti-cheat.
- `Assets/Scripts/Physics` — board generation, ball behavior, buckets.
- `Assets/Scripts/UI` — UI overlays, history, reward popups.
- `Assets/Scripts/Data` — config ScriptableObjects and player data models.
- `Assets/Scripts/Utils` — FPS monitor, physics optimizer, task helpers.

## Notes

- `plinkoTest.apk` is included as a reference Android build artifact.
