using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using log4net;
using log4net.Config;
using LimitOrderBookRepositories.Interfaces;
using LimitOrderBookUtilities;
using MathNet.Numerics;
using MathNet.Numerics.Statistics;

// Necessary to configure log4net
[assembly: XmlConfigurator(Watch = true)]

namespace LimitOrderBookRepositories.Model
{
    /// <summary>
    /// LOB trading data for a single trading day
    /// </summary>
    public class LobTradingData : IPriceProcess
    {
        #region Logging

        //Logging  
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #endregion Logging

        #region Fields

        private List<LobEvent> _limitOrders;
        private List<LobEvent> _marketOrders;
        private List<LobEvent> _submittedOrders;
        private List<LobEvent> _canceledOrders;

        private DiscreteDistribution _limitOrderDistribution;
        private DiscreteDistribution _limitSellOrderDistribution;
        private DiscreteDistribution _limitBuyOrderDistribution;
        private DiscreteDistribution _canceledOrderDistribution;
        private DiscreteDistribution _canceledSellOrderDistribution;
        private DiscreteDistribution _canceledBuyOrderDistribution;
        private DiscreteDistribution _averageDepthProfile;

        #endregion Fields

        #region Properties

        #region Repository Parameter

        private string WorkFolder { set; get; }
        private string RepositoryFolder { set; get; }
        private string LogFolder { set; get; }

        #endregion Repository Parameter

        #region LOB parameter
        
        /// <summary>
        /// Start trading duration [seconds after midnight]
        /// </summary>
        public double StartTradingTime { private set; get; }

        /// <summary>
        /// End trading duration [seconds after midnight]
        /// </summary>
        public double EndTradingTime { private set; get; }

        /// <summary>
        /// Total trading duration [seconds]
        /// </summary>
        public double TradingDuration { private set; get; }

        /// <summary>
        /// Level of the LOB data
        /// </summary>
        public int Level { private set; get; }

        /// <summary>
        /// Stock symbol
        /// </summary>
        public string Symbol { private set; get; }
        
        /// <summary>
        /// Limit order book states
        /// </summary>
        public LobState[] States { private set; get; }

        /// <summary>
        /// Limit order book events
        /// </summary>
        public LobEvent[] Events { private set; get; }

        /// <summary>
        /// Limit orders
        /// </summary>
        public List<LobEvent> LimitOrders
        {
            get
            {
                if (_limitOrders == null)
                {
                    _limitOrders = Events.Where(p => p.Type == LobEventType.Submission)
                                         .ToList();
                }
                return _limitOrders; 
                
            }
        }

        /// <summary>
        /// TODO: Consider the case of crossing limit events => extract the transacted part
        /// Get all market order 
        /// </summary>
        /// <returns></returns>
        public List<LobEvent> MarketOrders
        {
            get
            {
                if (_marketOrders == null)
                {
                    // Select all crossing limit limit orders, that result in an at least a partial transaction,
                    // The non-transacted part => limit order & transacted part => effective market order 
                    // var crossingLimitOrders = Entries.Where(p => p.IsCrossingLimitOrder)
                    //                                  .Select(p => p.Event).ToList();
                    // Select all those events that result in an immediate execution 
                    // var marketOrders = Entries.GroupBy(p => p.Event.OrderId)
                    //                             .Where(p => p.Count() == 1 && (p.First().Event.Type == EventType.ExecutionHiddenLimitOrder ||
                    //                                                            p.First().Event.Type == EventType.ExecutionVisibleLimitOrder))
                    //                             .SelectMany(p => p)
                    //                             .ToList();
                    _marketOrders = Events.Where(p => p.Type == LobEventType.ExecutionVisibleLimitOrder).ToList();
                }
                return _marketOrders;

            }
        }

        /// <summary>
        /// Get either market or limit orders 
        /// </summary>
        /// <returns></returns>
        public List<LobEvent> SubmittedOrders
        {
            get
            {
                if (_submittedOrders == null)
                {
                    _submittedOrders = new List<LobEvent>();

                    _submittedOrders.AddRange(LimitOrders);
                    _submittedOrders.AddRange(MarketOrders);
                }
                return _submittedOrders;
            }
        }

        /// <summary>
        /// Returns list of cancellations events 
        /// </summary>
        /// <returns></returns>
        public List<LobEvent> CanceledOrders
        {
            get
            {
                if (_canceledOrders == null)
                {
                    _canceledOrders = Events.Where(p => p.Type == LobEventType.Deletion || 
                                                        p.Type == LobEventType.Cancellation)
                                                 .ToList();
                }
                return _canceledOrders;
            }
        }

        /// <summary>
        /// Average size of the bid price interval
        /// </summary>
        public double AverageBuySideIntervalSize
        {
            get
            {
                return States.Select(state => (double)(state.BestAskPrice - state.BidPrice.Last()))
                              .Mean();
            }
        }

        /// <summary>
        ///  Average size of the ask price interval
        /// </summary>
        public double AverageAskSideIntervalSize
        {
            get
            {
                return States.Select(state => (double)(state.AskPrice.Last() - state.BestBidPrice))
                              .Mean();
            }
        }

        /// <summary>
        /// Price tick size  
        /// </summary>
        public long PriceTickSize { private set; get; }

        /// <summary>
        /// Characteristic order size  
        /// </summary>
        public double AverageOrderSize { private set; get; }

        /// <summary>
        /// Characteristic order size  
        /// </summary>
        public double AverageMarketOrderSize { private set; get; }

        /// <summary>
        /// Characteristic order size  
        /// </summary>
        public double AverageLimitOrderSize { private set; get; }

        /// <summary>
        /// List of the order ids of hidden orders
        /// </summary>
        private List<long> HiddenOrderIds { set; get; }

        #endregion LOB parameter

        #region Statistics
        
        #region Limit orders

        /// <summary>
        /// Average numbder of outstanding limit orders depending 
        /// on distance to best opposite quote
        /// </summary>
        public DiscreteDistribution AverageDepthProfile
        {
            get
            {
                if (_averageDepthProfile != null) return _averageDepthProfile;

                var outstandingLimitOrders = new Dictionary<long, double>();
                const long priceError = 9999999999;
                var totalWeight = 0.0;
                for (var i = 0; i < Events.Length - 1; i++)
                {
                    var state = Events[i].FinalState;
                    var weight = (Events[i + 1].Time - Events[i].Time) / TradingDuration;
                    totalWeight += weight;
                    for (var j = 0; j < Level; j++)
                    {
                        // Ask side
                        var askVolume = state.AskVolume[j];
                        
                        var askDistance = state.AskPrice[j] - state.BestBidPrice;

                        if (Math.Abs(state.BestBidPrice) != priceError)
                        {
                            if (!outstandingLimitOrders.ContainsKey(askDistance))
                            {
                                outstandingLimitOrders.Add(askDistance, 0);
                            }
                            outstandingLimitOrders[askDistance] += weight * askVolume;
                        }
                        
                        // Bid side
                        var bidVolume = state.BidVolume[j];
                        var bidDistance = state.BestAskPrice - state.BidPrice[j];
                        if (Math.Abs(state.BestAskPrice) != priceError)
                        {
                            if (!outstandingLimitOrders.ContainsKey(bidDistance))
                            {
                                outstandingLimitOrders.Add(bidDistance, 0);
                            }
                            outstandingLimitOrders[bidDistance] += weight *bidVolume;
                        }
                    }   
                }
                _averageDepthProfile = new DiscreteDistribution(outstandingLimitOrders.ToDictionary(p => p.Key, p => p.Value / totalWeight));
                
                return _averageDepthProfile;
                
            }
        }

        /// <summary>
        /// TODO: Error
        /// Number of submitted limit orders in dependence of 
        /// distance to best opposite quote 
        /// </summary>
        public DiscreteDistribution LimitOrderDistribution
        {
            get
            {
                if (_limitOrderDistribution != null) return _limitOrderDistribution;

                var data = new SortedDictionary<long, long>();
                foreach (var entry in LimitOrders.GroupBy(p => p.DistanceBestOppositeQuote))
                {
                    data.Add(entry.Key, entry.Sum(p => p.Volume));
                }
                _limitOrderDistribution = new DiscreteDistribution(data);

                return _limitOrderDistribution;
            }
        }

        /// <summary>
        /// TODO: Error
        /// Number of submitted limit sell orders in dependence of 
        /// distance to best opposite quote 
        /// </summary>
        public DiscreteDistribution LimitSellOrderDistribution
        {
            get
            {
                if (_limitSellOrderDistribution != null) return _limitSellOrderDistribution;

                var data = new SortedDictionary<long, long>();
                foreach (var entry in LimitOrders.Where(p=>p.Side == MarketSide.Sell)
                                                 .GroupBy(p => p.DistanceBestOppositeQuote))
                {
                    data.Add(entry.Key, entry.Sum(p => p.Volume));
                }
                _limitSellOrderDistribution = new DiscreteDistribution(data);

                return _limitSellOrderDistribution;
            }
        }

        /// <summary>
        /// TODO: Error
        /// Number of submitted limit buy orders in dependence of 
        /// distance to best opposite quote 
        /// </summary>
        public DiscreteDistribution LimitBuyOrderDistribution
        {
            get
            {
                if (_limitBuyOrderDistribution != null) return _limitBuyOrderDistribution;

                var data = new SortedDictionary<long, long>();
                foreach (var entry in LimitOrders.Where(p => p.Side == MarketSide.Buy)
                                                 .GroupBy(p => p.DistanceBestOppositeQuote))
                {
                    data.Add(entry.Key, entry.Sum(p => p.Volume));
                }
                _limitBuyOrderDistribution = new DiscreteDistribution(data);

                return _limitBuyOrderDistribution;
            }
        }

        #endregion Limit orders

        #region Cancelled orders

        /// <summary>
        /// Total volume of canceled orders in dependence of distance to 
        /// best opposite quote a
        /// </summary>
        public DiscreteDistribution CanceledOrderDistribution
        {
            get
            {
                if (_canceledOrderDistribution != null) return _canceledOrderDistribution;

                var data = new SortedDictionary<long, long>();
                foreach (var entry in CanceledOrders.GroupBy(p => p.DistanceBestOppositeQuote))
                {
                    data.Add(entry.Key, entry.Sum(p => p.Volume));
                }
                _canceledOrderDistribution = new DiscreteDistribution(data);
                return _canceledOrderDistribution;
            }
        }

        /// <summary>
        /// Total volume of canceled sell orders in dependence of distance to 
        /// best opposite quote a
        /// </summary>
        public DiscreteDistribution CanceledSellOrderDistribution
        {
            get
            {
                if (_canceledSellOrderDistribution != null) return _canceledSellOrderDistribution;

                var data = new SortedDictionary<long, long>();
                foreach (var entry in CanceledOrders.Where(p=>p.Side == MarketSide.Sell)
                                                    .GroupBy(p => p.DistanceBestOppositeQuote))
                {
                    data.Add(entry.Key, entry.Sum(p => p.Volume));
                }
                _canceledSellOrderDistribution = new DiscreteDistribution(data);
                return _canceledSellOrderDistribution;
            }
        }

        /// <summary>
        /// Total volume of canceled sell orders in dependence of distance to 
        /// best opposite quote a
        /// </summary>
        public DiscreteDistribution CanceledBuyOrderDistribution
        {
            get
            {
                if (_canceledBuyOrderDistribution != null) return _canceledBuyOrderDistribution;

                var data = new SortedDictionary<long, long>();
                foreach (var entry in CanceledOrders.Where(p => p.Side == MarketSide.Buy)
                                                    .GroupBy(p => p.DistanceBestOppositeQuote))
                {
                    data.Add(entry.Key, entry.Sum(p => p.Volume));
                }
                _canceledBuyOrderDistribution = new DiscreteDistribution(data);
                return _canceledBuyOrderDistribution;
            }
        }
        
        #endregion Cancelled orders 

        #endregion

        #endregion Properties

        #region Constructor

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="level"></param>
        /// <param name="events"></param>
        /// <param name="states"></param>
        /// <param name="skipFirstSeconds"></param>
        /// <param name="skipLastSeconds"></param>
        public LobTradingData(int level, 
                              LobEvent[] events, 
                              LobState[] states, 
                              double skipFirstSeconds = 0,
                              double skipLastSeconds = 0)
        {
            Level = level;

            #region Skip first and last seconds of states and events

            var t0 = events.Min(p => p.Time);
            var t1 = events.Max(p => p.Time);

            var k1 = Array.FindIndex(events, p => p.Time >= t0 + skipFirstSeconds) - 1;
            if (k1 > 0)
            {
                events = events.Skip(k1).ToArray();
                states = states.Skip(k1).ToArray();
            }
            var k2 = Array.FindLastIndex(events, p => p.Time <= t1 - skipLastSeconds) + 1;
            if (k2 > 0 && k2 <= events.Length)
            {
                Events = events.Take(k2).ToArray();
                States = states.Take(k2).ToArray();
            }
            else
            {
                Events = events;
                States = states;
            }

            #endregion Skip first and last seconds of states and events

            #region Map events and states to transitions

            // The 'message' and 'orderbook' files can be viewed as matrices of size (N x 6) and (N x (4 x NumLevel)) respectively, 
            // where N is the number of events in the requested price range and NumLevel is the number of levels requested
            // The k-th row in the 'message' file describes the limit order event causing the change in the limit order book 
            // from line k-1 to line k in the 'orderbook' file.
            for (var k = 1; k < Events.Length; k++)
            {
                Events[k].InitialState = States[k - 1];
                Events[k].FinalState = States[k];
            }
            Events = Events.Skip(1).ToArray();

            #endregion

            #region Tick size

            var prices = new List<long>();

            prices.AddRange(States.SelectMany(p => p.AskPrice));
            prices.AddRange(States.SelectMany(p => p.BidPrice));
            prices.Sort();

            var diffs = prices.Select((p, i) => i == 0 ? 0 : p - prices[i - 1])
                .Where(p => p > 0)
                .Distinct()
                .ToList();

            var guess = diffs.Min();

            PriceTickSize = diffs.Select(d => Euclid.GreatestCommonDivisor(guess, d)).Min();

            #endregion Tick size

            #region Hidden orders

            // Exclude any hidden orders by using their Id  by order id
            HiddenOrderIds =
                Events.Where(p => p.Type == LobEventType.ExecutionHiddenLimitOrder)
                    .Select(p => p.OrderId)
                    .Distinct()
                    .ToList();

            #endregion Hidden orders

            #region Characteristic order size 

            AverageLimitOrderSize = LimitOrders.Select(p => (double)p.Volume).Mean();
            AverageMarketOrderSize = MarketOrders.Select(p => (double)p.Volume).Mean();

            AverageOrderSize = 0.5 * (AverageLimitOrderSize + AverageMarketOrderSize);

            #endregion Characteristic order size 

            #region Time

            StartTradingTime = events.Min(p => p.Time);
            EndTradingTime = events.Max(p => p.Time);
            TradingDuration = EndTradingTime - StartTradingTime;
            
            #endregion
        }


        #endregion Constructor

        #region Testing

        /// <summary>
        /// Check if the given event is a consistent limit order
        /// </summary>
        /// <returns></returns>
        private static bool IsConsistentLimitOrder(LobEvent order)
        {
            var consistent = true;

            var initialState = order.InitialState;
            var finalState = order.FinalState;
                
            #region  Check if order price is non-positive
            if (order.Price <= 0)
            {
                Log.Error($"The price of limit order '{order.OrderId} (t={order.Time})' is not positive");
                consistent = false;
            }
            #endregion

            #region Check if order volume is non-positive
            if (order.Volume <= 0)
            {
                Log.Error($"The volume of limit order '{order.OrderId} (t={order.Time})' is not positive");
                consistent = false;
            }
            #endregion

            #region Sell limit order: price > best bid
            if (order.Side == MarketSide.Sell && !(order.Price > initialState.BestBidPrice))
            {
                Log.Error($"'{order.OrderId} (t={order.Time})' failed test: sell limit order: Price = {order.Price} > Best Bid = {initialState.BestBidPrice}");
                consistent = false;
            }
            #endregion

            #region Buy limit order: price < best ask
            if (order.Side == MarketSide.Buy && !(order.Price < initialState.BestAskPrice))
            {
                Log.Error($"'{order.OrderId} (t={order.Time})' failed test: buy limit order: Price = {order.Price} < Best ask = {initialState.BestAskPrice}");
                consistent = false;
            }
            #endregion

            #region Limit order: final depth - intial depth = order volume

            var intialDepth = initialState.Depth(order.Price, order.Side);
            var finalDepth = finalState.Depth(order.Price, order.Side);

            if (finalDepth - intialDepth != order.Volume)
            {
                Log.Error($"'{order.OrderId} (t={order.Time})' failed test: final depth - intial depth = order volume");
                consistent = false;
            }
            #endregion 
          
            return consistent;
        }

        /// <summary>
        /// Measured the change of total volume near spread of two given states, total difference 
        /// cannot be determined, as levels are fixed (number of price points for depth profile 
        /// on buý or sell side) 
        /// </summary>
        /// <param name="side"></param>
        /// <param name="initialState"></param>
        /// <param name="finalState"></param>
        /// <returns></returns>
        private long DepthDifferenceNearSpread(MarketSide side, LobState initialState, LobState finalState)
        {
            // Volume change near spread
            long dDepth = 0;
            long depthDifferenceNearSpread = 0;
            var price = side == MarketSide.Buy ? initialState.BestBidPrice : initialState.BestAskPrice;
            do
            {
                dDepth = initialState.Depth(price, side) - finalState.Depth(price, side);
                depthDifferenceNearSpread += dDepth;
                price = price + (side == MarketSide.Buy ? -1 : 1) * PriceTickSize;
            } while (dDepth > 0);
            return depthDifferenceNearSpread;
        }

        /// <summary>
        /// Check if given order is a market order 
        /// </summary>
        /// <returns></returns>
        private bool IsConsistentMarketOrder(LobEvent order)
        {
            var consistent = true;

            var initialState = order.InitialState;
            var finalState = order.FinalState;
            
            #region Check if order volume is non-positive
            if (order.Volume <= 0)
            {
                Log.Error($"The volume of limit order '{order.OrderId} (t={order.Time})' is not positive");
                consistent = false;
            }
            #endregion

            #region Buy market order: price = best ask price
            if (order.Side == MarketSide.Buy && order.Price != initialState.BestBidPrice)
            {
                Log.Error(
                    $"'{order.OrderId} (t={order.Time})' failed test: sell market order: Price = {order.Price} == Best bid = {initialState.BestBidPrice}");
                consistent = false;
            }
            #endregion
            
            #region Buy market order: initial total bid volume - final total bid volume = order volume
            if (order.Side == MarketSide.Buy && order.Price == initialState.BestBidPrice)
            {
                // The following lines below wond help, as boundary effects can take place due
                // the finite number of levels observed. 
                //var initialTotalVolume = initialState.BidVolume.Sum();
                //var finalTotalVolume = finalState.BidVolume.Sum();
                // Depth difference near spread on buy side (BestBidPrice)
                var depthDifference = DepthDifferenceNearSpread(order.Side, initialState, finalState);
                if (depthDifference != order.Volume)
                {
                    Log.Error($"'{order.OrderId} (t={order.Time})' failed test: sell market order: initial total bid volume - final total bid volume = order volume");
                    consistent = false;
                }
            }
            #endregion

            #region Sell market order: price = best bid price
            // Order.Side means which side of the LOB is addressed, if the sell side is addressed
            // the order is a buy order 
            if (order.Side == MarketSide.Sell && order.Price != initialState.BestAskPrice)
            {
                Log.Error(
                    $"'{order.OrderId} (t={order.Time})' failed test: buy market order: Price = {order.Price} == Best ask = {initialState.BestBidPrice}");
                consistent = false;
            }
            #endregion

            #region Sell market order: initial total ask volume - final total ask volume = order volume
            if (order.Side == MarketSide.Sell && order.Price == initialState.BestAskPrice)
            {
                // Depth difference near spread on sell side (BestAskPrice)
                var depthDifference = DepthDifferenceNearSpread(order.Side, initialState, finalState);
                if (depthDifference != order.Volume)
                {
                    Log.Error($"'{order.OrderId} (t={order.Time})' failed test: buy market order: initial total ask volume - final total ask volume = order volume");
                    consistent = false;
                }
            }
            #endregion 

            return consistent;
        }

        /// <summary>
        /// Check of order is a consistent canceled order 
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        private static bool IsConsistentCanceledOrder(LobEvent order)
        {
            var consistent = true;

            var initialState = order.InitialState;
            var finalState = order.FinalState;

            #region Cancel buy order: price <= best bid price
            if (order.Side == MarketSide.Buy && !(order.Price <= initialState.BestBidPrice))
            {
                Log.Error($"Cancel buy order: '{order.OrderId} (t={order.Time})' failed test: price <= best bid price");
                consistent = false;
            }
            #endregion 

            #region Cancel sell order: price <= best bid price
            if (order.Side == MarketSide.Sell && !(order.Price >= initialState.BestAskPrice))
            {
                Log.Error($"Cancel sell order: '{order.OrderId} (t={order.Time})' failed test: price >= best ask price");
                consistent = false;
            }
            #endregion

            #region Was order really cancelled?
            if (order.Side == MarketSide.Buy && order.Price <= initialState.BestBidPrice || (order.Side == MarketSide.Sell && order.Price >= initialState.BestAskPrice))
            {
                var initialDepth = initialState.Depth(order.Price, order.Side);
                var finalDepth = finalState.Depth(order.Price, order.Side);

                if (initialDepth - finalDepth != order.Volume)
                {
                    Log.Error($"Cancel order: '{order.OrderId} (t={order.Time})' failed test: depth(price,t-) - depth(price,t+) == volume");
                    consistent = false;
                }
            }
            #endregion 

            return consistent;
        }

        /// <summary>
        /// Check consistency
        /// </summary>
        public void CheckConsistency()
        {
            #region  Time consistency

            if (Events.Select(p => p.Time).Count() != Events.Select(p => p.Time).Distinct().Count())
            {
                var ng = Events.GroupBy(p => p.Time).Count(g => g.Count() > 1);
                Log.Warn($"There are {ng} different time goups");
            }

            #endregion

            #region Check limit orders
            var inconsistentLimitOrders = LimitOrders.Where(order => !IsConsistentLimitOrder(order))
                .ToList();

            if (inconsistentLimitOrders.Any())
            {
                Log.Error($"Inconsistent limit orders: {inconsistentLimitOrders.Count}");
            }
            else
            {
                Log.Info($"All {LimitOrders.Count} limit order are consistent");
            }
            #endregion

            #region Check market orders
            var inconsistentMarketOrders = MarketOrders.Where(order => !IsConsistentMarketOrder(order))
                .ToList();

            if (inconsistentMarketOrders.Any())
            {
                Log.Error($"Inconsistent market orders: {inconsistentMarketOrders.Count}");
            }
            else
            {
                Log.Info($"All {MarketOrders.Count} market order are consistent");
            }
            #endregion

            #region Check canceled orders
            var inconsistentCanceledOrders = CanceledOrders.Where(order => !IsConsistentCanceledOrder(order))
                .ToList();

            if (inconsistentCanceledOrders.Any())
            {
                Log.Error($"Inconsistent canceled orders: {inconsistentCanceledOrders.Count}");
            }
            else
            {
                Log.Info($"All {CanceledOrders.Count} canceled order are consistent");
            }
            #endregion
        }

        #endregion Testing

        #region Methods
        
        /// <summary>
        /// TODO: Use Distribution
        /// Average number of outstanding sell limit orders depending on  
        /// distance to best opposite quote 
        /// </summary>
        /// <returns></returns>
        public Dictionary<long, double> AverageNumberOfOutstandingLimitOrders(MarketSide side)
        {
            var averageNumber = new Dictionary<long, double>();
            var sign = side == MarketSide.Sell ? -1 : 1;
            var totalWeight = 0.0;

            for (var i = 0; i < Events.Length - 1; i++)
            {
                var state = Events[i].FinalState;

                var bestOppositePrice = side==MarketSide.Sell? state.BestBidPrice : state.BestAskPrice;

                var weight = (Events[i + 1].Time - Events[i].Time) / TradingDuration;
                totalWeight += weight;
                var level = side == MarketSide.Sell ? state.AskPrice.Length : state.BidPrice.Length;
                
                if (Math.Abs(bestOppositePrice) == 9999999999)
                {
                    continue;
                }
                
                for (var j = 0; j < level; j++)
                {
                    var price = side == MarketSide.Sell ? state.AskPrice[j] : state.BidPrice[j];
                    var distance = sign * (bestOppositePrice - price);
                    var depth = (double) (side == MarketSide.Sell ? state.AskVolume[j] : state.BidVolume[j]);

                    if (Math.Abs(price) == 9999999999)
                    {
                        continue;
                    }

                    if (!averageNumber.ContainsKey(distance))
                    {
                        averageNumber.Add(distance, 0);
                    }
                    averageNumber[distance] += weight * depth;

                }
            }
            return averageNumber.ToDictionary(p => p.Key, p=>p.Value / totalWeight);
        }

        /// <summary>
        /// Q[i], which is the average number of outstanding orders at a 
        /// distance of i ticks from the opposite best quote 
        /// </summary>
        public Dictionary<long, double> AverageNumberOfOutstandingLimitOrders()
        {
            var averageBuy = AverageNumberOfOutstandingLimitOrders(MarketSide.Buy);
            var averageSell = AverageNumberOfOutstandingLimitOrders(MarketSide.Sell);

            var average = new Dictionary<long, double>();
            foreach (var key in averageBuy.Keys.Concat(averageSell.Keys).Distinct())
            {
                double buyDepth = 0;
                double sellDepth = 0;

                averageBuy.TryGetValue(key, out buyDepth);
                averageSell.TryGetValue(key, out sellDepth);

                average.Add(key, 0.5 * (buyDepth + sellDepth));
            }
            return average;
        }
        
        #region Export

        /// <summary>
        /// Save price evolution
        /// </summary>
        public void SavePriceProcess(string file)
        {
            // Eliminate any points where there is no change
            var groupedPrices = Events.Select(p => new
            {
                Time = p.Time,
                Bid = p.FinalState.BestBidPrice,
                Ask = p.FinalState.BestAskPrice
            })
            .GroupBy(p => p.Time)
            .ToList();

            // More then one event at the same time can change the state
            // of the LOB, hence use the last state (bid, ask) 
            var prices = groupedPrices.Select(p => p.OrderBy(q => q.Time).Last())
                                        .OrderBy(p => p.Time)
                                        .ToList();

            using (var sw = new StreamWriter(file))
            {
                foreach (var price in prices)
                {
                    sw.WriteLine($"{price.Time},{price.Bid},{price.Ask}");
                }
            }
        }

        #endregion Export
        
        #endregion Methods
    }
}
