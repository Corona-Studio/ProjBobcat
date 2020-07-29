using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using ProjBobcat.Class.Model;

namespace ProjBobcat.Tests
{
    [TestClass]
    public class PlayerUuidTests
    {
        [TestMethod]
        public void JsonSTest()
        {
            var strs = new Dictionary<PlayerUUID, string> {{PlayerUUID.Random(), "abc"}};
            var result = JsonConvert.SerializeObject(strs);
            var r2 = JsonConvert.DeserializeObject<Dictionary<PlayerUUID, string>>(result);
            var result2 = JsonConvert.SerializeObject(r2);

            Assert.IsTrue(result == result2);
        }
    }
}