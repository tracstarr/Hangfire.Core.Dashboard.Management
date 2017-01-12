using System;
using System.Reflection;
using System.Threading.Tasks;
using Hangfire.Dashboard;

namespace Hangfire.Core.Dashboard.Management.Support
{
    internal class EmbeddedResourceDispatcher : IDashboardDispatcher
    {
        private readonly Assembly assembly;
        private readonly string resourceName;
        private readonly string contentType;

        public EmbeddedResourceDispatcher(Assembly assembly, string resourceName, string contentType = null)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));
            if (string.IsNullOrEmpty(resourceName))
                throw new ArgumentNullException(nameof(resourceName));

            this.assembly = assembly;
            this.resourceName = resourceName;
            this.contentType = contentType;
        }

        public Task Dispatch(DashboardContext context)
        {
            if (!string.IsNullOrEmpty(contentType))
            {
                var contentType = context.Response.ContentType;

                if (string.IsNullOrEmpty(contentType))
                {
                    // content type not yet set
                    context.Response.ContentType = this.contentType;
                }
                else if (contentType != this.contentType)
                {
                    // content type already set, but doesn't match ours
                    throw new InvalidOperationException($"ContentType '{this.contentType}' conflicts with '{context.Response.ContentType}'");
                }
            }

            return WriteResourceAsync(context.Response, assembly, resourceName);
        }

        private static async Task WriteResourceAsync(DashboardResponse response, Assembly assembly, string resourceName)
        {
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                    throw new ArgumentException($@"Resource '{resourceName}' not found in assembly {assembly}.");

                await stream.CopyToAsync(response.Body);
            }
        }
    }
}