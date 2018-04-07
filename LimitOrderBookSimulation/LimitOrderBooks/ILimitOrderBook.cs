using System.Collections.Generic;
using LimitOrderBookRepositories.Model;
using LimitOrderBookUtilities;
using Newtonsoft.Json;

namespace LimitOrderBookSimulation.LimitOrderBooks
{
    public interface ILimitOrderBook
    {
        #region Recording
        
        bool RecordTradingData { set; get; }
        
        [JsonIgnore]
        LobTradingData TradingData { get; }

        #endregion 

        double Time { set; get; }

        int Ask { get; }

        int Bid { get; }

        int GetDepthAtPriceTick(int priceTick);

        int GetRandomPriceFromSellSide(ExtendedRandom random, int pmin, int pmax);

        int GetRandomPriceFromBuySide(ExtendedRandom random, int pmin, int pmax);

        #region Events

        void SubmitLimitBuyOrder(int price, int amount = 1);

        void SubmitLimitSellOrder(int price, int amount = 1);

        int SubmitMarketBuyOrder(int amount =1);

        int SubmitMarketSellOrder(int amount =1);

        void CancelLimitBuyOrder(int price, int amount = 1);

        void CancelLimitSellOrder(int price, int amount = 1);

        #endregion Events
        
        #region Time evolution

        Dictionary<LimitOrderBookEvent, int> Counter { get; }

        /// <summary>
        /// Time-dependent pricing information 
        /// </summary>
        [JsonIgnore]
        SortedList<double, Price> PriceTimeSeries { get; }

        #endregion Time evolution

        #region Statistics

        bool IsBuySideEmpty();
        
        bool IsSellSideEmpty();

        int NumberOfBuyOrders(int minPrice = 0, int maxPrice = int.MaxValue);

        int NumberOfSellOrders(int minPrice = 0, int maxPrice = int.MaxValue);

        int NumberOfLimitOrders(int minPrice = 0, int maxPrice = int.MaxValue);

        #endregion
        
        #region Iinitialize

        void InitializeDepthProfileBuySide(IDictionary<int, int> depthProdile);
        void InitializeDepthProfileSellSide(IDictionary<int, int> depthProdile);
        
        #endregion Iinitialize

        void SaveDepthProfile(string path);
        void SaveDepthProfileBuySide(string path);
        void SaveDepthProfileSellSide(string path);
    }
}
