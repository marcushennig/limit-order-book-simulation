using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using LimitOrderBookSimulation.EventModels;
using LimitOrderBookSimulation.LimitOrderBooks;
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
        private const long Spread = TickSize * 6;
        private const long BuyMinPrice = 10000;
        private const long BuyMaxPrice = BuyMinPrice + TickSize * 100;
        private const long SellMinPrice = BuyMaxPrice + Spread;
        private const long SellMaxPrice = SellMinPrice + TickSize * 100;
        private Random Random { set; get; }

        #endregion
        
        [SetUp]
        public void Init()
        {
            Random = new Random(42);
            SellSide = new SortedDictionary<long, long>();
            BuySide = new SortedDictionary<long, long>();


            for (var p = BuyMinPrice; p <= BuyMaxPrice; p += TickSize)
            {
                BuySide.Add(p, GetDepth(p:p, pmin: BuyMaxPrice, pmax: BuyMinPrice, scale: 100));
            }

            for (var p = SellMinPrice; p <= SellMaxPrice; p += TickSize)
            {
                SellSide.Add(p, GetDepth(p:p, pmin:SellMinPrice, pmax:SellMaxPrice, scale:400));
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
            var model = new SmithFarmerModel
            {
                CancellationRate = 1,
                MarketOrderRate = 10,
                LimitOrderRateDensity = 5,
                
                CharacteristicOrderSize = 1,
                TickSize = TickSize,
                TickIntervalSize = TickSize * 20,
                LimitOrderBook = limitOrderBook
            };
            model.LimitOrderBook.SaveDepthProfile(Path.Combine(outputFolder, "depth_start.csv"));
            
            model.SimulateOrderFlow(duration:100);
            
            model.SavePriceProcess(Path.Combine(outputFolder, "test.csv"));
            model.LimitOrderBook.SaveDepthProfile(Path.Combine(outputFolder, "depth_end.csv"));
        }
    }
}