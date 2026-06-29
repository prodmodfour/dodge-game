using System;

namespace ReactionTactics.Core
{
    /// <summary>
    /// Represents the outcome of a tactical command or validation check without using
    /// exceptions for expected player-facing failures.
    /// </summary>
    public readonly struct TacticalResult
    {
        private TacticalResult(bool isSuccess, string errorMessage)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public bool IsSuccess { get; }

        public bool IsFailure
        {
            get { return !IsSuccess; }
        }

        public string ErrorMessage { get; }

        public static TacticalResult Success()
        {
            return new TacticalResult(true, string.Empty);
        }

        public static TacticalResult Failure(string errorMessage)
        {
            return new TacticalResult(false, NormalizeErrorMessage(errorMessage));
        }

        public override string ToString()
        {
            return IsSuccess ? "Success" : $"Failure: {ErrorMessage}";
        }

        internal static string NormalizeErrorMessage(string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
            {
                return "Command failed.";
            }

            return errorMessage.Trim();
        }
    }

    /// <summary>
    /// Represents the outcome of a tactical query that may return a value on success.
    /// </summary>
    /// <typeparam name="T">The successful result value type.</typeparam>
    public readonly struct TacticalResult<T>
    {
        private readonly T value;

        private TacticalResult(bool isSuccess, T value, string errorMessage)
        {
            IsSuccess = isSuccess;
            this.value = value;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public bool IsSuccess { get; }

        public bool IsFailure
        {
            get { return !IsSuccess; }
        }

        public string ErrorMessage { get; }

        public T Value
        {
            get
            {
                if (IsFailure)
                {
                    throw new InvalidOperationException("Cannot read the value of a failed tactical result.");
                }

                return value;
            }
        }

        public static TacticalResult<T> Success(T value)
        {
            return new TacticalResult<T>(true, value, string.Empty);
        }

        public static TacticalResult<T> Failure(string errorMessage)
        {
            return new TacticalResult<T>(false, default(T), TacticalResult.NormalizeErrorMessage(errorMessage));
        }

        public TacticalResult WithoutValue()
        {
            return IsSuccess ? TacticalResult.Success() : TacticalResult.Failure(ErrorMessage);
        }

        public override string ToString()
        {
            return IsSuccess ? $"Success: {value}" : $"Failure: {ErrorMessage}";
        }
    }
}
