using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjBobcat.Class.Helper
{
    public static class RandomHelper
    {
        private static readonly Random Random = new Random();

        public static T RandomSample<T>(this IEnumerable<T> enumerable)
        {
            var arr = enumerable.ToArray();
            if (arr.Length == 0)
                return default;
            return arr[Random.Next(0, arr.Length - 1)];
        }
    }
}