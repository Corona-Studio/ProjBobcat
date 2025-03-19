using System.Net.Http;

namespace ProjBobcat.Interface;

public interface IInstaller
{
    string? CustomId { get; init; }
    string RootPath { get; init; }
    string? InheritsFrom { get; init; }
    IHttpClientFactory HttpClientFactory { get; init; }
}