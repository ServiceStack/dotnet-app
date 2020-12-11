using System;
using System.Collections.Generic;
using System.Threading;
using ServiceStack;
using ServiceStack.IO;
using ServiceStack.Logging;

namespace GistRun.ServiceInterface
{
    public class StatsLogEntry
    {
        public DateTime StartDate { get; set; }
        public string Id { get; set; }         // path
        public int? ExitCode { get; set; }
        public int? OutLen { get; set; }
        public int? ErrLen { get; set; }
        public long DurationMs { get; set; }      // ms
        public string SessionId { get; set; }  // ss-id
        public string RemoteIp { get; set; }
        public string Error { get; set; }
        public int Count { get; set; }
    }
    
    public class StatsLogger
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(StatsLogger));

        readonly object semaphore = new object();
        private List<StatsLogEntry> logs = new List<StatsLogEntry>();
        private List<StatsLogEntry> errorLogs = new List<StatsLogEntry>();
        public Func<DateTime> CurrentDateFn { get; set; } = () => DateTime.UtcNow;

        private readonly IVirtualFiles files;
        private readonly string statsLogsPattern;
        private readonly string errorLogsPattern;
        private readonly TimeSpan appendEverySecs;
        private readonly Timer timer;
        
        public Action<List<StatsLogEntry>, Exception> OnWriteLogsError { get; set; }

        public StatsLogger(IVirtualFiles files = null, string statsLogsPattern = null, string errorLogsPattern = null, TimeSpan? appendEvery = null)
        {
            this.files = files ?? new FileSystemVirtualFiles(HostContext.Config.WebHostPhysicalPath);
            this.statsLogsPattern = statsLogsPattern ?? "logs/{year}-{month}/{year}-{month}-{day}.csv";
            this.errorLogsPattern = errorLogsPattern ?? "logs/{year}-{month}/{year}-{month}-{day}-errors.csv";
            this.appendEverySecs = appendEvery ?? TimeSpan.FromSeconds(1);

            timer = new Timer(OnFlush, null, this.appendEverySecs, Timeout.InfiniteTimeSpan);
        }

        public void Log(StatsLogEntry entry)
        {
            lock (semaphore)
            {
                logs.Add(entry);
            }
        }

        public void LogError(StatsLogEntry entry)
        {
            lock (semaphore)
            {
                errorLogs.Add(entry);
            }
        }
        
        protected virtual void OnFlush(object state)
        {
            if (logs.Count + errorLogs.Count > 0)
            {
                List<StatsLogEntry> logsSnapshot = null;
                List<StatsLogEntry> errorLogsSnapshot = null;

                lock (semaphore)
                {
                    if (logs.Count > 0)
                    {
                        logsSnapshot = this.logs;
                        this.logs = new List<StatsLogEntry>();
                    }
                    if (errorLogs.Count > 0)
                    {
                        errorLogsSnapshot = this.errorLogs;
                        this.errorLogs = new List<StatsLogEntry>();
                    }
                }

                var now = CurrentDateFn();
                if (logsSnapshot != null)
                {
                    var logFile = GetLogFilePath(statsLogsPattern, now);
                    WriteLogs(logsSnapshot, logFile);
                }
                if (errorLogsSnapshot != null)
                {
                    var logFile = GetLogFilePath(errorLogsPattern, now);
                    WriteLogs(errorLogsSnapshot, logFile);
                }
            }
            timer.Change(appendEverySecs, Timeout.InfiniteTimeSpan);
        }

        public string GetLogFilePath(string logFilePattern, DateTime forDate)
        {
            var year = forDate.Year.ToString("0000");
            var month = forDate.Month.ToString("00");
            var day = forDate.Day.ToString("00");
            return logFilePattern.Replace("{year}", year).Replace("{month}", month).Replace("{day}", day);
        }

        public virtual void WriteLogs(List<StatsLogEntry> logs, string logFile)
        {
            try
            {
                var csv = logs.ToCsv();
                if (!files.FileExists(logFile))
                {
                    files.WriteFile(logFile, csv);
                }
                else
                {
                    var csvRows = csv.Substring(csv.IndexOf('\n') + 1);
                    files.AppendFile(logFile, csvRows);
                }
            }
            catch (Exception ex)
            {
                OnWriteLogsError?.Invoke(logs, ex);
                log.Error(ex);
            }
        }
        
    }
}