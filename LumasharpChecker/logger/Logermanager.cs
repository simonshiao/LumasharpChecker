using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using NLog;
using System.IO;
using System.Reflection;


namespace Cyntec.Logger
{
    public static class Logmanager
    {
        private static NLog.Logger _mainloger;
        private static NLog.Logger _plcloger;
        public static void init()
        {
            //string assemblyFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            //LogManager.Configuration = new NLog.Config.XmlLoggingConfiguration(assemblyFolder + @"\NLog.config");
            LogManager.Configuration = new NLog.Config.XmlLoggingConfiguration(@"NLog.config");
        }
        public static NLog.Logger loger
        {
            get
            {
                if (_mainloger == null)
                {
                    _mainloger = NLog.LogManager.GetLogger("main");
                }
                return _mainloger;
            }
        }
        public static NLog.Logger plcloger
        {
            get
            {
                if (_plcloger == null)
                {
                    _plcloger = NLog.LogManager.GetLogger("plc");
                }
                return _plcloger;
            }
        }
        public static NLog.Logger monitorloger
        {
            get
            {
                if (_plcloger == null)
                {
                    _plcloger = NLog.LogManager.GetLogger("monitor");
                }
                return _plcloger;
            }
        }
    }
}

