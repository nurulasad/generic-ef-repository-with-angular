using System;
using System.Configuration;

namespace MyFramework
{
    public class Config
    {
   
        public static readonly string ConnectionKeyName = "connectionKey";

        
        public static readonly string CoreContainer = getStringFromConfig("CoreContainer", "CoreContainer");



        public static readonly string DATETIME_FORMAT = getStringFromConfig("DateTimeFormat", "dd-MMM-yyyy HH:MM:ss tt");

        public static readonly string BLL_ASSEMBLY_NAME = getStringFromConfig("BLL_ASSEMBLY_NAME", "BusinessLayer.Core");
        public static readonly string DAL_ASSEMBLY_NAME = getStringFromConfig("DAL_ASSEMBLY_NAME", "DatabaseLayer.Core");
        

        private static string getStringFromConfig(string key, string defaultValue)
        {
            string configValue = ConfigurationManager.AppSettings[key];

            if (string.IsNullOrEmpty(configValue))
            {
                return defaultValue;
            }

            return configValue;
        }

        //private static int getIntFromConfig(string key, int defaultValue)
        //{
        //    string configValue = ConfigurationManager.AppSettings[key];
        //    if (string.IsNullOrEmpty(configValue))
        //    {
        //        return defaultValue;
        //    }

        //    return Convert.ToInt32(configValue);
        //}

        //private static bool getBoolFromConfig(string key, bool defaultValue)
        //{
        //    string configValue = ConfigurationManager.AppSettings[key];
        //    if (string.IsNullOrEmpty(configValue))
        //    {
        //        return defaultValue;
        //    }

        //    return Convert.ToBoolean(configValue);
        //}
    }
}
