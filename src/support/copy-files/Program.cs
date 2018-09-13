using System;
using System.IO;
using Funq;
using ServiceStack;
using ServiceStack.IO;
using ServiceStack.Aws;
using Amazon.S3;
using Amazon;
using Microsoft.WindowsAzure.Storage;
using ServiceStack.Azure.Storage;

namespace CopyFiles
{
    public class Program
    {
        const string SourcePath = "~/../../apps/rockwind-vfs";

        public static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine("Usage: ");
                Console.WriteLine("copy-files [provider] [config]");

                Console.WriteLine("\nVirtual File System Providers:");
                Console.WriteLine(" - files [destination]");
                Console.WriteLine(" - s3 {AccessKey:key,SecretKey:secretKey,Region:us-east-1,Bucket:s3Bucket}");
                Console.WriteLine(" - azure {ConnectionString:connectionString,ContainerName:containerName}");
                return;
            }

            var destFs = GetVirtualFiles(ResolveValue(args[0]), ResolveValue(args[1]));
            if (destFs == null)
            {
                Console.WriteLine("Unknown Provider: " + ResolveValue(args[0]));
                return;
            }

            var sourcePath = SourcePath.MapProjectPath();
            if (!Directory.Exists(sourcePath))
            {
                Console.WriteLine("Source Directory does not exist: " + sourcePath);
                return;
            }

            var sourceFs = new FileSystemVirtualFiles(sourcePath);

            foreach (var file in sourceFs.GetAllMatchingFiles("*"))
            {
                Console.WriteLine("Copying: " + file.VirtualPath);
                destFs.WriteFile(file);
            }
        }

        public static string ResolveValue(string value)
        {
            if (value?.StartsWith("$") == true)
            {
                var envValue = Environment.GetEnvironmentVariable(value.Substring(1));
                if (!string.IsNullOrEmpty(envValue))
                    return envValue;
            }
            return value;
        }

        public class AwsConfig
        {
            public string AccessKey { get; set; }
            public string SecretKey { get; set; }
            public string Region { get; set; }
        }

        public class S3Config : AwsConfig
        {
            public string Bucket { get; set; }
        }

        public class AzureConfig
        {
            public string ConnectionString { get; set; }
            public string ContainerName { get; set; }
        }

        public static IVirtualPathProvider GetVirtualFiles(string provider, string config)
        {
            if (provider != null)
            {
                switch (provider.ToLower())
                {
                    case "fs":
                    case "files":
                        return new FileSystemVirtualFiles(config.MapProjectPath());
                    case "s3":
                    case "s3virtualfiles":
                        var s3Config = config.FromJsv<S3Config>();
                        var region = RegionEndpoint.GetBySystemName(ResolveValue(s3Config.Region));
                        var awsClient = new AmazonS3Client(
                            ResolveValue(s3Config.AccessKey), 
                            ResolveValue(s3Config.SecretKey), 
                            region);
                        return new S3VirtualFiles(awsClient, ResolveValue(s3Config.Bucket));
                    case "azure":
                    case "azureblobvirtualfiles":
                        var azureConfig = config.FromJsv<AzureConfig>();
                        var storageAccount = CloudStorageAccount.Parse(ResolveValue(azureConfig.ConnectionString));
                        var container = storageAccount.CreateCloudBlobClient().GetContainerReference(ResolveValue(azureConfig.ContainerName));
                        container.CreateIfNotExists();
                        return new AzureBlobVirtualFiles(container);
                }
            }

            return null;
        }
    }
}
