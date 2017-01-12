using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Hangfire.Common;
using Hangfire.Core.Dashboard.Management.Metadata;
using Hangfire.Core.Dashboard.Management.Support;
using Hangfire.Dashboard;
using Hangfire.Dashboard.Pages;
using Hangfire.Server;
using Hangfire.States;

namespace Hangfire.Core.Dashboard.Management.Pages
{
    public class ManagementBasePage : RazorPage
    {
        private readonly string pageTitle;
        private readonly string pageHeader;
        private readonly string queue;
        
        protected internal ManagementBasePage(string pageTitle, string pageHeader, string queue)
        {
            this.pageTitle = pageTitle;
            this.pageHeader = pageHeader;
            this.queue = queue;
        }

        protected virtual void Content()
        {
            var jobs = JobsHelper.Metadata.Where(j => j.Queue.Contains(queue));

            foreach (var jobMetadata in jobs)
            {
                var route = $"{ManagementPage.UrlRoute}/{queue}/{jobMetadata.DisplayName.Replace(" ", string.Empty)}";
                var id = $"{jobMetadata.DisplayName.Replace(" ", string.Empty)}";

                if (jobMetadata.MethodInfo.GetParameters().Length > 1)
                {

                    string inputs = string.Empty;

                    foreach (var parameterInfo in jobMetadata.MethodInfo.GetParameters())
                    {
                        if (parameterInfo.ParameterType == typeof(PerformContext) || parameterInfo.ParameterType == typeof(IJobCancellationToken))
                            continue;

                        DisplayDataAttribute displayInfo = null;
                        if (parameterInfo.GetCustomAttributes(true).OfType<DisplayDataAttribute>().Any())
                        {
                            displayInfo = parameterInfo.GetCustomAttribute<DisplayDataAttribute>();
                            
                        }
                        
                        var myId = $"{id}_{parameterInfo.Name}";
                        if (parameterInfo.ParameterType == typeof(string))
                        {
                            inputs += InputTextbox(myId, displayInfo?.LabelText??parameterInfo.Name, displayInfo?.PlaceholderText??parameterInfo.Name);
                        }
                        else if (parameterInfo.ParameterType == typeof(int))
                        {
                            inputs += InputNumberbox(myId, displayInfo?.LabelText ?? parameterInfo.Name, displayInfo?.PlaceholderText ?? parameterInfo.Name);
                        }
                        else if (parameterInfo.ParameterType == typeof(DateTime))
                        {
                            inputs += InputDatebox(myId, displayInfo?.LabelText ?? parameterInfo.Name, displayInfo?.PlaceholderText ?? parameterInfo.Name);
                        }
                        else if (parameterInfo.ParameterType == typeof(bool))
                        {
                            inputs += "<br/>" + InputCheckbox(myId, displayInfo?.LabelText ?? parameterInfo.Name, displayInfo?.PlaceholderText ?? parameterInfo.Name);
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }
                    }

                    Panel(id, jobMetadata.DisplayName, jobMetadata.Description, inputs, CreateButtons(route, "Enqueue", "enqueueing", id));

                }
                else
                {
                    Panel(id, jobMetadata.DisplayName, jobMetadata.Description, string.Empty, CreateButtons(route, "Enqueue", "enqueueing", id));

                }

            }

            WriteLiteral("\r\n<script src=\"");
            Write(Url.To($"/jsm"));
            WriteLiteral("\"></script>\r\n");

        }

        public static void AddCommands(string queue)
        {
            var jobs = JobsHelper.Metadata.Where(j => j.Queue.Contains(queue));

            foreach (var jobMetadata in jobs)
            {
                var route = $"{ManagementPage.UrlRoute}/{queue}/{jobMetadata.DisplayName.Replace(" ", string.Empty)}";

                DashboardRoutes.Routes.AddCommand(route, context =>
                {
                    var par = new List<object>();
                    var schedule = Task.Run(() => context.Request.GetFormValuesAsync($"{jobMetadata.DisplayName.Replace(" ", string.Empty)}_schedule")).Result.FirstOrDefault();
                    var cron = Task.Run(() => context.Request.GetFormValuesAsync($"{jobMetadata.DisplayName.Replace(" ", string.Empty)}_cron")).Result.FirstOrDefault();

                    foreach (var parameterInfo in jobMetadata.MethodInfo.GetParameters())
                    {
                        if (parameterInfo.ParameterType == typeof(PerformContext) || parameterInfo.ParameterType == typeof(IJobCancellationToken))
                        {
                            par.Add(null);
                            continue;
                        };

                        var variable = $"{jobMetadata.DisplayName.Replace(" ", string.Empty)}_{parameterInfo.Name}";
                        if (parameterInfo.ParameterType == typeof(DateTime))
                        {
                            variable = $"{variable}_datetimepicker";
                        }

                        var t = Task.Run(() => context.Request.GetFormValuesAsync(variable)).Result;

                        object item = null;
                        var formInput = t.FirstOrDefault();
                        if (parameterInfo.ParameterType == typeof(string))
                        {
                            item = formInput;
                        }
                        else if (parameterInfo.ParameterType == typeof(int))
                        {
                            if (formInput != null) item = int.Parse(formInput);
                        }
                        else if (parameterInfo.ParameterType == typeof(DateTime))
                        {
                            item = formInput == null ? DateTime.MinValue : DateTime.Parse(formInput);
                        }
                        else if (parameterInfo.ParameterType == typeof(bool))
                        {
                            item = formInput == "on";
                        }
                        else
                        {
                            throw new NotImplementedException();
                        }

                        par.Add(item);

                    }

                    var job = new Job(jobMetadata.Type, jobMetadata.MethodInfo, par.ToArray());

                    var client = new BackgroundJobClient(context.Storage);

                    if (!string.IsNullOrEmpty(schedule))
                    {
                        var minutes = int.Parse(schedule);
                        return client.Create(job, new ScheduledState(new TimeSpan(0, 0, minutes, 0))) != string.Empty;
                    }
                    else if (!string.IsNullOrEmpty(cron))
                    {
                        var manager = new RecurringJobManager(context.Storage);
                        try
                        {
                            manager.AddOrUpdate(jobMetadata.DisplayName, job, cron, TimeZoneInfo.Utc, queue);
                        }
                        catch (Exception)
                        {
                            return false;
                        }
                        return true;
                    }

                    var jobId = client.Create(job, new EnqueuedState(jobMetadata.Queue));
                    return jobId != string.Empty;
                });
            }
        }

        public override void Execute()
        {
            WriteLiteral("\r\n");
            Layout = new LayoutPage(pageTitle);

            WriteLiteral("<div class=\"row\">\r\n");
            WriteLiteral("<div class=\"col-md-3\">\r\n");

            Write(Html.RenderPartial(new CustomSidebarMenu(ManagementSidebarMenu.Items)));

            WriteLiteral("</div>\r\n");
            WriteLiteral("<div class=\"col-md-9\">\r\n");
            WriteLiteral("<h1 class=\"page-header\">\r\n");
            Write(pageHeader);
            WriteLiteral("</h1>\r\n");

            Content();

            WriteLiteral("\r\n</div>\r\n");
            WriteLiteral("\r\n</div>\r\n");
        }

        protected void Panel(string id, string heading, string description, string content, string buttons)
        {
            WriteLiteral($@"<div class=""panel panel-info js-management"">
                              <div class=""panel-heading"">{heading}</div>
                              <div class=""panel-body"">
                                <p>{description}</p>
                              </div>
                              <div class=""panel-body"">");

            if (!string.IsNullOrEmpty(content))
            {
                WriteLiteral($@"<div class=""well""> 
                                    { content}
                                </div>      
                                                     
                              ");
            }

            WriteLiteral($@"<div id=""{id}_error"" ></div>  
                            </div>
                            <div class=""panel-footer clearfix "">
                                <div class=""pull-right"">
                                    { buttons}
                                </div>
                              </div>
                            </div>");
        }

        protected string CreateButtons(string url, string text, string loadingText, string id)
        {
            return $@" 

                        <div class=""col-sm-2 pull-right"">
                            <button class=""js-management-input-commands btn btn-sm btn-success"" 
                                    data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"" input-id=""{id}""> 
                                <span class=""glyphicon glyphicon-play-circle""></span>
                                &nbsp;Enqueue
                            </button>
                        </div>
                        <div class=""btn-group col-3 pull-right"">
                            <button type=""button"" class=""btn btn-info btn-sm dropdown-toggle"" data-toggle=""dropdown"" aria-haspopup=""true"" aria-expanded=""false"">
                                Schedule &nbsp;
                                <span class=""caret""></span>
                            </button>
                                
                            <ul class=""dropdown-menu"">
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""5""  
                                    data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">5 min</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""10""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">10 min</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""15""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">15 min</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""30""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">30 min</a></li>
                                <li><a href=""#"" class=""js-management-input-commands"" input-id=""{id}"" schedule=""60""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">60 min</a></li>

                            </ul>
                        </div>

                        <div class=""col-sm-5 pull-right"">
                            <div class=""input-group input-group-sm"">
                                <input type=""text"" class=""form-control"" placeholder=""Enter a cron expression * * * * *"" id=""{id}_cron"">
                                <span class=""input-group-btn "">
                                <button class=""btn btn-default btn-sm btn-warning js-management-input-commands"" type=""button"" input-id=""{id}""
                                     data-url=""{Url.To(url)}"" data-loading-text=""{loadingText}"">
                                    <span class=""glyphicon glyphicon-repeat""></span>
                                    &nbsp;Add Recurring</button>
                                </span>
                            </div>
                        </div>
                       ";
        }

        private string Input(string id, string labelText, string placeholderText, string inputtype)
        {
            return $@"
                    <div class=""form-group"">
                        <label for=""{id}"" class=""control-label"">{labelText}</label>
                        <input type=""{inputtype}"" placeholder=""{placeholderText}"" id=""{id}"" >
                    </div>
            ";
        }

        protected string InputTextbox(string id, string labelText, string placeholderText)
        {
            return Input(id, labelText, placeholderText, "text");
        }
        protected string InputNumberbox(string id, string labelText, string placeholderText)
        {
            return Input(id, labelText, placeholderText, "number");
        }

        protected string InputDatebox(string id, string labelText, string placeholderText)
        {
            return $@"
                    <div class=""form-group"">
                        <label for=""{id}"" class=""control-label"">{labelText}</label>
                        <div class='input-group date' id='{id}_datetimepicker'>
                            <input type='text' class=""form-control"" placeholder=""{placeholderText}"" />
                            <span class=""input-group-addon"">
                                <span class=""glyphicon glyphicon-calendar""></span>
                            </span>
                        </div>
                    </div>";

        }

        protected string InputCheckbox(string id, string labelText, string placeholderText)
        {
            return $@"
                        <div class=""form-group"">
                            <div class=""checkbox"">
                              <label>
                                <input type=""checkbox"" id=""{id}"">
                                {labelText}
                              </label>                             
                            </div>
                        </div>
            ";
        }

    }
}