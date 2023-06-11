using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace MMu.ProcessLockChecker.ViewServices
{
    // Taken from https://stackoverflow.com/questions/317071/how-do-i-find-out-which-process-is-locking-a-file-using-net
    public static class FileUtil
    {
        private const int CchRmMaxAppName = 255;
        private const int CchRmMaxSvcName = 63;
        private const int RmRebootReasonNone = 0;

        // ReSharper disable once InconsistentNaming
        private enum RM_APP_TYPE
        {
            RmUnknownApp = 0,
            RmMainWindow = 1,
            RmOtherWindow = 2,
            RmService = 3,
            RmExplorer = 4,
            RmConsole = 5,
            RmCritical = 1000
        }

        public static List<Process> WhoIsLocking(string path)
        {
            var key = Guid.NewGuid().ToString();
            var processes = new List<Process>();

            var res = RmStartSession(out var handle, 0, key);

            if (res != 0)
            {
                throw new Exception("Could not begin restart session.  Unable to determine file locker.");
            }

            try
            {
                const int ErrorMoreData = 234;
                uint pnProcInfo = 0,
                    lpdwRebootReasons = RmRebootReasonNone;

                string[] resources = { path }; // Just checking on one resource.

                res = RmRegisterResources(handle, (uint)resources.Length, resources, 0, null, 0, null);

                if (res != 0)
                {
                    throw new Exception("Could not register resource.");
                }

                //Note: there's a race condition here -- the first call to RmGetList() returns
                //      the total number of process. However, when we call RmGetList() again to get
                //      the actual processes this number may have increased.
                res = RmGetList(handle, out var pnProcInfoNeeded, ref pnProcInfo, null, ref lpdwRebootReasons);

                if (res == ErrorMoreData)
                {
                    // Create an array to store the process results
                    var processInfo = new RM_PROCESS_INFO[pnProcInfoNeeded];
                    pnProcInfo = pnProcInfoNeeded;

                    // Get the list
                    res = RmGetList(handle, out pnProcInfoNeeded, ref pnProcInfo, processInfo, ref lpdwRebootReasons);

                    if (res == 0)
                    {
                        processes = new List<Process>((int)pnProcInfo);

                        for (var i = 0; i < pnProcInfo; i++)
                        {
                            try
                            {
                                processes.Add(Process.GetProcessById(processInfo[i].Process.dwProcessId));
                            }
                            // catch the error -- in case the process is no longer running
                            catch (ArgumentException) { }
                        }
                    }
                    else
                    {
                        throw new Exception("Could not list processes locking resource.");
                    }
                }
                else if (res != 0)
                {
                    throw new Exception("Could not list processes locking resource. Failed to get size of result.");
                }
            }
            finally
            {
                RmEndSession(handle);
            }

            return processes;
        }

        [DllImport("rstrtmgr.dll")]
        private static extern int RmEndSession(uint pSessionHandle);

        [DllImport("rstrtmgr.dll")]
        private static extern int RmGetList(uint dwSessionHandle,
            out uint pnProcInfoNeeded,
            ref uint pnProcInfo,
            [In][Out] RM_PROCESS_INFO[] rgAffectedApps,
            ref uint lpdwRebootReasons);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
        private static extern int RmRegisterResources(uint pSessionHandle,
            uint nFiles,
            string[] rgsFilenames,
            uint nApplications,
            [In] RmUniqueProcess[] rgApplications,
            uint nServices,
            string[] rgsServiceNames);

        [DllImport("rstrtmgr.dll", CharSet = CharSet.Auto)]
        private static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        // ReSharper disable once InconsistentNaming
        private struct RM_PROCESS_INFO
        {
            public readonly RmUniqueProcess Process;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchRmMaxAppName + 1)]
            public readonly string strAppName;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchRmMaxSvcName + 1)]
            public readonly string strServiceShortName;

            public readonly RM_APP_TYPE ApplicationType;
            public readonly uint AppStatus;
            public readonly uint TSSessionId;
            [MarshalAs(UnmanagedType.Bool)]
            public readonly bool bRestartable;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RmUniqueProcess
        {
            public readonly int dwProcessId;
            public readonly FILETIME ProcessStartTime;
        }
    }
}