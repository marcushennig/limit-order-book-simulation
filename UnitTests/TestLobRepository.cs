using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LimitOrderBookRepositories;
using LimitOrderBookRepositories.Model;
using NUnit.Framework;

namespace UnitTests
{
    [TestFixture]
    public class TestLobRepository
    {
        [SetUp]
        public void Init()
        {
        }

        /// <summary>
        /// Measured the change of total volume near spread of two given states, total difference 
        /// cannot be determined, as levels are fixed (number of price points for depth profile 
        /// on buy or sell side) 
        /// </summary>
        /// <param name="side"></param>
        /// <param name="initialState"></param>
        /// <param name="finalState"></param>
        /// <param name="priceTickSize"></param>
        /// <returns></returns>
        private  static int DepthDifferenceNearSpread(MarketSide side, LobState initialState, LobState finalState, int priceTickSize)
        {
            // Volume change near spread
            var dDepth = 0;
            var depthDifferenceNearSpread = 0;
            var price = side == MarketSide.Buy ? initialState.BestBidPrice : initialState.BestAskPrice;
            do
            {
                dDepth = initialState.Depth(price, side) - finalState.Depth(price, side);
                depthDifferenceNearSpread += dDepth;
                price = price + (side == MarketSide.Buy ? -1 : 1) * priceTickSize;
            } while (dDepth > 0);
            return depthDifferenceNearSpread;
        }
        
        /// <summary>
        /// Check of order is a consistent canceled order 
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        private static void TestIfEventIsConsistentCanceledOrder(LobEvent order)
        {
            var initialState = order.InitialState;
            var finalState = order.FinalState;

            // Cancel buy order: price <= best bid price
            Assert.False(order.Side == MarketSide.Buy && !(order.Price <= initialState.BestBidPrice),
                $"Cancel buy order: '{order.OrderId} (t={order.Time})' failed test: price <= best bid price");

            // Cancel sell order: price <= best bid price
            Assert.False(order.Side == MarketSide.Sell && !(order.Price >= initialState.BestAskPrice),
                $"Cancel sell order: '{order.OrderId} (t={order.Time})' failed test: price >= best ask price");

            // Was order really cancelled?
            if (order.Side == MarketSide.Buy && order.Price <= initialState.BestBidPrice || 
               (order.Side == MarketSide.Sell && order.Price >= initialState.BestAskPrice))
            {
                var initialDepth = initialState.Depth(order.Price, order.Side);
                var finalDepth = finalState.Depth(order.Price, order.Side);

                Assert.True(initialDepth - finalDepth == order.Volume,
                    $"Cancel order: '{order.OrderId} (t={order.Time})' failed test: depth(price,t-) - depth(price,t+) == volume");
            }            
        }

        /// <summary>
        /// Check if the given event is a consistent limit order
        /// </summary>
        /// <returns></returns>
        private static void TestIfEventIsConsistentLimitOrder(LobEvent order)
        {
            var initialState = order.InitialState;
            var finalState = order.FinalState;

            // Check if order price is non-positive
            Assert.True(order.Price > 0, 
                        $"The price of limit order '{order.OrderId} (t={order.Time})' is not positive");
            
            // Check if order volume is non-positive
            Assert.True(order.Volume > 0,
                        $"The volume of limit order '{order.OrderId} (t={order.Time})' is not positive");
            
            // Sell limit order: price > best bid
            Assert.False(order.Side == MarketSide.Sell && !(order.Price > initialState.BestBidPrice), 
                $"'{order.OrderId} (t={order.Time})' failed test: sell limit order: Price = {order.Price} > Best Bid = {initialState.BestBidPrice}");

            // Buy limit order: price < best ask
            Assert.False(order.Side == MarketSide.Buy && !(order.Price < initialState.BestAskPrice),
                $"'{order.OrderId} (t={order.Time})' failed test: buy limit order: Price = {order.Price} < Best ask = {initialState.BestAskPrice}");
            
            // Limit order: final depth - intial depth = order volume

            var intialDepth = initialState.Depth(order.Price, order.Side);
            var finalDepth = finalState.Depth(order.Price, order.Side);

            Assert.True(finalDepth - intialDepth == order.Volume, 
                $"'{order.OrderId} (t={order.Time})' failed test: final depth - intial depth = order volume");
        }

        /// <summary>
        /// Check if given order is a market order 
        /// </summary>
        /// <returns></returns>
        private static void TestIfEventIsConsistentMarketOrder(LobEvent order, int priceTickSize)
        {
            var initialState = order.InitialState;
            var finalState = order.FinalState;

            // Check if order volume is non-positive
            Assert.True(order.Volume > 0, 
                $"The volume of limit order '{order.OrderId} (t={order.Time})' is not positive");
            
            // Buy market order: price = best ask price
            Assert.False(order.Side == MarketSide.Buy && order.Price != initialState.BestBidPrice,
                $"'{order.OrderId} (t={order.Time})' failed test: sell market order: Price = {order.Price} == Best bid = {initialState.BestBidPrice}");

            // Buy market order: initial total bid volume - final total bid volume = order volume
            if (order.Side == MarketSide.Buy && order.Price == initialState.BestBidPrice)
            {
                // The following lines below wond help, as boundary effects can take place due
                // the finite number of levels observed. 
                //var initialTotalVolume = initialState.BidVolume.Sum();
                //var finalTotalVolume = finalState.BidVolume.Sum();
                // Depth difference near spread on buy side (BestBidPrice)
                var depthDifference = DepthDifferenceNearSpread(order.Side, initialState, finalState, priceTickSize);
                Assert.True(depthDifference == order.Volume, 
                    $"'{order.OrderId} (t={order.Time})' failed test: sell market order: initial total bid volume - final total bid volume = order volume");
            }
          
             // Sell market order: price = best bid price
            // Order.Side means which side of the LOB is addressed, if the sell side is addressed
            // the order is a buy order 
            Assert.False(order.Side == MarketSide.Sell && order.Price != initialState.BestAskPrice, 
                $"'{order.OrderId} (t={order.Time})' failed test: buy market order: Price = {order.Price} == Best ask = {initialState.BestBidPrice}");

            // Sell market order: initial total ask volume - final total ask volume = order volume
            if (order.Side == MarketSide.Sell && order.Price == initialState.BestAskPrice)
            {
                // Depth difference near spread on sell side (BestAskPrice)
                var depthDifference = DepthDifferenceNearSpread(order.Side, initialState, finalState, priceTickSize);
                Assert.True(depthDifference == order.Volume,
                    $"'{order.OrderId} (t={order.Time})' failed test: buy market order: initial total ask volume - final total ask volume = order volume");
            }            
        }

        private static void TestConsistencyOfTradingData(LobTradingData tradingData)
        {
            var priceTickSize = tradingData.PriceTickSize;

            #region Test time consistency
            
            /*var events = tradingData.Events;
            Assert.True(events.Select(p => p.Time).Count() ==
                        events.Select(p => p.Time).Distinct().Count(), 
                $"There are {events.GroupBy(p => p.Time).Count(g => g.Count() > 1)} different time goups");*/
            
            #endregion Test time consistency

            #region  Test order events

            tradingData.LimitOrders.ForEach(TestIfEventIsConsistentLimitOrder);
            tradingData.MarketOrders.ForEach(p => TestIfEventIsConsistentMarketOrder(p, priceTickSize));
            tradingData.CanceledOrders.ForEach(TestIfEventIsConsistentCanceledOrder);

            #endregion
        }

        /// <summary>
        /// Test repository with LOB data for given symbol 
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="level"></param>
        /// <param name="tradingDates"></param>
        private static void TestRepository(string symbol, int level, List<DateTime> tradingDates)
        {
            var repository = new LobRepository(symbol, level, tradingDates);            
            if (tradingDates.Any())
            {
                Assert.True(repository.TradingData.Any());
            }

            Assert.AreEqual(repository.TradingDays.Count, 
                            repository.TradingData.Keys.Count);

            // Test consistency of trading data 
            foreach (var tradingDay in repository.TradingDays)
            {
                var tradingData = repository.TradingData[tradingDay];
                TestConsistencyOfTradingData(tradingData);
            }
        }

        [TestCase("2016-01-04")]
        [TestCase("2016-01-05")]
        [TestCase("2016-01-06")]
        [TestCase("2016-01-11")]
        [TestCase("2016-01-12")]
        [TestCase("2016-01-13")]
        [TestCase("2016-01-14")]
        [TestCase("2016-01-15")]
        [TestCase("2016-01-19")]
        [TestCase("2016-01-20")]
        [TestCase("2016-01-21")]
        [TestCase("2016-01-22")]
        [TestCase("2016-01-25")]
        [TestCase("2016-01-26")]
        [TestCase("2016-01-27")]
        [TestCase("2016-01-28")]
        [TestCase("2016-01-29")]
        [TestCase("2016-02-01")]
        [TestCase("2016-02-02")]
        [TestCase("2016-02-03")]
        [TestCase("2016-02-04")]
        [TestCase("2016-02-05")]
        [TestCase("2016-02-08")]
        [TestCase("2016-02-09")]
        [TestCase("2016-02-10")]
        [TestCase("2016-02-11")]
        [TestCase("2016-02-12")]
        [TestCase("2016-02-16")]
        [TestCase("2016-02-17")]
        [TestCase("2016-02-18")]
        [TestCase("2016-02-19")]
        [TestCase("2016-02-22")]
        [TestCase("2016-02-23")]
        [TestCase("2016-02-24")]
        [TestCase("2016-02-25")]
        [TestCase("2016-02-26")]
        [TestCase("2016-02-29")]
        [TestCase("2016-03-01")]
        [TestCase("2016-03-02")]
        [TestCase("2016-03-03")]
        [TestCase("2016-03-04")]
        [TestCase("2016-03-07")]
        [TestCase("2016-03-08")]
        [TestCase("2016-03-04")]
        [TestCase("2016-03-09")]
        [TestCase("2016-03-10")]
        [TestCase("2016-03-11")]
        [TestCase("2016-03-14")]
        [TestCase("2016-03-15")]
        [TestCase("2016-03-16")]
        [TestCase("2016-03-17")]
        [TestCase("2016-03-18")]
        [TestCase("2016-03-21")]
        [TestCase("2016-03-22")]
        [TestCase("2016-03-23")]
        [TestCase("2016-03-24")]
        [TestCase("2016-03-28")]
        [TestCase("2016-03-29")]
        [TestCase("2016-03-30")]
        [TestCase("2016-03-31")]
        public void TestAmazonLobRepository(string tradeDay)
        {
            const int level = 10;
            const string smybol = "AMZN";
            var tradingDates = new List<DateTime>{ DateTime.ParseExact(tradeDay, "yyyy-MM-dd", CultureInfo.InvariantCulture) };

            TestRepository(smybol, level, tradingDates);
        }

        [TestCase("2016-01-04")]
        [TestCase("2016-01-05")]
        [TestCase("2016-01-06")]
        [TestCase("2016-01-11")]
        [TestCase("2016-01-12")]
        [TestCase("2016-01-13")]
        [TestCase("2016-01-14")]
        [TestCase("2016-01-15")]
        [TestCase("2016-01-19")]
        [TestCase("2016-01-20")]
        [TestCase("2016-01-21")]
        [TestCase("2016-01-22")]
        [TestCase("2016-01-25")]
        [TestCase("2016-01-26")]
        [TestCase("2016-01-27")]
        [TestCase("2016-01-28")]
        [TestCase("2016-01-29")]
        [TestCase("2016-02-01")]
        [TestCase("2016-02-02")]
        [TestCase("2016-02-03")]
        [TestCase("2016-02-04")]
        [TestCase("2016-02-05")]
        [TestCase("2016-02-08")]
        [TestCase("2016-02-09")]
        [TestCase("2016-02-10")]
        [TestCase("2016-02-11")]
        [TestCase("2016-02-12")]
        [TestCase("2016-02-16")]
        [TestCase("2016-02-17")]
        [TestCase("2016-02-18")]
        [TestCase("2016-02-19")]
        [TestCase("2016-02-22")]
        [TestCase("2016-02-23")]
        [TestCase("2016-02-24")]
        [TestCase("2016-02-25")]
        [TestCase("2016-02-26")]
        [TestCase("2016-02-29")]
        [TestCase("2016-03-01")]
        [TestCase("2016-03-02")]
        [TestCase("2016-03-03")]
        [TestCase("2016-03-04")]
        [TestCase("2016-03-07")]
        [TestCase("2016-03-08")]
        [TestCase("2016-03-04")]
        [TestCase("2016-03-09")]
        [TestCase("2016-03-10")]
        [TestCase("2016-03-11")]
        [TestCase("2016-03-14")]
        [TestCase("2016-03-15")]
        [TestCase("2016-03-16")]
        [TestCase("2016-03-17")]
        [TestCase("2016-03-18")]
        [TestCase("2016-03-21")]
        [TestCase("2016-03-22")]
        [TestCase("2016-03-23")]
        [TestCase("2016-03-24")]
        [TestCase("2016-03-28")]
        [TestCase("2016-03-29")]
        [TestCase("2016-03-30")]
        [TestCase("2016-03-31")]
        public void TestCiscoLobRepository(string tradeDay)
        {
            const int level = 10;
            const string smybol = "CSCO";
            var tradingDates = new List<DateTime> { DateTime.ParseExact(tradeDay, "yyyy-MM-dd", CultureInfo.InvariantCulture) };

            TestRepository(smybol, level, tradingDates);
        }

        [TestCase("2016-01-04")]
        [TestCase("2016-01-05")]
        [TestCase("2016-01-06")]
        [TestCase("2016-01-11")]
        [TestCase("2016-01-12")]
        [TestCase("2016-01-13")]
        [TestCase("2016-01-14")]
        [TestCase("2016-01-15")]
        [TestCase("2016-01-19")]
        [TestCase("2016-01-20")]
        [TestCase("2016-01-21")]
        [TestCase("2016-01-22")]
        [TestCase("2016-01-25")]
        [TestCase("2016-01-26")]
        [TestCase("2016-01-27")]
        [TestCase("2016-01-28")]
        [TestCase("2016-01-29")]
        [TestCase("2016-02-01")]
        [TestCase("2016-02-02")]
        [TestCase("2016-02-03")]
        [TestCase("2016-02-04")]
        [TestCase("2016-02-05")]
        [TestCase("2016-02-08")]
        [TestCase("2016-02-09")]
        [TestCase("2016-02-10")]
        [TestCase("2016-02-11")]
        [TestCase("2016-02-12")]
        [TestCase("2016-02-16")]
        [TestCase("2016-02-17")]
        [TestCase("2016-02-18")]
        [TestCase("2016-02-19")]
        [TestCase("2016-02-22")]
        [TestCase("2016-02-23")]
        [TestCase("2016-02-24")]
        [TestCase("2016-02-25")]
        [TestCase("2016-02-26")]
        [TestCase("2016-02-29")]
        [TestCase("2016-03-01")]
        [TestCase("2016-03-02")]
        [TestCase("2016-03-03")]
        [TestCase("2016-03-04")]
        [TestCase("2016-03-07")]
        [TestCase("2016-03-08")]
        [TestCase("2016-03-04")]
        [TestCase("2016-03-09")]
        [TestCase("2016-03-10")]
        [TestCase("2016-03-11")]
        [TestCase("2016-03-14")]
        [TestCase("2016-03-15")]
        [TestCase("2016-03-16")]
        [TestCase("2016-03-17")]
        [TestCase("2016-03-18")]
        [TestCase("2016-03-21")]
        [TestCase("2016-03-22")]
        [TestCase("2016-03-23")]
        [TestCase("2016-03-24")]
        [TestCase("2016-03-28")]
        [TestCase("2016-03-29")]
        [TestCase("2016-03-30")]
        [TestCase("2016-03-31")]
        public void TestNetflixLobRepository(string tradeDay)
        {
            const int level = 10;
            const string smybol = "NFLX";
            var tradingDates = new List<DateTime> { DateTime.ParseExact(tradeDay, "yyyy-MM-dd", CultureInfo.InvariantCulture) };

            TestRepository(smybol, level, tradingDates);
        }

        [TestCase("2016-01-04")]
        [TestCase("2016-01-05")]
        [TestCase("2016-01-06")]
        [TestCase("2016-01-11")]
        [TestCase("2016-01-12")]
        [TestCase("2016-01-13")]
        [TestCase("2016-01-14")]
        [TestCase("2016-01-15")]
        [TestCase("2016-01-19")]
        [TestCase("2016-01-20")]
        [TestCase("2016-01-21")]
        [TestCase("2016-01-22")]
        [TestCase("2016-01-25")]
        [TestCase("2016-01-26")]
        [TestCase("2016-01-27")]
        [TestCase("2016-01-28")]
        [TestCase("2016-01-29")]
        [TestCase("2016-02-01")]
        [TestCase("2016-02-02")]
        [TestCase("2016-02-03")]
        [TestCase("2016-02-04")]
        [TestCase("2016-02-05")]
        [TestCase("2016-02-08")]
        [TestCase("2016-02-09")]
        [TestCase("2016-02-10")]
        [TestCase("2016-02-11")]
        [TestCase("2016-02-12")]
        [TestCase("2016-02-16")]
        [TestCase("2016-02-17")]
        [TestCase("2016-02-18")]
        [TestCase("2016-02-19")]
        [TestCase("2016-02-22")]
        [TestCase("2016-02-23")]
        [TestCase("2016-02-24")]
        [TestCase("2016-02-25")]
        [TestCase("2016-02-26")]
        [TestCase("2016-02-29")]
        [TestCase("2016-03-01")]
        [TestCase("2016-03-02")]
        [TestCase("2016-03-03")]
        [TestCase("2016-03-04")]
        [TestCase("2016-03-07")]
        [TestCase("2016-03-08")]
        [TestCase("2016-03-04")]
        [TestCase("2016-03-09")]
        [TestCase("2016-03-10")]
        [TestCase("2016-03-11")]
        [TestCase("2016-03-14")]
        [TestCase("2016-03-15")]
        [TestCase("2016-03-16")]
        [TestCase("2016-03-17")]
        [TestCase("2016-03-18")]
        [TestCase("2016-03-21")]
        [TestCase("2016-03-22")]
        [TestCase("2016-03-23")]
        [TestCase("2016-03-24")]
        [TestCase("2016-03-28")]
        [TestCase("2016-03-29")]
        [TestCase("2016-03-30")]
        [TestCase("2016-03-31")]
        public void TestTeslaLobRepository(string tradeDay)
        {
            const int level = 10;
            const string smybol = "TSLA";
            var tradingDates = new List<DateTime> { DateTime.ParseExact(tradeDay, "yyyy-MM-dd", CultureInfo.InvariantCulture) };

            TestRepository(smybol, level, tradingDates);
        }
    }
}
