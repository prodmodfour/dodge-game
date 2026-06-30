using System.Collections.Generic;
using ReactionTactics.Actions;
using ReactionTactics.Commands;
using ReactionTactics.Core;
using ReactionTactics.Grid;
using ReactionTactics.Input;
using ReactionTactics.Reactions;
using ReactionTactics.Targeting;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.Turns
{
    /// <summary>
    /// Scene-level shell for the high-level combat loop. It starts combat, refreshes
    /// round AP, advances active turns, listens for routed commands, opens the
    /// explicit pass reaction windows for telegraphed actions, sends
    /// declared active actions through deterministic resolution, and enters
    /// CombatOver when one team has no living units.
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
        private readonly ReactionOrderService reactionOrderService = new ReactionOrderService();
        private readonly ReactionEligibilityService reactionEligibilityService = new ReactionEligibilityService();
        private PlayerCommandRouter subscribedInputRouter;
        private ReactionWindow currentReactionWindow;
        private readonly HashSet<TacticalUnit> deathSubscribedUnits = new HashSet<TacticalUnit>();
        private bool hasCombatEndOutcome;
        private bool combatEndHasWinningTeam;
        private TeamId combatEndWinningTeam = TeamId.Player;

        public CombatState CurrentState
        {
            get { return currentState; }
        }

        public ReactionWindow CurrentReactionWindow
        {
            get { return currentReactionWindow; }
        }

        public bool HasCombatEndOutcome
        {
            get { return currentState.IsCombatOver && hasCombatEndOutcome; }
        }

        public bool HasWinningTeam
        {
            get { return HasCombatEndOutcome && combatEndHasWinningTeam; }
        }

        public TeamId WinningTeam
        {
            get { return combatEndWinningTeam; }
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
            UnsubscribeFromRegisteredUnitDeaths();
            this.unitRegistry = unitRegistry;
            this.gridManager = gridManager;
            this.inputRouter = inputRouter;
            if (this.inputRouter != null)
            {
                this.inputRouter.CombatManager = this;
            }

            this.eventBus = eventBus;

            if (isActiveAndEnabled)
            {
                SubscribeToInputRouter();
                SubscribeToRegisteredUnitDeaths();
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

            ClearCombatEndOutcome();
            SubscribeToRegisteredUnitDeaths();

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

            return TryEnterCombatOverIfNeeded(activeUnit);
        }

        /// <summary>
        /// Validates whether a full reaction window may open for an action intent. The
        /// prototype only opens these windows from active actions; nested windows while
        /// another action is pending are rejected so reaction commands cannot recurse.
        /// Future special reactions should add an explicit opt-in rule instead of
        /// bypassing this guard.
        /// </summary>
        public TacticalResult ValidateCanOpenReactionWindow(ActionIntent intent)
        {
            if (intent == null)
            {
                return TacticalResult.Failure("Cannot open a reaction window because the source action intent is missing.");
            }

            if (!intent.TriggersReactionWindow)
            {
                return TacticalResult.Failure(
                    $"Cannot open a reaction window for '{intent.Ability.DisplayName}' because the ability does not trigger reactions.");
            }

            if (currentReactionWindow != null && currentReactionWindow.IsOpen)
            {
                return TacticalResult.Failure(
                    $"Cannot open a nested reaction window for '{intent.Ability.DisplayName}' while '{currentReactionWindow.SourceIntent.Ability.DisplayName}' already has an open reaction window. "
                    + "Reactions do not trigger full reaction windows in this prototype; future special reactions need an explicit nested-window rule.");
            }

            if (currentState.IsReactionPhase || currentState.IsResolvingAction || currentState.HasPendingActionIntent)
            {
                return TacticalResult.Failure(
                    $"Cannot open a reaction window for '{intent.Ability.DisplayName}' while another action is pending in phase {currentState.Phase}. "
                    + "Reaction commands cannot recursively start full reaction windows in this prototype.");
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
            if (currentState.IsCombatOver)
            {
                return TacticalResult.Failure("Cannot end the active turn because combat is already over.");
            }

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
            var combatEndResult = TryEnterCombatOverIfNeeded(previousActiveUnit);
            if (combatEndResult.IsFailure || currentState.IsCombatOver)
            {
                return combatEndResult;
            }

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

        /// <summary>
        /// Moves the current active unit to a reachable destination, spends movement AP,
        /// and keeps the combat loop on that unit's active turn.
        /// </summary>
        public TacticalResult MoveActiveUnit(GridPosition destination)
        {
            return MoveActiveUnit(currentState.ActiveUnit, destination);
        }

        /// <summary>
        /// Moves a specific unit only when it is the active unit in the active-turn phase.
        /// Simple active movement does not open a reaction window in this prototype.
        /// </summary>
        public TacticalResult MoveActiveUnit(TacticalUnit unit, GridPosition destination)
        {
            if (unitRegistry == null)
            {
                return TacticalResult.Failure($"Cannot active move because {nameof(UnitRegistry)} is missing.");
            }

            var mapResult = ResolveCurrentMapForActiveMove();
            if (mapResult.IsFailure)
            {
                return TacticalResult.Failure(mapResult.ErrorMessage);
            }

            var commandResult = ActiveMoveCommand.TryCreate(
                unit,
                destination,
                mapResult.Value,
                unitRegistry,
                currentState);
            if (commandResult.IsFailure)
            {
                return TacticalResult.Failure(commandResult.ErrorMessage);
            }

            var command = commandResult.Value;
            if (!unitRegistry.TryGetLivingUnit(command.Unit.UnitId, out var registeredUnit)
                || !ReferenceEquals(registeredUnit, command.Unit))
            {
                return TacticalResult.Failure($"{DescribeUnit(command.Unit)} cannot active move because it is not the registered living unit for {command.Unit.UnitId}.");
            }

            var previousAP = command.Unit.CurrentAP;
            var previousPosition = command.Unit.CurrentGridPosition;
            var executeResult = command.Execute();
            if (executeResult.IsFailure)
            {
                return executeResult;
            }

            if (command.Unit.CurrentAP != previousAP)
            {
                eventBus?.PublishActionPointsChanged(command.Unit, previousAP, command.Unit.CurrentAP);
            }

            currentState.SetState(currentState.CurrentRound, CombatPhase.ActiveTurn, command.Unit, null, null);
            PresentActiveMove(command);
            LogActiveMove(command, previousPosition, previousAP);
            return TacticalResult.Success();
        }

        /// <summary>
        /// Passes the currently active reaction turn without spending AP. This is the
        /// Space-key path: combat state already identifies which unit is reacting.
        /// </summary>
        public TacticalResult PassCurrentReaction()
        {
            return PassCurrentReaction(currentState.ReactingUnit);
        }

        /// <summary>
        /// Passes a specific unit's current reaction turn, then advances the reaction
        /// window or resolves the pending action when all reactors are complete.
        /// </summary>
        public TacticalResult PassCurrentReaction(TacticalUnit reactor)
        {
            var sourceIntent = currentReactionWindow != null
                ? currentReactionWindow.SourceIntent
                : currentState.PendingActionIntent as ActionIntent;
            var commandResult = PassReactionCommand.TryCreate(
                reactor,
                sourceIntent,
                currentState,
                currentReactionWindow);
            if (commandResult.IsFailure)
            {
                return TacticalResult.Failure(commandResult.ErrorMessage);
            }

            var command = commandResult.Value;
            currentReactionWindow.CompleteCurrentReactor();
            LogReactionPass(command);
            currentState.SetState(currentState.CurrentRound, CombatPhase.ReactionWindow, sourceIntent.Actor, null, sourceIntent);
            return AdvanceReactionWindowOrResolve(sourceIntent);
        }

        /// <summary>
        /// Braces the current reactor, spending AP for one-shot fixed damage reduction,
        /// then advances the reaction window or resolves the pending action.
        /// </summary>
        public TacticalResult BraceCurrentReaction()
        {
            return BraceCurrentReaction(currentState.ReactingUnit);
        }

        /// <summary>
        /// Braces a specific unit only when it is the current reactor in the open
        /// reaction window. Brace is cleared when it reduces damage or the pending
        /// action finishes resolving.
        /// </summary>
        public TacticalResult BraceCurrentReaction(TacticalUnit reactor)
        {
            if (unitRegistry == null)
            {
                return TacticalResult.Failure($"Cannot brace because {nameof(UnitRegistry)} is missing.");
            }

            var sourceIntent = currentReactionWindow != null
                ? currentReactionWindow.SourceIntent
                : currentState.PendingActionIntent as ActionIntent;
            var commandResult = BraceReactionCommand.TryCreate(
                reactor,
                sourceIntent,
                currentState,
                currentReactionWindow);
            if (commandResult.IsFailure)
            {
                return TacticalResult.Failure(commandResult.ErrorMessage);
            }

            var command = commandResult.Value;
            if (!unitRegistry.TryGetLivingUnit(command.Reactor.UnitId, out var registeredReactor)
                || !ReferenceEquals(registeredReactor, command.Reactor))
            {
                return TacticalResult.Failure($"{DescribeUnit(command.Reactor)} cannot brace because it is not the registered living unit for {command.Reactor.UnitId}.");
            }

            var previousAP = command.Reactor.CurrentAP;
            var executeResult = command.Execute();
            if (executeResult.IsFailure)
            {
                return executeResult;
            }

            if (command.Reactor.CurrentAP != previousAP)
            {
                eventBus?.PublishActionPointsChanged(command.Reactor, previousAP, command.Reactor.CurrentAP);
            }

            currentReactionWindow.CompleteCurrentReactor();
            LogReactionBrace(command, previousAP);
            currentState.SetState(currentState.CurrentRound, CombatPhase.ReactionWindow, sourceIntent.Actor, null, sourceIntent);
            return AdvanceReactionWindowOrResolve(sourceIntent);
        }

        /// <summary>
        /// Moves the current reactor to a reachable destination, spends movement AP,
        /// completes that reactor's turn, and advances or closes the reaction window.
        /// </summary>
        public TacticalResult MoveCurrentReaction(GridPosition destination)
        {
            return MoveCurrentReaction(currentState.ReactingUnit, destination);
        }

        /// <summary>
        /// Moves a specific unit only when it is the current reactor in the open
        /// reaction window. Occupancy updates through the unit's authoritative grid position.
        /// </summary>
        public TacticalResult MoveCurrentReaction(TacticalUnit reactor, GridPosition destination)
        {
            if (unitRegistry == null)
            {
                return TacticalResult.Failure($"Cannot reaction move because {nameof(UnitRegistry)} is missing.");
            }

            var mapResult = ResolveCurrentMapForReactionMove();
            if (mapResult.IsFailure)
            {
                return TacticalResult.Failure(mapResult.ErrorMessage);
            }

            var commandResult = ReactionMoveCommand.TryCreate(
                reactor,
                destination,
                mapResult.Value,
                unitRegistry,
                currentState,
                currentReactionWindow);
            if (commandResult.IsFailure)
            {
                return TacticalResult.Failure(commandResult.ErrorMessage);
            }

            var command = commandResult.Value;
            if (!unitRegistry.TryGetLivingUnit(command.Reactor.UnitId, out var registeredReactor)
                || !ReferenceEquals(registeredReactor, command.Reactor))
            {
                return TacticalResult.Failure($"{DescribeUnit(command.Reactor)} cannot reaction move because it is not the registered living unit for {command.Reactor.UnitId}.");
            }

            var sourceIntent = command.SourceIntent;
            var previousAP = command.Reactor.CurrentAP;
            var previousPosition = command.Reactor.CurrentGridPosition;
            var executeResult = command.Execute();
            if (executeResult.IsFailure)
            {
                return executeResult;
            }

            if (command.Reactor.CurrentAP != previousAP)
            {
                eventBus?.PublishActionPointsChanged(command.Reactor, previousAP, command.Reactor.CurrentAP);
            }

            currentReactionWindow.CompleteCurrentReactor();
            LogReactionMove(command, previousPosition, previousAP);
            currentState.SetState(currentState.CurrentRound, CombatPhase.ReactionWindow, sourceIntent.Actor, null, sourceIntent);
            return AdvanceReactionWindowOrResolve(sourceIntent);
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
            UnsubscribeFromRegisteredUnitDeaths();
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
            ClearCombatEndOutcome();
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

            if (inputRouter != null && inputRouter.CombatManager == null)
            {
                inputRouter.CombatManager = this;
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
            var combatEndResult = TryEnterCombatOverIfNeeded(previousActiveUnit);
            if (combatEndResult.IsFailure || currentState.IsCombatOver)
            {
                return combatEndResult;
            }

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
            if (currentState.IsCombatOver)
            {
                if (request.CommandType == PlayerCommandType.Cancel)
                {
                    ClearCombatOverSelection();
                    return;
                }

                Debug.LogWarning($"{nameof(CombatManager)} ignored {request.CommandType} because combat is over.", this);
                return;
            }

            if (request.CommandType == PlayerCommandType.EndTurn)
            {
                if (currentState.IsReactionPhase)
                {
                    var passResult = PassCurrentReaction();
                    if (passResult.IsFailure)
                    {
                        Debug.LogWarning($"{nameof(CombatManager)} could not pass reaction: {passResult.ErrorMessage}", this);
                    }

                    return;
                }

                var endTurnResult = EndActiveTurn();
                if (endTurnResult.IsFailure)
                {
                    Debug.LogWarning($"{nameof(CombatManager)} could not end turn: {endTurnResult.ErrorMessage}", this);
                }

                return;
            }

            if (request.CommandType == PlayerCommandType.PassReaction)
            {
                var passResult = PassCurrentReaction();
                if (passResult.IsFailure)
                {
                    Debug.LogWarning($"{nameof(CombatManager)} could not pass reaction: {passResult.ErrorMessage}", this);
                }

                return;
            }

            if (request.CommandType == PlayerCommandType.SelectReaction
                && request.ActionMode == SelectionActionMode.Move)
            {
                if (!request.HasTarget)
                {
                    return;
                }

                if (request.Target.Kind != SelectionTargetKind.Cell)
                {
                    Debug.LogWarning($"{nameof(CombatManager)} could not reaction move: reaction movement requires a target cell.", this);
                    return;
                }

                var moveResult = MoveCurrentReaction(request.Unit ?? currentState.ReactingUnit, request.Target.Cell);
                if (moveResult.IsFailure)
                {
                    Debug.LogWarning($"{nameof(CombatManager)} could not reaction move: {moveResult.ErrorMessage}", this);
                }

                return;
            }

            if (request.CommandType == PlayerCommandType.SelectReaction
                && request.ActionMode == SelectionActionMode.Brace)
            {
                var braceResult = BraceCurrentReaction(request.Unit ?? currentState.ReactingUnit);
                if (braceResult.IsFailure)
                {
                    Debug.LogWarning($"{nameof(CombatManager)} could not brace reaction: {braceResult.ErrorMessage}", this);
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

            if (request.CommandType == PlayerCommandType.ConfirmTarget
                && request.ActionMode == SelectionActionMode.Move)
            {
                if (!request.HasTarget || request.Target.Kind != SelectionTargetKind.Cell)
                {
                    Debug.LogWarning($"{nameof(CombatManager)} could not active move: active movement requires a target cell.", this);
                    return;
                }

                var moveResult = MoveActiveUnit(request.Unit, request.Target.Cell);
                if (moveResult.IsFailure)
                {
                    Debug.LogWarning($"{nameof(CombatManager)} could not active move: {moveResult.ErrorMessage}", this);
                    return;
                }

                ClearCompletedActiveMoveSelection(request.Unit);
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
        /// stores the intent while reactions and resolution are pending, starts the
        /// explicit reaction window when the ability triggers reactions, and resolves
        /// the action after all reactors pass or complete.
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
            currentReactionWindow = null;

            if (intent.TriggersReactionWindow)
            {
                var openWindowResult = OpenReactionWindow(intent);
                if (openWindowResult.IsFailure)
                {
                    return openWindowResult;
                }
            }
            else
            {
                currentState.SetState(currentState.CurrentRound, CombatPhase.ResolvingAction, request.Unit, null, intent);
            }

            PublishActionDeclarationEvents(intent, previousAP);
            LogActionDeclared(intent, previousAP);

            return intent.TriggersReactionWindow
                ? AdvanceReactionWindowOrResolve(intent)
                : ResolveDeclaredAction(intent);
        }

        private TacticalResult OpenReactionWindow(ActionIntent intent)
        {
            var nestingGuardResult = ValidateCanOpenReactionWindow(intent);
            if (nestingGuardResult.IsFailure)
            {
                return nestingGuardResult;
            }

            if (unitRegistry == null)
            {
                return TacticalResult.Failure($"Cannot open a reaction window because {nameof(UnitRegistry)} is missing.");
            }

            var orderedReactors = reactionOrderService.BuildReactionOrder(intent, unitRegistry.GetLivingUnits());
            currentReactionWindow = new ReactionWindow(intent, orderedReactors);
            currentReactionWindow.Open();
            currentState.SetState(currentState.CurrentRound, CombatPhase.ReactionWindow, intent.Actor, null, intent);
            LogReactionWindowOpened(intent, orderedReactors);
            return TacticalResult.Success();
        }

        private TacticalResult AdvanceReactionWindowOrResolve(ActionIntent intent)
        {
            if (intent == null)
            {
                return TacticalResult.Failure("Cannot process a reaction window because the source action intent is missing.");
            }

            if (currentReactionWindow == null)
            {
                return TacticalResult.Failure("Cannot process a reaction window because no reaction window is open.");
            }

            if (!ReferenceEquals(currentReactionWindow.SourceIntent, intent))
            {
                return TacticalResult.Failure("Cannot process a reaction window for a different source action intent.");
            }

            while (currentReactionWindow.TryStartNextReactor(out var reactor))
            {
                currentState.SetState(currentState.CurrentRound, CombatPhase.ReactionWindow, intent.Actor, reactor, intent);
                eventBus?.PublishReactionTurnStarted(reactor, intent);

                var eligibility = EvaluateCurrentReactionEligibility(reactor, intent);
                if (!eligibility.IsEligible || eligibility.ShouldAutoPass)
                {
                    LogReactionAutoPass(intent, reactor, eligibility);
                    currentReactionWindow.SkipCurrentReactor();
                    currentState.SetState(currentState.CurrentRound, CombatPhase.ReactionWindow, intent.Actor, null, intent);
                    continue;
                }

                FocusInputOnCurrentReactor(reactor);
                LogReactionAwaitingPass(intent, reactor, eligibility);
                return TacticalResult.Success();
            }

            currentReactionWindow.Close();
            LogReactionWindowClosed(intent, currentReactionWindow);
            return ResolveDeclaredAction(intent);
        }

        private ReactionEligibilityResult EvaluateCurrentReactionEligibility(TacticalUnit reactor, ActionIntent intent)
        {
            var mapResult = ResolveCurrentMapForReactionAvailability();
            return reactionEligibilityService.CanUnitReact(
                reactor,
                intent,
                currentState,
                currentReactionWindow,
                mapResult.IsSuccess ? mapResult.Value : null,
                unitRegistry);
        }

        private TacticalResult<IGridMap> ResolveCurrentMapForReactionAvailability()
        {
            if (gridManager == null)
            {
                return TacticalResult<IGridMap>.Failure($"Cannot inspect reaction movement because {nameof(GridManager)} is missing.");
            }

            if (!gridManager.HasCurrentMap && !gridManager.RebuildMap())
            {
                return TacticalResult<IGridMap>.Failure($"Cannot inspect reaction movement because {nameof(GridManager)} could not build a current map.");
            }

            if (gridManager.CurrentMap == null)
            {
                return TacticalResult<IGridMap>.Failure($"Cannot inspect reaction movement because {nameof(GridManager)} has no current map.");
            }

            return TacticalResult<IGridMap>.Success(gridManager.CurrentMap);
        }

        private TacticalResult ResolveDeclaredAction(ActionIntent intent)
        {
            if (intent == null)
            {
                return TacticalResult.Failure("Cannot resolve an action because no action intent was provided.");
            }

            currentState.SetState(currentState.CurrentRound, CombatPhase.ResolvingAction, intent.Actor, null, intent);
            var reactionWindowToClear = currentReactionWindow;
            var resolver = new ActionResolver(eventBus, this, logActionFlow, () => unitRegistry.GetLivingUnits());
            var resolveResult = resolver.Resolve(intent);
            if (resolveResult.IsFailure)
            {
                currentReactionWindow = null;
                return resolveResult;
            }

            ClearBraceStatesAfterPendingAction(reactionWindowToClear, intent);
            currentReactionWindow = null;
            var combatEndResult = TryEnterCombatOverIfNeeded(intent.Actor);
            if (combatEndResult.IsFailure || currentState.IsCombatOver)
            {
                return combatEndResult;
            }

            currentState.SetState(currentState.CurrentRound, CombatPhase.ActiveTurn, intent.Actor, null, null);
            ClearResolvedActionSelection();
            return TacticalResult.Success();
        }

        private void FocusInputOnCurrentReactor(TacticalUnit reactor)
        {
            if (inputRouter == null || reactor == null)
            {
                return;
            }

            inputRouter.CombatManager = this;
            var focusResult = inputRouter.FocusCurrentReactorSelection();
            if (focusResult.IsFailure && logActionFlow)
            {
                Debug.LogWarning(
                    $"{nameof(CombatManager)} could not focus current reactor {DescribeUnit(reactor)} for input: {focusResult.ErrorMessage}",
                    this);
            }
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
            subscribedInputRouter.CombatManager = this;
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

        private void SubscribeToRegisteredUnitDeaths()
        {
            if (unitRegistry == null)
            {
                return;
            }

            var registeredUnits = unitRegistry.GetRegisteredUnits();
            for (var i = 0; i < registeredUnits.Count; i += 1)
            {
                var unit = registeredUnits[i];
                if (unit == null || deathSubscribedUnits.Contains(unit))
                {
                    continue;
                }

                unit.Died += HandleRegisteredUnitDied;
                deathSubscribedUnits.Add(unit);
            }
        }

        private void UnsubscribeFromRegisteredUnitDeaths()
        {
            foreach (var unit in deathSubscribedUnits)
            {
                if (unit != null)
                {
                    unit.Died -= HandleRegisteredUnitDied;
                }
            }

            deathSubscribedUnits.Clear();
        }

        private void HandleRegisteredUnitDied(TacticalUnit unit, DamageSource source)
        {
            if (currentState.Phase == CombatPhase.NotStarted || currentState.IsCombatOver)
            {
                return;
            }

            if (currentState.IsResolvingAction)
            {
                return;
            }

            var result = TryEnterCombatOverIfNeeded(currentState.ActiveUnit);
            if (result.IsFailure && logCombatStart)
            {
                Debug.LogWarning($"{nameof(CombatManager)} could not evaluate combat end after {DescribeUnit(unit)} died: {result.ErrorMessage}", this);
            }
        }

        private TacticalResult TryEnterCombatOverIfNeeded(TacticalUnit previousActiveUnit)
        {
            if (currentState.IsCombatOver)
            {
                return TacticalResult.Success();
            }

            if (unitRegistry == null)
            {
                return TacticalResult.Failure($"Cannot check combat end because {nameof(UnitRegistry)} is missing.");
            }

            SubscribeToRegisteredUnitDeaths();

            if (!TryGetCombatEndOutcome(out var hasWinningTeam, out var winningTeam))
            {
                return TacticalResult.Success();
            }

            EnterCombatOver(hasWinningTeam, winningTeam, previousActiveUnit);
            return TacticalResult.Success();
        }

        private bool TryGetCombatEndOutcome(out bool hasWinningTeam, out TeamId winningTeam)
        {
            var registeredUnits = unitRegistry.GetRegisteredUnits();
            var playerTeamPresent = false;
            var enemyTeamPresent = false;
            var playerAlive = false;
            var enemyAlive = false;

            for (var i = 0; i < registeredUnits.Count; i += 1)
            {
                var unit = registeredUnits[i];
                if (unit == null)
                {
                    continue;
                }

                if (unit.Team == TeamId.Player)
                {
                    playerTeamPresent = true;
                    playerAlive |= unit.IsAlive;
                }
                else if (unit.Team == TeamId.Enemy)
                {
                    enemyTeamPresent = true;
                    enemyAlive |= unit.IsAlive;
                }
            }

            if (!playerTeamPresent || !enemyTeamPresent)
            {
                hasWinningTeam = false;
                winningTeam = TeamId.Player;
                return false;
            }

            if (playerAlive && enemyAlive)
            {
                hasWinningTeam = false;
                winningTeam = TeamId.Player;
                return false;
            }

            if (playerAlive)
            {
                hasWinningTeam = true;
                winningTeam = TeamId.Player;
                return true;
            }

            if (enemyAlive)
            {
                hasWinningTeam = true;
                winningTeam = TeamId.Enemy;
                return true;
            }

            hasWinningTeam = false;
            winningTeam = TeamId.Player;
            return true;
        }

        private void EnterCombatOver(bool hasWinningTeam, TeamId winningTeam, TacticalUnit previousActiveUnit)
        {
            if (currentReactionWindow != null && currentReactionWindow.IsOpen)
            {
                currentReactionWindow.Close();
            }

            currentReactionWindow = null;
            hasCombatEndOutcome = true;
            combatEndHasWinningTeam = hasWinningTeam;
            combatEndWinningTeam = winningTeam;

            var previousActive = previousActiveUnit != null ? previousActiveUnit : currentState.ActiveUnit;
            currentState.SetState(currentState.CurrentRound, CombatPhase.CombatOver, null, null, null);
            if (previousActive != null)
            {
                eventBus?.PublishActiveUnitChanged(previousActive, null);
            }

            if (hasWinningTeam)
            {
                eventBus?.PublishCombatEnded(winningTeam);
            }
            else
            {
                eventBus?.PublishCombatEndedWithoutWinner();
            }

            LogCombatEnded(hasWinningTeam, winningTeam);
            ClearCombatOverSelection();
        }

        private void LogCombatEnded(bool hasWinningTeam, TeamId winningTeam)
        {
            var message = hasWinningTeam
                ? $"{winningTeam} team won combat. CombatOver phase entered; further actions and reactions are disabled."
                : "Combat ended with no winning team. CombatOver phase entered; further actions and reactions are disabled.";
            eventBus?.PublishCombatLog(message);

            if (!logCombatStart)
            {
                return;
            }

            Debug.Log($"[Combat Log] {message}", this);
        }

        private void ClearCombatEndOutcome()
        {
            hasCombatEndOutcome = false;
            combatEndHasWinningTeam = false;
            combatEndWinningTeam = TeamId.Player;
        }

        private void ClearCombatOverSelection()
        {
            var selectionController = inputRouter != null ? inputRouter.SelectionController : null;
            if (selectionController != null)
            {
                selectionController.ClearForPhaseChange();
            }
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

        private TacticalResult<IGridMap> ResolveCurrentMapForReactionMove()
        {
            if (gridManager == null)
            {
                return TacticalResult<IGridMap>.Failure($"Cannot reaction move because {nameof(GridManager)} is missing.");
            }

            if (!gridManager.HasCurrentMap && !gridManager.RebuildMap())
            {
                return TacticalResult<IGridMap>.Failure($"Cannot reaction move because {nameof(GridManager)} could not build a current map.");
            }

            if (gridManager.CurrentMap == null)
            {
                return TacticalResult<IGridMap>.Failure($"Cannot reaction move because {nameof(GridManager)} has no current map.");
            }

            return TacticalResult<IGridMap>.Success(gridManager.CurrentMap);
        }

        private TacticalResult<IGridMap> ResolveCurrentMapForActiveMove()
        {
            if (gridManager == null)
            {
                return TacticalResult<IGridMap>.Failure($"Cannot active move because {nameof(GridManager)} is missing.");
            }

            if (!gridManager.HasCurrentMap && !gridManager.RebuildMap())
            {
                return TacticalResult<IGridMap>.Failure($"Cannot active move because {nameof(GridManager)} could not build a current map.");
            }

            if (gridManager.CurrentMap == null)
            {
                return TacticalResult<IGridMap>.Failure($"Cannot active move because {nameof(GridManager)} has no current map.");
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
            eventBus?.PublishCombatLog(
                $"{DescribeUnit(intent.Actor)} declared {intent.Ability.DisplayName} targeting {intent.Target}; AP {previousAP}->{intent.Actor.CurrentAP}. Reactions resolve before telegraphed actions hit.");

            if (!logActionFlow)
            {
                return;
            }

            Debug.Log(
                $"Declared action '{intent.Ability.DisplayName}' by {DescribeUnit(intent.Actor)} targeting {intent.Target}; AP {previousAP}->{intent.Actor.CurrentAP}.",
                this);
        }

        private void LogReactionWindowOpened(ActionIntent intent, IReadOnlyList<TacticalUnit> orderedReactors)
        {
            var reactionOrder = DescribeReactionOrder(intent, orderedReactors);
            eventBus?.PublishCombatLog(
                $"Reaction order for {intent.Ability.DisplayName}: {reactionOrder}. Units react by distance from {DescribeUnit(intent.Actor)}.");

            if (!logActionFlow)
            {
                return;
            }

            Debug.Log(
                $"[Combat Log] Reaction window opened for '{intent.Ability.DisplayName}' by {DescribeUnit(intent.Actor)}. Order: {reactionOrder}.",
                this);
        }

        private void LogReactionAutoPass(
            ActionIntent intent,
            TacticalUnit reactor,
            ReactionEligibilityResult eligibility)
        {
            var reason = string.IsNullOrEmpty(eligibility.Reason)
                ? "auto-passed by reaction eligibility rules"
                : eligibility.Reason;
            eventBus?.PublishCombatLog(
                $"{DescribeUnit(reactor)} auto-passed reaction to {intent.Ability.DisplayName}: {reason}.");

            if (!logActionFlow)
            {
                return;
            }

            Debug.Log(
                $"[Combat Log] Reaction auto-pass: {DescribeUnit(reactor)} responding to '{intent.Ability.DisplayName}' from {DescribeUnit(intent.Actor)} — {reason}.",
                this);
        }

        private void LogReactionAwaitingPass(
            ActionIntent intent,
            TacticalUnit reactor,
            ReactionEligibilityResult eligibility)
        {
            var reason = string.IsNullOrEmpty(eligibility.Reason)
                ? "waiting for an explicit reaction command"
                : eligibility.Reason;
            eventBus?.PublishCombatLog(
                $"{DescribeUnit(reactor)} can react to {intent.Ability.DisplayName} from {DescribeUnit(intent.Actor)}: {reason}. Pass costs 0 AP.");

            if (!logActionFlow)
            {
                return;
            }

            Debug.Log(
                $"[Combat Log] Reaction turn started: {DescribeUnit(reactor)} responding to '{intent.Ability.DisplayName}' from {DescribeUnit(intent.Actor)} — {reason}. Pass is available for 0 AP.",
                this);
        }

        private void LogReactionPass(PassReactionCommand command)
        {
            eventBus?.PublishCombatLog(
                $"{DescribeUnit(command.Reactor)} passed reaction to {command.SourceIntent.Ability.DisplayName} for {command.Cost} AP.");

            if (!logActionFlow)
            {
                return;
            }

            Debug.Log(
                $"[Combat Log] {DescribeUnit(command.Reactor)} passed reaction to '{command.SourceIntent.Ability.DisplayName}' for {command.Cost} AP.",
                this);
        }

        private void PresentActiveMove(ActiveMoveCommand command)
        {
            if (command.Unit == null || gridManager == null)
            {
                return;
            }

            var metrics = gridManager.Metrics;
            var mover = command.Unit.GetComponent<GridPathMover>();
            if (mover != null)
            {
                mover.Metrics = metrics;
                if (Application.isPlaying && mover.isActiveAndEnabled && mover.gameObject.activeInHierarchy)
                {
                    if (mover.IsMoving)
                    {
                        mover.StopMovement();
                    }

                    mover.MoveAlongPath(command.Path, metrics);
                }
                else
                {
                    mover.SnapTo(command.Destination, metrics);
                }

                return;
            }

            command.Unit.transform.position = metrics.GridToWorldCenter(command.Destination);
        }

        private void LogActiveMove(ActiveMoveCommand command, GridPosition previousPosition, int previousAP)
        {
            eventBus?.PublishCombatLog(
                $"{DescribeUnit(command.Unit)} moved on its active turn from {previousPosition} to {command.Destination}; AP {previousAP}->{command.Unit.CurrentAP}.");

            if (!logActionFlow)
            {
                return;
            }

            Debug.Log(
                $"[Combat Log] {DescribeUnit(command.Unit)} active-moved from {previousPosition} to {command.Destination}; AP {previousAP}->{command.Unit.CurrentAP}.",
                this);
        }

        private void LogReactionMove(ReactionMoveCommand command, GridPosition previousPosition, int previousAP)
        {
            eventBus?.PublishCombatLog(
                $"{DescribeUnit(command.Reactor)} reaction-moved from {previousPosition} to {command.Destination} in response to {command.SourceIntent.Ability.DisplayName}; AP {previousAP}->{command.Reactor.CurrentAP}.");

            if (!logActionFlow)
            {
                return;
            }

            Debug.Log(
                $"[Combat Log] {DescribeUnit(command.Reactor)} reaction-moved from {previousPosition} to {command.Destination} responding to '{command.SourceIntent.Ability.DisplayName}'; AP {previousAP}->{command.Reactor.CurrentAP}.",
                this);
        }

        private void LogReactionBrace(BraceReactionCommand command, int previousAP)
        {
            eventBus?.PublishCombatLog(
                $"{DescribeUnit(command.Reactor)} braced against {command.SourceIntent.Ability.DisplayName} for {command.Cost} AP; next incoming damage is reduced by {command.DamageReduction}. AP {previousAP}->{command.Reactor.CurrentAP}.");

            if (!logActionFlow)
            {
                return;
            }

            Debug.Log(
                $"[Combat Log] {DescribeUnit(command.Reactor)} braced against '{command.SourceIntent.Ability.DisplayName}' for {command.Cost} AP; next incoming damage is reduced by {command.DamageReduction}. AP {previousAP}->{command.Reactor.CurrentAP}.",
                this);
        }

        private void LogReactionWindowClosed(ActionIntent intent, ReactionWindow window)
        {
            eventBus?.PublishCombatLog(
                $"All reactions for {intent.Ability.DisplayName} are complete ({window.ProcessedReactorCount}/{window.ReactorCount}). Resolving the original action now.");

            if (!logActionFlow)
            {
                return;
            }

            Debug.Log(
                $"[Combat Log] Reaction window closed for '{intent.Ability.DisplayName}' after {window.ProcessedReactorCount}/{window.ReactorCount} reactors completed or skipped. Resolving original action next.",
                this);
        }

        private void ClearBraceStatesAfterPendingAction(ReactionWindow reactionWindow, ActionIntent intent)
        {
            if (reactionWindow == null)
            {
                return;
            }

            var reactors = reactionWindow.OrderedReactors;
            for (var i = 0; i < reactors.Count; i += 1)
            {
                var reactor = reactors[i];
                if (reactor == null || !reactor.BracedUntilNextHit)
                {
                    continue;
                }

                reactor.ClearBrace();
                LogBraceExpiredAfterPendingAction(reactor, intent);
            }
        }

        private void LogBraceExpiredAfterPendingAction(TacticalUnit reactor, ActionIntent intent)
        {
            eventBus?.PublishCombatLog(
                $"{DescribeUnit(reactor)}'s brace expired after {intent.Ability.DisplayName} resolved without consuming it.");

            if (!logActionFlow)
            {
                return;
            }

            Debug.Log(
                $"[Combat Log] {DescribeUnit(reactor)}'s brace expired after '{intent.Ability.DisplayName}' resolved without consuming it.",
                this);
        }

        private string DescribeReactionOrder(ActionIntent intent, IReadOnlyList<TacticalUnit> orderedReactors)
        {
            if (orderedReactors == null || orderedReactors.Count == 0)
            {
                return "none";
            }

            var entries = new List<string>();
            for (var i = 0; i < orderedReactors.Count; i += 1)
            {
                var reactor = orderedReactors[i];
                entries.Add($"{i + 1}:{DescribeUnit(reactor)} d={reactionOrderService.GetReactionDistance(reactor, intent)}");
            }

            return string.Join(" -> ", entries);
        }

        private void ClearResolvedActionSelection()
        {
            var selectionController = inputRouter != null ? inputRouter.SelectionController : null;
            if (selectionController == null)
            {
                return;
            }

            selectionController.ClearActionMode();
            if (currentState.ActiveUnit != null && currentState.ActiveUnit.IsAlive)
            {
                selectionController.SelectUnit(currentState.ActiveUnit);
            }
        }

        private void ClearCompletedActiveMoveSelection(TacticalUnit movedUnit)
        {
            var selectionController = inputRouter != null ? inputRouter.SelectionController : null;
            if (selectionController == null)
            {
                return;
            }

            if (selectionController.SelectedActionMode == SelectionActionMode.Move)
            {
                selectionController.ClearActionMode();
            }

            if (movedUnit != null && movedUnit.IsAlive)
            {
                selectionController.SelectUnit(movedUnit);
            }
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
                case AbilityShape.Cone:
                    if (!target.HasTargetCell || map == null)
                    {
                        return new GridPosition[0];
                    }

                    var direction = target.HasDirection
                        ? target.Direction
                        : CardinalDirectionMath.FromTo(actor.CurrentGridPosition, target.TargetCell);
                    return ToArray(AreaShapeService.GetConeCells(actor.CurrentGridPosition, direction, ability.Range, map));
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
