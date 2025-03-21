using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography;
using System.Text;

namespace ProjBobcat.Class.Helper;

/// <summary>
///     Guid 帮助器。
/// </summary>
public static class GuidHelper
{
    /// <summary>
    ///     根据一段字符串，计算其哈希值，并转换为一个对应的 <see cref="Guid" /> 。
    ///     相等的字符串将产生相等的 <see cref="Guid" /> 。
    /// </summary>
    /// <param name="str">字符串。</param>
    /// <returns>生成结果。</returns>
    public static Guid ToGuidHash(this string str)
    {
        var data = MD5.HashData(Encoding.UTF8.GetBytes(str));
        return new Guid(data);
    }

    /// <summary>
    ///     根据离线玩家名，按一定方式处理并计算哈希值，转换为一个对应的 <see cref="Guid" /> 。
    ///     相等的玩家名将产生相等的 <see cref="Guid" /> 。
    /// </summary>
    /// <param name="username">玩家名。</param>
    /// <returns>生成结果。</returns>
    public static Guid ToGuidHashAsName(this string username)
    {
        byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes("OfflinePlayer:" + username));

        hash[6] = (byte)((hash[6] & 0x0f) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3f) | 0x80);

        return Guid.Parse(new Uuid(hash).ToString());
    }

    /// <summary>
    ///     Rfc4122 格式的 Guid 字符串的最小实现结构体，引自 https://github.com/vanbukin/Uuids
    /// </summary>
    /// <seealso cref="https://github.com/vanbukin/Uuids/blob/main/src/Uuids/Uuid.cs"/>
    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct Uuid
    {
        private const ushort MaximalChar = 103;

        private static readonly uint* TableToHex;
        private static readonly byte* TableFromHexToBytes;

        static Uuid()
        {
            TableToHex = (uint*)Marshal.AllocHGlobal(sizeof(uint) * 256).ToPointer();
            for (int i = 0; i < 256; i++)
            {
                string chars = Convert.ToString(i, 16).PadLeft(2, '0');
                TableToHex[i] = ((uint)chars[1] << 16) | chars[0];
            }

            TableFromHexToBytes = (byte*)Marshal.AllocHGlobal(103).ToPointer();
            for (int i = 0; i < 103; i++)
            {
                TableFromHexToBytes[i] = (char)i switch
                {
                    '0' => 0x0,
                    '1' => 0x1,
                    '2' => 0x2,
                    '3' => 0x3,
                    '4' => 0x4,
                    '5' => 0x5,
                    '6' => 0x6,
                    '7' => 0x7,
                    '8' => 0x8,
                    '9' => 0x9,
                    'a' => 0xa,
                    'A' => 0xa,
                    'b' => 0xb,
                    'B' => 0xb,
                    'c' => 0xc,
                    'C' => 0xc,
                    'd' => 0xd,
                    'D' => 0xd,
                    'e' => 0xe,
                    'E' => 0xe,
                    'f' => 0xf,
                    'F' => 0xf,
                    _ => byte.MaxValue
                };
            }
        }

        private readonly byte _byte0;
        private readonly byte _byte1;
        private readonly byte _byte2;
        private readonly byte _byte3;
        private readonly byte _byte4;
        private readonly byte _byte5;
        private readonly byte _byte6;
        private readonly byte _byte7;
        private readonly byte _byte8;
        private readonly byte _byte9;
        private readonly byte _byte10;
        private readonly byte _byte11;
        private readonly byte _byte12;
        private readonly byte _byte13;
        private readonly byte _byte14;
        private readonly byte _byte15;

        /// <summary>
        /// Initializes a new instance of the <see cref="Uuid" /> structure by using the specified array of bytes.
        /// </summary>
        /// <param name="bytes">A 16-element byte array containing values with which to initialize the <see cref="Uuid" />.</param>
        /// <exception cref="ArgumentNullException"><paramref name="bytes" /> is <see langword="null" />.</exception>
        /// <exception cref="ArgumentException"><paramref name="bytes" /> is not 16 bytes long.</exception>
        public Uuid(byte[] bytes)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            if (bytes.Length != 16)
            {
                throw new ArgumentException("Byte array for Uuid must be exactly 16 bytes long.", nameof(bytes));
            }

            this = Unsafe.ReadUnaligned<Uuid>(ref MemoryMarshal.GetReference(bytes.AsSpan()));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private void FormatN(char* dest)
        {
            // dddddddddddddddddddddddddddddddd
            if (Avx2.IsSupported)
            {
                fixed (Uuid* thisPtr = &this)
                {
                    Vector256<short> uuidVector = Avx2.ConvertToVector256Int16(Sse3.LoadDquVector128((byte*)thisPtr));
                    Vector256<byte> hi = Avx2.ShiftRightLogical(uuidVector, 4).AsByte();
                    Vector256<byte> lo = Avx2.Shuffle(uuidVector.AsByte(),
                        Vector256.Create(
                            255, 0, 255, 2, 255, 4, 255, 6, 255, 8, 255, 10, 255, 12, 255, 14,
                            255, 0, 255, 2, 255, 4, 255, 6, 255, 8, 255, 10, 255, 12, 255, 14));
                    Vector256<byte> asciiBytes = Avx2.Shuffle(
                        Vector256.Create(
                            (byte)48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 97, 98, 99, 100, 101, 102,
                            48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 97, 98, 99, 100, 101, 102),
                        Avx2.And(Avx2.Or(hi, lo), Vector256.Create((byte)0x0F)));
                    Avx.Store((short*)dest, Avx2.ConvertToVector256Int16(asciiBytes.GetLower()));
                    Avx.Store((short*)dest + 16, Avx2.ConvertToVector256Int16(asciiBytes.GetUpper()));
                }
            }
            else
            {
                uint* destUints = (uint*)dest;
                destUints[0] = TableToHex[_byte0];
                destUints[1] = TableToHex[_byte1];
                destUints[2] = TableToHex[_byte2];
                destUints[3] = TableToHex[_byte3];
                destUints[4] = TableToHex[_byte4];
                destUints[5] = TableToHex[_byte5];
                destUints[6] = TableToHex[_byte6];
                destUints[7] = TableToHex[_byte7];
                destUints[8] = TableToHex[_byte8];
                destUints[9] = TableToHex[_byte9];
                destUints[10] = TableToHex[_byte10];
                destUints[11] = TableToHex[_byte11];
                destUints[12] = TableToHex[_byte12];
                destUints[13] = TableToHex[_byte13];
                destUints[14] = TableToHex[_byte14];
                destUints[15] = TableToHex[_byte15];
            }
        }

        public override string ToString()
        {
            string uuidString = new('\0', 32);
            fixed (char* uuidChars = &uuidString.GetPinnableReference())
                FormatN(uuidChars);

            return uuidString;
        }
    }
}