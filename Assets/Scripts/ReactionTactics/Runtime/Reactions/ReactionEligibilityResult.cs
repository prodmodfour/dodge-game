using ReactionTactics.Core;

namespace ReactionTactics.Reactions
{
    /// <summary>
    /// Explains whether an ordered reactor may receive a reaction turn and whether
    /// that turn should be auto-passed by reaction-window flow code.
    /// </summary>
    public readonly struct ReactionEligibilityResult
    {
        private ReactionEligibilityResult(bool canReact, bool shouldAutoPass, string reason)
        {
            CanReact = canReact;
            ShouldAutoPass = shouldAutoPass;
            Reason = reason ?? string.Empty;
        }

        public bool CanReact { get; }

        public bool IsEligible
        {
            get { return CanReact; }
        }

        public bool IsIneligible
        {
            get { return !CanReact; }
        }

        public bool IsSuccess
        {
            get { return CanReact; }
        }

        public bool IsFailure
        {
            get { return !CanReact; }
        }

        public string ErrorMessage
        {
            get { return CanReact ? string.Empty : Reason; }
        }

        /// <summary>
        /// True when reaction-window flow can safely advance this reactor without
        /// presenting meaningful choices. Ineligible reactors and pass-only reactors
        /// both report an explanatory reason for logs/UI.
        /// </summary>
        public bool ShouldAutoPass { get; }

        public string Reason { get; }

        public TacticalResult ToTacticalResult()
        {
            return CanReact ? TacticalResult.Success() : TacticalResult.Failure(Reason);
        }

        public static ReactionEligibilityResult Eligible(string reason = "")
        {
            return new ReactionEligibilityResult(true, false, NormalizeOptionalReason(reason));
        }

        public static ReactionEligibilityResult AutoPass(string reason)
        {
            return new ReactionEligibilityResult(true, true, NormalizeRequiredReason(reason));
        }

        public static ReactionEligibilityResult Ineligible(string reason)
        {
            return new ReactionEligibilityResult(false, true, NormalizeRequiredReason(reason));
        }

        public override string ToString()
        {
            if (CanReact && !ShouldAutoPass)
            {
                return string.IsNullOrEmpty(Reason) ? "Eligible" : $"Eligible: {Reason}";
            }

            return CanReact ? $"Eligible auto-pass: {Reason}" : $"Ineligible auto-pass: {Reason}";
        }

        private static string NormalizeOptionalReason(string reason)
        {
            return string.IsNullOrWhiteSpace(reason) ? string.Empty : reason.Trim();
        }

        private static string NormalizeRequiredReason(string reason)
        {
            return string.IsNullOrWhiteSpace(reason) ? "Reaction turn will auto-pass." : reason.Trim();
        }
    }
}
