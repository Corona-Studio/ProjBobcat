﻿using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProjBobcat.Class.Helper;

/// <summary>
///     文件操作帮助器。
/// </summary>
public static class FileHelper
{
    public static string GetValidPathName(string input)
    {
        return Path.GetInvalidFileNameChars().Aggregate(input, (current, c) => current.Replace(c, '_'));
    }

    public static async Task<FileStream?> OpenReadAsync(string path, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
            try
            {
                return File.OpenRead(path);
            }
            catch (IOException)
            {
                await Task.Delay(1000, token);
            }

        return null;
    }

    public static bool DeleteFileWithRetry(string filePath, int retryCount = 3)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(retryCount, 0);

        for (var i = 0; i < retryCount; i++)
            try
            {
                File.Delete(filePath);
                return true;
            }
            catch
            {
                // ignored
            }

        return false;
    }

    public static bool MoveFileWithRetry(string oldPath, string newPath, int retryCount = 3)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(retryCount, 0);

        for (var i = 0; i < retryCount; i++)
            try
            {
                File.Move(oldPath, newPath);
                return true;
            }
            catch
            {
                // ignored
            }

        return false;
    }
}