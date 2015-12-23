using System;
using System.Windows;
using System.IO;
using log4net;
using log4net.Appender;

namespace ParsingApp
{
    public partial class MainWindow : Window, IAppender
    {
        #region Private fields

        private static readonly ILog Logger = LogManager.GetLogger(typeof(MainWindow));
        private readonly Parser _parser = new Parser();
        private readonly object _outputFileStreamWriterLock = new Object();

        private ResultProcessingOptions _resultProcessingOption = ResultProcessingOptions.SaveToFile;
        private StreamWriter _outputFileStreamWriter;

        #endregion

        #region Public constructor

        public MainWindow()
        {
            InitializeComponent();

            saveToFileRadioButton.IsChecked = true;
            _parser.ProductParsed += parser_ProductParsed;
            _parser.ParsingFinished += parser_ParsingFinished;
        }

        #endregion

        #region Public methods
        public void DoAppend(log4net.Core.LoggingEvent loggingEvent)
        {
            var appendAction = new Action(() => logTextBox.AppendText(String.Format(
                "{0}: {1}\t {2}{3}",
                loggingEvent.TimeStamp,
                loggingEvent.Level.Name,
                loggingEvent.MessageObject,
                Environment.NewLine)));

            try
            {
                if (outputTextBox.Dispatcher.CheckAccess())
                {
                    appendAction();
                }
                else
                {
                    outputTextBox.Dispatcher.Invoke(appendAction, null);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error occured", ex);
            }
        }
        #endregion

        #region Private callbacks

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).Root.AddAppender(this);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            lock (_outputFileStreamWriterLock)
            {
                if (_outputFileStreamWriter != null)
                {
                    Logger.Warn("Output stream writer wasn't closed, closing...");
                    _outputFileStreamWriter.Dispose();
                    _outputFileStreamWriter = null;
                }
            }

            ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).Root.RemoveAppender(this);
        }

        private void browseInputFileButton_Click(object sender, RoutedEventArgs e)
        {
            var openInputFileDialog = new Microsoft.Win32.OpenFileDialog();

            if(openInputFileDialog.ShowDialog() == true)
            {
                inputFileNameTextBox.Text = openInputFileDialog.FileName;
            }
        }

        private void browseOutputFileButton_Click(object sender, RoutedEventArgs e)
        {
            var openOutputFileDialog = new Microsoft.Win32.OpenFileDialog();

            if (openOutputFileDialog.ShowDialog() == true)
            {
                outputFileNameTextBox.Text = openOutputFileDialog.FileName;
            }
        }

        private void startParsingButton_Click(object sender, RoutedEventArgs e)
        {
            if (_resultProcessingOption == ResultProcessingOptions.SaveToFile)
            {
                try
                {
                    string outputFileName = outputFileNameTextBox.Text;
                    bool isHeaderNeeded = false;

                    if (String.IsNullOrWhiteSpace(outputFileName))
                    {
                        Logger.Error(String.Format("Bad output file name: {0}", outputFileName));
                        return;
                    }

                    if (!File.Exists(outputFileName))
                    {
                        isHeaderNeeded = true;
                    }

                    lock (_outputFileStreamWriterLock)
                    {
                        _outputFileStreamWriter = new StreamWriter(outputFileName, true);

                        if (isHeaderNeeded)
                        {
                            _outputFileStreamWriter.WriteLine(@"Id,Name,Price");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(String.Format("Unhandled {0} exception while creating StreamWriter", ex.GetType()), ex);
                    _outputFileStreamWriter = null;
                    return;
                }
            }

            outputTextBox.Clear();
            logTextBox.Clear();
            SetControlsState(false);

            _parser.StartParsing(inputFileNameTextBox.Text);
        }

        private void RadioButton_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (saveToFileRadioButton.IsChecked == true)
            {
                _resultProcessingOption = ResultProcessingOptions.SaveToFile;
            }

            if (saveToDatabaseRadioButton.IsChecked == true)
            {
                _resultProcessingOption = ResultProcessingOptions.SaveToDatabase;
            }

            if (saveToDisplayRadioButton.IsChecked == true)
            {
                _resultProcessingOption = ResultProcessingOptions.DisplayOnScreen;
            }
        }

        private void parser_ProductParsed(object sender, ProductParsedEventArgs e)
        {
            switch (_resultProcessingOption)
            {
                case ResultProcessingOptions.SaveToFile:
                    SaveProductToFile(e.Product);
                    break;
                case ResultProcessingOptions.SaveToDatabase:
                    SaveProductToDatabase(e.Product);
                    break;
                case ResultProcessingOptions.DisplayOnScreen:
                    DisplayProduct(e.Product);
                    break;
                default:
                    App.Log.Error(String.Format("Impossible resultProcessingOption: {0}", _resultProcessingOption));
                    break;
            }
        }

        private void parser_ParsingFinished(object sender, EventArgs e)
        {
            lock (_outputFileStreamWriterLock)
            {
                if (_outputFileStreamWriter != null)
                {
                    _outputFileStreamWriter.Dispose();
                    _outputFileStreamWriter = null;
                }
            }

            SetControlsState(true);
        }

        #endregion

        #region Private methods

        private void SetControlsState(bool isEnabled)
        {
            Action setStateAction = () =>
            {
                radioButtonsGroupBox.IsEnabled = isEnabled;
                startParsingButton.IsEnabled = isEnabled;
            };

            if (Dispatcher.CheckAccess())
            {
                setStateAction();
            }
            else
            {
                Dispatcher.Invoke(setStateAction);
            }
        }

        private void SaveProductToFile(Product product)
        {
            lock (_outputFileStreamWriterLock)
            {
                if (_outputFileStreamWriter != null)
                {
                    _outputFileStreamWriter.WriteLine(product);
                }
            }
        }

        private void SaveProductToDatabase(Product product)
        {
            Logger.Debug(product);
        }

        private void DisplayProduct(Product product)
        {
            var displayAction = new Action<Product>(
                p => outputTextBox.AppendText(p.ToString() + Environment.NewLine));

            if (outputTextBox.Dispatcher.CheckAccess())
            {
                displayAction(product);
            }
            else
            {
                outputTextBox.Dispatcher.Invoke(displayAction, new object[] { product });
            }
        }

        #endregion
    }

    enum ResultProcessingOptions
    {
        SaveToFile,
        SaveToDatabase,
        DisplayOnScreen
    }
}
