using System;

namespace ParsingApp
{
    struct Product
    {
        public Product(long id, string name, string price)
            : this() 
        {
            Id = id;
            Name = name;
            Price = price;
        }

        public override string ToString()
        {
            return ToString("{0},{1},\"{2}\"");
        }

        public string ToString(string format)
        {
            return String.Format(format, Id, Name, Price);
        }

        public long Id
        {
            get;
            private set;
        }

        public string Name
        {
            get;
            private set;
        }

        public string Price
        {
            get;
            private set;
        }
    }
}