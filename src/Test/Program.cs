using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Win32;
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
        private static void SetProjectCurrentDirectory(string path) => Directory.SetCurrentDirectory(Path.Combine("..\\..\\..\\", path));

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
        public async Task version()
        {
            await Startup.CreateWebHost("x", new[]{ "-verbose", "--version" });
        }
        
        [Test]
        public async Task WebMix_help()
        {
            await Startup.CreateWebHost("web mix", new[]{ "mix" });
        }
        
        [Test]
        public async Task Mix_search_db()
        {
            await Startup.Mix("mix", new []{ "[db]" });
            await Startup.Mix("mix", new []{ "#db" });
        }
        
        [Test]
        public async Task Mix_search_project_db()
        {
            await Startup.Mix("mix", new []{ "[project,db]" });
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
        public async Task Mix_gist()
        {
            DeleteCreateAndSetDirectory("wip\\MixText");
            await Startup.Mix("mix", new[] { "redis" });
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
            await Startup.CreateWebHost("x", new[] {
                "gist-new", @"C:\src\dotnet-app\src\Test\protos", "-token", Environment.GetEnvironmentVariable("GISTLYN_TOKEN")
            });
        }

        [Test]
        public async Task Can_publish_folder_to_create_and_publish_gist()
        {
            RetryExec(() => Directory.SetCurrentDirectory(@"C:\src\dotnet-app\src\Test\protos"));
            await Startup.CreateWebHost("x", new[] {
                "publish", "-desc", "test .protos", "-token", Environment.GetEnvironmentVariable("GISTLYN_TOKEN")
            });
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
            
            await Startup.CreateWebHost("x", new[] {
                "mix", "autodto", 
                "-replace", "DIALECT=postgresql", 
                "-replace", "CONNECTION_STRING=$TECHSTACKS_DB"  
            });
        }

        [Test]
        public async Task Can_Replace_Tokens2()
        {
            DeleteCreateAndSetDirectory("wip\\TestRepo");
            await Startup.CreateWebHost("x", new[] {
                "mix", "sharpdata", 
                "-replace", "DIALECT=sqlite", 
                "-replace", "CONNECTION_STRING=\"northwind.sqlite\""  
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

        [Test]
        public async Task Can_download_rockwind_aws_app()
        {
            DeleteCreateAndSetDirectory("wip\\TestRepo");
            await Startup.CreateWebHost("x", new[] { "download", "sharp-apps/rockwind-aws", });
        }

        [Test]
        public async Task Can_mix_large_chinook_db()
        {
            DeleteCreateAndSetDirectory("wip\\TestRepo");
            await Startup.CreateWebHost("x", new[] { "mix", "chinook.sharpdata", });
        }

        [Test]
        public async Task Can_load_multitenancy()
        {
            SetProjectCurrentDirectory("apps\\multitenancy\\");
            var host = (await Startup.CreateWebHost("x", new string[0]))?.Build();
            host?.Run();
        }

        [Test]
        public void Can_view_CSV()
        {
            DeleteCreateAndSetDirectory("wip\\TestRepo");
            File.WriteAllText("test.csv","A,B\n1,2\n");
            var fullPath = Path.GetFullPath("test.csv");
            // fullPath.Print();
            var p = new Process {
                StartInfo = new ProcessStartInfo("excel", fullPath) {
                    UseShellExecute = true
                }
            };
            p.Start();
        }

        [Test]
        public async Task Can_override_user_appsettings()
        {
            // await Startup.CreateWebHost("x", new[]{ "open", "sharpdata", "db", "postgres", "db.connection", "$TECHSTACKS_DB" });
            await Startup.CreateWebHost("x", new[]{ "open", "sharpdata" });
        }

        [Test]
        public async Task Can_publish_northwind_sqlite()
        {
            Directory.SetCurrentDirectory("C:\\src\\mix");
            await Startup.CreateWebHost("x", new[]{ "run", "sqlite.ss" });
        }

        [Test]
        public async Task Can_mix_and_open_sharpdata_sharpapp()
        {
            DeleteCreateAndSetDirectory("wip\\TestRepo");
            //app open sharpdata -mix northwind.sqlite -db sqlite -db.connection northwind.sqlite
            // await Startup.CreateWebHost("x", new[]{ "open", "sharpdata", "mix", "northwind.sqlite", "-db", "sqlite", "-db.connection", "northwind.sqlite" });
            await Startup.CreateWebHost("x", new[]{ "open", "sharpdata", "mix", "dc49cbcf6178033500c19b80f2ec8c3a", "-token", Environment.GetEnvironmentVariable("GISTLYN_TOKEN") });
        }

        [Test]
        public async Task Can_create_shortcut()
        {
            Directory.SetCurrentDirectory("C:\\src\\netcore\\SharpData\\bin\\Release\\netcoreapp3.1\\publish");
            
            await Startup.CreateWebHost("app", new[]{ "shortcut", "SharpData.exe" }, new WebAppEvents {
                CreateShortcut = Shortcut.Create,
            });
        }

        [Test]
        public async Task Can_open_studio_verbose()
        {
            DeleteCreateAndSetDirectory("wip\\TestRepo");
            await Startup.CreateWebHost("app", new[]{ "open", "studio", "-debug" }, new WebAppEvents {
                CreateShortcut = Shortcut.Create,
            });
        }

        [Test]
        public async Task Can_open_sharpdata_verbose()
        {
            DeleteCreateAndSetDirectory("wip\\TestRepo");
            var host = (await Startup.CreateWebHost("app", new[]{ "open", "sharpdata", "-debug" }))?.Build();
            host?.Run();
        }

        [Test]
        public async Task Can_create_vuedesktop_project()
        {
            //app new vue-desktop VueApp
            Directory.SetCurrentDirectory(@"C:\projects\");
            try 
            {
                await Startup.CreateWebHost("x", new[]{ "new", "vue-desktop", "VueApp" });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        [Test]
        public async Task Can_create_shortcut_custom_target_and_arguments()
        {
            Directory.SetCurrentDirectory(@"C:\Users\mythz\apps\vuedesktop\dist");
            try 
            {
                await Startup.CreateWebHost("app", new[] {
                    "shortcut", 
                    "-target", 
                    "C:\\Program Files\\dotnet\\dotnet.exe",
                    "-arguments",
                    "%USERPROFILE%\\apps\\vuedesktop\\app\\app.dll %USERPROFILE%\\apps\\vuedesktop\\dist\\app.settings",
                    "-workdir",
                    "^%USERPROFILE^%\\apps\\vuedesktop\\dist",
                });
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        [Test]
        public async Task Can_open_mythz_spirals()
        {
            DeleteCreateAndSetDirectory("wip\\TestRepo");
            var host = (await Startup.CreateWebHost("app", new[]{ "open", "mythz/spirals" }))?.Build();
            host?.Run();
        }

        [Test]
        public void Can_run_TodoCrud_gists()
        {
            var gistId = "f111d10bd0b8de1f40cbd0f20c34a937";
            var response = $"https://localhost:5001/gists/{gistId}/run".PostJsonToUrl("");
            response.Print();
        }

        [Test]
        public void Can_get_rate_limits()
        {
            var gateway = new GitHubGateway(Environment.GetEnvironmentVariable("GITHUB_TOKEN"));
            var result = gateway.GetRateLimits();
            result.PrintDump();
        }

        [Test]
        public void Register_gist_URL_Scheme()
        {
            var rootKey = "gist";
            var exeName = "x.exe";
            
            var openKeys = new List<RegistryKey>();
            RegistryKey recordKey(RegistryKey key)
            {
                if (key != null) openKeys.Add(key);
                return key;
            }

            try
            {
                var appKey = recordKey(Registry.CurrentUser.OpenSubKey("Software")?.OpenSubKey("Classes")?.OpenSubKey(rootKey));
                if (appKey == null)
                {
                    var userRoot = recordKey(Registry.CurrentUser.OpenSubKey("Software", true))?
                        .OpenSubKey("Classes", true);   
                    var key = userRoot.CreateSubKey(rootKey);   
                    key.SetValue("URL Protocol", "gist.cafe");   
                    var profilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    var commandStr = Path.Combine(profilePath, ".dotnet", "tools", exeName) + " \"%1\"";
                    key.CreateSubKey(@"shell\open\command")?.SetValue("", commandStr);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
            finally
            {
                foreach (var key in openKeys)
                {
                    try
                    {
                        key.Close();
                    }
                    catch { }
                }
            }            
        }

        [Test]
        public async Task Can_mix_gist_version()
        {
            DeleteCreateAndSetDirectory("wip\\TestRepo");
            var host = (await Startup.CreateWebHost("x", new[] {
                "mix",
                "c71b3f0123b3d9d08c1b11c98c2ff379/54e50e17bb9486eb23469c0bee77d9c518d32a37"
            }))?.Build();
        }

        [Test]
        public async Task Can_mix_gist_version_url()
        {
            DeleteCreateAndSetDirectory("wip\\TestRepo");
            var host = (await Startup.CreateWebHost("x", new[] {
                "mix",
                "https://gist.github.com/gistlyn/c71b3f0123b3d9d08c1b11c98c2ff379/54e50e17bb9486eb23469c0bee77d9c518d32a37"
            }))?.Build();
        }

        [Test]
        public async Task Can_open_gist_version_url()
        {
            DeleteCreateAndSetDirectory("wip\\TestRepo");

            var url = "gist://c3d8523540a2ade806e156a3a8138c54/cbddc54dbea64c6f5845290693e6075cf010481e";
            //var url = "gist://c3d8523540a2ade806e156a3a8138c54/cbddc54dbea64c6f5845290693e6075cf010481e?out=Node.js%20Hello%20World";
            var args = url.ConvertUrlSchemeToCommands("gist-open").ToArray();
            var host = (await Startup.CreateWebHost("x", args))?.Build();
        }

        [Test]
        public async Task Can_open_gist_apps_url()
        {
            var url = "gist://apps.servicestack.net/gists/techstacks.io/swift/FindTechnologies(VendorName:Google,Take:5,OrderByDesc:ViewCount,Fields:%22Name,ProductUrl,Tier,VendorName%22)?use=xcode&name=MyApp";
            var args = url.ConvertUrlSchemeToCommands("gist-open").ToArray();
            var host = (await Startup.CreateWebHost("x", args))?.Build();
        }

        [Test]
        public async Task Can_open_http_gist()
        {
            var url = "https://localhost:5002/gists/techstacks.io/csharp/GetTechnology";
            var args = url.ConvertUrlSchemeToCommands("gist-open").ToArray();
            var host = (await Startup.CreateWebHost("x", args))?.Build();
        }

        [Test]
        public async Task Can_open_http_gist_out()
        {
            var url = "https://localhost:5002/gists/techstacks.io/csharp/GetTechnology?out=Acme";
            var args = url.ConvertUrlSchemeToCommands("gist-open").ToArray();
            var host = (await Startup.CreateWebHost("x", args))?.Build();
        }

        [Test]
        public async Task Can_open_http_gist_name()
        {
            var url = "https://localhost:5002/gists/techstacks.io/csharp/GetTechnology?name=Acme";
            var args = url.ConvertUrlSchemeToCommands("gist-open").ToArray();
            var host = (await Startup.CreateWebHost("x", args))?.Build();
        }

        [Test]
        public async Task Can_open_http_gist_name_out()
        {
            var url = "https://localhost:5002/gists/techstacks.io/csharp/GetTechnology?name=TheProject&out=Acme";
            var args = url.ConvertUrlSchemeToCommands("gist-open").ToArray();
            var host = (await Startup.CreateWebHost("x", args))?.Build();
        }

        [Test]
        public async Task Can_open_http_gist_request_with_args()
        {
            var url = "https://localhost:5002/gists/techstacks.io/csharp/QueryPosts(Ids=[1001,6860,6848],Tier=Server,OrderBy=-Points,Take=3)";
            var args = url.ConvertUrlSchemeToCommands("gist-open").ToArray();
            var host = (await Startup.CreateWebHost("x", args))?.Build();
        }

        [Test]
        public async Task Can_open_http_gist_request_with_args_with_use()
        {
            // var use = "code";
            // var use = "folder";
            // var use = "vs";
            //var use = "rider";
            var use = "webstorm";
            var url = $"https://localhost:5002/gists/techstacks.io/typescript/QueryPosts(Ids=[1001,6860,6848],Tier=Server,OrderBy=-Points,Take=3)?name=Acme&use={use}";
            var args = url.ConvertUrlSchemeToCommands("gist-open").ToArray();
            var host = (await Startup.CreateWebHost("x", args))?.Build();
        }

        [Test]
        public async Task Can_open_http_gist_encoded()
        {
            //gist://localhost:5001/serviceref/csharp/GetTechnology/techstacks.io
            DeleteCreateAndSetDirectory("wip\\TestRepo");
            // var url = "gist://localhost:5001/serviceref/csharp/techstacks.io/GetTechnology";
            //var url = "gist://localhost:5001/serviceref/csharp/http.localhost:8000/GetTechnology";
            var url = "https%3A%2F%2Flocalhost%3A5002%2Fgists%2Ftechstacks.io%2Fcsharp%2FGetTechnology";
            var args = url.ConvertUrlSchemeToCommands("gist-open").ToArray();
            var host = (await Startup.CreateWebHost("x", args))?.Build();
        }

        [Test]
        public async Task Can_mix_alternative_gist_url()
        {
            //gist://localhost:5002/gists/techstacks.io/csharp/GetTechnology
            DeleteCreateAndSetDirectory("wip\\TestRepo");
            var url = "https://localhost:5002/gists/techstacks.io/csharp/GetTechnology";
            var host = (await Startup.CreateWebHost("x", new[] {
                "mix",
                url
            }))?.Build();
        }

        [Test]
        public async Task Can_delete_mix_alternative_gist_url()
        {
            //gist://localhost:5001/serviceref/csharp/GetTechnology/techstacks.io
            DeleteCreateAndSetDirectory("wip\\TestRepo");
            var url = "https://localhost:5001/serviceref/csharp/techstacks.io/GetTechnology";
            var host = (await Startup.CreateWebHost("x", new[] {
                "mix",
                "-delete",
                url
            }))?.Build();
        }

        [Test]
        public async Task Can_run_packagejson_script()
        {
            var url = "https://localhost:5002/gists/techstacks.io/csharp/GetTechnology";
            var host = (await Startup.CreateWebHost("x", new[] {
                "scripts",
                "echo",
                url
            }))?.Build();
        }

        [Test]
        public async Task Can_eval_expression()
        {
            var host = (await Startup.CreateWebHost("x", new[] {
                "-e",
                "\"now; \n now\"",
            }))?.Build();
        }

        [Test]
        public async Task Does_list_repos()
        {
            var host = (await Startup.CreateWebHost("x", new[] {
                "download",
                "gistcafe",
            }))?.Build();
        }

        [Test]
        public async Task Does_create_project_template_into_current_dir_by_default()
        {
            Directory.SetCurrentDirectory("wip");
            await Startup.CreateWebHost("x", new[]{ "new", "web", "Chinook" });
        }

        [Test]
        public async Task Does_create_project_template_into_out_dir()
        {
            Directory.SetCurrentDirectory("wip");
            await Startup.CreateWebHost("x", new[]{ "new", "web", "Chinook", "-out", "src" });
        }

        [Test]
        public async Task Can_generate_jupyter_notebook()
        {
            Directory.SetCurrentDirectory("wip");
            await Startup.CreateWebHost("x", new[]{ "jupyter", "https://techstacks.io" });
        }

        [Test]
        public async Task Can_generate_jupyter_notebook_with_API()
        {
            Directory.SetCurrentDirectory("wip");
            await Startup.CreateWebHost("x", new[]{ "jupyter", "https://techstacks.io", "AppOverview" });
        }

        [Test]
        public async Task Can_generate_jupyter_notebook_with_API_and_out()
        {
            Directory.SetCurrentDirectory("wip");
            await Startup.CreateWebHost("x", new[]{ "jupyter", "https://techstacks.io", "AppOverview", "-out", "techstacks-custom" });
            // await Startup.CreateWebHost("x", new[]{ "jupyter", "https://techstacks.io", "FindTechnologies", "{Ids:[1,2,3],VendorName:'Google'}", "-out", "techstacks-custom-find" });
        }

        [Test]
        public async Task Can_generate_jupyter_notebook_with_complex_API()
        {
            Directory.SetCurrentDirectory("wip");
            await Startup.CreateWebHost("x", new[]{ "jupyter", "https://techstacks.io", "FindTechnologies", "{Ids:[1,2,3],VendorName:'Google'}" });
        }

        [Test]
        public async Task Can_get_info_from_baseurl()
        {
            Directory.SetCurrentDirectory("wip");
            // await Startup.CreateWebHost("x", new[]{ "info", "https://techstacks.io" });
            await Startup.CreateWebHost("x", new[]{ "info", "https://covid-vac-watch.netcore.io", "GetLocations" });
        }

        [Test]
        public async Task Can_call_API_with_spaces()
        {
            Directory.SetCurrentDirectory("wip");
            // await Startup.CreateWebHost("x", new[]{ "send", "https://covid-vac-watch.netcore.io", "QueryVaccinationRates", "{Location:'New Jersey'}" });
            await Startup.CreateWebHost("x", new[]{ "send", "https://covid-vac-watch.netcore.io", "QueryVaccinationRates", "{Location:'New", "Jersey'}", "-raw" });
        }

        [Test]
        public async Task Can_call_complex_StoreLogs_API()
        {
            Directory.SetCurrentDirectory("wip");
            // await Startup.CreateWebHost("x", new[]{ "send", "https://covid-vac-watch.netcore.io", "QueryVaccinationRates", "{Location:'New Jersey'}" });
            await Startup.CreateWebHost("x", new[]{ "GET", "http://test.servicestack.net", "StoreLogs", 
                "{Loggers:[{Id:786,Devices:[{Id:5955,Type:Panel,Channels:[{Name:Temperature,Value:58},{Name:Status,Value:On}]}]}]}", "-raw" });
        }

        [Test]
        public void Adhoc()
        {
            try
            {
                var dir = @"C:\src\gistcafe\gistcafe\src\GistRun\App_Data\scratch\localhost_5002_gists_test.servicestack.net_csharp_AllTypes(Id_1,NullableId_2,Byte_3,Short_4,Int_5,Long_6,UShort_7,UInt_8,ULong_9,Float_10,Double_11,Decimal_12,String_String,DateTime_2021-12-31T11_59,TimeSpan_01_02_03,Guid_bc525c18-ac21-4a60-a2be-6187c9af2716,Char_a,StringList_[A,C,C],StringArray_[a,2,3])";
                Directory.CreateDirectory(dir);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

    }
}