using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ProjBobcat.Class.Model
{
    [JsonConverter(typeof(Converter))]
    public struct PlayerUUID : IFormattable, IComparable<PlayerUUID>, IEquatable<PlayerUUID>
    {
        Guid guid;
        public PlayerUUID(byte[] guidBytes)
        {
            this.guid = new Guid(guidBytes);
        }
        public PlayerUUID(Guid guid)
        {
            this.guid = guid;
        }
        public PlayerUUID(string guidString)
        {
            this.guid = new Guid(guidString);
        }
        public int CompareTo(PlayerUUID other)
        {
            return this.guid.CompareTo(other.guid);
        }

        public override bool Equals(object obj)
        {
            if (obj is PlayerUUID playerUUID)
                return guid.Equals(playerUUID.guid);
            return false;
        }

        public bool Equals(PlayerUUID other)
        {
            return this.guid.Equals(other.guid);
        }
        public override int GetHashCode()
            => guid.GetHashCode();

        public string ToString(string format, IFormatProvider formatProvider) =>
            this.guid.ToString(format, formatProvider);

        public static bool operator ==(PlayerUUID left, PlayerUUID right)
        {
            return left.guid == right.guid;
        }

        public static bool operator !=(PlayerUUID left, PlayerUUID right)
        {
            return left.guid != right.guid;
        }

        public Guid ToGuid()
        {
            return guid;
        }

        public static PlayerUUID FromOfflinePlayerName(string playerName, string prefix = "OfflinePlayer:")
        {
            using var md5 = MD5.Create();
            var data = md5.ComputeHash(Encoding.UTF8.GetBytes($"{prefix}{playerName}"));
            return new PlayerUUID(data);
        }

        public string ToString(string format = "N")
        {
            return guid.ToString(format);
        }

        public override string ToString()
        {
            return guid.ToString("N");
        }
        public class Converter : JsonConverter<PlayerUUID>
        {
            public override PlayerUUID ReadJson(JsonReader reader, Type objectType, 
                PlayerUUID existingValue, bool hasExistingValue, JsonSerializer serializer)
            {
                var s = serializer.Deserialize<string>(reader);
                return new PlayerUUID(s);
            }
            public override void WriteJson(JsonWriter writer, PlayerUUID value, JsonSerializer serializer)
            {
                serializer.Serialize(writer, value.ToString());
            }
        }
        public static PlayerUUID Random()
        {
            return new PlayerUUID(Guid.NewGuid());
        }
    }
}