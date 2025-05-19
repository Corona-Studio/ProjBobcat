using System;

namespace ProjBobcat.Exceptions;

public class CurseForgeFileResolveException(string? message) : Exception(message);