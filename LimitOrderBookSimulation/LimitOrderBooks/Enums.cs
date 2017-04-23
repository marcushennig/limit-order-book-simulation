namespace LimitOrderBookSimulation.LimitOrderBooks
{
    /// <summary>
    /// Buy or sell
    /// </summary>
    public enum MarketSide
    {
        Buy = 1,
        Sell = -1
    }

    /// <summary>
    /// Limit order book events
    /// </summary>
    public enum LimitOrderBookEvent
    {
        CancelLimitSellOrder,
        CancelLimitBuyOrder,
        SubmitMarketSellOrder,
        SubmitMarketBuyOrder,
        SubmitLimitSellOrder,
        SubmitLimitBuyOrder,
    }
}
