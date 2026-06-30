using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using ReactionTactics.Actions;
using ReactionTactics.Grid;
using ReactionTactics.Units;

namespace ReactionTactics.Reactions
{
    /// <summary>
    /// Runtime model for the ordered off-turn response phase opened by a declared action.
    /// The caller supplies already-ordered reactors; this class only owns lifecycle state
    /// and records which reactors completed or skipped their reaction turn.
    /// </summary>
    public sealed class ReactionWindow
    {
        private readonly ReadOnlyCollection<TacticalUnit> orderedReactors;
        private readonly List<TacticalUnit> completedReactors;
        private readonly ReadOnlyCollection<TacticalUnit> completedReactorsView;
        private readonly List<TacticalUnit> skippedReactors;
        private readonly ReadOnlyCollection<TacticalUnit> skippedReactorsView;
        private int currentIndex;

        public ReactionWindow(ActionIntent sourceIntent, IEnumerable<TacticalUnit> orderedReactors)
        {
            if (sourceIntent == null)
            {
                throw new ArgumentNullException(nameof(sourceIntent));
            }

            SourceIntent = sourceIntent;
            ActionActorPositionAtDeclaration = sourceIntent.ActorPositionAtDeclaration;
            this.orderedReactors = CopyOrderedReactors(orderedReactors);
            completedReactors = new List<TacticalUnit>();
            completedReactorsView = new ReadOnlyCollection<TacticalUnit>(completedReactors);
            skippedReactors = new List<TacticalUnit>();
            skippedReactorsView = new ReadOnlyCollection<TacticalUnit>(skippedReactors);
            currentIndex = -1;
            Phase = ReactionWindowPhase.NotOpened;
        }

        public ActionIntent SourceIntent { get; }

        /// <summary>
        /// The acting unit's grid position captured when the source action was declared.
        /// Reaction ordering uses this fixed position even if the actor later moves.
        /// </summary>
        public GridPosition ActionActorPositionAtDeclaration { get; }

        public IReadOnlyList<TacticalUnit> OrderedReactors
        {
            get { return orderedReactors; }
        }

        public IReadOnlyList<TacticalUnit> CompletedReactors
        {
            get { return completedReactorsView; }
        }

        public IReadOnlyList<TacticalUnit> SkippedReactors
        {
            get { return skippedReactorsView; }
        }

        public int CurrentIndex
        {
            get { return currentIndex; }
        }

        public TacticalUnit CurrentReactor
        {
            get { return IsReactorTurnActive ? orderedReactors[currentIndex] : null; }
        }

        public ReactionWindowPhase Phase { get; private set; }

        public bool IsOpen
        {
            get { return Phase != ReactionWindowPhase.NotOpened && Phase != ReactionWindowPhase.Closed; }
        }

        public bool IsClosed
        {
            get { return Phase == ReactionWindowPhase.Closed; }
        }

        public bool IsReactorTurnActive
        {
            get
            {
                return Phase == ReactionWindowPhase.ReactorStarted
                    && currentIndex >= 0
                    && currentIndex < orderedReactors.Count;
            }
        }

        public int ReactorCount
        {
            get { return orderedReactors.Count; }
        }

        public int ProcessedReactorCount
        {
            get { return completedReactors.Count + skippedReactors.Count; }
        }

        public int RemainingReactorCount
        {
            get { return Math.Max(0, orderedReactors.Count - ProcessedReactorCount); }
        }

        public bool HasRemainingReactors
        {
            get { return ProcessedReactorCount < orderedReactors.Count; }
        }

        public bool HasProcessedAllReactors
        {
            get { return ProcessedReactorCount == orderedReactors.Count; }
        }

        public void Open()
        {
            if (Phase != ReactionWindowPhase.NotOpened)
            {
                throw new InvalidOperationException($"Reaction window cannot open from phase {Phase}.");
            }

            Phase = ReactionWindowPhase.Opened;
        }

        public bool TryStartNextReactor(out TacticalUnit reactor)
        {
            EnsureCanStartNextReactor();
            if (!HasRemainingReactors)
            {
                reactor = null;
                return false;
            }

            currentIndex += 1;
            reactor = orderedReactors[currentIndex];
            Phase = ReactionWindowPhase.ReactorStarted;
            return true;
        }

        public void CompleteCurrentReactor()
        {
            var reactor = RequireCurrentReactor();
            completedReactors.Add(reactor);
            Phase = ReactionWindowPhase.ReactorCompleted;
        }

        public void SkipCurrentReactor()
        {
            var reactor = RequireCurrentReactor();
            skippedReactors.Add(reactor);
            Phase = ReactionWindowPhase.ReactorCompleted;
        }

        public void Close()
        {
            if (Phase == ReactionWindowPhase.NotOpened)
            {
                throw new InvalidOperationException("Reaction window must be opened before it can close.");
            }

            if (Phase == ReactionWindowPhase.Closed)
            {
                throw new InvalidOperationException("Reaction window is already closed.");
            }

            if (IsReactorTurnActive)
            {
                throw new InvalidOperationException("Cannot close a reaction window while a reactor turn is active.");
            }

            if (!HasProcessedAllReactors)
            {
                throw new InvalidOperationException("Cannot close a reaction window before all reactors complete or skip.");
            }

            Phase = ReactionWindowPhase.Closed;
        }

        public override string ToString()
        {
            var actorName = SourceIntent.Actor != null ? SourceIntent.Actor.DisplayName : "none";
            return $"ReactionWindow(source={SourceIntent.Ability.DisplayName}, actor={actorName}, phase={Phase}, currentIndex={currentIndex}, processed={ProcessedReactorCount}/{orderedReactors.Count})";
        }

        private TacticalUnit RequireCurrentReactor()
        {
            if (!IsReactorTurnActive)
            {
                throw new InvalidOperationException("No reaction turn is currently active.");
            }

            return orderedReactors[currentIndex];
        }

        private void EnsureCanStartNextReactor()
        {
            if (Phase == ReactionWindowPhase.NotOpened)
            {
                throw new InvalidOperationException("Reaction window must be opened before reactors can start.");
            }

            if (Phase == ReactionWindowPhase.Closed)
            {
                throw new InvalidOperationException("Reaction window is already closed.");
            }

            if (IsReactorTurnActive)
            {
                throw new InvalidOperationException("Current reactor must complete or skip before the next reactor starts.");
            }
        }

        private static ReadOnlyCollection<TacticalUnit> CopyOrderedReactors(IEnumerable<TacticalUnit> reactors)
        {
            if (reactors == null)
            {
                return new ReadOnlyCollection<TacticalUnit>(Array.Empty<TacticalUnit>());
            }

            var copiedReactors = reactors.ToList();
            for (var i = 0; i < copiedReactors.Count; i += 1)
            {
                if (copiedReactors[i] == null)
                {
                    throw new ArgumentException("Reaction windows cannot contain a null reactor.", nameof(reactors));
                }

                for (var previous = 0; previous < i; previous += 1)
                {
                    if (ReferenceEquals(copiedReactors[previous], copiedReactors[i]))
                    {
                        throw new ArgumentException("Reaction windows cannot contain duplicate reactor entries.", nameof(reactors));
                    }
                }
            }

            return new ReadOnlyCollection<TacticalUnit>(copiedReactors);
        }
    }
}
