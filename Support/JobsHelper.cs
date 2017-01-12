using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Hangfire.Core.Dashboard.Management.Metadata;

namespace Hangfire.Core.Dashboard.Management.Support
{
    public static class JobsHelper
    {
        public static List<JobMetadata> Metadata { get; private set; }
        internal static List<ManagementPageAttribute> Pages { get; set; }

        internal static void GetAllJobs(Assembly assembly)
        {
            Metadata = new List<JobMetadata>();
            Pages = new List<ManagementPageAttribute>();

            foreach (Type ti in  assembly.GetTypes().Where(x => x.IsInterface && typeof(IJob).IsAssignableFrom(x) && x.Name != (typeof(IJob).Name)))
            {
                var q="default";

                if (ti.GetCustomAttributes(true).OfType<ManagementPageAttribute>().Any())
                {
                    var attr = ti.GetCustomAttribute<ManagementPageAttribute>();
                    q =  attr.Queue;
                    Pages.Add(attr);
                }
                

                foreach (var methodInfo in ti.GetMethods())
                {
                    var meta = new JobMetadata { Type = ti, Queue = q};

                    if (methodInfo.GetCustomAttributes(true).OfType<DescriptionAttribute>().Any())
                    {
                        meta.Description = methodInfo.GetCustomAttribute<DescriptionAttribute>().Description;
                    }

                    if (methodInfo.GetCustomAttributes(true).OfType<DisplayNameAttribute>().Any())
                    {
                        meta.MethodInfo = methodInfo;
                        meta.DisplayName = methodInfo.GetCustomAttribute<DisplayNameAttribute>().DisplayName;
                    }

                    Metadata.Add(meta);
                }
            }
        }
    }
}
