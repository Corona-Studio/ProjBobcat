using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProjBobcat.Class.Helper;

public static class ListHelper
{
    public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> asyncEnumerable)
    {
        var result = new List<T>();

        await foreach (var element in asyncEnumerable)
            result.Add(element);

        return result;
    }
}