namespace ProjBobcat.Class.Model.Microsoft.Graph;

/// <summary>
/// A Microsoft identity response contains the requested model, an OAuth error, or no payload.
/// </summary>
public union GraphResponse<T>(T?, GraphResponseErrorModel) where T : class;
