using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using NUnit.Framework;
using ServiceStack;
using ServiceStack.IO;
using Web;

namespace Run
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            try
            {
                var host = (await Startup.CreateWebHost("web", args))?.Build();
                host?.Run();
            } 
            catch (Exception ex)
            {
                ex = ex.UnwrapIfSingleException();
                Console.WriteLine(Startup.Verbose ? ex.ToString() : ex.Message);
            }
        }
    }

    [TestFixture]
    class Tests
    {
        public Tests()
        {
            Startup.Verbose = true;
            Startup.UserInputYesNo = Startup.ApproveUserInputRequests;
            //Startup.UserInputYesNo = Startup.DenyUserInputRequests;
        }

        static void RetryExec(Action fn, int retryTimes=5)
        {
            while (retryTimes-- > 0)
            {
                try
                {
                    fn();
                    return;
                }
                catch
                {
                    Thread.Sleep(200);
                }
            }
        }

        private static void DeleteDirectory(string dir)
        {
            RetryExec(() => FileSystemVirtualFiles.DeleteDirectoryRecursive(dir));
        }

        private static void DeleteAndCreateDirectory(string dir)
        {
            DeleteDirectory(dir);
            Directory.CreateDirectory(dir);
        }

        private static void DeleteCreateAndSetDirectory(string dir)
        {
            DeleteDirectory(dir);
            RetryExec(() => Directory.CreateDirectory(dir));
            RetryExec(() => Directory.SetCurrentDirectory(dir));
        }

        void CreateHostProject()
        {
            DeleteAndCreateDirectory("wip\\MyProject");
            File.WriteAllText("wip\\MyProject\\appsettings.json","");
        }

        [Test]
        public async Task Run_web()
        {
            await Startup.CreateWebHost("web", new string[]{ });
        }

        [Test]
        public async Task Run_web_new()
        {
            await Startup.CreateWebHost("web", new[]{ "new" });
        }

        [Test]
        public async Task Run_web_list()
        {
            await Startup.CreateWebHost("web", new[]{ "list" });
        }

        [Test]
        public async Task Run_web_run()
        {
            await Startup.CreateWebHost("web", new[]{ "run" });
        }

        private static void SetProjectCurrentDirectory() => Directory.SetCurrentDirectory("..\\..\\..\\");

        [Test]
        public async Task Run_web_run_script_html()
        {
            //web run script.html -id 10643 > 10643.html && start 10643.html
            SetProjectCurrentDirectory();
            await Startup.CreateWebHost("web", new[]{ "run", "script.html", "-id", "10643" });
        }

        [Test]
        public async Task Run_web_run_script_ss()
        {
            //web run script.html -id 10643 > 10643.html && start 10643.html
            SetProjectCurrentDirectory();
            await Startup.CreateWebHost("web", new[]{ "run", "script.ss", "-id", "10643" });
        }

        [Test]
        public async Task Run_web_run_script_aws()
        {
            SetProjectCurrentDirectory();
            await Startup.CreateWebHost("web", new[]{ "run", "script-aws.ss" });
        }

        [Test]
        public async Task Run_web_run_script_azure()
        {
            SetProjectCurrentDirectory();
            await Startup.CreateWebHost("web", new[]{ "run", "script-azure.ss" });
        }

        [Test]
        public async Task Run_web_run_path_appsettings()
        {
            await Startup.CreateWebHost("web", new[]{ "run", "path/app.settings" });
        }

        [Test]
        public async Task Run_web_help()
        {
            await Startup.CreateWebHost("web", new[]{ "/h" });
        }
        
        [Test]
        public async Task Run_plus()
        {
            await Startup.CreateWebHost("web", new[]{ "+" });
        }
        
        [Test]
        public async Task Run_plus_tag_sharp()
        {
            await Startup.CreateWebHost("web", new[]{ "+", "#sharp" });
        }
        
        [Test]
        public async Task Run_plus_tag_project()
        {
            await Startup.CreateWebHost("web", new[]{ "+", "#project" });
        }
        
        [Test]
        public async Task Run_plus_tag_project_sharp()
        {
            await Startup.CreateWebHost("web", new[]{ "+", "#project,sharp" });
        }

        [Test]
        public async Task Run_init()
        {
            Directory.CreateDirectory("wip\\spirals");
            Directory.SetCurrentDirectory("wip\\spirals");
            await Startup.CreateWebHost("web", new[]{ "init" });
        }

        [Test]
        public async Task Run_plus_nginx()
        {
            CreateHostProject();
            Directory.SetCurrentDirectory("wip");
            await Startup.CreateWebHost("web", new[]{ "+nginx" });
        }

        [Test]
        public async Task Run_plus_validation_contacts()
        {
            CreateHostProject();
            Directory.SetCurrentDirectory("wip");
            await Startup.CreateWebHost("web", new[]{ "+validation-contacts" });
        }

        [Test]
        public async Task Run_plus_validation_contacts_with_project()
        {
            CreateHostProject();
            Directory.SetCurrentDirectory("wip");
            await Startup.CreateWebHost("web", new[]{ "/f", "+validation-contacts", "TheProject" });
        }

        [Test]
        public async Task Run_plus_auth_memory_plus_validation_contacts_with_project()
        {
            CreateHostProject();
            Directory.SetCurrentDirectory("wip");
            await Startup.CreateWebHost("web", new[]{ "+auth-memory+validation-contacts", "TheProject" });
        }

        [Test]
        public async Task Run_from_scratch_bootstrap_validation_contacts_with_project()
        {
            Directory.CreateDirectory("wip\\FromScratch");
            Directory.SetCurrentDirectory("wip\\FromScratch");
            await Startup.CreateWebHost("web", new[]{ "+init+bootstrap-sharp+validation-contacts", "TheProject" });
        }

        [Test]
        public async Task Run_from_scratch_lts_bootstrap_validation_contacts_with_project()
        {
            Directory.CreateDirectory("wip\\FromScratch");
            Directory.SetCurrentDirectory("wip\\FromScratch");
            await Startup.CreateWebHost("web", new[]{ "+init-lts+bootstrap-sharp+validation-contacts", "TheProject" });
        }

        [Test]
        public async Task Run_creating_new_web_project()
        {
            Directory.SetCurrentDirectory("wip");
            await Startup.CreateWebHost("web", new[]{ "new", "web+auth-memory+validation-contacts", "TheProject" });
        }

        [Test]
        public async Task Run_creating_new_project_with_validation_contacts()
        {
            Directory.SetCurrentDirectory("wip");
            await Startup.CreateWebHost("web", new[]{ "new", "web+bootstrap-sharp+auth-memory+validation-contacts", "TheProject" });
        }

        [Test]
        public async Task Run_vue_lite_lib()
        {
            CreateHostProject();
            Directory.SetCurrentDirectory("wip\\MyProject");
            await Startup.CreateWebHost("web", new[]{ "+vue-lite-lib", "TheProject" });
        }

        [Test]
        public async Task Run_react_lite_lib()
        {
            CreateHostProject();
            Directory.SetCurrentDirectory("wip\\MyProject");
            await Startup.CreateWebHost("web", new[]{ "+react-lite-lib", "TheProject" });
        }

        [Test]
        public async Task Run_creating_new_init()
        {
            CreateHostProject();
            Directory.SetCurrentDirectory("wip\\MyProject");
            await Startup.CreateWebHost("web", new[]{ "+init+bootstrap-sharp+validation-contacts+auth-sqlite", "TheProject" });
        }

        [Test]
        public async Task Run_creating_new_sqlite()
        {
            CreateHostProject();
            Directory.SetCurrentDirectory("wip\\MyProject");
            await Startup.CreateWebHost("web", new[]{ "+init+sqlite", "TheProject" });
        }

        [Test]
        public async Task Run_clean()
        {
            CreateHostProject();
            Directory.SetCurrentDirectory("wip");
            await Startup.CreateWebHost("web", new[]{ "/clean" });
        }

        [Test]
        public async Task Run_creating_new_react_lite()
        {
            CreateHostProject();
            Directory.SetCurrentDirectory("wip");
            await Startup.CreateWebHost("web", new[]{ "new", "react-lite", "rl" });
        }

        [Test]
        public async Task Run_apply_init_authsqlserver_sqlite_default_project()
        {
            DeleteCreateAndSetDirectory("wip\\TestSqlite");
            await Startup.CreateWebHost("web", new[]{ "+init+bootstrap-sharp+sqlite+auth-db" });
        }

        [Test]
        public async Task Run_apply_init_authsqlserver_sqlite_default_project_rename()
        {
            DeleteCreateAndSetDirectory("wip\\test-sqlite");
            await Startup.CreateWebHost("web", new[]{ "+init+bootstrap-sharp+auth-sqlserver+sqlite" });
        }

        [Test]
        public async Task Mix_help()
        {
            await Startup.Mix(new string[0]);
        }
        
        [Test]
        public async Task Mix_search_db()
        {
            await Startup.Mix(new []{ "#db" });
        }
        
        [Test]
        public async Task Mix_search_project_db()
        {
            await Startup.Mix(new []{ "#project,db" });
        }

        [Test]
        public async Task Mix_init_bootstrap_sharp()
        {
            DeleteCreateAndSetDirectory("wip\\MixText");
            await Startup.Mix(new[] { "init", "bootstrap-sharp" });
        }

        [Test]
        public async Task Mix_init_bootstrap_sharp_indexes()
        {
            DeleteCreateAndSetDirectory("wip\\MixText");
            await Startup.Mix(new[] { "1", "5" });
        }

        [Test]
        public async Task Mix_init_bootstrap_sharp_indexes_invalid()
        {
            DeleteCreateAndSetDirectory("wip\\MixText");
            try
            {
                await Startup.Mix(new[] { "0", "1000" });
            }
            catch (ArgumentOutOfRangeException) {}
        }

    }
}