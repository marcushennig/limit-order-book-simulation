using System.Collections.Generic;

namespace LimitOrderBookSimulation.LimitOrderBooks
{
    public interface ILimitOrderBook
    {
        #region Properties

        double Time { set; get; }

        long Ask { get; }

        long Bid { get; }
        
        #endregion Properties

        #region Methods

        #region Events

        #region Limit order

        void SubmitLimitBuyOrder(long price, long amount = 1);

        void SubmitLimitSellOrder(long price, long amount = 1);

        #endregion Limit order

        #region Market order

        void SubmitMarketBuyOrder(long amount =1);

        void SubmitMarketSellOrder(long amount =1);

        #endregion Market order
        
        #region Cancel order

        void CancelLimitBuyOrder(long price, long amount = 1);

        void CancelLimitSellOrder(long price, long amount = 1);

        #endregion Cancel order

        #endregion Events
        
        #region Time evolution

        Dictionary<LimitOrderBookEvent, long> Counter { get; }

        /// <summary>
        /// Time-dependent pricing information 
        /// </summary>
        SortedList<double, Price> PriceTimeSeries { get; }

        #endregion Time evolution

        #region Statistics

        #region Number of orders

        long NumberOfBuyOrders(long minPrice = 0, long maxPrice = long.MaxValue);

        long NumberOfSellOrders(long minPrice = 0, long maxPrice = long.MaxValue);

        long NumberOfLimitOrders(long minPrice = 0, long maxPrice = long.MaxValue);

        #endregion Number of orders

        #region Inverse CDF

        long InverseCDF(long minPrice, long maxPrice, long q);

        long InverseCDFSellSide(long minPrice, long maxPrice, long q);

        long InverseCDFBuySide(long minPrice, long maxPrice, long q);

        #endregion Inverse CDF
        
        #endregion
        
        #region Iinitialize

        void InitializeDepthProfileBuySide(IDictionary<long, long> depthProdile);
        void InitializeDepthProfileSellSide(IDictionary<long, long> depthProdile);
        
        #endregion Iinitialize

        void SaveDepthProfile(string path, long maxReleativeTick = 0);

        #endregion Methods
    }
}
