using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using log4net;
using LimitOrderBookRepositories.Model;
using LimitOrderBookSimulation.LimitOrderBooks;
using LimitOrderBookUtilities;

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
        /// <param name="calibrated"></param>
        /// <param name="intialDepthProfile"></param>
        public SmithFarmerModel(SmithFarmerModelParameter calibrated, 
                                LobState intialDepthProfile) : this()
        {
            #region Model parameter
            
            Parameter.CharacteristicOrderSize = calibrated.CharacteristicOrderSize;
            Parameter.PriceTickSize = calibrated.PriceTickSize;            
            Parameter.MarketOrderRate = calibrated.MarketOrderRate;
            Parameter.CancellationRate = calibrated.CancellationRate;
            Parameter.LimitOrderRateDensity = calibrated.LimitOrderRateDensity;
            Parameter.Seed = 42;
                       
            #endregion

            #region Initialize sell and buy side of the limit order book

           var initalBids = intialDepthProfile.Bids.ToDictionary(p => (int)(p.Key / Parameter.PriceTickSize), 
                p => (int)Math.Ceiling(1*(p.Value) / Parameter.CharacteristicOrderSize));

            var initalAsks = intialDepthProfile.Asks.ToDictionary(p => (int) (p.Key/Parameter.PriceTickSize),
                p => (int) Math.Ceiling(1*(p.Value) / Parameter.CharacteristicOrderSize));
            
            LimitOrderBook.InitializeDepthProfileBuySide(initalBids);
            LimitOrderBook.InitializeDepthProfileSellSide(initalAsks);
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
            var minTick = LimitOrderBook.Ask;
            var maxTick = minTick + Parameter.SimulationIntervalSize;
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
            var maxTick = LimitOrderBook.Bid;
            var minTick = maxTick - Parameter.SimulationIntervalSize;
            var priceTick = LimitOrderBook.GetRandomPriceFromBuySide(Random, minTick, maxTick);

            LimitOrderBook.CancelLimitBuyOrder(price:priceTick, amount: 1);
            
            return priceTick;
        }

        /// <summary>
        /// Limit buy order @ time = t
        /// </summary>
        public int SubmitLimitBuyOrder()
        {
            var priceTick = LimitOrderBook.Ask - 1 - Random.Next(Parameter.SimulationIntervalSize + 1);
            LimitOrderBook.SubmitLimitBuyOrder(priceTick, amount:1);
           
            return priceTick;
        }

        /// <summary>
        /// Limit sell order @ time = t
        /// </summary>
        public int SubmitLimitSellOrder()
        {
            var priceTick = LimitOrderBook.Bid + 1 + Random.Next(Parameter.SimulationIntervalSize + 1);
            LimitOrderBook.SubmitLimitSellOrder(priceTick, amount:1);
           
            return priceTick;
        }

        #endregion Events

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
            Random = new ExtendedRandom(Parameter.Seed);
            
            var t0 = LimitOrderBook.Time;
            var tEnd = t0 + duration;
            
            // It is impossible to simulate order arrivals and cancelations at integer price levels from −∞ to −∞
            // So consider only order arrivals and cancelations in a moving band of width centered around
            // the current best quotes.
            //  - L should be chosen conservatively so as to ensure minimal edge effects.
            //  - Within the band, the arrival rate of limit orders is α, cancelation rate is δ times outstanding shares.
            //  - Outside the band, orders may neither arrive nor be canceled.
            // TODO: Parameter.SimulationIntervalSize = ?
            
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
        
        /// <summary>
        /// Save current depth profile 
        /// </summary>
        /// <param name="fileName"></param>
        public void SaveDepthProfile(string fileName)
        {
            LimitOrderBook.SaveDepthProfile(fileName);
        }
        
        #endregion
    }
}