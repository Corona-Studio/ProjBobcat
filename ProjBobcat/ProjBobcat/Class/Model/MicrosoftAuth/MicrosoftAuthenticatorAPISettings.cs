namespace ProjBobcat.Class.Model.MicrosoftAuth;

public class MicrosoftAuthenticatorAPISettings
{
    public required string ClientId { get; init; }
    public required string TenentId { get; init; }
    public required string[] Scopes { get; init; }
}