using System;

namespace ProjBobcat.Exceptions;

public class CurseForgeAddonResolveException(string? message) : Exception(message);