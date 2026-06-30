# Prototype Playtest Checklist

Use this checklist to manually verify the Reaction Tactics prototype from a fresh run of `MainPrototype` or the latest Linux build. The goal is to evaluate whether the tactical action/reaction loop is understandable and feels deterministic.

## Session details

- Date/time:
- Tester:
- Build or commit:
- Platform:
- Input device:
- Scenario notes:

## Setup

- [ ] Start `MainPrototype` in Play Mode or launch the Linux build.
- [ ] Confirm the combat HUD, unit HP/AP plates, action menu, reaction menu, and combat log are visible when relevant.
- [ ] Confirm both teams have living units and combat starts at round 1 with one active unit highlighted.

## Core flow checks

| Check | Pass | Notes |
| --- | :---: | --- |
| Can select the active unit and choose **Move**. | [ ] | |
| Can move the active unit to a highlighted reachable cell. | [ ] | |
| Movement spends AP and updates the unit HP/AP plate. | [ ] | |
| Can end the active unit's turn and advance to the next unit. | [ ] | |
| Can spend some AP on an active turn while saving enough AP for later reactions. | [ ] | |
| Units with no useful reaction are auto-passed or can pass without stalling the window. | [ ] | |

## Melee timing checks

| Check | Pass | Notes |
| --- | :---: | --- |
| A melee attack can be declared only against a hostile unit in melee range. | [ ] | |
| Declaring melee opens a reaction window before damage resolves. | [ ] | |
| The melee target can reaction-move out of melee range and avoid the hit. | [ ] | |
| The combat log describes the avoid as movement out of range, not a dodge roll. | [ ] | |
| If the melee target stays in melee range, the attack hits and applies deterministic damage. | [ ] | |

## Cone attack checks

| Check | Pass | Notes |
| --- | :---: | --- |
| Selecting **Cone Shot** previews cone danger cells from the hovered target direction. | [ ] | |
| Declaring Cone Shot opens reactions before damage resolves. | [ ] | |
| A threatened target can reaction-move out of the cone. | [ ] | |
| Units outside the cone at resolution take no damage. | [ ] | |
| Units still inside the cone at resolution take deterministic damage. | [ ] | |

## AoE attack checks

| Check | Pass | Notes |
| --- | :---: | --- |
| Selecting **Fireball** previews radius danger cells around the hovered target cell. | [ ] | |
| Declaring Fireball opens reactions before damage resolves. | [ ] | |
| A threatened target can reaction-move out of the AoE. | [ ] | |
| Units outside the AoE at resolution take no damage. | [ ] | |
| Units still inside the AoE at resolution take deterministic damage. | [ ] | |

## Brace and reaction ordering checks

| Check | Pass | Notes |
| --- | :---: | --- |
| During a reaction turn, **Brace** spends AP and ends that unit's reaction. | [ ] | |
| Brace reduces incoming damage when the braced unit is still hit. | [ ] | |
| Brace is not needed or consumed when movement avoids the pending attack. | [ ] | |
| Reaction turns occur in distance order from the acting unit's declaration position. | [ ] | |
| Tied reaction distances resolve consistently across repeated attempts. | [ ] | |

## Determinism and combat end checks

| Check | Pass | Notes |
| --- | :---: | --- |
| No observed outcome uses dodge chance, accuracy chance, random hit rolls, or hidden evasion. | [ ] | |
| The combat log explains each hit, avoid, brace reduction, death, and round transition in plain language. | [ ] | |
| Enemy AI can complete active turns without manual console commands. | [ ] | |
| Enemy AI can react by moving, bracing, or passing. | [ ] | |
| Combat can end in Victory when enemies are defeated. | [ ] | |
| Combat can end in Defeat when player units are defeated. | [ ] | |
| After combat ends, further actions and reactions are disabled. | [ ] | |

## Observed bugs

| ID | Steps to reproduce | Expected | Actual | Severity | Notes |
| --- | --- | --- | --- | --- | --- |
| BUG-001 |  |  |  |  |  |
| BUG-002 |  |  |  |  |  |
| BUG-003 |  |  |  |  |  |

## Tuning notes

Use this section for game-feel observations, not implementation plans.

- AP economy:
- Movement range and costs:
- Melee clarity:
- Cone readability:
- AoE readability:
- Brace usefulness:
- Reaction pacing/order readability:
- AI behavior:
- UI/combat log clarity:
- Overall fun/confusion points:

## Final playtest verdict

- [ ] Ready for prototype review.
- [ ] Needs tuning before review.
- [ ] Blocked by bugs before review.

Summary:
