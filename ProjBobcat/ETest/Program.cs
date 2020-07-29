using ProjBobcat.Class.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ETest
{
    class Program
    {
        static void Main(string[] args)
        {
            Dictionary<PlayerUUID, string> strs = new Dictionary<PlayerUUID, string>();
            strs.Add(PlayerUUID.Random(), "abc");
            var result = Newtonsoft.Json.JsonConvert.SerializeObject(strs);
            var r2 = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<PlayerUUID, string>>(result);
        }
    }
}
