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
using System.Linq.Expressions;
using System.Collections.ObjectModel;

namespace MongoDB.Xtras
{
    public delegate object Pair(object n);
    public class Pairs : Collection<Pair>
    {
        public Pairs(IList<Pair> pairs) : base(pairs) { }

        public Pairs() { }
    }

    public static class DocumentExtensions
    {
        public static void Fill(this Document doc, params Pair[] hash)
        {
            foreach (var func in hash)
            {
                object ret = func(null);

                Pairs pairs = ret as Pairs;

                if(pairs != null)
                {
                    Document d = new Document();
                    d.Fill(pairs.ToArray());
                    ret = d;
                }

                doc[func.Method.GetParameters()[0].Name] = ret;
            } 
        } 
    }
}
