using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using log4net;

namespace LimitOrderBookUtilities
{
    /// <summary>
    /// Class for handling  command line parameters 
    /// </summary>
    public abstract class CliParameter
    {
        #region Properties

        /// <summary>
        /// Name of the command line parameter
        /// </summary>
        protected string Name { set; get; }

        #endregion Properties 

        #region Methods

        /// <summary>
        /// String representation
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return Name;
        }

        #endregion Methods
    }

    // Ein Kommandozeilenargument fehlt.
    public class MissingCommandLineArgumentException : Exception
    {
        #region Properties

        /// <summary>
        /// Missing argument 
        /// </summary>
        private string MissingArgument { set; get; }

        /// <summary>
        /// Message 
        /// </summary>
        public override string Message => $"Missing command line parameter: \"{MissingArgument}\". ";

        #endregion Properties

        #region Constructor 

        /// <summary>
        /// Constructor 
        /// </summary>
        /// <param name="missingArgument"></param>
        public MissingCommandLineArgumentException(string missingArgument)
        {
            MissingArgument = missingArgument;
        }

        #endregion Constructor
    }

    /// <summary>
    /// Abstract command line interface 
    /// </summary>
    public abstract class CommandLineInterface
    {
        #region Logging

        /// <summary>
        /// Logger
        /// </summary>
        protected static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #endregion Logging

        #region Methods

        /// <summary>
        /// Extract value of command line arguement  and convert it to required type
        /// </summary>
        /// <param name="values"></param>
        /// <param name="parameter"></param>
        /// <param name="optional"></param>
        /// <param name="format"></param>
        /// <returns></returns>
        protected T GetValue<T>(IDictionary<CliParameter, string> values, CliParameter parameter, bool optional = false, string format = null)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));

            string valueString;
            if (!values.TryGetValue(parameter, out valueString))
            {
                if (!optional)
                {
                    throw new MissingCommandLineArgumentException(parameter.ToString());
                }
                return default(T);
            }
            // Check if we have a datetime 
            if (typeof(T) == typeof(DateTime) && !string.IsNullOrEmpty(format))
            {
                try
                {
                    return (T)((object)DateTime.ParseExact(valueString, format, CultureInfo.InvariantCulture));
                }
                catch (Exception)
                {
                    throw new FormatException($"Format für '{valueString}' entspricht nicht dem erwarteten '{format}'");
                }
            }
            var converter = TypeDescriptor.GetConverter(typeof(T));
            return (T)converter.ConvertFromString(valueString);
        }

        /// <summary>
        /// Convert string to CLI Parameter 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        protected abstract CliParameter Find(string key);

        /// <summary>
        ///  Parse command line parameters
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        protected Dictionary<CliParameter, string> ParseCommandLineParameters(IEnumerable<string> args)
        {
            var parameters = new Dictionary<CliParameter, string>();
            var parameterMatcher = new Regex("^(?:-{1,2}|/)([a-zA-Z]\\w{0,})[:=]{0,1}(.*)$", RegexOptions.Compiled);

            foreach (var str in args)
            {
                var matches = parameterMatcher.Matches(str);

                foreach (Match match in matches)
                {
                    var key = match.Groups[1].Value;
                    var value = match.Groups[2].Value;

                    try
                    {
                        parameters.Add(Find(key), value);
                    }
                    catch (Exception)
                    {
                        throw new ArgumentException($"CommandLine term {str} is invalid");
                    }
                }
            }
            return parameters;
        }

        #endregion Methods
    }
}
