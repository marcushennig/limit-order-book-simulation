using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using log4net;
using LimitOrderBookRepositories;
using LimitOrderBookRepositories.Model;
using MathNet.Numerics.Statistics;

namespace LimitOrderBookSimulation.EventModels
{
    public static class SmithFarmerModelCalibration
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        /// <summary>
        /// Determine the characteristic order size
        /// </summary>
        /// <param name="tradingData">LOB data for a trading day</param>
        /// <returns></returns>
        private static double CalibrateCharacteristicOrderSize(LobTradingData tradingData) 
        {
            var sizes = new List<double>();

            sizes.AddRange(tradingData.LimitOrders.Select(order => (double) order.Volume));
            sizes.AddRange(tradingData.MarketOrders.Select(order => (double)order.Volume));
            sizes.AddRange(tradingData.CanceledOrders.Select(order => (double)order.Volume));
            
            return sizes.Mean();
        }
        
        /// <summary>
        /// Calibrate model with trading data from LOB within a narrow price band near the spread,
        /// given by a lower and higher quantile of the limit order distribution. 
        // Select only limit order events events that fall into window where price < price_60%
        // Check Paper: PNAS: The predictive power of zero intelligence models in financial markes
        // C:\Users\d90789\Documents\Oxford MSc in Mathematical Finance\Thesis\Literature\Farmer Smith Model
        /// Note: Martin Gould suggested to calibrate in a closed window near the spread 
        /// Roughly 70% of all orders are placed either at
        /// the best price or inside the spread. Outside the spread the density
        /// of limit order placement falls o as a power law as a function of
        /// the distance from the best prices
        /// Determine the number of orders within a q_lower and q_upper, where q_n is the n quantil 
        /// of the distribution of orders, any strategy for estimating the density 
        /// q_upper is made in a compromise to include as much data as possible for 
        /// statistical stability, but not so much as to
        /// include orders that are unlikely to ever be executed, and therefore
        /// unlikely to have any effect on prices.
        /// </summary>
        /// <param name="tradingData"></param>
        /// <param name="lowerQuantileProbability"></param>
        /// <param name="upperQuantileProbability"></param>
        /// <returns></returns>
        public static SmithFarmerModelParameter Calibrate(LobTradingData tradingData, 
            double lowerQuantileProbability = 0.01, 
            double upperQuantileProbability = 0.80) 
        {
            var parameter = new SmithFarmerModelParameter();
            var duration = tradingData.TradingDuration;
            
            var sigma = CalibrateCharacteristicOrderSize(tradingData);
            var pi = tradingData.PriceTickSize;
            
            parameter.PriceTickSize = pi;
            parameter.CharacteristicOrderSize = sigma;
            
            // Calibrate within a narrow band within the lower - upper quantile
            // of the limit order distribution. Use average limit order depth
            // profile to determine a small band around the spread, where we
            // calibrate the limit order rate 
            var averageDepthProfile = tradingData.AverageDepthProfile;
            
            var lowerQuantile = averageDepthProfile.Quantile(lowerQuantileProbability);
            var upperQuantile = averageDepthProfile.Quantile(upperQuantileProbability); 
            
            // Save parameters
            parameter.LowerQuantileProbability = lowerQuantileProbability;
            parameter.UpperQuantileProbability = upperQuantileProbability;
            parameter.LowerQuantile = lowerQuantile;
            parameter.UpperQuantile = upperQuantile;
            
            parameter.MinTradingTime = tradingData.StartTradingTime;
            parameter.MaxTradingTime = tradingData.EndTradingTime;

            #region Estimate market order rate
            
            var totalVolumeOfMarketOrders = tradingData.MarketOrders.Sum(p => p.Volume);
            var numberOfMarketOrders = totalVolumeOfMarketOrders / sigma;                                       
            parameter.MarketOrderRate = numberOfMarketOrders / duration / 2;
            
            #endregion
            
            #region Estimate limit order rate
            
            var priceRangeInTicks = (upperQuantile - lowerQuantile) / pi;
            // Count the number of limit sell and buy orders that where placed within price band in 
            // units of the characteristic order size 
            var countOfLimitOrders = tradingData.LimitOrders
                                         .Where(order => lowerQuantile <= order.DistanceBestOppositeQuote && 
                                                         order.DistanceBestOppositeQuote <= upperQuantile)
                                         .Sum(order => order.Volume) / sigma;

            // Determine the rate density of limit sell/buy orders 
            parameter.LimitOrderRateDensity = countOfLimitOrders / (duration * priceRangeInTicks) / 2;
            
            #endregion
            
            #region Estimate cancellation rate 
            var canceledOrderRateDistribution = tradingData.CanceledOrderDistribution;
            
            var totalDepthOfLimitOrderInQuantile = averageDepthProfile.Data
                .Where(p => lowerQuantile <= p.Key && p.Key <= upperQuantile)
                .Select(p => p.Value)
                .Sum();
            
            var totalCanceledVolumeOfOrderInQuantile = canceledOrderRateDistribution.Data
                .Where(p => lowerQuantile <= p.Key && p.Key <= upperQuantile)
                .Select(p => p.Value)
                .Sum();
            
            parameter.CancellationRate = totalCanceledVolumeOfOrderInQuantile / (totalDepthOfLimitOrderInQuantile * duration) / 2;
            
            #endregion
            
            return parameter;
        }

        /// <summary>
        /// Calibrate model with a set of trading data by averaging the
        /// calibration results for each trading date  
        /// </summary>
        /// <param name="repository"></param>
        public static SmithFarmerModelParameter Calibrate(LobRepository repository)
        {
            if (!repository.TradingData.Any())
            {
                throw new ArgumentException("Cannot calibrate model without trading data");
            }
            Log.Info("Start calibrating model");
            
            var parameters = new List<SmithFarmerModelParameter>();
            
            // straightforward: to measure mu, for example, we simply compute
            // the total number of shares of market orders and divide by time
            // or, alternatively, we compute mu(t) for each day and average; we get
            // similar results in either case.
            foreach (var tradingDay in repository.TradingDays)
            {
                var parameter = Calibrate(repository.TradingData[tradingDay]);

                Log.Info("===================================================================");
                Log.Info($"Calibration parameters for: {tradingDay: yyyy - MM - dd}");
                Log.Info("===================================================================");
                Log.Info($"Market order rate (mu): {parameter.MarketOrderRate} [sigma]/[time]");
                Log.Info(
                    $"Limit order rate density (alpha): {parameter.LimitOrderRateDensity} [sigma]/([time]*[tick])");
                Log.Info($"Cancellation rate density (delta): {parameter.CancellationRate} [sigma]/([time]*[depth])");
                Log.Info($"Characteristic size (sigma): {parameter.CharacteristicOrderSize}");
                Log.Info($"Tick size (pi): {parameter.PriceTickSize}");

                parameters.Add(parameter);
            }

            var mean = new SmithFarmerModelParameter
            {
                MarketOrderRate = parameters.Select(p => p.MarketOrderRate).Mean(),
                CancellationRate = parameters.Select(p => p.CancellationRate).Mean(),
                LimitOrderRateDensity = parameters.Select(p => p.LimitOrderRateDensity).Mean(),
                PriceTickSize = parameters.Select(p => p.PriceTickSize).Mean(),
                CharacteristicOrderSize = parameters.Select(p => p.CharacteristicOrderSize).Mean()
            };

            Log.Info("===================================================================");
            Log.Info("Mean calibration parameters");
            Log.Info("===================================================================");

            Log.Info($"Market order rate (mu): {mean.MarketOrderRate} [sigma]/[time]");
            Log.Info($"Limit order rate density (alpha): {mean.LimitOrderRateDensity} [sigma]/([time]*[tick])");
            Log.Info($"Cancellation rate density (delta): {mean.CancellationRate} [sigma]/([time]*[depth])");
            Log.Info($"Characteristic size (sigma): {mean.CharacteristicOrderSize}");
            Log.Info($"Tick size (pi): {mean.PriceTickSize}");

            Log.Info("Finished calibrating model");

            return mean;
        }
        
        public static SortedDictionary<int, int> CalibrateInitalBuySide(LobState intialState)
        {
            return null;
        }
    }
}