using System;
using System.Collections.Generic;
using System.IO;
using LimitOrderBookSimulation.EventModels;
using LimitOrderBookSimulation.LimitOrderBooks;
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


            for (var p = BuyMinPrice; p < BuyMaxPrice; p += TickSize)
            {
                BuySide.Add(p, Random.Next(1, 1000));
            }

            for (var p = SellMinPrice; p < SellMaxPrice; p += TickSize)
            {
                SellSide.Add(p, Random.Next(1, 1000));
            }
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
            var limitOrderBook = GenerateLimitOrderBook();
            var model = new SmithFarmerModel
            {
                CancellationRate = 10,
                MarketOrderRate = 3,
                LimitOrderRateDensity = 5,
                
                CharacteristicOrderSize = 1,
                TickSize = TickSize,
                TickIntervalSize = TickSize * 20,
                LimitOrderBook = limitOrderBook
            };
            model.SimulateOrderFlow(duration:100);
            model.SavePriceProcess("C:\\Users\\d90789\\Documents\\d-fine\\Trainings\\Oxford MSc in Mathematical Finance\\Thesis\\Source\\4 Output\\test.csv");
        }
    }
}