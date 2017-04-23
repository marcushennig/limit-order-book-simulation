using System;
using System.Collections.Generic;
using System.Linq;
using LimitOrderBookUtilities;

namespace CommandLineTool
{
    /// <summary>
    /// Command line parameter
    /// </summary>
    public class CommandLineParameter : CliParameter
    {
        #region Properties

        /// <summary>
        /// parameter names as they are used in the command line
        /// </summary>
        public static readonly CommandLineParameter Application = new CommandLineParameter("Application");
        public static readonly CommandLineParameter Symbol = new CommandLineParameter("Symbol");
        public static readonly CommandLineParameter TradingDate = new CommandLineParameter("TradingDate");
        public static readonly CommandLineParameter OutputPath = new CommandLineParameter("OutputPath");

        /// <summary>
        /// List of all allowed parameters
        /// </summary>
        private static readonly List<CommandLineParameter> All = new List<CommandLineParameter>
        {
            Application,
            Symbol,
            TradingDate,
            OutputPath,
        };

        #endregion Properties

        #region Constructor

        /// <summary>
        /// Constructor 
        /// </summary>
        /// <param name="name"></param>
        private CommandLineParameter(string name)
        {
            Name = name;
        }

        #endregion Constructor

        #region Methods

        /// <summary>
        /// Parse name and return corresponding instance
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static CommandLineParameter Parse(string name)
        {
            return All.First(p => string.Equals(p.Name, name, StringComparison.CurrentCultureIgnoreCase));
        }

        #endregion Methods
    }
}
