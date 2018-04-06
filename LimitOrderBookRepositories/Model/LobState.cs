using System;
using System.Collections.Generic;
using System.Linq;

namespace LimitOrderBookRepositories.Model
{
    /// <summary>
    /// - Levels:
    /// The term level refers to occupied price levels. This implies 
    ///	that the difference between two levels in the LOBSTER output 
    ///	is not necessarily the minimum ticks size.
    ///	- Unoccupied Price Levels
    /// </summary>
    public class LobState
    {
        #region Properties

        /// <summary>
        /// Level
        /// </summary>
        public int Level { get; }

        /// <summary>
        /// Level 1-n ask prices 
        /// </summary>
        public int[] AskPrice { get; }
        
        /// <summary>
        /// Level 1-n ask volumes 
        /// </summary>
        public int[] AskVolume { get; }

        /// <summary>
        /// Level 1-n of bid prices 
        /// </summary>
        public int[] BidPrice { get; }

        /// <summary>
        /// Level 1-n bid volumes 
        /// </summary>
        public int[] BidVolume { get; }
        
        #endregion Properties 

        #region Characteristics

        /// <summary>
        /// Return ask side as dictionary
        /// </summary>
        public IDictionary<int, int> Asks => AskPrice
            .Zip(AskVolume, (price, volume) => new KeyValuePair<int, int>(price, volume))
            .ToDictionary(p => p.Key, p => p.Value);

        /// <summary>
        /// Return ask side as dictionary
        /// </summary>
        public IDictionary<int, int> Bids => BidPrice
            .Zip(BidVolume, (price, volume) => new KeyValuePair<int, int>(price, volume))
            .ToDictionary(p => p.Key, p => p.Value);

        /// <summary>
        /// Spread 
        /// </summary>
        public int Spread => BestAskPrice - BestBidPrice;

        /// <summary>
        /// Best ask volume
        /// </summary>
        public int BestAskVolume => AskVolume[0];

        /// <summary>
        /// Best ask price
        /// </summary>
        public int BestAskPrice => AskPrice[0];

        /// <summary>
        /// Best bid volume
        /// </summary>
        public int BestBidVolume => BidVolume[0];

        /// <summary>
        /// Best bid price
        /// </summary>
        public int BestBidPrice => BidPrice[0];

        #endregion

        #region Constructor 

        /// <summary>
        /// Constructor 
        /// </summary>
        /// <param name="askPrice"></param>
        /// <param name="askVolume"></param>
        /// <param name="bidPrice"></param>
        /// <param name="bidVolume"></param>
        public LobState(int[] askPrice, int[] askVolume, int[] bidPrice, int[] bidVolume)
        {
            if (askPrice == null || askVolume == null || bidPrice == null || bidVolume == null)
            {
                throw new ArgumentException("Prices or volumes are not allowed to be NULL");
            }

            if (bidPrice.Length != bidVolume.Length)
            {
                throw new ArgumentException($"The number of prices and volumes on bide side are different (Number of prices={askPrice.Length}, number of volumes={askVolume.Length})");
            }

            if (askPrice.Length != askVolume.Length)
            {
                throw new ArgumentException($"The number of prices and volumes on ask side are different (Number of prices={askPrice.Length}, number of volumes={askVolume.Length})");
            }

            Level = askPrice.Length;

            AskPrice = askPrice;
            AskVolume = askVolume;

            BidPrice = bidPrice;
            BidVolume = bidVolume;
        }

        #endregion
        
        #region Methods

        /// <summary>
        /// Check is a price is in quantile on given side
        /// </summary>
        /// <param name="price"></param>
        /// <param name="side"></param>
        /// <param name="quantile"></param>
        /// <returns></returns>
        public bool IsPriceInQuantile(int price, LobMarketSide side, double quantile)
        {
            if (quantile < 0 || quantile > 1)
            {
                throw new ArgumentException("Quantile can only be in the intervale [0,1]");
            }

            if (price > BestBidPrice && price < BestAskPrice)
            {
                return true;
            }

            if (side == LobMarketSide.Sell)
            {
                
                double totalVolume = AskVolume.Sum();
                var k = Array.FindIndex(AskPrice, p => p > price);
                if (k != -1)
                {
                    double volume = AskVolume.Take(k + 1).Sum();
                    return volume / totalVolume <= quantile;
                }
                return false;
            }
            else
            {
                double totalVolume = BidVolume.Sum();
                var k = Array.FindIndex(BidPrice, p => p < price);
                if (k != -1)
                {
                    double volume = BidVolume.Take(k + 1).Sum();
                    return volume / totalVolume <= quantile;
                }
                return false;
            }
        }

        /// <summary>
        /// Determine the depth at given price on buy or sell side
        /// </summary>
        /// <param name="price"></param>
        /// <param name="side"></param>
        /// <returns></returns>
        public int Depth(int price, LobMarketSide side)
        {
            if (side == LobMarketSide.Sell)
            {
                var k = Array.IndexOf(AskPrice, price);
                if (k >= 0)
                {
                    return AskVolume[k];
                }
                return 0;

            }
            if (side == LobMarketSide.Buy)
            {
                var k = Array.IndexOf(BidPrice, price);
                if (k >= 0)
                {
                    return BidVolume[k];
                }
                return 0;
            }
            return 0;
        }

        /// <summary>
        /// String representation
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $" BestBidPrice={BestBidPrice} ({BestBidVolume}), BestAskPrice={BestAskPrice} ({BestAskVolume})";
        }

        #endregion
    }
}
