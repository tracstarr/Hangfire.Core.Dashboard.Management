using System;
using System.Reflection;

namespace Hangfire.Core.Dashboard.Management.Metadata
{
    public class JobMetadata
    {
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Queue { get; set; }
        public Type Type { get; set; }
        public MethodInfo MethodInfo { get; set; }
    }
}