using System.Collections.Generic;

namespace LimitOrderBookSimulation.LimitOrderBooks
{
    public interface ILimitOrderBook
    {
        #region Properties

        double Time { set; get; }

        int Ask { get; }

        int Bid { get; }
        
        #endregion Properties

        #region Methods

        #region Events

        #region Limit order

        void SubmitLimitBuyOrder(int price, int amount = 1);

        void SubmitLimitSellOrder(int price, int amount = 1);

        #endregion Limit order

        #region Market order

        void SubmitMarketBuyOrder(int amount =1);

        void SubmitMarketSellOrder(int amount =1);

        #endregion Market order
        
        #region Cancel order

        void CancelLimitBuyOrder(int price, int amount = 1);

        void CancelLimitSellOrder(int price, int amount = 1);

        #endregion Cancel order

        #endregion Events
        
        #region Time evolution

        Dictionary<LimitOrderBookEvent, int> Counter { get; }

        /// <summary>
        /// Time-dependent pricing information 
        /// </summary>
        SortedList<double, Price> PriceTimeSeries { get; }

        #endregion Time evolution

        #region Statistics

        #region Number of orders

        int NumberOfBuyOrders(int minPrice = 0, int maxPrice = int.MaxValue);

        int NumberOfSellOrders(int minPrice = 0, int maxPrice = int.MaxValue);

        int NumberOfLimitOrders(int minPrice = 0, int maxPrice = int.MaxValue);

        #endregion Number of orders

        #region Inverse CDF

        int InverseCDF(int minPrice, int maxPrice, int q);

        int InverseCDFSellSide(int minPrice, int maxPrice, int q);

        int InverseCDFBuySide(int minPrice, int maxPrice, int q);

        #endregion Inverse CDF
        
        #endregion
        
        #region Iinitialize

        void InitializeDepthProfileBuySide(IDictionary<int, int> depthProdile);
        void InitializeDepthProfileSellSide(IDictionary<int, int> depthProdile);
        
        #endregion Iinitialize

        void SaveDepthProfile(string path);

        #endregion Methods
    }
}
