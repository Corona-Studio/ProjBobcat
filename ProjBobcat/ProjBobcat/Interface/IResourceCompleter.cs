using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProjBobcat.Class.Model;
using ProjBobcat.Event;

namespace ProjBobcat.Interface;

public interface IResourceCompleter : IDisposable
{
    /// <summary>
    ///     重试最大计数
    /// </summary>
    int TotalRetry { get; set; }

    TimeSpan TimeoutPerFile { get; }

    /// <summary>
    ///     下载分片数量
    /// </summary>
    int DownloadParts { get; set; }

    /// <summary>
    ///     最大并行下载数量
    /// </summary>
    int DownloadThread { get; set; }

    int MaxDegreeOfParallelism { get; set; }

    /// <summary>
    ///     验证下载文件
    /// </summary>
    bool CheckFile { get; set; }

    /// <summary>
    ///     游戏资源解析器集合
    /// </summary>
    IReadOnlyList<IResourceInfoResolver>? ResourceInfoResolvers { get; set; }

    /// <summary>
    ///     检查并下载（同步）
    /// </summary>
    /// <returns>任务是否成功完成</returns>
    TaskResult<ResourceCompleterCheckResult?> CheckAndDownload();

    /// <summary>
    ///     检查并下载（异步）
    /// </summary>
    /// <returns>任务是否成功完成</returns>
    Task<TaskResult<ResourceCompleterCheckResult?>> CheckAndDownloadTaskAsync();

    /// <summary>
    ///     解析状态事件
    /// </summary>
    event EventHandler<GameResourceInfoResolveEventArgs> GameResourceInfoResolveStatus;

    /// <summary>
    ///     下载进度变化事件
    /// </summary>
    event EventHandler<DownloadFileChangedEventArgs> DownloadFileChangedEvent;

    /// <summary>
    ///     文件下载完成事件
    /// </summary>
    event EventHandler<DownloadFileCompletedEventArgs> DownloadFileCompletedEvent;
}