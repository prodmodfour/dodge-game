using System;

namespace ReactionTactics.Turns
{
    /// <summary>
    /// Plain-language combat log line published by the scene-scoped combat event bus.
    /// Entries intentionally store deterministic outcome text only; no random hit,
    /// dodge, accuracy, or roll data is represented.
    /// </summary>
    public readonly struct CombatLogEntry : IEquatable<CombatLogEntry>
    {
        public CombatLogEntry(int sequenceNumber, string message, float timestamp)
        {
            if (sequenceNumber < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(sequenceNumber),
                    sequenceNumber,
                    "Combat log sequence numbers start at 1.");
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Combat log messages cannot be empty.", nameof(message));
            }

            SequenceNumber = sequenceNumber;
            Message = message.Trim();
            Timestamp = Math.Max(0f, timestamp);
        }

        public int SequenceNumber { get; }

        public string Message { get; }

        public float Timestamp { get; }

        public bool Equals(CombatLogEntry other)
        {
            return SequenceNumber == other.SequenceNumber
                && string.Equals(Message, other.Message, StringComparison.Ordinal)
                && Timestamp.Equals(other.Timestamp);
        }

        public override bool Equals(object obj)
        {
            return obj is CombatLogEntry other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = SequenceNumber;
                hashCode = (hashCode * 397) ^ StringComparer.Ordinal.GetHashCode(Message);
                hashCode = (hashCode * 397) ^ Timestamp.GetHashCode();
                return hashCode;
            }
        }

        public override string ToString()
        {
            return $"{SequenceNumber}: {Message}";
        }

        public static bool operator ==(CombatLogEntry left, CombatLogEntry right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CombatLogEntry left, CombatLogEntry right)
        {
            return !left.Equals(right);
        }
    }
}
