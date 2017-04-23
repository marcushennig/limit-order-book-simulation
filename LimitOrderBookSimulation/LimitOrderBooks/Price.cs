namespace LimitOrderBookSimulation.LimitOrderBooks
{
    /// <summary>
    /// Bid/Ask Price structure
    /// </summary>
    public class Price
    {
        #region Properties

        public long Bid { set; get; }
        public long Ask { set; get; }
        public long Spread => Ask - Bid;
        public double Mid => 0.5 * (Bid + Ask);

        #endregion Properties

        #region Constructor 

        public Price(long bid, long ask)
        {
            Bid = bid;
            Ask = ask;
        }

        #endregion Constructor 

        #region Methods

        public override string ToString()
        {
            return $"[{Bid}, {Ask}]";
        }

        #endregion Methods
    }
}
