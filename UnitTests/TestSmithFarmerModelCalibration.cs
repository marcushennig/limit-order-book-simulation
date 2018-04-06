using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using LimitOrderBookRepositories;
using LimitOrderBookSimulation.EventModels;
using NUnit.Framework;

namespace UnitTests
{
    [TestFixture]
    public class TestSmithFarmerModelCalibration
    {        
        [SetUp]
        public void Init()
        {          
        }

        [TestCase("2016-01-04", "AMZN")]
        public void TestCalibration(string tradingDateString, string symbol)
        {
            // Get data from App.config
            var workFolder = ConfigurationManager.AppSettings["WorkFolder"];
            Assert.True(Directory.Exists(workFolder));

            var level = 10;
            var tradingDate = DateTime.ParseExact(tradingDateString, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var tradingDates = new List<DateTime> { tradingDate };
            var repository = new LobRepository(symbol, level, tradingDates);

            var tradingData =  repository.TradingData[tradingDate];
            var tickSize = tradingData.PriceTickSize;

            // Use average depth profile to determine a small band around 
            // the spread, where we calibrate the limit order rate 
            const double probability = 0.6;
            const double tolerance  = 0.01;

            var averageDepthProfile = tradingData.AverageDepthProfile;
            var distanceBestOppositeQuoteQuantile = averageDepthProfile.Quantile(probability);

            var measuredProbability = averageDepthProfile.Probability
                .Where(p => p.Key <= distanceBestOppositeQuoteQuantile)
                .Select(p => p.Value)
                .Sum();

            Assert.True(Math.Abs(probability - measuredProbability) < tolerance, "");

            averageDepthProfile.Save(Path.Combine(workFolder, $"depth_profile_{symbol}_{tradingDate:yyyyMMdd}.csv"));

            var orderSize = SmithFarmerModelCalibration.CalibrateCharacteristicOrderSize(tradingData);
            var marketOrderRate = SmithFarmerModelCalibration.CalibrateMarketOrderRate(tradingData, orderSize);
            var limitOrderRate = SmithFarmerModelCalibration.CalibrateLimitOrderRate(tradingData, orderSize, tickSize, distanceBestOppositeQuoteQuantile);
            var cancelationRate = SmithFarmerModelCalibration.CalibrateCancelationRate(tradingData, orderSize, tickSize, distanceBestOppositeQuoteQuantile);


            Console.WriteLine(cancelationRate);
        }
    }
}
