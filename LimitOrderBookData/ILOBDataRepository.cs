using System.Collections.Generic;
using LimitOrderBookUtilities;

namespace LimitOrderBookRepositories
{
    public interface ILOBDataRepository
    {
        #region Properties

        LOBEvent[] Events { get; }
        LOBState[] States { get; }
        
        List<LOBEvent> LimitOrders { get; }
        List<LOBEvent> MarketOrders { get; }
        List<LOBEvent> SubmittedOrders { get; }
        List<LOBEvent> CanceledOrders { get; }

        string Symbol { get; }
        int Level { get; }
        double StartTradingTime { get; }
        double EndTradingTime { get; }
        double TradingDuration { get; }
        double AverageBuySideIntervalSize { get; }
        double AverageAskSideIntervalSize { get; }
        long PriceTickSize { get; }

        DiscreteDistribution LimitOrderDistribution { get; }
        DiscreteDistribution LimitSellOrderDistribution { get; }
        DiscreteDistribution LimitBuyOrderDistribution { get; }

        DiscreteDistribution CanceledOrderDistribution { get; }
        DiscreteDistribution CanceledSellOrderDistribution { get; }
        DiscreteDistribution CanceledBuyOrderDistribution { get; }

        DiscreteDistribution AverageDepthProfile { get; }

        #endregion Properties

        #region Methods

        Dictionary<long, double> AverageNumberOfOutstandingLimitOrders(MarketSide side);
        Dictionary<long, double> AverageNumberOfOutstandingLimitOrders();

        /// <summary>
        /// Check consistency of the data
        /// </summary>
        void CheckConsistency();

        /// <summary>
        /// Save price evolution to file
        /// </summary>
        void SavePriceProcess(string path);

        #endregion Methods

    }
}
