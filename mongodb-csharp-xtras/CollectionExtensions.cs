/*
 * Copyright © Ilya Khaprov http://dead-trickster.com 2010
 * Use it as you want. But please tell me about bugs and your suggestions. 
 * Don't remove this copyright message.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Driver;

namespace MongoDB.Xtras
{
    public static class CollectionExtensions
    {
        public static void Fill(this IMongoCollection collection, string[] names, object[][] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                collection.Insert(names, values[i]);
            }
        }

        private static Document creatadocFromArrays(string[] names, object[] values)
        {
            if (values.Length != names.Length)
            {
                throw new MongoXtrasException("MongoCollection.Fill: the length is different");
            }

            Document doc = new Document();

            for (int i = 0; i < names.Length; i++)
            {
                doc.Append(names[i], values[i]);
            }
            return doc;
        }

        public static void Insert(this IMongoCollection collection, string[] names, params object[] values)
        {
            Document doc = creatadocFromArrays(names, values);

            collection.Insert(doc);
        }

        public static void Delete(this IMongoCollection collection, string[] names, params object[] values)
        {
            collection.Delete(creatadocFromArrays(names, values));
        }

        public static ICursor Find(this IMongoCollection collection, string[] names, params object[] values)
        {
            return collection.Find(creatadocFromArrays(names, values));
        }

        public static Document FindOne(this IMongoCollection collection, string[] names, params object[] values)
        {
            return collection.FindOne(creatadocFromArrays(names, values));
        }
    }
}
