using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace FileRecord.Utils
{
    public class UsnJournalService
    {
        // Win32 API 定义
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool DeviceIoControl(
            IntPtr hDevice,
            uint dwIoControlCode,
            IntPtr lpInBuffer,
            int nInBufferSize,
            IntPtr lpOutBuffer,
            int nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;
        private const uint OPEN_EXISTING = 3;
        private const uint FSCTL_QUERY_USN_JOURNAL = 0x000900f4;
        private const uint FSCTL_READ_USN_JOURNAL = 0x000900bb;

        [StructLayout(LayoutKind.Sequential)]
        public struct USN_JOURNAL_DATA
        {
            public ulong UsnJournalID;
            public long FirstUsn;
            public long NextUsn;
            public long LowestValidUsn;
            public long MaxUsn;
            public ulong MaximumSize;
            public ulong AllocationDelta;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct READ_USN_JOURNAL_DATA
        {
            public long StartUsn;
            public uint ReasonMask;
            public uint ReturnOnlyOnClose;
            public ulong Timeout;
            public ulong BytesToWaitFor;
            public ulong UsnJournalID;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct USN_RECORD_V2
        {
            public uint RecordLength;
            public ushort MajorVersion;
            public ushort MinorVersion;
            public ulong FileReferenceNumber;
            public ulong ParentFileReferenceNumber;
            public long Usn;
            public long TimeStamp;
            public uint Reason;
            public uint SourceInfo;
            public uint SecurityId;
            public uint FileAttributes;
            public ushort FileNameLength;
            public ushort FileNameOffset;
        }

        /// <summary>
        /// 检查驱动器是否支持 NTFS USN 日志
        /// </summary>
        public bool IsNtfsUsnSupported(string driveLetter)
        {
            try
            {
                DriveInfo drive = new DriveInfo(driveLetter);
                if (drive.DriveFormat != "NTFS") return false;

                string volName = $@"\\.\{driveLetter.TrimEnd('\\')}";
                IntPtr hVol = CreateFile(volName, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);

                if (hVol == new IntPtr(-1)) return false;

                int size = Marshal.SizeOf(typeof(USN_JOURNAL_DATA));
                IntPtr outBuffer = Marshal.AllocHGlobal(size);
                uint bytesReturned;
                
                bool success = DeviceIoControl(hVol, FSCTL_QUERY_USN_JOURNAL, IntPtr.Zero, 0, outBuffer, size, out bytesReturned, IntPtr.Zero);

                Marshal.FreeHGlobal(outBuffer);
                CloseHandle(hVol);
                return success;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 获取当前最大的 USN 号作为下一次同步的起点
        /// </summary>
        public long GetCurrentUsn(string driveLetter)
        {
            string volName = $@"\\.\{driveLetter.TrimEnd('\\')}";
            IntPtr hVol = CreateFile(volName, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            if (hVol == new IntPtr(-1)) return 0;

            int size = Marshal.SizeOf(typeof(USN_JOURNAL_DATA));
            IntPtr outBuffer = Marshal.AllocHGlobal(size);
            uint bytesReturned;
            long nextUsn = 0;

            if (DeviceIoControl(hVol, FSCTL_QUERY_USN_JOURNAL, IntPtr.Zero, 0, outBuffer, size, out bytesReturned, IntPtr.Zero))
            {
                USN_JOURNAL_DATA data = Marshal.PtrToStructure<USN_JOURNAL_DATA>(outBuffer);
                nextUsn = data.NextUsn;
            }

            Marshal.FreeHGlobal(outBuffer);
            CloseHandle(hVol);
            return nextUsn;
        }

        /// <summary>
        /// 获取自上次以来变更过的文件名列表
        /// </summary>
        public HashSet<string> GetModifiedFiles(string driveLetter, long lastUsn)
        {
            HashSet<string> changedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string volName = $@"\\.\{driveLetter.TrimEnd('\\')}";
            IntPtr hVol = CreateFile(volName, GENERIC_READ, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);
            
            if (hVol == new IntPtr(-1)) return changedFiles;

            try {
                int journalDataSize = Marshal.SizeOf(typeof(USN_JOURNAL_DATA));
                IntPtr journalBuffer = Marshal.AllocHGlobal(journalDataSize);
                uint bytesReturned;

                if (!DeviceIoControl(hVol, FSCTL_QUERY_USN_JOURNAL, IntPtr.Zero, 0, journalBuffer, journalDataSize, out bytesReturned, IntPtr.Zero))
                {
                    return changedFiles;
                }

                USN_JOURNAL_DATA journalData = Marshal.PtrToStructure<USN_JOURNAL_DATA>(journalBuffer);
                Marshal.FreeHGlobal(journalBuffer);

                READ_USN_JOURNAL_DATA readData = new READ_USN_JOURNAL_DATA {
                    StartUsn = lastUsn,
                    ReasonMask = 0xFFFFFFFF,
                    ReturnOnlyOnClose = 0,
                    Timeout = 0,
                    BytesToWaitFor = 0,
                    UsnJournalID = journalData.UsnJournalID
                };

                int readDataSize = Marshal.SizeOf(readData);
                IntPtr readDataBuffer = Marshal.AllocHGlobal(readDataSize);
                Marshal.StructureToPtr(readData, readDataBuffer, false);

                IntPtr outputBuffer = Marshal.AllocHGlobal(65536);
                if (DeviceIoControl(hVol, FSCTL_READ_USN_JOURNAL, readDataBuffer, readDataSize, outputBuffer, 65536, out bytesReturned, IntPtr.Zero))
                {
                    IntPtr currentRecord = outputBuffer + sizeof(long);
                    uint processedBytes = sizeof(long);

                    while (processedBytes < bytesReturned)
                    {
                        USN_RECORD_V2 record = Marshal.PtrToStructure<USN_RECORD_V2>(currentRecord);
                        if (record.RecordLength == 0) break;

                        string fileName = Marshal.PtrToStringUni(currentRecord + record.FileNameOffset, record.FileNameLength / 2);
                        changedFiles.Add(fileName);

                        processedBytes += record.RecordLength;
                        currentRecord += (int)record.RecordLength;
                    }
                }
                Marshal.FreeHGlobal(readDataBuffer);
                Marshal.FreeHGlobal(outputBuffer);
            } finally {
                CloseHandle(hVol);
            }

            return changedFiles;
        }

        /// <summary>
        /// 获取当前 USN 日志状态（示例逻辑）
        /// </summary>
        public string GetJournalInfo(string driveLetter)
        {
            if (!IsNtfsUsnSupported(driveLetter)) return "不支持或权限不足";
            return "NTFS USN 日志已激活，可用于高效筛选。";
        }
    }
}
