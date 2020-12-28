using System.Linq;
using System.Management;
using System.Threading;

namespace ProjBobcat.Class.Helper
{
    /// <summary>
    ///     处理器工具类
    /// </summary>
    public static class ProcessorHelper
    {
        /// <summary>
        ///     获取物理处理器核心数量
        /// </summary>
        /// <returns></returns>
        public static int GetPhysicalProcessorCount()
        {
            using var managedObject = new ManagementObjectSearcher("Select NumberOfCores from Win32_Processor");
            var processorCoreCount = managedObject.Get().Cast<ManagementBaseObject>().Sum(item =>
                int.TryParse(item["NumberOfCores"].ToString(), out var num) ? num : 1);
            return processorCoreCount;
        }

        /// <summary>
        /// 自动设置最大线程数
        /// </summary>
        /// <returns></returns>
        public static bool SetMaxThreads()
        {
            ThreadPool.GetMaxThreads(out var maxWorkerThreads,
                out var maxConcurrentActiveRequests);

            var changeSucceeded = ThreadPool.SetMaxThreads(
                maxWorkerThreads, maxConcurrentActiveRequests);

            return changeSucceeded;
        }
    }
}