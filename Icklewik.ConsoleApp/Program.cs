using System;
using System.IO;
using Icklewik.Core;

namespace Icklewik.ConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            File.WriteAllText("./index.md", "Hello World");

            Directory.CreateDirectory(Path.Combine(".", "subdir"));

            WikiSite site = new WikiSite(new WikiConfig()
                {
                    SiteName = "Tester",
                    RootSourcePath = ".", 
                    RootWikiPath = Path.Combine(".", "wiki"),
                    Convertor = new Convertor(new MarkdownSharpDialogue())
                });

            // add another page
            File.WriteAllText("./firstFile.md", "Hello Again");

            // and another
            File.WriteAllText(Path.Combine(".", "subdir", "index.md"), "Hello World Sub Directory");

            // then delete one
            File.Delete("./index.md");

            System.Threading.Thread.Sleep(1000000);

            Console.Write("Done");
        }
    }
}
