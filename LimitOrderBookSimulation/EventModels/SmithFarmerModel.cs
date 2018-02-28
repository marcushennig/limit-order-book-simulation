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
    /// TODO: Sell as well as Buy side should have their own depth profile  
    /// TODO: Scale everything to ticks instead of prices
    ///  => set the corresponding best price to boundaries of the price interval
    /// Artifical double auction market proposed by E. Smith & J.D. Farmer, 
    /// Paper: "Quantitative finance 3.6 (2003): 481-514"
    /// Goal: Designed to capture long-run statistical properties of L(t) 
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
        #region Logging

        protected static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #endregion Logging

        #region Properties

        private RandomUtilities Random { set; get; }

        /// <summary>
        /// Underlying limit order book
        /// </summary>
        public LimitOrderBook LimitOrderBook { set; get; }

        #region Model parameter
        
        /// <summary>
        /// Inital depth profile on bid side
        /// </summary>
        public Dictionary<long, long> InitalBids { set; get; }

        /// <summary>
        /// Initial depth profile on ask side
        /// </summary>
        public Dictionary<long, long> InitalAsks { set; get; }

        /// <summary>
        /// Limit order rate in units of shares / (ticks * time)
        /// </summary>
        public double LimitOrderRateDensity { set; get; }
        
        /// <summary>
        /// Market order rate in units of shares / time
        /// </summary>
        public double MarketOrderRate { set; get; }

        /// <summary>
        /// Cancellation rate in 1/time
        /// </summary>
        public double CancellationRate { set; get; }
        
        /// <summary>
        /// Size of one tick in unites of price
        /// </summary>
        public double TickSize { private set; get; }
        
        /// <summary>
        /// Characteristic order size 
        /// </summary>
        public double CharacteristicOrderSize { set; get; }
        
        /// <summary>
        /// All prices are measured in units of ticks 
        /// </summary>
        public int MaxTick { set; get; }

        public int TickIntervalSize { set; get; }
        
        /// <summary>
        /// Work directory for saving log, intermediate states....
        /// </summary>
        public string WorkDirectory { set; get; }

        #endregion Model parameter

        #region Characteric scales

        public double CharacteristicNumberOfShares => MarketOrderRate / (2 * CancellationRate);
        public double CharacteristicPriceInterval => MarketOrderRate / (2 * LimitOrderRateDensity);
        public double CharacteristicTime => 1 / CancellationRate;
        public double NondimensionalTickSize => 2 * LimitOrderRateDensity * TickSize / MarketOrderRate;
        public double AsymptoticDepth => LimitOrderRateDensity / CancellationRate;
        public double BidAskSpread => MarketOrderRate/(2*LimitOrderRateDensity);
        public double Resolution => 2*LimitOrderRateDensity*TickSize/MarketOrderRate;
        
        /// <summary>
        /// NondimensionalOrderSize: epsilon
        /// [1] Large epsilon: epsilon > 0.1. In this regime large accumulation of orders at the
        /// best quotes is observed.The market impact is nearly linear, and long- and
        /// short-time diffusion rates are roughly equal.
        /// [2] Medium epsilon: epsilon ∼ 0.01. Here the accumulation of the orders at the best
        /// bid and ask is small, and the depth profile increases almost linearly in
        /// price.The price impact shows roughly a square root dependence on
        /// the order size.
        /// [3] Small epsilon: epsilon lt 0.001. In this range order accumulation at the best quotes
        /// is very small, the depth profile is a convex function of price near the
        /// midpoint and the price impact is very concave.
        /// </summary>
        public double NondimensionalOrderSize => 2 * CancellationRate * CharacteristicOrderSize / MarketOrderRate;

        #endregion Characteric scales

        #endregion Properties

        #region Constructors
        
        /// <summary>
        /// Empty constructor  
        /// </summary>
        public SmithFarmerModel()
        {
            Random = new RandomUtilities(43);
            LimitOrderBook = new LimitOrderBook();
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
            CharacteristicOrderSize = limitOrderSize;
            Log.Info($"Characteristic size: {CharacteristicOrderSize}");
            
            // Price unit
            TickSize = lob.PriceTickSize;
            
            // Some Statistics 
            Log.Info($"Number of market order events: {lob.MarketOrders.Count}");
            Log.Info($"Number of limit order events: {lob.LimitOrders.Count}");
            Log.Info($"Number of cancel order events: {lob.CanceledOrders.Count}");
            
            // Initialize sell and buy side of the limit order book
            var state = lob.States.First();
         
            InitalBids = state.Bids.ToDictionary(p => (long)(p.Key / TickSize), 
                                                 p => (long)Math.Ceiling(1*(p.Value) / CharacteristicOrderSize));

            InitalAsks = state.Asks.ToDictionary(p => (long) (p.Key/TickSize),
                                                 p => (long) Math.Ceiling(1*(p.Value) / CharacteristicOrderSize));
            
            // Determine the 'mu'-parameter in Famer & Smith's model 
            // Mu characterizes the average market order arrival rate and it is just the number of shares of 
            // effective market order ('buy' and 'sell') to the number of events during the trading day
            // Unit: [# shares / time]
            var nm = lob.MarketOrders.Sum(p => p.Volume) / CharacteristicOrderSize;
            //var nm = lob.MarketOrders.Count() * marketOrderSize / CharacteristicOrderSize;
            Log.Info($"Rescaled factor market order events: {marketOrderSize / CharacteristicOrderSize}");

            MarketOrderRate = nm / T;
            Log.Info($"Market order rate: {MarketOrderRate} [sigma]/[time]");

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
            var limitOrderDistribution = lob.LimitOrderDistribution.Scale(1 / TickSize, 1 / CharacteristicOrderSize);

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
            LimitOrderRateDensity = totalVolumeInRange / (T * priceRange) / 6; 
            Log.Info($"Limit order rate density: {LimitOrderRateDensity} [sigma]/([time]*[tick])");

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
                                               .Scale(1 / TickSize, 
                                                      1 / CharacteristicOrderSize);
            var averageDepthProfile = lob.AverageDepthProfile
                                         .Scale(1 / TickSize, 
                                                1 / CharacteristicOrderSize);
            
            // TODO: The devison could be cumbersome, as rate can become very large   
            var cancellationRateDistribution = canceledOrderDistribution.Divide(averageDepthProfile)
                                                                        .Scale(1, 1 / T);

            // TODO: Is this correct here, there is a distibution so hence do the correct mean???
            // TODO: Improve the calculation of the discrete cumulative distribution function as
            // TODO: approximation of the continuous case 
            var rate3 = cancellationRateDistribution.CummulativeDistributionFunction.Last(p => p.Key <= quantile3).Value;
            var rate60 = cancellationRateDistribution.CummulativeDistributionFunction.Last(p => p.Key <= quantile60).Value;

            var rate = (rate60 - rate3)/(quantile60 - quantile3);
            CancellationRate = rate; //cancellationRateDistribution.Data 
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

            Log.Info($"Cancellation rate density: {CancellationRate} [sigma]/([time]*[depth])");



            //lob.CanceledOrders.Count * canceledOrderSize / ( model.CharacteristicOrderSize * lob.TradingDuration );
            Log.Info("Finished calibrating model");
        }

        #endregion Constructors

        #region Methods
     
        #region Events

        #region Market orders

        /// <summary>
        /// Submit market buy order 
        /// </summary>
        private void SubmitMarketBuyOrder()
        {
            LimitOrderBook.SubmitMarketBuyOrder(amount: 1);
        }

        /// <summary>
        /// Submit market sell order
        /// </summary>
        private void SubmitMarketSellOrder()
        {
            LimitOrderBook.SubmitMarketSellOrder(amount: 1);
        }

        #endregion

        #region Cancel orders
        
        /// <summary>
        /// TODO: Cancel limit sell order
        /// </summary>
        private void CancelLimitSellOrder()
        {
            var ask = LimitOrderBook.Ask;
            var priceMin = ask;
            var priceMax = ask + TickIntervalSize;

            var n = LimitOrderBook.NumberOfSellOrders(priceMin, priceMax);

            // generate random price with the distribution in [priceMin, priceMax]
            var q = Random.Next(1, n);
            var price = LimitOrderBook.InverseCDFSellSide(priceMin, priceMax, q);

            LimitOrderBook.CancelLimitSellOrder(price, amount: 1);
        }

        /// <summary>
        ///  TODO: Cancel limit buy order
        /// </summary>
        private void CancelLimitBuyOrder()
        {
            var bid = LimitOrderBook.Bid;

            var priceMin = bid - TickIntervalSize;
            var priceMax = bid;

            var n = LimitOrderBook.NumberOfBuyOrders(priceMin, priceMax);

            // generate random price with the distribution in [priceMin, priceMax]
            var q = Random.Next(1, n);
            var price = LimitOrderBook.InverseCDFBuySide(priceMin, priceMax, q);

            LimitOrderBook.CancelLimitBuyOrder(price, amount: 1);
        }

        #endregion

        #region Limit orders

        /// <summary>
        /// Limit buy order @ time = t
        /// </summary>
        private void SubmitLimitBuyOrder()
        {
            //if (is.na(price))
            //{ prx << -(bestOffer() - pick(L))}
            //else prx << -price
            //if (logging == T) { eventLog[count,] << -c("LB", prx)}
            //book$buySize[book$Price == prx] << -book$buySize[book$Price == prx] + 1}

            var price = LimitOrderBook.Ask - Random.Next(1, TickIntervalSize);
            LimitOrderBook.SubmitLimitBuyOrder(price, amount:1);
        }

        /// <summary>
        /// Limit sell order @ time = t
        /// </summary>
        private void SubmitLimitSellOrder()
        {
            //    if (is.na(price))
            //{ prx << -(bestBid() + pick(L))}
            // else prx << -price
            // if (logging == T) { eventLog[count,] << -c("LS", prx)}
            //    book$sellSize[book$Price == prx] << -book$sellSize[book$Price == prx] + 1}

            var price = LimitOrderBook.Bid + Random.Next(1, TickIntervalSize);
            LimitOrderBook.SubmitLimitSellOrder(price, amount:1);
        }

        #endregion Limit orders

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

            CharacteristicOrderSize = characteristicSizes.Mean();
            LimitOrderRateDensity = limitOrderRateDensities.Mean();
            CancellationRate = cancelationRateDensities.Mean();
            MarketOrderRate = marketOrderRates.Mean();
            TickSize = tickSizes.Mean();

            Log.Info($"Market order rate (mu): {MarketOrderRate} [sigma]/[time]");
            Log.Info($"Limit order rate density (alpha): {LimitOrderRateDensity} [sigma]/([time]*[tick])");
            Log.Info($"Cancellation rate density (delta): {CancellationRate} [sigma]/([time]*[depth])");
            Log.Info($"Characteristic size (sigma): {CharacteristicOrderSize}");
            Log.Info($"Tick size (pi): {TickSize}");

            Log.Info("Finished calibrating model");

        }

        #endregion Calibration

        #region Simulation

        /// <summary>
        /// Run order flow simulation and store result in given file
        /// </summary>
        /// <param name="duration">In units of seconds</param>
        public void SimulateOrderFlow(double duration)
        {
            // Pseudo-code:
            // [1] Compute the best bid B(t) and best offer A(t).
            // [2] Compute the number of shares n_B on the bid side of the book from level A(t) - 1 to level A(t) - L.
            // [3] Compute the number of shares n_A on the offered side of the book from level B(t) + 1 to level B(t) + L.
            // [4] Draw a new event according to the relative probabilities {ℙMB, ℙMS, ℙLB, ℙLS, ℙCS, ℙCB} ~ {μ/2, μ/2, L * α, L * α, δ * nA, δ * nB}
            //      - If the selected event is a limit order, draw the relative price level from {1, 2,…, L}.
            //      - If the selected event is a cancelation, select randomly which order within the band to cancel.
            // [5] Update the order book and increment t.

            var startTradingTime = 0.0;
            var endTradingTime = startTradingTime + duration;

            // Scale prices to ticks and volumes to units of characteristic size

            LimitOrderBook.Time = startTradingTime;
            LimitOrderBook.InitializeDepthProfileBuySide(InitalBids);
            LimitOrderBook.InitializeDepthProfileSellSide(InitalAsks);

            // How to determine the interval tick size?? 
            // TODO: Choose it conservatively so as to ensure minimal edge effects.
            TickIntervalSize = 1000;

            // Rates are mesured per price 
            using (var progress = new ProgressBar(duration, "Calculate limit order book process"))
            {
                var limitOrderRate = LimitOrderRateDensity * TickIntervalSize;
                //var depthProfileFile = Path.Combine(WorkDirectory, "depth_profile.csv");

                // Save intial state of depth profile
                //LimitOrderBook.SaveDepthProfile(depthProfileFile);

                var dt = endTradingTime / 20;
                var T = startTradingTime + dt;

                // Initialize event probabilities
                var probability = new Dictionary<Action, double>
                {
                    {SubmitLimitSellOrder, 0},
                    {SubmitLimitBuyOrder, 0},
                    {SubmitMarketBuyOrder, 0},
                    {SubmitMarketSellOrder, 0},
                    {CancelLimitSellOrder, 0},
                    {CancelLimitBuyOrder, 0},
                };
               
                var t = startTradingTime;

                // Clean up limit order book
                LimitOrderBook.Time = t;
     
                while (t <= endTradingTime)
                {
                    var ask = LimitOrderBook.Ask;
                    var bid = LimitOrderBook.Bid;

                    //var nBidSide = LimitOrderBook.NumberOfBuyOrders(ask - TickIntervalSize, ask - 1);
                    //var nAskSide = LimitOrderBook.NumberOfSellOrders(bid + 1, bid + TickIntervalSize);
                    var nBidSide = LimitOrderBook.NumberOfBuyOrders(bid - TickIntervalSize, bid);
                    var nAskSide = LimitOrderBook.NumberOfSellOrders(ask, ask + TickIntervalSize);

                    //Console.WriteLine($"({nBidSide}, {nAskSide})");
                    var cancellatioRateSell = nAskSide * CancellationRate;
                    var cancellationRateBuy = nBidSide * CancellationRate;

                    // total event rate 
                    var eventRate = 2 * MarketOrderRate + 2 * limitOrderRate + 
                                    cancellatioRateSell + cancellationRateBuy;

                    // re-calculate probabilities of events
                    probability[SubmitLimitSellOrder] = limitOrderRate / eventRate;
                    probability[SubmitLimitBuyOrder] = limitOrderRate / eventRate;
                    probability[SubmitMarketBuyOrder] = MarketOrderRate / eventRate;
                    probability[SubmitMarketSellOrder] = MarketOrderRate / eventRate;
                    probability[CancelLimitBuyOrder] = cancellationRateBuy / eventRate;
                    probability[CancelLimitSellOrder] = cancellatioRateSell / eventRate;

                    t += Random.PickExponentialTime(eventRate);
                    Random.PickEvent(probability).Invoke();

                    if (!LimitOrderBook.Asks.Any() || !LimitOrderBook.Bids.Any())
                    {
                        throw new Exception("Either the bis or ask side is empty");
                    }

                    // Update time of limit order book 
                    // due to submitted order events 
                    LimitOrderBook.Time = t;

                    // Update progress bar
                    progress.Tick(t - startTradingTime);
                }
                progress.Finished();
            }
        }

        #endregion Simulation

        #region Utilities
        
        /// <summary>
        /// Save model in json 
        /// </summary>
        /// <param name="path"></param>
        public void Save(string path)
        {
            try
            {
                var jsonString = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(path, jsonString);
            }
            catch (Exception exception)
            {
                Log.Error("Could not save model calibration parameters");
                Log.Error($"Exception: {exception}");
            }

        }

        /// <summary>
        /// Calibrate model on LOB data and return result  
        /// </summary>
        /// <param name="path">path to calibration fil </param>
        public static SmithFarmerModel Load(string path)
        {
            try
            {
                var jsonString = File.ReadAllText(path);
                return JsonConvert.DeserializeObject<SmithFarmerModel>(jsonString);
            }
            catch (Exception exception)
            {
                Log.Error("Could load model");
                Log.Error($"Exception: {exception}");

                return new SmithFarmerModel();
            }
        }

        /// <summary>
        /// Save the price process to a file 
        /// </summary>
        /// <param name="fileName"></param>
        public void SavePriceProcess(string fileName)
        {
            using (var file = new StreamWriter(fileName))
            using (var progressBar = new ProgressBar(LimitOrderBook.PriceTimeSeries.Count, "Write price process to file"))
            {
                var done = 0;
                foreach (var entry in LimitOrderBook.PriceTimeSeries)
                {
                    var time = entry.Key;
                    var price = entry.Value;
                    
                    file.WriteLine($"{time}\t{price.Bid * TickSize}\t{price.Ask * TickSize}");

                    progressBar.Tick(++done);
                }
                progressBar.Finished();
            }
        }

        #endregion Utilities
        
        #endregion
    }
}