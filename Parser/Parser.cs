using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using System.Net;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using log4net;

namespace ParsingApp
{
    class Parser
    {
        #region Private fields

        private static readonly ILog Log = LogManager.GetLogger(typeof(Parser));
        private const int MAX_TASKS_COUNT = 10;
        private readonly TimeSpan _linksBunchParsingTimeout = TimeSpan.FromSeconds(20.0);

        private int _successfulTaskCount = 0;
        private bool _isParsingStarted = false;

        #endregion

        #region Public constructor

        public Parser()
        {
            ProductParsed += (sender, e) => { };
            ParsingFinished += (sender, e) => { };
        }

        #endregion

        #region Public events

        public event EventHandler<ProductParsedEventArgs> ProductParsed;
        public event EventHandler ParsingFinished;

        #endregion

        #region Public properties

        public string InputFileName { get; set; }

        #endregion

        #region Public methods

        public void StartParsing(string inputFileName)
        {
            InputFileName = inputFileName;
            StartParsing();
        }

        public void StartParsing()
        {
            if (_isParsingStarted)
            {
                Log.Warn("Attempt to start new parsing session while processing previous one.");
                return;
            }

            _isParsingStarted = true;
            _successfulTaskCount = 0;
            List<string> links = GetAllLinks();

            if (links == null)
            {
                OnParsingFinished();
            }
            else
            {
                Log.InfoFormat("{0} tasks will be launched", links.Count);
                Task.Factory.StartNew(() => ProcessAllLinks(links));
            }
        }

        #endregion

        #region Private methods

        private void OnProductParsed(Product product)
        {
            if (!EqualityComparer<Product>.Default.Equals(product, (default(Product))))
            {
                _successfulTaskCount++;
                ProductParsed(this, new ProductParsedEventArgs(product));
            }
        }

        private void OnParsingFinished()
        {
            Log.InfoFormat(
                "{0} {1} finished successfully",
                _successfulTaskCount,
                _successfulTaskCount == 1 ? "task" : "tasks");
            _isParsingStarted = false;
            ParsingFinished(this, EventArgs.Empty);
        }

        private List<string> GetAllLinks()
        {
            List<string> links = null;

            if (File.Exists(InputFileName))
            {
                try
                {
                    links = File.ReadAllLines(InputFileName).ToList();
                }
                catch (Exception ex)
                {
                    Log.Error("Failed to read input file", ex);
                }
            }
            else
            {
                Log.Error(String.Format("Input file \"{0}\" does not exist.", InputFileName));
            }

            return links;
        }

        private void ProcessAllLinks(List<string> links)
        {
            try
            {
                var tasks = new List<Task>();

                while (links.Count > 0)
                {
                    var tmpLinksList = links.GetRange(0, Math.Min(MAX_TASKS_COUNT, links.Count));
                    links.RemoveRange(0, Math.Min(MAX_TASKS_COUNT, links.Count));

                    foreach (var link in tmpLinksList)
                    {
                        var linkBuffer = String.Copy(link);
                        tasks.Add(Task.Factory.StartNew(() => OnProductParsed(GetProductFromLink(linkBuffer))));
                    }

                    if (!Task.WaitAll(tasks.ToArray(), _linksBunchParsingTimeout))
                    {
                        Log.InfoFormat("WaitAll timeout ({0})", _linksBunchParsingTimeout);
                    }
                }
            }
            catch (AggregateException aggEx)
            {
                aggEx.Handle(ex =>
                {
                    Log.Error(String.Format("Unhandled {0} while waiting for all parse tasks", ex.GetType()), ex);
                    return true;
                });
            }

            OnParsingFinished();
        }

        private Product GetProductFromLink(string link)
        {
            Product retProduct = default(Product);

            var webGet = new HtmlWeb();

            try
            {
                Uri testUri;

                if (Uri.TryCreate(link, UriKind.Absolute, out testUri))
                {
                    var page = webGet.Load(link);

                    if (page != null)
                    {
                        long id = GetIdFromPage(page);
                        var name = GetNameFromPage(page);
                        var price = GetPriceFromPage(page);

                        if (id == default(long) || String.IsNullOrEmpty(name) || String.IsNullOrEmpty(price))
                        {
                            Log.Error(String.Format("Failed to parse page {0}. id: {1}, name: {2}, price: {3}", link, id, name, price));
                        }
                        else
                        {
                            retProduct = new Product(id, name, price);
                        }
                    }
                }
                else
                {
                    Log.Error("Bad URL format");
                }
            }
            catch (WebException webEx)
            {
                Log.Error(String.Format("Failed to load page {0}", link), webEx);
            }
            catch (Exception ex)
            {
                Log.Error(String.Format("Unhandled {0} while parsing {1}", ex.GetType(), link), ex);
            }

            return retProduct;
        }

        private HtmlNode GetHtmlNode(HtmlDocument page, string nodeType, string nodeClass)
        {
            var ret = page.DocumentNode
                .Descendants()
                .FirstOrDefault(node => node.Name == nodeType && node.Attributes
                    .Any(a => a.Name == "class" && a.Value == nodeClass));
            return ret;
        }

        private long GetIdFromPage(HtmlDocument page)
        {
            long id = default(long);
            // <div class="shouhinmei">
            var idNode = GetHtmlNode(page, "div", "shouhinmei");

            if (idNode != null)
            {
                idNode = idNode.Descendants().FirstOrDefault(node => node.Name == "span");
            }

            if (idNode != null && idNode.InnerText != null)
            {
                var rgx = new Regex(@"[0-9]+");          // any digits sequence
                var match = rgx.Match(idNode.InnerText);

                if (match.Success)
                {
                    long.TryParse(match.Value, out id);
                }
            }

            return id;
        }

        private string GetNameFromPage(HtmlDocument page)
        {
            string name = String.Empty;
            // <h1 class="shouhin_name">
            var nameNode = GetHtmlNode(page, "h1", "shouhin_name");

            if (nameNode != null)
            {
                name = nameNode.InnerText;
            }

            return name;
        }

        private string GetPriceFromPage(HtmlDocument page)
        {
            string price = String.Empty;
            // <span class="price">
            var priceNode = GetHtmlNode(page, "span", "price");

            if (priceNode != null && priceNode.InnerText != null)
            {
                var rgx = new Regex(@"(\d{0,3},)*\d{1,3}");    // 123,456,789,000 or just 214
                var match = rgx.Match(priceNode.InnerText);

                if (match.Success)
                {
                    price = match.Value;
                }
            }

            return price;
        }

        #endregion
    }
}