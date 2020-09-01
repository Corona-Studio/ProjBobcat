using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;

namespace ProjBobcat.Class.Model
{
    [JsonConverter(typeof(JsonConverter))]
    [TypeConverter(typeof(TypeConverter))]
    public struct PlayerUUID : IFormattable, IComparable<PlayerUUID>, IEquatable<PlayerUUID>
    {
        private readonly Guid _guid;

        public PlayerUUID(byte[] guidBytes)
        {
            _guid = new Guid(guidBytes);
        }

        public PlayerUUID(Guid guid)
        {
            _guid = guid;
        }

        public PlayerUUID(string guidString)
        {
            _guid = new Guid(guidString);
        }

        public int CompareTo(PlayerUUID other)
        {
            return _guid.CompareTo(other._guid);
        }

        public override bool Equals(object obj)
        {
            if (obj is PlayerUUID playerUuid)
                return _guid.Equals(playerUuid._guid);
            return false;
        }

        public bool Equals(PlayerUUID other)
        {
            return _guid.Equals(other._guid);
        }

        public override int GetHashCode()
            => _guid.GetHashCode();

        public string ToString(string format, IFormatProvider formatProvider) =>
            _guid.ToString(format, formatProvider);

        public static bool operator ==(PlayerUUID left, PlayerUUID right)
        {
            return left._guid == right._guid;
        }

        public static bool operator !=(PlayerUUID left, PlayerUUID right)
        {
            return left._guid != right._guid;
        }

        public Guid ToGuid()
        {
            return _guid;
        }

        public static PlayerUUID FromOfflinePlayerName(string playerName, string prefix = "OfflinePlayer:")
        {
            using var md5 = MD5.Create();
            var data = md5.ComputeHash(Encoding.UTF8.GetBytes($"{prefix}{playerName}"));
            return new PlayerUUID(data);
        }

        public string ToString(string format = "N")
        {
            return _guid.ToString(format);
        }

        public override string ToString()
        {
            return _guid.ToString("N");
        }

         class TypeConverter : System.ComponentModel.TypeConverter
        {
            public override bool CanConvertFrom(ITypeDescriptorContext context, System.Type sourceType)
            {
                if (sourceType == typeof(string))
                {
                    return true;
                }
                return base.CanConvertFrom(context, sourceType);
            }
            public override object ConvertFrom(ITypeDescriptorContext context, System.Globalization.CultureInfo culture, object value)
            {
                if (value is string str)
                {
                    return new PlayerUUID(str);
                }
                return base.ConvertFrom(context, culture, value);
            }
        }
        class JsonConverter : JsonConverter<PlayerUUID>
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