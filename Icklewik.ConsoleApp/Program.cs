using System;
using System.IO;
using Icklekwik.Core.Cache;
using Icklewik.Core;
using Icklewik.Core.Model;
using Icklewik.Core.Site;
using Icklewik.Core.Source;
using System.Threading;

namespace Icklewik.ConsoleApp
{
    class Program
    {
        static AutoResetEvent waitHandle = new AutoResetEvent(false);

        static void Main(string[] args)
        {
            string scanDirectory = Path.GetFullPath(args[0]);
            string outputDirectory = Path.GetFullPath(args[1]);

            Convertor convertor = new Convertor(new MarkdownSharpDialogue());
            WikiSite site = new WikiSite(
                new WikiConfig()
                {
                    SiteName = "Tester",
                    RootSourcePath = scanDirectory,
                    RootWikiPath = outputDirectory,
                    Convertor = new Convertor(new MarkdownSharpDialogue())
                }, 
                new MasterRepository(convertor.FileExtension),
                new NullSourceWatcher(),
                new PageCache());

            site.InitialisationComplete += site_InitialisationComplete;

            site.Start();

            waitHandle.WaitOne();

            site.Dispose();

            Console.Write("Done");
        }

        static void site_InitialisationComplete(object sender, EventArgs e)
        {
            waitHandle.Set();
        }
    }
}
