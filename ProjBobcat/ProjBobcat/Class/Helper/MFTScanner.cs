using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using ProjBobcat.Class.Model;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace ProjBobcat.Class.Helper
{
    public class MFTScanner
    {
        private static IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);
        private const uint GENERIC_READ = 0x80000000;
        private const int FILE_SHARE_READ = 0x1;
        private const int FILE_SHARE_WRITE = 0x2;
        private const int OPEN_EXISTING = 3;
        private const int FILE_READ_ATTRIBUTES = 0x80;
        private const int FILE_NAME_IINFORMATION = 9;
        private const int FILE_FLAG_BACKUP_SEMANTICS = 0x2000000;
        private const int FILE_OPEN_FOR_BACKUP_INTENT = 0x4000;
        private const int FILE_OPEN_BY_FILE_ID = 0x2000;
        private const int FILE_OPEN = 0x1;
        private const int OBJ_CASE_INSENSITIVE = 0x40;
        private const int FSCTL_ENUM_USN_DATA = 0x900b3;

        [StructLayout(LayoutKind.Sequential)]
        private struct MFT_ENUM_DATA
        {
            public long StartFileReferenceNumber;
            public long LowUsn;
            public long HighUsn;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct USN_RECORD
        {
            public int RecordLength;
            public short MajorVersion;
            public short MinorVersion;
            public long FileReferenceNumber;
            public long ParentFileReferenceNumber;
            public long Usn;
            public long TimeStamp;
            public int Reason;
            public int SourceInfo;
            public int SecurityId;
            public FileAttributes FileAttributes;
            public short FileNameLength;
            public short FileNameOffset;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_STATUS_BLOCK
        {
            public int Status;
            public int Information;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct UNICODE_STRING
        {
            public short Length;
            public short MaximumLength;
            public IntPtr Buffer;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct OBJECT_ATTRIBUTES
        {
            public int Length;
            public IntPtr RootDirectory;
            public IntPtr ObjectName;
            public int Attributes;
            public int SecurityDescriptor;
            public int SecurityQualityOfService;
        }

        //// MFT_ENUM_DATA
        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool DeviceIoControl(IntPtr hDevice, int dwIoControlCode, ref MFT_ENUM_DATA lpInBuffer, int nInBufferSize, IntPtr lpOutBuffer, int nOutBufferSize, ref int lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess, int dwShareMode, IntPtr lpSecurityAttributes, int dwCreationDisposition, int dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Auto)]
        private static extern Int32 CloseHandle(IntPtr lpObject);

        [DllImport("ntdll.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int NtCreateFile(ref IntPtr FileHandle, int DesiredAccess, ref OBJECT_ATTRIBUTES ObjectAttributes, ref IO_STATUS_BLOCK IoStatusBlock, int AllocationSize, int FileAttribs, int SharedAccess, int CreationDisposition, int CreateOptions, int EaBuffer,
        int EaLength);

        [DllImport("ntdll.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int NtQueryInformationFile(IntPtr FileHandle, ref IO_STATUS_BLOCK IoStatusBlock, IntPtr FileInformation, int Length, int FileInformationClass);

        private IntPtr m_hCJ;
        private IntPtr m_Buffer;
        private int m_BufferSize;

        private string m_DriveLetter;

        private class FSNode
        {
            public long FRN;
            public long ParentFRN;
            public string FileName;

            public bool IsFile;
            public FSNode(long lFRN, long lParentFSN, string sFileName, bool bIsFile)
            {
                FRN = lFRN;
                ParentFRN = lParentFSN;
                FileName = sFileName;
                IsFile = bIsFile;
            }
        }

        private IntPtr OpenVolume(string szDriveLetter)
        {

            IntPtr hCJ = default(IntPtr);
            //// volume handle

            m_DriveLetter = szDriveLetter;
            hCJ = CreateFile(@"\\.\" + szDriveLetter, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);

            return hCJ;

        }


        private void Cleanup()
        {
            if (m_hCJ != IntPtr.Zero)
            {
                // Close the volume handle.
                CloseHandle(m_hCJ);
                m_hCJ = INVALID_HANDLE_VALUE;
            }

            if (m_Buffer != IntPtr.Zero)
            {
                // Free the allocated memory
                Marshal.FreeHGlobal(m_Buffer);
                m_Buffer = IntPtr.Zero;
            }

        }


        public IEnumerable<String> EnumerateFiles(string szDriveLetter)
        {
            try
            {
                var usnRecord = default(USN_RECORD);
                var mft = default(MFT_ENUM_DATA);
                var dwRetBytes = 0;
                var cb = 0;
                var dicFRNLookup = new Dictionary<long, FSNode>();
                var bIsFile = false;

                // This shouldn't be called more than once.
                if (m_Buffer.ToInt32() != 0)
                {
                    throw new Exception("invalid buffer");
                }

                // Assign buffer size
                m_BufferSize = 65536;
                //64KB

                // Allocate a buffer to use for reading records.
                m_Buffer = Marshal.AllocHGlobal(m_BufferSize);

                // correct path
                szDriveLetter = szDriveLetter.TrimEnd('\\');

                // Open the volume handle
                m_hCJ = OpenVolume(szDriveLetter);

                // Check if the volume handle is valid.
                if (m_hCJ == INVALID_HANDLE_VALUE)
                {
                    string errorMsg = "Couldn't open handle to the volume.";
                    if (!IsAdministrator())
                        errorMsg += "Current user is not administrator";

                    throw new Exception(errorMsg);
                }

                mft.StartFileReferenceNumber = 0;
                mft.LowUsn = 0;
                mft.HighUsn = long.MaxValue;

                do
                {
                    if (DeviceIoControl(m_hCJ, FSCTL_ENUM_USN_DATA, ref mft, Marshal.SizeOf(mft), m_Buffer, m_BufferSize, ref dwRetBytes, IntPtr.Zero))
                    {
                        cb = dwRetBytes;
                        // Pointer to the first record
                        IntPtr pUsnRecord = new IntPtr(m_Buffer.ToInt32() + 8);

                        while ((dwRetBytes > 8))
                        {
                            // Copy pointer to USN_RECORD structure.
                            usnRecord = (USN_RECORD)Marshal.PtrToStructure(pUsnRecord, usnRecord.GetType());

                            // The filename within the USN_RECORD.
                            string FileName = Marshal.PtrToStringUni(new IntPtr(pUsnRecord.ToInt32() + usnRecord.FileNameOffset), usnRecord.FileNameLength / 2);

                            bIsFile = !usnRecord.FileAttributes.HasFlag(FileAttributes.Directory);
                            dicFRNLookup.Add(usnRecord.FileReferenceNumber, new FSNode(usnRecord.FileReferenceNumber, usnRecord.ParentFileReferenceNumber, FileName, bIsFile));

                            // Pointer to the next record in the buffer.
                            pUsnRecord = new IntPtr(pUsnRecord.ToInt32() + usnRecord.RecordLength);

                            dwRetBytes -= usnRecord.RecordLength;
                        }

                        // The first 8 bytes is always the start of the next USN.
                        mft.StartFileReferenceNumber = Marshal.ReadInt64(m_Buffer, 0);


                    }
                    else
                    {
                        break; // TODO: might not be correct. Was : Exit Do

                    }

                } while (!(cb <= 8));

                // Resolve all paths for Files
                foreach (FSNode oFSNode in dicFRNLookup.Values.Where(o => o.IsFile))
                {
                    string sFullPath = oFSNode.FileName;
                    FSNode oParentFSNode = oFSNode;

                    while (dicFRNLookup.TryGetValue(oParentFSNode.ParentFRN, out oParentFSNode))
                    {
                        sFullPath = string.Concat(oParentFSNode.FileName, @"\", sFullPath);
                    }
                    sFullPath = string.Concat(szDriveLetter, @"\", sFullPath);

                    yield return sFullPath;
                }
            }
            finally
            {
                //// cleanup
                Cleanup();
            }
        }

        public static bool IsAdministrator()
        {
            WindowsIdentity identity = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
