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
        

        [TestCase("2016-01-04", "AMZN", 10)]
        [TestCase("2016-01-05", "AMZN", 10)]
        public void TestCalibration(string tradingDateString, 
                                    string symbol, 
                                    double duration)
        {
            var tradingDate = DateTime.ParseExact(tradingDateString, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            const double percent = 1e-2;
            const double relativeTolerance  = 2 * percent;
            
            #region Create test folder
            
            var testFolder = Path.Combine(WorkFolder, $"Test_Case_{symbol}_{tradingDate:yyyyMMdd}");
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
            
            var probability = upperProbability - lowerProbability;
            var relativeError = 1 - measuredProbability / probability;
            Assert.True(Math.Abs(relativeError) < relativeTolerance, 
                $"Probability: {probability} Measured: {measuredProbability} ({relativeError * 100}%)");
            
            var calibratedParameters = SmithFarmerModelCalibration.Calibrate(tradingData, lowerProbability, upperProbability);
            
            SharedUtilities.SaveAsJson(calibratedParameters, Path.Combine(testFolder, "model_calibrated_parameters.json"));
            averageDepthProfile.Save(Path.Combine(testFolder, "model_average_depth_profile.csv"));
            SharedUtilities.SaveAsJson(averageDepthProfile,  Path.Combine(testFolder, "model_average_depth_profile.json"));
            
            #endregion
            
            #region Simulate order flow

            var initalState = tradingData.States.First();
            
            // TODO: Save intial state 
            
            var model = new SmithFarmerModel(calibratedParameters, initalState);
            model.SimulateOrderFlow(duration);
            
            // Save the model            
            SharedUtilities.SaveAsJson(model, Path.Combine(testFolder, "model.json"));
            model.SavePriceProcess(Path.Combine(testFolder, "model_price.csv"));
            
            #endregion
        }
    }
}
