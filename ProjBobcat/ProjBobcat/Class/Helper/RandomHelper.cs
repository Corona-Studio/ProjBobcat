using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace ProjBobcat.Class.Helper;

/// <summary>
///     随机数帮助器。
/// </summary>
public static class RandomHelper
{
    /*
    [ThreadStatic] private static RandomNumberGenerator _random;

    private static RandomNumberGenerator Random
    {
        get { return _random ??= new RNGCryptoServiceProvider(); }
    }
    */

    /// <summary>
    ///     在一个有限的 <see cref="IEnumerable{T}" /> 中等概率地随机取出一项返回。
    ///     若其中元素数为 0 ，则会返回此类型的默认值；
    ///     若其中有无限的元素，程序可能会长时间在此停留，直至出现异常，这个异常是不可预测的，经过测试及相关分析已知有可能是 <see cref="IndexOutOfRangeException" /> 和
    ///     <see cref="OutOfMemoryException" /> 。
    /// </summary>
    /// <typeparam name="T">数据的类型。</typeparam>
    /// <param name="enumerable">一个有限的 <see cref="IEnumerable{T}" /> 。</param>
    /// <returns> <paramref name="enumerable" /> 中的随机一项。</returns>
    /// <exception cref="ArgumentNullException"></exception>
    public static T RandomSample<T>(this IEnumerable<T> enumerable)
    {
        var arr = enumerable.ToArray();
        return arr.Length == 0 ? default : arr[RandomInteger(0, arr.Length - 1)];
    }

    /// <summary>
    ///     生成一个完全随机数
    /// </summary>
    /// <param name="min"></param>
    /// <param name="max"></param>
    /// <returns></returns>
    public static int RandomInteger(int min, int max)
    {
        return RandomNumberGenerator.GetInt32(min, max + 1);
        /*
        var scale = uint.MaxValue;
        while (scale == uint.MaxValue)
        {
            var fourBytes = new byte[4];
            Random.GetBytes(fourBytes);

            scale = BitConverter.ToUInt32(fourBytes, 0);
        }

        return (int) (min + (max - min) *
            (scale / (double) uint.MaxValue));
        */
    }

    /// <summary>
    ///     随机打乱集合当中的元素
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    public static void Shuffle<T>(this IList<T> list)
    {
        if (!(list?.Any() ?? false)) return;

        var n = list.Count;
        while (n > 1)
        {
            n--;
            var k = RandomInteger(0, n);
            (list[k], list[n]) = (list[n], list[k]);
        }
    }
}