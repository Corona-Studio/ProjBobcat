using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjBobcat.Class.Helper
{
    /// <summary>
    ///     随机数帮助器。
    /// </summary>
    public static class RandomHelper
    {
        private static readonly Random Random = new Random();

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
            if (arr.Length == 0)
                return default;
            return arr[Random.Next(0, arr.Length - 1)];
        }
    }
}