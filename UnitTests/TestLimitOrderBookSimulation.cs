﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using LimitOrderBookRepositories.Model;
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

        private SortedDictionary<int, int> SellSide { set; get; }
        private SortedDictionary<int, int> BuySide { set; get; }

        private const int Spread = 10;
        private const int BuyMinPriceTick = 100;
        private const int BuyMaxPriceTick = BuyMinPriceTick + 200;
        private const int SellMinPriceTick = BuyMaxPriceTick + Spread;
        private const int SellMaxPriceTick = SellMinPriceTick + 200;
        private const int BuyMaxDepth = 100;
        private const int SellMaxDepth = 100;

        #endregion

        private string WorkFolder { set; get; }

        [SetUp]
        public void Init()
        {
            WorkFolder = ConfigurationManager.AppSettings["WorkFolder"];
            SellSide = new SortedDictionary<int, int>();
            BuySide = new SortedDictionary<int, int>();

            for (var price = BuyMinPriceTick; price <= BuyMaxPriceTick; price++)
            {
                BuySide.Add(price, GetDepth(p: price, 
                                            pmin: BuyMaxPriceTick, 
                                            pmax: BuyMinPriceTick, 
                                            scale: BuyMaxDepth));
            }

            for (var price = SellMinPriceTick; price <= SellMaxPriceTick; price++)
            {
                SellSide.Add(price, GetDepth(p: price, 
                                             pmin: SellMinPriceTick, 
                                             pmax: SellMaxPriceTick, 
                                             scale: SellMaxDepth));
            }
        }
        
        /**
         * Depth profile on [pmin, pmax] that more ore less resemble the reality
         */
        private static int GetDepth(int p, int pmin, int pmax, int scale)
        {
            var x01 = (p - pmin) / (double)(pmax - pmin);
            
            const double lambda = 5e-3;
            const double fmax = 0.07512;
            
            var f = Math.Exp(x01 * Math.Log(lambda) - lambda) / SpecialFunctions.Gamma(x01);
            
            return Math.Max((int) (scale * f / fmax), 1);
        }

        /**
         * Generate a pre-initialized limit order book
         */
        private LimitOrderBook GenerateLimitOrderBook()
        {
            var buySide = new SortedDictionary<int, int>();
            foreach (var pair in BuySide)
            {
                buySide.Add(pair.Key, pair.Value);
            }
            var sellSide = new SortedDictionary<int, int>();
            foreach (var pair in SellSide)
            {
                sellSide.Add(pair.Key, pair.Value);
            }
            var lob = new LimitOrderBook {Time = 0};
            
            lob.InitializeDepthProfileBuySide(buySide);
            lob.InitializeDepthProfileSellSide(sellSide);
            
            return lob;
        }

        /// <summary>
        /// Generate model with sythetic parameters 
        /// </summary>
        /// <returns></returns>
        private SmithFarmerModel GenerateSmithFarmerModel(double muC = 0.05, int seed=42)
        {
            // Choose parameter such that certain end result is achieved 
            // Check characteristic scales 
            const double asymtoticDepth = 0.5 * (BuyMaxDepth + SellMaxDepth);
            var muL = asymtoticDepth * muC;
            var muM = Spread * muL * 2;

            var parameter = new SmithFarmerModelParameter
            {
                Seed = seed,
                CancellationRate = muC,
                MarketOrderRate = muM,
                LimitOrderRateDensity = muL,
                SimulationIntervalSize = Spread * 4,
                CharacteristicOrderSize = 10.3,
                PriceTickSize = 5.6,
            };
            
            return new SmithFarmerModel
            {
                Parameter = parameter,
                LimitOrderBook =  GenerateLimitOrderBook()
            };
        }

        [Test]
        public void TestSmithFarmerModel()
        {
            var workFolder = ConfigurationManager.AppSettings["WorkFolder"];
            
            var model = GenerateSmithFarmerModel();
            
            model.SaveDepthProfile(Path.Combine(workFolder, "depth_start.csv"));
            
            model.SimulateOrderFlow(duration: 1000);
            
            // Save simulation result for further inspection in e.g. Matlab
            model.SavePriceProcess(Path.Combine(workFolder, "price_process.csv"));
            model.SaveDepthProfile(Path.Combine(workFolder, "depth_end.csv"));
            
            SharedUtilities.SaveAsJson(model.LimitOrderBook.Counter, Path.Combine(workFolder, "counter.json"));
            SharedUtilities.SaveAsJson(model.Parameter, Path.Combine(workFolder, "model_parameter.json"));
            
            // Do some plausibility checks
            var lob = model.LimitOrderBook;
            
            Assert.True(lob.Counter[LimitOrderBookEvent.SubmitMarketSellOrder] > 0);
            Assert.True(lob.Counter[LimitOrderBookEvent.SubmitMarketBuyOrder] > 0);
            Assert.True(lob.Counter[LimitOrderBookEvent.SubmitLimitSellOrder] > 0);
            Assert.True(lob.Counter[LimitOrderBookEvent.SubmitLimitBuyOrder] > 0);
            Assert.True(lob.Counter[LimitOrderBookEvent.CancelLimitSellOrder] > 0);
            Assert.True(lob.Counter[LimitOrderBookEvent.CancelLimitBuyOrder] > 0);
            
            // Make sure that all depths are positive 
            Assert.True(lob.Ask > lob.Bid, "Ask must be greater than bid price");
            
            Assert.True(lob.PriceTimeSeries.Any(), "No events where triggered");
            
            Assert.True(lob.PriceTimeSeries
                .Select(p => p.Value)
                .All(p => p.Ask > p.Bid), "Ask must be greater than bid price");          
        }

        [TestCase(0.08)]
        [TestCase(0.06)]
        [TestCase(0.05)]
        [TestCase(0.04)]
        [TestCase(0.03)]
        [TestCase(0.02)]
        public void TestCalibrationOfSmithFarmerModel(double cancellationRate)
        {
            const double duration = 1000.0;
            const double percent = 0.01;
            const double relativeTolerance = 5 * percent;

            #region Simulate events

            var model = GenerateSmithFarmerModel(cancellationRate);
            model.SimulateOrderFlow(duration);
            
            var parameter = model.Parameter;           
            var tradingData = model.LimitOrderBook.TradingData;

            const double probabilityLow  = 0.01;
            const double probabilityHigh = 0.80;
            
            // Estimate parameter from simulated trading data 
            // within a narrow band near the spread (given by the 1% and 80% quantile)            
            var estimated = SmithFarmerModelCalibration.Calibrate(tradingData, probabilityLow, probabilityHigh);

            #endregion
            
            var relativeErrorMarketOrderRate = 1 - estimated.MarketOrderRate / parameter.MarketOrderRate; 
            Assert.True(Math.Abs(relativeErrorMarketOrderRate) < relativeTolerance,
                $"Market order rate: {parameter.MarketOrderRate}, estimated rate: {estimated.MarketOrderRate} ({relativeErrorMarketOrderRate * 100}%)");

            var relativeErrorLimitOrderRateDensity = 1 - estimated.LimitOrderRateDensity / parameter.LimitOrderRateDensity; 
            Assert.True(Math.Abs(relativeErrorLimitOrderRateDensity) < relativeTolerance,
                $"Limit order rate: {parameter.LimitOrderRateDensity}, estimated rate: {estimated.LimitOrderRateDensity} ({relativeErrorLimitOrderRateDensity * 100}%)");

            var relativeErrorCancelationRate = 1 - estimated.CancellationRate / parameter.CancellationRate;           
            Assert.True(Math.Abs(relativeErrorCancelationRate) < relativeTolerance,
                $"Cancellation rate: { parameter.CancellationRate}, estimated rate: {estimated.CancellationRate} ({relativeErrorCancelationRate * 100}%)");            
        }

        [Test]
        public void TestSubmitMarketBuyOrderEvent()
        {
            var model = GenerateSmithFarmerModel();

            var ask = model.LimitOrderBook.Ask;
            var depth = model.LimitOrderBook.GetDepthAtPriceTick(ask);
            
            var price = model.SubmitMarketBuyOrder();

            var measuredDepth = model.LimitOrderBook.GetDepthAtPriceTick(price);
            var expectedDepth = depth - 1;
            
            Assert.True(price == ask);
            Assert.True(measuredDepth == expectedDepth);
        }
        
        [Test]
        public void TestSubmitMarketSellOrderEvent()
        {
            var model = GenerateSmithFarmerModel();

            var bid = model.LimitOrderBook.Bid;
            var depth = model.LimitOrderBook.GetDepthAtPriceTick(bid);
            
            var price = model.SubmitMarketSellOrder();

            var measuredDepth = model.LimitOrderBook.GetDepthAtPriceTick(price);
            var expectedDepth = depth - 1;
            
            Assert.True(price == bid);
            Assert.True(measuredDepth == expectedDepth);
        }
        
        [Test]
        public void TestSubmitLimitBuyOrderEvent()
        {
            var model = GenerateSmithFarmerModel();
            for (var i = 0; i < 1000; i++)
            {
                var ask = model.LimitOrderBook.Ask;
                var price = model.SubmitLimitBuyOrder();
                
                Assert.True(price >= ask - 1 - model.Parameter.SimulationIntervalSize && 
                            price <= ask - 1);     
            }
        }
        
        [Test]
        public void TestSubmitLimitSellOrderEvent()
        {
            var model = GenerateSmithFarmerModel();

            for (var i = 0; i < 1000; i++)
            {
                var bid = model.LimitOrderBook.Bid;
                var price = model.SubmitLimitSellOrder();
                
                Assert.True(price >= bid + 1 && 
                            price <= bid + 1 + model.Parameter.SimulationIntervalSize);     
            }
        }
        
        [Test]
        public void TestCancelLimitBuyOrderEvent()
        {
            var model = GenerateSmithFarmerModel();

            for (var i = 0; i < 1000; i++)
            {
                var bid = model.LimitOrderBook.Bid;
                var price = model.CancelLimitBuyOrder();
                    
                Assert.True(price >= bid - model.Parameter.SimulationIntervalSize && 
                            price <= bid);     
            }
        }
        
        [Test]
        public void TestCancelLimitSellOrderEvent()
        {
            var model = GenerateSmithFarmerModel();

            for (var i = 0; i < 1000; i++)
            {
                var ask = model.LimitOrderBook.Ask;
                var price = model.CancelLimitSellOrder();
                    
                Assert.True(price >= ask && 
                            price <= ask + model.Parameter.SimulationIntervalSize);     
            }
        }
    }
}