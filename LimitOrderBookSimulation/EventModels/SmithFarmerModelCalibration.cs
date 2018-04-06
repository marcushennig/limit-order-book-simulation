using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using log4net;
using LimitOrderBookRepositories;
using LimitOrderBookRepositories.Model;
using MathNet.Numerics.Statistics;

namespace LimitOrderBookSimulation.EventModels
{
    // TODO: intialize by defining narrow band near spread for calibration
    public static class SmithFarmerModelCalibration
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Estimate the market order rate from given market order events (execution of limit order)
        /// </summary>
        /// <param name="marketOrderEvents">List of market order events that where observed during time</param>
        /// <param name="characteristicOrderSize">Characteristic order size</param>
        /// <returns></returns>
        public static double EstimateMarketOrderRate(List<LobEvent> marketOrderEvents,
                                                     double characteristicOrderSize)
        {
            var duration = marketOrderEvents.Select(p => p.Time).Max() - marketOrderEvents.Select(p => p.Time).Min();
            var totalVolumeOfMarketOrders = marketOrderEvents.Sum(p => p.Volume);
            var numberOfMarketOrders = totalVolumeOfMarketOrders / characteristicOrderSize;
            
            // Devide by 2 to account for buy/sell
            return numberOfMarketOrders / duration / 2;
        }

        /// <summary>
        /// TODO: Clarify if it is necessary to divide by factor of 2 (buy/sell)
        /// Calibrate limit order rate density (buy or sell) from given trading data.
        ///  
        /// Martin Gould suggested to calibrate in a closed window near the spread.
        /// If level would be say 100, but for small levels no need 
        /// [...] Roughly 70% of all orders are placed either at the best price or inside the spread. 
        /// Outside the spread the density of limit order placement falls as a power law as a function of
        /// the distance from the best prices
        /// [...] Determine the number of orders within a q_3 and q_60, where q_n is the n quantil 
        /// of the distribution of orders, any strategy for estimating the density 
        /// q_60 is made in a compromise to include as much data as possible for 
        /// statistical stability, but not so much as to
        /// include orders that are unlikely to ever be executed, and therefore
        /// unlikely to have any effect on prices.
        /// [...] Here we count the number of limit order events that are submitted 
        /// into a small price band near the spread.
        /// </summary>
        /// <param name="limitOrderEvents">Limit order events that where observed</param>
        /// <param name="duration">Duration of observation</param>
        /// <param name="characteristicOrderSize">characteristic volume of an order</param>
        /// <param name="tickSize">price tick size</param>
        /// <param name="lowerDistanceBestOppositeQuoteQuantile">Lower quantile for distance to best opposite quote</param>
        /// <param name="higherDistanceBestOppositeQuoteQuantile">Higher quantile for distance to best opposite quote</param>
        /// <returns></returns>
        public static double EstimateLimitOrderRate(List<LobEvent> limitOrderEvents,
                                                    double characteristicOrderSize,
                                                    double tickSize,
                                                    double lowerDistanceBestOppositeQuoteQuantile,
                                                    double higherDistanceBestOppositeQuoteQuantile)
        {
            // Price range in ticks within which limit order events will be counted 
            var q0 = lowerDistanceBestOppositeQuoteQuantile;
            var q1 = higherDistanceBestOppositeQuoteQuantile;
            var duration = limitOrderEvents.Select(p => p.Time).Max() - limitOrderEvents.Select(p => p.Time).Min();

            var priceRangeInTicks = (q1 - q0) / tickSize;
            // Count the number of limit sell and buy orders that where placed within price band in 
            // units of the characteristic order size 
            var countOfLimitOrders = limitOrderEvents
                .Where(order => q0 <= order.DistanceBestOppositeQuote && 
                                order.DistanceBestOppositeQuote <= q1)
                .Sum(order => order.Volume) / characteristicOrderSize;

            // Determine the rate density of limit sell and buy orders 
            var limitOrderRateDensity = countOfLimitOrders / (duration * priceRangeInTicks);
            return limitOrderRateDensity / 2;
        }


        /// <summary>
        /// TODO: Question: Calibrate characteristic near spread?
        /// Determine the characteristic order size
        /// </summary>
        /// <param name="tradingData">LOB data for a trading day</param>
        /// <returns></returns>
        public static double CalibrateCharacteristicOrderSize(LobTradingData tradingData)
        {
            // => Sl, Sm, Sc
            var limitOrderSizes = tradingData.LimitOrders
                .Select(order => (double) order.Volume)
                .ToList();
            var limitOrderSize = limitOrderSizes.Mean();
            var stdLimitOrderSize = limitOrderSizes.StandardDeviation();
            Console.WriteLine($"Limit order size: {limitOrderSize} ± {stdLimitOrderSize}");

            var marketOrderSizes = tradingData.MarketOrders
                .Select(order => (double)order.Volume)
                .ToList();
            var marketOrderSize = marketOrderSizes.Mean();
            var stdMarketOrderSize = marketOrderSizes.StandardDeviation();
            Console.WriteLine($"Market order size: {marketOrderSize} ± {stdMarketOrderSize}");

            var canceledOrderSizes = tradingData.CanceledOrders
                .Select(order => (double) order.Volume)
                .ToList();

            var canceledOrderSize = canceledOrderSizes.Mean();
            var stdCanceledOrderSize = canceledOrderSizes.StandardDeviation();
            Console.WriteLine($"Canceled order size: {canceledOrderSize} ± {stdCanceledOrderSize}");

            var sizes = new List<double>();

            sizes.AddRange(marketOrderSizes);
            sizes.AddRange(canceledOrderSizes);
            sizes.AddRange(limitOrderSizes);

            Console.WriteLine($"Order size: {sizes.Mean()} ± {sizes.StandardDeviation()}");

            return sizes.Mean();
        }

        /// <summary>
        /// TODO: Clarify if mu is the rate for buy and sell 
        /// Determine the 'mu'-parameter in Famer & Smith's model 
        /// Mu characterizes the average market order arrival rate and it is just the number of shares of 
        /// effective market order ('buy' or 'sell') to the number of events during the trading day
        /// Unit: [# shares / time]
        /// </summary>
        /// <param name="tradingData">LOB data for a trading day</param>
        /// <param name="characteristicOrderSize">Characteristic order size</param>
        /// <returns></returns>
        public static double CalibrateMarketOrderRate(LobTradingData tradingData, double characteristicOrderSize)
        {
            var duration = tradingData.TradingDuration;
            var totalVolumeOfMarketOrders = tradingData.MarketOrders.Sum(p => p.Volume);

            var numberOfMarketOrders = totalVolumeOfMarketOrders / characteristicOrderSize;
            var marketOrderRate = numberOfMarketOrders / duration;

            return marketOrderRate;
        }

        /// <summary>
        /// TODO: Clarify if it is necessary to divide by factor of 2 (buy/sell)
        /// Calibrate limit order rate density (buy or sell) from given trading data.
        ///  
        /// Martin Gould suggested to calibrate in a closed window near the spread.
        /// If level would be say 100, but for small levels no need 
        /// [...] Roughly 70% of all orders are placed either at the best price or inside the spread. 
        /// Outside the spread the density of limit order placement falls as a power law as a function of
        /// the distance from the best prices
        /// [...] Determine the number of orders within a q_3 and q_60, where q_n is the n quantil 
        /// of the distribution of orders, any strategy for estimating the density 
        /// q_60 is made in a compromise to include as much data as possible for 
        /// statistical stability, but not so much as to
        /// include orders that are unlikely to ever be executed, and therefore
        /// unlikely to have any effect on prices.
        /// [...] Here we count the number of limit order events that are submitted 
        /// into a small price band near the spread.
        /// </summary>
        /// <param name="tradingData">Trading data (events and states) for fixed trading date</param>
        /// <param name="characteristicOrderSize">characteristic volume of an order</param>
        /// <param name="tickSize">price tick size</param>
        /// <param name="distanceBestOppositeQuoteQuantile">Quantile for distance to best opposite quote</param>
        /// <returns></returns>
        public static double CalibrateLimitOrderRate(LobTradingData tradingData, 
                                                     double characteristicOrderSize, 
                                                     double tickSize, 
                                                     double distanceBestOppositeQuoteQuantile)
        {            
            // Price range in ticks within which limit order events will be counted 
            var priceRangeInTicks = distanceBestOppositeQuoteQuantile / tickSize;

            // Count the number of limit sell and buy orders that where placed within price band in 
            // units of the characteristic order size 
            var countOfLimitOrders = tradingData.LimitOrders
                .Where(order => order.DistanceBestOppositeQuote <= distanceBestOppositeQuoteQuantile)
                .Sum(order => order.Volume) / characteristicOrderSize;

            // Determine the rate density of limit sell and buy orders 
            var limitOrderRateDensity = countOfLimitOrders / (tradingData.TradingDuration * priceRangeInTicks);

            return limitOrderRateDensity;
        }

        /// <summary>
        /// Calibrate cancelation rate  
        /// We similarly compute delta as the inverse of the average lifetime of orders canceled inside the same price window W.
        /// </summary>
        /// <param name="tradingData"></param>
        /// <param name="characteristicOrderSize"></param>
        /// <param name="tickSize"></param>
        /// <param name="distanceBestOppositeQuoteQuantile">Quantile for distance to best opposite quote</param>
        /// <returns></returns>
        public static double CalibrateCancelationRate(LobTradingData tradingData, 
            double characteristicOrderSize,  
            double tickSize, 
            double distanceBestOppositeQuoteQuantile)
        {
            // TODO: Think more clearly about cancellation rate 
            // TODO: Work within a narrow band within the 3%-60% 
            // TODO: Quantile of the limit order distribution
            // Cancellations occuring at each price level with a rate 
            // propotional to the depth at this price 

            var canceledOrderRateDistribution = tradingData.CanceledOrderDistribution.Scale(1, 1.0 / tradingData.TradingDuration);
            var averageDepthProfile = tradingData.AverageDepthProfile;
  
            // TODO: The devison could be cumbersome, as rate can become very large   
            var cancellationRateDistribution = canceledOrderRateDistribution.Divide(averageDepthProfile);

            var output = "C:\\Users\\d90789\\Documents\\d-fine\\Trainings\\Oxford MSc in Mathematical Finance\\Thesis\\Source\\4 Output";
            averageDepthProfile.Save(Path.Combine(output, $"depth_profile.csv"));
            cancellationRateDistribution.Save(Path.Combine(output, $"cancellation_rate_distribution.csv"));

            // TODO: Is this correct here, there is a distibution so hence do the correct mean???
            // TODO: Improve the calculation of the discrete cumulative distribution function as
            // TODO: approximation of the continuous case 
            //var rate3 = cancellationRateDistribution.CummulativeDistributionFunction.Last(p => p.Key <= quantile3).Value;
            //var rate60 = cancellationRateDistribution.CummulativeDistributionFunction.Last(p => p.Key <= quantile60).Value;

            // var rate = (rate60 - rate3) / (quantile60 - quantile3);

            // return rate;

            return 0;
        }

        
        /// <summary>
        /// Calibrate model with LOB data  
        /// </summary>
        /// <param name="repository"></param>
        public static SmithFarmerModelParameter Calibrate(LobRepository repository)
        {
            if (!repository.TradingData.Any())
            {
                throw new ArgumentException("Cannot calibrate model without trading data");
            }
            Log.Info("Start calibrating model");

            var parameter = new SmithFarmerModelParameter();
            
            var marketOrderRates = new List<double>();
            var limitOrderRateDensities = new List<double>();
            var cancelationRateDensities = new List<double>();
            var characteristicSizes = new List<double>();
            var tickSizes  =new List<double>();
            

            // Measurement of the parameters mu(market order rate) and sigma(size) is
            // straightforward: to measure mu, for example, we simply compute
            // the total number of shares of market orders and divide by time
            // or, alternatively, we compute mu(t) for each day and average; we get
            // similar results in either case.
            foreach (var tradingDay in repository.TradingDays)
            {
                var tradingData = repository.TradingData[tradingDay];
                var sigma = CalibrateCharacteristicOrderSize(tradingData);
                var pi = tradingData.PriceTickSize;
                var mu = CalibrateMarketOrderRate(tradingData, sigma);

                // Determine the price band near the spread which will be used
                // for calibration. To this end, we determine the average depth provile (average depth vs. distance to best opposite quote)
                // and the 60% quantile of outstanding limit orders.  
                var averageDepthProfile = tradingData.AverageDepthProfile;
                // Probability(order.distanceBestOpposite <= distanceBestOppositeQuoteQuantile) = p
                var distanceBestOppositeQuoteQuantile = averageDepthProfile.Quantile(0.6);

                var alpha = CalibrateLimitOrderRate(tradingData, sigma, pi, distanceBestOppositeQuoteQuantile);
                var delta = CalibrateCancelationRate(tradingData, sigma, pi, distanceBestOppositeQuoteQuantile);

                Log.Info("===================================================================");
                Log.Info($"Calibration parameters for: {tradingDay: yyyy - MM - dd}");
                Log.Info("===================================================================");
                Log.Info($"Market order rate (mu): {mu} [sigma]/[time]");
                Log.Info($"Limit order rate density (alpha): {alpha} [sigma]/([time]*[tick])");
                Log.Info($"Cancellation rate density (delta): {delta} [sigma]/([time]*[depth])");
                Log.Info($"Characteristic size (sigma): {sigma}");
                Log.Info($"Tick size (pi): {pi}");
                
                characteristicSizes.Add(sigma);
                marketOrderRates.Add(mu);
                limitOrderRateDensities.Add(alpha);
                cancelationRateDensities.Add(delta);
                tickSizes.Add(pi);
            }

            Log.Info("===================================================================");
            Log.Info("Mean calibration parameters");
            Log.Info("===================================================================");

            parameter.CharacteristicOrderSize = characteristicSizes.Mean();
            parameter.LimitOrderRateDensity = limitOrderRateDensities.Mean();
            parameter.CancellationRate = cancelationRateDensities.Mean();
            parameter.MarketOrderRate = marketOrderRates.Mean();
            parameter.PriceTickSize = tickSizes.Mean();

            Log.Info($"Market order rate (mu): {parameter.MarketOrderRate} [sigma]/[time]");
            Log.Info($"Limit order rate density (alpha): {parameter.LimitOrderRateDensity} [sigma]/([time]*[tick])");
            Log.Info($"Cancellation rate density (delta): {parameter.CancellationRate} [sigma]/([time]*[depth])");
            Log.Info($"Characteristic size (sigma): {parameter.CharacteristicOrderSize}");
            Log.Info($"Tick size (pi): {parameter.PriceTickSize}");

            Log.Info("Finished calibrating model");

            return parameter;
        }
    }
}