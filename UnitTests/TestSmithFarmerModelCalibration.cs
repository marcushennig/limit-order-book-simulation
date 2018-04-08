using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using LimitOrderBookRepositories;
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
            const double relativeTolerance  = 2 * percent;
            
            #region Create test folder
            
            var testFolder = Path.Combine(WorkFolder, $"Simulation_{symbol}_{tradingDate:yyyyMMdd}");
            Directory.CreateDirectory(testFolder);
            Assert.True(Directory.Exists(testFolder), $"The test folder  {testFolder} could not be created");
            
            #endregion
            
            #region Load trading data and save statistics
            
            const int level = 10;
            var tradingDates = new List<DateTime> { tradingDate };
            var repository = new LobRepository(symbol, level, tradingDates);          
            var tradingData =  repository.TradingData[tradingDate];

            // Save statistics about LOB data 
            tradingData.LimitOrderDistribution.Save(Path.Combine(testFolder, "tradingData_limit_order_distribution.csv"));
            tradingData.LimitSellOrderDistribution.Save(Path.Combine(testFolder, "tradingData_limit_sell_order_distribution.csv"));
            tradingData.LimitBuyOrderDistribution.Save(Path.Combine(testFolder, "tradingData_limit_buy_order_distribution.csv"));
            tradingData.CanceledOrderDistribution.Save(Path.Combine(testFolder, "tradingData_canceled_order_distribution.csv"));
            tradingData.CanceledSellOrderDistribution.Save(Path.Combine(testFolder, "tradingData_canceled_sell_order_distribution.csv"));
            tradingData.CanceledBuyOrderDistribution.Save(Path.Combine(testFolder, "tradingData_canceled_buy_order_distribution.csv"));
            tradingData.AverageDepthProfile.Save(Path.Combine(testFolder, "tradingData_outstanding_limit_order_distribution.csv"));
            tradingData.SavePriceProcess(Path.Combine(testFolder, "tradingData_price.csv"));
            
            #endregion
            
            #region Calibrate Smith-Farmer model
            
            // Calibrate Smith-Farmer model within a narrow price-band near
            // the spread (given by lower and upper price quantile). For this purpose 
            // we use the time-averaged depth profile (depth vs. distance to best opposite quote) 
            const double lowerProbability  = 0.01;
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
            
            var calibratedParameters = SmithFarmerModelCalibration.Calibrate(tradingData, lowerProbability, upperProbability);
            
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
            model.LimitOrderBook.SaveDepthProfileBuySide(Path.Combine(testFolder, "model_initial_depth_profile_buy.csv"));
            model.LimitOrderBook.SaveDepthProfileSellSide(Path.Combine(testFolder, "model_initial_depth_profile_sell.csv"));

            model.SimulateOrderFlow(duration);
            
            model.LimitOrderBook.SaveDepthProfile(Path.Combine(testFolder, "model_final_depth_profile.csv"));
            model.LimitOrderBook.SaveDepthProfileBuySide(Path.Combine(testFolder, "model_final_depth_profile_buy.csv"));
            model.LimitOrderBook.SaveDepthProfileSellSide(Path.Combine(testFolder, "model_final_depth_profile_sell.csv"));

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
        [TestCase("2016-01-04", "AMZN", 10000, 40)]
        [TestCase("2016-01-05", "AMZN", 10000, 40)]
        [TestCase("2016-01-06", "AMZN", 10000, 40)]
        [TestCase("2016-01-11", "AMZN", 10000, 40)]
        [TestCase("2016-01-12", "AMZN", 10000, 40)]
        [TestCase("2016-01-13", "AMZN", 10000, 40)]
        [TestCase("2016-01-14", "AMZN", 10000, 40)]
        [TestCase("2016-01-15", "AMZN", 10000, 40)]
        [TestCase("2016-01-19", "AMZN", 10000, 40)]
        [TestCase("2016-01-20", "AMZN", 10000, 40)]
        [TestCase("2016-01-21", "AMZN", 10000, 40)]
        [TestCase("2016-01-22", "AMZN", 10000, 40)]
        [TestCase("2016-01-25", "AMZN", 10000, 40)]
        [TestCase("2016-01-26", "AMZN", 10000, 40)]
        [TestCase("2016-01-27", "AMZN", 10000, 40)]
        [TestCase("2016-01-28", "AMZN", 10000, 40)]
        [TestCase("2016-01-29", "AMZN", 10000, 40)]
        [TestCase("2016-02-01", "AMZN", 10000, 40)]
        [TestCase("2016-02-02", "AMZN", 10000, 40)]
        [TestCase("2016-02-03", "AMZN", 10000, 40)]
        [TestCase("2016-02-04", "AMZN", 10000, 40)]
        [TestCase("2016-02-05", "AMZN", 10000, 40)]
        [TestCase("2016-02-08", "AMZN", 10000, 40)]
        [TestCase("2016-02-09", "AMZN", 10000, 40)]
        [TestCase("2016-02-10", "AMZN", 10000, 40)]
        [TestCase("2016-02-11", "AMZN", 10000, 40)]
        [TestCase("2016-02-12", "AMZN", 10000, 40)]
        [TestCase("2016-02-16", "AMZN", 10000, 40)]
        [TestCase("2016-02-17", "AMZN", 10000, 40)]
        [TestCase("2016-02-18", "AMZN", 10000, 40)]
        [TestCase("2016-02-19", "AMZN", 10000, 40)]
        [TestCase("2016-02-22", "AMZN", 10000, 40)]
        [TestCase("2016-02-23", "AMZN", 10000, 40)]
        [TestCase("2016-02-24", "AMZN", 10000, 40)]
        [TestCase("2016-02-25", "AMZN", 10000, 40)]
        [TestCase("2016-02-26", "AMZN", 10000, 40)]
        [TestCase("2016-02-29", "AMZN", 10000, 40)]
        [TestCase("2016-03-01", "AMZN", 10000, 40)]
        [TestCase("2016-03-02", "AMZN", 10000, 40)]
        [TestCase("2016-03-03", "AMZN", 10000, 40)]
        [TestCase("2016-03-04", "AMZN", 10000, 40)]
        [TestCase("2016-03-07", "AMZN", 10000, 40)]
        [TestCase("2016-03-08", "AMZN", 10000, 40)]
        [TestCase("2016-03-09", "AMZN", 10000, 40)]
        [TestCase("2016-03-10", "AMZN", 10000, 40)]
        [TestCase("2016-03-11", "AMZN", 10000, 40)]
        [TestCase("2016-03-14", "AMZN", 10000, 40)]
        [TestCase("2016-03-15", "AMZN", 10000, 40)]
        [TestCase("2016-03-16", "AMZN", 10000, 40)]
        [TestCase("2016-03-17", "AMZN", 10000, 40)]
        [TestCase("2016-03-18", "AMZN", 10000, 40)]
        [TestCase("2016-03-21", "AMZN", 10000, 40)]
        [TestCase("2016-03-22", "AMZN", 10000, 40)]
        [TestCase("2016-03-23", "AMZN", 10000, 40)]
        [TestCase("2016-03-24", "AMZN", 10000, 40)]
        [TestCase("2016-03-28", "AMZN", 10000, 40)]
        [TestCase("2016-03-29", "AMZN", 10000, 40)]
        [TestCase("2016-03-30", "AMZN", 10000, 40)]
        [TestCase("2016-03-31", "AMZN", 10000, 40)]
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
                new List<DateTime> { tradingDate }, 
                skipFirstSeconds: 0, 
                skipLastSeconds: duration);              
            var inSampleTradingData =  repository.TradingData[tradingDate];
            
            // Get the outsample trading data set 
            var repository2 = new LobRepository(
                symbol, 
                level, 
                new List<DateTime> { tradingDate }, 
                skipFirstSeconds: inSampleTradingData.EndTradingTime - inSampleTradingData.StartTradingTime, 
                skipLastSeconds: 0);              
            
            var outOfSampleTradingData =  repository2.TradingData[tradingDate];
            
            // Save the price trajectory to later compute the relalized volatility signature plot  
            inSampleTradingData.SavePriceProcess(Path.Combine(outputFolder, "price_in_sample.csv"));
            outOfSampleTradingData.SavePriceProcess(Path.Combine(outputFolder, "price_out_of_sample.csv"));

            #endregion
            
            #region Calibrate Smith-Farmer model
            
            // Calibrate Smith-Farmer model within a narrow price-band near
            // the spread (given by lower and upper price quantile) with the in-sample trading data.
            const double lowerProbability  = 0.01;
            const double upperProbability = 0.80;
            
            inSampleTradingData.AverageDepthProfile.Save(Path.Combine(outputFolder, "in_sample_average_depth_profile.csv"));
            
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
                
                model.SimulateOrderFlow(duration, useSeed:false);
                model.SavePriceProcess(Path.Combine(outputFolder, $"Paths\\simulated_price_{pathNumber}.csv"));                
            }
            
            #endregion
        }
    }
}