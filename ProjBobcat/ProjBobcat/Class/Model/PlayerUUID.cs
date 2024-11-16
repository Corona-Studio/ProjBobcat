using System;
using System.ComponentModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProjBobcat.Class.Model;

public class PlayerUUIDJsonConverter : JsonConverter<PlayerUUID>
{
    public override PlayerUUID Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return new PlayerUUID(reader.GetString() ?? Guid.Empty.ToString());
    }

    public override void Write(Utf8JsonWriter writer, PlayerUUID value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToGuid());
    }
}

class PlayerUUIDTypeConverter : TypeConverter
{
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
    {
        return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
    }

    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
    {
        if (value is string str) return new PlayerUUID(str);
        return base.ConvertFrom(context, culture, value);
    }
}

[JsonConverter(typeof(PlayerUUIDJsonConverter))]
[TypeConverter(typeof(PlayerUUIDTypeConverter))]
public readonly struct PlayerUUID : IFormattable, IComparable<PlayerUUID>, IEquatable<PlayerUUID>
{
    readonly Guid _guid;

    public PlayerUUID(byte[] guidBytes)
    {
        this._guid = new Guid(guidBytes);
    }

    public PlayerUUID(Guid guid)
    {
        this._guid = guid;
    }

    public PlayerUUID(string guidString)
    {
        this._guid = new Guid(guidString);
    }

    public int CompareTo(PlayerUUID other)
    {
        return this._guid.CompareTo(other._guid);
    }

    public override bool Equals(object? obj)
    {
        if (obj is PlayerUUID playerUuid)
            return this._guid.Equals(playerUuid._guid);
        return false;
    }

    public bool Equals(PlayerUUID other)
    {
        return this._guid.Equals(other._guid);
    }

    public override int GetHashCode()
    {
        return this._guid.GetHashCode();
    }

    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        return this._guid.ToString(format, formatProvider);
    }

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
        return this._guid;
    }

    public static PlayerUUID FromOfflinePlayerName(string playerName, string prefix = "OfflinePlayer:")
    {
        var data = MD5.HashData(Encoding.UTF8.GetBytes($"{prefix}{playerName}"));
        return new PlayerUUID(data);
    }

    public string ToString(string format = "N")
    {
        return this._guid.ToString(format);
    }

    public override string ToString()
    {
        return this._guid.ToString("N");
    }

    public static PlayerUUID Random()
    {
        return new PlayerUUID(Guid.NewGuid());
    }
}