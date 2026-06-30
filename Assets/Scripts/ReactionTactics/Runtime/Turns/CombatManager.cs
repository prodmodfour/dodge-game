using System.Collections.Generic;
using ReactionTactics.Actions;
using ReactionTactics.Core;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Targeting;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Turns
{
    /// <summary>
    /// Scene-level shell for the high-level combat loop. It starts combat, refreshes
    /// round AP, advances active turns, listens for routed commands, and sends
    /// declared active actions through the shell resolver; reactions and win/loss
    /// checks are added by later tickets.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CombatManager : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Scene registry that owns living units and grid occupancy.")]
        private UnitRegistry unitRegistry;

        [SerializeField]
        [Tooltip("Scene grid manager for the active tactical map.")]
        private GridManager gridManager;

        [SerializeField]
        [Tooltip("Player command router that will feed turn commands into combat systems.")]
        private PlayerCommandRouter inputRouter;

        [SerializeField]
        [Tooltip("Optional scene-scoped combat event bus for UI and logs.")]
        private CombatEventBus eventBus;

        [SerializeField]
        [Tooltip("Automatically start combat during Start when the scene enters play mode.")]
        private bool startCombatOnStart = true;

        [SerializeField]
        [Tooltip("Write concise round-start and active-unit logs while the prototype combat loop advances.")]
        private bool logCombatStart = true;

        [SerializeField]
        [Tooltip("Write concise declaration and resolution logs while actions pass through the shell resolver.")]
        private bool logActionFlow = true;

        private readonly CombatState currentState = new CombatState();
        private readonly TurnOrderService turnOrderService = new TurnOrderService();
        private readonly RoundLifecycleService roundLifecycleService = new RoundLifecycleService();
        private readonly ActionDeclarationService actionDeclarationService = new ActionDeclarationService();
        private PlayerCommandRouter subscribedInputRouter;

        public CombatState CurrentState
        {
            get { return currentState; }
        }

        public TurnOrderService TurnOrder
        {
            get { return turnOrderService; }
        }

        public UnitRegistry UnitRegistry
        {
            get { return unitRegistry; }
        }

        public GridManager GridManager
        {
            get { return gridManager; }
        }

        public PlayerCommandRouter InputRouter
        {
            get { return inputRouter; }
        }

        public CombatEventBus EventBus
        {
            get { return eventBus; }
        }

        public bool StartCombatOnStart
        {
            get { return startCombatOnStart; }
            set { startCombatOnStart = value; }
        }

        public bool LogCombatStart
        {
            get { return logCombatStart; }
            set { logCombatStart = value; }
        }

        public bool LogActionFlow
        {
            get { return logActionFlow; }
            set { logActionFlow = value; }
        }

        /// <summary>
        /// Assigns scene dependencies in one step for editor setup tools and tests.
        /// </summary>
        public void Configure(
            UnitRegistry unitRegistry,
            GridManager gridManager,
            PlayerCommandRouter inputRouter,
            CombatEventBus eventBus = null)
        {
            UnsubscribeFromInputRouter();
            this.unitRegistry = unitRegistry;
            this.gridManager = gridManager;
            this.inputRouter = inputRouter;
            this.eventBus = eventBus;

            if (isActiveAndEnabled)
            {
                SubscribeToInputRouter();
            }
        }

        /// <summary>
        /// Starts the initial combat round and selects the first living unit in deterministic turn order.
        /// </summary>
        public TacticalResult StartCombat()
        {
            if (currentState.Phase != CombatPhase.NotStarted)
            {
                return TacticalResult.Failure($"Cannot start combat while phase is {currentState.Phase}.");
            }

            var dependencyResult = ResolveRequiredDependencies();
            if (dependencyResult.IsFailure)
            {
                return dependencyResult;
            }

            var bootstrapResult = EnsureRegistryHasUnits();
            if (bootstrapResult.IsFailure)
            {
                return bootstrapResult;
            }

            var livingUnits = unitRegistry.GetLivingUnits();
            if (livingUnits.Count == 0)
            {
                return TacticalResult.Failure("Cannot start combat because the unit registry has no living units.");
            }

            roundLifecycleService.StartNextRound(currentState, livingUnits, eventBus);
            turnOrderService.SetUnits(livingUnits);

            if (!turnOrderService.TryGetCurrentActiveUnit(out var activeUnit))
            {
                currentState.Reset();
                return TacticalResult.Failure("Cannot start combat because no living unit could be selected as active.");
            }

            currentState.SetState(currentState.CurrentRound, CombatPhase.ActiveTurn, activeUnit);
            eventBus?.PublishActiveUnitChanged(null, activeUnit);

            if (logCombatStart)
            {
                Debug.Log($"Reaction Tactics round {currentState.CurrentRound} started with {livingUnits.Count} living units.", this);
                Debug.Log($"Reaction Tactics active unit: {activeUnit.DisplayName} {activeUnit.UnitId} [{activeUnit.Team}] at {activeUnit.CurrentGridPosition}.", this);
            }

            return TacticalResult.Success();
        }

        /// <summary>
        /// Returns true when the given unit may choose or confirm active-turn actions.
        /// </summary>
        public bool CanUnitTakeAction(TacticalUnit unit)
        {
            return ValidateUnitCanTakeAction(unit).IsSuccess;
        }

        /// <summary>
        /// Validates whether a unit may use active-turn commands. Active actions are legal
        /// only for the current active unit during ActiveTurn or ActionTargeting phases.
        /// </summary>
        public TacticalResult ValidateUnitCanTakeAction(TacticalUnit unit)
        {
            if (unit == null)
            {
                return TacticalResult.Failure("Cannot take an active action because no tactical unit was provided.");
            }

            if (!unit.IsAlive)
            {
                return TacticalResult.Failure($"{DescribeUnit(unit)} cannot take an active action because it is defeated.");
            }

            if (!currentState.IsActiveUnitPhase)
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(unit)} cannot take an active action while combat phase is {currentState.Phase}. "
                    + $"Active actions are only legal during {CombatPhase.ActiveTurn} or {CombatPhase.ActionTargeting}.");
            }

            if (currentState.ActiveUnit == null)
            {
                return TacticalResult.Failure("Cannot take an active action because no active unit is selected.");
            }

            if (!ReferenceEquals(unit, currentState.ActiveUnit))
            {
                return TacticalResult.Failure(
                    $"{DescribeUnit(unit)} cannot take an active action because the active unit is {DescribeUnit(currentState.ActiveUnit)}.");
            }

            return TacticalResult.Success();
        }

        /// <summary>
        /// Ends the current active unit's turn. The next living unit in deterministic
        /// order becomes active, or a new round starts and refreshes AP when the order is exhausted.
        /// </summary>
        public TacticalResult EndActiveTurn()
        {
            if (!currentState.IsActiveUnitPhase)
            {
                return TacticalResult.Failure($"Cannot end the active turn while combat phase is {currentState.Phase}.");
            }

            if (currentState.ActiveUnit == null)
            {
                return TacticalResult.Failure("Cannot end the active turn because no active unit is selected.");
            }

            if (unitRegistry == null)
            {
                return TacticalResult.Failure($"Cannot end the active turn because {nameof(UnitRegistry)} is missing.");
            }

            var previousActiveUnit = currentState.ActiveUnit;
            if (turnOrderService.TryAdvanceToNext())
            {
                if (!turnOrderService.TryGetCurrentActiveUnit(out var nextActiveUnit))
                {
                    return TacticalResult.Failure("Turn order advanced but no living active unit could be selected.");
                }

                SetActiveTurn(previousActiveUnit, nextActiveUnit);
                return TacticalResult.Success();
            }

            return StartNextRoundAfterTurnOrderEnds(previousActiveUnit);
        }

        private void Awake()
        {
            ResolveSceneReferences();
        }

        private void OnEnable()
        {
            ResolveSceneReferences();
            SubscribeToInputRouter();
        }

        private void OnDisable()
        {
            UnsubscribeFromInputRouter();
        }

        private void Start()
        {
            if (!startCombatOnStart)
            {
                return;
            }

            var result = StartCombat();
            if (result.IsFailure)
            {
                Debug.LogError($"{nameof(CombatManager)} failed to start combat: {result.ErrorMessage}", this);
            }
        }

        private void Reset()
        {
            startCombatOnStart = true;
            logCombatStart = true;
            logActionFlow = true;
            ResolveSceneReferences();
        }

        private TacticalResult ResolveRequiredDependencies()
        {
            ResolveSceneReferences();

            if (unitRegistry == null)
            {
                return TacticalResult.Failure($"{nameof(CombatManager)} requires a {nameof(UnitRegistry)} reference.");
            }

            if (gridManager == null)
            {
                return TacticalResult.Failure($"{nameof(CombatManager)} requires a {nameof(GridManager)} reference.");
            }

            if (inputRouter == null)
            {
                return TacticalResult.Failure($"{nameof(CombatManager)} requires a {nameof(PlayerCommandRouter)} reference.");
            }

            SubscribeToInputRouter();
            return TacticalResult.Success();
        }

        private void ResolveSceneReferences()
        {
            if (unitRegistry == null)
            {
                unitRegistry = FindAnyObjectByType<UnitRegistry>();
            }

            if (gridManager == null)
            {
                gridManager = FindAnyObjectByType<GridManager>();
            }

            if (inputRouter == null)
            {
                inputRouter = FindAnyObjectByType<PlayerCommandRouter>();
            }

            if (eventBus == null)
            {
                eventBus = GetComponent<CombatEventBus>();
                if (eventBus == null)
                {
                    eventBus = FindAnyObjectByType<CombatEventBus>();
                }
            }
        }

        private TacticalResult StartNextRoundAfterTurnOrderEnds(TacticalUnit previousActiveUnit)
        {
            var livingUnits = unitRegistry.GetLivingUnits();
            if (livingUnits.Count == 0)
            {
                currentState.SetState(currentState.CurrentRound, CombatPhase.CombatOver, null);
                eventBus?.PublishActiveUnitChanged(previousActiveUnit, null);
                return TacticalResult.Failure("Cannot start a new round because no living units remain.");
            }

            roundLifecycleService.StartNextRound(currentState, livingUnits, eventBus);
            turnOrderService.SetUnits(livingUnits);

            if (!turnOrderService.TryGetCurrentActiveUnit(out var nextActiveUnit))
            {
                currentState.SetState(currentState.CurrentRound, CombatPhase.CombatOver, null);
                eventBus?.PublishActiveUnitChanged(previousActiveUnit, null);
                return TacticalResult.Failure("Cannot start a new round because no living unit could be selected as active.");
            }

            SetActiveTurn(previousActiveUnit, nextActiveUnit);
            return TacticalResult.Success();
        }

        private void SetActiveTurn(TacticalUnit previousActiveUnit, TacticalUnit nextActiveUnit)
        {
            currentState.SetState(currentState.CurrentRound, CombatPhase.ActiveTurn, nextActiveUnit);
            eventBus?.PublishActiveUnitChanged(previousActiveUnit, nextActiveUnit);

            if (logCombatStart)
            {
                Debug.Log($"Reaction Tactics active unit: {nextActiveUnit.DisplayName} {nextActiveUnit.UnitId} [{nextActiveUnit.Team}] at {nextActiveUnit.CurrentGridPosition}.", this);
            }
        }

        private void HandleCommandRequested(PlayerCommandRequest request)
        {
            if (request.CommandType == PlayerCommandType.EndTurn)
            {
                var endTurnResult = EndActiveTurn();
                if (endTurnResult.IsFailure)
                {
                    Debug.LogWarning($"{nameof(CombatManager)} could not end turn: {endTurnResult.ErrorMessage}", this);
                }

                return;
            }

            if (!RequiresActiveActionLegality(request))
            {
                return;
            }

            var actionResult = ValidateUnitCanTakeAction(request.Unit);
            if (actionResult.IsFailure)
            {
                ClearIllegalActiveActionSelection();
                Debug.LogWarning(
                    $"{nameof(CombatManager)} rejected active action command {request.CommandType}: {actionResult.ErrorMessage}",
                    this);
                return;
            }

            if (request.CommandType != PlayerCommandType.ConfirmTarget || !IsDeclarableActionMode(request.ActionMode))
            {
                return;
            }

            var declarationResult = DeclareAndResolveSelectedAction(request);
            if (declarationResult.IsFailure)
            {
                Debug.LogWarning(
                    $"{nameof(CombatManager)} could not declare action command {request.ActionMode}: {declarationResult.ErrorMessage}",
                    this);
            }
        }

        /// <summary>
        /// Declares the selected active ability from a routed target-confirmation request,
        /// stores the intent while it resolves, and returns control to the same active unit.
        /// Reaction windows are intentionally skipped until the reaction milestone.
        /// </summary>
        public TacticalResult DeclareAndResolveSelectedAction(PlayerCommandRequest request)
        {
            if (request.CommandType != PlayerCommandType.ConfirmTarget)
            {
                return TacticalResult.Failure($"Cannot declare an action from command {request.CommandType}; a confirmed target is required.");
            }

            if (!IsDeclarableActionMode(request.ActionMode))
            {
                return TacticalResult.Failure($"Selection mode {request.ActionMode} does not declare an active ability action yet.");
            }

            var legalityResult = ValidateUnitCanTakeAction(request.Unit);
            if (legalityResult.IsFailure)
            {
                return legalityResult;
            }

            var abilityResult = ResolveSelectedActionAbility(request.Unit, request.ActionMode);
            if (abilityResult.IsFailure)
            {
                return TacticalResult.Failure(abilityResult.ErrorMessage);
            }

            var ability = abilityResult.Value;
            var targetResult = CreateActionTarget(request.Unit, ability, request.Target);
            if (targetResult.IsFailure)
            {
                return TacticalResult.Failure(targetResult.ErrorMessage);
            }

            var mapResult = ResolveCurrentMap();
            if (mapResult.IsFailure)
            {
                return TacticalResult.Failure(mapResult.ErrorMessage);
            }

            var target = targetResult.Value;
            var previousAP = request.Unit.CurrentAP;
            var declaredAffectedCells = CreateDeclaredAffectedCells(request.Unit, ability, target, mapResult.Value);
            var intentResult = actionDeclarationService.DeclareAction(
                request.Unit,
                ability,
                target,
                currentState,
                mapResult.Value,
                declaredAffectedCells);

            if (intentResult.IsFailure)
            {
                return TacticalResult.Failure(intentResult.ErrorMessage);
            }

            var intent = intentResult.Value;
            currentState.SetState(currentState.CurrentRound, CombatPhase.ResolvingAction, request.Unit, null, intent);
            PublishActionDeclarationEvents(intent, previousAP);
            LogActionDeclared(intent, previousAP);

            var resolver = new ActionResolver(eventBus, this, logActionFlow, () => unitRegistry.GetLivingUnits());
            var resolveResult = resolver.Resolve(intent);
            if (resolveResult.IsFailure)
            {
                return resolveResult;
            }

            currentState.SetState(currentState.CurrentRound, CombatPhase.ActiveTurn, request.Unit, null, null);
            ClearResolvedActionSelection();
            return TacticalResult.Success();
        }

        private void SubscribeToInputRouter()
        {
            if (inputRouter == null)
            {
                return;
            }

            if (subscribedInputRouter == inputRouter)
            {
                return;
            }

            UnsubscribeFromInputRouter();
            subscribedInputRouter = inputRouter;
            subscribedInputRouter.CommandRequested += HandleCommandRequested;
        }

        private void UnsubscribeFromInputRouter()
        {
            if (subscribedInputRouter == null)
            {
                return;
            }

            subscribedInputRouter.CommandRequested -= HandleCommandRequested;
            subscribedInputRouter = null;
        }

        private void ClearIllegalActiveActionSelection()
        {
            var selectionController = inputRouter != null ? inputRouter.SelectionController : null;
            if (selectionController == null || !IsActiveActionMode(selectionController.SelectedActionMode))
            {
                return;
            }

            selectionController.ClearActionMode();
        }

        private static bool RequiresActiveActionLegality(PlayerCommandRequest request)
        {
            switch (request.CommandType)
            {
                case PlayerCommandType.SelectMove:
                case PlayerCommandType.SelectAttack:
                    return true;
                case PlayerCommandType.ConfirmTarget:
                    return IsActiveActionMode(request.ActionMode);
                default:
                    return false;
            }
        }

        private TacticalResult<AbilityDefinition> ResolveSelectedActionAbility(TacticalUnit unit, SelectionActionMode actionMode)
        {
            if (unit == null)
            {
                return TacticalResult<AbilityDefinition>.Failure("Cannot resolve an action ability because no unit was selected.");
            }

            if (!TryGetAbilityShapeForActionMode(actionMode, out var requiredShape))
            {
                return TacticalResult<AbilityDefinition>.Failure($"Selection mode {actionMode} does not map to a declarable ability shape.");
            }

            var loadout = unit.GetComponent<UnitAbilityLoadout>();
            if (loadout == null)
            {
                return TacticalResult<AbilityDefinition>.Failure($"{DescribeUnit(unit)} has no ability loadout for {actionMode}.");
            }

            var actionAbilities = loadout.GetActionAbilities();
            for (var i = 0; i < actionAbilities.Count; i += 1)
            {
                var ability = actionAbilities[i];
                if (ability != null && ability.Shape == requiredShape)
                {
                    return TacticalResult<AbilityDefinition>.Success(ability);
                }
            }

            return TacticalResult<AbilityDefinition>.Failure($"{DescribeUnit(unit)} has no active {actionMode} ability assigned.");
        }

        private TacticalResult<IGridMap> ResolveCurrentMap()
        {
            if (gridManager == null)
            {
                return TacticalResult<IGridMap>.Failure($"Cannot declare an action because {nameof(GridManager)} is missing.");
            }

            if (!gridManager.HasCurrentMap && !gridManager.RebuildMap())
            {
                return TacticalResult<IGridMap>.Failure($"Cannot declare an action because {nameof(GridManager)} could not build a current map.");
            }

            if (gridManager.CurrentMap == null)
            {
                return TacticalResult<IGridMap>.Failure($"Cannot declare an action because {nameof(GridManager)} has no current map.");
            }

            return TacticalResult<IGridMap>.Success(gridManager.CurrentMap);
        }

        private void PublishActionDeclarationEvents(ActionIntent intent, int previousAP)
        {
            if (intent.Actor.CurrentAP != previousAP)
            {
                eventBus?.PublishActionPointsChanged(intent.Actor, previousAP, intent.Actor.CurrentAP);
            }

            eventBus?.PublishActionDeclared(intent.Actor, intent);
        }

        private void LogActionDeclared(ActionIntent intent, int previousAP)
        {
            if (!logActionFlow)
            {
                return;
            }

            Debug.Log(
                $"Declared action '{intent.Ability.DisplayName}' by {DescribeUnit(intent.Actor)} targeting {intent.Target}; AP {previousAP}->{intent.Actor.CurrentAP}.",
                this);
        }

        private void ClearResolvedActionSelection()
        {
            var selectionController = inputRouter != null ? inputRouter.SelectionController : null;
            if (selectionController == null)
            {
                return;
            }

            selectionController.ClearActionMode();
        }

        private static bool IsActiveActionMode(SelectionActionMode actionMode)
        {
            return actionMode == SelectionActionMode.Move
                || actionMode == SelectionActionMode.Melee
                || actionMode == SelectionActionMode.Cone
                || actionMode == SelectionActionMode.AreaOfEffect;
        }

        private static bool IsDeclarableActionMode(SelectionActionMode actionMode)
        {
            return actionMode == SelectionActionMode.Melee
                || actionMode == SelectionActionMode.Cone
                || actionMode == SelectionActionMode.AreaOfEffect;
        }

        private static bool TryGetAbilityShapeForActionMode(SelectionActionMode actionMode, out AbilityShape shape)
        {
            switch (actionMode)
            {
                case SelectionActionMode.Melee:
                    shape = AbilityShape.Melee;
                    return true;
                case SelectionActionMode.Cone:
                    shape = AbilityShape.Cone;
                    return true;
                case SelectionActionMode.AreaOfEffect:
                    shape = AbilityShape.Radius;
                    return true;
                default:
                    shape = default;
                    return false;
            }
        }

        private static TacticalResult<ActionTarget> CreateActionTarget(
            TacticalUnit actor,
            AbilityDefinition ability,
            SelectionTarget selectedTarget)
        {
            if (actor == null)
            {
                return TacticalResult<ActionTarget>.Failure("Cannot create an action target because no acting unit was provided.");
            }

            if (ability == null)
            {
                return TacticalResult<ActionTarget>.Failure("Cannot create an action target because no ability was selected.");
            }

            ActionTarget actionTarget;
            if (ability.Shape == AbilityShape.Self)
            {
                actionTarget = ActionTarget.None;
            }
            else
            {
                var targetResult = CreateNonSelfActionTarget(selectedTarget);
                if (targetResult.IsFailure)
                {
                    return targetResult;
                }

                actionTarget = targetResult.Value;
            }

            if (ability.Shape != AbilityShape.Cone)
            {
                return TacticalResult<ActionTarget>.Success(actionTarget);
            }

            if (!actionTarget.HasTargetCell)
            {
                return TacticalResult<ActionTarget>.Failure($"{ability.DisplayName} requires a target cell to choose a cone direction.");
            }

            if (actionTarget.TargetCell == actor.CurrentGridPosition)
            {
                return TacticalResult<ActionTarget>.Failure($"{ability.DisplayName} requires a target cell away from {DescribeUnit(actor)} to choose a cone direction.");
            }

            var direction = CardinalDirectionMath.FromTo(actor.CurrentGridPosition, actionTarget.TargetCell);
            return TacticalResult<ActionTarget>.Success(actionTarget.WithDirection(direction));
        }

        private static TacticalResult<ActionTarget> CreateNonSelfActionTarget(SelectionTarget selectedTarget)
        {
            if (!selectedTarget.HasTarget)
            {
                return TacticalResult<ActionTarget>.Failure("Cannot declare an action without a confirmed target.");
            }

            switch (selectedTarget.Kind)
            {
                case SelectionTargetKind.Cell:
                    return TacticalResult<ActionTarget>.Success(ActionTarget.ForCell(selectedTarget.Cell));
                case SelectionTargetKind.Unit:
                    if (selectedTarget.Unit == null)
                    {
                        return TacticalResult<ActionTarget>.Failure("Cannot declare an action against a missing target unit.");
                    }

                    return TacticalResult<ActionTarget>.Success(ActionTarget.ForUnit(selectedTarget.Unit));
                default:
                    return TacticalResult<ActionTarget>.Failure("Cannot declare an action without a cell or unit target.");
            }
        }

        private static GridPosition[] CreateDeclaredAffectedCells(
            TacticalUnit actor,
            AbilityDefinition ability,
            ActionTarget target,
            IGridMap map)
        {
            switch (ability.Shape)
            {
                case AbilityShape.Self:
                    return new[] { target.HasTargetCell ? target.TargetCell : actor.CurrentGridPosition };
                case AbilityShape.SingleTarget:
                case AbilityShape.Melee:
                    return target.HasTargetCell ? new[] { target.TargetCell } : new GridPosition[0];
                case AbilityShape.Radius:
                    if (!target.HasTargetCell || map == null)
                    {
                        return new GridPosition[0];
                    }

                    return ToArray(AreaShapeService.GetRadiusCells(target.TargetCell, ability.Radius, map));
                default:
                    return new GridPosition[0];
            }
        }

        private static GridPosition[] ToArray(IReadOnlyList<GridPosition> cells)
        {
            if (cells == null || cells.Count == 0)
            {
                return new GridPosition[0];
            }

            var result = new GridPosition[cells.Count];
            for (var i = 0; i < cells.Count; i += 1)
            {
                result[i] = cells[i];
            }

            return result;
        }

        private static string DescribeUnit(TacticalUnit unit)
        {
            if (unit == null)
            {
                return "no unit";
            }

            return unit.IsInitialized ? $"{unit.DisplayName} {unit.UnitId}" : unit.DisplayName;
        }

        private TacticalResult EnsureRegistryHasUnits()
        {
            if (unitRegistry.RegisteredCount > 0)
            {
                return TacticalResult.Success();
            }

            var bootstrapUnits = FindBootstrapUnits();
            if (bootstrapUnits.Count == 0)
            {
                return TacticalResult.Failure(
                    "Cannot start combat because the unit registry is empty and no initialized child TacticalUnit components were found.");
            }

            foreach (var unit in bootstrapUnits)
            {
                var result = unitRegistry.Register(unit);
                if (result.IsFailure)
                {
                    return TacticalResult.Failure($"Failed to register scene unit '{unit.name}' for combat start: {result.ErrorMessage}");
                }
            }

            return TacticalResult.Success();
        }

        private IReadOnlyList<TacticalUnit> FindBootstrapUnits()
        {
            var units = new List<TacticalUnit>();
            if (unitRegistry == null)
            {
                return units;
            }

            var childUnits = unitRegistry.GetComponentsInChildren<TacticalUnit>(includeInactive: true);
            foreach (var unit in childUnits)
            {
                if (unit != null && unit.IsInitialized)
                {
                    units.Add(unit);
                }
            }

            units.Sort(TurnOrderService.CompareUnitsForTurnOrder);
            return units;
        }
    }
}
