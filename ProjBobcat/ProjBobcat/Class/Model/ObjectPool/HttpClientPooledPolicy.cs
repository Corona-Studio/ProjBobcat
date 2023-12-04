using System.Net.Http;
using Microsoft.Extensions.ObjectPool;
using ProjBobcat.Class.Helper;

namespace ProjBobcat.Class.Model.ObjectPool;

public class HttpClientPooledPolicy : PooledObjectPolicy<HttpClient>
{
    public override HttpClient Create()
    {
        return HttpClientHelper.CreateInstance();
    }

    public override bool Return(HttpClient obj) => true;
}