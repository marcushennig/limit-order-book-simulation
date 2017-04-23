using System;
using System.Collections.Generic;
using System.Linq;

namespace LimitOrderBookSimulation.LimitOrderBooks
{
    /// <summary>
    /// Implementation of order books based on maps 
    /// </summary>
    public class LimitOrderBook : ILimitOrderBook
    {
        #region Properties

        /// <summary>
        /// Use map for asks price, stores price/quantity
        /// </summary>
        public SortedDictionary<long, long> Asks { set; get; }
        public SortedDictionary<long, long> Bids { set; get; }

        /// <summary>
        /// Check if there is anything in the order book
        /// </summary>
        public bool IsEmpty => !Asks.Any() && !Bids.Any();

        /// <summary>
        /// TODO
        /// </summary>
        public double Time { get; set; }

        /// <summary>
        /// Best ask price (Asks is sorted according to key, first is smallest )
        /// </summary>
        public long Ask => Asks.Any()? Asks.First().Key : long.MaxValue;
        
        /// <summary>
        /// Best bid (Asks is sorted according to key, first is smallest )
        /// </summary>
        public long Bid => Bids.Any() ? Bids.Last().Key : long.MinValue;

        public Dictionary<LimitOrderBookEvent, long> Counter { get; }

        /// <summary>
        /// Evolution of the limit order book
        /// </summary>
        public SortedList<double, Price> PriceTimeSeries { get; }

        #endregion Properties

        #region Constructors

        /// <summary>
        /// Constructor 
        /// </summary>
        public LimitOrderBook()
        {
            Asks = new SortedDictionary<long, long>();
            Bids = new SortedDictionary<long, long>();

            PriceTimeSeries = new SortedList<double, Price>();

            // Counter used for statistics
            Counter = new Dictionary<LimitOrderBookEvent, long>
            {
                { LimitOrderBookEvent.CancelLimitBuyOrder, 0 },
                { LimitOrderBookEvent.CancelLimitSellOrder, 0 },
                { LimitOrderBookEvent.SubmitLimitBuyOrder, 0 },
                { LimitOrderBookEvent.SubmitLimitSellOrder, 0 },
                { LimitOrderBookEvent.SubmitMarketBuyOrder, 0 },
                { LimitOrderBookEvent.SubmitMarketSellOrder, 0},
            };
        }

        #endregion Constructors 
        
        #region Methods

        #region Private
        
        /// <summary>
        /// Add limit buy or sell order 
        /// </summary>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <param name="side"></param>
        private void Add(long price, long amount, MarketSide side)
        {
            var table = side == MarketSide.Buy ? Bids : Asks;

            if (!table.ContainsKey(price))
                table[price] = amount;
            else
                table[price] += amount;

            SaveCurrentPrice();
        }

        /// <summary>
        /// Add limit buy order
        /// </summary>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        private void AddBid(long price, long amount)
        {
            Add(price, amount, MarketSide.Buy);
        }

        /// <summary>
        /// Add limit sell order
        /// </summary>
        private void AddAsk(long price, long amount)
        {
            Add(price, amount, MarketSide.Sell);
        }

        /// <summary>
        /// repmove limit order
        /// </summary>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <param name="side"></param>
        private void Remove(long price, long amount, MarketSide side)
        {
            var table = side==MarketSide.Buy ? Bids : Asks;

            if (table.ContainsKey(price))
            {
                table[price] -= amount;

                if (table[price] == 0)
                    table.Remove(price);

                SaveCurrentPrice();
            }
        }

        /// <summary>
        /// Remove limit buy order 
        /// </summary>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        private void RemoveBid(long price, long amount)
        {
            Remove(price, amount, MarketSide.Buy);
        }
        
        /// <summary>
        /// Remove limit sell order 
        /// </summary>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        private void RemoveAsk(long price, long amount)
        {
            Remove(price, amount, MarketSide.Sell);
        }

        /// <summary>
        /// Save current price 
        /// </summary>
        private void SaveCurrentPrice()
        {
            PriceTimeSeries.Add(Time, new Price(bid: Bid, ask: Ask));
        }

        #endregion Private

        #region Public

        #region Limit order
        public void SubmitLimitBuyOrder(long price, long amount =1)
        {
            AddBid(price, amount);
            Counter[LimitOrderBookEvent.SubmitLimitBuyOrder]++;
        }

        public void SubmitLimitSellOrder(long price, long amount = 1)
        {
            AddAsk(price, amount);
            Counter[LimitOrderBookEvent.SubmitLimitSellOrder]++;
        }
        #endregion Limit order

        #region Market order

        public void SubmitMarketBuyOrder(long amount = 1)
        {
            RemoveAsk(Ask,  amount);
            Counter[LimitOrderBookEvent.SubmitMarketBuyOrder]++;
        }

        public void SubmitMarketSellOrder(long amount = 1)
        {
            RemoveBid(Bid, amount);
            Counter[LimitOrderBookEvent.SubmitMarketSellOrder]++;
        }

        #endregion Market order

        #region Cancel order

        public void CancelLimitBuyOrder(long price, long amount =1)
        {
            RemoveBid(price, amount);
            Counter[LimitOrderBookEvent.CancelLimitBuyOrder]++;
        }

        public void CancelLimitSellOrder(long price, long amount = 1)
        {
            RemoveAsk(price, amount);
            Counter[LimitOrderBookEvent.CancelLimitSellOrder]++;
        }

        #endregion Cancel order

        #region Number of orders

        private long NumberOfOrders(long minPrice, long maxPrice, MarketSide side)
        {
            var table = side == MarketSide.Buy ? Bids : Asks;
            return table.Where(p => p.Key >= minPrice && p.Key <= maxPrice)
                      .Sum(p => p.Value);
        }

        /// <summary>
        /// Number of buy orders within given price range
        /// </summary>
        /// <returns></returns>
        public long NumberOfBuyOrders(long minPrice = 0, long maxPrice = long.MaxValue)
        {
            return NumberOfOrders(minPrice, maxPrice, MarketSide.Buy);
        }
        
        /// <summary>
        /// Number of sell orders within given price range
        /// </summary>
        /// <returns></returns>
        public long NumberOfSellOrders(long minPrice = 0, long maxPrice = long.MaxValue)
        {
            return NumberOfOrders(minPrice, maxPrice, MarketSide.Sell);
        }
        
        /// <summary>
        /// Number of limit orders within given price range
        /// </summary>
        /// <param name="minPrice"></param>
        /// <param name="maxPrice"></param>
        /// <returns></returns>
        public long NumberOfLimitOrders(long minPrice, long maxPrice)
        {
            return NumberOfBuyOrders(minPrice, maxPrice) + NumberOfSellOrders(minPrice, maxPrice);
        }

        #endregion Number of orders

        #region Inverse CDF

        public long InverseCDF(long priceMin, long priceMax, long q)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Inverse cummulative distribution function
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <param name="q"></param>
        /// <param name="side"></param>
        /// <returns></returns>
        private long InverseCDF(long min, long max, long q, MarketSide side)
        {
            var quantity = side==MarketSide.Buy ? Bids : Asks;
            long sum = 0;

            for (var price = min; price <= max; price++)
            {
                if (!quantity.ContainsKey(price)) continue;

                sum += quantity[price];
                if (sum >= q)
                {
                    return price;
                }
            }
            throw new ArgumentOutOfRangeException($"q={q} is too large");
        }

        /// <summary>
        /// Inverse cummulative distribution function 
        /// </summary>
        /// <param name="minPrice"></param>
        /// <param name="maxPrice"></param>
        /// <param name="q"></param>
        /// <returns></returns>
        public long InverseCDFSellSide(long minPrice, long maxPrice, long q)
        {
            return InverseCDF(minPrice, maxPrice, q, MarketSide.Sell);
        }

        /// <summary>
        /// Inverse cummulative distribution function 
        /// </summary>
        /// <param name="minPrice"></param>
        /// <param name="maxPrice"></param>
        /// <param name="q"></param>
        /// <returns></returns>
        public long InverseCDFBuySide(long minPrice, long maxPrice, long q)
        {
            return InverseCDF(minPrice, maxPrice, q, MarketSide.Buy);
        }

        #endregion Inverse CDF

        #region Initialize depth profile

        /// <summary>
        /// Initialize depth profile
        /// </summary>
        /// <param name="depthProdile"></param>
        /// <param name="side"></param>
        private void InitilizeDepthProfile(IDictionary<long, long> depthProdile, MarketSide side)
        {
            var table = side == MarketSide.Buy ? Bids : Asks;

            table.Clear();
            foreach (var entry in depthProdile)
            {
                table.Add(entry.Key, entry.Value);
            }
        }

        /// <summary>
        /// Initialize depth profile on buy side
        /// </summary>
        /// <param name="depthProdile"></param>
        public void InitializeDepthProfileBuySide(IDictionary<long, long> depthProdile)
        {
            InitilizeDepthProfile(depthProdile, MarketSide.Buy);
        }

        /// <summary>
        /// Initialize depth profile on buy side
        /// </summary>
        /// <param name="depthProdile"></param>
        public void InitializeDepthProfileSellSide(IDictionary<long, long> depthProdile)
        {
            InitilizeDepthProfile(depthProdile, MarketSide.Sell);
        }
        
        #endregion Initialize depth profile

        public void SaveDepthProfile(string path, long maxReleativeTick = 0)
        {
            // TODO
        }
        
        #endregion Public
        
        #endregion Methods
    }
}
