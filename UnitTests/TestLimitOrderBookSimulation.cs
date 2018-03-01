using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LimitOrderBookSimulation.EventModels;
using LimitOrderBookSimulation.LimitOrderBooks;
using LimitOrderBookUtilities;
using MathNet.Numerics;
using NUnit.Framework;

namespace UnitTests
{
    [TestFixture]
    public class TestLimitOrderBookSimulation
    {
        #region Limit order book data

        private SortedDictionary<long, long> SellSide { set; get; }
        private SortedDictionary<long, long> BuySide { set; get; }

        private const int TickSize = 10;
        private const long Spread = TickSize * 4;
        private const long BuyMinPrice = 10000;
        private const long BuyMaxPrice = BuyMinPrice + TickSize * 100;
        private const long SellMinPrice = BuyMaxPrice + Spread;
        private const long SellMaxPrice = SellMinPrice + TickSize * 100;
        private const long BuyMaxDepth = 100;
        private const long SellMaxDepth = 100;

        private Random Random { set; get; }

        #endregion
        
        [SetUp]
        public void Init()
        {
            Random = new Random(42);
            SellSide = new SortedDictionary<long, long>();
            BuySide = new SortedDictionary<long, long>();

            for (var price = BuyMinPrice; price <= BuyMaxPrice; price += TickSize)
            {
                BuySide.Add(price, GetDepth(p: price, pmin: BuyMaxPrice, pmax: BuyMinPrice, scale: BuyMaxDepth));
            }

            for (var price = SellMinPrice; price <= SellMaxPrice; price += TickSize)
            {
                SellSide.Add(price, GetDepth(p: price, pmin: SellMinPrice, pmax: SellMaxPrice, scale: SellMaxDepth));
            }
        }
        
        /**
         * Depth profile on [pmin, pmax] that more ore less resemble the reality
         */
        private static long GetDepth(long p, long pmin, long pmax, long scale)
        {
            var x01 = (p - pmin) / (double)(pmax - pmin);
            
            const double lambda = 5e-3;
            const double fmax = 0.07512;
            
            var f = Math.Exp(x01 * Math.Log(lambda) - lambda) / SpecialFunctions.Gamma(x01);
            
            return (long) (scale * f / fmax);
        }

        /**
         * Generate a pre-initialized limit order book
         */
        private LimitOrderBook GenerateLimitOrderBook()
        {
            var buySide = new SortedDictionary<long, long>();
            foreach (var pair in BuySide)
            {
                buySide.Add(pair.Key, pair.Value);
            }
            var sellSide = new SortedDictionary<long, long>();
            foreach (var pair in SellSide)
            {
                sellSide.Add(pair.Key, pair.Value);
            }
            var lob = new LimitOrderBook {Time = 0};
            
            lob.InitializeDepthProfileBuySide(buySide);
            lob.InitializeDepthProfileSellSide(sellSide);
            
            return lob;
        }
            
        [Test]
        public void TestSmithFarmerModel()
        {
            const string outputFolder =
                "C:\\Users\\d90789\\Documents" +
                "\\d-fine\\Trainings\\Oxford MSc in Mathematical Finance" +
                "\\Thesis\\Source\\4 Output\\";
            
            var limitOrderBook = GenerateLimitOrderBook();
            // Choose parameter such that certain end result is achieved 
            // Check characteristic scales 
            const double asymtoticDepth = 0.5 * (BuyMaxDepth + SellMaxDepth);
            const double muC = 0.01;
            const double muL = asymtoticDepth * muC;
            const double muM = Spread * muL * 2;
            const double T = 1000;
            
            var model = new SmithFarmerModel
            {
                CancellationRate = muC,
                MarketOrderRate = muM,
                LimitOrderRateDensity = muL,
                CharacteristicOrderSize = 1,
                TickSize = TickSize,
                SimulationIntervalSize = TickSize * 10,
                LimitOrderBook = limitOrderBook
            };
            model.LimitOrderBook.SaveDepthProfile(Path.Combine(outputFolder, "depth_start.csv"));
            
            model.SimulateOrderFlow(duration: T);
            
            // Save simulation result for further inspection in e.g. Matlab
            model.SavePriceProcess(Path.Combine(outputFolder, "price_process.csv"));
            model.LimitOrderBook.SaveDepthProfile(Path.Combine(outputFolder, "depth_end.csv"));
            SharedUtilities.SaveAsJson(model.LimitOrderBook.Counter, Path.Combine(outputFolder, "counter.json"));
           
            // Do some plausibility checks
            var lob = model.LimitOrderBook;
            
            Assert.True(lob.Counter[LimitOrderBookEvent.SubmitMarketSellOrder] > 0);
            Assert.True(lob.Counter[LimitOrderBookEvent.SubmitMarketBuyOrder] > 0);
            Assert.True(lob.Counter[LimitOrderBookEvent.SubmitLimitSellOrder] > 0);
            Assert.True(lob.Counter[LimitOrderBookEvent.SubmitLimitBuyOrder] > 0);
            Assert.True(lob.Counter[LimitOrderBookEvent.CancelLimitSellOrder] > 0);
            Assert.True(lob.Counter[LimitOrderBookEvent.CancelLimitBuyOrder] > 0);
            
            // Make sure that all depths are positive 
            Assert.True(lob.Asks.Values.All(p => p > 0), "Depth cannot be negative on sell side");
            Assert.True(lob.Bids.Values.All(p => p > 0), "Depth cannot be negative on buy side");

            Assert.True(lob.Ask > lob.Bid, "Ask must be greater than bid price");
            
            Assert.True(lob.PriceTimeSeries.Any(), "No events where triggered");
            
            Assert.True(lob.PriceTimeSeries
                .Select(p => p.Value)
                .All(p => p.Ask > p.Bid), "Ask must be greater than bid price");          
        }
    }
}