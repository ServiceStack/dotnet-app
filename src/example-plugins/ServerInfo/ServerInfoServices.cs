using System.Threading.Tasks;
using ServiceStack;
using ServiceStack.Templates;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System;
using ServiceStack.Redis;
using ServiceStack.OrmLite;
using System.Reflection;

namespace ServerInfo
{
    [Route("/process/{Id}")]
    [Route("/process/current")]
    public class GetProcess : IReturn<GetProcessResponse>
    {
        public int? Id { get; set; }
    }

    public class GetProcessResponse
    {
        public ProcessInfo Result { get; set; }
    }

    [Route("/processes")]
    public class SearchProcess : IReturn<SearchProcessResponse>
    {
        public string NameContains { get; set; }
        public int? MemoryBytesAbove { get; set; }
        public int? ActiveThreadsAbove { get; set; }
    }

    public class SearchProcessResponse
    {
        public List<ProcessInfo> Results { get; set; }
        public ResponseStatus ResponseStatus { get; set; }
    }

    [Route("/drives")]
    public class SearchDrives 
    {
        public int? LargerThanBytes { get; set; }
        public int? SmallerThanBytes { get; set; }
        public List<string> DriveFormatIn { get; set; }
    }

    public class SearchDrivesResponse
    {
        public List<Drive> Results { get; set; }
        public ResponseStatus ResponseStatus { get; set; }
    }

    public class ProcessInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string CpuTime { get; set; }
        public string UserTime { get; set; }
        public long MemoryBytes { get; set; }
        public long MemoryPeakBytes { get; set; }
        public int ActiveThreads { get; set; }
    }

    public class Drive
    {
        public string Name { get; set; }
        public string DriveType { get; set; }
        public string VolumeLabel { get; set; }
        public string DriveFormat { get; set; }
        public long AvailableFreeSpaceBytes { get; set; }
        public long TotalFreeSpaceBytes { get; set; }
        public long TotalSizeBytes { get; set; }
    }

    public class ServerInfoServices : Service
    {
        public object Any(GetProcess request) => new GetProcessResponse {
            Result = request.Id != null 
                ? Process.GetProcessById(request.Id.Value).ToDto()
                : Process.GetCurrentProcess().ToDto()
        };

        public object Any(SearchProcess request)
        {
            var allProcesses = ServerInfoFilters.Instance.processes();
            if (!string.IsNullOrEmpty(request.NameContains))
                allProcesses = allProcesses.Where(x => x.ProcessName.Contains(request.NameContains));
            if (request.MemoryBytesAbove != null)
                allProcesses = allProcesses.Where(x => x.WorkingSet64 > request.MemoryBytesAbove);
            if (request.ActiveThreadsAbove != null)
                allProcesses = allProcesses.Where(x => x.Threads.Count > request.ActiveThreadsAbove);

            return new SearchProcessResponse {
                Results = allProcesses.Map(x => x.ToDto())
            };
        }

        public object Any(SearchDrives request) 
        {
            var allDrives = ServerInfoFilters.Instance.drives();

            if (request.LargerThanBytes != null)
                allDrives = allDrives.Where(x => x.TotalSize > request.LargerThanBytes);

            if (request.SmallerThanBytes != null)
                allDrives = allDrives.Where(x => x.TotalSize < request.SmallerThanBytes);

            if (request.DriveFormatIn?.Count > 0)
                allDrives = allDrives.Where(x => request.DriveFormatIn.Contains(x.DriveFormat));

            return new SearchDrivesResponse {
                Results = allDrives.Map(x => x.ToDto())
            };
        }
    }

    public static class ProcessExtensions
    {
        public static ProcessInfo ToDto(this Process process) => process == null ? null : new ProcessInfo {
            Id = process.Id,
            Name = process.ProcessName,
            CpuTime = process.TotalProcessorTime.ToString(),
            UserTime = process.UserProcessorTime.ToString(),
            MemoryBytes = process.WorkingSet64,
            MemoryPeakBytes = process.PeakWorkingSet64,
            ActiveThreads = process.Threads.Count,
        };

        public static Drive ToDto(this DriveInfo drive) => drive == null ? null : new Drive {
            Name = drive.Name,
            DriveType = drive.DriveType.ToString(),
            VolumeLabel = drive.VolumeLabel,
            DriveFormat = drive.DriveFormat,
            AvailableFreeSpaceBytes = drive.AvailableFreeSpace,
            TotalFreeSpaceBytes = drive.TotalFreeSpace,
            TotalSizeBytes = drive.TotalSize,
        };
    }
}
