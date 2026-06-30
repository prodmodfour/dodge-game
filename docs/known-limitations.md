# Known limitations

This document separates the current Reaction Tactics prototype boundary from future product work. The prototype is complete when it demonstrates the deterministic action/reaction loop described in `docs/prototype-design.md` and can be validated with `docs/playtest-checklist.md`; the limitations below are intentional scope boundaries unless they cause those checks to fail.

## Prototype scope boundary

- The current goal is a readable, playable tactics prototype, not a production-ready game.
- Combat is intentionally deterministic: attacks connect or are avoided because of final grid positions and explicit reactions, not dodge chance, accuracy rolls, or hidden evasion.
- The default scenario is built to exercise melee timing, cones, AoEs, reaction movement, Brace, and AI handoff in a compact encounter.
- Systems favor inspectable runtime logic and editor tooling over final presentation polish.

## Current rough edges

### Visual presentation

- Units, tiles, markers, and effects use primitive shapes, simple materials, and placeholder styling.
- Combat feedback relies on highlights, nameplates, and log text rather than finished animation, VFX, or audio.
- Melee and attack presentations are readable placeholders, not final character animation.
- Camera, lighting, tile spacing, and markers are tuned for prototype readability on the default map only.

### UI and input

- The HUD, action menu, reaction menu, combat log, help overlay, and debug hover information are prototype UI surfaces.
- UI layout is optimized for fast iteration in the Unity editor and Linux prototype build, not for multiple aspect ratios, localization, gamepad navigation, touch input, or accessibility review.
- Some in-game debugging information remains visible by design because it helps validate grid positions, AP costs, occupancy, and action outcomes during playtests; noisy console click/action/AI debug logs are opt-in and disabled by default.

### Scenario content and balance

- The default skirmish is a small hand-authored encounter, not a campaign or content pipeline.
- Unit archetypes are limited to the current prototype classes and enemies.
- AP, HP, movement costs, and damage values are tuned to demonstrate reaction choices, not long-term competitive balance.
- Ability loadouts are minimal and focused on Move, Melee Slash, Cone Shot, Fireball, Brace, and Pass Reaction.

### AI behavior

- Enemy AI is deterministic and heuristic-driven.
- AI chooses nearby targets, uses available attacks when valuable, moves toward enemies, and reacts by moving, bracing, or passing.
- AI does not plan multiple turns ahead, bluff, bait reactions, preserve complex AP budgets, coordinate team tactics deeply, or expose difficulty settings.
- AI behavior exists to keep solo playtests moving and to exercise the reaction system symmetrically.

### Grid, movement, and targeting math

- Movement uses 4-way horizontal neighbors over stepped terrain with simple climb/drop limits and deterministic movement costs.
- Cone targeting uses cardinal directions and fixed widening bands; it is designed to be readable on the prototype grid rather than physically realistic.
- Radius AoE uses horizontal Manhattan distance over grid cells.
- Line of sight uses a simple projected grid-line check with `blocksLineOfSight` cells. Terrain height differences do not create a full 3D visibility model by themselves.
- Cover, partial cover, soft blockers, destructible terrain, flanking, facing bonuses, and elevation advantage are not implemented.

### Combat and reaction rules

- Reactions are limited to movement, Brace, and Pass Reaction.
- Reactions do not open full nested reaction windows.
- The prototype does not include opportunity attacks, interrupts, counterspells, counterattacks, overwatch, held actions, or reaction attacks.
- Push, pull, knockback, forced movement, zones of control, status effects, buffs, debuffs, summons, and environmental hazards are future mechanics.
- Damage resolution is deterministic and intentionally excludes hit chance, dodge chance, critical chance, and random damage variance.

### Build and production readiness

- The Linux standalone build path is validated for prototype review; other platforms are not a current target.
- There is no save/load, settings menu, key rebinding UI, localization, analytics, telemetry, networking, multiplayer, or persistence layer.
- Performance notes cover an initial editor play-mode profile only; production profiling and optimization are future work.
- Unity CI can be added later when an appropriate runner is available; current validation is local/tool-driven.
- Assets are prototype-safe placeholders and should be replaced before any public art or audio milestone.

## Intended future expansions

Potential follow-up work should be planned as new tickets rather than folded into the prototype completion milestone:

- Opportunity attacks, overwatch, interrupts, counterspells, and other special reactions.
- Push/pull abilities, knockback, repositioning attacks, traps, and area denial.
- Cover rules, richer elevation effects, improved line of sight, and more nuanced blocker behavior.
- Larger authored maps, procedural maps, additional scenarios, and campaign structure.
- More unit classes, asymmetric loadouts, status effects, equipment, and progression.
- Stronger enemy planning, team coordination, difficulty settings, and AI personality profiles.
- Better animations, VFX, SFX, music, camera polish, and final UI/UX treatment.
- Wider platform support, input remapping, accessibility options, save/load, and production build packaging.

## Handoff note

Future work should preserve the prototype's core identity unless a new design decision explicitly changes it: tactical avoidance comes from visible grid movement and explicit defensive choices, not random avoidance rolls.
