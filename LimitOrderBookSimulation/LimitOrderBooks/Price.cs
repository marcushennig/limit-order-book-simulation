namespace LimitOrderBookSimulation.LimitOrderBooks
{
    /// <summary>
    /// Bid/Ask Price structure
    /// </summary>
    public class Price
    {
        #region Properties

        public int Bid { get; }
        public int Ask { get; }
        public int Spread => Ask - Bid;
        public double Mid => 0.5 * (Bid + Ask);

        #endregion Properties

        #region Constructor 

        public Price(int bid, int ask)
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
