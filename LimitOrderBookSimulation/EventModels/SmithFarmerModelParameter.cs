namespace LimitOrderBookSimulation.EventModels
{
    public class SmithFarmerModelParameter
    {
        /// <summary>
        /// Seed for random generator  
        /// </summary>
        public int Seed;

        #region Calibration parameter

        /// <summary>
        /// Percentage of events with
        /// price distance to the oposite quote smallter than 'LowerQuantile' 
        /// </summary>
        public double LowerQuantileProbability { set; get; }
        
        /// <summary>
        /// Percentage of events with
        /// price distance to the oposite quote smallter than 'UpperQuantile' 
        /// </summary>
        public double UpperQuantileProbability { set; get; }
        
        /// <summary>
        /// Lower quantile of the price bane
        /// </summary>
        public double LowerQuantile { set; get; }

        /// <summary>
        /// Upper quantile of the price bane
        /// </summary>
        public double UpperQuantile { set; get; }

        /// <summary>
        /// Model is calibrated from Trading data within tim interval [MinTradingTime, MaxTradingTime]
        /// </summary>
        public double MinTradingTime { set; get; }

        /// <summary>
        /// Model is calibrated from Trading data within tim interval [MinTradingTime, MaxTradingTime]
        /// </summary>
        public double MaxTradingTime { set; get; }
        
        #endregion
        
        #region Model parameter
        
        /// <summary>
        /// Limit order rate
        /// Unit: shares / (ticks * time))
        /// </summary>
        public double LimitOrderRateDensity { set; get; }
        
        /// <summary>
        /// MarketOrderRate [mu] characterizes the average market order arrival rate and it is just
        /// the number of shares of effective market order ('buy' and 'sell') to the number of events
        /// during the trading day
        /// Unit: shares / time
        /// </summary>
        public double MarketOrderRate { set; get; }

        /// <summary>
        /// Cancellations occure at each price level with a rate propotional to the depth at this price 
        /// Unit: 1 / time
        /// </summary>
        public double CancellationRate { set; get; }
        
        /// <summary>
        /// Size of the simulation interval L (in units of ticks)
        /// It is impossible to simulate order arrivals and cancelations at integer price levels from −∞ to −∞
        // So consider only order arrivals and cancelations in a moving band of width centered around
        // the current best quotes.
        //  - The size L should be chosen conservatively so as to ensure minimal edge effects.        
        //  - L should be chosen conservatively so as to ensure minimal edge effects.
        //  - Within the band, the arrival rate of limit orders is α, cancelation rate is δ times outstanding shares.
        //  - Outside the band, orders may neither arrive nor be canceled.
        /// Unit: PriceTicks
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
        
        #endregion Model parameter
        
        #region Characteric scales

        public double CharacteristicNumberOfShares => MarketOrderRate / (2 * CancellationRate);
        public double CharacteristicPriceInterval => MarketOrderRate / (2 * LimitOrderRateDensity);
        public double CharacteristicTime => 1 / CancellationRate;
        public double NondimensionalTickSize => 2 * LimitOrderRateDensity * PriceTickSize / MarketOrderRate;
        public double AsymptoticDepth => LimitOrderRateDensity / CancellationRate;
        public double BidAskSpread => MarketOrderRate / (2 * LimitOrderRateDensity);
        public double Resolution => 2 * LimitOrderRateDensity * PriceTickSize / MarketOrderRate;
        
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
        public double NondimensionalOrderSize => 2 * CancellationRate * CharacteristicOrderSize / MarketOrderRate;

        #endregion
    }
}