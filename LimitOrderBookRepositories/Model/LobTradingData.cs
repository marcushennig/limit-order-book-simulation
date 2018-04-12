using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LimitOrderBookUtilities;
using MathNet.Numerics;
using MathNet.Numerics.Statistics;

namespace LimitOrderBookRepositories.Model
{
    /// <summary>
    /// LOB trading data for a single trading day
    /// </summary>
    public class LobTradingData
    {
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
    
        #region LOB parameter
        
        /// <summary>
        /// Start trading duration [seconds after midnight]
        /// </summary>
        public double StartTradingTime { get; }

        /// <summary>
        /// End trading duration [seconds after midnight]
        /// </summary>
        public double EndTradingTime { get; }

        /// <summary>
        /// Total trading duration [seconds]
        /// </summary>
        public double TradingDuration { get; }

        /// <summary>
        /// Level of the LOB data
        /// </summary>
        public int Level { get; }
        
        /// <summary>
        /// Limit order book states
        /// </summary>
        public LobState[] States { get; }

        /// <summary>
        /// Limit order book events
        /// </summary>
        public LobEvent[] Events { get; }

        /// <summary>
        /// Limit orders
        /// </summary>
        public List<LobEvent> LimitOrders
        {
            get
            {
                return _limitOrders ?? (_limitOrders = Events.Where(p => p.Type == LobEventType.Submission).ToList());
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
                return _marketOrders ?? (_marketOrders = Events.Where(p => p.Type == LobEventType.ExecutionVisibleLimitOrder || 
                                                                           p.Type == LobEventType.ExecutionHiddenLimitOrder).ToList());
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
                if (_submittedOrders != null) return _submittedOrders;

                _submittedOrders = new List<LobEvent>();

                _submittedOrders.AddRange(LimitOrders);
                _submittedOrders.AddRange(MarketOrders);

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
                return _canceledOrders ?? (_canceledOrders = Events.Where(p => p.Type == LobEventType.Deletion ||
                                                                               p.Type == LobEventType.Cancellation).ToList());
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
        public int PriceTickSize { get; }

        /// <summary>
        /// Characteristic order size  
        /// </summary>
        public double AverageOrderSize { get; }

        /// <summary>
        /// Characteristic order size  
        /// </summary>
        public double AverageMarketOrderSize { get; }

        /// <summary>
        /// Characteristic order size  
        /// </summary>
        public double AverageLimitOrderSize { get; }

        /// <summary>
        /// List of the order ids of hidden orders
        /// </summary>
        private List<int> HiddenOrderIds { set; get; }

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

                var outstandingLimitOrders = new Dictionary<int, double>();

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

                        if (!outstandingLimitOrders.ContainsKey(askDistance))
                        {
                            outstandingLimitOrders.Add(askDistance, 0);
                        }
                        outstandingLimitOrders[askDistance] += weight * askVolume;
                        
                        // Bid side
                        var bidVolume = state.BidVolume[j];
                        var bidDistance = state.BestAskPrice - state.BidPrice[j];
                        if (!outstandingLimitOrders.ContainsKey(bidDistance))
                        {
                            outstandingLimitOrders.Add(bidDistance, 0);
                        }
                        outstandingLimitOrders[bidDistance] += weight * bidVolume;
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

                var data = new SortedDictionary<int, int>();
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

                var data = new SortedDictionary<int, int>();
                foreach (var entry in LimitOrders.Where(p=>p.Side == LobMarketSide.Sell)
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

                var data = new SortedDictionary<int, int>();
                foreach (var entry in LimitOrders.Where(p => p.Side == LobMarketSide.Buy)
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

                var data = new SortedDictionary<int, int>();
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

                var data = new SortedDictionary<int, int>();
                foreach (var entry in CanceledOrders.Where(p=>p.Side == LobMarketSide.Sell)
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

                var data = new SortedDictionary<int, int>();
                foreach (var entry in CanceledOrders.Where(p => p.Side == LobMarketSide.Buy)
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

            var prices = new List<int>();

            prices.AddRange(States.SelectMany(p => p.AskPrice));
            prices.AddRange(States.SelectMany(p => p.BidPrice));
            prices.Sort();

            var diffs = prices.Select((p, i) => i == 0 ? 0 : p - prices[i - 1])
                .Where(p => p > 0)
                .Distinct()
                .ToList();

            var guess = diffs.Min();

            PriceTickSize = diffs.Select(d => (int)Euclid.GreatestCommonDivisor(guess, d)).Min();

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

            StartTradingTime = Events.Min(p => p.Time);
            EndTradingTime = Events.Max(p => p.Time);
            TradingDuration = EndTradingTime - StartTradingTime;
            
            #endregion
        }


        #endregion Constructor

        #region Methods
        
        /// <summary>
        /// TODO: Use Distribution
        /// Average number of outstanding sell limit orders depending on  
        /// distance to best opposite quote 
        /// </summary>
        /// <returns></returns>
        public Dictionary<int, double> AverageNumberOfOutstandingLimitOrders(LobMarketSide side)
        {
            var averageNumber = new Dictionary<int, double>();
            var sign = side == LobMarketSide.Sell ? -1 : 1;
            var totalWeight = 0.0;

            for (var i = 0; i < Events.Length - 1; i++)
            {
                var state = Events[i].FinalState;

                var bestOppositePrice = side==LobMarketSide.Sell? state.BestBidPrice : state.BestAskPrice;

                var weight = (Events[i + 1].Time - Events[i].Time) / TradingDuration;
                totalWeight += weight;
                var level = side == LobMarketSide.Sell ? state.AskPrice.Length : state.BidPrice.Length;
                
                if (Math.Abs(bestOppositePrice) == 9999999999)
                {
                    continue;
                }
                
                for (var j = 0; j < level; j++)
                {
                    var price = side == LobMarketSide.Sell ? state.AskPrice[j] : state.BidPrice[j];
                    var distance = sign * (bestOppositePrice - price);
                    var depth = (double) (side == LobMarketSide.Sell ? state.AskVolume[j] : state.BidVolume[j]);

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
        public Dictionary<int, double> AverageNumberOfOutstandingLimitOrders()
        {
            var averageBuy = AverageNumberOfOutstandingLimitOrders(LobMarketSide.Buy);
            var averageSell = AverageNumberOfOutstandingLimitOrders(LobMarketSide.Sell);

            var average = new Dictionary<int, double>();
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