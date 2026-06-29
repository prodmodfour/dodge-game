using System;

namespace ReactionTactics.Units
{
    /// <summary>
    /// Identifies the prototype combat side a unit belongs to.
    /// </summary>
    public enum TeamId
    {
        Player = 0,
        Enemy = 1
    }

    public static class TeamIdExtensions
    {
        public static bool IsFriendlyTo(this TeamId team, TeamId other)
        {
            EnsureDefined(team, nameof(team));
            EnsureDefined(other, nameof(other));
            return team == other;
        }

        public static bool IsHostileTo(this TeamId team, TeamId other)
        {
            return !team.IsFriendlyTo(other);
        }

        private static void EnsureDefined(TeamId team, string parameterName)
        {
            if (!Enum.IsDefined(typeof(TeamId), team))
            {
                throw new ArgumentOutOfRangeException(parameterName, team, "Unknown team identifier.");
            }
        }
    }
}
