using System;
using System.IO;
using System.Reflection;
using log4net;
using Newtonsoft.Json;

namespace LimitOrderBookUtilities
{
    public static class SharedUtilities
    {
        private static readonly ILog Log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);


        /// <summary>
        /// Save bid as well as ask side into a CSV file
        /// </summary>
        /// <param name="obj">Objects to be converted to JSON</param>
        /// <param name="fileName">Path of CSV file</param>
        public static void SaveAsJson(object obj, string fileName)
        {
            try
            {
                var jsonString = JsonConvert.SerializeObject(obj, Formatting.Indented);
                File.WriteAllText(fileName, jsonString);
            }
            catch (Exception exception)
            {
                Log.Error("Could not save object as json");
                Log.Error($"Exception: {exception}");
            }
        }
        
        /// <summary>
        /// Load object of Type from JSON file
        /// </summary>
        /// <param name="fileName">Path to json file</param>
        public static T LoadFromJson<T>(string fileName)
        {
            try
            {
                var jsonString = File.ReadAllText(fileName);
                return JsonConvert.DeserializeObject<T>(jsonString);
            }
            catch (Exception exception)
            {
                Log.Error("Could load model");
                Log.Error($"Exception: {exception}");

                throw;
            }
        }
    }
}