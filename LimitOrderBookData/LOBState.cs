using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;

namespace LimitOrderBookRepositories
{
    /// <summary>
    /// - Levels:
    /// The term level refers to occupied price levels. This implies 
    ///	that the difference between two levels in the LOBSTER output 
    ///	is not necessarily the minimum ticks size.
    ///	- Unoccupied Price Levels
    /// </summary>
    public class LOBState
    {
        #region Properties
        
        public long[] AskPrice { set; get; }

        public long[] AskVolume { set; get; }

        public long[] BidPrice { set; get; }

        /// <summary>
        /// Level 1-n Bid Volume 
        /// </summary>
        public long[] BidVolume { set; get; }

        /// <summary>
        /// Return ask side as dictionary
        /// </summary>
        public IDictionary<long, long> Asks => AskPrice.Zip(AskVolume, (price, volume) => new KeyValuePair<long, long>(price, volume))
                                                       .ToDictionary(p => p.Key, p => p.Value);

        /// <summary>
        /// Return ask side as dictionary
        /// </summary>
        public IDictionary<long, long> Bids => BidPrice.Zip(BidVolume, (price, volume) => new KeyValuePair<long, long>(price, volume))
                                                       .ToDictionary(p => p.Key, p => p.Value);
        /// <summary>
        /// Spread 
        /// </summary>
        public long Spread => BestAskPrice - BestBidPrice;

        /// <summary>
        /// Best ask volume
        /// </summary>
        public long BestAskVolume => AskVolume[0];

        /// <summary>
        /// Best ask price
        /// </summary>
        public long BestAskPrice => AskPrice[0];

        /// <summary>
        /// Best bid volume
        /// </summary>
        public long BestBidVolume => BidVolume[0];

        /// <summary>
        /// Best bid price
        /// </summary>
        public long BestBidPrice => BidPrice[0];

        #endregion Properties 

        #region Methods

        /// <summary>
        /// Determine the depth at given price on buy or sell side
        /// </summary>
        /// <param name="price"></param>
        /// <param name="side"></param>
        /// <returns></returns>
        public long Depth(long price, MarketSide side)
        {
            if (side == MarketSide.Sell)
            {
                var k = Array.IndexOf(AskPrice, price);
                if (k >= 0)
                {
                    return AskVolume[k];
                }
                return 0;

            }
            else if (side == MarketSide.Buy)
            {
                var k = Array.IndexOf(BidPrice, price);
                if (k >= 0)
                {
                    return BidVolume[k];
                }
                return 0;
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// String representation
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $" BestBidPrice={BestBidPrice} ({BestBidVolume}), BestAskPrice={BestAskPrice} ({BestAskVolume})";
        }

        /// <summary>
        /// Parse line in LOBSTER data
        /// </summary>
        /// <param name="line"></param>
        /// <param name="skipDummyData"></param>
        /// <returns></returns>
        public static LOBState Parse(string line, bool skipDummyData = false)
        {
            // Columns:
            // 1.) Ask Price 1: 	Level 1 Ask Price 	(Best Ask)
            // 2.) Ask Size 1: 	Level 1 Ask Volume 	(Best Ask Volume)
            // 3.) Bid Price 1: 	Level 1 Bid Price 	(Best Bid)
            // 4.) Bid Size 1: 	Level 1 Bid Volume 	(Best Bid Volume)
            // 5.) Ask Price 2: 	Level 2 Ask Price 	(2nd Best Ask)
            // ...
            // Dollar price times 10000 (i.e., A stock price of $91.14 is given by 911400)
            //	When the selected number of levels exceeds the number of levels 
            //	available the empty order book positions are filled with dummy 
            //	information to guarantee a symmetric output. The extra bid 
            //	and/or ask prices are set to -9999999999 and 9999999999, 
            //	respectively. The Corresponding volumes are set to 0. 
            const long dummyValue = 9999999999;

            var data = line.Split(',').Select(p => Convert.ToInt64(p)).ToList();
            var askPrice = data.Where((value, index) => index % 4 == 0);
            var askVolume = data.Where((value, index) => (index - 1) % 4 == 0);
            var bidPrice = data.Where((value, index) => (index - 2) % 4 == 0);
            var bidVolume = data.Where((value, index) => (index - 3) % 4 == 0);

            if (!skipDummyData)
            {
                return new LOBState
                {
                    AskPrice = askPrice.ToArray(),
                    AskVolume = askVolume.ToArray(),
                    BidPrice = bidPrice.ToArray(),
                    BidVolume = bidVolume.ToArray()
                };
            }
            // Skipy dummy data in LOBSTER file line
            var ask = askPrice.Zip(askVolume, (p, q) => new { Price = p, Volume = q })
                .Where(p => p.Price != +dummyValue)
                .ToList();

            var bid = bidPrice.Zip(bidVolume, (p, q) => new { Price = p, Volume = q })
                .Where(p => p.Price != -dummyValue)
                .ToList();
            
            return new LOBState
            {
                AskPrice = ask.Select(p => p.Price).ToArray(),
                AskVolume = ask.Select(p => p.Volume).ToArray(),
                BidPrice = bid.Select(p => p.Price).ToArray(),
                BidVolume = bid.Select(p => p.Volume).ToArray()
            };
        }

        #endregion Methods
    }
}
