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
        public long OrderId {get; }

        /// <summary>
        /// Seconds after midnight with decimal 
	    /// precision of at least milliseconds 
	    /// and up to nanoseconds depending on 
	    /// the requested period
        /// </summary>
        public double Time { get; }

        /// <summary>
        /// Type
        /// </summary>
        public LobEventType Type { get; }

        /// <summary>
        /// Number of shares
        /// </summary>
        public long Volume { get; }

        /// <summary>
        /// Dollar price times 10000 (i.e., A stock price of $91.14 is given by 911400)
        /// </summary>
        public long Price { get; }

        /// <summary>
        /// Sell/Buy limit order
        /// Note: 
        ///		Execution of a sell (buy) limit
        ///		order corresponds to a buyer (seller) 
        ///		initiated trade, i.e. Buy (Sell) trade.
        /// </summary>
        public MarketSide Side { get; }
        
        /// <summary>
        ///Initial state of limit order book before the above event occured 
        /// </summary>
        public LobState InitialState { set; get; }

        /// <summary>
        /// State of limit order book after the above event occured 
        /// </summary>
        public LobState FinalState { set; get; }

        #region Characteristic
        
        public long TotalBidVolumeChange => FinalState.BidVolume.Sum() - InitialState.BidVolume.Sum();
        public long TotalAskVolumeChange => FinalState.AskVolume.Sum() - InitialState.AskVolume.Sum();

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
        /// Relative price of the event to 
        /// the state before the event was submitted 
        /// </summary>
        public long RelativePrice => Side == MarketSide.Buy
            ? InitialState.BestBidPrice - Price
            : Price - InitialState.BestAskPrice;

        /// <summary>
        /// Distance i from the opposite best quote 
        /// </summary>
        public long DistanceBestOppositeQuote => Side == MarketSide.Buy 
            ? InitialState.BestAskPrice - Price 
            : Price - InitialState.BestBidPrice;
        
        /// <summary>
        /// Relative price to best bid
        /// </summary>
        private long BidRelativePrice => InitialState.BestBidPrice - Price;

        /// <summary>
        /// Relative price to best ask
        /// </summary>
        private long AskRelativePrice => Price - InitialState.BestAskPrice;

        #endregion Characteristic

        #endregion Properties

        #region Constructor 

        /// <summary>
        /// Constructor 
        /// </summary>
        /// <param name="orderId"></param>
        /// <param name="time"></param>
        /// <param name="type"></param>
        /// <param name="volume"></param>
        /// <param name="price"></param>
        /// <param name="side"></param>
        public LobEvent(long orderId, double time, LobEventType type, long volume, long price, MarketSide side)
        {
            OrderId = orderId;
            Time = time;
            Type = type;
            Volume = volume;
            Price = price;
            Side = side;
        }

        #endregion Constructor 

        #region Methods

        /// <summary>
        /// Check if price is quantile  
        /// </summary>
        /// <param name="quantile"></param>
        /// <returns></returns>
        public bool IsPriceInQuantile(double quantile)
        {
            return InitialState.IsPriceInQuantile(Price, Side, quantile);
        }

        /// <summary>
        /// Events are equal if they have the same order ID
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(LobEvent other)
        {
            return OrderId == other?.OrderId;
        }

        /// <summary>
        /// String representation
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"(Time={Time}, Type={Type}, OrderId={OrderId}, Volume={Volume}, Price={Price}, Side={Side})";
        }

        #endregion Methods
    }
}
