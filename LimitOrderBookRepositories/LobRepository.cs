using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using log4net;
using LimitOrderBookRepositories.Model;

namespace LimitOrderBookRepositories
{
    /// <summary>
    /// Lob repository consisting of LOB data for 
    /// multiple trading days for a given asset
    /// </summary>
    public class LobRepository
    {
        #region Logging

        //Logging  
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #endregion Logging

        #region Properties

        /// <summary>
        /// Stock symbol
        /// </summary>
        public string Symbol { private set; get; }

        /// <summary>
        /// Level of the LOB data
        /// </summary>
        public int Level { private set; get; }

        /// <summary>
        /// Number of seconds which where skipped at the beginning at each trading day
        /// </summary>
        public double SkipFirstSeconds { private set; get; }
        
        /// <summary>
        /// Number of the seconds where were skipped at the end of each trading day 
        /// </summary>
        public double SkipLastSeconds { private set; get; }

        /// <summary>
        /// Trading data for stock
        /// </summary>
        public Dictionary<DateTime, LobTradingData> TradingData { private set; get; }
        
        /// <summary>
        /// Trading dates 
        /// </summary>
        public List<DateTime> TradingDays { private set; get; }
        
        #endregion

        #region Constructor 

        /// <summary>
        /// Constructor 
        /// </summary>
        public LobRepository(string symbol, int level, List<DateTime> tradingDays, 
            double skipFirstSeconds = 0,
            double skipLastSeconds = 0)
        {
            Symbol = symbol;
            Level = level;
            TradingDays = tradingDays;
            SkipFirstSeconds = skipFirstSeconds;
            SkipLastSeconds = skipLastSeconds;

            TradingData = new Dictionary<DateTime, LobTradingData>();

            LoadTradingData();
        }

        #endregion

        #region Methods

        #region File loader

        /// <summary>
        /// Parse line in LOBSTER data
        /// </summary>
        /// <param name="line"></param>
        /// <returns></returns>
        private static LobEvent ParseLobEvent(string line)
        {
            var data = line.Split(',').ToArray();

            var time = Convert.ToDouble(data[0]);
            var type = (LobEventType)Convert.ToInt32(data[1]);
            var orderId = Convert.ToInt32(data[2]);
            var volume = Convert.ToInt32(data[3]);
            var price = Convert.ToInt32(data[4]);
            var side = (LobMarketSide)Convert.ToInt32(data[5]);
            
            return new LobEvent(orderId, time, type, volume, price, side);
        }

        /// <summary>
        /// Parse line in LOBSTER data
        /// </summary>
        /// <param name="line"></param>
        /// <param name="skipDummyData"></param>
        /// <returns></returns>
        private static LobState ParseLobState(string line, bool skipDummyData = false)
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

            var data = line.Split(',').Select(p => Convert.ToInt32(p)).ToList();

            var askPrice = data.Where((value, index) => index % 4 == 0);
            var askVolume = data.Where((value, index) => (index - 1) % 4 == 0);
            var bidPrice = data.Where((value, index) => (index - 2) % 4 == 0);
            var bidVolume = data.Where((value, index) => (index - 3) % 4 == 0);

            if (!skipDummyData)
            {
                return new LobState(
                    askPrice.ToArray(), 
                    askVolume.ToArray(), 
                    bidPrice.ToArray(), 
                    bidVolume.ToArray());
            }
            // Skipy dummy data in LOBSTER file line
            var ask = askPrice.Zip(askVolume, (p, q) => new { Price = p, Volume = q })
                .Where(p => p.Price != +dummyValue)
                .ToList();

            var bid = bidPrice.Zip(bidVolume, (p, q) => new { Price = p, Volume = q })
                .Where(p => p.Price != -dummyValue)
                .ToList();

            return new LobState(
                ask.Select(p => p.Price).ToArray(), 
                ask.Select(p => p.Volume).ToArray(),
                bid.Select(p => p.Price).ToArray(), 
                bid.Select(p => p.Volume).ToArray());
        }

        /// <summary>
        /// Find LOB data file in folder and all its subfolders
        /// </summary>
        /// <param name="repositoryFolder"></param>
        /// <param name="tradingDate"></param>
        /// <param name="type"></param>
        /// <returns></returns>
        private string FindLobDataFile(string repositoryFolder, DateTime tradingDate, LobDataFileType type)
        {
            var fileTypeIdentifier = string.Empty;
            switch (type)
            {
                case LobDataFileType.EventFile:
                    fileTypeIdentifier = "message";
                    break;
                case LobDataFileType.StateFile:
                    fileTypeIdentifier = "orderbook";
                    break;
            }
            var searchPattern = $"{Symbol}_{tradingDate:yyyy-MM-dd}_*_{fileTypeIdentifier}_{Level}.csv";

            var files = Directory.GetFiles(repositoryFolder, searchPattern, SearchOption.AllDirectories);
            if (files.Length == 1)
            {
                return files.First();
            }
            throw new Exception($"Could not find file with the search pattern: '{searchPattern}' in folder: '{repositoryFolder}'");
        
        }

        /// <summary>
        /// Load event file from CSV-file
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static LobEvent[] LoadEventsFromFile(string path)
        {
            var lines = File.ReadAllLines(path);
            var events = new LobEvent[lines.Length];
            Parallel.For(0, lines.Length, i =>
            {
                try
                {
                    events[i] = ParseLobEvent(lines[i]);
                }
                catch (Exception e)
                {
                    Log.Error(e.Message);
                }
            });
            return events;
        }

        /// <summary>
        /// Load state file from CSV-file
        /// </summary>
        /// <param name="path"></param>
        /// <param name="cleanData"></param>
        /// <returns></returns>
        private static LobState[] LoadStatesFromFile(string path, bool cleanData = false)
        {
            var lines = File.ReadAllLines(path);
            var states = new LobState[lines.Length];
            Parallel.For(0, lines.Length, i =>
            {
                try
                {
                    states[i] = ParseLobState(lines[i], cleanData);
                }
                catch (Exception e)
                {
                    Log.Error(e.Message);
                }
            });
            return states;
        }
        
        #endregion

        /// <summary>
        /// Load trading data from 
        /// </summary>
        private void LoadTradingData()
        {
            // Get data from App.config
            var workFolder = ConfigurationManager.AppSettings["WorkFolder"];
            if (!Directory.Exists(workFolder))
            {
                Log.Error($"The folder '{workFolder}' does not exists, please correct in App.config");
                return;
            }
            var lobDataPath = ConfigurationManager.AppSettings["RespositoryFolder"];
            if (!Directory.Exists(lobDataPath))
            {
                Log.Error($"The folder '{lobDataPath}' does not exists, please correct in App.config");
                return;
            }
            var repositoryFolder = !Path.IsPathRooted(lobDataPath) ? Path.Combine(workFolder, lobDataPath) : lobDataPath;

            TradingData.Clear();

            foreach (var tradingDate in TradingDays)
            {
                string eventFile;
                string stateFile;
                try
                {
                    eventFile = FindLobDataFile(repositoryFolder, tradingDate, LobDataFileType.EventFile);
                    stateFile = FindLobDataFile(repositoryFolder, tradingDate, LobDataFileType.StateFile);
                }
                catch (Exception e)
                {
                    Log.Error($"Could not find event or state file for trading date {tradingDate:yyyy-MM-dd}");
                    Log.Error(e.Message);
                    continue;
                }

                var events = LoadEventsFromFile(eventFile);
                var states = LoadStatesFromFile(stateFile, cleanData: true);

                TradingData.Add(tradingDate, new LobTradingData(Level, events, states, SkipFirstSeconds, SkipLastSeconds));

                Log.Info($"Loaded {events.Length} events and {states.Length} states for {tradingDate:yyyy-MM-dd}");
            }
            
        }

        #endregion
    }
}
