using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using log4net;

namespace LimitOrderBookSimulation.LimitOrderBooks
{
   /// <summary>
    /// Normalized limit order book
    /// </summary>
    public class LimitOrderBookOld : ILimitOrderBook
    {
        #region Logging

        protected static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #endregion Logging
        
        #region Properties

        /// <summary>
        /// Maximum tick size that can be handeled by limit order book
        /// </summary>
        public long MaxPrice { get; }

        /// <summary>
        /// Current time 
        /// </summary>
        public double Time { set; get; }

        /// <summary>
        /// Best ask quote in units of ticks 
        /// </summary>
        public long Ask { private set; get; }

        /// <summary>
        /// Best bis quote in units of ticks 
        /// </summary>
        public long Bid { private set; get; }
        
        /// <summary>
        /// Number of outstanding orders at ticks 
        /// in untits of characteristic order size
        /// </summary>
        private long[] DepthProfile { get; }

        /// <summary>
        /// Time series of limit order books state
        /// </summary>
        public SortedList<double, Price> PriceTimeSeries { get; }
        
        /// <summary>
        /// Event count 
        /// </summary>
        public Dictionary<LimitOrderBookEvent, long> Counter { get; }

        #endregion Properties

        #region Constructor 

        /// <summary>
        /// Constructor  
        /// </summary>
        public LimitOrderBookOld(int maxPrice)
        {
            MaxPrice = maxPrice;
            PriceTimeSeries = new SortedList<double, Price>();
            DepthProfile = new long[MaxPrice + 1];
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

        #endregion Constructor

        #region Methods

        #region Limit order

       /// <summary>
       /// A limit buy order at price level p &lt; pA(t) 
       /// increases the quantity at level p: X → X - e_p
       /// </summary>
       /// <param name="price"></param>
       /// <param name="amount"></param>
       public void SubmitLimitBuyOrder(long price, long amount =1)
        {
            if (price < Ask)
            {
                DepthProfile[price] -= amount;
                Bid = Math.Max(Bid, price);
                Counter[LimitOrderBookEvent.SubmitLimitBuyOrder]++;
                SaveCurrentPrice();
            }
            else
            {
                throw new NotImplementedException("Submission of an marketable limit order is not implemented");
            }
        }

       /// <summary>
       /// A limit sell order at price level p > pB(t) 
       /// increases the quantity at level p: X → X + e_p
       /// </summary>
       /// <param name="price">In units of ticks</param>
       /// <param name="amount"></param>
       public void SubmitLimitSellOrder(long price, long amount =1)
        {
            if (price > Bid)
            {
                DepthProfile[price] += amount;
                Ask = Math.Min(Ask, price);
                Counter[LimitOrderBookEvent.SubmitLimitSellOrder]++;
                SaveCurrentPrice();
            }
            else
            {
                throw new NotImplementedException("Submission of an marketable limit order is not implemented");
            }
        }

        #endregion Limit order

        #region Market order

        /// <summary>
        /// A market buy order decreases the 
        /// quantity at the ask price: X→ X - e_pA
        /// </summary>
        public void SubmitMarketBuyOrder(long amount =1)
        {
            var depth = DepthProfile[Ask] -= amount;
            Counter[LimitOrderBookEvent.SubmitMarketBuyOrder]++;
            if (depth == 0)
            {
                Ask = Array.FindIndex(DepthProfile, q => q > 0);
            }
            SaveCurrentPrice();
        }

        /// <summary>
        /// A market sell order decreases the 
        /// quantity at the bid price: x → xpB(t)+1
        /// </summary>
        public void SubmitMarketSellOrder(long amount =1)
        {
            var depth = DepthProfile[Bid] += amount;
            Counter[LimitOrderBookEvent.SubmitMarketSellOrder]++;
            if (depth == 0)
            {
                Bid = Array.FindLastIndex(DepthProfile, q => q < 0);
            }
            SaveCurrentPrice();
        }

        #endregion Market order

        #region Cancel order

        /// <summary>
        /// -1: Cancel limit buy order
        /// +1: Cancel limit sell order
        /// </summary>
        /// <param name="price"></param>
        /// <param name="sign"></param>
        private void CancelLimitOrder(long price, long amount)
        {
            if (Math.Abs(DepthProfile[price]) == 0) return;

            var depth = DepthProfile[price] -= amount;

            if (amount < 0)
            {
                Counter[LimitOrderBookEvent.CancelLimitBuyOrder]++;
                if (depth == 0)
                {
                    Bid = Array.FindLastIndex(DepthProfile, q => q < 0);
                }
            }
            else
            {
                Counter[LimitOrderBookEvent.CancelLimitSellOrder]++;
                if (depth == 0)
                {
                    Ask = Array.FindIndex(DepthProfile, q => q > 0);
                }
            }
            SaveCurrentPrice();
        }

       /// <summary>
       /// A cancellation of an oustanding limit buy order at price level p &lt; pA(t)
       /// decreases the quantity at level p: x → xp+1
       /// </summary>
       /// <param name="price">In units of ticks</param>
       /// <param name="amount"></param>
       public void CancelLimitBuyOrder(long price, long amount =1)
        {
            CancelLimitOrder(price, -amount);
        }

        /// <summary> 
       /// A cancellation of an oustanding limit sell order at price level p > pB(t)
       /// decreases the quantity at level p: x → xp−1
       /// </summary>
       /// <param name="price">In units of ticks</param>
       /// <param name="amount"></param>
        public void CancelLimitSellOrder(long price, long amount =1)
        {
            CancelLimitOrder(price, amount);
        }

        #endregion Cancel order

        #region Inverse CDF

        /// <summary>
        /// Inverse cummulative function for the interval [tickMin, tickMax]
        /// </summary>
        /// <param name="priceMin"></param>
        /// <param name="priceMax"></param>
        /// <param name="q"></param>
        /// <returns></returns>
        public long InverseCDF(long priceMin, long priceMax, long q)
        {
            long sum = 0;
            for (var price = priceMin; price <= priceMax; price++)
            {
                sum += Math.Abs(DepthProfile[price]);
                if (sum >= q)
                {
                    return price;
                }
            }
            throw new ArgumentOutOfRangeException($"q={q} is too large");
        }
        
        public long InverseCDFSellSide(long minPrice, long maxPrice, long q)
        {
            throw new NotImplementedException();
        }

        public long InverseCDFBuySide(long minPrice, long maxPrice, long q)
        {
            throw new NotImplementedException();
        }

      

       #endregion Inverse CDF

        #region Number of orders

        /// <summary>
        /// Number of limit orders within given price interval [priceMin, priceMax]
        /// </summary>
        /// <param name="priceMin"></param>
        /// <param name="priceMax"></param>
        /// <returns></returns>
        public long NumberOfLimitOrders(long priceMin, long priceMax)
        {
            long sum = 0;
            for (var price = priceMin; price <= priceMax; price++)
            {
                sum += Math.Abs(DepthProfile[price]);
            }
            return sum;
        }

        public long NumberOfBuyOrders(long minPrice = 0, long maxPrice = long.MaxValue)
        {
            throw new NotImplementedException();
        }

        public long NumberOfSellOrders(long minPrice = 0, long maxPrice = long.MaxValue)
        {
            throw new NotImplementedException();
        }
        
        #endregion Number of orders

        /// <summary>
        /// Initilize depth profile 
        /// </summary>
        public void InitilizeDepthProfile(int[,] buyProfile, int[,] sellProfile)
        {
            Ask = int.MaxValue;
            Bid = int.MinValue;

            for (var i = 0; i < buyProfile.GetLength(0); i++)
            {
                var tick = buyProfile[i, 0];
                var depth = buyProfile[i, 1];
                if (depth > 0)
                {
                    DepthProfile[tick] = -depth;
                    Bid = Math.Max(tick, Bid);
                }    
            }
            for (var i = 0; i < sellProfile.GetLength(0); i++)
            {
                var tick = sellProfile[i, 0];
                var depth = sellProfile[i, 1];
                if (depth > 0)
                {
                    DepthProfile[tick] = depth;
                    Ask = Math.Min(tick, Ask);
                }
            }
        }

        public void InitializeDepthProfileBuySide(IDictionary<long, long> depthProdile)
        {
            throw new NotImplementedException();
        }

        public void InitializeDepthProfileSellSide(IDictionary<long, long> depthProdile)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Save current frame 
        /// </summary>
        private void SaveCurrentPrice()
        {
            PriceTimeSeries.Add(Time, new Price(bid: Bid, ask: Ask));
        }

        /// <summary>
        /// Save current depth profile 
        /// </summary>
        /// <param name="path"></param>
        /// <param name="maxReleativeTick"></param>
        public void SaveDepthProfile(string path, long maxReleativeTick = 0)
        {
            // Add text header if neccessary
            if (!File.Exists(path))
            {
                // TODO Add header
            }
            using (var file = File.AppendText(path))
            {
                foreach (var tick in DepthProfile.Where(depth => depth != 0).Select((v,i)=>i))
                {
                    file.Write($"{tick}\t{DepthProfile[tick]}\t");
                }
                file.Write("\n");
            }
        }

        #endregion Methods
    }
}
