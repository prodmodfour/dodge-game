namespace ReactionTactics.Actions
{
    /// <summary>
    /// Describes when an ability applies its effects relative to reaction windows.
    /// Immediate abilities resolve without waiting for off-turn movement, while
    /// telegraphed abilities are declared first and resolve after reactions.
    /// </summary>
    public enum AbilityTiming
    {
        Immediate = 0,
        Telegraphed = 1
    }
}
