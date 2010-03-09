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

namespace MongoDB.Xtras
{
    public static class DocumentExtensions
    {
        public static void Fill(this Document doc, params Func<object, object>[] hash)
        {
            foreach (var func in hash)
            {
                doc[func.Method.GetParameters()[0].Name] = func(null);
            } 
        } 
    }
}
