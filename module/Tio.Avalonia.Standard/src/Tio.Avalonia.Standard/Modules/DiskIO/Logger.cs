using System.Reflection;
using System.Text;

namespace Tio.Avalonia.Standard.Modules.DiskIO;

public class Logger
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Fatal
    }

    private const int MaxLogBackups = 3; // 最多保留的备份数量
    private static readonly Lock LockObj = new();
    private static string _logFilePath = string.Empty;
    private static bool _initialized;
    private static int _minimumLevel = (int)LogLevel.Info;
    private static readonly StringBuilder LogCache = new();

    public static LogLevel MinimumLevel
    {
        get => (LogLevel)Volatile.Read(ref _minimumLevel);
        set => Volatile.Write(ref _minimumLevel,
            Enum.IsDefined(value) ? (int)value : (int)LogLevel.Info);
    }
    
    public static void Initialize(
        string logDirectory, 
        string appName = "Application", 
        string version = "Unknown")
    {
        if (_initialized) return;

        try
        {
            if (string.IsNullOrWhiteSpace(logDirectory))
            {
                throw new ArgumentNullException(nameof(logDirectory), "日志输出目录不能为空。");
            }

            if (!Directory.Exists(logDirectory)) 
                Directory.CreateDirectory(logDirectory);

            // 保留以前的日志文件，创建带时间戳的备份
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            _logFilePath = Path.Combine(logDirectory, "latest.log");

            if (File.Exists(_logFilePath))
            {
                var backupPath = Path.Combine(logDirectory, $"log_{timestamp}.log");
                try
                {
                    File.Move(_logFilePath, backupPath);
                }
                catch (Exception exception)
                {
                    WriteFallbackError("轮换旧日志文件失败，将继续写入当前日志文件。", exception);
                }
            }

            // 清理旧日志文件，保持备份数量不超过上限
            CleanupOldLogFiles(logDirectory);

            // 如果传入空字符串或纯空格，兜底显示为 "Unknown"
            if (string.IsNullOrWhiteSpace(version))
            {
                version = "Unknown";
            }

            // 组装通用文件头
            var header = $"== {appName} Log {timestamp} ==\n" +
                         $"Version: {version}\n" +
                         $"OS: {Environment.OSVersion}\n" +
                         $"Runtime: {Environment.Version}\n" +
                         "===============================\n";

            File.WriteAllText(_logFilePath, header);

            _initialized = true;

            // 写入缓存的日志
            if (LogCache.Length > 0)
            {
                lock (LockObj)
                {
                    File.AppendAllText(_logFilePath, LogCache.ToString());
                }
                LogCache.Clear();
            }

            Info("日志系统初始化完成");
        }
        catch (Exception ex)
        {
            WriteFallbackError("日志系统初始化失败。", ex);
        }
    }

    /// <summary>
    /// 清理旧的日志备份文件，只保留指定数量的最新备份
    /// </summary>
    private static void CleanupOldLogFiles(string logDirectory)
    {
        try
        {
            // 获取所有备份日志文件
            var backupFiles = Directory.GetFiles(logDirectory, "log_*.log")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .ToList();

            // 如果备份文件数量超过限制，删除最旧的文件
            if (backupFiles.Count > MaxLogBackups)
            {
                foreach (var file in backupFiles.Skip(MaxLogBackups))
                {
                    try
                    {
                        file.Delete();
                        Info($"删除过时日志文件 : {file.Name}");
                    }
                    catch (Exception exception)
                    {
                        WriteFallbackError($"删除过时日志文件失败：{file.FullName}", exception);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            WriteFallbackError("清理过时日志文件失败。", ex);
        }
    }

    private static void WriteLog(LogLevel level, string message)
    {
        if (level < MinimumLevel)
            return;

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var threadId = Thread.CurrentThread.ManagedThreadId.ToString();
        var logEntry = $"[{timestamp}] [{level}] [Thread-{threadId}] {message}\n";

        try
        {
            if (!_initialized)
            {
                lock (LockObj)
                {
                    LogCache.Append(logEntry);
                }
                Console.WriteLine($"[{level}] {message}");
                return;
            }

            lock (LockObj)
            {
                File.AppendAllText(_logFilePath, logEntry);
            }

            Console.WriteLine($"[{level}] {message}");
        }
        catch (Exception ex)
        {
            WriteFallbackError($"写入 {level} 日志失败。", ex);
        }
    }

    public static void Debug(string message) => WriteLog(LogLevel.Debug, message);

    public static void Info(string message) => WriteLog(LogLevel.Info, message);

    public static void Warning(string message) => WriteLog(LogLevel.Warning, message);

    public static void Error(string message) => WriteLog(LogLevel.Error, message);

    public static void Error(Exception ex) => Error(ex.ToString());

    public static void Error(string message, Exception ex) => Error($"{message}{Environment.NewLine}{ex}");

    public static void Fatal(string message) => WriteLog(LogLevel.Fatal, message);

    public static void Fatal(Exception ex)
    {
        Fatal(ex.ToString());
    }

    public static void Fatal(string message, Exception ex) => Fatal($"{message}{Environment.NewLine}{ex}");

    private static void WriteFallbackError(string message, Exception exception)
    {
        try
        {
            Console.Error.WriteLine($"[Logger] {message}{Environment.NewLine}{exception}");
        }
        catch
        {
            // Console output is the final logging fallback and may be unavailable in GUI applications.
        }
    }
}
