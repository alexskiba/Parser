using System.Windows;
using log4net;

namespace ParsingApp
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(MainWindow));

        public App()
        {
            log4net.Config.BasicConfigurator.Configure();
        }

        public static ILog Log
        {
            get { return Logger; }
        }
    }
}
