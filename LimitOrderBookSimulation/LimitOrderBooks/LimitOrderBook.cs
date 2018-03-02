using System;
using System.Collections.Generic;
using System.IO;
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
        public SortedDictionary<int, int> Asks { get; }
        public SortedDictionary<int, int> Bids { get; }

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
        public int Ask => Asks.Any()? Asks.First().Key : int.MaxValue;
        
        /// <summary>
        /// Best bid (Asks is sorted according to key, first is smallest )
        /// </summary>
        public int Bid => Bids.Any() ? Bids.Last().Key : int.MinValue;

        public Dictionary<LimitOrderBookEvent, int> Counter { get; }

        /// <inheritdoc />
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
            Asks = new SortedDictionary<int, int>();
            Bids = new SortedDictionary<int, int>();

            PriceTimeSeries = new SortedList<double, Price>();

            // Counter used for statistics
            Counter = new Dictionary<LimitOrderBookEvent, int>
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
        /// <param name="buyOrAsk"></param>
        private void Add(int price, int amount, MarketSide buyOrAsk)
        {
            var marketSide = buyOrAsk == MarketSide.Buy ? Bids : Asks;

            if (!marketSide.ContainsKey(price))
            {
                marketSide[price] = amount;
            }
            else
            {
                marketSide[price] += amount;
            }

            SaveCurrentPrice();
        }

        /// <summary>
        /// Add limit buy order
        /// </summary>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        private void AddBid(int price, int amount)
        {
            Add(price, amount, MarketSide.Buy);
        }

        /// <summary>
        /// Add limit sell order
        /// </summary>
        private void AddAsk(int price, int amount)
        {
            Add(price, amount, MarketSide.Sell);
        }

        /// <summary>
        /// repmove limit order
        /// </summary>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <param name="buyOrAsk"></param>
        private void Remove(int price, int amount, MarketSide buyOrAsk)
        {
            var marketSide = buyOrAsk == MarketSide.Buy ? Bids : Asks;

            if (!marketSide.ContainsKey(price)) return;
            
            marketSide[price] -= amount;
            // Make sure that 
            if (marketSide[price] <= 0)
            {
                marketSide.Remove(price);
            }
            SaveCurrentPrice();
        }

        /// <summary>
        /// Remove limit buy order 
        /// </summary>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        private void RemoveBid(int price, int amount)
        {
            Remove(price, amount, MarketSide.Buy);
        }
        
        /// <summary>
        /// Remove limit sell order 
        /// </summary>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        private void RemoveAsk(int price, int amount)
        {
            Remove(price, amount, MarketSide.Sell);
        }

        /// <summary>
        /// Save current price 
        /// </summary>
        private void SaveCurrentPrice()
        {
            if (!PriceTimeSeries.ContainsKey(Time))
            {
                PriceTimeSeries.Add(Time, new Price(bid: Bid, ask: Ask));
            }
            else
            {
                PriceTimeSeries[Time] = new Price(bid: Bid, ask: Ask);
            }
        }

        #endregion Private

        #region Public

        #region Limit order
        
        public void SubmitLimitBuyOrder(int price, int amount = 1)
        {
            AddBid(price, amount);
            Counter[LimitOrderBookEvent.SubmitLimitBuyOrder]++;
        }

        public void SubmitLimitSellOrder(int price, int amount = 1)
        {
            AddAsk(price, amount);
            Counter[LimitOrderBookEvent.SubmitLimitSellOrder]++;
        }
        
        #endregion Limit order

        #region Market order

        public void SubmitMarketBuyOrder(int amount = 1)
        {
            RemoveAsk(Ask,  amount);
            Counter[LimitOrderBookEvent.SubmitMarketBuyOrder]++;
        }

        public void SubmitMarketSellOrder(int amount = 1)
        {
            RemoveBid(Bid, amount);
            Counter[LimitOrderBookEvent.SubmitMarketSellOrder]++;
        }

        #endregion Market order

        #region Cancel order

        public void CancelLimitBuyOrder(int price, int amount =1)
        {
            RemoveBid(price, amount);
            Counter[LimitOrderBookEvent.CancelLimitBuyOrder]++;
        }

        public void CancelLimitSellOrder(int price, int amount = 1)
        {
            RemoveAsk(price, amount);
            Counter[LimitOrderBookEvent.CancelLimitSellOrder]++;
        }

        #endregion Cancel order

        #region Number of orders

        private int NumberOfOrders(int minPrice, int maxPrice, MarketSide buyOrSell)
        {
            var marketSide = buyOrSell == MarketSide.Buy ? Bids : Asks;
            return marketSide.Where(p => p.Key >= minPrice && p.Key <= maxPrice)
                             .Sum(p => p.Value);
        }

        /// <summary>
        /// Number of buy orders within given price range
        /// </summary>
        /// <returns></returns>
        public int NumberOfBuyOrders(int minPrice = 0, int maxPrice = int.MaxValue)
        {
            return NumberOfOrders(minPrice, maxPrice, MarketSide.Buy);
        }
        
        /// <summary>
        /// Number of sell orders within given price range
        /// </summary>
        /// <returns></returns>
        public int NumberOfSellOrders(int minPrice = 0, int maxPrice = int.MaxValue)
        {
            return NumberOfOrders(minPrice, maxPrice, MarketSide.Sell);
        }
        
        /// <summary>
        /// Number of limit orders within given price range
        /// </summary>
        /// <param name="minPrice"></param>
        /// <param name="maxPrice"></param>
        /// <returns></returns>
        public int NumberOfLimitOrders(int minPrice, int maxPrice)
        {
            return NumberOfBuyOrders(minPrice, maxPrice) + NumberOfSellOrders(minPrice, maxPrice);
        }

        #endregion Number of orders

        #region Inverse CDF

        public int InverseCDF(int priceMin, int priceMax, int q)
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
        private int InverseCDF(int min, int max, int q, MarketSide side)
        {
            var quantity = side==MarketSide.Buy ? Bids : Asks;
            int sum = 0;

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
        public int InverseCDFSellSide(int minPrice, int maxPrice, int q)
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
        public int InverseCDFBuySide(int minPrice, int maxPrice, int q)
        {
            return InverseCDF(minPrice, maxPrice, q, MarketSide.Buy);
        }

        #endregion Inverse CDF

        #region Initialize depth profile

        /// <summary>
        /// Initialize depth profile
        /// </summary>
        /// <param name="depthProdile"></param>
        /// <param name="buyOrSell"></param>
        private void InitilizeDepthProfile(IDictionary<int, int> depthProdile, MarketSide buyOrSell)
        {
            var marketSide = buyOrSell == MarketSide.Buy ? Bids : Asks;

            marketSide.Clear();
            foreach (var entry in depthProdile)
            {
                marketSide.Add(entry.Key, entry.Value);
            }
        }

        /// <summary>
        /// Initialize depth profile on buy side
        /// </summary>
        /// <param name="depthProdile"></param>
        public void InitializeDepthProfileBuySide(IDictionary<int, int> depthProdile)
        {
            InitilizeDepthProfile(depthProdile, MarketSide.Buy);
        }

        /// <summary>
        /// Initialize depth profile on buy side
        /// </summary>
        /// <param name="depthProdile"></param>
        public void InitializeDepthProfileSellSide(IDictionary<int, int> depthProdile)
        {
            InitilizeDepthProfile(depthProdile, MarketSide.Sell);
        }
        
        #endregion Initialize depth profile
     
       
        /// <summary>
        /// Save bid as well as ask side into a CSV file
        /// </summary>
        /// <param name="fileName">Path of CSV file</param>
        public void SaveDepthProfile(string fileName)
        {
            using (var file = new StreamWriter(fileName))
            {
                foreach (var side in new List<SortedDictionary<int,int>>{Bids, Asks})
                {
                    foreach (var entry in side)
                    {
                        var price = entry.Key;
                        var depth = entry.Value;
                        
                        file.WriteLine($"{price}\t{depth}");
                    }
                }
            }
        }
      
        #endregion Public
        
        #endregion Methods
    }
}
