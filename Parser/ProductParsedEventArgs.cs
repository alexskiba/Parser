using System;
namespace ParsingApp
{
    class ProductParsedEventArgs : EventArgs
    {
        public ProductParsedEventArgs(Product product)
        {
            Product = product;
        }

        public Product Product
        {
            get;
            private set;
        }
    }
}
