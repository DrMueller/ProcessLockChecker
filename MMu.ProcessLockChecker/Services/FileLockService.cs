using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace MMu.ProcessLockChecker.ViewServices
{
    public static class FileLockService
    {
        public static IReadOnlyCollection<string> CheckWhichProcessIsLocking(string filePath)
        {
            var processesLocking = FileUtil.WhoIsLocking(filePath);

            if (!processesLocking.Any())
            {
                return new List<string>
                {
                    "No locking processed found"
                };
            }

            var result = processesLocking.Select(f => $"{f.ProcessName}").ToList();
            return result;
        }
    }
}