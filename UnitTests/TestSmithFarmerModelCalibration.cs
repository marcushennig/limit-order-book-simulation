﻿using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using LimitOrderBookRepositories;
using LimitOrderBookRepositories.Model;
using LimitOrderBookSimulation.EventModels;
using LimitOrderBookUtilities;
using NUnit.Framework;

namespace UnitTests
{
    [TestFixture]
    public class TestSmithFarmerModelCalibration
    {
        private string WorkFolder { set; get; }

        [SetUp]
        public void Init()
        {
            // Get data from app.config
            WorkFolder = ConfigurationManager.AppSettings["WorkFolder"];
            Assert.True(Directory.Exists(WorkFolder));
        }

        [TestCase("2016-01-04", "AMZN", 10000)]
        [TestCase("2016-01-05", "AMZN", 10000)]
        [TestCase("2016-01-06", "AMZN", 10000)]
        [TestCase("2016-01-11", "AMZN", 10000)]
        [TestCase("2016-01-12", "AMZN", 10000)]
        [TestCase("2016-01-13", "AMZN", 10000)]
        [TestCase("2016-01-14", "AMZN", 10000)]
        [TestCase("2016-01-15", "AMZN", 10000)]
        [TestCase("2016-01-19", "AMZN", 10000)]
        [TestCase("2016-01-20", "AMZN", 10000)]
        [TestCase("2016-01-21", "AMZN", 10000)]
        [TestCase("2016-01-22", "AMZN", 10000)]
        [TestCase("2016-01-25", "AMZN", 10000)]
        [TestCase("2016-01-26", "AMZN", 10000)]
        [TestCase("2016-01-27", "AMZN", 10000)]
        [TestCase("2016-01-28", "AMZN", 10000)]
        [TestCase("2016-01-29", "AMZN", 10000)]
        [TestCase("2016-02-01", "AMZN", 10000)]
        [TestCase("2016-02-02", "AMZN", 10000)]
        [TestCase("2016-02-03", "AMZN", 10000)]
        [TestCase("2016-02-04", "AMZN", 10000)]
        [TestCase("2016-02-05", "AMZN", 10000)]
        [TestCase("2016-02-08", "AMZN", 10000)]
        [TestCase("2016-02-09", "AMZN", 10000)]
        [TestCase("2016-02-10", "AMZN", 10000)]
        [TestCase("2016-02-11", "AMZN", 10000)]
        [TestCase("2016-02-12", "AMZN", 10000)]
        [TestCase("2016-02-16", "AMZN", 10000)]
        [TestCase("2016-02-17", "AMZN", 10000)]
        [TestCase("2016-02-18", "AMZN", 10000)]
        [TestCase("2016-02-19", "AMZN", 10000)]
        [TestCase("2016-02-22", "AMZN", 10000)]
        [TestCase("2016-02-23", "AMZN", 10000)]
        [TestCase("2016-02-24", "AMZN", 10000)]
        [TestCase("2016-02-25", "AMZN", 10000)]
        [TestCase("2016-02-26", "AMZN", 10000)]
        [TestCase("2016-02-29", "AMZN", 10000)]
        [TestCase("2016-03-01", "AMZN", 10000)]
        [TestCase("2016-03-02", "AMZN", 10000)]
        [TestCase("2016-03-03", "AMZN", 10000)]
        [TestCase("2016-03-04", "AMZN", 10000)]
        [TestCase("2016-03-07", "AMZN", 10000)]
        [TestCase("2016-03-08", "AMZN", 10000)]
        [TestCase("2016-03-09", "AMZN", 10000)]
        [TestCase("2016-03-10", "AMZN", 10000)]
        [TestCase("2016-03-11", "AMZN", 10000)]
        [TestCase("2016-03-14", "AMZN", 10000)]
        [TestCase("2016-03-15", "AMZN", 10000)]
        [TestCase("2016-03-16", "AMZN", 10000)]
        [TestCase("2016-03-17", "AMZN", 10000)]
        [TestCase("2016-03-18", "AMZN", 10000)]
        [TestCase("2016-03-21", "AMZN", 10000)]
        [TestCase("2016-03-22", "AMZN", 10000)]
        [TestCase("2016-03-23", "AMZN", 10000)]
        [TestCase("2016-03-24", "AMZN", 10000)]
        [TestCase("2016-03-28", "AMZN", 10000)]
        [TestCase("2016-03-29", "AMZN", 10000)]
        [TestCase("2016-03-30", "AMZN", 10000)]
        [TestCase("2016-03-31", "AMZN", 10000)]
        public void TestCalibration(string tradingDateString,
            string symbol,
            double duration)
        {
            var tradingDate = DateTime.ParseExact(tradingDateString, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            const double percent = 1e-2;
            const double relativeTolerance = 2 * percent;

            #region Create test folder

            var testFolder = Path.Combine(WorkFolder, $"Simulation_{symbol}_{tradingDate:yyyyMMdd}");
            Directory.CreateDirectory(testFolder);
            Assert.True(Directory.Exists(testFolder), $"The test folder  {testFolder} could not be created");

            #endregion

            #region Load trading data and save statistics

            const int level = 10;
            var tradingDates = new List<DateTime> {tradingDate};
            var repository = new LobRepository(symbol, level, tradingDates);
            var tradingData = repository.TradingData[tradingDate];

            // Save statistics about LOB data 
            tradingData.LimitOrderDistribution.Save(
                Path.Combine(testFolder, "tradingData_limit_order_distribution.csv"));
            tradingData.LimitSellOrderDistribution.Save(Path.Combine(testFolder,
                "tradingData_limit_sell_order_distribution.csv"));
            tradingData.LimitBuyOrderDistribution.Save(Path.Combine(testFolder,
                "tradingData_limit_buy_order_distribution.csv"));
            tradingData.CanceledOrderDistribution.Save(Path.Combine(testFolder,
                "tradingData_canceled_order_distribution.csv"));
            tradingData.CanceledSellOrderDistribution.Save(Path.Combine(testFolder,
                "tradingData_canceled_sell_order_distribution.csv"));
            tradingData.CanceledBuyOrderDistribution.Save(Path.Combine(testFolder,
                "tradingData_canceled_buy_order_distribution.csv"));
            tradingData.AverageDepthProfile.Save(Path.Combine(testFolder,
                "tradingData_outstanding_limit_order_distribution.csv"));
            tradingData.SavePriceProcess(Path.Combine(testFolder, "tradingData_price.csv"));

            #endregion

            #region Calibrate Smith-Farmer model

            // Calibrate Smith-Farmer model within a narrow price-band near
            // the spread (given by lower and upper price quantile). For this purpose 
            // we use the time-averaged depth profile (depth vs. distance to best opposite quote) 
            const double lowerProbability = 0.01;
            const double upperProbability = 0.80;

            var averageDepthProfile = tradingData.AverageDepthProfile;
            var lowerQuantile = averageDepthProfile.Quantile(lowerProbability);
            var upperQuantile = averageDepthProfile.Quantile(upperProbability);

            var measuredProbability = averageDepthProfile.Probability
                .Where(p => lowerQuantile <= p.Key &&
                            p.Key <= upperQuantile)
                .Select(p => p.Value)
                .Sum();

            const double probability = upperProbability - lowerProbability;
            var relativeError = 1 - measuredProbability / probability;
            Assert.True(Math.Abs(relativeError) < relativeTolerance,
                $"Probability: {probability} Measured: {measuredProbability} ({relativeError * 100}%)");

            var calibratedParameters =
                SmithFarmerModelCalibration.Calibrate(tradingData, lowerProbability, upperProbability);

            SharedUtilities.SaveAsJson(calibratedParameters, Path.Combine(testFolder, "model_parameter.json"));
            averageDepthProfile.Save(Path.Combine(testFolder, "model_average_depth_profile.csv"));

            #endregion

            #region Initial state of LOB

            // Problematic part, as scaling with characteristic order size 
            // results in very low depth profiles, any suggestion here from M. Gould  
            var initalState = tradingData.States.First();

            var initialBids = initalState.Bids
                .ToDictionary(p => (int) (p.Key / calibratedParameters.PriceTickSize),
                    p => (int) Math.Ceiling(p.Value / calibratedParameters.CharacteristicOrderSize));

            var initialAsks = initalState.Asks
                .ToDictionary(p => (int) (p.Key / calibratedParameters.PriceTickSize),
                    p => (int) Math.Ceiling(p.Value / calibratedParameters.CharacteristicOrderSize));

            var initialSpread = (int) (initalState.Spread / calibratedParameters.PriceTickSize);

            #endregion

            #region Simulate order flow

            // Choose narrow simulation interval in order of the spread
            var simulationIntervalSize = 4 * initialSpread;

            var model = new SmithFarmerModel(calibratedParameters,
                initialBids,
                initialAsks,
                simulationIntervalSize);

            model.LimitOrderBook.SaveDepthProfile(Path.Combine(testFolder, "model_initial_depth_profile.csv"));
            model.LimitOrderBook.SaveDepthProfileBuySide(
                Path.Combine(testFolder, "model_initial_depth_profile_buy.csv"));
            model.LimitOrderBook.SaveDepthProfileSellSide(Path.Combine(testFolder,
                "model_initial_depth_profile_sell.csv"));

            model.SimulateOrderFlow(duration);

            model.LimitOrderBook.SaveDepthProfile(Path.Combine(testFolder, "model_final_depth_profile.csv"));
            model.LimitOrderBook.SaveDepthProfileBuySide(Path.Combine(testFolder, "model_final_depth_profile_buy.csv"));
            model.LimitOrderBook.SaveDepthProfileSellSide(
                Path.Combine(testFolder, "model_final_depth_profile_sell.csv"));

            model.SavePriceProcess(Path.Combine(testFolder, "model_price.csv"));

            SharedUtilities.SaveAsJson(model, Path.Combine(testFolder, "model.json"));

            #endregion
        }

        /// <summary>
        ///  Calibrate model using an in-sample set and trying to predict the
        /// LOB price behavior of the out-of-sample LOB price data 
        /// </summary>
        /// <param name="tradingDateString"></param>
        /// <param name="symbol"></param>
        /// <param name="duration"></param>
        /// <param name="maxPathNumber"></param>
        // Amazon
        [TestCase("2016-01-04", "AMZN", 10000, 10)]
        [TestCase("2016-01-05", "AMZN", 10000, 10)]
        [TestCase("2016-01-06", "AMZN", 10000, 10)]
        [TestCase("2016-01-11", "AMZN", 10000, 10)]
        [TestCase("2016-01-12", "AMZN", 10000, 10)]
        [TestCase("2016-01-13", "AMZN", 10000, 10)]
        [TestCase("2016-01-14", "AMZN", 10000, 10)]
        [TestCase("2016-01-15", "AMZN", 10000, 10)]
        [TestCase("2016-01-19", "AMZN", 10000, 10)]
        [TestCase("2016-01-20", "AMZN", 10000, 10)]
        [TestCase("2016-01-21", "AMZN", 10000, 10)]
        [TestCase("2016-01-22", "AMZN", 10000, 10)]
        [TestCase("2016-01-25", "AMZN", 10000, 10)]
        [TestCase("2016-01-26", "AMZN", 10000, 10)]
        [TestCase("2016-01-27", "AMZN", 10000, 10)]
        [TestCase("2016-01-28", "AMZN", 10000, 10)]
        [TestCase("2016-01-29", "AMZN", 10000, 10)]
        // Cisco TODO: Problem with calibration
        // [TestCase("2016-01-04", "CSCO", 10000, 10)]
        // [TestCase("2016-01-05", "CSCO", 10000, 10)]
        // [TestCase("2016-01-06", "CSCO", 10000, 10)]
        // [TestCase("2016-01-11", "CSCO", 10000, 10)]
        // [TestCase("2016-01-12", "CSCO", 10000, 10)]
        // [TestCase("2016-01-13", "CSCO", 10000, 10)]
        // [TestCase("2016-01-14", "CSCO", 10000, 10)]
        // [TestCase("2016-01-15", "CSCO", 10000, 10)]
        // [TestCase("2016-01-19", "CSCO", 10000, 10)]
        // [TestCase("2016-01-20", "CSCO", 10000, 10)]
        // [TestCase("2016-01-21", "CSCO", 10000, 10)]
        // [TestCase("2016-01-22", "CSCO", 10000, 10)]
        // [TestCase("2016-01-25", "CSCO", 10000, 10)]
        // [TestCase("2016-01-26", "CSCO", 10000, 10)]
        // [TestCase("2016-01-27", "CSCO", 10000, 10)]
        // [TestCase("2016-01-28", "CSCO", 10000, 10)]
        // [TestCase("2016-01-29", "CSCO", 10000, 10)]
        // Tesla
        [TestCase("2016-01-04", "TSLA", 10000, 10)]
        [TestCase("2016-01-05", "TSLA", 10000, 10)]
        [TestCase("2016-01-06", "TSLA", 10000, 10)]
        [TestCase("2016-01-11", "TSLA", 10000, 10)]
        [TestCase("2016-01-12", "TSLA", 10000, 10)]
        [TestCase("2016-01-13", "TSLA", 10000, 10)]
        [TestCase("2016-01-14", "TSLA", 10000, 10)]
        [TestCase("2016-01-15", "TSLA", 10000, 10)]
        [TestCase("2016-01-19", "TSLA", 10000, 10)]
        [TestCase("2016-01-20", "TSLA", 10000, 10)]
        [TestCase("2016-01-21", "TSLA", 10000, 10)]
        [TestCase("2016-01-22", "TSLA", 10000, 10)]
        [TestCase("2016-01-25", "TSLA", 10000, 10)]
        [TestCase("2016-01-26", "TSLA", 10000, 10)]
        [TestCase("2016-01-27", "TSLA", 10000, 10)]
        [TestCase("2016-01-28", "TSLA", 10000, 10)]
        [TestCase("2016-01-29", "TSLA", 10000, 10)]
        // Netflix
        [TestCase("2016-01-04", "NFLX", 10000, 10)]
        [TestCase("2016-01-05", "NFLX", 10000, 10)]
        [TestCase("2016-01-06", "NFLX", 10000, 10)]
        [TestCase("2016-01-11", "NFLX", 10000, 10)]
        [TestCase("2016-01-12", "NFLX", 10000, 10)]
        [TestCase("2016-01-13", "NFLX", 10000, 10)]
        [TestCase("2016-01-14", "NFLX", 10000, 10)]
        [TestCase("2016-01-15", "NFLX", 10000, 10)]
        [TestCase("2016-01-19", "NFLX", 10000, 10)]
        [TestCase("2016-01-20", "NFLX", 10000, 10)]
        [TestCase("2016-01-21", "NFLX", 10000, 10)]
        [TestCase("2016-01-22", "NFLX", 10000, 10)]
        [TestCase("2016-01-25", "NFLX", 10000, 10)]
        [TestCase("2016-01-26", "NFLX", 10000, 10)]
        [TestCase("2016-01-27", "NFLX", 10000, 10)]
        [TestCase("2016-01-28", "NFLX", 10000, 10)]
        [TestCase("2016-01-29", "NFLX", 10000, 10)]
        public void TestSamplingOfModel(string tradingDateString,
            string symbol,
            double duration,
            int maxPathNumber)
        {
            // Trading date for the calibration data set 
            var tradingDate = DateTime.ParseExact(tradingDateString, "yyyy-MM-dd", CultureInfo.InvariantCulture);

            // Folder to store all results 
            var outputFolder = Path.Combine(WorkFolder, $"{symbol}\\{tradingDate:yyyyMMdd}");

            Directory.CreateDirectory(outputFolder);
            Directory.CreateDirectory(Path.Combine(outputFolder, "Paths"));

            Assert.True(Directory.Exists(outputFolder), $"The output folder {outputFolder} could not be created");

            #region Load in- and out-of-sample trading data

            // We will simulate the LOB for a fixed time duration mutliple times (MC-Simualtion)
            // In order to test the model's forecast performance we split the trading data into
            // an in-sample period, used for the initial parameter calibration,
            // and an out-of-sample period, used to evaluate forecasting performance.
            // Forcasting horizon is given by duration 
            const int level = 10;
            var repository = new LobRepository(
                symbol,
                level,
                new List<DateTime> {tradingDate},
                skipFirstSeconds: 0,
                skipLastSeconds: duration);
            var inSampleTradingData = repository.TradingData[tradingDate];

            // Get the outsample trading data set 
            var repository2 = new LobRepository(
                symbol,
                level,
                new List<DateTime> {tradingDate},
                skipFirstSeconds: inSampleTradingData.EndTradingTime - inSampleTradingData.StartTradingTime,
                skipLastSeconds: 0);

            var outOfSampleTradingData = repository2.TradingData[tradingDate];

            // Save the price trajectory to later compute the relalized volatility signature plot  
            inSampleTradingData.SavePriceProcess(Path.Combine(outputFolder, "price_in_sample.csv"));
            outOfSampleTradingData.SavePriceProcess(Path.Combine(outputFolder, "price_out_of_sample.csv"));

            #endregion

            #region Calibrate Smith-Farmer model

            // Calibrate Smith-Farmer model within a narrow price-band near
            // the spread (given by lower and upper price quantile) with the in-sample trading data.
            const double lowerProbability = 0.01;
            const double upperProbability = 0.80;

            inSampleTradingData.AverageDepthProfile.Save(Path.Combine(outputFolder,
                "average_depth_profile_in_sample.csv"));

            var calibratedParameters = SmithFarmerModelCalibration.Calibrate(inSampleTradingData,
                lowerProbability,
                upperProbability);

            SharedUtilities.SaveAsJson(calibratedParameters, Path.Combine(outputFolder, "model_parameter.json"));

            #endregion

            #region Initial state of LOB

            // Problematic part, as scaling with characteristic order size 
            // results in very low depth profiles, any suggestion here from M. Gould?
            // Use last LOB state for intial depth profile 
            var initalState = inSampleTradingData.States.Last();
            var initialBids = initalState.Bids
                .ToDictionary(p => (int) (p.Key / calibratedParameters.PriceTickSize),
                    p => (int) Math.Ceiling(p.Value / calibratedParameters.CharacteristicOrderSize));

            var initialAsks = initalState.Asks
                .ToDictionary(p => (int) (p.Key / calibratedParameters.PriceTickSize),
                    p => (int) Math.Ceiling(p.Value / calibratedParameters.CharacteristicOrderSize));

            var initialSpread = (int) (initalState.Spread / calibratedParameters.PriceTickSize);

            #endregion

            #region Simulate mutiple price paths 

            // Choose narrow simulation interval in order of the spread
            var simulationIntervalSize = 4 * initialSpread;
            for (var pathNumber = 0; pathNumber < maxPathNumber; pathNumber++)
            {
                var model = new SmithFarmerModel(calibratedParameters,
                    initialBids,
                    initialAsks,
                    simulationIntervalSize);

                if (pathNumber == 0)
                {
                    model.LimitOrderBook.SaveDepthProfile(Path.Combine(outputFolder,
                        "model_initial_depth_profile.csv"));
                    model.LimitOrderBook.SaveDepthProfileBuySide(Path.Combine(outputFolder,
                        "model_initial_depth_profile_buy.csv"));
                    model.LimitOrderBook.SaveDepthProfileSellSide(Path.Combine(outputFolder,
                        "model_initial_depth_profile_sell.csv"));
                }

                model.SimulateOrderFlow(duration, useSeed: false);
                model.SavePriceProcess(Path.Combine(outputFolder, $"Paths\\simulated_price_{pathNumber}.csv"));

                if (pathNumber == 0)
                {
                    SharedUtilities.SaveAsJson(model, Path.Combine(outputFolder, "model.json"));
                }
            }

            #endregion
        }


        private class LobStatistics
        {
            public double RateOfLimitOrdersBuySide { set; get; }
            public double RateOfLimitOrdersSellSide { set; get; }
            public double RateOfCancelledOrdersBuySide { set; get; }
            public double RateOfCancelledOrdersSellSide { set; get; }
            public double RateOfDeletedOrdersBuySide { set; get; }
            public double RateOfDeletedOrdersSellSide { set; get; }
            public double RateOfExecutedVisibleOrdersBuySide { set; get; }
            public double RateOfExecutedVisibleOrdersOrdersSellSide { set; get; }
            public double RateOfExecutedHiddenOrdersBuySide { set; get; }
            public double RateOfExecutedHiddenOrdersSellSide { set; get; }

            public double[] InterarrivalTimesLimitOrders { set; get; }
            public double[] InterarrivalTimesMarketOrders { set; get; }
            public double[] InterarrivalTimesCancelOrders { set; get; }
        }
        
        private double[] Diff(double[] x)
        {
            var difference = new double[x.Length-1];

            for (var i = 0; i < difference.Length; i++)
            {
                difference[i] = x[i + 1] - x[i];
            }            
            return difference;
        }

        [TestCase("2016-01-04", "AMZN", 10000)]
        [TestCase("2016-01-05", "AMZN", 10000)]
        [TestCase("2016-01-06", "AMZN", 10000)]
        [TestCase("2016-01-11", "AMZN", 10000)]
        [TestCase("2016-01-12", "AMZN", 10000)]
        [TestCase("2016-01-13", "AMZN", 10000)]
        [TestCase("2016-01-14", "AMZN", 10000)]
        [TestCase("2016-01-15", "AMZN", 10000)]
        [TestCase("2016-01-19", "AMZN", 10000)]
        [TestCase("2016-01-20", "AMZN", 10000)]
        [TestCase("2016-01-21", "AMZN", 10000)]
        [TestCase("2016-01-22", "AMZN", 10000)]
        [TestCase("2016-01-25", "AMZN", 10000)]
        [TestCase("2016-01-26", "AMZN", 10000)]
        [TestCase("2016-01-27", "AMZN", 10000)]
        [TestCase("2016-01-28", "AMZN", 10000)]
        [TestCase("2016-01-29", "AMZN", 10000)]
        // Cisco TODO: Problem with calibration
        [TestCase("2016-01-04", "CSCO", 10000)]
        [TestCase("2016-01-05", "CSCO", 10000)]
        [TestCase("2016-01-06", "CSCO", 10000)]
        [TestCase("2016-01-11", "CSCO", 10000)]
        [TestCase("2016-01-12", "CSCO", 10000)]
        [TestCase("2016-01-13", "CSCO", 10000)]
        [TestCase("2016-01-14", "CSCO", 10000)]
        [TestCase("2016-01-15", "CSCO", 10000)]
        [TestCase("2016-01-19", "CSCO", 10000)]
        [TestCase("2016-01-20", "CSCO", 10000)]
        [TestCase("2016-01-21", "CSCO", 10000)]
        [TestCase("2016-01-22", "CSCO", 10000)]
        [TestCase("2016-01-25", "CSCO", 10000)]
        [TestCase("2016-01-26", "CSCO", 10000)]
        [TestCase("2016-01-27", "CSCO", 10000)]
        [TestCase("2016-01-28", "CSCO", 10000)]
        [TestCase("2016-01-29", "CSCO", 10000)]
        // Tesla
        [TestCase("2016-01-04", "TSLA", 10000)]
        [TestCase("2016-01-05", "TSLA", 10000)]
        [TestCase("2016-01-06", "TSLA", 10000)]
        [TestCase("2016-01-11", "TSLA", 10000)]
        [TestCase("2016-01-12", "TSLA", 10000)]
        [TestCase("2016-01-13", "TSLA", 10000)]
        [TestCase("2016-01-14", "TSLA", 10000)]
        [TestCase("2016-01-15", "TSLA", 10000)]
        [TestCase("2016-01-19", "TSLA", 10000)]
        [TestCase("2016-01-20", "TSLA", 10000)]
        [TestCase("2016-01-21", "TSLA", 10000)]
        [TestCase("2016-01-22", "TSLA", 10000)]
        [TestCase("2016-01-25", "TSLA", 10000)]
        [TestCase("2016-01-26", "TSLA", 10000)]
        [TestCase("2016-01-27", "TSLA", 10000)]
        [TestCase("2016-01-28", "TSLA", 10000)]
        [TestCase("2016-01-29", "TSLA", 10000)]
        // Netflix
        [TestCase("2016-01-04", "NFLX", 10000)]
        [TestCase("2016-01-05", "NFLX", 10000)]
        [TestCase("2016-01-06", "NFLX", 10000)]
        [TestCase("2016-01-11", "NFLX", 10000)]
        [TestCase("2016-01-12", "NFLX", 10000)]
        [TestCase("2016-01-13", "NFLX", 10000)]
        [TestCase("2016-01-14", "NFLX", 10000)]
        [TestCase("2016-01-15", "NFLX", 10000)]
        [TestCase("2016-01-19", "NFLX", 10000)]
        [TestCase("2016-01-20", "NFLX", 10000)]
        [TestCase("2016-01-21", "NFLX", 10000)]
        [TestCase("2016-01-22", "NFLX", 10000)]
        [TestCase("2016-01-25", "NFLX", 10000)]
        [TestCase("2016-01-26", "NFLX", 10000)]
        [TestCase("2016-01-27", "NFLX", 10000)]
        [TestCase("2016-01-28", "NFLX", 10000)]
        [TestCase("2016-01-29", "NFLX", 10000)]
        public void TestStatisticsOfTradingData(string tradingDateString, string symbol, double duration)
        {
            // Trading date for the calibration data set 
            var tradingDate = DateTime.ParseExact(tradingDateString, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            
            // Folder to store all results 
            var outputFolder = Path.Combine(WorkFolder, $"{symbol}\\{tradingDate:yyyyMMdd}");
            
            Directory.CreateDirectory(outputFolder);
            Directory.CreateDirectory(Path.Combine(outputFolder, "Paths"));
            
            Assert.True(Directory.Exists(outputFolder), $"The output folder {outputFolder} could not be created");  
            
            const int level = 10;
            var repository = new LobRepository(
                symbol, 
                level, 
                new List<DateTime> { tradingDate }, 
                skipFirstSeconds: 0, 
                skipLastSeconds: duration);              
            var inSampleTradingData =  repository.TradingData[tradingDate];
            
            // Get the outsample trading data set 
            repository = new LobRepository(
                symbol, 
                level, 
                new List<DateTime> { tradingDate }, 
                skipFirstSeconds: inSampleTradingData.EndTradingTime - inSampleTradingData.StartTradingTime, 
                skipLastSeconds: 0);              
            
            var outOfSampleTradingData =  repository.TradingData[tradingDate];
            
            // Get the outsample trading data set 
            repository = new LobRepository(
                symbol, 
                level, 
                new List<DateTime> { tradingDate });              
            
            var tradingData =  repository.TradingData[tradingDate];
            
            
            
            var statistics = new LobStatistics
            {
                RateOfLimitOrdersBuySide =  tradingData.Events.Count(p => p.Type == LobEventType.Submission && p.Side == LobMarketSide.Buy) / tradingData.TradingDuration,
                RateOfLimitOrdersSellSide = tradingData.Events.Count(p => p.Type == LobEventType.Submission && p.Side == LobMarketSide.Sell) / tradingData.TradingDuration,
                RateOfCancelledOrdersBuySide = tradingData.Events.Count(p => p.Type == LobEventType.Cancellation && p.Side == LobMarketSide.Buy) / tradingData.TradingDuration,
                RateOfCancelledOrdersSellSide = tradingData.Events.Count(p => p.Type == LobEventType.Cancellation && p.Side == LobMarketSide.Sell) / tradingData.TradingDuration,
                RateOfDeletedOrdersBuySide = tradingData.Events.Count(p => p.Type == LobEventType.Deletion && p.Side == LobMarketSide.Buy) / tradingData.TradingDuration,
                RateOfDeletedOrdersSellSide = tradingData.Events.Count(p => p.Type == LobEventType.Deletion && p.Side == LobMarketSide.Sell) / tradingData.TradingDuration,
                RateOfExecutedVisibleOrdersBuySide = tradingData.Events.Count(p => p.Type == LobEventType.ExecutionVisibleLimitOrder && p.Side == LobMarketSide.Buy) / tradingData.TradingDuration,
                RateOfExecutedVisibleOrdersOrdersSellSide = tradingData.Events.Count(p => p.Type == LobEventType.ExecutionVisibleLimitOrder && p.Side == LobMarketSide.Sell) / tradingData.TradingDuration,
                RateOfExecutedHiddenOrdersBuySide = tradingData.Events.Count(p => p.Type == LobEventType.ExecutionHiddenLimitOrder && p.Side == LobMarketSide.Buy) / tradingData.TradingDuration,
                RateOfExecutedHiddenOrdersSellSide = tradingData.Events.Count(p => p.Type == LobEventType.ExecutionHiddenLimitOrder && p.Side == LobMarketSide.Sell) / tradingData.TradingDuration,
                                                     
                InterarrivalTimesLimitOrders = Diff(tradingData.Events.Where(p => p.Type == LobEventType.Submission).Select(p => p.Time).ToArray()),
                InterarrivalTimesMarketOrders = Diff(tradingData.Events.Where(p => p.Type == LobEventType.ExecutionHiddenLimitOrder || p.Type == LobEventType.ExecutionVisibleLimitOrder).Select(p => p.Time).ToArray()),
                InterarrivalTimesCancelOrders = Diff(tradingData.Events.Where(p => p.Type == LobEventType.Deletion || p.Type == LobEventType.Cancellation).Select(p => p.Time).ToArray())
            };
            
            var inSamplestatistics = new LobStatistics
            {
                RateOfLimitOrdersBuySide =  inSampleTradingData.Events.Count(p => p.Type == LobEventType.Submission && p.Side == LobMarketSide.Buy) / inSampleTradingData.TradingDuration,
                RateOfLimitOrdersSellSide = inSampleTradingData.Events.Count(p => p.Type == LobEventType.Submission && p.Side == LobMarketSide.Sell) / inSampleTradingData.TradingDuration,
                RateOfCancelledOrdersBuySide = inSampleTradingData.Events.Count(p => p.Type == LobEventType.Cancellation && p.Side == LobMarketSide.Buy) / inSampleTradingData.TradingDuration,
                RateOfCancelledOrdersSellSide = inSampleTradingData.Events.Count(p => p.Type == LobEventType.Cancellation && p.Side == LobMarketSide.Sell) / inSampleTradingData.TradingDuration,
                RateOfDeletedOrdersBuySide = inSampleTradingData.Events.Count(p => p.Type == LobEventType.Deletion && p.Side == LobMarketSide.Buy) / inSampleTradingData.TradingDuration,
                RateOfDeletedOrdersSellSide = inSampleTradingData.Events.Count(p => p.Type == LobEventType.Deletion && p.Side == LobMarketSide.Sell) / inSampleTradingData.TradingDuration,
                RateOfExecutedVisibleOrdersBuySide = inSampleTradingData.Events.Count(p => p.Type == LobEventType.ExecutionVisibleLimitOrder && p.Side == LobMarketSide.Buy) / inSampleTradingData.TradingDuration,
                RateOfExecutedVisibleOrdersOrdersSellSide = inSampleTradingData.Events.Count(p => p.Type == LobEventType.ExecutionVisibleLimitOrder && p.Side == LobMarketSide.Sell) / inSampleTradingData.TradingDuration,
                RateOfExecutedHiddenOrdersBuySide = inSampleTradingData.Events.Count(p => p.Type == LobEventType.ExecutionHiddenLimitOrder && p.Side == LobMarketSide.Buy) / inSampleTradingData.TradingDuration,
                RateOfExecutedHiddenOrdersSellSide = inSampleTradingData.Events.Count(p => p.Type == LobEventType.ExecutionHiddenLimitOrder && p.Side == LobMarketSide.Sell) / inSampleTradingData.TradingDuration,                                                     
                InterarrivalTimesLimitOrders = Diff(inSampleTradingData.Events.Where(p => p.Type == LobEventType.Submission).Select(p => p.Time).ToArray()),
                InterarrivalTimesMarketOrders = Diff(inSampleTradingData.Events.Where(p => p.Type == LobEventType.ExecutionHiddenLimitOrder || p.Type == LobEventType.ExecutionVisibleLimitOrder).Select(p => p.Time).ToArray()),
                InterarrivalTimesCancelOrders = Diff(inSampleTradingData.Events.Where(p => p.Type == LobEventType.Deletion || p.Type == LobEventType.Cancellation).Select(p => p.Time).ToArray())
            };
            
            var outOfSamplestatistics = new LobStatistics
            {
                RateOfLimitOrdersBuySide =  outOfSampleTradingData.Events.Count(p => p.Type == LobEventType.Submission && p.Side == LobMarketSide.Buy) / outOfSampleTradingData.TradingDuration,
                RateOfLimitOrdersSellSide = outOfSampleTradingData.Events.Count(p => p.Type == LobEventType.Submission && p.Side == LobMarketSide.Sell) / outOfSampleTradingData.TradingDuration,
                RateOfCancelledOrdersBuySide = outOfSampleTradingData.Events.Count(p => p.Type == LobEventType.Cancellation && p.Side == LobMarketSide.Buy) / outOfSampleTradingData.TradingDuration,
                RateOfCancelledOrdersSellSide = outOfSampleTradingData.Events.Count(p => p.Type == LobEventType.Cancellation && p.Side == LobMarketSide.Sell) / outOfSampleTradingData.TradingDuration,
                RateOfDeletedOrdersBuySide = outOfSampleTradingData.Events.Count(p => p.Type == LobEventType.Deletion && p.Side == LobMarketSide.Buy) / outOfSampleTradingData.TradingDuration,
                RateOfDeletedOrdersSellSide = outOfSampleTradingData.Events.Count(p => p.Type == LobEventType.Deletion && p.Side == LobMarketSide.Sell) / outOfSampleTradingData.TradingDuration,
                RateOfExecutedVisibleOrdersBuySide = outOfSampleTradingData.Events.Count(p => p.Type == LobEventType.ExecutionVisibleLimitOrder && p.Side == LobMarketSide.Buy) / outOfSampleTradingData.TradingDuration,
                RateOfExecutedVisibleOrdersOrdersSellSide = outOfSampleTradingData.Events.Count(p => p.Type == LobEventType.ExecutionVisibleLimitOrder && p.Side == LobMarketSide.Sell) / outOfSampleTradingData.TradingDuration,
                RateOfExecutedHiddenOrdersBuySide = outOfSampleTradingData.Events.Count(p => p.Type == LobEventType.ExecutionHiddenLimitOrder && p.Side == LobMarketSide.Buy) / outOfSampleTradingData.TradingDuration,
                RateOfExecutedHiddenOrdersSellSide = outOfSampleTradingData.Events.Count(p => p.Type == LobEventType.ExecutionHiddenLimitOrder && p.Side == LobMarketSide.Sell) / outOfSampleTradingData.TradingDuration,                                                     
                InterarrivalTimesLimitOrders = Diff(outOfSampleTradingData.Events.Where(p => p.Type == LobEventType.Submission).Select(p => p.Time).ToArray()),
                InterarrivalTimesMarketOrders = Diff(outOfSampleTradingData.Events.Where(p => p.Type == LobEventType.ExecutionHiddenLimitOrder || p.Type == LobEventType.ExecutionVisibleLimitOrder).Select(p => p.Time).ToArray()),
                InterarrivalTimesCancelOrders = Diff(outOfSampleTradingData.Events.Where(p => p.Type == LobEventType.Deletion || p.Type == LobEventType.Cancellation).Select(p => p.Time).ToArray())
            };            
            
            inSampleTradingData.CanceledOrderDistribution.Scale(1, 1.0 / inSampleTradingData.TradingDuration).Save(Path.Combine(outputFolder, "cancel_order_rate_distribution_in_sample.csv"));
            
            SharedUtilities.SaveAsJson(statistics, Path.Combine(outputFolder, "statistics_all.json"));
            SharedUtilities.SaveAsJson(inSamplestatistics, Path.Combine(outputFolder, "statistics_in_sample.json"));
            SharedUtilities.SaveAsJson(outOfSamplestatistics, Path.Combine(outputFolder, "statistics_out_of_sample.json"));                        

        }
    }       
}