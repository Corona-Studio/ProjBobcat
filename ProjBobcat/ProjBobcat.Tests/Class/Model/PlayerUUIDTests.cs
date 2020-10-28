using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace ProjBobcat.Class.Model.Tests
{
    [TestClass]
    public class PlayerUUIDTests
    {
        [TestMethod]
        public void SerializeTest()
        {
            var o = PlayerUUID.Random();
            var s = JsonConvert.SerializeObject(o);
            var d = JsonConvert.DeserializeObject<string>(s);
            var d2 = JsonConvert.DeserializeObject<PlayerUUID>(s);

            Assert.AreEqual(o, new PlayerUUID(d));
            Assert.AreEqual(o, d2);
        }

        [TestMethod]
        public void SerializeAsDictionaryKeyTest()
        {
            var pairs = new Dictionary<PlayerUUID, string>
            {
                {PlayerUUID.Random(), "abc"},
                {PlayerUUID.Random(), "def"}
            };
            var s = JsonConvert.SerializeObject(pairs);

            var d = JsonConvert.DeserializeObject<Dictionary<PlayerUUID, string>>(s);

            Assert.AreEqual(pairs.Count, d.Count);

            foreach (var p in pairs) Assert.AreEqual(p.Value, d[p.Key]);
        }
    }
}