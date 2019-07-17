using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace TcpClient
{
    class Log
    {
        private string name;

        internal Log(string name)
        {
            this.name = string.IsNullOrWhiteSpace(name) ? "" : name;
        }

        public void LogInfo(string message)
        {
            Trace.TraceInformation(string.Format("<{0}> <{1}> <{2}>", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), name, message));
        }

        public void LogWarn(string message)
        {
            Trace.TraceWarning(string.Format("<{0}> <{1}> <{2}>", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), name, message));
        }

        public void LogError(string message)
        {
            Trace.TraceError(string.Format("<{0}> <{1}> <{2}>", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), name, message));
        }
    }

    class LogManager
    {
        private static readonly string LOG_DIR = "logs";

        private static IDictionary<string, Log> loggerTable = new Dictionary<string, Log>();

        private static bool initialized = false;

        public static Log GetLogger(string name)
        {
            if (!initialized)
            {
                Directory.CreateDirectory(LOG_DIR);

                string filename = string.Format("{0}\\{1}-{2}.log", LOG_DIR, ProductInfo.PRODUCT_NAME, DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                Trace.Listeners.Add(new TextWriterTraceListener(filename));
                Trace.AutoFlush = true;

                initialized = true;
            }

            Log logger = null;

            if (!loggerTable.TryGetValue(name, out logger))
            {
                logger = new Log(name);
                loggerTable.Add(name, logger);
            }

            return logger;
        }

        public static void RemoveLogger(string name)
        {
            loggerTable.Remove(name);
        }
    }
}
