using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using NUnit.Framework;
using ServiceStack;
using ServiceStack.Desktop;
using ServiceStack.IO;
using ServiceStack.Script;
using ServiceStack.Text;
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
            await Startup.Mix("mix", new string[0]);
        }
        
        [Test]
        public async Task WebMix_help()
        {
            await Startup.CreateWebHost("web mix", new[]{ "mix" });
        }
        
        [Test]
        public async Task Mix_search_db()
        {
            await Startup.Mix("mix", new []{ "#db" });
        }
        
        [Test]
        public async Task Mix_search_project_db()
        {
            await Startup.Mix("mix", new []{ "#project,db" });
        }

        [Test]
        public async Task Mix_init_bootstrap_sharp()
        {
            DeleteCreateAndSetDirectory("wip\\MixText");
            await Startup.Mix("mix", new[] { "init", "bootstrap-sharp" });
        }

        [Test]
        public async Task Mix_init_bootstrap_sharp_indexes()
        {
            DeleteCreateAndSetDirectory("wip\\MixText");
            await Startup.Mix("mix", new[] { "1", "5" });
        }

        [Test]
        public async Task Mix_init_bootstrap_sharp_indexes_invalid()
        {
            DeleteCreateAndSetDirectory("wip\\MixText");
            try
            {
                await Startup.Mix("mix", new[] { "0", "1000" });
            }
            catch (ArgumentOutOfRangeException) {}
        }

        [Test]
        public async Task Mix_gist_with_name()
        {
            DeleteCreateAndSetDirectory("wip\\MixText");
            await Startup.Mix("mix", new[] { "-name", "ProjectName", "init", "bootstrap-sharp" });
        }
        
        [Test]
        public async Task Mix_replace_terms()
        {
            DeleteCreateAndSetDirectory("wip\\MixText");
            await Startup.Mix("mix", new[] { "-replace", "Config=Remix", "-replace", "\"IApplicationBuilder app, \"=\"/*ignore*/\"", "init", "bootstrap-sharp" });
        }

        [Test]
        public async Task Mix_delete()
        {
            RetryExec(() => Directory.SetCurrentDirectory("wip\\MixText"));
            await Startup.Mix("mix", new[] { "-delete", "init", "bootstrap-sharp" });
        }

        [Test]
        public async Task Mix_with_resolved_ServiceInterface()
        {
            RetryExec(() => Directory.SetCurrentDirectory("wip\\TheProject"));
            await Startup.Mix("mix", new[] { "feature-mq" });
        }
        
        [Test]
        public async Task Run_web_apps()
        {
            await Startup.CreateWebHost("web", new[]{ "apps" });
        }

        [Test]
        public async Task Run_web_install_redis()
        {
            await Startup.CreateWebHost("web", new[]{ "install", "redis" });
        }

        [Test]
        public async Task Run_web_open_redis()
        {
            await Startup.CreateWebHost("web", new[]{ "open", "redis" });
        }

        [Test]
        public async Task Run_web_run_redis()
        {
            await Startup.CreateWebHost("web", new[]{ "run", "redis" });
        }
 
        [Test]
        public async Task Run_web_open_url()
        {
            await Startup.CreateWebHost("web", new[]{ "open", "https://github.com/sharp-apps/bare" });
        }

        [Test]
        public async Task Run_version()
        {
            await Startup.CreateWebHost("web", new[]{ "/version" });
        }

        [Test]
        public async Task Run_lisp_repl()
        {
            await Startup.CreateWebHost("web", new[]{ "lisp" });
        }

        [Test]
        public async Task Run_lisp_run_parse_rss()
        {
            await Startup.CreateWebHost("web", new[]{ "run", "..\\..\\..\\parse-rss.l" });
        }
 
        [Test]
        public async Task Run_ts_http2()
        {
            await Startup.CreateWebHost("web", new[]{ "ts", "https://localhost:5001" });
        }

        [Test]
        public async Task Run_proto_url()
        {
            DeleteCreateAndSetDirectory("wip\\TestGrpc");
            await Startup.CreateWebHost("web", new[]{ "proto", "https://localhost:5001" });
        }

        [Test]
        public async Task Run_proto_url_file()
        {
            DeleteCreateAndSetDirectory("wip\\TestGrpc");
            await Startup.CreateWebHost("web", new[]{ "proto", "https://localhost:5001", "grpc.services.proto" });
        }

        [Test]
        public async Task Run_proto_update_multiple()
        {
            DeleteCreateAndSetDirectory("wip\\TestGrpc");
            await Startup.CreateWebHost("web", new[]{ "proto", "https://localhost:5001", "todoworld" });
            await Startup.CreateWebHost("web", new[]{ "proto", "https://localhost:5002", "alltypes" });
            await Startup.CreateWebHost("web", new[]{ "proto" });
        }

        [Test]
        public async Task Run_proto_langs()
        {
            await Startup.CreateWebHost("web", new[]{ "proto-langs" });
        }

        [Test]
        public async Task Run_proto_test_error()
        {
//            await Startup.CreateWebHost("web", new[]{ "proto-test" });
            await Startup.CreateWebHost("web", new[]{ "proto-test", "https://localhost:5001" });
        }

        [Test]
        public async Task Run_proto_csharp()
        {
            DeleteCreateAndSetDirectory("wip\\TestGrpc");
            await Startup.CreateWebHost("web", new[]{ "proto-csharp", "https://localhost:5001", "todoworld" });
        }

        [Test]
        public async Task Run_proto_csharp_with_flags()
        {
            DeleteCreateAndSetDirectory("wip\\TestGrpc");
            await Startup.CreateWebHost("web", new[]{ "-v", "proto-csharp", "https://localhost:5001", "todoworld" });
        }

        [Test]
        public async Task Run_proto_csharp_with_outdir()
        {
            DeleteCreateAndSetDirectory("wip\\TestGrpc");
            await Startup.CreateWebHost("web", new[]{ "-v", "proto-csharp", "https://localhost:5001", "todoworld", "--out", "CSharp" });
        }

        [Test]
        public async Task Run_proto_all_langs()
        {
            DeleteCreateAndSetDirectory("wip\\TestGrpc");
            
            var client = new JsonServiceClient(Startup.GrpcSource);
            var response = client.Get(new GetLanguages());

            foreach (var entry in response.Results)
            {
                var lang = entry.Key;
                await Startup.CreateWebHost("web", new[]{ "-v", "proto-" + lang, "https://localhost:5001", "todoworld", "--out", lang.ToPascalCase() });
            }
        }
 
        [Test]
        public async Task Run_proto_csharp_proto_and_file_update()
        {
            DeleteCreateAndSetDirectory("wip\\TestGrpc");
            await Startup.CreateWebHost("web", new[]{ "proto", "https://localhost:5001", "todoworld" });
            
            await Startup.CreateWebHost("web", new[]{ "proto", "todoworld.services.proto" }); // update .proto
            await Startup.CreateWebHost("web", new[]{ "proto-csharp", "todoworld.services.proto" }); // update .cs
        }
 
        [Test]
        public async Task Run_proto_csharp_dir_update()
        {
            DeleteCreateAndSetDirectory("wip\\TestGrpc");
            "..\\..\\protos\\alltypes".CopyAllTo(Environment.CurrentDirectory);
            
            await Startup.CreateWebHost("web", new[]{ "proto-csharp", "." }); // update .cs
        }
 
        [Test]
        public async Task Run_proto_csharp_dir_update_out_dir()
        {
            DeleteCreateAndSetDirectory("wip\\TestGrpc");
            
            await Startup.CreateWebHost("web", new[]{ "proto-csharp", "..\\..\\protos\\alltypes", "-out", "CSharp" }); // update .cs
        }
 
        [Test]
        public async Task Run_proto_csharp_all_update()
        {
            DeleteCreateAndSetDirectory("wip\\TestGrpc");
            "..\\..\\protos\\alltypes".CopyAllTo(Path.Combine(Environment.CurrentDirectory, "alltypes"));
            
            await Startup.CreateWebHost("web", new[]{ "proto-csharp" }); // update .cs
        }

        [Test]
        public async Task Can_create_template()
        {
            DeleteCreateAndSetDirectory("wip\\TestAurelia");
            //"web new aurelia-spa testproject --verbose"
            await Startup.CreateWebHost("web", new[]{ "new", "aurelia-spa", "testpoject" });
        }

        [Test]
        public async Task Can_create_template_from_private_repo()
        {
            DeleteCreateAndSetDirectory("wip\\TestRepo");
            //"web new aurelia-spa testproject --verbose"
            await Startup.CreateWebHost("web", new[]{ "new", "mythz/web", "TheProject" });
        }

        [Test]
        public async Task Can_create_template_from_URL()
        {
            DeleteCreateAndSetDirectory("wip\\TestRepo");
            //"web new aurelia-spa testproject --verbose"
            await Startup.CreateWebHost("web", new[]{ "new", "https://github.com/mythz/web/archive/master.zip", "TheProject" });
        }

        [Test]
        public async Task Can_run_app()
        {
            Directory.SetCurrentDirectory(@"C:\Source\projects\VueSpa\VueSpa\bin\Release\netcoreapp3.1\publish");
            try 
            {
                await Startup.CreateWebHost("x", new[]{ "VueSpa.dll" });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        [Test]
        public async Task Can_run_private_SharpApp()
        {
            Directory.SetCurrentDirectory(@"C:\Source\wip\");
            try 
            {
                await Startup.CreateWebHost("x", new[]{ "open", "mythz/spirals-private" });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        [Test]
        public async Task Can_download_repo()
        {
            Directory.SetCurrentDirectory(@"C:\Source\wip\");
            try 
            {
                await Startup.CreateWebHost("x", new[]{ "download", "NetCoreApps/NorthwindCrud" });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        [Test]
        public async Task Can_run_studio_connect()
        {
            Directory.SetCurrentDirectory(@"C:\Source\wip\");
            try 
            {
                Startup.GetAppHostInstructions = _ => new AppHostInstructions {
                    ImportParams = DesktopConfig.Instance.ImportParams,
                };
                var host = (await Startup.CreateWebHost("x",
                    new[] {"open", "studio", "-connect", "https://localhost:5001"}))?.Build();
                host?.Run();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        [Test]
        public void ParsePostData()
        {
            var formDataElement = @"
------WebKitFormBoundaryZTA4w2lDeTVKtFy0
Content-Disposition: form-data; name=""EvaluateCode""

1 + 1
------WebKitFormBoundaryZTA4w2lDeTVKtFy0--
";
            var el = formDataElement.AsSpan().ParsePostDataElement();
            Assert.That(el.Type, Is.EqualTo("form-data"));
            Assert.That(el.Name, Is.EqualTo("EvaluateCode"));
            Assert.That(el.Body, Is.EqualTo("1 + 1"));
        }

        [Test]
        public void Parse_scheme()
        {
            var firstArg = "app://studio?connect=https://localhost:5001&debug";
            var cmds = firstArg.ConvertUrlSchemeToCommands();
            cmds.Join(",").Print();
            Assert.That(cmds, Is.EquivalentTo(new[]{ "open", "studio", "-connect", "https://localhost:5001", "-debug" }));
        }

        [Test]
        public async Task Can_create_new_gist_from_dir()
        {
            //"web new aurelia-spa testproject --verbose"
            await Startup.CreateWebHost("x", new[]{ "gist-new", @"C:\src\dotnet-app\src\Test\protos", "-token", Environment.GetEnvironmentVariable("GISTLYN_TOKEN") });
        }

        [Test]
        public async Task Can_run_autodto_northwind_example()
        {
            RetryExec(() => Directory.SetCurrentDirectory("apps\\autodto\\northwind"));
            var host = (await Startup.CreateWebHost("x", new string[0]))?.Build();
            host?.Run();
        }

        [Test]
        public async Task Can_Replace_Tokens()
        {
            DeleteCreateAndSetDirectory("wip\\TestRepo");
            await Startup.CreateWebHost("x", new[] {
                "mix", "autodto", 
                "-replace", "DIALECT=postgresql", 
                "-replace", "CONNECTION_STRING=\"" + Environment.GetEnvironmentVariable("TECHSTACKS_DB") + "\""  
            });
        }

        [Test]
        public async Task Can_Update_FSharp()
        {
            RetryExec(() => Directory.SetCurrentDirectory("C:\\Source\\projects\\autodto"));
            await Startup.CreateWebHost("x", new[] {
                "fs", 
            });
        }

    }
}