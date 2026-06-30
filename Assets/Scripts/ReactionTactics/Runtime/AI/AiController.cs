using System;
using System.Collections.Generic;
using ReactionTactics.Actions;
using ReactionTactics.Core;
using ReactionTactics.Turns;
using ReactionTactics.Units;
using UnityEngine;

namespace ReactionTactics.AI
{
    /// <summary>
    /// Deterministic shell controller for prototype enemy units. It exposes stable
    /// target selection for later action choices while current active/reaction turn
    /// behavior still passes until movement and attack AI tickets are implemented.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AiController : MonoBehaviour
    {
        public const int DefaultTargetSelectionVerticalWeight = 1;

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

        [SerializeField]
        [Min(0)]
        [Tooltip("Vertical grid weight used when choosing the nearest hostile target.")]
        private int targetSelectionVerticalWeight = DefaultTargetSelectionVerticalWeight;

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
            targetSelectionVerticalWeight = DefaultTargetSelectionVerticalWeight;
            ResolveCombatManager();
        }

        private void OnValidate()
        {
            targetSelectionVerticalWeight = Math.Max(0, targetSelectionVerticalWeight);
        }

        private CombatManager ResolveCombatManager()
        {
            if (combatManager == null)
            {
                combatManager = GetComponent<CombatManager>();
            }

            return combatManager;
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
