using System.Collections.Generic;
using ReactionTactics.Core;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Turns
{
    /// <summary>
    /// Scene-level shell for the high-level combat loop. It starts combat, refreshes
    /// round AP, advances active turns, and listens for routed end-turn requests;
    /// actions, reactions, and win/loss checks are added by later tickets.
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

        private readonly CombatState currentState = new CombatState();
        private readonly TurnOrderService turnOrderService = new TurnOrderService();
        private readonly RoundLifecycleService roundLifecycleService = new RoundLifecycleService();
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
            if (actionResult.IsSuccess)
            {
                return;
            }

            ClearIllegalActiveActionSelection();
            Debug.LogWarning(
                $"{nameof(CombatManager)} rejected active action command {request.CommandType}: {actionResult.ErrorMessage}",
                this);
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

        private static bool IsActiveActionMode(SelectionActionMode actionMode)
        {
            return actionMode == SelectionActionMode.Move
                || actionMode == SelectionActionMode.Melee
                || actionMode == SelectionActionMode.Cone
                || actionMode == SelectionActionMode.AreaOfEffect;
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
