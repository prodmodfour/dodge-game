using System;
using ReactionTactics.Units;

namespace ReactionTactics.Turns
{
    /// <summary>
    /// Mutable runtime state for the current combat loop position. This is intentionally
    /// a small data holder: turn-order, action declaration, reactions, and resolution are
    /// implemented by later systems that read and update this state.
    /// </summary>
    public sealed class CombatState
    {
        public CombatState()
            : this(0, CombatPhase.NotStarted, null, null, null)
        {
        }

        public CombatState(
            int currentRound,
            CombatPhase phase,
            TacticalUnit activeUnit = null,
            TacticalUnit reactingUnit = null,
            object pendingActionIntent = null)
        {
            SetState(currentRound, phase, activeUnit, reactingUnit, pendingActionIntent);
        }

        public int CurrentRound { get; private set; }

        public CombatPhase Phase { get; private set; }

        public TacticalUnit ActiveUnit { get; private set; }

        public TacticalUnit ReactingUnit { get; private set; }

        /// <summary>
        /// Holds the declared action intent while reactions and resolution are pending.
        /// The concrete ActionIntent type is introduced by the ability/action tickets.
        /// </summary>
        public object PendingActionIntent { get; private set; }

        public bool HasActiveUnit
        {
            get { return ActiveUnit != null; }
        }

        public bool HasReactingUnit
        {
            get { return ReactingUnit != null; }
        }

        public bool HasPendingActionIntent
        {
            get { return PendingActionIntent != null; }
        }

        public bool IsActiveUnitPhase
        {
            get { return Phase == CombatPhase.ActiveTurn || Phase == CombatPhase.ActionTargeting; }
        }

        public bool IsReactionPhase
        {
            get { return Phase == CombatPhase.ReactionWindow; }
        }

        public bool IsResolvingAction
        {
            get { return Phase == CombatPhase.ResolvingAction; }
        }

        public bool IsCombatOver
        {
            get { return Phase == CombatPhase.CombatOver; }
        }

        public void SetState(
            int currentRound,
            CombatPhase phase,
            TacticalUnit activeUnit = null,
            TacticalUnit reactingUnit = null,
            object pendingActionIntent = null)
        {
            ValidateCurrentRound(currentRound);
            ValidatePhase(phase);

            CurrentRound = currentRound;
            Phase = phase;
            ActiveUnit = activeUnit;
            ReactingUnit = reactingUnit;
            PendingActionIntent = pendingActionIntent;
        }

        public void SetCurrentRound(int currentRound)
        {
            ValidateCurrentRound(currentRound);
            CurrentRound = currentRound;
        }

        public void SetPhase(CombatPhase phase)
        {
            ValidatePhase(phase);
            Phase = phase;
        }

        public void SetActiveUnit(TacticalUnit activeUnit)
        {
            ActiveUnit = activeUnit;
        }

        public void SetReactingUnit(TacticalUnit reactingUnit)
        {
            ReactingUnit = reactingUnit;
        }

        public void SetPendingActionIntent(object pendingActionIntent)
        {
            PendingActionIntent = pendingActionIntent;
        }

        public void ClearPendingActionIntent()
        {
            PendingActionIntent = null;
        }

        public void ClearReactingUnit()
        {
            ReactingUnit = null;
        }

        public void Reset()
        {
            SetState(0, CombatPhase.NotStarted, null, null, null);
        }

        public override string ToString()
        {
            var activeLabel = ActiveUnit != null ? ActiveUnit.DisplayName : "none";
            var reactingLabel = ReactingUnit != null ? ReactingUnit.DisplayName : "none";
            var intentLabel = PendingActionIntent != null ? PendingActionIntent.ToString() : "none";
            return $"Round {CurrentRound} {Phase} active={activeLabel} reacting={reactingLabel} pending={intentLabel}";
        }

        private static void ValidateCurrentRound(int currentRound)
        {
            if (currentRound < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(currentRound),
                    currentRound,
                    "Current round cannot be negative.");
            }
        }

        private static void ValidatePhase(CombatPhase phase)
        {
            if (!Enum.IsDefined(typeof(CombatPhase), phase))
            {
                throw new ArgumentOutOfRangeException(nameof(phase), phase, "Unknown combat phase.");
            }
        }
    }
}
