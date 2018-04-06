namespace LimitOrderBookSimulation.EventModels
{
    public class SmithFarmerModelParameter
    {
        /// <summary>
        /// Seed for random generator  
        /// </summary>
        public int Seed;
        
        /// <summary>
        /// Limit order rate
        /// Unit: shares / (ticks * time))
        /// </summary>
        public double LimitOrderRateDensity { set; get; }
        
        /// <summary>
        /// Market order rate
        /// Unit: shares / time
        /// </summary>
        public double MarketOrderRate { set; get; }

        /// <summary>
        /// Cancellation rate:
        /// Unit: 1 / time)
        /// </summary>
        public double CancellationRate { set; get; }
        
        /// <summary>
        /// Size of the simulation interval (in units of ticks)
        /// </summary>
        public int SimulationIntervalSize { set; get; }
        
        /// <summary>
        /// TODO: Should be part of the limit order book
        /// Denotes the size of a tick in units of price
        /// </summary>
        public double PriceTickSize { set; get; }
        
        /// <summary>
        /// TODO: Should be part of the limit order book
        /// Denotes what is the number of shared for on depth unit
        /// in limit order book
        /// </summary>
        public double CharacteristicOrderSize { set; get; }
    }
}