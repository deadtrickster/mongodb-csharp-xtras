using MongoDB.Xtras;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Driver;
using System;

namespace Tests
{
    
    
    /// <summary>
    ///This is a test class for DocumentExtensionsTest and is intended
    ///to contain all DocumentExtensionsTest Unit Tests
    ///</summary>
    [TestClass()]
    public class DocumentExtensionsTest
    {


        private TestContext testContextInstance;

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext
        {
            get
            {
                return testContextInstance;
            }
            set
            {
                testContextInstance = value;
            }
        }

        #region Additional test attributes
        // 
        //You can use the following additional attributes as you write your tests:
        //
        //Use ClassInitialize to run code before running the first test in the class
        //[ClassInitialize()]
        //public static void MyClassInitialize(TestContext testContext)
        //{
        //}
        //
        //Use ClassCleanup to run code after all tests in a class have run
        //[ClassCleanup()]
        //public static void MyClassCleanup()
        //{
        //}
        //
        //Use TestInitialize to run code before running each test
        //[TestInitialize()]
        //public void MyTestInitialize()
        //{
        //}
        //
        //Use TestCleanup to run code after each test has run
        //[TestCleanup()]
        //public void MyTestCleanup()
        //{
        //}
        //
        #endregion


        /// <summary>
        ///A test for Fill
        ///</summary>
        [TestMethod()]
        public void FillTest()
        {
            Document doc = new Document();
            doc.Fill(
                Width => 100,
                Height => 200,
                Attributes => new Pairs{ attr1 => 1, 
                                         attr2 => 2 });

            Assert.AreEqual(100, (int)doc["Width"]);
            Assert.AreEqual(200, (int)doc["Height"]);
            Assert.IsInstanceOfType(doc["Attributes"], typeof(Document));

        }
    }
}
