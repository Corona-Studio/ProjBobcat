using System;
using System.Collections.Generic;
using System.Linq;

namespace ProjBobcat.Class.Helper
{
    public static class RandomHelper
    {
        private static readonly Random Random = new Random();

        public static T RandomSample<T>(this IEnumerable<T> collection)
        {
            var enumerable = collection.ToList();
            if (!enumerable.Any()) return default;
            return enumerable.Count == 1 ? enumerable[0] : enumerable[Random.Next(0, enumerable.Count - 1)];
        }
    }
}