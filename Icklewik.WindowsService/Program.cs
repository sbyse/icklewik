using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration.Install;
using System.IO;
using System.ServiceProcess;
using Icklekwik.Server;
using Icklewik.Core;
using Nancy.Hosting.Self;

namespace Icklewik.WindowsService
{
    class Program
    {
        private static ServerStartup startup;

        static int Main(string[] args)
        {
            bool install = false, uninstall = false, console = false, rethrow = false;
            try
            {
                foreach (string arg in args)
                {
                    switch (arg)
                    {
                        case "-i":
                        case "-install":
                            install = true; break;
                        case "-u":
                        case "-uninstall":
                            uninstall = true; break;
                        case "-c":
                        case "-console":
                            console = true; break;
                        default:
                            Console.Error.WriteLine("Argument not expected: " + arg);
                            break;
                    }
                }

                if (uninstall)
                {
                    Install(true, args);
                }
                if (install)
                {
                    Install(false, args);
                }
                if (console)
                {
                    Console.WriteLine("Starting...");
                    StartUp(args);
                    Console.WriteLine("System running; press any keyto stop");
                    Console.ReadKey(true);
                    ShutDown();
                    Console.WriteLine("System stopped");
                }
                else if (!(install || uninstall))
                {
                    rethrow = true; // so that windows sees error...
                    ServiceBase[] services = { new Service(StartUp, ShutDown) };
                    ServiceBase.Run(services);
                    rethrow = false;
                }
                return 0;
            }
            catch (Exception ex)
            {
                if (rethrow) throw;
                Console.Error.WriteLine(ex.Message);
                return -1;
            }
        }

        static void Install(bool undo, string[] args)
        {
            try
            {
                Console.WriteLine(undo ? "uninstalling" : "installing");
                using (AssemblyInstaller inst = new AssemblyInstaller(typeof(Program).Assembly, args))
                {
                    IDictionary state = new Hashtable();
                    inst.UseNewContext = true;
                    try
                    {
                        if (undo)
                        {
                            inst.Uninstall(state);
                        }
                        else
                        {
                            inst.Install(state);
                            inst.Commit(state);
                        }
                    }
                    catch
                    {
                        try
                        {
                            inst.Rollback(state);
                        }
                        catch { }
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
            }
        }

        static void StartUp(string[] args)
        {
            string coreDirectory = "D:\\ickletest";

            File.WriteAllText(Path.Combine(coreDirectory, "index.md"), "Hello World");

            Directory.CreateDirectory(Path.Combine(coreDirectory, "subdir"));

            // reference the bootstrapper (and turn on diagnostics)
            ServerConfig serverConfig = new ServerConfig(
                new List<WikiConfig>() 
                { 
                    new WikiConfig()
                    {
                        SiteName = "Tester",
                        RootSourcePath = coreDirectory,
                        RootWikiPath = Path.Combine(coreDirectory, "wiki"),
                        Convertor = new Convertor(new MarkdownSharpDialogue())
                    } 
                });

            // basic startup pattern taken from: 
            // https://github.com/loudej/owin-samples/blob/master/src/ConsoleNancySignalR/Program.cs
            // TODO: All this Gate/Owin stuff seems to be up in the air so this code needs reviewing
            // at some point

            startup = new ServerStartup(serverConfig, true, "password"); // this program's Startup.cs class
            
            startup.Start();

            // add another page
            File.WriteAllText(Path.Combine(coreDirectory, "firstFile.md"), "Hello Again");

            // and another
            File.WriteAllText(Path.Combine(coreDirectory, "subdir", "index.md"), "Hello World Sub Directory");

            // then delete one
            File.Delete(Path.Combine(coreDirectory, "index.md"));
        }

        static void ShutDown()
        {
            startup.Dispose();
        }
    }
}
