using System;

namespace ProjBobcat.Exceptions;

public class ChunkSplitExhaustException(Exception innerException) : Exception("ALL CHUNK SPLIT TRIAL USED", innerException);