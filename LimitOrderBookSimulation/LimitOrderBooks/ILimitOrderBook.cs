using System.Collections.Generic;

namespace LimitOrderBookSimulation.LimitOrderBooks
{
    public interface ILimitOrderBook
    {
        #region Properties

        double Time { set; get; }

        long Ask { get; }

        long Bid { get; }

        Dictionary<LimitOrderBookEvent, long> Counter { get; }

        /// <summary>
        /// Evolution of the limit order book
        /// </summary>
        SortedList<double, Price> PriceTimeSeries { get; }

        #endregion Properties

        #region Methods

        #region Limit order

        void SubmitLimitBuyOrder(long price, long amount =1);

        void SubmitLimitSellOrder(long price, long amount =1);

        #endregion Limit order

        #region Market order

        void SubmitMarketBuyOrder(long amount =1);

        void SubmitMarketSellOrder(long amount =1);

        #endregion Market order
        
        #region Cancel order

        void CancelLimitBuyOrder(long price, long amount =1);

        void CancelLimitSellOrder(long price, long amount =1);

        #endregion Cancel order

        #region Number of orders

        long NumberOfBuyOrders(long minPrice = 0, long maxPrice = long.MaxValue);

        long NumberOfSellOrders(long minPrice = 0, long maxPrice = long.MaxValue);

        long NumberOfLimitOrders(long priceMin, long priceMax);

        #endregion Number of orders

        #region Inverse CDF

        long InverseCDF(long priceMin, long priceMax, long q);

        long InverseCDFSellSide(long minPrice, long maxPrice, long q);

        long InverseCDFBuySide(long minPrice, long maxPrice, long q);

        #endregion Inverse CDF

        #region Iinitialize depth profile

        void InitializeDepthProfileBuySide(IDictionary<long, long> depthProdile);
        void InitializeDepthProfileSellSide(IDictionary<long, long> depthProdile);
        
        #endregion Iinitialize depth profile

        void SaveDepthProfile(string path, long maxReleativeTick = 0);

        #endregion Methods
    }
}
