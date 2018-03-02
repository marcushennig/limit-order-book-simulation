using System;
using System.Collections.Generic;
using System.Linq;
using LimitOrderBookSimulation.LimitOrderBooks;
using NUnit.Framework;

namespace UnitTests
{
    [TestFixture]
    public class TestLimitOrderBook
    {
        #region Limit order book data

        private SortedDictionary<int, int> SellSide { set; get; }
        private SortedDictionary<int, int> BuySide { set; get; }

        private const int TickSize = 10;
        private const int Spread = TickSize * 6;
        private const int BuyMinPrice = 10000;
        private const int BuyMaxPrice = BuyMinPrice + TickSize * 100;
        private const int SellMinPrice = BuyMaxPrice + Spread;
        private const int SellMaxPrice = SellMinPrice + TickSize * 100;
        private Random Random { set; get; }

        #endregion

        [SetUp]
        public void Init()
        {
            Random = new Random(42);
            SellSide = new SortedDictionary<int, int>();
            BuySide = new SortedDictionary<int, int>();


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
            var lob = new LimitOrderBook();
            var buySide = new SortedDictionary<int, int>();
            foreach (var pair in BuySide)
            {
                buySide.Add(pair.Key, pair.Value);
            }

            lob.InitializeDepthProfileBuySide(buySide);

            var sellSide = new SortedDictionary<int, int>();
            foreach (var pair in SellSide)
            {
                sellSide.Add(pair.Key, pair.Value);
            }

            lob.InitializeDepthProfileSellSide(sellSide);

            return lob;
        }

        [Test]
        public void TestSubmitLimitBuyOrder()
        {
            var lob = GenerateLimitOrderBook();

            const int price = BuyMinPrice;
            const int amount = 1;

            lob.SubmitLimitBuyOrder(price, amount);

            var expectdDepth = BuySide[price] + amount;
            var depth = lob.Bids[price];

            Assert.True(depth == expectdDepth,
                $"Depth on buy side: {depth} is not the expectd depth: {expectdDepth}");

        }

        [Test]
        public void TestSubmitLimitSellOrder()
        {
            var lob = GenerateLimitOrderBook();

            const int price = SellMinPrice + TickSize * 3;
            const int amount = 1;

            lob.SubmitLimitSellOrder(price, amount);

            var expectdDepth = SellSide[price] + amount;
            var depth = lob.Asks[price];

            Assert.True(depth == expectdDepth,
                $"Depth on sell side: {depth} is not the expectd depth: {expectdDepth}");

        }

        [Test]
        public void TestSubmitMarketBuyOrder()
        {
            var lob = GenerateLimitOrderBook();

            var askPrice = SellSide.Keys.Min();
            const int amount = 1;

            lob.SubmitMarketBuyOrder(amount);

            var expectdDepth = SellSide[askPrice] - amount;
            var depth = lob.Asks[askPrice];


            Assert.True(depth == expectdDepth,
                $"Depth on sell side: {depth} is not the expectd depth: {expectdDepth}");

        }

        [Test]
        public void TestSubmitMarketSellOrder()
        {
            var lob = GenerateLimitOrderBook();

            var bidPrice = BuySide.Keys.Max();
            const int amount = 1;

            lob.SubmitMarketSellOrder(amount);

            var expectdDepth = BuySide[bidPrice] - amount;
            var depth = lob.Bids[bidPrice];


            Assert.True(depth == expectdDepth,
                $"Depth on buy side: {depth} is not the expectd depth: {expectdDepth}");

        }

        [Test]
        public void TestCancelLimitBuyOrder()
        {
            var lob = GenerateLimitOrderBook();

            const int price = BuyMaxPrice - TickSize * 3;
            const int amount = 1;

            lob.CancelLimitBuyOrder(price, amount);

            var expectdDepth = BuySide[price] - amount;
            var depth = lob.Bids[price];


            Assert.True(depth == expectdDepth,
                $"Depth on buy side: {depth} is not the expectd depth: {expectdDepth}");

        }

        [Test]
        public void TestCancelLimitSellOrder()
        {
            var lob = GenerateLimitOrderBook();

            const int price = SellMinPrice + TickSize * 3;
            const int amount = 1;

            lob.CancelLimitSellOrder(price, amount);

            var expectdDepth = SellSide[price] - amount;
            var depth = lob.Asks[price];


            Assert.True(depth == expectdDepth,
                $"Depth on buy side: {depth} is not the expectd depth: {expectdDepth}");

        }

        [Test]
        public void TestMultipleLimitOrderSubmissions()
        {
            var lob = GenerateLimitOrderBook();

            const int price = BuyMinPrice + 3 * TickSize;
            const int amount = 1;
            const int numberOfSubmission = 5;

            for (var i = 0; i < numberOfSubmission; i++)
            {
                lob.SubmitLimitBuyOrder(price, amount);
            }

            var expectdDepth = BuySide[price] + numberOfSubmission * amount;
            var depth = lob.Bids[price];

            Assert.True(depth == expectdDepth,
                $"Depth on buy side: {depth} is not the expectd depth: {expectdDepth}");
        }
        
        [Test]
        public void TestMultipleSubmissionsAndCancellations()
        {
            var lob = GenerateLimitOrderBook();
            const int price = BuyMinPrice + 3 * TickSize;
            const int amount = 1;
            
            lob.SubmitLimitBuyOrder(price, amount);
            lob.SubmitLimitBuyOrder(price, amount);
            lob.CancelLimitBuyOrder(price, amount);
            lob.SubmitLimitBuyOrder(price, amount);
            lob.CancelLimitBuyOrder(price, amount);

            var expectdDepth = BuySide[price] + (3 - 2) * amount;
            var depth = lob.Bids[price];

            Assert.True(depth == expectdDepth,
                $"Depth on buy side: {depth} is not the expectd depth: {expectdDepth}");
        }
        
        [Test]
        public void TestBid()
        {
            var lob = GenerateLimitOrderBook();
            var expectdBidPrice = BuySide.Keys.Max();
            Assert.True(lob.Bid == expectdBidPrice,
                $"The bid: {lob.Bid} is not the expectd bid price: {expectdBidPrice}");
        }
        
        [Test]
        public void TestAsk()
        {
            var lob = GenerateLimitOrderBook();
            var expectdAskPrice = SellSide.Keys.Min();
            Assert.True(lob.Ask == expectdAskPrice,
                $"The ask: {lob.Bid} is not the expectd ask price: {expectdAskPrice}");
        }

        [Test]
        public void TestChangeOfAskPrice()
        {
            var lob = GenerateLimitOrderBook();
            var ask = lob.Ask;
            var depth = lob.Asks[ask];
            
            lob.SubmitMarketBuyOrder(depth);
            var newAsk = lob.Ask;
            
            Assert.True(lob.Asks.ContainsKey(ask) == false);
            Assert.True(newAsk > ask);
        }
        
        [Test]
        public void TestChangeOfBidPrice()
        {
            var lob = GenerateLimitOrderBook();
            var bid = lob.Bid;
            var depth = lob.Bids[bid];
            
            lob.SubmitMarketSellOrder(depth);
            var newBid = lob.Bid;
            
            Assert.True(lob.Bids.ContainsKey(bid) == false);
            Assert.True(newBid < bid);
        }
        
        [Test]
        public void TestCancellationBuyOrderRobustness()
        {
            var lob = GenerateLimitOrderBook();
            const int price = BuyMinPrice + 3 * TickSize;
            var depth = lob.Bids[price];

            for (var i = 0; i < depth + 10; i++)
            {
                lob.CancelLimitBuyOrder(price);    
            }
            
            Assert.True(lob.Bids.ContainsKey(price) == false);
        }
        
        [Test]
        public void TestCancellationSellOrderRobustness()
        {
            var lob = GenerateLimitOrderBook();
            const int price = SellMinPrice + 3 * TickSize;
            var depth = lob.Asks[price];

            for (var i = 0; i < depth + 10; i++)
            {
                lob.CancelLimitSellOrder(price);    
            }
            Assert.True(lob.Asks.ContainsKey(price) == false);
        }
        
        
        [Test]
        public void TestSubmitMarketBuyOrderRobustness()
        {
            var lob = GenerateLimitOrderBook();
            
            var ask = lob.Ask;
            var depth = lob.Asks[ask];

            for (var i = 0; i < depth + 10; i++)
            {
                lob.SubmitMarketBuyOrder();    
            }
            Assert.True(lob.Asks.ContainsKey(ask) == false);
        }
        
        [Test]
        public void TestSubmitMarketSellOrderRobustness()
        {
            var lob = GenerateLimitOrderBook();
            
            var bid = lob.Bid;
            var depth = lob.Bids[bid];

            for (var i = 0; i < depth + 10; i++)
            {
                lob.SubmitMarketSellOrder();    
            }
            Assert.True(lob.Bids.ContainsKey(bid) == false);
        }
    }
}