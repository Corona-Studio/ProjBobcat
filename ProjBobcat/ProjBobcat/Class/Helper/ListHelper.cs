using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProjBobcat.Class.Helper;

public static class ListHelper
{
    extension<T>(IAsyncEnumerable<T> asyncEnumerable)
    {
        public async Task<List<T>> ToListAsync()
        {
            var result = new List<T>();

            await foreach (var element in asyncEnumerable)
                result.Add(element);

            return result;
        }
    }
}