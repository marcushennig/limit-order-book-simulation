using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LimitOrderBookUtilities;

namespace LimitOrderBookSimulation.LimitOrderBooks
{
    /// <summary>
    /// Implementation of order books based on maps 
    /// </summary>
    public class LimitOrderBook : ILimitOrderBook
    {
        /// <summary>
        /// Depth profile of sell side
        /// Map: price tick => depth
        /// </summary>
        private SortedDictionary<int, int> DepthSellSide { get; }
        
        /// <summary>
        /// Depth profile of buy side
        /// Map: price tick => depth
        /// </summary>
        private SortedDictionary<int, int> DepthBuySide { get; }
        
        #region Interface

        /// <summary>
        /// Current time 
        /// </summary>
        public double Time { get; set; }

        /// <summary>
        /// Best ask price (Asks is sorted according to key, first is smallest )
        /// </summary>
        public int Ask => DepthSellSide.Any()? DepthSellSide.First().Key : int.MaxValue;
        
        /// <summary>
        /// Best bid (Asks is sorted according to key, first is smallest )
        /// </summary>
        public int Bid => DepthBuySide.Any() ? DepthBuySide.Last().Key : int.MinValue;
 
        /// <summary>
        /// Returns the depth at given price tick
        /// </summary>
        /// <param name="priceTick">Price tick</param>
        /// <returns></returns>
        public int GetDepthAtPriceTick(int priceTick)
        {
            if (DepthBuySide.ContainsKey(priceTick))
            {
                return DepthBuySide[priceTick];
            }
            return DepthSellSide.ContainsKey(priceTick) ? DepthSellSide[priceTick] : 0;
        }

        public int GetRandomPriceFromSellSide(ExtendedRandom random, int minTick, int maxTick)
        {
            var weightedPriceTicks = DepthSellSide
                .Where(p => p.Key >= minTick && 
                            p.Key <= maxTick)
                .ToDictionary(s => s.Key, 
                    s => s.Value);
            
            return random.NextFromWeights(weightedPriceTicks);
        }

        public int GetRandomPriceFromBuySide(ExtendedRandom random, int minTick, int maxTick)
        {
            var weightedPriceTicks = DepthBuySide
                .Where(p => p.Key >= minTick && 
                            p.Key <= maxTick)
                .ToDictionary(s => s.Key, 
                    s => s.Value);
            
            return random.NextFromWeights(weightedPriceTicks);
        }

        public bool IsBuySideEmpty()
        {
            return !DepthBuySide.Any();
        }
        
        public bool IsSellSideEmpty()
        {
            return !DepthSellSide.Any();
        }
        
        public Dictionary<LimitOrderBookEvent, int> Counter { get; }

        /// <inheritdoc />
        /// <summary>
        /// Evolution of the limit order book
        /// </summary>
        public SortedList<double, Price> PriceTimeSeries { get; }
        
        #region Limit order
        
        public void SubmitLimitBuyOrder(int price, int amount = 1)
        {
            AddToBuySide(price, amount);
            Counter[LimitOrderBookEvent.SubmitLimitBuyOrder]++;
        }

        public void SubmitLimitSellOrder(int price, int amount = 1)
        {
            AddToSellSide(price, amount);
            Counter[LimitOrderBookEvent.SubmitLimitSellOrder]++;
        }
        
        #endregion Limit order
        
        #region Market order

        public int SubmitMarketBuyOrder(int amount = 1)
        {
            var price = Ask;
            
            RemoveFromSellSide(price,  amount);
            Counter[LimitOrderBookEvent.SubmitMarketBuyOrder]++;
            
            return price;
        }

        public int SubmitMarketSellOrder(int amount = 1)
        {
            var price = Bid;
            
            RemoveFromBuySide(price, amount);
            Counter[LimitOrderBookEvent.SubmitMarketSellOrder]++;
            
            return price;
        }

        #endregion Market order

        #region Cancel order

        public void CancelLimitBuyOrder(int price, int amount =1)
        {
            RemoveFromBuySide(price, amount);
            Counter[LimitOrderBookEvent.CancelLimitBuyOrder]++;
        }

        public void CancelLimitSellOrder(int price, int amount = 1)
        {
            RemoveFromSellSide(price, amount);
            Counter[LimitOrderBookEvent.CancelLimitSellOrder]++;
        }

        #endregion Cancel order

        #region Number of orders

        private int NumberOfOrders(int minPrice, int maxPrice, MarketSide buyOrSell)
        {
            var marketSide = buyOrSell == MarketSide.Buy ? DepthBuySide : DepthSellSide;
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
        
        /// <summary>
        /// Save bid as well as ask side into a CSV file
        /// </summary>
        /// <param name="fileName">Path of CSV file</param>
        public void SaveDepthProfile(string fileName)
        {
            using (var file = new StreamWriter(fileName))
            {
                foreach (var side in new List<SortedDictionary<int,int>>{DepthBuySide, DepthSellSide})
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
        
        #endregion Interface

        #region Constructors

        /// <summary>
        /// Constructor 
        /// </summary>
        public LimitOrderBook()
        {
            DepthSellSide = new SortedDictionary<int, int>();
            DepthBuySide = new SortedDictionary<int, int>();

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
     
        #region Private
        
        /// <summary>
        /// Initialize depth profile
        /// </summary>
        /// <param name="depthProdile"></param>
        /// <param name="buyOrSell"></param>
        private void InitilizeDepthProfile(IDictionary<int, int> depthProdile, MarketSide buyOrSell)
        {
            var marketSide = buyOrSell == MarketSide.Buy ? DepthBuySide : DepthSellSide;

            marketSide.Clear();
            foreach (var entry in depthProdile)
            {
                marketSide.Add(entry.Key, entry.Value);
            }
        }
        
        /// <summary>
        /// Add limit buy or sell order 
        /// </summary>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        /// <param name="buyOrAsk"></param>
        private void Add(int price, int amount, MarketSide buyOrAsk)
        {
            var marketSide = buyOrAsk == MarketSide.Buy ? DepthBuySide : DepthSellSide;

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
        private void AddToBuySide(int price, int amount)
        {
            Add(price, amount, MarketSide.Buy);
        }

        /// <summary>
        /// Add limit sell order
        /// </summary>
        private void AddToSellSide(int price, int amount)
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
            var marketSide = buyOrAsk == MarketSide.Buy ? DepthBuySide : DepthSellSide;

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
        private void RemoveFromBuySide(int price, int amount)
        {
            Remove(price, amount, MarketSide.Buy);
        }
        
        /// <summary>
        /// Remove limit sell order 
        /// </summary>
        /// <param name="price"></param>
        /// <param name="amount"></param>
        private void RemoveFromSellSide(int price, int amount)
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
    }
}
