using System.Linq;
using System.Management;

namespace ProjBobcat.Class.Helper
{
    public static class ProcessorHelper
    {
        public static int GetPhysicalProcessorCount()
        {
            using var managedObject = new ManagementObjectSearcher("Select NumberOfCores from Win32_Processor");
            var processorCoreCount = managedObject.Get().Cast<ManagementBaseObject>().Sum(item =>
                int.TryParse(item["NumberOfCores"].ToString(), out var num) ? num : 1);
            return processorCoreCount;
        }
    }
}