using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using log4net.Config;
using LimitOrderBookRepositories;
using LimitOrderBookSimulation;
using LimitOrderBookUtilities;

[assembly: XmlConfigurator(Watch = true)]

namespace CommandLineTool
{
    static class Program
    {
        /// <summary>
        /// Test random events 
        /// </summary>
        static void TestRandomEvent()
        {
            var random = new RandomUtilities(34);
            var events = new List<Action>()
            {
                new Action(() => { }),
                new Action(() => { }),
                new Action(() => { }),
                new Action(() => { }),
            };
            var probability = new Dictionary<Action,double>
            {
                { events[0], 0.6},
                { events[1], 0.2},
                { events[2], 0.15},
                { events[3], 0.05},
            };
            var counter = new Dictionary<Action, double>
            {
                {events[0], 0},
                {events[1], 0},
                {events[2], 0},
                {events[3], 0},
            };
            var n = 100000;
            for (var i = 0; i < n; i++)
            {
                var e = random.PickEvent(probability);
                counter[e]++;
            }
            var total = counter.Values.Sum();
            for (var i = 0; i < events.Count; i++)
            {
                var prob = (double)counter[events[i]]/total;
                Console.WriteLine($"P(event_{i})={prob}");
            }

        }

        /// <summary>
        /// Test loading LOB data
        /// </summary>
        static void LoadLobData()
        {
            const int level = 10;
            const string symbol = "AMZN";
            var tradingDates = new List<DateTime>{

                new DateTime(2016, 1, 4),
                new DateTime(2016, 1, 5)

            };
            var lobRepository = new LobRepository(symbol, level, tradingDates);
            var lobModel = new SmithFarmerModel();

            lobModel.Calibrate(lobRepository);

            Console.WriteLine(lobRepository);

        }

        /// <summary>
        /// Load lob data into repository 
        /// and use it for calibration
        /// </summary>
        static void StartSimulation()
        {
            var workFolder = ConfigurationManager.AppSettings["WorkFolder"];
            var logFolder = Path.Combine(workFolder, ConfigurationManager.AppSettings["LogFolder"]);
           
            const int level = 10;
            const string symbol = "AMZN";
            var tradingDate = new DateTime(2016, 1, 5);
            var tradingDates = new List<DateTime>{
               new DateTime(2016, 1, 5)
            };

            var lobRepository = new LobRepository(symbol, level, tradingDates);

            var lobData = lobRepository.TradingData[tradingDate];

            lobData.CheckConsistency();

            var model = new SmithFarmerModel(lobData);
            
            // Save the calibration parameters
            model.Save(Path.Combine(workFolder, "calibration.json"));
            
            #region Statistics           
            
            // Save statistics about LOB data 
            lobData.LimitOrderDistribution.Save(Path.Combine(workFolder, "limit_order_distribution.csv"));
            lobData.LimitSellOrderDistribution.Save(Path.Combine(workFolder, "limit_sell_order_distribution.csv"));
            lobData.LimitBuyOrderDistribution.Save(Path.Combine(workFolder, "limit_buy_order_distribution.csv"));

            lobData.CanceledOrderDistribution.Save(Path.Combine(workFolder, "canceled_order_distribution.csv"));
            lobData.CanceledSellOrderDistribution.Save(Path.Combine(workFolder, "canceled_sell_order_distribution.csv"));
            lobData.CanceledBuyOrderDistribution.Save(Path.Combine(workFolder, "canceled_buy_order_distribution.csv"));

            lobData.AverageDepthProfile.Save(Path.Combine(workFolder, "outstanding_limit_order_distribution.csv"));
            lobData.SavePriceProcess(Path.Combine(workFolder, "lob_price.csv"));

            lobData.CanceledOrderDistribution
                   .Divide(lobData.AverageDepthProfile)
                   .Scale(1, 1/lobData.TradingDuration)
                   .Save(Path.Combine(workFolder, "cancellation_rate_distribution.csv"));

            #endregion

            model.SimulateOrderFlow(60 *10);
            model.Save(Path.Combine(workFolder, "simulation.json"));
            model.SavePriceProcess(Path.Combine(workFolder, "price.csv"));
        }

        /// <summary>
        /// Main
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {

            // ExportPriceEvolution();
            // LoadLobData();
            //StartSimulation();
            LoadLobData();
            Console.WriteLine("Press any key.");
            Console.ReadKey();
        }
    }
}
