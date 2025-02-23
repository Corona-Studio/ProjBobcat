using System;

namespace ProjBobcat.Exceptions;

public class DownloadChunkSplitException(bool split, Exception? innerEx = null) : Exception(string.Empty, innerEx)
{
    public bool Split => split;
}