using System;
using ReactionTactics.Core;

namespace ReactionTactics.Commands
{
    /// <summary>
    /// Player-facing validation outcome for movement command creation.
    /// Successful results carry the command that can be executed by either active
    /// movement or reaction movement systems.
    /// </summary>
    public readonly struct MovementValidationResult
    {
        private readonly MoveCommand command;

        private MovementValidationResult(bool isValid, MoveCommand command, string errorMessage)
        {
            IsValid = isValid;
            this.command = command;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public bool IsValid { get; }

        public bool IsInvalid
        {
            get { return !IsValid; }
        }

        public string ErrorMessage { get; }

        public MoveCommand Command
        {
            get
            {
                if (IsInvalid)
                {
                    throw new InvalidOperationException("Cannot read the command from an invalid movement result.");
                }

                return command;
            }
        }

        public static MovementValidationResult Valid(MoveCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            return new MovementValidationResult(true, command, string.Empty);
        }

        public static MovementValidationResult Invalid(string errorMessage)
        {
            return new MovementValidationResult(
                false,
                null,
                TacticalResult.Failure(errorMessage).ErrorMessage);
        }

        public TacticalResult WithoutCommand()
        {
            return IsValid ? TacticalResult.Success() : TacticalResult.Failure(ErrorMessage);
        }

        public TacticalResult<MoveCommand> ToCommandResult()
        {
            return IsValid
                ? TacticalResult<MoveCommand>.Success(command)
                : TacticalResult<MoveCommand>.Failure(ErrorMessage);
        }

        public override string ToString()
        {
            return IsValid ? $"Valid movement: {command}" : $"Invalid movement: {ErrorMessage}";
        }
    }
}
