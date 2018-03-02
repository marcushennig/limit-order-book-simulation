using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using log4net;
using LimitOrderBookRepositories;
using LimitOrderBookRepositories.Interfaces;
using LimitOrderBookRepositories.Model;
using LimitOrderBookSimulation.LimitOrderBooks;
using LimitOrderBookUtilities;
using MathNet.Numerics.Statistics;
using Newtonsoft.Json;

namespace LimitOrderBookSimulation.EventModels
{
    /// <summary>
    /// TODO: Implement initialDepthProfile, should be property of the model 
    /// TODO: Scale everything to ticks instead of prices
    ///  => set the corresponding best price to boundaries of the price interval
    /// Artifical double auction market proposed by E. Smith & J.D. Farmer, 
    /// Paper: "Quantitative finance 3.6 (2003): 481-514"
    /// Goal: Designed to capture int-run statistical properties of L(t) 
    /// Assumptions:
    /// [.] All orders are for unit size sigma
    /// [.] All order ows governed by independent Poisson processes
    /// [.] Buy market orders arrive with fixed rate 
    /// [.] Sell market orders arrive with fixed rate 
    /// [.] Buy limit orders arrive with fixed rate  at all prices p &lt; a(t)
    /// [.] Sell limit orders arrive with ifxed rate  at all prices p &gt; b(t)
    /// [.] All active orders are cancelled with xed rate 
    /// </summary>
    public class SmithFarmerModel : IPriceProcess
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private ExtendedRandom Random { set; get; }
                
        /// <summary>
        /// Underlying limit order book
        /// Unit: Price: ticks
        ///       Depth: characteristic orderSize
        /// </summary>
        public LimitOrderBook LimitOrderBook { set; get; }
        
        /// <summary>
        /// Model paramater
        /// </summary>
        public SmithFarmerModelParameter Parameter { set; get; }
        
        #region Characteric scales

        public double CharacteristicNumberOfShares => Parameter.MarketOrderRate / (2 * Parameter.CancellationRate);
        public double CharacteristicPriceInterval => Parameter.MarketOrderRate / (2 * Parameter.LimitOrderRateDensity);
        public double CharacteristicTime => 1 / Parameter.CancellationRate;
        public double NondimensionalTickSize => 2 * Parameter.LimitOrderRateDensity * Parameter.PriceTickSize / Parameter.MarketOrderRate;
        public double AsymptoticDepth => Parameter.LimitOrderRateDensity / Parameter.CancellationRate;
        public double BidAskSpread => Parameter.MarketOrderRate / (2 * Parameter.LimitOrderRateDensity);
        public double Resolution => 2 * Parameter.LimitOrderRateDensity * Parameter.PriceTickSize / Parameter.MarketOrderRate;
        
        /// <summary>
        /// NondimensionalOrderSize: epsilon
        /// [1] Large epsilon: epsilon > 0.1. In this regime large accumulation of orders at the
        /// best quotes is observed.The market impact is nearly linear, and int- and
        /// short-time diffusion rates are roughly equal.
        /// [2] Medium epsilon: epsilon ∼ 0.01. Here the accumulation of the orders at the best
        /// bid and ask is small, and the depth profile increases almost linearly in
        /// price.The price impact shows roughly a square root dependence on
        /// the order size.
        /// [3] Small epsilon: epsilon lt 0.001. In this range order accumulation at the best quotes
        /// is very small, the depth profile is a convex function of price near the
        /// midpoint and the price impact is very concave.
        /// </summary>
        public double NondimensionalOrderSize => 2 * Parameter.CancellationRate * Parameter.CharacteristicOrderSize / Parameter.MarketOrderRate;

        #endregion Characteric scales

        #region Constructors
        
        /// <summary>
        /// Empty constructor  
        /// </summary>
        public SmithFarmerModel()
        {
            Random = new ExtendedRandom(43);
            LimitOrderBook = new LimitOrderBook();
            Parameter = new SmithFarmerModelParameter();
        }
        
        /// <summary>
        /// Calibrate the Smith Farmer model from LOB data 
        /// </summary>
        /// <param name="lob"></param>
        public SmithFarmerModel(LobTradingData lob) : this()
        {
            if (!lob.Events.Any())
            {
                throw new ArgumentException("Cannot calibrate model from empty data");
            }

            Log.Info("Start calibrating model");
            
            // It is impossible to simulate order arrivals and cancelations at integer price levels from −∞ to −∞
            // So consider only order arrivals and cancelations in a moving band of width centered around
            // the current best quotes.
            //  - TODO: L should be chosen conservatively so as to ensure minimal edge effects.
            //  - Within the band, the arrival rate of limit orders is α, cancelation rate is δ times outstanding shares.
            //  - Outside the band, orders may neither arrive nor be canceled.
 
            // Parameter estimation
            // Compute the average size of market, limit and canceled orders
            // => Sl, Sm, Sc
            var limitOrderSize = lob.LimitOrders.Select(order => (double)order.Volume).Mean();
            var marketOrderSize = lob.MarketOrders.Select(order => (double)order.Volume).Mean();
            var canceledOrderSize = lob.CanceledOrders.Select(order => (double)order.Volume).Mean();


            var T = lob.TradingDuration;

            Log.Info($"Avereage limit order size: {limitOrderSize}");
            Log.Info($"Avereage market order size: {marketOrderSize}");
            Log.Info($"Avereage canceled order size: {canceledOrderSize}");
            Log.Info($"Trading duration: {T}");


            // Characteristic size 
            Parameter.CharacteristicOrderSize = limitOrderSize;
            Log.Info($"Characteristic size: {Parameter.CharacteristicOrderSize}");
            
            // Price unit
            Parameter.PriceTickSize = lob.PriceTickSize;
            
            // Some Statistics 
            Log.Info($"Number of market order events: {lob.MarketOrders.Count}");
            Log.Info($"Number of limit order events: {lob.LimitOrders.Count}");
            Log.Info($"Number of cancel order events: {lob.CanceledOrders.Count}");
            
            // Initialize sell and buy side of the limit order book
            var state = lob.States.First();
         
            var initalBids = state.Bids.ToDictionary(p => (int)(p.Key / Parameter.PriceTickSize), 
                                                 p => (int)Math.Ceiling(1*(p.Value) / Parameter.CharacteristicOrderSize));

            var initalAsks = state.Asks.ToDictionary(p => (int) (p.Key/Parameter.PriceTickSize),
                                                 p => (int) Math.Ceiling(1*(p.Value) / Parameter.CharacteristicOrderSize));
            
            LimitOrderBook.InitializeDepthProfileBuySide(initalBids);
            LimitOrderBook.InitializeDepthProfileSellSide(initalAsks);
            LimitOrderBook.Time = 0;
            
            // Determine the 'mu'-parameter in Famer & Smith's model 
            // Mu characterizes the average market order arrival rate and it is just the number of shares of 
            // effective market order ('buy' and 'sell') to the number of events during the trading day
            // Unit: [# shares / time]
            var nm = lob.MarketOrders.Sum(p => p.Volume) / Parameter.CharacteristicOrderSize;
            //var nm = lob.MarketOrders.Count() * marketOrderSize / CharacteristicOrderSize;
            Log.Info($"Rescaled factor market order events: {marketOrderSize / Parameter.CharacteristicOrderSize}");

            Parameter.MarketOrderRate = nm / T;
            Log.Info($"Market order rate: {Parameter.MarketOrderRate} [sigma]/[time]");

            //***************************************************************************************************************
            // Error:
            // Select only limit order events events that fall into window where price < price_60%
            // Check Paper: PNAS: The predictive power of zero intelligence models in financial markes
            // C:\Users\d90789\Documents\Oxford MSc in Mathematical Finance\Thesis\Literature\Farmer Smith Model
            //
            //***************************************************************************************************************


            //*** Martin Gould suggested to calibrate in a closed window near the spread 
            //*** If level would be say 100, but for small levels no need 

            // Roughly 70% of all orders are placed either at
            // the best price or inside the spread. Outside the spread the density
            // of limit order placement falls o as a power law as a function of
            // the distance from the best prices
            // Determine the number of orders within a q_3 and q_60, where q_n is the n quantil 
            // of the distribution of orders, any strategy for estimating the density 
            // q_60 is made in a compromise to include as much data as possible for 
            //statistical stability, but not so much as to
            // include orders that are unlikely to ever be executed, and therefore
            // unlikely to have any effect on prices.
            var limitOrderDistribution = lob.LimitOrderDistribution.Scale(1 / Parameter.PriceTickSize, 1 / Parameter.CharacteristicOrderSize);

            // TODO: Should be properties of the model, 
            // TODO: use  the percentage of submitted limitorders  
            var quantile60 = limitOrderDistribution.Quantile(0.7);
            var quantile3 = limitOrderDistribution.Quantile(0.01);

            var volume3 = limitOrderDistribution.CummulativeDistributionFunction.Last(p => p.Key <= quantile3).Value;
            var volume70 = limitOrderDistribution.CummulativeDistributionFunction.Last(p => p.Key <= quantile60).Value;

            var totalVolumeInRange = volume70 - volume3;
             
            var priceRange = (quantile60 - quantile3);
            
            Log.Info($"Rescaled factor limit order events: {totalVolumeInRange}");
            Log.Info($"Price interval (Ask+Bid): {priceRange}");

            // Divide by factor of 2, as sell/buy 
            Parameter.LimitOrderRateDensity = totalVolumeInRange / (T * priceRange) / 6; 
            Log.Info($"Limit order rate density: {Parameter.LimitOrderRateDensity} [sigma]/([time]*[tick])");

            // Old Calibration: Quite bad at this point 
            // Try to define an interval in which most limit orders fall 
            //var dP = (lob.AverageAskSideIntervalSize + lob.AverageBuySideIntervalSize) / TickSize;
            //var nl = lob.LimitOrders.Sum(p => p.Volume) / CharacteristicOrderSize;
            ////var nl = lob.LimitOrders.Count * limitOrderSize / CharacteristicOrderSize;

            //Log.Info($"Rescaled factor limit order events: {limitOrderSize / CharacteristicOrderSize}");
            //Log.Info($"Price interval (Ask+Bid): {dP}");

            //LimitOrderRateDensity = nl / (T * dP);
            //Log.Info($"Limit order rate density: {LimitOrderRateDensity} [sigma]/([time]*[tick])");

            // TODO: Think more clearly about cancellation rate 
            // TODO: Work within a narrow band within the 3%-60% 
            // TODO: Quantile of the limit order distribution
            // Cancellations occuring at each price level with a rate 
            // propotional to the depth at this price 
            
            var canceledOrderDistribution = lob.CanceledOrderDistribution
                                               .Scale(1 / Parameter.PriceTickSize, 
                                                      1 / Parameter.CharacteristicOrderSize);
            var averageDepthProfile = lob.AverageDepthProfile
                                         .Scale(1 / Parameter.PriceTickSize, 
                                                1 / Parameter.CharacteristicOrderSize);
            
            // TODO: The devison could be cumbersome, as rate can become very large   
            var cancellationRateDistribution = canceledOrderDistribution.Divide(averageDepthProfile)
                                                                        .Scale(1, 1 / T);

            // TODO: Is this correct here, there is a distibution so hence do the correct mean???
            // TODO: Improve the calculation of the discrete cumulative distribution function as
            // TODO: approximation of the continuous case 
            var rate3 = cancellationRateDistribution.CummulativeDistributionFunction.Last(p => p.Key <= quantile3).Value;
            var rate60 = cancellationRateDistribution.CummulativeDistributionFunction.Last(p => p.Key <= quantile60).Value;

            var rate = (rate60 - rate3)/(quantile60 - quantile3);
            Parameter.CancellationRate = rate; //cancellationRateDistribution.Data 
                                     //                      .Select(p => p.Value)
                                     //                      .Mean();

            //var cancellationDistribution = lob.CanceledOrders.GroupBy(p => p.DistanceBestOppositeQuote)
            //                          .Select(q => new
            //                          {
            //                              Distance = q.Key / TickSize, //Distance to best opposite quote in units of ticks 
            //                              Nc = q.Count() * canceledOrderSize / CharacteristicOrderSize    // Number of canceles order in this bin
            //                          }).ToList();

           
            //Log.Info($"Rescaled factor cancelled order events: {canceledOrderSize / CharacteristicOrderSize}");
            
            ////  Scale average depth distribution by to tick and characteristic size
            //var averageDepthDistribution = lob.AverageNumberOfOutstandingLimitOrders()
            //                                  .ToDictionary(p => p.Key / TickSize, 
            //                                                p => p.Value / CharacteristicOrderSize);

            //// Divide rate at distince by average depth  
            //CancellationRate = cancellationDistribution.Where(p => averageDepthDistribution.ContainsKey(p.Distance))
                                                             //.Select(p => p.Nc / (T * averageDepthDistribution[p.Distance]))
                                                             //.Mean();

            Log.Info($"Cancellation rate density: {Parameter.CancellationRate} [sigma]/([time]*[depth])");



            //lob.CanceledOrders.Count * canceledOrderSize / ( model.CharacteristicOrderSize * lob.TradingDuration );
            Log.Info("Finished calibrating model");
        }

        #endregion Constructors
     
        #region Events
        
        /// <summary>
        /// Function signature  
        /// </summary>
        private delegate int LimitOrderBookEvent();
        
        /// <summary>
        /// Submit market buy order 
        /// </summary>
        public int SubmitMarketBuyOrder()
        {
            return LimitOrderBook.SubmitMarketBuyOrder(amount: 1);
        }

        /// <summary>
        /// Submit market sell order
        /// </summary>
        public int SubmitMarketSellOrder()
        {
            
            return LimitOrderBook.SubmitMarketSellOrder(amount: 1);
        }
        
        /// <summary>
        ///  Cancel limit sell order by selecting the price randomly from
        ///  the interval [ask, ask + SimulationIntervalSize] using the depth as weight.
        /// </summary>
        public int CancelLimitSellOrder()
        {
          var ask = LimitOrderBook.Ask;
          var weightedPriceTicks = LimitOrderBook.DepthSellSide
                .Where(p => p.Key >= ask && 
                            p.Key <= ask + Parameter.SimulationIntervalSize)
                .ToDictionary(s => s.Key, 
                              s => s.Value);
            
            var priceTick = Random.NextFromWeights(weightedPriceTicks);
            LimitOrderBook.CancelLimitSellOrder(price:priceTick, amount: 1);
            
            return priceTick;
        }

        /// <summary>
        ///  Cancel limit buy order by selecting the price randomly from
        ///  the interval [bid - SimulationIntervalSize, bid] using the depth as weight
        /// </summary>
        public int CancelLimitBuyOrder()
        {
            var bid = LimitOrderBook.Bid;
            var weightedPriceTicks = LimitOrderBook.DepthBuySide
                .Where(p => p.Key >= bid - Parameter.SimulationIntervalSize && 
                            p.Key <= bid)
                .ToDictionary(s => s.Key, 
                              s => s.Value);
            
            var priceTick = Random.NextFromWeights(weightedPriceTicks);
            LimitOrderBook.CancelLimitBuyOrder(price:priceTick, amount: 1);
            
            return priceTick;
        }

        /// <summary>
        /// Limit buy order @ time = t
        /// </summary>
        public int SubmitLimitBuyOrder()
        {
            var maxTick = LimitOrderBook.Ask - 1;
            var minTick = maxTick - Parameter.SimulationIntervalSize;
            var priceTick = Random.Next(minTick, maxTick);
            
            LimitOrderBook.SubmitLimitBuyOrder(price:priceTick, amount:1);
            
            return priceTick;
        }

        /// <summary>
        /// Limit sell order @ time = t
        /// </summary>
        public int SubmitLimitSellOrder()
        {
            var minTick = LimitOrderBook.Bid + 1;
            var maxTick = minTick + Parameter.SimulationIntervalSize;
            var priceTick = Random.Next(minTick, maxTick);

            LimitOrderBook.SubmitLimitSellOrder(price:priceTick, amount:1);
            
            return priceTick;
        }

        #endregion Events

        #region Calibration
        
        /// <summary>
        /// Determine the characteristic order size
        /// </summary>
        /// <param name="tradingData">LOB data for a trading day</param>
        /// <returns></returns>
        private static double CalibrateCharacteristicOrderSize(LobTradingData tradingData)
        {
            // => Sl, Sm, Sc
            var limitOrderSize = tradingData.LimitOrders
                .Select(order => (double)order.Volume)
                .Mean();

            var marketOrderSize = tradingData.MarketOrders
                .Select(order => (double)order.Volume)
                .Mean();
                
            var canceledOrderSize = tradingData.CanceledOrders
                .Select(order => (double)order.Volume)
                .Mean();
            
            return (limitOrderSize + marketOrderSize + canceledOrderSize) / 3.0;
        }

        /// <summary>
        /// Determine the 'mu'-parameter in Famer & Smith's model 
        /// Mu characterizes the average market order arrival rate and it is just the number of shares of 
        /// effective market order ('buy' and 'sell') to the number of events during the trading day
        /// Unit: [# shares / time]
        /// </summary>
        /// <param name="tradingData">LOB data for a trading day</param>
        /// <param name="characteristicOrderSize">Characteristic order size</param>
        /// <returns></returns>
        private static double CalibrateMarketOrderRate(LobTradingData tradingData, double characteristicOrderSize)
        {
            var duration = tradingData.TradingDuration;
            var totalVolumeOfMarketOrders = tradingData.MarketOrders.Sum(p => p.Volume);

            var numberOfMarketOrders = totalVolumeOfMarketOrders / characteristicOrderSize;
            var marketOrderRate = numberOfMarketOrders / duration;

            // Divide by factor 2 to get rate 
            // for either sell or buy side
            return marketOrderRate / 2;
        }

        /// <summary>
        /// Calibrate limit order rate density from given trading data (assume that rate for sell and buy are equal). 
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
        private static double CalibrateLimitOrderRate(LobTradingData tradingData, 
            double characteristicOrderSize, 
            double tickSize, double distanceBestOppositeQuoteQuantile)
        {
            
            // Price range in ticks within which limit order events will be counted 
            var priceRangeInTicks = distanceBestOppositeQuoteQuantile / tickSize;

            // var output = "C:\\Users\\d90789\\Documents\\Oxford MSc in Mathematical Finance\\Thesis\\Lob\\4 Output";
            // averageDepthProfile.Save(Path.Combine(output, $"depth_profile.csv"));

            // Count the number of limit sell and buy orders that where placed within price band in 
            // units of the characteristic order size 
            var countOfLimitOrders = tradingData.LimitOrders
                .Where(order => order.DistanceBestOppositeQuote < distanceBestOppositeQuoteQuantile)
                .Sum(order => order.Volume) / characteristicOrderSize;

          
            // Determine the rate density of limit sell and buy orders 
            var limitOrderRateDensity = countOfLimitOrders / (tradingData.TradingDuration * priceRangeInTicks);

            // Divide by factor 2 to get rate 
            // for either sell or buy side
            return limitOrderRateDensity / 2;
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
        private static double CalibrateCancelationRate(LobTradingData tradingData, 
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
        public void Calibrate(LobRepository repository)
        {
            if (!repository.TradingData.Any())
            {
                //throw new ArgumentException("Cannot calibrate model without trading data");
            }

            Log.Info("Start calibrating model");

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

            Parameter.CharacteristicOrderSize = characteristicSizes.Mean();
            Parameter.LimitOrderRateDensity = limitOrderRateDensities.Mean();
            Parameter.CancellationRate = cancelationRateDensities.Mean();
            Parameter.MarketOrderRate = marketOrderRates.Mean();
            Parameter.PriceTickSize = tickSizes.Mean();

            Log.Info($"Market order rate (mu): {Parameter.MarketOrderRate} [sigma]/[time]");
            Log.Info($"Limit order rate density (alpha): {Parameter.LimitOrderRateDensity} [sigma]/([time]*[tick])");
            Log.Info($"Cancellation rate density (delta): {Parameter.CancellationRate} [sigma]/([time]*[depth])");
            Log.Info($"Characteristic size (sigma): {Parameter.CharacteristicOrderSize}");
            Log.Info($"Tick size (pi): {Parameter.PriceTickSize}");

            Log.Info("Finished calibrating model");

        }

        #endregion Calibration

        #region Simulation
        
        /// <summary>
        // Pseudo-code:
        // [1] Compute the best bid B(t) and best offer A(t).
        // [2] Compute the number of shares n_B on the bid side of the book from level A(t) - 1 to level A(t) - L.
        // [3] Compute the number of shares n_A on the offered side of the book from level B(t) + 1 to level B(t) + L.
        // [4] Draw a new event according to the relative probabilities {ℙMB, ℙMS, ℙLB, ℙLS, ℙCS, ℙCB} ~ {μ/2, μ/2, L * α, L * α, δ * nA, δ * nB}
        //      - If the selected event is a limit order, draw the relative price level from {1, 2,…, L}.
        //      - If the selected event is a cancelation, select randomly which order within the band to cancel.
        // [5] Update the order book and increment t.
        /// </summary>
        /// <param name="duration">In units of seconds</param>
        public void SimulateOrderFlow(double duration)
        {
            var t0 = LimitOrderBook.Time;
            var tEnd = t0 + duration;

            // Rates are measured per price 
            //using (var progress = new ProgressBar(duration, "Calculate limit order book process"))
            //{
                var limitOrderRate = Parameter.LimitOrderRateDensity * Parameter.SimulationIntervalSize;

                // Initialize event probabilities
                var probability = new Dictionary<LimitOrderBookEvent, double>
                {
                    {SubmitLimitSellOrder, 0},
                    {SubmitLimitBuyOrder, 0},
                    {SubmitMarketBuyOrder, 0},
                    {SubmitMarketSellOrder, 0},
                    {CancelLimitSellOrder, 0},
                    {CancelLimitBuyOrder, 0},
                };
               
                var t = t0;
                while (t <= tEnd)
                {
                    var ask = LimitOrderBook.Ask;
                    var bid = LimitOrderBook.Bid;

                    //var nBidSide = LimitOrderBook.NumberOfBuyOrders(ask - TickIntervalSize, ask - 1);
                    //var nAskSide = LimitOrderBook.NumberOfSellOrders(bid + 1, bid + TickIntervalSize);
                    var nBidSide = LimitOrderBook.NumberOfBuyOrders(bid - Parameter.SimulationIntervalSize, bid);
                    var nAskSide = LimitOrderBook.NumberOfSellOrders(ask, ask + Parameter.SimulationIntervalSize);

                    //Console.WriteLine($"({nBidSide}, {nAskSide})");
                    var cancellationRateSell = nAskSide * Parameter.CancellationRate;
                    var cancellationRateBuy = nBidSide * Parameter.CancellationRate;

                    // total event rate 
                    var eventRate = 2 * Parameter.MarketOrderRate + 2 * limitOrderRate + 
                                    cancellationRateSell + cancellationRateBuy;

                    // re-calculate probabilities of events
                    probability[SubmitLimitSellOrder] = limitOrderRate / eventRate;
                    probability[SubmitLimitBuyOrder] = limitOrderRate / eventRate;
                    probability[SubmitMarketBuyOrder] = Parameter.MarketOrderRate / eventRate;
                    probability[SubmitMarketSellOrder] = Parameter.MarketOrderRate / eventRate;
                    probability[CancelLimitBuyOrder] = cancellationRateBuy / eventRate;
                    probability[CancelLimitSellOrder] = cancellationRateSell / eventRate;

                    t += Random.NextExponentialTime(eventRate);
                    
                    var orderFlowEvent = Random.NextFromProbabilities(probability);
                    orderFlowEvent.Invoke();

                    if (!LimitOrderBook.DepthSellSide.Any() || !LimitOrderBook.DepthBuySide.Any())
                    {
                        throw new Exception("Either the bid or ask side is empty");
                    }

                    // Update time of limit order book 
                    // due to submitted order events 
                    LimitOrderBook.Time = t;

                    // Update progress bar
                    //progress.Tick(t - t0);
                //}
                //progress.Finished();
            }
        }

        #endregion Simulation

        #region Utilities
      
        /// <summary>
        /// Save the price process to a file 
        /// </summary>
        /// <param name="fileName"></param>
        public void SavePriceProcess(string fileName)
        {
            using (var file = new StreamWriter(fileName))
            {
                foreach (var entry in LimitOrderBook.PriceTimeSeries)
                {
                    var time = entry.Key;
                    var price = entry.Value;
                    
                    file.WriteLine($"{time}\t" +
                                   $"{price.Bid * Parameter.PriceTickSize}\t" +
                                   $"{price.Ask * Parameter.PriceTickSize}");
                }
            }
        }
        
        /// <summary>
        /// Save current depth profile 
        /// </summary>
        /// <param name="fileName"></param>
        public void SaveDepthProfile(string fileName)
        {
            LimitOrderBook.SaveDepthProfile(fileName);
        }

        #endregion Utilities
    }
}