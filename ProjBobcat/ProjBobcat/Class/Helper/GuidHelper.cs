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
    /// <param name="str">字符串。</param>
    extension(string str)
    {
        /// <summary>
        ///     根据一段字符串，计算其哈希值，并转换为一个对应的 <see cref="Guid" /> 。
        ///     相等的字符串将产生相等的 <see cref="Guid" /> 。
        /// </summary>
        /// <returns>生成结果。</returns>
        public Guid ToGuidHash()
        {
            var data = MD5.HashData(Encoding.UTF8.GetBytes(str));
            return new Guid(data);
        }

        /// <summary>
        ///     根据离线玩家名，按一定方式处理并计算哈希值，转换为一个对应的 <see cref="Guid" /> 。
        ///     相等的玩家名将产生相等的 <see cref="Guid" /> 。
        ///     满足 Bukkit 离线玩家 UUID 生成规则。
        ///     <see href="https://github.com/MCLF-CN/docs/issues/7" />
        /// </summary>
        /// <returns>生成结果。</returns>
        public Guid ToGuidHashAsName()
        {
            var hash = MD5.HashData(Encoding.UTF8.GetBytes("OfflinePlayer:" + str));

            hash[6] = (byte)((hash[6] & 0x0f) | 0x30);
            hash[8] = (byte)((hash[8] & 0x3f) | 0x80);

            return Guid.Parse(new Uuid(hash).ToString());
        }
    }

    /// <summary>
    ///     Rfc4122 格式的 Guid 字符串的最小实现结构体，引自 <see href="https://github.com/vanbukin/Uuids" />
    ///     <para>
    ///         <seealso href="https://github.com/vanbukin/Uuids/blob/main/src/Uuids/Uuid.cs" />
    ///     </para>
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct Uuid
    {
        private const ushort MaximalChar = 103;

        private static readonly uint* TableToHex;
        private static readonly byte* TableFromHexToBytes;

        static Uuid()
        {
            TableToHex = (uint*)Marshal.AllocHGlobal(sizeof(uint) * 256).ToPointer();
            for (var i = 0; i < 256; i++)
            {
                var chars = Convert.ToString(i, 16).PadLeft(2, '0');
                TableToHex[i] = ((uint)chars[1] << 16) | chars[0];
            }

            TableFromHexToBytes = (byte*)Marshal.AllocHGlobal(103).ToPointer();
            for (var i = 0; i < 103; i++)
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
        ///     Initializes a new instance of the <see cref="Uuid" /> structure by using the specified array of bytes.
        /// </summary>
        /// <param name="bytes">A 16-element byte array containing values with which to initialize the <see cref="Uuid" />.</param>
        /// <exception cref="ArgumentNullException"><paramref name="bytes" /> is <see langword="null" />.</exception>
        /// <exception cref="ArgumentException"><paramref name="bytes" /> is not 16 bytes long.</exception>
        public Uuid(byte[] bytes)
        {
            ArgumentNullException.ThrowIfNull(bytes);
            if (bytes.Length != 16)
                throw new ArgumentException("Byte array for Uuid must be exactly 16 bytes long.", nameof(bytes));

            this = Unsafe.ReadUnaligned<Uuid>(ref MemoryMarshal.GetReference(bytes.AsSpan()));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private void FormatN(char* dest)
        {
            if (Avx2.IsSupported)
            {
                fixed (Uuid* thisPtr = &this)
                {
                    var uuidVector = Avx2.ConvertToVector256Int16(Sse3.LoadDquVector128((byte*)thisPtr));
                    var hi = Avx2.ShiftRightLogical(uuidVector, 4).AsByte();
                    var lo = Avx2.Shuffle(uuidVector.AsByte(),
                        Vector256.Create(
                            255, 0, 255, 2, 255, 4, 255, 6, 255, 8, 255, 10, 255, 12, 255, 14,
                            255, 0, 255, 2, 255, 4, 255, 6, 255, 8, 255, 10, 255, 12, 255, 14));
                    var asciiBytes = Avx2.Shuffle(
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
                var destUints = (uint*)dest;
                destUints[0] = TableToHex[this._byte0];
                destUints[1] = TableToHex[this._byte1];
                destUints[2] = TableToHex[this._byte2];
                destUints[3] = TableToHex[this._byte3];
                destUints[4] = TableToHex[this._byte4];
                destUints[5] = TableToHex[this._byte5];
                destUints[6] = TableToHex[this._byte6];
                destUints[7] = TableToHex[this._byte7];
                destUints[8] = TableToHex[this._byte8];
                destUints[9] = TableToHex[this._byte9];
                destUints[10] = TableToHex[this._byte10];
                destUints[11] = TableToHex[this._byte11];
                destUints[12] = TableToHex[this._byte12];
                destUints[13] = TableToHex[this._byte13];
                destUints[14] = TableToHex[this._byte14];
                destUints[15] = TableToHex[this._byte15];
            }
        }

        /// <summary>
        ///     仅摘取了原实现中的 FormatN 方法。
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            string uuidString = new('\0', 32);
            fixed (char* uuidChars = &uuidString.GetPinnableReference())
            {
                this.FormatN(uuidChars);
            }

            return uuidString;
        }
    }
}