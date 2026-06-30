using ReactionTactics.Actions;
using ReactionTactics.Core;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.AI
{
    /// <summary>
    /// Deterministic shell controller for prototype enemy units. The initial AI
    /// behavior intentionally only passes: enemy active turns end immediately and
    /// enemy reaction turns use Pass, leaving richer targeting and movement choices
    /// for later AI tickets.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AiController : MonoBehaviour
    {
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
        [Tooltip("Write concise debug logs for AI pass decisions.")]
        private bool logDecisions = true;

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
        /// Initial active-turn AI behavior: pass by ending the controlled unit's turn.
        /// </summary>
        public TacticalResult TakeActiveTurn()
        {
            return TakeActiveTurn(ResolveCombatManager());
        }

        /// <summary>
        /// Initial active-turn AI behavior: pass by ending the controlled unit's turn.
        /// </summary>
        public TacticalResult TakeActiveTurn(CombatManager manager)
        {
            var validation = ValidateActiveTurn(manager);
            if (validation.IsFailure)
            {
                return validation;
            }

            var activeUnit = manager.CurrentState.ActiveUnit;
            LogDecision($"AI controlling {activeUnit.DisplayName} {activeUnit.UnitId} [{activeUnit.Team}] passed its active turn.");
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

        private void Awake()
        {
            ResolveCombatManager();
        }

        private void Reset()
        {
            controlledTeam = TeamId.Enemy;
            automaticDelegationEnabled = true;
            logDecisions = true;
            ResolveCombatManager();
        }

        private CombatManager ResolveCombatManager()
        {
            if (combatManager == null)
            {
                combatManager = GetComponent<CombatManager>();
            }

            return combatManager;
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
