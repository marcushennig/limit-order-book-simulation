namespace LimitOrderBookRepositories.Model
{
    /// <summary>
    /// Limit order event type  
    /// </summary>
    public enum LobEventType
    {
        Submission = 1,                 // Submission of a new limit order
        Cancellation = 2,               // Cancellation (Partial deletion of a limit order)
        Deletion = 3,                   // Deletion (Total deletion of a limit order)
        ExecutionVisibleLimitOrder = 4, // Execution of a visible limit order			   	 
        ExecutionHiddenLimitOrder = 5,  // Execution of a hidden limit order
        TradingHalt = 7                 // Trading halt indicator (Detailed information below)
    }
}
