using System;
using ProjBobcat.Class.Model;
using ProjBobcat.Event;

namespace ProjBobcat.Interface;

public interface IDownloadFile
{
    int PartialDownloadRetryCount { get; }

    /// <summary>
    ///     下载路径
    /// </summary>
    string DownloadPath { get; init; }

    /// <summary>
    ///     保存的文件名
    /// </summary>
    string FileName { get; init; }

    /// <summary>
    ///     最大重试计数
    /// </summary>
    int RetryCount { get; }

    /// <summary>
    ///     文件类型（仅在Lib/Asset补全时可用）
    /// </summary>
    ResourceType FileType { get; }

    /// <summary>
    ///     文件大小
    /// </summary>
    long FileSize { get; init; }

    /// <summary>
    ///     文件检验码
    /// </summary>
    string? CheckSum { get; init; }

    /// <summary>
    ///     下载Uri
    /// </summary>
    string GetDownloadUrl();

    /// <summary>
    ///     下载完成事件
    /// </summary>
    event EventHandler<DownloadFileCompletedEventArgs>? Completed;

    /// <summary>
    ///     下载改变事件
    /// </summary>
    event EventHandler<DownloadFileChangedEventArgs>? Changed;
}