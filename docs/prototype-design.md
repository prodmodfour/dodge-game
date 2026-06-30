# Prototype Combat Design

This document records the intended rules for the Reaction Tactics prototype. It is a design target for implementation tickets; individual systems may be introduced incrementally, but they should remain aligned with these mechanics.

## Core intent

The prototype is about deterministic tactical timing. Attacks are avoided by spending action points (AP) to physically move on the grid or by using explicit defensive reactions, not by hidden dodge chance, accuracy rolls, or random hit rolls.

## Grid and units

- Units occupy discrete `x, y, z` grid positions on visible stepped terrain.
- A unit's current grid position at declaration and resolution matters for targeting, reaction order, and whether an attack connects.
- Terrain, blockers, occupancy, and movement costs should be queried through shared runtime services so combat outcomes are testable without scene-specific logic.

## AP lifecycle

- Each living unit has one shared AP pool.
- AP is refreshed at the start of each round, not at the start of each individual unit turn.
- Active-turn actions and off-turn reactions spend from the same AP pool.
- Spending AP on an active turn can reduce that unit's ability to react later in the round.
- Dead units do not need AP refreshes and should be ignored by turn and reaction systems.

## Prototype balance snapshot

Current default unit stats are tuned so active actions leave meaningful reaction choices without letting a unit do everything in one round:

| Unit | Max HP | Max AP | Notes |
| --- | ---: | ---: | --- |
| Knight | 18 | 7 | Durable melee anchor; a melee action leaves 4 AP for later movement or Brace. |
| Rogue | 14 | 8 | Mobile skirmisher with enough AP for a short active move, melee, and a modest reaction. |
| Archer | 14 | 8 | Cone user; Cone Shot leaves 4 AP before any active movement. |
| Mage | 12 | 8 | Fragile AoE user; Fireball leaves 3 AP, so active movement competes with reaction safety. |
| Goblin | 12 | 7 | Low-durability melee enemy that can still keep AP for one reaction choice. |
| Shaman | 13 | 8 | Enemy AoE caster with the same heavy-action tradeoff as the Mage. |

Default action and reaction values are:

| Ability or command | AP cost | Damage | Balance intent |
| --- | ---: | ---: | --- |
| Movement | path cost | 0 | Each entered cell costs the destination terrain movement cost, usually 1 AP and occasionally 2 AP on rough cells. Active and reaction movement use the same shared AP pool. |
| Melee Slash | 3 | 3 | Efficient single-target pressure that still requires the target to remain in melee range after reactions. |
| Cone Shot | 4 | 3 | Wider threat that leaves enough AP for a later short reaction if the user did not also move far. |
| Fireball | 5 | 4 | Highest default damage and area threat; the cost intentionally leaves only limited AP for reaction movement. |
| Brace | 2 | reduces next hit by 2 | Defensive fallback when movement cannot escape the pending action. |
| Pass Reaction | 0 | 0 | Conserves AP and advances the reaction window. |

These values mean the minimum default HP is 12 while the highest single hit is 4 damage, so no default unit is defeated by one unavoidable attack and most units survive at least two hits. Spending AP on active movement or heavy attacks reduces how far that unit can reaction-move later in the round.

## Actions vs. reactions

### Targeting confirmation

- Active movement executes immediately after selecting Move: hovering highlighted cells previews the path and AP cost, then clicking a reachable destination spends AP and moves the active unit.
- Targeted attacks and reaction movement use a two-step confirmation flow: the first click marks the unit or cell as the selected target, and the command is declared only after the player clicks the same target again or presses the on-screen Confirm button.
- `Escape` cancels the selected action or reaction mode, clears the pending target, and removes transient targeting highlights so the player can recover from a wrong click.
- Programmatic command tests and UI buttons may call the router's explicit confirm method directly when they already own a deliberate target selection.

### Actions

- Actions are commands a unit can use only on its own active turn.
- Only the current active unit may declare an active action.
- Legal actions spend AP when declared.
- Active movement is an own-turn command that spends AP per path cost, updates the unit's grid position, and returns to the active turn.
- Simple active movement does not open a reaction window in this prototype.
- A declared action records an intent: actor, origin position, target data, affected cells when applicable, and declaration sequence.
- Actions that trigger reactions resolve only after their reaction window closes.

### Reactions

- Reactions are commands a unit can use only during another unit's action window.
- The acting unit does not react to its own action.
- Only the current reactor in the reaction order may take a reaction command.
- During a reaction turn, input selection and the current-reactor highlight should point at that unit; selecting another unit must not let it react out of order.
- Reaction movement, Brace, and Pass are the prototype's expected baseline reaction choices.
- Reaction movement spends AP, updates the unit's grid position, and can change whether the pending action affects that unit.
- Brace costs 2 AP by default, prepares a one-shot fixed 2 damage reduction against the next positive incoming damage, and then advances the reaction window.
- A brace is consumed only by positive incoming damage that still hits the unit; moving out of melee range, cone cells, or AoE cells does not consume the brace during resolution.
- In a normal reaction window, an unused brace expires when the pending action finishes resolving.
- Pass costs 0 AP and advances the reaction window.

## Reaction window ordering

- Every action that triggers reactions opens a reaction window before resolution.
- Every other living unit receives one reaction turn unless eligibility rules auto-pass them.
- Reactors auto-pass when they are defeated, disabled by a reaction rule, have no AP, or have no legal reaction command other than Pass; debug action-flow logs include the reason.
- Reactor order is based on distance from the acting unit's grid position at action declaration time.
- Implementations should use one canonical tactical distance helper for this ordering.
- Ties must be deterministic, for example by stable unit ID or active-turn order.
- The original action resolves only after all eligible reactors have completed or passed.

## No nested full reaction windows

Reactions do not open full nested reaction windows in the prototype. A reaction command should not recursively cause every other unit to receive another reaction turn. Future abilities may explicitly opt into special interrupt behavior, but that must be designed and implemented as a separate rule rather than as the default reaction behavior.

## Solo AI shell

When an `AiController` is present on the combat systems object, the combat manager delegates Enemy-team active turns and Enemy-team reaction turns to it. The deterministic AI selects the nearest hostile target, declares melee, cone, or AoE actions through the normal action declaration flow when valuable, otherwise moves toward the nearest hostile while preserving a small reaction AP reserve. During reaction turns, AI units move to safe cells when possible, brace when threatened and unable to escape, or pass. If an enemy AI action opens a player reaction window, control returns to the AI after the player reaction completes just long enough to finish/end that enemy turn, then hands back to the next player-team turn or combat-over state. Automatic AI decisions use a short configurable play-mode pacing delay so enemy choices are readable; tests and scripted validation can disable or skip that delay.

## Option A melee timing

Melee uses declaration-in-range, resolution-after-reactions timing:

1. The active unit declares a melee action against a hostile target that is currently in melee range.
2. The melee action spends AP and creates a pending action intent.
3. A reaction window opens for every other living unit in distance order.
4. The target and other reactors may move, brace, or pass if legal.
5. After the reaction window closes, melee range is checked again using final positions.
6. If the target is alive and still in melee range, the attack hits and applies deterministic damage.
7. If the target is no longer in melee range, the attack is avoided by movement. This is not a dodge roll or miss chance.

## Line of sight

- Prototype line of sight uses a simple projected grid line across horizontal `x/z` coordinates between origin and target.
- The sampled line resolves each horizontal coordinate to the map cell's actual terrain height, but height differences do not bend or block sight by themselves.
- Intermediate cells marked `blocksLineOfSight` block ranged targeting through them.
- Origin and target cells do not block their own line; only missing cells or sight blockers between them stop the query.
- Cone and AoE declarations require line of sight from the acting unit to the chosen target cell before AP is spent, unless a future special ability explicitly ignores line of sight.
- The recorded AoE or cone footprint is not retroactively changed by line-of-sight checks after reactions; final positions determine who is hit.

## Ranged cone timing

- Cone attacks are declared by choosing a target cell or direction from the actor's origin position.
- Declaration records the cone's affected cells according to deterministic grid shape rules.
- Prototype cones use a cardinal direction from the actor's origin; width is 0 at distance 1, 1 at distances 2-3, and 2 at distance 4 or farther, clipped to existing terrain cells.
- Reactions happen before damage resolution.
- At resolution, hostile units are affected only if their final grid positions are still inside the declared cone shape.
- Prototype cone friendly fire is disabled: friendly units inside the cone are logged as ignored rather than damaged.
- Moving out of the cone during the reaction window is the way to avoid cone damage.

## AoE timing

- AoE attacks are declared by choosing a target cell within range.
- Declaration records the affected area according to deterministic radius or area rules.
- Reactions happen before damage resolution.
- At resolution, units are affected only if their final grid positions are still inside the declared area.
- Moving out of the area during the reaction window is the way to avoid AoE damage.

## Combat end state

- Combat ends when either the Player team or Enemy team has no living units remaining after both teams are present in the battle.
- The combat manager enters `CombatOver`, clears active/reaction control, closes any pending reaction window, and disables further action or reaction commands.
- The prototype UI shows Victory when the Player team wins and Defeat when the Enemy team wins, with a simple restart-scene control for playtest resets.

## Implementation guidance

- Keep combat rules deterministic and covered by EditMode tests where possible.
- Keep Unity scene and prefab wiring thin; core rules should live in runtime classes.
- Combat logs and UI should describe positional outcomes plainly, such as "Rogue avoided Melee Slash by moving out of range."
- Do not add random dodge, accuracy, hidden evasion, or probabilistic hit logic to prototype attack resolution.
