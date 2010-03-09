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
using System.Web.Security;
using System.Data.Common;

namespace MongoDB.Xtras
{    
    public class MongoDBConnection : IDisposable
    {
        Mongo m;
        string database;

        public MongoDBConnection(Mongo m, string database)
        {
            this.m = m;
            this.database = database;
            m.Connect();
        }

        public Database Database
        {
            get { return m.getDB(database); }
        }

        public IMongoCollection GetCollection(string name)
        {
            return Database.GetCollection(name);
        }

        #region IDisposable Members

        public void Dispose()
        {
            m.Disconnect();
            m.Dispose();
        }

        #endregion
    }

    public class ConnectionManager
    {
        DbConnectionStringBuilder builder
                = new DbConnectionStringBuilder();

        public ConnectionManager(string connectionString)
        {
            builder.ConnectionString = connectionString;
        }

        Mongo m;
        string d;

        public MongoDBConnection New
        {
            get
            {
                Mongo m;

                string database;

                if (!builder.ContainsKey("db"))
                {
                    throw new MongoException("unknown database");
                }

                database = (string)builder["db"];

                if (builder.ContainsKey("host"))
                {
                    string host = (string)builder["host"];

                    if (builder.ContainsKey("port"))
                    {
                        string portstr = (string)builder["port"];

                        int port;

                        if (!int.TryParse(portstr, out port))
                        {
                            throw new MongoException("unable to parse port " + portstr);
                        }

                        m = new Mongo(host, port);
                    }

                    m = new Mongo(host);
                }
                else
                    m = new Mongo();

                this.m = m;
                this.d = database;

                return new MongoDBConnection(m, database);
            }
        }
    }
}
