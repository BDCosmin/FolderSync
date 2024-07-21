using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Timers;

class Sync
{
    private static string sourceFolder;
    private static string replicaFolder;
    private static string logFilePath;

    private static System.Timers.Timer timer;

    static void Main(string[] args)
    {
        if (args.Length != 4)
        {
            Console.WriteLine("Usage: <sourceFolder> <replicaFolder> <logFilePath> <intervalInSec>");
            return;
        }

        sourceFolder = args[0];
        replicaFolder = args[1];
        logFilePath = args[2];

        if (!int.TryParse(args[3], out int interval))
        {
            Console.WriteLine("Invalid interval.");
            return;
        }

        timer = new System.Timers.Timer(interval * 1000);
        timer.Elapsed += OnTimedEvent;
        timer.AutoReset = true;
        timer.Enabled = true;

        Console.WriteLine("Synchronization started. The updates will be displayed here every 15 seconds. Press [Enter] to exit.");
        Console.ReadLine();
    }

    private static void OnTimedEvent(object source, ElapsedEventArgs e)   // calling for sync method
    {
        Console.WriteLine($"Synchronization is up to date at {DateTime.Now}");
        SynchronizeFolders(sourceFolder, replicaFolder);
    }

    private static void SynchronizeFolders(string sourceDir, string targetDir)  // sync method
    {
        // check if the directory exists in replica - if not, create one
        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        // get all the directories and files from the source directory
        var sourceDirectories = Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories);
        var sourceFiles = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);

        var targetDirectories = Directory.GetDirectories(targetDir, "*", SearchOption.AllDirectories);
        var targetFiles = Directory.GetFiles(targetDir, "*", SearchOption.AllDirectories);

        var sourceFilePaths = sourceFiles.Select(f => Path.GetRelativePath(sourceDir, f)).ToHashSet();
        var targetFilePaths = targetFiles.Select(f => Path.GetRelativePath(targetDir, f)).ToHashSet();

        // create directories in the replica that exist in the source
        foreach (var sourceDirectory in sourceDirectories)
        {
            var relativePath = Path.GetRelativePath(sourceDir, sourceDirectory);
            var targetDirectory = Path.Combine(targetDir, relativePath);
            if (!Directory.Exists(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
                Log($"Created directory: {targetDirectory}");
            }
        }

        // copy files from source to replica
        foreach (var sourceFilePath in sourceFilePaths)
        {
            var sourceFile = Path.Combine(sourceDir, sourceFilePath);
            var targetFile = Path.Combine(targetDir, sourceFilePath);

            if (!File.Exists(targetFile) || ComputeSHA256(sourceFile) != ComputeSHA256(targetFile))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
                File.Copy(sourceFile, targetFile, true);
                Log($"Copied: {sourceFile} -> {targetFile}");
            }
        }

        // delete the files and directories from target that don't exist in source
        foreach (var targetFilePath in targetFilePaths)
        {
            var targetFile = Path.Combine(targetDir, targetFilePath);
            var sourceFile = Path.Combine(sourceDir, targetFilePath);

            if (!File.Exists(sourceFile))
            {
                File.Delete(targetFile);
                Log($"Deleted: {targetFile}");
            }
        }

        foreach (var targetDirectory in targetDirectories)
        {
            var relativePath = Path.GetRelativePath(targetDir, targetDirectory);
            var sourceDirectory = Path.Combine(sourceDir, relativePath);
            if (!Directory.Exists(sourceDirectory))
            {
                Directory.Delete(targetDirectory, true);
                Log($"Deleted directory: {targetDirectory}");
            }
        }
    }

    public static string ComputeSHA256(string filePath)  // compute the SHA-256 hash of a file specified by its file path
    {
            try
            {
                using (var sha256 = SHA256.Create())
                using (var stream = File.OpenRead(filePath))
                {
                    var hash = sha256.ComputeHash(stream);
                    var sb = new StringBuilder();
                    foreach (var b in hash)
                        sb.Append(b.ToString("X2"));
                    return sb.ToString();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error computing SHA256 for {filePath}: {ex.Message}");
                return string.Empty; // return empty string if catches an exception
            }
        }

        private static void Log(string message)  // the print inside the log file
        {
            string userName = Environment.UserName;

            try
            {
                string directory = Path.GetDirectoryName(logFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                Console.WriteLine(message);
                using (var writer = new StreamWriter(logFilePath, true))
                {
                    writer.WriteLine($"[{DateTime.Now}] [to {userName}]: {message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to log file: {ex.Message}");
            }
        }
}
