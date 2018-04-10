using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using log4net;
using LimitOrderBookSimulation.LimitOrderBooks;
using LimitOrderBookUtilities;
using NUnit.Framework.Constraints;

namespace LimitOrderBookSimulation.EventModels
{
    /// <summary>
    /// Artifical double auction market proposed by E. Smith & J.D. Farmer, 
    /// Paper: "Quantitative finance 3.6 (2003): 481-514"
    /// Assumptions:
    /// [.] All orders are for unit size sigma
    /// [.] All order flows governed by independent Poisson processes
    /// [.] Buy market orders arrive with fixed rate 'mu'
    /// [.] Sell market orders arrive with fixed rate 'mu'
    /// [.] Buy limit orders arrive with fixed rate 'alpha' at all prices p &lt; a(t)
    /// [.] Sell limit orders arrive with fixed rate 'alpha' at all prices p &gt; b(t)
    /// [.] All active orders are cancelled with fixed rate 'delta'
    /// </summary>
    public class SmithFarmerModel
    {
        #region Logging
        
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #endregion
        
        #region Properties
        
        private ExtendedRandom Random { set; get; }
                
        /// <summary>
        /// Underlying limit order book
        /// Unit: Price: ticks
        ///       Depth: characteristic orderSize
        /// </summary>
        public ILimitOrderBook LimitOrderBook { set; get; }
        
        /// <summary>
        /// Model paramater
        /// </summary>
        public SmithFarmerModelParameter Parameter { set; get; }

        #endregion
        
        #region Constructor 
        
        /// <summary>
        /// Empty constructor  
        /// </summary>
        public SmithFarmerModel()
        {
            LimitOrderBook = new LimitOrderBook();
            Parameter = new SmithFarmerModelParameter();
        }

        /// <inheritdoc />
        /// <summary>
        /// Initialize the model with calibrated parameters and set initial
        /// state of the limit order book
        /// </summary>
        /// <param name="calibrated">Calibrated model parameter</param>
        /// <param name="initialBids">Initial depth profile of buy side of LOB</param>
        /// <param name="initialAsks">Initial depth profile of sell side of LOB</param>
        /// <param name="simulationIntervalSize">Size of the simulation interval</param>
        public SmithFarmerModel(SmithFarmerModelParameter calibrated, 
                                IDictionary<int, int> initialBids,
                                IDictionary<int, int> initialAsks,
                                int simulationIntervalSize) : this()
        {
            #region Model parameter
            
            Parameter.Seed = 42;                   
            Parameter.SimulationIntervalSize = simulationIntervalSize;
            
            Parameter.CharacteristicOrderSize = calibrated.CharacteristicOrderSize;
            Parameter.PriceTickSize = calibrated.PriceTickSize;            
            Parameter.MarketOrderRate = calibrated.MarketOrderRate;
            Parameter.CancellationRate = calibrated.CancellationRate;
            Parameter.LimitOrderRateDensity = calibrated.LimitOrderRateDensity;
                       
            #endregion

            #region Calibration information 

            Parameter.LowerQuantile = calibrated.LowerQuantile;
            Parameter.LowerQuantileProbability = calibrated.LowerQuantileProbability;
            Parameter.UpperQuantile = calibrated.UpperQuantile;
            Parameter.UpperQuantileProbability = calibrated.UpperQuantileProbability;
            
            #endregion
            
            #region Initialize sell and buy side of the limit order book
  
            LimitOrderBook.InitializeDepthProfileBuySide(initialBids);
            LimitOrderBook.InitializeDepthProfileSellSide(initialAsks);
            LimitOrderBook.Time = 0;
            
            #endregion
            
            Log.Info("Use calibrated model parameters");
            Log.Info($"Characteristic size: {Parameter.CharacteristicOrderSize}");
            Log.Info($"Price tick size: {Parameter.PriceTickSize}");
            Log.Info($"Limit order rate density: {Parameter.LimitOrderRateDensity} [sigma]/([time]*[tick])");
            Log.Info($"Market order rate: {Parameter.MarketOrderRate} [sigma]/[time]");
            Log.Info($"Cancellation rate density: {Parameter.CancellationRate} [sigma]/([time]*[depth])");
        }
     
        #endregion
        
        #region Methods
        
        #region Limit order book Events
        
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
            // Flexible simulation window best opposite quote
            var minTick = LimitOrderBook.Ask;
            var maxTick = minTick + Parameter.SimulationIntervalSize;
            
            // Fixed simulation window best opposite quote  
            //var minTick = LimitOrderBook.Ask;
            //var maxTick = LimitOrderBook.Bid + 1 + Parameter.SimulationIntervalSize;
            
            var priceTick = LimitOrderBook.GetRandomPriceFromSellSide(Random, minTick, maxTick);
           
            LimitOrderBook.CancelLimitSellOrder(price:priceTick, amount: 1);
            
            return priceTick;
        }

        /// <summary>
        ///  Cancel limit buy order by selecting the price randomly from
        ///  the interval [bid - SimulationIntervalSize, bid] using the depth as weight
        /// </summary>
        public int CancelLimitBuyOrder()
        {
            // Flexible simulation window best opposite quote
            var maxTick = LimitOrderBook.Bid;
            var minTick = maxTick - Parameter.SimulationIntervalSize;
            
            // Fixed simulation window best opposite quote  
            //var maxTick = LimitOrderBook.Bid;
            //var minTick = LimitOrderBook.Ask - 1 - Parameter.SimulationIntervalSize;

            var priceTick = LimitOrderBook.GetRandomPriceFromBuySide(Random, minTick, maxTick);

            LimitOrderBook.CancelLimitBuyOrder(price:priceTick, amount: 1);
            
            return priceTick;
        }

        /// <summary>
        /// Limit buy order @ time = t
        /// </summary>
        public int SubmitLimitBuyOrder()
        {
            // Flexible simulation window best opposite quote
            var priceMax = LimitOrderBook.Ask - 1;
            var priceMin = LimitOrderBook.Bid - Parameter.SimulationIntervalSize;
            
            var priceTick = priceMin + Random.Next(priceMax - priceMin + 1);
            
            // Fixed simulation window best opposite quote  
            //var priceTick = LimitOrderBook.Ask - 1 - Random.Next(Parameter.SimulationIntervalSize + 1);
            
            LimitOrderBook.SubmitLimitBuyOrder(priceTick, amount:1);
           
            return priceTick;
        }

        /// <summary>
        /// Limit sell order @ time = t
        /// </summary>
        public int SubmitLimitSellOrder()
        {
            var priceMin = LimitOrderBook.Bid + 1;
            var priceMax = LimitOrderBook.Ask + Parameter.SimulationIntervalSize;
            
            var priceTick = priceMin + Random.Next(priceMax - priceMin + 1);

            // Fixed simulation window best opposite quote  
            // var priceTick = LimitOrderBook.Bid + 1 + Random.Next(Parameter.SimulationIntervalSize + 1);
            
            LimitOrderBook.SubmitLimitSellOrder(priceTick, amount:1);
           
            return priceTick;
        }

        #endregion Events

        /// <summary>
        /// </summary>
        /// <param name="duration">In units of seconds</param>
        /// <param name="useSeed">True if results shoule be reproducible otherwise false</param>
        public void SimulateOrderFlow(double duration, bool useSeed=true)
        {
            Random = useSeed ? new ExtendedRandom(Parameter.Seed) : new ExtendedRandom();

            var t0 = LimitOrderBook.Time;
            var tEnd = t0 + duration;


            // Initialize event probabilities
            var probability = new Dictionary<LimitOrderBookEvent, double>
            {
                {SubmitLimitSellOrder, 0},
                {SubmitLimitBuyOrder, 0},
                {SubmitMarketBuyOrder, 0},
                {SubmitMarketSellOrder, 0},
                {CancelLimitSellOrder, 0},
                {CancelLimitBuyOrder, 0}
            };
           
            var t = t0;
            while (t <= tEnd)
            {
                var ask = LimitOrderBook.Ask;
                var bid = LimitOrderBook.Bid;
                var spread = ask - bid;
                
                // Fixed simulation window best opposite quote  
                // var limitOrderRate = Parameter.LimitOrderRateDensity * Parameter.SimulationIntervalSize;
                var limitOrderRate = Parameter.LimitOrderRateDensity * (Parameter.SimulationIntervalSize + spread);
                
                // Fixed simulation window to oposite quite 
                //var nBidSide = LimitOrderBook.NumberOfBuyOrders(ask - 1 - Parameter.SimulationIntervalSize, ask - 1);
                //var nAskSide = LimitOrderBook.NumberOfSellOrders(bid + 1, bid + 1 + Parameter.SimulationIntervalSize);
                
                // Flexible simulation window from best price 
                var nBidSide = LimitOrderBook.NumberOfBuyOrders(bid - Parameter.SimulationIntervalSize, bid);
                var nAskSide = LimitOrderBook.NumberOfSellOrders(ask, ask + Parameter.SimulationIntervalSize);

                var cancellationRateSell = nAskSide * Parameter.CancellationRate;
                var cancellationRateBuy = nBidSide * Parameter.CancellationRate;

                // total event rate 
                var eventRate = 2 * Parameter.MarketOrderRate + 
                                2 * limitOrderRate + 
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
                
                
                // Workaround to prevent undefined state of limit order book
                // Force the algorithm to submit a limit order 
                if (nAskSide <= 1 && (orderFlowEvent == SubmitMarketBuyOrder || orderFlowEvent == CancelLimitSellOrder) ||
                    nBidSide <= 1 && (orderFlowEvent == SubmitMarketSellOrder || orderFlowEvent == CancelLimitBuyOrder))
                {
                    continue;                    
                }
                
                orderFlowEvent.Invoke();
                
                // Check if undefined state was reached 
                if (LimitOrderBook.IsBuySideEmpty() || 
                    LimitOrderBook.IsSellSideEmpty())
                {
                    throw new Exception("Either the bid or ask side is empty");
                }

                // Update time of limit order book 
                // due to submitted order events 
                LimitOrderBook.Time = t;
            }
        }
        
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
        
        #endregion
    }
}