/*
 * Copyright © Ilya Khaprov http://dead-trickster.com 2010
 * Use it as you want. But please tell me about bugs and your suggestions. 
 * Don't remove this copyright message.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration.Provider;
using MongoDB.Xtras;
using System.Configuration;
using MongoDB.Driver;
using System.Diagnostics;

namespace System.Web.Security
{
    class MongoDBRoleProvider: RoleProvider
    {
        //
        // Global connection string, generic exception message, event log info.
        //

        private string eventSource = "OdbcRoleProvider";
        private string eventLog = "Application";
        private string exceptionMessage = "An exception occurred. Please check the Event Log.";

        private ConnectionStringSettings pConnectionStringSettings;
        private string connectionString;


        //
        // If false, exceptions are thrown to the caller. If true,
        // exceptions are written to the event log.
        //

        private bool pWriteExceptionsToEventLog = false;

        public bool WriteExceptionsToEventLog
        {
            get { return pWriteExceptionsToEventLog; }
            set { pWriteExceptionsToEventLog = value; }
        }

        //
        // System.Web.Security.RoleProvider properties.
        //

        private string pApplicationName;


        public override string ApplicationName
        {
            get { return pApplicationName; }
            set { pApplicationName = value; }
        } 

        ConnectionManager cm;

        // schema
        string[] usersInRolesFields = { "Username", "Rolename", "ApplicationName" };
        string[] rolesFields = { "Rolename", "ApplicationName" };


        public override void Initialize(string name, System.Collections.Specialized.NameValueCollection config)
        {
            //
            // Initialize values from web.config.
            //

            if (config == null)
                throw new ArgumentNullException("config");

            if (name == null || name.Length == 0)
                name = "OdbcRoleProvider";

            if (String.IsNullOrEmpty(config["description"]))
            {
                config.Remove("description");
                config.Add("description", "Sample ODBC Role provider");
            }

            // Initialize the abstract base class.
            base.Initialize(name, config);


            if (config["applicationName"] == null || config["applicationName"].Trim() == "")
            {
                pApplicationName = System.Web.Hosting.HostingEnvironment.ApplicationVirtualPath;
            }
            else
            {
                pApplicationName = config["applicationName"];
            }


            if (config["writeExceptionsToEventLog"] != null)
            {
                if (config["writeExceptionsToEventLog"].ToUpper() == "TRUE")
                {
                    pWriteExceptionsToEventLog = true;
                }
            }


            //
            // Initialize OdbcConnection.
            //

            pConnectionStringSettings = ConfigurationManager.
              ConnectionStrings[config["connectionStringName"]];

            if (pConnectionStringSettings == null || pConnectionStringSettings.ConnectionString.Trim() == "")
            {
                throw new ProviderException("Connection string cannot be blank.");
            }

            connectionString = pConnectionStringSettings.ConnectionString;

            cm = new ConnectionManager(connectionString);

        }

        public override void AddUsersToRoles(string[] usernames, string[] rolenames)
        {
            foreach (string rolename in rolenames)
            {
                if (!RoleExists(rolename))
                {
                    throw new ProviderException("Role name not found.");
                }
            }

            foreach (string username in usernames)
            {
                if (username.Contains(","))
                {
                    throw new ArgumentException("User names cannot contain commas.");
                }

                foreach (string rolename in rolenames)
                {
                    if (IsUserInRole(username, rolename))
                    {
                        throw new ProviderException("User is already in role.");
                    }
                }
            }

            try
            {
                using (var conn = cm.New)
                {
                    var usersInRoles = conn.GetCollection("aspnet_usersinroles");

                    List<object[]> values = new List<object[]>();

                    foreach (string username in usernames)
                    {
                        foreach (string rolename in rolenames)
                        {
                            usersInRoles.Insert(usersInRolesFields, username, rolename, ApplicationName);
                        }
                    }
                }
            }
            catch(MongoException e)
            {
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "AddUsersToRoles");
                }
                else
                {
                    throw new ProviderException(e.Message, e);
                }
            }
        }

        public override void CreateRole(string rolename)
        {
            if (rolename.Contains(","))
            {
                throw new ArgumentException("Role names cannot contain commas.");
            }

            if (RoleExists(rolename))
            {
                throw new ProviderException("Role name already exists.");
            }

            try
            {
                using (var conn = cm.New)
                {
                    var roles = conn.GetCollection("aspnet_roles");
                    roles.Insert(rolesFields, rolename, ApplicationName);
                }
            }
            catch (MongoException e)
            {
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "CreateRole");
                }
                else
                {
                    throw new ProviderException(e.Message, e);
                }
            }
        }

        public override bool DeleteRole(string rolename, bool throwOnPopulatedRole)
        {
            if (!RoleExists(rolename))
            {
                throw new ProviderException("Role does not exist.");
            }

            if (throwOnPopulatedRole && GetUsersInRole(rolename).Length > 0)
            {
                throw new ProviderException("Cannot delete a populated role.");
            }

            try
            {
                using (var conn = cm.New)
                {
                    var usersInRoles = conn.GetCollection("aspnet_usersinroles");
                    usersInRoles.Delete(rolesFields, rolename, ApplicationName);

                   var roles = conn.GetCollection("aspnet_roles");
                   roles.Delete(rolesFields, rolename, ApplicationName);
                }
            }
            catch (MongoException e)
            {
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "DeleteRole");

                    return false;
                }
                else
                {
                    throw new ProviderException(e.Message, e);
                }
            }

            return true;

        }

        public override string[] FindUsersInRole(string roleName, string usernameToMatch)
        {
            List<string> users = new List<string>();

            try
            {
                using (var conn = cm.New)
                {
                    var usersInRoles = conn.GetCollection("aspnet_usersinroles");
                    using (var cursor = usersInRoles.Find(usersInRolesFields, new MongoRegex(usernameToMatch, "i"), roleName, ApplicationName))
                    {
                        foreach (var doc in cursor.Documents)
                        {
                            users.Add((string)doc["Username"]);
                        }
                    }
                }
            }
            catch (MongoException e)
            {
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "FindUsersInRole");
                }
                else
                {
                    throw new ProviderException(e.Message, e);
                }
            }

            return users.ToArray();

        }

        public override string[] GetAllRoles()
        {
            List<string> roles = new List<string>();

            try
            {
                using (var conn = cm.New)
                {
                    var usersInRoles = conn.GetCollection("aspnet_usersinroles");

                    using (var cursor = usersInRoles.Find(new Document().Append("ApplicationName", ApplicationName)))
                    {
                        foreach (var doc in cursor.Documents)
                        {
                            roles.Add((string)doc["Rolename"]);
                        }
                    }
                }
            }
            catch (MongoException e)
            {
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "GetAllRoles");
                }
                else
                {
                    throw new ProviderException(e.Message, e);
                }
            }

            return roles.ToArray();
        }

        public override string[] GetRolesForUser(string username)
        {
            List<string> roles = new List<string>();

            try
            {
                using (var conn = cm.New)
                {
                    var usersInRoles = conn.GetCollection("aspnet_usersinroles");

                    using (var cursor = usersInRoles.Find(new Document().Append("Username", username).Append("ApplicationName", ApplicationName)))
                    {
                        foreach (var doc in cursor.Documents)
                        {
                            roles.Add((string)doc["Rolename"]);
                        }
                    }
                }
            }
            catch (MongoException e)
            {
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "GetRolesForUser");
                }
                else
                {
                    throw new ProviderException(e.Message, e);
                }
            }

            return roles.ToArray();
        }

        public override string[] GetUsersInRole(string roleName)
        {
            List<string> names = new List<string>();

            try
            {
                using (var conn = cm.New)
                {
                    var usersInRoles = conn.GetCollection("aspnet_usersinroles");

                    using (var cursor = usersInRoles.Find(rolesFields, roleName, ApplicationName))
                    {
                        foreach (var doc in cursor.Documents)
                        {
                            names.Add((string)doc["Username"]);
                        }
                    }
                }
            }
            catch (MongoException e)
            {
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "GetUsersInRole");
                }
                else
                {
                    throw new ProviderException(e.Message, e);
                }
            }

            return names.ToArray();
        }

        public override bool IsUserInRole(string username, string rolename)
        {
            try
            {
                using (var conn = cm.New)
                {
                    var roles = conn.GetCollection("aspnet_roles");
                    return roles.FindOne(usersInRolesFields, username, rolename, ApplicationName) == null ? false : true;
                }
            }
            catch (MongoException e)
            {
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "IsUserInRole");
                }
                else
                {
                    throw new ProviderException(e.Message, e);
                }
            }

            return false;
        }

        public override void RemoveUsersFromRoles(string[] usernames, string[] rolenames)
        {
            foreach (string rolename in rolenames)
            {
                if (!RoleExists(rolename))
                {
                    throw new ProviderException("Role name not found.");
                }
            }

            foreach (string username in usernames)
            {
                foreach (string rolename in rolenames)
                {
                    if (!IsUserInRole(username, rolename))
                    {
                        throw new ProviderException("User is not in role.");
                    }
                }
            }

            try
            {
                using (var conn = cm.New)
                {
                    var usersInRoles = conn.GetCollection("aspnet_usersinroles");

                    foreach (string username in usernames)
                    {
                        foreach (string rolename in rolenames)
                        {
                            usersInRoles.Delete(usersInRolesFields, username, rolename, ApplicationName);
                        }
                    }
                }
            }
            catch (MongoException e)
            {
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "RemoveUsersFromRoles");
                }
                else
                {
                    throw new ProviderException(e.Message, e);
                }
            }

        }

        public override bool RoleExists(string rolename)
        {
            try
            {
                using (var conn = cm.New)
                {
                    var roles = conn.GetCollection("aspnet_roles");
                    return roles.FindOne(rolesFields, rolename, ApplicationName) == null ? false : true;
                }
            }
            catch (MongoException e)
            {
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "RoleExists");
                }
                else
                {
                    throw new ProviderException(e.Message, e);
                }
            }

            return false;
        }


        //
        // WriteToEventLog
        //   A helper function that writes exception detail to the event log. Exceptions
        // are written to the event log as a security measure to avoid private database
        // details from being returned to the browser. If a method does not return a status
        // or boolean indicating the action succeeded or failed, a generic exception is also 
        // thrown by the caller.
        //

        private void WriteToEventLog(MongoException e, string action)
        {
            EventLog log = new EventLog();
            log.Source = eventSource;
            log.Log = eventLog;

            string message = exceptionMessage + "\n\n";
            message += "Action: " + action + "\n\n";
            message += "Exception: " + e.ToString();

            log.WriteEntry(message);
        }

    }
}
