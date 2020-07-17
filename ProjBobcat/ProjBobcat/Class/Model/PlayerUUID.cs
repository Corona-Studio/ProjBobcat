using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ProjBobcat.Class.Model
{
    public struct PlayerUUID : IFormattable, IComparable<PlayerUUID>, IEquatable<PlayerUUID>
    {
        Guid guid;
        public PlayerUUID(Guid guid)
        {
            this.guid = guid;
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
            return new PlayerUUID(new Guid(data));
        }

        public string ToString(string format)
        {
            return guid.ToString(format);
        }
    }
}