using System;
using System.Collections.Generic;
using System.IO;
using LimitOrderBookRepositories;
using LimitOrderBookUtilities;

namespace CommandLineTool
{

    /// <summary>
    /// Command line for BSP
    /// </summary>
    public class CommandLine : CommandLineInterface
    {
        #region Properties

        /// <summary>
        /// Error code for unexpected error
        /// </summary>
        private const int ReturnCodeUnexpectedError = 200;

        #endregion Properties

        #region Methods

        /// <summary>
        /// Convert string to CLI Command 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        protected override CliParameter Find(string key)
        {
            return CommandLineParameter.Parse(key);
        }

        /// <summary>
        ///  Start commmand line 
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        private int Start(IDictionary<CliParameter, string> parameters)
        {
            var application = GetValue<Application>(parameters, CommandLineParameter.Application, optional:false);

            // Application export mid price 
            if (application == Application.ExportPriceEvolution)
            {
                var symbol = GetValue<string>(parameters, CommandLineParameter.Symbol, optional: false);
                var tradingDate = GetValue<DateTime>(parameters, CommandLineParameter.TradingDate, optional: false, format: "yyyy/MM/dd");
                var outputPath = GetValue<string>(parameters, CommandLineParameter.OutputPath);

                const int level = 10;

                try
                {
                    var repository = new LOBDataRepository(symbol, level, tradingDate, outputPath);

                    Console.WriteLine($"Save price process of '{symbol}' for {tradingDate:yyyy-MM-dd}");
                    repository.SavePriceProcess(Path.Combine(outputPath, $"{symbol}_{tradingDate:yyyy-MM-dd}.csv"));
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"Could not save price process of '{symbol}' for {tradingDate:yyyy-MM-dd}");
                    Console.WriteLine($"Exception: {exception.Message}");
                }
            }
            
            return 0;
        }

        /// <summary>
        /// Start BSP from command line 
        /// </summary>
        /// <returns></returns>
        public int Start(IEnumerable<string> args)
        {
            return Start(ParseCommandLineParameters(args));
        }

        #endregion Methods
    }
}
