using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Icklewik.Core;
using Nancy;
using Nancy.Bootstrapper;
using Nancy.Conventions;
using Nancy.Diagnostics;
using Nancy.TinyIoc;

namespace Icklekwik.Server
{
    public class WikiBootstrapper : DefaultNancyBootstrapper
    {
        private ServerConfig config;
        private bool enableDiagnostics;
        private string diagnosticsPassword;

        public WikiBootstrapper(ServerConfig config, bool enableDiagnostics = false, string diagnosticsPassword = "")
            : base()
        {
            this.config = config;
            this.enableDiagnostics = enableDiagnostics;
            this.diagnosticsPassword = diagnosticsPassword;
        }

        protected override void ApplicationStartup(TinyIoCContainer container, IPipelines pipelines)
        {
            if (!enableDiagnostics)
            {
                DiagnosticsHook.Disable(pipelines);
            }

            container.Register<ServerConfig>(config);
        }

        protected override void ConfigureConventions(NancyConventions conventions)
        {
            base.ConfigureConventions(conventions);

            conventions.StaticContentsConventions.Add(
                StaticContentConventionBuilder.AddDirectory("styles", @"Static/Styles")
            );

            conventions.StaticContentsConventions.Add(
                StaticContentConventionBuilder.AddDirectory("scripts", @"Static/Scripts")
            );

            conventions.StaticContentsConventions.Add(
                StaticContentConventionBuilder.AddDirectory("img", @"Static/Images")
            );
        }

        protected override DiagnosticsConfiguration DiagnosticsConfiguration
        {
            get { return new DiagnosticsConfiguration { Password = diagnosticsPassword }; }
        }
    }
}
