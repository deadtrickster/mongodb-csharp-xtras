/*
 * Copyright © Ilya Khaprov http://dead-trickster.com 2010
 * Use it as you want. But please tell me about bugs and your suggestions. 
 * Don't remove this copyright message.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Security;
using System.Collections.Specialized;
using System.Configuration;
using System.Web.Configuration;
using System.Configuration.Provider;
using MongoDB.Driver;
using System.Data.Common;
using System.Diagnostics;
using System.Security.Cryptography;

namespace System.Web.Security
{
    class MongoDBConnection : IDisposable
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

        #region IDisposable Members

        public void Dispose()
        {
            m.Disconnect();
        }

        #endregion
    }


    class ConnectionManager
    {
        DbConnectionStringBuilder builder
                = new DbConnectionStringBuilder();



        internal ConnectionManager(string connectionString)
        {
            builder.ConnectionString = connectionString;
        }

        public MongoDBConnection newConnection()
        {
            Mongo m;

            string database;

            if (!builder.ContainsKey("db"))
            {
                throw new ProviderException("unknown database");
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
                        throw new ProviderException("unable to parse port " + portstr);
                    }

                    m = new Mongo(host, port);
                }

                m = new Mongo(host);
            }
            else
                m = new Mongo();

            return new MongoDBConnection(m, database);
        }
    }

    public class MongoDBMembershipProvider : MembershipProvider
    {
        //
        // Global connection string, generated password length, generic exception message, event log info.
        //

        private int newPasswordLength = 8;
        private string eventSource = "MongoDBMembershipProvider";
        private string eventLog = "Application";
        private string exceptionMessage = "An exception occurred. Please check the Event Log.";
        private string connectionString;

        private ConnectionManager cm;
        //
        // Used when determining encryption key values.
        //

        private MachineKeySection machineKey;

        //
        // If false, exceptions are thrown to the caller. If true,
        // exceptions are written to the event log.
        //

        private bool pWriteExceptionsToEventLog;

        public bool WriteExceptionsToEventLog
        {
            get { return pWriteExceptionsToEventLog; }
            set { pWriteExceptionsToEventLog = value; }
        }

        public override void Initialize(string name, NameValueCollection config)
        {
            //
            // Initialize values from web.config.
            //

            if (config == null)
                throw new ArgumentNullException("config");

            if (name == null || name.Length == 0)
                name = "MongoDBMembershipProvider";

            if (String.IsNullOrEmpty(config["description"]))
            {
                config.Remove("description");
                config.Add("description", "MongoDB Membership provider");
            }

            // Initialize the abstract base class.
            base.Initialize(name, config);

            pApplicationName = GetConfigValue(config["applicationName"],
                                            System.Web.Hosting.HostingEnvironment.ApplicationVirtualPath);
            pMaxInvalidPasswordAttempts = Convert.ToInt32(GetConfigValue(config["maxInvalidPasswordAttempts"], "5"));
            pPasswordAttemptWindow = Convert.ToInt32(GetConfigValue(config["passwordAttemptWindow"], "10"));
            pMinRequiredNonAlphanumericCharacters = Convert.ToInt32(GetConfigValue(config["minRequiredNonAlphanumericCharacters"], "1"));
            pMinRequiredPasswordLength = Convert.ToInt32(GetConfigValue(config["minRequiredPasswordLength"], "7"));
            pPasswordStrengthRegularExpression = Convert.ToString(GetConfigValue(config["passwordStrengthRegularExpression"], ""));
            pEnablePasswordReset = Convert.ToBoolean(GetConfigValue(config["enablePasswordReset"], "true"));
            pEnablePasswordRetrieval = Convert.ToBoolean(GetConfigValue(config["enablePasswordRetrieval"], "true"));
            pRequiresQuestionAndAnswer = Convert.ToBoolean(GetConfigValue(config["requiresQuestionAndAnswer"], "false"));
            pRequiresUniqueEmail = Convert.ToBoolean(GetConfigValue(config["requiresUniqueEmail"], "true"));
            pWriteExceptionsToEventLog = Convert.ToBoolean(GetConfigValue(config["writeExceptionsToEventLog"], "true"));

            string temp_format = config["passwordFormat"];
            if (temp_format == null)
            {
                temp_format = "Hashed";
            }

            switch (temp_format)
            {
                case "Hashed":
                    pPasswordFormat = MembershipPasswordFormat.Hashed;
                    break;
                case "Encrypted":
                    pPasswordFormat = MembershipPasswordFormat.Encrypted;
                    break;
                case "Clear":
                    pPasswordFormat = MembershipPasswordFormat.Clear;
                    break;
                default:
                    throw new ProviderException("Password format not supported.");
            }

            //
            // Initialize OdbcConnection.
            //

            ConnectionStringSettings ConnectionStringSettings =
              ConfigurationManager.ConnectionStrings[config["connectionStringName"]];

            if (ConnectionStringSettings == null || ConnectionStringSettings.ConnectionString.Trim() == "")
            {
                throw new ProviderException("Connection string cannot be blank.");
            }

            connectionString = ConnectionStringSettings.ConnectionString;

            cm = new ConnectionManager(connectionString);


            // Get encryption and decryption key information from the configuration.
            var cfg =
              WebConfigurationManager.OpenWebConfiguration(System.Web.Hosting.HostingEnvironment.ApplicationVirtualPath);
            machineKey = (MachineKeySection)cfg.GetSection("system.web/machineKey");

            if (machineKey.ValidationKey.Contains("AutoGenerate"))
                if (PasswordFormat != MembershipPasswordFormat.Clear)
                    throw new ProviderException("Hashed or Encrypted passwords " +
                                                "are not supported with auto-generated keys.");

        }

        //
        // A helper function to retrieve config values from the configuration file.
        //

        private string GetConfigValue(string configValue, string defaultValue)
        {
            if (String.IsNullOrEmpty(configValue))
                return defaultValue;

            return configValue;
        }


        //
        // System.Web.Security.MembershipProvider properties.
        //


        private string pApplicationName;
        private bool pEnablePasswordReset;
        private bool pEnablePasswordRetrieval;
        private bool pRequiresQuestionAndAnswer;
        private bool pRequiresUniqueEmail;
        private int pMaxInvalidPasswordAttempts;
        private int pPasswordAttemptWindow;
        private MembershipPasswordFormat pPasswordFormat;


        public override string ApplicationName
        {
            get { return pApplicationName; }
            set { pApplicationName = value; }
        }

        public override bool ChangePassword(string username, string oldPassword, string newPassword)
        {
            if (!ValidateUser(username, oldPassword))
                return false;


            ValidatePasswordEventArgs args =
              new ValidatePasswordEventArgs(username, oldPassword, true);

            OnValidatingPassword(args);

            if (args.Cancel)
                if (args.FailureInformation != null)
                    throw args.FailureInformation;
                else
                    throw new MembershipPasswordException("Change password canceled due to new password validation failure.");

            try
            {
                using (MongoDBConnection conn = cm.newConnection())
                {
                    var collection = conn.Database.GetCollection("aspnet_members");
                    using (var cursor = collection.Find(new Document().Append("Username", username).Append("ApplicationName", pApplicationName)))
                    {
                        Document doc = cursor.Documents.FirstOrDefault(null);

                        if (doc == null)
                        {
                            return false;
                        }

                        doc["Password"] = newPassword;
                        doc["LastPasswordChangedDate"] = DateTime.Now;

                        collection.Update(doc);
                    }

                }
            }
            catch (MongoException e)
            {
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "ChangePassword");

                    throw new ProviderException(exceptionMessage);
                }
                else
                {
                    throw e;
                }
            }

            return true;
        }

        public override bool ChangePasswordQuestionAndAnswer(string username, string password, string newPasswordQuestion, string newPasswordAnswer)
        {
            if (!ValidateUser(username, password))
                return false;

            try
            {
                using (MongoDBConnection conn = cm.newConnection())
                {
                    var collection = conn.Database.GetCollection("aspnet_members");
                    using (var cursor = collection.Find(new Document().Append("Username", username).Append("ApplicationName", pApplicationName)))
                    {
                        Document doc = cursor.Documents.FirstOrDefault(null);

                        if (doc == null)
                        {
                            return false;
                        }

                        doc["PasswordQuestion"] = newPasswordQuestion;
                        doc["LastPasswordChangedDate"] = EncodePassword(newPasswordAnswer);

                        collection.Update(doc);
                    }
                }
            }
            catch (MongoException e)
            {
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "ChangePassword");

                    throw new ProviderException(exceptionMessage);
                }
                else
                {
                    throw e;
                }
            }

            return true;

        }

        public override MembershipUser CreateUser(string username, string password, string email, string passwordQuestion, string passwordAnswer, bool isApproved, object providerUserKey, out MembershipCreateStatus status)
        {
            ValidatePasswordEventArgs args =
        new ValidatePasswordEventArgs(username, password, true);

            OnValidatingPassword(args);

            if (args.Cancel)
            {
                status = MembershipCreateStatus.InvalidPassword;
                return null;
            }



            if (RequiresUniqueEmail && GetUserNameByEmail(email) != "")
            {
                status = MembershipCreateStatus.DuplicateEmail;
                return null;
            }

            MembershipUser u = GetUser(username, false);

            if (u == null)
            {
                DateTime createDate = DateTime.Now;

                if (providerUserKey == null)
                {
                    providerUserKey = Guid.NewGuid();
                }
                else
                {
                    if (!(providerUserKey is Guid))
                    {
                        status = MembershipCreateStatus.InvalidProviderUserKey;
                        return null;
                    }
                }

                try
                {

                    using (MongoDBConnection conn = cm.newConnection())
                    {
                        var collection = conn.Database.GetCollection("aspnet_members");

                        Document doc = new Document();

                        doc.Append("PKID", providerUserKey);
                        doc.Append("Username", username);
                        doc.Append("Password", EncodePassword(password));
                        doc.Append("Email", email);
                        doc.Append("PasswordQuestion", passwordQuestion);
                        doc.Append("PasswordAnswer", EncodePassword(passwordAnswer));
                        doc.Append("IsApproved", isApproved);
                        doc.Append("Comment", "");
                        doc.Append("CreationDate", createDate);
                        doc.Append("LastPasswordChangedDate", createDate);
                        doc.Append("LastActivityDate", createDate);
                        doc.Append("ApplicationName", pApplicationName);
                        doc.Append("IsLockedOut", false);
                        doc.Append("LastLockedOutDate", createDate);
                        doc.Append("FailedPasswordAttemptCount", 0);
                        doc.Append("FailedPasswordAttemptWindowStart", createDate);
                        doc.Append("FailedPasswordAnswerAttemptCount", 0);
                        doc.Append("FailedPasswordAnswerAttemptWindowStart", createDate);

                        collection.Insert(doc);
                    }

                    status = MembershipCreateStatus.Success;
                }
                catch (MongoException e)
                {
                    if (WriteExceptionsToEventLog)
                    {
                        WriteToEventLog(e, "CreateUser");
                    }

                    status = MembershipCreateStatus.ProviderError;
                }


                return GetUser(username, false);
            }
            else
            {
                status = MembershipCreateStatus.DuplicateUserName;
            }


            return null;

        }

        public override bool DeleteUser(string username, bool deleteAllRelatedData)
        {
            try
            {
                using (MongoDBConnection conn = cm.newConnection())
                {
                    var collection = conn.Database.GetCollection("aspnet_members");
                    using (var cursor = collection.Find(new Document().Append("Username", username).Append("ApplicationName", pApplicationName)))
                    {
                        Document doc = cursor.Documents.FirstOrDefault(null);

                        if (doc == null)
                        {
                            return false;
                        }

                        collection.Delete(doc);
                    }
                }

                if (deleteAllRelatedData)
                {
                    // Process commands to delete all data for the user in the database.
                }
            }
            catch (MongoException e)
            {
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "DeleteUser");

                    throw new ProviderException(exceptionMessage);
                }
                else
                {
                    throw e;
                }
            }

            return true;
        }

        public override bool EnablePasswordReset
        {
            get { return pEnablePasswordReset; }
        }

        public override bool EnablePasswordRetrieval
        {
            get { return pEnablePasswordRetrieval; }
        }


        public override MembershipUserCollection FindUsersByEmail(string emailToMatch, int pageIndex, int pageSize, out int totalRecords)
        {
            throw new NotImplementedException();
        }

        public override MembershipUserCollection FindUsersByName(string usernameToMatch, int pageIndex, int pageSize, out int totalRecords)
        {
            throw new NotImplementedException();
        }

        public override MembershipUserCollection GetAllUsers(int pageIndex, int pageSize, out int totalRecords)
        {
            MembershipUserCollection users = new MembershipUserCollection();

            try
            {
                using (MongoDBConnection conn = cm.newConnection())
                {
                    var collection = conn.Database.GetCollection("aspnet_members");

                    using (var cursor = collection.Find(new Document().Append("ApplicationName", pApplicationName)))
                    {
                        foreach (var doc in cursor.Documents)
                        {
                            users.Add(GetUserFromDocument(doc));
                        }
                    }

                    totalRecords = users.Count;
                }
            }
            catch (MongoException e)
            {
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "DeleteUser");

                    throw new ProviderException(exceptionMessage);
                }
                else
                {
                    throw e;
                }
            }

            return users;
        }

        public override int GetNumberOfUsersOnline()
        {
            TimeSpan onlineSpan = new TimeSpan(0, System.Web.Security.Membership.UserIsOnlineTimeWindow, 0);
            DateTime compareTime = DateTime.Now.Subtract(onlineSpan);

            try
            {
                using (MongoDBConnection conn = cm.newConnection())
                {
                    var collection = conn.Database.GetCollection("aspnet_members");

                    var count = collection.Count(new Document().Append("LastActivityDate", new Document().Append("$gt", compareTime)).Append("ApplicationName", pApplicationName));

                    if (count > Int32.MaxValue)
                    {
                        throw new ProviderException("Number of users is too big");
                    }

                    return (int)count;
                }
            }
            catch (MongoException e)
            {
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "GetNumberOfUsersOnline");

                    throw new ProviderException(exceptionMessage);
                }
                else
                {
                    throw e;
                }
            }
        }

        public override string GetPassword(string username, string answer)
        {
            if (!EnablePasswordRetrieval)
            {
                throw new ProviderException("Password Retrieval Not Enabled.");
            }

            if (PasswordFormat == MembershipPasswordFormat.Hashed)
            {
                throw new ProviderException("Cannot retrieve Hashed passwords.");
            }

            string password = "";
            string passwordAnswer = "";

            try
            {
                using (MongoDBConnection conn = cm.newConnection())
                {
                    var collection = conn.Database.GetCollection("aspnet_members");

                    using (var cursor = collection.Find(new Document().Append("Username", username).Append("ApplicationName", pApplicationName)))
                    {
                        Document user = cursor.Documents.FirstOrDefault(null);

                        if (user == null)
                        {
                            throw new MembershipPasswordException("The supplied user name is not found.");
                        }

                        if ((bool)user["IsLockedOut"])
                        {
                            throw new MembershipPasswordException("The supplied user is locked out.");
                        }


                        password = (string)user["Password"];
                        passwordAnswer = (string)user["PasswordAnswer"];
                    }
                }
            }
            catch (MongoException e)
            {
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "GetPassword");

                    throw new ProviderException(exceptionMessage);
                }
                else
                {
                    throw e;
                }
            }

            if (RequiresQuestionAndAnswer && !CheckPassword(answer, passwordAnswer))
            {
                UpdateFailureCount(username, "passwordAnswer");

                throw new MembershipPasswordException("Incorrect password answer.");
            }


            if (PasswordFormat == MembershipPasswordFormat.Encrypted)
            {
                password = UnEncodePassword(password);
            }

            return password;
        }

        public override MembershipUser GetUser(string username, bool userIsOnline)
        {
            MembershipUser u = null;

            try
            {
                using (MongoDBConnection conn = cm.newConnection())
                {
                    var collection = conn.Database.GetCollection("aspnet_members");

                    using (var cursor = collection.Find(new Document().Append("Username", username).Append("ApplicationName", pApplicationName)))
                    {
                        Document user = cursor.Documents.FirstOrDefault(null);

                        if (user != null)
                        {
                            u = GetUserFromDocument(user);
                        }

                        if (userIsOnline)
                        {
                            user["LastActivityDate"] = DateTime.Now;
                            collection.Update(user);
                        }
                    }
                }
            }
            catch (MongoException e)
            {
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "GetUser(String, Boolean)");

                    throw new ProviderException(exceptionMessage);
                }
                else
                {
                    throw e;
                }
            }

            return u;
        }

        public override MembershipUser GetUser(object providerUserKey, bool userIsOnline)
        {
            MembershipUser u = null;
            try
            {

                using (MongoDBConnection conn = cm.newConnection())
                {
                    var collection = conn.Database.GetCollection("aspnet_members");

                    using (var cursor = collection.Find(new Document().Append("PKID", providerUserKey)))
                    {
                        Document user = cursor.Documents.FirstOrDefault(null);

                        if (user != null)
                        {
                            u = GetUserFromDocument(user);
                        }

                        if (userIsOnline)
                        {
                            user["LastActivityDate"] = DateTime.Now;
                            collection.Update(user);
                        }
                    }
                }

            }
            catch (MongoException e)
            {
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "GetUser(Object, Boolean)");

                    throw new ProviderException(exceptionMessage);
                }
                else
                {
                    throw e;
                }
            }

            return u;

        }

        public override string GetUserNameByEmail(string email)
        {
            string username = "";

            try
            {

                using (MongoDBConnection conn = cm.newConnection())
                {
                    var collection = conn.Database.GetCollection("aspnet_members");

                    using (var cursor = collection.Find(new Document().Append("Email", email).Append("ApplicationName", pApplicationName)))
                    {
                        Document user = cursor.Documents.FirstOrDefault(null);

                        if (user != null)
                        {
                            username = (string)user["Usernmae"];
                        }
                    }
                }
            }
            catch (MongoException e)
            {
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "GetUserNameByEmail");

                    throw new ProviderException(exceptionMessage);
                }
                else
                {
                    throw e;
                }
            }

            if (username == null)
                username = "";

            return username;

        }

        public override int MaxInvalidPasswordAttempts
        {
            get { return pMaxInvalidPasswordAttempts; }
        }

        public override int PasswordAttemptWindow
        {
            get { return pPasswordAttemptWindow; }
        }

        public override MembershipPasswordFormat PasswordFormat
        {
            get { return pPasswordFormat; }
        }

        private int pMinRequiredNonAlphanumericCharacters;

        public override int MinRequiredNonAlphanumericCharacters
        {
            get { return pMinRequiredNonAlphanumericCharacters; }
        }

        private int pMinRequiredPasswordLength;

        public override int MinRequiredPasswordLength
        {
            get { return pMinRequiredPasswordLength; }
        }

        private string pPasswordStrengthRegularExpression;

        public override string PasswordStrengthRegularExpression
        {
            get { return pPasswordStrengthRegularExpression; }
        }

        public override bool RequiresQuestionAndAnswer
        {
            get { return pRequiresQuestionAndAnswer; }
        }

        public override bool RequiresUniqueEmail
        {
            get { return pRequiresUniqueEmail; }
        }

        public override string ResetPassword(string username, string answer)
        {
            if (!EnablePasswordReset)
            {
                throw new NotSupportedException("Password reset is not enabled.");
            }

            if (answer == null && RequiresQuestionAndAnswer)
            {
                UpdateFailureCount(username, "passwordAnswer");

                throw new ProviderException("Password answer required for password reset.");
            }

            string newPassword =
              System.Web.Security.Membership.GeneratePassword(newPasswordLength, MinRequiredNonAlphanumericCharacters);


            ValidatePasswordEventArgs args =
              new ValidatePasswordEventArgs(username, newPassword, true);

            OnValidatingPassword(args);

            if (args.Cancel)
                if (args.FailureInformation != null)
                    throw args.FailureInformation;
                else
                    throw new MembershipPasswordException("Reset password canceled due to password validation failure.");

            string passwordAnswer = "";

            try
            {

                using (MongoDBConnection conn = cm.newConnection())
                {
                    var collection = conn.Database.GetCollection("aspnet_members");

                    using (var cursor = collection.Find(new Document().Append("Username", username).Append("ApplicationName", pApplicationName)))
                    {
                        Document user = cursor.Documents.FirstOrDefault(null);

                        if (user != null)
                        {
                            if ((bool)user["IsLockedOut"])
                                throw new MembershipPasswordException("The supplied user is locked out.");

                            passwordAnswer = (string)user["PasswordAnswer"];
                        }
                        else
                        {
                            throw new MembershipPasswordException("The supplied user name is not found.");
                        }



                        if (RequiresQuestionAndAnswer && !CheckPassword(answer, passwordAnswer))
                        {
                            UpdateFailureCount(username, "passwordAnswer");

                            throw new MembershipPasswordException("Incorrect password answer.");
                        }

                        user["Password"] = EncodePassword(newPassword);
                        user["LastPasswordChangedDate"] = DateTime.Now;

                        collection.Update(user);
                    }
                }
            }
            catch (MongoException e)
            {
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "ResetPassword");

                    throw new ProviderException(exceptionMessage);
                }
                else
                {
                    throw e;
                }
            }

            return newPassword;
        }

        public override bool UnlockUser(string username)
        {
            try
            {
                using (MongoDBConnection conn = cm.newConnection())
                {
                    var collection = conn.Database.GetCollection("aspnet_members");

                    using (var cursor = collection.Find(new Document().Append("Username", username).Append("ApplicationName", pApplicationName)))
                    {
                        Document user = cursor.Documents.FirstOrDefault(null);

                        if (user == null)
                            return false;

                        user["IsLockedOut"] = false;
                        user["LastLockedOutDate"] = DateTime.Now;

                        collection.Update(user);
                    }
                }

            }
            catch (MongoException e)
            {
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "UnlockUser");

                    throw new ProviderException(exceptionMessage);
                }
                else
                {
                    throw e;
                }
            }

            return true;
        }

        public override void UpdateUser(MembershipUser u)
        {
            try
            {
                using (MongoDBConnection conn = cm.newConnection())
                {
                    var collection = conn.Database.GetCollection("aspnet_members");

                    using (var cursor = collection.Find(new Document().Append("Username", u.UserName).Append("ApplicationName", pApplicationName)))
                    {
                        Document user = cursor.Documents.FirstOrDefault(null);

                        if (user == null)
                            throw new ProviderException("Unknown user " + u.UserName);

                        user["Email"] = u.Email;
                        user["Comment"] = u.Comment;
                        user["IsApproved"] = u.IsApproved;
                    }
                }
            }
            catch (MongoException e)
            {
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "UpdateUser");

                    throw new ProviderException(exceptionMessage);
                }
                else
                {
                    throw e;
                }
            }
        }

        public override bool ValidateUser(string username, string password)
        {
            bool isValid = false;

            bool isApproved = false;
            string pwd = "";

            try
            {
                using (MongoDBConnection conn = cm.newConnection())
                {
                    var collection = conn.Database.GetCollection("aspnet_members");

                    using (var cursor = collection.Find(new Document().Append("Username", username).Append("ApplicationName", pApplicationName)))
                    {
                        Document user = cursor.Documents.FirstOrDefault(null);

                        if (user != null)
                        {
                            pwd = (string)user["Password"];
                            isApproved = (bool)user["IsApproved"];
                        }
                        else
                        {
                            return false;
                        }

                        if (CheckPassword(password, pwd))
                        {
                            if (isApproved)
                            {
                                isValid = true;

                                collection.Update(user);
                            }
                        }
                        else
                        {
                            UpdateFailureCount(username, "password");
                        }
                    }
                }
            }
            catch (MongoException e)
            {
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "ValidateUser");

                    throw new ProviderException(exceptionMessage);
                }
                else
                {
                    throw e;
                }
            }

            return isValid;

        }

        //
        // UpdateFailureCount
        //   A helper method that performs the checks and updates associated with
        // password failure tracking.
        //

        private void UpdateFailureCount(string username, string failureType)
        {
            DateTime windowStart = new DateTime();
            int failureCount = 0;

            try
            {
                using (MongoDBConnection conn = cm.newConnection())
                {
                    var collection = conn.Database.GetCollection("aspnet_members");

                    using (var cursor = collection.Find(new Document().Append("Username", username).Append("ApplicationName", pApplicationName)))
                    {
                        Document user = cursor.Documents.FirstOrDefault(null);

                        if (user != null)
                        {
                            if (failureType == "password")
                            {
                                failureCount = (int)user["FailedPasswordAttemptCount"];
                                windowStart = (DateTime)user["FailedPasswordAttemptWindowStart"];
                            }

                            if (failureType == "passwordAnswer")
                            {
                                failureCount = (int)user["FailedPasswordAnswerAttemptCount"];
                                windowStart = (DateTime)user["FailedPasswordAnswerAttemptWindowStart"];
                            }
                        }

                        DateTime windowEnd = windowStart.AddMinutes(PasswordAttemptWindow);

                        if (failureCount == 0 || DateTime.Now > windowEnd)
                        {
                            // First password failure or outside of PasswordAttemptWindow. 
                            // Start a new password failure count from 1 and a new window starting now.

                            if (failureType == "password")
                            {
                                user["FailedPasswordAttemptCount"] = 1;
                                user["FailedPasswordAttemptWindowStart"] = DateTime.Now;
                            }

                            if (failureType == "passwordAnswer")
                            {
                                user["FailedPasswordAnswerAttemptCount"] = 1;
                                user["FailedPasswordAnswerAttemptWindowStart"] = DateTime.Now;
                            }
                        }
                        else
                        {
                            if (failureCount++ >= MaxInvalidPasswordAttempts)
                            {
                                // Password attempts have exceeded the failure threshold. Lock out
                                // the user.

                                user["IsLockedOut"] = true;
                                user["LastLockedOutDate"] = DateTime.Now;
                            }
                            else
                            {
                                // Password attempts have not exceeded the failure threshold. Update
                                // the failure counts. Leave the window the same.

                                if (failureType == "password")
                                {
                                    user["FailedPasswordAttemptCount"] = failureCount;
                                }

                                if (failureType == "passwordAnswer")
                                {
                                    user["FailedPasswordAnswerAttemptCount"] = failureCount;
                                }
                            }
                        }

                        collection.Update(user);
                    }
                }
            }
            catch (MongoException e)
            {
                if (WriteExceptionsToEventLog)
                {
                    WriteToEventLog(e, "UpdateFailureCount");

                    throw new ProviderException(exceptionMessage);
                }
                else
                {
                    throw e;
                }
            }
        }

        //
        // CheckPassword
        //   Compares password values based on the MembershipPasswordFormat.
        //

        private bool CheckPassword(string password, string dbpassword)
        {
            string pass1 = password;
            string pass2 = dbpassword;

            switch (PasswordFormat)
            {
                case MembershipPasswordFormat.Encrypted:
                    pass2 = UnEncodePassword(dbpassword);
                    break;
                case MembershipPasswordFormat.Hashed:
                    pass1 = EncodePassword(password);
                    break;
                default:
                    break;
            }

            if (pass1 == pass2)
            {
                return true;
            }

            return false;
        }


        //
        // EncodePassword
        //   Encrypts, Hashes, or leaves the password clear based on the PasswordFormat.
        //

        private string EncodePassword(string password)
        {
            string encodedPassword = password;

            switch (PasswordFormat)
            {
                case MembershipPasswordFormat.Clear:
                    break;
                case MembershipPasswordFormat.Encrypted:
                    encodedPassword =
                      Convert.ToBase64String(EncryptPassword(Encoding.Unicode.GetBytes(password)));
                    break;
                case MembershipPasswordFormat.Hashed:
                    HMACSHA1 hash = new HMACSHA1();
                    hash.Key = HexToByte(machineKey.ValidationKey);
                    encodedPassword =
                      Convert.ToBase64String(hash.ComputeHash(Encoding.Unicode.GetBytes(password)));
                    break;
                default:
                    throw new ProviderException("Unsupported password format.");
            }

            return encodedPassword;
        }


        //
        // UnEncodePassword
        //   Decrypts or leaves the password clear based on the PasswordFormat.
        //

        private string UnEncodePassword(string encodedPassword)
        {
            string password = encodedPassword;

            switch (PasswordFormat)
            {
                case MembershipPasswordFormat.Clear:
                    break;
                case MembershipPasswordFormat.Encrypted:
                    password =
                      Encoding.Unicode.GetString(DecryptPassword(Convert.FromBase64String(password)));
                    break;
                case MembershipPasswordFormat.Hashed:
                    throw new ProviderException("Cannot unencode a hashed password.");
                default:
                    throw new ProviderException("Unsupported password format.");
            }

            return password;
        }

        //
        // HexToByte
        //   Converts a hexadecimal string to a byte array. Used to convert encryption
        // key values from the configuration.
        //

        private byte[] HexToByte(string hexString)
        {
            byte[] returnBytes = new byte[hexString.Length / 2];
            for (int i = 0; i < returnBytes.Length; i++)
                returnBytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            return returnBytes;
        }

        private MembershipUser GetUserFromDocument(Document doc)
        {
            object providerUserKey = doc["PKID"];
            string username = (string)doc["Username"];
            string email = (string)doc["Email"];

            string passwordQuestion = "";
            if (doc["PasswordQuestion"] != null)
                passwordQuestion = (string)doc["PasswordQuestion"];

            string comment = "";
            if (doc["Comment"] != null)
                comment = (string)doc["Comment"];

            bool isApproved = (bool)doc["IsApproved"];
            bool isLockedOut = (bool)doc["IsLockedOut"];
            DateTime creationDate = (DateTime)doc["CreationDate"];

            DateTime lastLoginDate = new DateTime();
            if (doc["LastLoginDate"] != null)
                lastLoginDate = (DateTime)doc["LastLoginDate"];

            DateTime lastActivityDate = (DateTime)doc["LastActivityDate"];
            DateTime lastPasswordChangedDate = (DateTime)doc["LastPasswordChangedDate"];

            DateTime lastLockedOutDate = new DateTime();
            if (doc["LastLockedOutDAte"] != null)
                lastLockedOutDate = (DateTime)doc["LastLockedOutDate"];

            MembershipUser u = new MembershipUser(this.Name,
                                                  username,
                                                  providerUserKey,
                                                  email,
                                                  passwordQuestion,
                                                  comment,
                                                  isApproved,
                                                  isLockedOut,
                                                  creationDate,
                                                  lastLoginDate,
                                                  lastActivityDate,
                                                  lastPasswordChangedDate,
                                                  lastLockedOutDate);

            return u;
        }


        //
        // WriteToEventLog
        //   A helper function that writes exception detail to the event log. Exceptions
        // are written to the event log as a security measure to avoid private database
        // details from being returned to the browser. If a method does not return a status
        // or boolean indicating the action succeeded or failed, a generic exception is also 
        // thrown by the caller.
        //

        private void WriteToEventLog(Exception e, string action)
        {
            EventLog log = new EventLog();
            log.Source = eventSource;
            log.Log = eventLog;

            string message = "An exception occurred communicating with the data source.\n\n";
            message += "Action: " + action + "\n\n";
            message += "Exception: " + e.ToString();

            log.WriteEntry(message);
        }

    }
}

