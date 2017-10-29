using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using log4net;
using log4net.Core;
using LimitOrderBookRepositories.Model;

namespace LimitOrderBookRepositories
{

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
        /// Trading data for stock
        /// </summary>
        public Dictionary<DateTime, LobTradingData> TradingData { private set; get; }
        
        /// <summary>
        /// Trading dates 
        /// </summary>
        public List<DateTime> TradingDates { private set; get; }
        
        #endregion

        #region Constructor 

        /// <summary>
        /// Constructor 
        /// </summary>
        public LobRepository(string symbol, List<DateTime> tradingDates)
        {
            Symbol = symbol;
            TradingDates = tradingDates;
        }

        #endregion

        #region Methods

        #region File loader

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
                events[i] = LobEvent.Parse(lines[i]);
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
                states[i] = LobState.Parse(lines[i], cleanData);
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
            var lobDataPath = ConfigurationManager.AppSettings["RespositoryFolder"];
            var repositoryFolder = !Path.IsPathRooted(lobDataPath) ? Path.Combine(workFolder, lobDataPath) : lobDataPath;

            TradingData = new Dictionary<DateTime, LobTradingData>();

            foreach (var tradingDate in TradingDates)
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

                TradingData.Add(tradingDate, new LobTradingData(events, states));

                Log.Info($"Loaded {events.Length} events");
                Log.Info($"Loaded {states.Length} states");
                
            }
            
        }

        #endregion


    }
}
