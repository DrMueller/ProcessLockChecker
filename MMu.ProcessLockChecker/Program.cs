using MMu.ProcessLockChecker.ViewServices;

namespace MMu.ProcessLockChecker
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.Write("Enter filepath:");

            var line = Console.ReadLine();

            var lockingFiles = FileLockService.CheckWhichProcessIsLocking(line!);

            foreach (var lockingFile in lockingFiles)
            {
                Console.WriteLine(lockingFile);
            }

            Main(args);
        }
    }
}