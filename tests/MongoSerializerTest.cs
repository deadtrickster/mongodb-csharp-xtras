using Microsoft.VisualStudio.TestTools.UnitTesting;
using MongoDB.Driver;
using System.Collections;
using System.Linq;
using MongoDB.Xtras;
namespace MongoSerializerTest
{
    /// <summary>
    ///This is a test class for MongoSerializerTest and is intended
    ///to contain all MongoSerializerTest Unit Tests
    ///</summary>
    [TestClass()]
    public class MongoSerializerTest
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
        ///A test for Serialize
        ///</summary>
        [TestMethod()]
        public void SerializeSimpleObjectTest()
        {
            object obj = 1;
            Document d = obj.Serialize();

            Assert.AreEqual(1, d.Count);
            Assert.AreEqual(1, d[""]);
        }

        [TestMethod()]
        public void SerializeCollectionTest()
        {
            Room[] rooms = new Room[]
            {
                new Room{
                    Length = 2,
                    Width = 2
                },
                new Room{
                    Length = 3,
                    Width = 3
                }
            };

            Document d = rooms.Serialize();

            Assert.AreEqual(1, d.Count);
            Assert.IsInstanceOfType(d[""], typeof(IEnumerable));
            Assert.AreEqual(2, (d[""] as IEnumerable).Cast<Document>().Count());
            Assert.AreEqual(3, (d[""] as IEnumerable).Cast<Document>().ElementAt(1)["Width"]);
        }

        [TestMethod()]
        public void SerializeComplexObjectTest()
        {
            House house = new House("Springfield", new Room[]
            {
                new Room{
                    Length = 2,
                    Width = 2
                },
                new Room{
                    Length = 3,
                    Width = 3
                }
            });

            Document d = house.Serialize();

            Assert.AreEqual(2, d.Count);
            Assert.IsInstanceOfType(d["Rooms"], typeof(IEnumerable));

            Assert.AreEqual(2, (d["Rooms"] as IEnumerable).Cast<Document>().Count());
            Assert.AreEqual(3, (d["Rooms"] as IEnumerable).Cast<Document>().ElementAt(1)["Width"]);
        }
    }

    public class House
    {

        public House(string address, Room[] rooms)
        {
            Address = address;
            Rooms = rooms;
        }

        public string Address
        {
            get;
            set;
        }

        public Room[] Rooms
        {
            get;
            set;
        }
    }

    public class Room
    {
        public int Length
        {
            get;
            set;
        }

        public int Width
        {
            get;
            set;
        }
    }
}
