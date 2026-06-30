using System;
using System.Collections.Generic;
using ReactionTactics.Actions;
using ReactionTactics.Core;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Pathfinding;
using ReactionTactics.Targeting;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.AI
{
    /// <summary>
    /// Value object describing the deterministic cone action an AI unit selected.
    /// </summary>
    public readonly struct AiConeAttackChoice
    {
        public AiConeAttackChoice(
            AbilityDefinition ability,
            GridPosition targetCell,
            CardinalDirection direction,
            int hostileThreatCount,
            int friendlyThreatCount,
            int nearestHostileDistance)
        {
            Ability = ability ?? throw new ArgumentNullException(nameof(ability));
            TargetCell = targetCell;
            Direction = direction;
            HostileThreatCount = hostileThreatCount >= 0
                ? hostileThreatCount
                : throw new ArgumentOutOfRangeException(nameof(hostileThreatCount), hostileThreatCount, "Hostile threat count cannot be negative.");
            FriendlyThreatCount = friendlyThreatCount >= 0
                ? friendlyThreatCount
                : throw new ArgumentOutOfRangeException(nameof(friendlyThreatCount), friendlyThreatCount, "Friendly threat count cannot be negative.");
            NearestHostileDistance = nearestHostileDistance >= 0
                ? nearestHostileDistance
                : throw new ArgumentOutOfRangeException(nameof(nearestHostileDistance), nearestHostileDistance, "Nearest hostile distance cannot be negative.");
        }

        public AbilityDefinition Ability { get; }

        public GridPosition TargetCell { get; }

        public CardinalDirection Direction { get; }

        public int HostileThreatCount { get; }

        public int FriendlyThreatCount { get; }

        public int NearestHostileDistance { get; }
    }

    /// <summary>
    /// Deterministic shell controller for prototype enemy units. It exposes stable
    /// target selection, declares adjacent melee attacks, declares valuable cone
    /// shots, advances active units toward targets when no attack currently
    /// validates, and still passes reaction turns until reaction-specific AI tickets are implemented.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AiController : MonoBehaviour
    {
        public const int DefaultTargetSelectionVerticalWeight = 1;
        public const int DefaultReactionApReserve = 2;
        public const string DefaultMeleeSlashAbilityKey = "melee_slash";
        public const string DefaultMeleeSlashDisplayName = "Melee Slash";
        public const string DefaultConeShotAbilityKey = "cone_shot";
        public const string DefaultConeShotDisplayName = "Cone Shot";

        [SerializeField]
        [Tooltip("Team controlled by this deterministic prototype AI controller.")]
        private TeamId controlledTeam = TeamId.Enemy;

        [SerializeField]
        [Tooltip("When enabled, CombatManager may delegate this team's active turns and reaction turns to the AI.")]
        private bool automaticDelegationEnabled = true;

        [SerializeField]
        [Tooltip("Optional CombatManager controlled by this AI. Defaults to the manager on the same GameObject.")]
        private CombatManager combatManager;

        [SerializeField]
        [Tooltip("Write concise debug logs for AI movement and pass decisions.")]
        private bool logDecisions = true;

        [SerializeField]
        [Min(0)]
        [Tooltip("Vertical grid weight used when choosing the nearest hostile target.")]
        private int targetSelectionVerticalWeight = DefaultTargetSelectionVerticalWeight;

        [SerializeField]
        [Min(0)]
        [Tooltip("AP the AI tries to keep after active movement so it can still react later in the round.")]
        private int reactionApReserve = DefaultReactionApReserve;

        public TeamId ControlledTeam
        {
            get { return controlledTeam; }
            set { controlledTeam = value; }
        }

        public bool AutomaticDelegationEnabled
        {
            get { return automaticDelegationEnabled; }
            set { automaticDelegationEnabled = value; }
        }

        public CombatManager CombatManager
        {
            get { return combatManager; }
            set { combatManager = value; }
        }

        public bool LogDecisions
        {
            get { return logDecisions; }
            set { logDecisions = value; }
        }

        public int TargetSelectionVerticalWeight
        {
            get { return targetSelectionVerticalWeight; }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        value,
                        "AI target selection vertical distance weight cannot be negative.");
                }

                targetSelectionVerticalWeight = value;
            }
        }

        public int ReactionApReserve
        {
            get { return reactionApReserve; }
            set
            {
                if (value < 0)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(value),
                        value,
                        "AI reaction AP reserve cannot be negative.");
                }

                reactionApReserve = value;
            }
        }

        public bool ControlsUnit(TacticalUnit unit)
        {
            return unit != null && unit.IsAlive && unit.Team == controlledTeam;
        }

        public bool ShouldHandleActiveTurn(CombatManager manager)
        {
            if (!automaticDelegationEnabled || manager == null || !isActiveAndEnabled)
            {
                return false;
            }

            var state = manager.CurrentState;
            return state != null
                && state.IsActiveUnitPhase
                && ControlsUnit(state.ActiveUnit);
        }

        public bool ShouldHandleReactionTurn(
            CombatManager manager,
            ActionIntent sourceIntent,
            TacticalUnit reactor)
        {
            if (!automaticDelegationEnabled || manager == null || sourceIntent == null || !isActiveAndEnabled)
            {
                return false;
            }

            var state = manager.CurrentState;
            return state != null
                && state.IsReactionPhase
                && ReferenceEquals(state.PendingActionIntent, sourceIntent)
                && ReferenceEquals(state.ReactingUnit, reactor)
                && ControlsUnit(reactor);
        }

        /// <summary>
        /// Selects the nearest living hostile target by tactical distance, then by
        /// lowest current HP and stable unit ID. Returns null when the acting unit is
        /// dead or no living hostile target is available.
        /// </summary>
        public TacticalUnit SelectNearestHostileTarget(
            TacticalUnit actor,
            IEnumerable<TacticalUnit> candidates)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            if (candidates == null)
            {
                throw new ArgumentNullException(nameof(candidates));
            }

            if (!actor.IsAlive)
            {
                return null;
            }

            TacticalUnit bestTarget = null;
            foreach (var candidate in candidates)
            {
                if (!IsSelectableHostileTarget(actor, candidate))
                {
                    continue;
                }

                if (bestTarget == null || CompareTargetCandidates(actor, candidate, bestTarget) < 0)
                {
                    bestTarget = candidate;
                }
            }

            return bestTarget;
        }

        public bool TrySelectNearestHostileTarget(
            TacticalUnit actor,
            IEnumerable<TacticalUnit> candidates,
            out TacticalUnit target)
        {
            target = SelectNearestHostileTarget(actor, candidates);
            return target != null;
        }

        public int GetTargetSelectionDistance(TacticalUnit actor, TacticalUnit target)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            return actor.CurrentGridPosition.TacticalDistanceTo(
                target.CurrentGridPosition,
                targetSelectionVerticalWeight);
        }

        public int GetConservativeMovementBudget(TacticalUnit actor)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            if (!actor.IsAlive)
            {
                return 0;
            }

            return Math.Max(0, actor.CurrentAP - reactionApReserve);
        }

        /// <summary>
        /// Chooses the reachable destination that gets the actor closest to the target
        /// while preserving the configured reaction AP reserve. The actor's current
        /// cell is never returned; failure means the AI should pass or wait for a
        /// future attack behavior instead of moving away from the target.
        /// </summary>
        public TacticalResult<GridPosition> TryChooseActiveMoveDestination(
            TacticalUnit actor,
            TacticalUnit target,
            IGridMap map,
            IGridOccupancy occupancy)
        {
            var movementContextResult = ValidateMovementChoiceContext(actor, target, map);
            if (movementContextResult.IsFailure)
            {
                return TacticalResult<GridPosition>.Failure(movementContextResult.ErrorMessage);
            }

            var movementBudget = GetConservativeMovementBudget(actor);
            if (movementBudget <= 0)
            {
                return TacticalResult<GridPosition>.Failure(
                    $"{DescribeUnit(actor)} has no conservative active-move budget after reserving {reactionApReserve} AP for reactions.");
            }

            IReadOnlyDictionary<GridPosition, ReachableCell> reachableCells;
            try
            {
                var search = new ReachableCellSearch();
                reachableCells = search.FindReachableCells(
                    map,
                    actor.CurrentGridPosition,
                    movementBudget,
                    occupancy);
            }
            catch (ArgumentException exception)
            {
                return TacticalResult<GridPosition>.Failure(
                    $"{DescribeUnit(actor)} cannot evaluate active movement: {exception.Message}");
            }
            catch (InvalidOperationException exception)
            {
                return TacticalResult<GridPosition>.Failure(
                    $"{DescribeUnit(actor)} cannot evaluate active movement: {exception.Message}");
            }

            var currentDistance = actor.CurrentGridPosition.TacticalDistanceTo(
                target.CurrentGridPosition,
                targetSelectionVerticalWeight);
            var hasBestCell = false;
            var bestCell = default(ReachableCell);
            var bestDistance = int.MaxValue;

            foreach (var reachableCell in reachableCells.Values)
            {
                if (reachableCell.Position == actor.CurrentGridPosition)
                {
                    continue;
                }

                var distance = reachableCell.Position.TacticalDistanceTo(
                    target.CurrentGridPosition,
                    targetSelectionVerticalWeight);
                if (distance >= currentDistance)
                {
                    continue;
                }

                if (!hasBestCell || CompareMoveDestination(reachableCell, distance, bestCell, bestDistance) < 0)
                {
                    hasBestCell = true;
                    bestCell = reachableCell;
                    bestDistance = distance;
                }
            }

            if (!hasBestCell)
            {
                return TacticalResult<GridPosition>.Failure(
                    $"No reachable cell within {movementBudget} AP moves {DescribeUnit(actor)} closer to {DescribeUnit(target)} while preserving {reactionApReserve} AP.");
            }

            return TacticalResult<GridPosition>.Success(bestCell.Position);
        }

        public bool HasValidActiveAttackAvailable(
            TacticalUnit actor,
            TacticalUnit target,
            IGridMap map,
            CombatState combatState)
        {
            if (actor == null)
            {
                throw new ArgumentNullException(nameof(actor));
            }

            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (combatState == null)
            {
                throw new ArgumentNullException(nameof(combatState));
            }

            if (!IsSelectableHostileTarget(actor, target))
            {
                return false;
            }

            var loadout = actor.GetComponent<UnitAbilityLoadout>();
            if (loadout == null)
            {
                return false;
            }

            var actionAbilities = loadout.GetActionAbilities();
            for (var i = 0; i < actionAbilities.Count; i += 1)
            {
                var ability = actionAbilities[i];
                if (!IsAttackAbility(ability) || !TryCreateAttackTarget(actor, target, ability, out var actionTarget))
                {
                    continue;
                }

                var context = new AbilityTargetValidationContext(
                    actor,
                    ability,
                    actionTarget,
                    AbilityUsage.Action,
                    combatState,
                    map);
                if (AbilityTargetValidator.Instance.ValidateTarget(context).IsSuccess)
                {
                    return true;
                }
            }

            return false;
        }

        public TacticalResult<TacticalUnit> TryChooseMeleeAttackTarget(
            TacticalUnit actor,
            IEnumerable<TacticalUnit> candidates,
            IGridMap map,
            CombatState combatState)
        {
            var contextResult = ValidateMeleeAttackChoiceContext(actor, candidates, map, combatState);
            if (contextResult.IsFailure)
            {
                return TacticalResult<TacticalUnit>.Failure(contextResult.ErrorMessage);
            }

            var abilityResult = TryResolveMeleeSlashAbility(actor);
            if (abilityResult.IsFailure)
            {
                return TacticalResult<TacticalUnit>.Failure(abilityResult.ErrorMessage);
            }

            var ability = abilityResult.Value;
            TacticalUnit bestTarget = null;
            foreach (var candidate in candidates)
            {
                if (!IsSelectableHostileTarget(actor, candidate))
                {
                    continue;
                }

                var actionTarget = ActionTarget.ForUnit(candidate);
                var validationContext = new AbilityTargetValidationContext(
                    actor,
                    ability,
                    actionTarget,
                    AbilityUsage.Action,
                    combatState,
                    map);
                if (AbilityTargetValidator.Instance.ValidateTarget(validationContext).IsFailure)
                {
                    continue;
                }

                if (bestTarget == null || CompareTargetCandidates(actor, candidate, bestTarget) < 0)
                {
                    bestTarget = candidate;
                }
            }

            if (bestTarget == null)
            {
                return TacticalResult<TacticalUnit>.Failure(
                    $"No valid in-range hostile target is available for {DescribeUnit(actor)} to use {ability.DisplayName}.");
            }

            return TacticalResult<TacticalUnit>.Success(bestTarget);
        }

        public TacticalResult<AiConeAttackChoice> TryChooseConeAttack(
            TacticalUnit actor,
            IEnumerable<TacticalUnit> candidates,
            IGridMap map,
            CombatState combatState)
        {
            var contextResult = ValidateConeAttackChoiceContext(actor, candidates, map, combatState);
            if (contextResult.IsFailure)
            {
                return TacticalResult<AiConeAttackChoice>.Failure(contextResult.ErrorMessage);
            }

            var abilityResult = TryResolveConeShotAbility(actor);
            if (abilityResult.IsFailure)
            {
                return TacticalResult<AiConeAttackChoice>.Failure(abilityResult.ErrorMessage);
            }

            var ability = abilityResult.Value;
            if (!actor.CanSpendAP(ability.APCost))
            {
                return TacticalResult<AiConeAttackChoice>.Failure(
                    $"{DescribeUnit(actor)} cannot choose {ability.DisplayName} because it needs {ability.APCost} AP but only has {actor.CurrentAP} AP.");
            }

            var candidateUnits = CollectLivingCandidateUnits(candidates);
            var hasBestChoice = false;
            var bestChoice = default(AiConeAttackChoice);

            for (var directionIndex = 0; directionIndex < 4; directionIndex += 1)
            {
                var direction = (CardinalDirection)directionIndex;
                if (!TryFindConeDeclarationCell(actor, ability, direction, map, combatState, out var targetCell))
                {
                    continue;
                }

                var affectedCells = new HashSet<GridPosition>(
                    AreaShapeService.GetConeCells(actor.CurrentGridPosition, direction, ability.Range, map));
                var hostileThreatCount = 0;
                var friendlyThreatCount = 0;
                var nearestHostileDistance = int.MaxValue;

                for (var i = 0; i < candidateUnits.Count; i += 1)
                {
                    var candidate = candidateUnits[i];
                    if (ReferenceEquals(candidate, actor)
                        || !affectedCells.Contains(candidate.CurrentGridPosition))
                    {
                        continue;
                    }

                    if (actor.Team.IsHostileTo(candidate.Team))
                    {
                        hostileThreatCount += 1;
                        nearestHostileDistance = Math.Min(
                            nearestHostileDistance,
                            GetTargetSelectionDistance(actor, candidate));
                    }
                    else if (actor.Team.IsFriendlyTo(candidate.Team))
                    {
                        friendlyThreatCount += 1;
                    }
                }

                if (hostileThreatCount <= 0 || friendlyThreatCount > hostileThreatCount)
                {
                    continue;
                }

                var choice = new AiConeAttackChoice(
                    ability,
                    targetCell,
                    direction,
                    hostileThreatCount,
                    friendlyThreatCount,
                    nearestHostileDistance);
                if (!hasBestChoice || CompareConeChoices(choice, bestChoice) < 0)
                {
                    hasBestChoice = true;
                    bestChoice = choice;
                }
            }

            if (!hasBestChoice)
            {
                return TacticalResult<AiConeAttackChoice>.Failure(
                    $"No valuable {ability.DisplayName} cone direction is available for {DescribeUnit(actor)}: every valid direction either threatens no hostiles or would threaten more friendlies than hostiles.");
            }

            return TacticalResult<AiConeAttackChoice>.Success(bestChoice);
        }

        /// <summary>
        /// Active-turn AI behavior: declare an adjacent melee attack when possible;
        /// otherwise declare a valuable cone shot when available; otherwise, if no
        /// attack currently validates, advance toward the nearest hostile with
        /// conservative movement, then end the active turn.
        /// </summary>
        public TacticalResult TakeActiveTurn()
        {
            return TakeActiveTurn(ResolveCombatManager());
        }

        /// <summary>
        /// Active-turn AI behavior: declare an adjacent melee attack when possible;
        /// otherwise declare a valuable cone shot when available; otherwise, if no
        /// attack currently validates, advance toward the nearest hostile with
        /// conservative movement, then end the active turn.
        /// </summary>
        public TacticalResult TakeActiveTurn(CombatManager manager)
        {
            var validation = ValidateActiveTurn(manager);
            if (validation.IsFailure)
            {
                return validation;
            }

            var activeUnit = manager.CurrentState.ActiveUnit;
            var meleeResult = TryDeclareMeleeAttackAtNearestTarget(manager, activeUnit);
            if (meleeResult.IsFailure)
            {
                return TacticalResult.Failure(meleeResult.ErrorMessage);
            }

            if (meleeResult.Value)
            {
                return CompleteActiveTurnAfterDeclaredAction(manager, activeUnit, "melee");
            }

            var coneResult = TryDeclareConeAttackAtBestDirection(manager, activeUnit);
            if (coneResult.IsFailure)
            {
                return TacticalResult.Failure(coneResult.ErrorMessage);
            }

            if (coneResult.Value)
            {
                return CompleteActiveTurnAfterDeclaredAction(manager, activeUnit, "cone");
            }

            var movementResult = TryMoveTowardNearestTarget(manager, activeUnit);
            if (movementResult.IsFailure)
            {
                return TacticalResult.Failure(movementResult.ErrorMessage);
            }

            if (movementResult.Value)
            {
                LogDecision($"AI controlling {activeUnit.DisplayName} {activeUnit.UnitId} [{activeUnit.Team}] ended its active turn after moving toward a target.");
            }
            else
            {
                LogDecision($"AI controlling {activeUnit.DisplayName} {activeUnit.UnitId} [{activeUnit.Team}] passed its active turn.");
            }

            return manager.EndActiveTurn();
        }

        /// <summary>
        /// Initial reaction AI behavior: pass the controlled unit's current reaction turn.
        /// </summary>
        public TacticalResult TakeReactionTurn(ActionIntent sourceIntent, TacticalUnit reactor)
        {
            return TakeReactionTurn(ResolveCombatManager(), sourceIntent, reactor);
        }

        /// <summary>
        /// Initial reaction AI behavior: pass the controlled unit's current reaction turn.
        /// </summary>
        public TacticalResult TakeReactionTurn(
            CombatManager manager,
            ActionIntent sourceIntent,
            TacticalUnit reactor)
        {
            var validation = ValidateReactionTurn(manager, sourceIntent, reactor);
            if (validation.IsFailure)
            {
                return validation;
            }

            LogDecision($"AI controlling {reactor.DisplayName} {reactor.UnitId} [{reactor.Team}] passed reaction to {sourceIntent.Ability.DisplayName}.");
            return manager.PassCurrentReaction(reactor);
        }

        private TacticalResult CompleteActiveTurnAfterDeclaredAction(
            CombatManager manager,
            TacticalUnit activeUnit,
            string actionLabel)
        {
            if (manager.CurrentState.IsCombatOver)
            {
                return TacticalResult.Success();
            }

            if (manager.CurrentState.IsActiveUnitPhase
                && ReferenceEquals(manager.CurrentState.ActiveUnit, activeUnit))
            {
                LogDecision($"AI controlling {activeUnit.DisplayName} {activeUnit.UnitId} [{activeUnit.Team}] ended its active turn after its {actionLabel} action resolved without waiting for player input.");
                return manager.EndActiveTurn();
            }

            LogDecision($"AI controlling {activeUnit.DisplayName} {activeUnit.UnitId} [{activeUnit.Team}] declared a {actionLabel} action and is waiting for reactions to finish.");
            return TacticalResult.Success();
        }

        private void Awake()
        {
            ResolveCombatManager();
        }

        private void Reset()
        {
            controlledTeam = TeamId.Enemy;
            automaticDelegationEnabled = true;
            logDecisions = true;
            targetSelectionVerticalWeight = DefaultTargetSelectionVerticalWeight;
            reactionApReserve = DefaultReactionApReserve;
            ResolveCombatManager();
        }

        private void OnValidate()
        {
            targetSelectionVerticalWeight = Math.Max(0, targetSelectionVerticalWeight);
            reactionApReserve = Math.Max(0, reactionApReserve);
        }

        private CombatManager ResolveCombatManager()
        {
            if (combatManager == null)
            {
                combatManager = GetComponent<CombatManager>();
            }

            return combatManager;
        }

        private TacticalResult<bool> TryMoveTowardNearestTarget(CombatManager manager, TacticalUnit activeUnit)
        {
            if (manager.UnitRegistry == null)
            {
                LogDecision($"AI could not evaluate active movement for {DescribeUnit(activeUnit)} because no unit registry is assigned.");
                return TacticalResult<bool>.Success(false);
            }

            var livingUnits = manager.UnitRegistry.GetLivingUnits();
            if (!TrySelectNearestHostileTarget(activeUnit, livingUnits, out var target))
            {
                LogDecision($"AI found no living hostile target for {DescribeUnit(activeUnit)}.");
                return TacticalResult<bool>.Success(false);
            }

            var mapResult = ResolveCurrentMap(manager);
            if (mapResult.IsFailure)
            {
                LogDecision($"AI could not evaluate active movement for {DescribeUnit(activeUnit)}: {mapResult.ErrorMessage}");
                return TacticalResult<bool>.Success(false);
            }

            if (HasValidActiveAttackAvailable(activeUnit, target, mapResult.Value, manager.CurrentState))
            {
                LogDecision($"AI kept {DescribeUnit(activeUnit)} at {activeUnit.CurrentGridPosition} because a valid attack is already available against {DescribeUnit(target)}.");
                return TacticalResult<bool>.Success(false);
            }

            var destinationResult = TryChooseActiveMoveDestination(
                activeUnit,
                target,
                mapResult.Value,
                manager.UnitRegistry);
            if (destinationResult.IsFailure)
            {
                LogDecision($"AI found no active move for {DescribeUnit(activeUnit)} toward {DescribeUnit(target)}: {destinationResult.ErrorMessage}");
                return TacticalResult<bool>.Success(false);
            }

            var destination = destinationResult.Value;
            var start = activeUnit.CurrentGridPosition;
            var apBeforeMove = activeUnit.CurrentAP;
            var moveResult = manager.MoveActiveUnit(activeUnit, destination);
            if (moveResult.IsFailure)
            {
                return TacticalResult<bool>.Failure(moveResult.ErrorMessage);
            }

            LogDecision(
                $"AI moved {DescribeUnit(activeUnit)} from {start} to {destination} toward {DescribeUnit(target)}; AP {apBeforeMove}->{activeUnit.CurrentAP}, reserve target {reactionApReserve}.");
            return TacticalResult<bool>.Success(true);
        }

        private static TacticalResult<IGridMap> ResolveCurrentMap(CombatManager manager)
        {
            if (manager.GridManager == null)
            {
                return TacticalResult<IGridMap>.Failure($"{nameof(AiController)} cannot evaluate active choices because no {nameof(GridManager)} is assigned.");
            }

            if (!manager.GridManager.HasCurrentMap)
            {
                if (manager.GridManager.MapDefinition == null)
                {
                    return TacticalResult<IGridMap>.Failure($"{nameof(AiController)} cannot evaluate active choices because {nameof(GridManager)} has no map definition assigned.");
                }

                if (!manager.GridManager.RebuildMap())
                {
                    return TacticalResult<IGridMap>.Failure($"{nameof(AiController)} cannot evaluate active choices because {nameof(GridManager)} could not build a current map.");
                }
            }

            if (manager.GridManager.CurrentMap == null)
            {
                return TacticalResult<IGridMap>.Failure($"{nameof(AiController)} cannot evaluate active choices because {nameof(GridManager)} has no current map.");
            }

            return TacticalResult<IGridMap>.Success(manager.GridManager.CurrentMap);
        }

        private TacticalResult<bool> TryDeclareMeleeAttackAtNearestTarget(CombatManager manager, TacticalUnit activeUnit)
        {
            if (manager.UnitRegistry == null)
            {
                LogDecision($"AI could not evaluate melee actions for {DescribeUnit(activeUnit)} because no unit registry is assigned.");
                return TacticalResult<bool>.Success(false);
            }

            var mapResult = ResolveCurrentMap(manager);
            if (mapResult.IsFailure)
            {
                LogDecision($"AI could not evaluate melee actions for {DescribeUnit(activeUnit)}: {mapResult.ErrorMessage}");
                return TacticalResult<bool>.Success(false);
            }

            var targetResult = TryChooseMeleeAttackTarget(
                activeUnit,
                manager.UnitRegistry.GetLivingUnits(),
                mapResult.Value,
                manager.CurrentState);
            if (targetResult.IsFailure)
            {
                LogDecision($"AI found no melee action for {DescribeUnit(activeUnit)}: {targetResult.ErrorMessage}");
                return TacticalResult<bool>.Success(false);
            }

            var target = targetResult.Value;
            var previousAP = activeUnit.CurrentAP;
            var request = new PlayerCommandRequest(
                PlayerCommandType.ConfirmTarget,
                activeUnit,
                SelectionActionMode.Melee,
                SelectionTarget.ForUnit(target),
                SelectionState.Empty);
            var declarationResult = manager.DeclareAndResolveSelectedAction(request);
            if (declarationResult.IsFailure)
            {
                return TacticalResult<bool>.Failure(declarationResult.ErrorMessage);
            }

            LogDecision(
                $"AI declared {DefaultMeleeSlashDisplayName} with {DescribeUnit(activeUnit)} against {DescribeUnit(target)}; AP {previousAP}->{activeUnit.CurrentAP}.");
            return TacticalResult<bool>.Success(true);
        }

        private TacticalResult<bool> TryDeclareConeAttackAtBestDirection(CombatManager manager, TacticalUnit activeUnit)
        {
            if (manager.UnitRegistry == null)
            {
                LogDecision($"AI could not evaluate cone actions for {DescribeUnit(activeUnit)} because no unit registry is assigned.");
                return TacticalResult<bool>.Success(false);
            }

            var mapResult = ResolveCurrentMap(manager);
            if (mapResult.IsFailure)
            {
                LogDecision($"AI could not evaluate cone actions for {DescribeUnit(activeUnit)}: {mapResult.ErrorMessage}");
                return TacticalResult<bool>.Success(false);
            }

            var choiceResult = TryChooseConeAttack(
                activeUnit,
                manager.UnitRegistry.GetLivingUnits(),
                mapResult.Value,
                manager.CurrentState);
            if (choiceResult.IsFailure)
            {
                LogDecision($"AI found no cone action for {DescribeUnit(activeUnit)}: {choiceResult.ErrorMessage}");
                return TacticalResult<bool>.Success(false);
            }

            var choice = choiceResult.Value;
            var previousAP = activeUnit.CurrentAP;
            var request = new PlayerCommandRequest(
                PlayerCommandType.ConfirmTarget,
                activeUnit,
                SelectionActionMode.Cone,
                SelectionTarget.ForCell(choice.TargetCell),
                SelectionState.Empty);
            var declarationResult = manager.DeclareAndResolveSelectedAction(request);
            if (declarationResult.IsFailure)
            {
                return TacticalResult<bool>.Failure(declarationResult.ErrorMessage);
            }

            LogDecision(
                $"AI declared {choice.Ability.DisplayName} with {DescribeUnit(activeUnit)} toward {choice.Direction} via {choice.TargetCell}; threatened {choice.HostileThreatCount} hostile(s) and {choice.FriendlyThreatCount} friendly unit(s); AP {previousAP}->{activeUnit.CurrentAP}.");
            return TacticalResult<bool>.Success(true);
        }

        private static TacticalResult<AbilityDefinition> TryResolveMeleeSlashAbility(TacticalUnit actor)
        {
            if (actor == null)
            {
                return TacticalResult<AbilityDefinition>.Failure("Cannot choose a melee action because no acting unit was provided.");
            }

            var loadout = actor.GetComponent<UnitAbilityLoadout>();
            if (loadout == null)
            {
                return TacticalResult<AbilityDefinition>.Failure($"{DescribeUnit(actor)} has no ability loadout for {DefaultMeleeSlashDisplayName}.");
            }

            AbilityDefinition fallback = null;
            var actionAbilities = loadout.GetActionAbilities();
            for (var i = 0; i < actionAbilities.Count; i += 1)
            {
                var ability = actionAbilities[i];
                if (!IsMeleeAttackAbility(ability))
                {
                    continue;
                }

                if (IsDefaultMeleeSlashAbility(ability))
                {
                    return TacticalResult<AbilityDefinition>.Success(ability);
                }

                if (fallback == null)
                {
                    fallback = ability;
                }
            }

            if (fallback != null)
            {
                return TacticalResult<AbilityDefinition>.Success(fallback);
            }

            return TacticalResult<AbilityDefinition>.Failure($"{DescribeUnit(actor)} has no active {DefaultMeleeSlashDisplayName} ability assigned.");
        }

        private static TacticalResult<AbilityDefinition> TryResolveConeShotAbility(TacticalUnit actor)
        {
            if (actor == null)
            {
                return TacticalResult<AbilityDefinition>.Failure("Cannot choose a cone action because no acting unit was provided.");
            }

            var loadout = actor.GetComponent<UnitAbilityLoadout>();
            if (loadout == null)
            {
                return TacticalResult<AbilityDefinition>.Failure($"{DescribeUnit(actor)} has no ability loadout for {DefaultConeShotDisplayName}.");
            }

            AbilityDefinition fallback = null;
            var actionAbilities = loadout.GetActionAbilities();
            for (var i = 0; i < actionAbilities.Count; i += 1)
            {
                var ability = actionAbilities[i];
                if (!IsConeAttackAbility(ability))
                {
                    continue;
                }

                if (IsDefaultConeShotAbility(ability))
                {
                    return TacticalResult<AbilityDefinition>.Success(ability);
                }

                if (fallback == null)
                {
                    fallback = ability;
                }
            }

            if (fallback != null)
            {
                return TacticalResult<AbilityDefinition>.Success(fallback);
            }

            return TacticalResult<AbilityDefinition>.Failure($"{DescribeUnit(actor)} has no active {DefaultConeShotDisplayName} ability assigned.");
        }

        private static TacticalResult ValidateMeleeAttackChoiceContext(
            TacticalUnit actor,
            IEnumerable<TacticalUnit> candidates,
            IGridMap map,
            CombatState combatState)
        {
            if (actor == null)
            {
                return TacticalResult.Failure("Cannot choose an AI melee target because no acting unit was provided.");
            }

            if (candidates == null)
            {
                return TacticalResult.Failure($"Cannot choose an AI melee target for {DescribeUnit(actor)} because no candidate units were provided.");
            }

            if (map == null)
            {
                return TacticalResult.Failure($"Cannot choose an AI melee target for {DescribeUnit(actor)} because no grid map is available.");
            }

            if (combatState == null)
            {
                return TacticalResult.Failure($"Cannot choose an AI melee target for {DescribeUnit(actor)} because combat state is missing.");
            }

            if (!actor.IsAlive)
            {
                return TacticalResult.Failure($"{DescribeUnit(actor)} cannot choose an AI melee target because it is defeated.");
            }

            return TacticalResult.Success();
        }

        private static TacticalResult ValidateConeAttackChoiceContext(
            TacticalUnit actor,
            IEnumerable<TacticalUnit> candidates,
            IGridMap map,
            CombatState combatState)
        {
            if (actor == null)
            {
                return TacticalResult.Failure("Cannot choose an AI cone action because no acting unit was provided.");
            }

            if (candidates == null)
            {
                return TacticalResult.Failure($"Cannot choose an AI cone action for {DescribeUnit(actor)} because no candidate units were provided.");
            }

            if (map == null)
            {
                return TacticalResult.Failure($"Cannot choose an AI cone action for {DescribeUnit(actor)} because no grid map is available.");
            }

            if (combatState == null)
            {
                return TacticalResult.Failure($"Cannot choose an AI cone action for {DescribeUnit(actor)} because combat state is missing.");
            }

            if (!actor.IsAlive)
            {
                return TacticalResult.Failure($"{DescribeUnit(actor)} cannot choose an AI cone action because it is defeated.");
            }

            if (!map.TryGetCell(actor.CurrentGridPosition, out _))
            {
                return TacticalResult.Failure($"{DescribeUnit(actor)} cannot choose an AI cone action because its current cell {actor.CurrentGridPosition} is not on the map.");
            }

            return TacticalResult.Success();
        }

        private int CompareTargetCandidates(TacticalUnit actor, TacticalUnit left, TacticalUnit right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            var distanceComparison = GetTargetSelectionDistance(actor, left)
                .CompareTo(GetTargetSelectionDistance(actor, right));
            if (distanceComparison != 0)
            {
                return distanceComparison;
            }

            var hpComparison = left.CurrentHP.CompareTo(right.CurrentHP);
            if (hpComparison != 0)
            {
                return hpComparison;
            }

            var idComparison = left.UnitId.CompareTo(right.UnitId);
            if (idComparison != 0)
            {
                return idComparison;
            }

            var nameComparison = string.Compare(left.name, right.name, StringComparison.Ordinal);
            if (nameComparison != 0)
            {
                return nameComparison;
            }

            return left.GetInstanceID().CompareTo(right.GetInstanceID());
        }

        private static bool IsSelectableHostileTarget(TacticalUnit actor, TacticalUnit candidate)
        {
            return candidate != null
                && candidate.IsAlive
                && !ReferenceEquals(candidate, actor)
                && actor.Team.IsHostileTo(candidate.Team);
        }

        private static TacticalResult ValidateMovementChoiceContext(
            TacticalUnit actor,
            TacticalUnit target,
            IGridMap map)
        {
            if (actor == null)
            {
                return TacticalResult.Failure("Cannot choose an AI active move because no acting unit was provided.");
            }

            if (target == null)
            {
                return TacticalResult.Failure($"Cannot choose an AI active move for {DescribeUnit(actor)} because no target was provided.");
            }

            if (map == null)
            {
                return TacticalResult.Failure($"Cannot choose an AI active move for {DescribeUnit(actor)} because no grid map is available.");
            }

            if (!actor.IsAlive)
            {
                return TacticalResult.Failure($"{DescribeUnit(actor)} cannot choose an AI active move because it is defeated.");
            }

            if (!target.IsAlive)
            {
                return TacticalResult.Failure($"{DescribeUnit(actor)} cannot choose an AI active move because target {DescribeUnit(target)} is defeated.");
            }

            if (!actor.Team.IsHostileTo(target.Team))
            {
                return TacticalResult.Failure($"{DescribeUnit(actor)} cannot choose an AI active move toward friendly unit {DescribeUnit(target)}.");
            }

            if (!map.TryGetCell(actor.CurrentGridPosition, out _))
            {
                return TacticalResult.Failure($"{DescribeUnit(actor)} cannot choose an AI active move because its current cell {actor.CurrentGridPosition} is not on the map.");
            }

            if (!map.TryGetCell(target.CurrentGridPosition, out _))
            {
                return TacticalResult.Failure($"{DescribeUnit(actor)} cannot choose an AI active move because target cell {target.CurrentGridPosition} is not on the map.");
            }

            return TacticalResult.Success();
        }

        private static bool IsAttackAbility(AbilityDefinition ability)
        {
            return ability != null
                && ability.CanBeUsedAsAction
                && ability.Damage > 0
                && (ability.Shape == AbilityShape.Melee
                    || ability.Shape == AbilityShape.SingleTarget
                    || ability.Shape == AbilityShape.Cone
                    || ability.Shape == AbilityShape.Radius);
        }

        private static bool IsMeleeAttackAbility(AbilityDefinition ability)
        {
            return ability != null
                && ability.CanBeUsedAsAction
                && ability.Shape == AbilityShape.Melee
                && ability.Damage > 0;
        }

        private static bool IsConeAttackAbility(AbilityDefinition ability)
        {
            return ability != null
                && ability.CanBeUsedAsAction
                && ability.Shape == AbilityShape.Cone
                && ability.Damage > 0;
        }

        private static bool IsDefaultMeleeSlashAbility(AbilityDefinition ability)
        {
            return ability != null
                && (string.Equals(ability.AbilityKey, DefaultMeleeSlashAbilityKey, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(ability.DisplayName, DefaultMeleeSlashDisplayName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsDefaultConeShotAbility(AbilityDefinition ability)
        {
            return ability != null
                && (string.Equals(ability.AbilityKey, DefaultConeShotAbilityKey, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(ability.DisplayName, DefaultConeShotDisplayName, StringComparison.OrdinalIgnoreCase));
        }

        private static bool TryCreateAttackTarget(
            TacticalUnit actor,
            TacticalUnit target,
            AbilityDefinition ability,
            out ActionTarget actionTarget)
        {
            actionTarget = ActionTarget.None;
            if (actor == null || target == null || ability == null)
            {
                return false;
            }

            switch (ability.Shape)
            {
                case AbilityShape.Melee:
                case AbilityShape.SingleTarget:
                    actionTarget = ActionTarget.ForUnit(target);
                    return true;
                case AbilityShape.Cone:
                    if (actor.CurrentGridPosition == target.CurrentGridPosition)
                    {
                        return false;
                    }

                    actionTarget = ActionTarget.ForCell(target.CurrentGridPosition)
                        .WithDirection(CardinalDirectionMath.FromTo(actor.CurrentGridPosition, target.CurrentGridPosition));
                    return true;
                case AbilityShape.Radius:
                    actionTarget = ActionTarget.ForCell(target.CurrentGridPosition);
                    return true;
                default:
                    return false;
            }
        }

        private static List<TacticalUnit> CollectLivingCandidateUnits(IEnumerable<TacticalUnit> candidates)
        {
            var livingUnits = new List<TacticalUnit>();
            foreach (var candidate in candidates)
            {
                if (candidate != null && candidate.IsAlive)
                {
                    livingUnits.Add(candidate);
                }
            }

            return livingUnits;
        }

        private static bool TryFindConeDeclarationCell(
            TacticalUnit actor,
            AbilityDefinition ability,
            CardinalDirection direction,
            IGridMap map,
            CombatState combatState,
            out GridPosition targetCell)
        {
            targetCell = default;
            for (var distance = 1; distance <= ability.Range; distance += 1)
            {
                if (!TryFindCellInDirection(actor.CurrentGridPosition, direction, distance, map, out var candidateCell))
                {
                    continue;
                }

                var actionTarget = ActionTarget.ForCellAndDirection(candidateCell, direction);
                var validationContext = new AbilityTargetValidationContext(
                    actor,
                    ability,
                    actionTarget,
                    AbilityUsage.Action,
                    combatState,
                    map);
                if (AbilityTargetValidator.Instance.ValidateTarget(validationContext).IsFailure)
                {
                    continue;
                }

                targetCell = candidateCell;
                return true;
            }

            return false;
        }

        private static bool TryFindCellInDirection(
            GridPosition origin,
            CardinalDirection direction,
            int distance,
            IGridMap map,
            out GridPosition cellPosition)
        {
            var offset = direction.ToOffset();
            var targetX = origin.X + (offset.X * distance);
            var targetZ = origin.Z + (offset.Z * distance);
            foreach (var cell in map.AllCells)
            {
                var position = cell.Position;
                if (position.X == targetX && position.Z == targetZ)
                {
                    cellPosition = position;
                    return true;
                }
            }

            cellPosition = default;
            return false;
        }

        private static int CompareConeChoices(AiConeAttackChoice left, AiConeAttackChoice right)
        {
            var hostileComparison = right.HostileThreatCount.CompareTo(left.HostileThreatCount);
            if (hostileComparison != 0)
            {
                return hostileComparison;
            }

            var friendlyComparison = left.FriendlyThreatCount.CompareTo(right.FriendlyThreatCount);
            if (friendlyComparison != 0)
            {
                return friendlyComparison;
            }

            var distanceComparison = left.NearestHostileDistance.CompareTo(right.NearestHostileDistance);
            if (distanceComparison != 0)
            {
                return distanceComparison;
            }

            var directionComparison = left.Direction.CompareTo(right.Direction);
            if (directionComparison != 0)
            {
                return directionComparison;
            }

            return CompareGridPositions(left.TargetCell, right.TargetCell);
        }

        private static int CompareMoveDestination(
            ReachableCell left,
            int leftDistance,
            ReachableCell right,
            int rightDistance)
        {
            var distanceComparison = leftDistance.CompareTo(rightDistance);
            if (distanceComparison != 0)
            {
                return distanceComparison;
            }

            var costComparison = left.TotalApCost.CompareTo(right.TotalApCost);
            if (costComparison != 0)
            {
                return costComparison;
            }

            return CompareGridPositions(left.Position, right.Position);
        }

        private static int CompareGridPositions(GridPosition left, GridPosition right)
        {
            var xComparison = left.X.CompareTo(right.X);
            if (xComparison != 0)
            {
                return xComparison;
            }

            var zComparison = left.Z.CompareTo(right.Z);
            if (zComparison != 0)
            {
                return zComparison;
            }

            return left.Y.CompareTo(right.Y);
        }

        private TacticalResult ValidateActiveTurn(CombatManager manager)
        {
            if (manager == null)
            {
                return TacticalResult.Failure($"{nameof(AiController)} cannot take an active turn because no {nameof(CombatManager)} is assigned.");
            }

            var state = manager.CurrentState;
            if (state == null)
            {
                return TacticalResult.Failure($"{nameof(AiController)} cannot take an active turn because combat state is missing.");
            }

            if (!state.IsActiveUnitPhase)
            {
                return TacticalResult.Failure($"{nameof(AiController)} cannot take an active turn while combat phase is {state.Phase}.");
            }

            if (!ControlsUnit(state.ActiveUnit))
            {
                return TacticalResult.Failure(
                    $"{nameof(AiController)} controls {controlledTeam} units, but the active unit is {DescribeUnit(state.ActiveUnit)}.");
            }

            return TacticalResult.Success();
        }

        private TacticalResult ValidateReactionTurn(
            CombatManager manager,
            ActionIntent sourceIntent,
            TacticalUnit reactor)
        {
            if (manager == null)
            {
                return TacticalResult.Failure($"{nameof(AiController)} cannot take a reaction turn because no {nameof(CombatManager)} is assigned.");
            }

            if (sourceIntent == null)
            {
                return TacticalResult.Failure($"{nameof(AiController)} cannot take a reaction turn because the source action intent is missing.");
            }

            if (!ControlsUnit(reactor))
            {
                return TacticalResult.Failure(
                    $"{nameof(AiController)} controls {controlledTeam} units, but the reacting unit is {DescribeUnit(reactor)}.");
            }

            var state = manager.CurrentState;
            if (state == null)
            {
                return TacticalResult.Failure($"{nameof(AiController)} cannot take a reaction turn because combat state is missing.");
            }

            if (!state.IsReactionPhase)
            {
                return TacticalResult.Failure($"{nameof(AiController)} cannot take a reaction turn while combat phase is {state.Phase}.");
            }

            if (!ReferenceEquals(state.PendingActionIntent, sourceIntent))
            {
                return TacticalResult.Failure($"{nameof(AiController)} cannot react to a source intent that is not pending.");
            }

            if (!ReferenceEquals(state.ReactingUnit, reactor))
            {
                return TacticalResult.Failure(
                    $"{nameof(AiController)} cannot react with {DescribeUnit(reactor)} because the current reactor is {DescribeUnit(state.ReactingUnit)}.");
            }

            return TacticalResult.Success();
        }

        private void LogDecision(string message)
        {
            if (!logDecisions)
            {
                return;
            }

            Debug.Log($"[AI] {message}", this);
        }

        private static string DescribeUnit(TacticalUnit unit)
        {
            if (unit == null)
            {
                return "no unit";
            }

            return unit.IsInitialized ? $"{unit.DisplayName} {unit.UnitId} [{unit.Team}]" : unit.DisplayName;
        }
    }
}
