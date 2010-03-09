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
using System.Collections;

namespace MongoDBXtras
{
    public static class MongoSerializer
    {
        public static Document Serialize(this object obj)
        {
            object serialized = SerializeMember(obj);

            Document doc = serialized as Document;
            if (doc != null)
                return doc;

            return new Document().Append("", serialized);
        }

        private static object SerializeMember(object obj)
        {
            Type objType = obj.GetType();

            switch (Type.GetTypeCode(objType))
            {
                case TypeCode.Boolean:
                case TypeCode.Byte:
                case TypeCode.Char:
                    return obj;
                case TypeCode.DBNull:
                    return null;
                case TypeCode.DateTime:
                case TypeCode.Decimal:
                case TypeCode.Double:
                    return obj;
                case TypeCode.Empty:
                    return null;
                case TypeCode.Int16:
                case TypeCode.Int32:
                case TypeCode.Int64:
                    return obj;
                case TypeCode.Object:
                    IEnumerable enumerable = obj as IEnumerable;
                    if (enumerable != null)
                    {
                        List<object> docs = new List<object>();
                        foreach (var item in enumerable)
                        {
                            docs.Add(SerializeMember(item));
                        }

                        return docs.ToArray();
                    }
                    else if (objType.IsEnum)
                    {
                        return obj;
                    }
                    else
                    {
                        var publicProperties = objType.GetProperties();

                        Document doc = new Document();

                        for (int i = 0; i < publicProperties.Length; i++)
                        {
                            var property = publicProperties[i];

                            if (property.GetGetMethod().GetParameters().Length != 0)
                            {
                                continue;
                            }

                            object propertyValue = property.GetValue(obj, null);

                            if (propertyValue == null)
                            {
                                continue;
                            }

                            doc.Add(property.Name, SerializeMember(propertyValue));
                        }

                        return doc;
                    }
                case TypeCode.SByte:
                case TypeCode.Single:
                case TypeCode.String:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                    return obj;
                default:
                    return obj;
            }
        }
    }
}
