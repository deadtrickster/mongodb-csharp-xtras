using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Driver;

namespace MongoDB.Xtras
{
    public class MongoXtrasException : MongoException
    {
        public MongoXtrasException(string message) : base(message) { }
        public MongoXtrasException(string message, Exception inner) : base(message, inner) { }
    }
}
