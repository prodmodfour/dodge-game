using System;
using static ReactionTactics.Turns.CombatEventPayloadValidation;
using ReactionTactics.Units;

namespace ReactionTactics.Turns
{
    /// <summary>
    /// Event raised when a new combat round begins.
    /// </summary>
    public readonly struct RoundStartedEvent
    {
        public RoundStartedEvent(int roundNumber)
        {
            if (roundNumber < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(roundNumber),
                    roundNumber,
                    "Round number must be at least 1 when a round starts.");
            }

            RoundNumber = roundNumber;
        }

        public int RoundNumber { get; }
    }

    /// <summary>
    /// Event raised when control moves from one active unit to another, or when active control is cleared.
    /// </summary>
    public readonly struct ActiveUnitChangedEvent
    {
        public ActiveUnitChangedEvent(TacticalUnit previousUnit, TacticalUnit activeUnit)
        {
            if (previousUnit == null && activeUnit == null)
            {
                throw new ArgumentException("An active-unit change requires at least one previous or current unit.");
            }

            PreviousUnit = previousUnit;
            ActiveUnit = activeUnit;
        }

        public TacticalUnit PreviousUnit { get; }

        public TacticalUnit ActiveUnit { get; }
    }

    /// <summary>
    /// Event raised after a unit's shared action point pool changes.
    /// </summary>
    public readonly struct ActionPointsChangedEvent
    {
        public ActionPointsChangedEvent(TacticalUnit unit, int previousAP, int currentAP)
        {
            RequireUnit(unit, nameof(unit));
            RequireNonNegative(previousAP, nameof(previousAP));
            RequireNonNegative(currentAP, nameof(currentAP));

            Unit = unit;
            PreviousAP = previousAP;
            CurrentAP = currentAP;
        }

        public TacticalUnit Unit { get; }

        public int PreviousAP { get; }

        public int CurrentAP { get; }

        public int Delta
        {
            get { return CurrentAP - PreviousAP; }
        }
    }

    /// <summary>
    /// Event raised after a unit's hit points change.
    /// </summary>
    public readonly struct HitPointsChangedEvent
    {
        public HitPointsChangedEvent(TacticalUnit unit, int previousHP, int currentHP, DamageSource source)
        {
            RequireUnit(unit, nameof(unit));
            RequireNonNegative(previousHP, nameof(previousHP));
            RequireNonNegative(currentHP, nameof(currentHP));

            Unit = unit;
            PreviousHP = previousHP;
            CurrentHP = currentHP;
            Source = source;
        }

        public TacticalUnit Unit { get; }

        public int PreviousHP { get; }

        public int CurrentHP { get; }

        public int Delta
        {
            get { return CurrentHP - PreviousHP; }
        }

        public DamageSource Source { get; }
    }

    /// <summary>
    /// Event raised after an active unit declares an action intent.
    /// </summary>
    public readonly struct ActionDeclaredEvent
    {
        public ActionDeclaredEvent(TacticalUnit actor, object actionIntent)
        {
            RequireUnit(actor, nameof(actor));
            RequireActionIntent(actionIntent, nameof(actionIntent));

            Actor = actor;
            ActionIntent = actionIntent;
        }

        public TacticalUnit Actor { get; }

        /// <summary>
        /// Holds the declared action intent. The concrete ActionIntent type is introduced by later ability tickets.
        /// </summary>
        public object ActionIntent { get; }
    }

    /// <summary>
    /// Event raised when a specific unit begins its reaction turn against a pending action.
    /// </summary>
    public readonly struct ReactionTurnStartedEvent
    {
        public ReactionTurnStartedEvent(TacticalUnit reactor, object sourceActionIntent)
        {
            RequireUnit(reactor, nameof(reactor));
            RequireActionIntent(sourceActionIntent, nameof(sourceActionIntent));

            Reactor = reactor;
            SourceActionIntent = sourceActionIntent;
        }

        public TacticalUnit Reactor { get; }

        /// <summary>
        /// The pending action intent that opened the reaction window.
        /// </summary>
        public object SourceActionIntent { get; }
    }

    /// <summary>
    /// Event raised after an action intent has finished resolving.
    /// </summary>
    public readonly struct ActionResolvedEvent
    {
        public ActionResolvedEvent(TacticalUnit actor, object actionIntent)
        {
            RequireUnit(actor, nameof(actor));
            RequireActionIntent(actionIntent, nameof(actionIntent));

            Actor = actor;
            ActionIntent = actionIntent;
        }

        public TacticalUnit Actor { get; }

        /// <summary>
        /// The resolved action intent. The concrete ActionIntent type is introduced by later ability tickets.
        /// </summary>
        public object ActionIntent { get; }
    }

    /// <summary>
    /// Event raised when a unit is defeated.
    /// </summary>
    public readonly struct UnitDiedEvent
    {
        public UnitDiedEvent(TacticalUnit unit, DamageSource source)
        {
            RequireUnit(unit, nameof(unit));

            Unit = unit;
            Source = source;
        }

        public TacticalUnit Unit { get; }

        public DamageSource Source { get; }
    }

    /// <summary>
    /// Event raised when combat reaches a terminal state.
    /// </summary>
    public readonly struct CombatEndedEvent
    {
        public CombatEndedEvent(bool hasWinningTeam, TeamId winningTeam)
        {
            if (hasWinningTeam)
            {
                ValidateTeam(winningTeam, nameof(winningTeam));
            }

            HasWinningTeam = hasWinningTeam;
            WinningTeam = winningTeam;
        }

        public bool HasWinningTeam { get; }

        public TeamId WinningTeam { get; }

        public static CombatEndedEvent WithWinner(TeamId winningTeam)
        {
            return new CombatEndedEvent(true, winningTeam);
        }

        public static CombatEndedEvent WithoutWinner()
        {
            return new CombatEndedEvent(false, TeamId.Player);
        }
    }

    internal static class CombatEventPayloadValidation
    {
        public static void RequireUnit(TacticalUnit unit, string parameterName)
        {
            if (unit == null)
            {
                throw new ArgumentNullException(parameterName);
            }
        }

        public static void RequireActionIntent(object actionIntent, string parameterName)
        {
            if (actionIntent == null)
            {
                throw new ArgumentNullException(parameterName);
            }
        }

        public static void RequireNonNegative(int value, string parameterName)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Event values cannot be negative.");
            }
        }

        public static void ValidateTeam(TeamId team, string parameterName)
        {
            if (!Enum.IsDefined(typeof(TeamId), team))
            {
                throw new ArgumentOutOfRangeException(parameterName, team, "Unknown tactical team.");
            }
        }
    }
}
