using System;

namespace ProjBobcat.Class.Helper;

/// <summary>
///     加密助手
/// </summary>
public static class CryptoHelper
{
    public static string BytesToString(this byte[] bytes)
    {
        return BitConverter.ToString(bytes).Replace("-", string.Empty);
    }
}