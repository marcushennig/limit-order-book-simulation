using System;
using System.Linq;

namespace LimitOrderBookRepositories.Model
{
    /// <summary>
    /// Event causing 
    /// an update of the limit order book in the requested price range. All 
    /// events are timestamped to seconds after midnight, with decimal 
    /// precision of at least milliseconds and up to nanoseconds depending 
    /// </summary>
    public class  LobEvent : IEquatable<LobEvent>
    {
        #region Properties

        /// <summary>
        /// Unique order reference number (Assigned in order flow)
        /// </summary>
        public long OrderId { set; get; }

        /// <summary>
        /// Seconds after midnight with decimal 
	    /// precision of at least milliseconds 
	    /// and up to nanoseconds depending on 
	    /// the requested period
        /// </summary>
        public double Time { set; get; }

        /// <summary>
        /// Type
        /// </summary>
        public LobEventType Type { set; get; }

        /// <summary>
        /// Number of shares
        /// </summary>
        public long Volume { set; get; }

        /// <summary>
        /// Dollar price times 10000 (i.e., A stock price of $91.14 is given by 911400)
        /// </summary>
        public long Price { set; get; }

        /// <summary>
        /// Sell/Buy limit order
        /// Note: 
        ///		Execution of a sell (buy) limit
        ///		order corresponds to a buyer (seller) 
        ///		initiated trade, i.e. Buy (Sell) trade.
        /// </summary>
        public MarketSide Side { set; get; }
        
        /// <summary>
        ///Initial state of limit order book before the above event occured 
        /// </summary>
        public LobState InitialState { set; get; }

        /// <summary>
        /// State of limit order book after the above event occured 
        /// </summary>
        public LobState FinalState { set; get; }

        public long TotalBidVolumeChange => FinalState.BidVolume.Sum() - InitialState.BidVolume.Sum();
        public long TotalAskVolumeChange => FinalState.AskVolume.Sum() - InitialState.AskVolume.Sum();

       
        #region Characteristic

        /// <summary>
        /// Submission event that will at least partlly be executed
        /// </summary>
        public bool IsMarketableLimitOrder => Type == LobEventType.Submission &&
                                              (Side == MarketSide.Buy ?
                                                  AskRelativePrice >= 0 : BidRelativePrice >= 0);

        /// <summary>
        /// Submission event that will change the best ask/bid price 
        /// </summary>
        public bool IsAggressiveLimitOrder => Type == LobEventType.Submission && Price > InitialState.BidPrice[0] && Price < InitialState.AskPrice[0];

        /// <summary>
        /// Check if order is crossing limit order 
        /// </summary>
        public bool IsCrossingLimitOrder => Type == LobEventType.Submission &&
                                            (Side == MarketSide.Buy
                                                ? Price >= InitialState.AskPrice[0]
                                                : InitialState.BidPrice[0] >= Price);

        /// <summary>
        /// Relative price of the event to the state before the event was submitted 
        /// </summary>
        public long RelativePrice => Side == MarketSide.Buy
            ? BidRelativePrice
            : AskRelativePrice;

        /// <summary>
        /// Logarithmic relative price of the event to the state before the event was submitted 
        /// </summary>
        public double LogRelativePrice => Side == MarketSide.Buy
            ? Math.Log(InitialState.BestBidPrice) - Math.Log(Price)
            : Math.Log(Price) - Math.Log(InitialState.BestAskPrice);

        /// <summary>
        /// Relative price to best bid
        /// </summary>
        public long BidRelativePrice => InitialState.BestBidPrice - Price;

        /// <summary>
        /// Relative price to best ask
        /// </summary>
        public long AskRelativePrice => Price - InitialState.BestAskPrice;//AskPrice[0];

        /// <summary>
        /// Distance i from the opposite best quote 
        /// </summary>
        public long DistanceBestOppositeQuote
        {
            get
            {
                switch (Side)
                {
                    case MarketSide.Buy:
                        return -AskRelativePrice;
                    case MarketSide.Sell:
                        return -BidRelativePrice;
                    default:
                        return 0;
                }
            }
        }

        #endregion Characteristic

        #endregion Properties

        #region Methods
        
        /// <summary>
        /// Events are equal if they have the same order ID
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(LobEvent other)
        {
            return OrderId == other.OrderId;
        }

        /// <summary>
        /// String representation
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"(Time={Time}, Type={Type}, OrderId={OrderId}, Volume={Volume}, Price={Price}, Side={Side})";
        }

        /// <summary>
        /// Parse line in LOBSTER data
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        public static LobEvent Parse(string line)
        {
            var data = line.Split(',').ToArray();
            var e = new LobEvent
            {
                Time = Convert.ToDouble(data[0]),
                Type = (LobEventType)Convert.ToInt32(data[1]),
                OrderId = Convert.ToInt64(data[2]),
                Volume = Convert.ToInt64(data[3]),
                Price = Convert.ToInt64(data[4]),
                Side = (MarketSide)Convert.ToInt32(data[5])
            };
            return e;
        }

        #endregion Methods
    }
}
