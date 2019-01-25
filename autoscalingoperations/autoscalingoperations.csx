using System.Linq;

private static string GetEstimateScaleRuleQuery(OperationContext<App> cxt, dynamic observerSite)
{
    var subscriptionId = cxt.Resource.SubscriptionId;    
    var resourceGroup = cxt.Resource.ResourceGroup;
    var siteName = cxt.Resource.Name;
    var serverFarm = (string)observerSite.server_farm.server_farm_name;

    string s =  $@"cluster('azureinsights').database('Insights').JobTraces 
    | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime)} 
    | where jobPartition  == '{subscriptionId}'
    | where message has '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Web/serverfarms/{serverFarm}'
    | where operationName == 'EstimateScaleRule' 
    | parse message with * ""with name '"" MetricName ""', namespace '', statistic '"" Statistic ""', timegrain '"" TimeGrain ""', timewindow '"" TimeWindow ""', timeaggregation '"" Aggregation ""', operator '"" Operator ""' and threshold '"" Threshold ""' estimated as '"" Status ""' with value projected '"" ProjectedValue ""' and projection '"" Projection ""'"" *
    | project PreciseTimeStamp, ActivityId  , MetricName , Statistic , TimeGrain , TimeWindow , Aggregation, Operator , Threshold , Status , Projection, ProjectedValue, message 
    | summarize by bin(PreciseTimeStamp, 1m), ActivityId  , MetricName , Statistic , TimeGrain , TimeWindow , Aggregation, Operator , Threshold , Status , Projection, ProjectedValue, message
    | join kind= rightouter (
                cluster('azureinsights').database('Insights').JobTraces 
                | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime)}
                | where jobPartition  == '{subscriptionId}'  
                | where message has '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Web/serverfarms/{serverFarm}'
                | where operationName == 'AutoscaleResource' and message startswith 'AutoscaleResource: current instances count'
                | parse message with * "" current instances count '"" CurrentInstanceCount: long ""' is"" * ""estimated scale "" Direction "" capacity '"" ProjectedInstanceCount:long ""' for"" *
                | summarize by  bin(PreciseTimeStamp, 1m), ActivityId , operationName , CurrentInstanceCount, Direction, ProjectedInstanceCount
                ) on ActivityId 
    | project PreciseTimeStamp , InstanceCount=CurrentInstanceCount, Direction, NewInstanceCount=ProjectedInstanceCount,  Rule = strcat(MetricName ,' ', Operator,' ',Threshold), Operator, Status, ProjectedValue
    | where Status == 'Triggered' and Direction == 'up'
    | order by PreciseTimeStamp asc , Operator asc
    | project-away Operator";

    return s;
}

private static string GetEstimateScaleInRuleQuery(OperationContext<App> cxt, dynamic observerSite)
{
    var subscriptionId = cxt.Resource.SubscriptionId;    
    var resourceGroup = cxt.Resource.ResourceGroup;
    var siteName = cxt.Resource.Name;
    var serverFarm = (string)observerSite.server_farm.server_farm_name;

    string s =  $@"cluster('azureinsights').database('Insights').JobTraces 
    | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime)} 
    | where jobPartition  == '{subscriptionId}'
    | where message has '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Web/serverfarms/{serverFarm}'
    | where operationName == 'EstimateScaleRule' 
    | parse message with * ""with name '"" MetricName ""', namespace '', statistic '"" Statistic ""', timegrain '"" TimeGrain ""', timewindow '"" TimeWindow ""', timeaggregation '"" Aggregation ""', operator '"" Operator ""' and threshold '"" Threshold ""' estimated as '"" Status ""' with value"" * ""and projection '"" Projection ""'"" *
    | project PreciseTimeStamp, ActivityId  , MetricName , Statistic , TimeGrain , TimeWindow , Aggregation, Operator , Threshold , Status , Projection, message 
    | summarize by bin(PreciseTimeStamp, 1m), ActivityId  , MetricName , Statistic , TimeGrain , TimeWindow , Aggregation, Operator , Threshold , Status , Projection, message
    | join kind= rightouter (
                cluster('azureinsights').database('Insights').JobTraces 
                | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime)}
                | where jobPartition  == '{subscriptionId}'  
                | where message has '/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Web/serverfarms/{serverFarm}'
                | where operationName == 'AutoscaleResource' and message startswith 'AutoscaleResource: current instances count'
                | parse message with * "" current instances count '"" CurrentInstanceCount: long ""' is"" * ""estimated scale "" Direction "" capacity '"" ProjectedInstanceCount:long ""' for"" *
                | summarize by  bin(PreciseTimeStamp, 1m), ActivityId , operationName , CurrentInstanceCount, Direction, ProjectedInstanceCount
                ) on ActivityId 
    | project PreciseTimeStamp , ActivityId, InstanceCount=CurrentInstanceCount, Direction, NewInstanceCount=ProjectedInstanceCount,  Rule = strcat(MetricName ,' ', Operator,' ',Threshold), Operator, Status
    | summarize Rules=makeset(Rule) by ActivityId , PreciseTimeStamp, InstanceCount, NewInstanceCount, Status, Operator
    | order by PreciseTimeStamp asc , Operator asc
    | project-away Operator, ActivityId";

    return s;
}

private static string GetAutoScaleProfileQuery(OperationContext<App> cxt, dynamic observerSite)
{
    var subscriptionId = cxt.Resource.SubscriptionId;    
    var resourceGroup = cxt.Resource.ResourceGroup;
    var siteName = cxt.Resource.Name;
    var serverFarm = (string)observerSite.server_farm.server_farm_name;

    return $@"cluster('azureinsights').database('Insights').JobTraces 
    | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime)} 
    | where jobPartition  =~ '{subscriptionId}' and operationName =~ 'GetAutoscaleProfile' 
    | parse message with * ""Profile: '"" Profile ""'"" * 
    | where message contains '{serverFarm}'
    | summarize count() by Profile 
    | project Profile";
}

private static string GetCurrentInstanceViewQuery(OperationContext<App> cxt, dynamic observerSite)
{
    var siteName = cxt.Resource.Name;
    
    //TODO: Use the Site Runtime name here. This will break if the Web App has multiple slots
    var siteNameFilter = $"ApplicationPool =~ '{siteName}' or ApplicationPool startswith '{siteName}__'";

    return $@"StatsDWASWorkerProcessTenMinuteTable
        | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource, "TIMESTAMP")}
        | where {siteNameFilter}
        | make-series count() on TIMESTAMP in range(datetime({cxt.StartTime}), datetime({cxt.EndTime}), 5m)
        | project Instances = series_fill_linear(count_, 0), TIMESTAMP
        | mvexpand TIMESTAMP to typeof(datetime), Instances to typeof(long) limit 300";
}


private static string GetServerFarmUpdateQuery(OperationContext<App> cxt, dynamic observerSite)
{
    var serverFarm = (string)observerSite.server_farm.server_farm_name;
    return $@"AntaresAdminSubscriptionAuditEvents 
        | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime)} 
        | where ServerFarmName == '{serverFarm}'
        | where EntityType == 'ServerFarm' and OperationType =='Update'    
        | parse RequestContent with * ""<NumberOfWorkers>"" NumberOfWorkers:int  ""</NumberOfWorkers>"" * ""<SKU>"" Sku:string  ""</SKU>"" * ""<WorkerSizeId>"" WorkerSizeId:int ""</WorkerSizeId>"" *    
        | project TIMESTAMP, EntityType, OperationType, OperationStatus, ServerFarmName , NumberOfWorkers, Sku, WorkerSizeId
        | order by TIMESTAMP asc";
}

[AppFilter(AppType = AppType.WebApp, PlatformType = PlatformType.Windows, StackType = StackType.All, InternalOnly = false)]
[Definition(Id = "autoscalingoperations", Name = "Autoscaling Operations", Author = "puneetg", Description = "Find out all Autoscaling operations for the web app")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{

    string stampName = cxt.Resource.Stamp.Name;
    string siteName = cxt.Resource.Name;

    var observerSite = (await dp.Observer.GetSite(stampName, siteName))[0];
    
    var autoScaleProfiles = await dp.Kusto.ExecuteQuery(GetAutoScaleProfileQuery(cxt, observerSite), stampName);
    var profilesTable = GetAutoScaleProfileTable(autoScaleProfiles, res);

    if(profilesTable.Rows.Count <= 0)
    {
        // The app doesnt have auto-scale configured.
        var insightDetail = new Dictionary<string, string>();
        insightDetail["Tip"] = "For production apps, its recommended that autoscaling is configured. This ensures that the app is ready for burst loads as well as save costs when demand is low (eg: nights and weekends). If this is not a production app, you can safely ignore this warning.";
        insightDetail["Learn More"] = "We recommend choosing an adequate margin between the scale-out and in thresholds (a difference of more than 30) to avoid 'flapping' situations, where scale-in and scale-out actions continually go back and forth. For more details refer to <a href='https://docs.microsoft.com/en-us/azure/monitoring-and-diagnostics/insights-autoscale-best-practices' target='_blank'>Autoscaling best practices</a>";
        
        Insight insight = new Insight(InsightStatus.Warning, "We found that the web app has no autoscale profiles configured", insightDetail);
        res.AddInsight(insight); 

        return res;
    }

    var currentInstanceViewTask = dp.Kusto.ExecuteQuery(GetCurrentInstanceViewQuery(cxt, observerSite), stampName);
    var scaleUpOperationsTask = dp.Kusto.ExecuteQuery(GetEstimateScaleRuleQuery(cxt, observerSite), stampName);
    var scaleDownOperationsTask = dp.Kusto.ExecuteQuery(GetEstimateScaleInRuleQuery(cxt, observerSite), stampName);
    var serverFarmUpdatesTask = dp.Kusto.ExecuteQuery(GetServerFarmUpdateQuery(cxt, observerSite), stampName);

    if(cxt.IsInternalCall && cxt.Resource.Name == "tgna-e-img"){
        var description = @"
        Detected uncoventional autoscale rules. Customer's app service plan is increasing instances when CPU and Memory is falling below 60% and 45%.
        Create rules that will increase instances when metric surpasses a certain threshold and decrease instances when metric falls below a certain threshold";
        var profileDiagnosticData = new DiagnosticData()
        {
            Table = profilesTable,
            RenderingProperties = new Rendering(RenderingType.Table){Title = "Autoscale Profiles", Description = "This shows the AutoScale profiles configured for the App Service plan."}
        };

        res.AddDynamicInsight(new DynamicInsight(InsightStatus.Warning, "Profile Configuration Issue", profileDiagnosticData, false, description));

                    res.AddMarkdownView(@"
                Hello, 

                I've been investigating the issue that you've been having with your app, which looks to be related to an issue with an autoscaling operation that you have enabled. 

                In the last 24 hours, I noticed that the **autoscale** rules that you have enabled on your app may not have been configured properly. 

                **Root Cause**

                The reason why your autoscale rules may have caused this issue is because it will have caused your app to move to a new worker instance and experienced multiple cold starts more frequently than expected. This is responsible for the long load times that your web app may have experienced. 

                **Recommendation/Fix**

                I'd recommend that instead of scaling in when CPU and memory is greater than a certain threshold, create two autoscale rules to set both a lower and upper bound on CPU and memory percentage. For example, one autoscale rule is set to increase the number of instances (scale out) when CPU and memory is greater than 60%, and the second autoscale rule (scale in) is set to decrease the number of instances when the CPU and memory falls below 15%. You can learn more about setting up autoscale rules [here]( https://docs.microsoft.com/en-us/azure/monitoring-and-diagnostics/insights-how-to-scale?toc=%2fazure%2fapp-service%2ftoc.json#scaling-based-on-a-pre-set-metric).

                I hope this has been helpful in getting your web app back up and running and answered your question. If you have additional questions about your autoscaling issue that is not answered above or in the links for more details, please do not hesitate to ask. 

                Best Regards,

            ", "Customer Ready Email");
    }else{
        res.Dataset.Add(new DiagnosticData()
            {
                Table = profilesTable,
                RenderingProperties = new Rendering(RenderingType.Table){Title = "Autoscale Profiles", Description = "This shows the AutoScale profiles configured for the App Service plan."}
            });
    }

        res.Dataset.Add(new DiagnosticData()
        {
            Table = await currentInstanceViewTask,
            RenderingProperties = new Rendering(RenderingType.TimeSeries){Title = "Instance Count", Description = "This shows all instances count at a given point of time"}
        });

        var foundScalingOperations = false;
        var scaleUpOperations = await scaleUpOperationsTask;
        if (scaleUpOperations.Rows.Count > 0) 
        {
            foundScalingOperations = true;
            res.Dataset.Add(new DiagnosticData()
            {
                Table = scaleUpOperations,
                RenderingProperties = new Rendering(RenderingType.Table){Title = "Scale-Up Operations Triggered by Autoscale", Description = "This shows all the entries where Autoscale engine estimated that instance count should be increased because one of the Autoscale rules configured met the condition. A Scale-Up operation happens when one of the configured Autoscale rule condition is met."}
            });
        }
        
        var scaleDownOperations = await scaleDownOperationsTask;
        if (scaleDownOperations.Rows.Count > 0)
        {
            foundScalingOperations = true;
            res.Dataset.Add(new DiagnosticData()
            {
                Table = scaleDownOperations,
                RenderingProperties = new Rendering(RenderingType.Table){Title = "Scale-In Operations Triggered by Autoscale", Description = "This shows all the entries where Autoscale estimated that instances should be decreased because all of the configured rules for Autoscaling rule met the condition. A Scale-In operation happens when all of the configured Autoscale rule condition is met."}
            });
        }
        
        var serverFarmUpdates = await serverFarmUpdatesTask;
        if (serverFarmUpdates.Rows.Count > 0)
        {
            res.Dataset.Add(new DiagnosticData()
            {
                Table = serverFarmUpdates,
                RenderingProperties = new Rendering(RenderingType.Table){Title = "Actual Scale Operations at Azure App Service level", Description = "This shows all the entries where the Autoscale request to increase or decrease the instances reached to Azure App Service infrastructure."}
            });
        }

        if (!foundScalingOperations)
        {
            Insight insight = new Insight(InsightStatus.Success, "We found no scale-in or scale-out operations in this time interval");
            res.AddInsight(insight); 
        }
    
    return res;
}


private static DataTable GetAutoScaleProfileTable(DataTable autoScaleProfiles, Response res)
{
    List<AutoScaleProfile> profiles = new List<AutoScaleProfile>();

    foreach (DataRow row in autoScaleProfiles.Rows)
    {               
        var autoScaleProfileObject = JsonConvert.DeserializeObject<AutoScaleProfile>(row["Profile"].ToString());
        profiles.Add(autoScaleProfileObject);                            
    }

    DataTable profilesTable = new DataTable();
    profilesTable.Columns.Add("Settings");
    profilesTable.Columns.Add("Value");

    int ruleCount = 1;
    var metricRules = new List<MetricRule>();

    foreach(var profile in profiles)
    {        
        profilesTable.Rows.Add($"<b>Profile - {ruleCount}</b>", profile.Name);
        profilesTable.Rows.Add("Capacity", $"Min={profile.Capacity.Minimum}, Max={profile.Capacity.Maximum} , Default={profile.Capacity.Default}");

        foreach (var rule in profile.Rules)
        {
            string symbol = rule.MetricTrigger.Operator ;
            profilesTable.Rows.Add($"{rule.ScaleAction.Direction} Instances by {rule.ScaleAction.Value}", $"When {rule.MetricTrigger.Name} {symbol} {rule.MetricTrigger.Threshold.ToString("0.#########")} with TimeGrain = {rule.MetricTrigger.TimeGrain.Substring(2)} in TimeWindow = {rule.MetricTrigger.TimeWindow.Substring(2)} and Cooldown of {rule.ScaleAction.Cooldown.Substring(2)}");

            if (rule.MetricTrigger.Name == "CpuPercentage" || rule.MetricTrigger.Name == "MemoryPercentage")
            {
                var m = metricRules.Where(x=>x.Name == rule.MetricTrigger.Name).FirstOrDefault();
                if (m == null)
                {   
                    m = new MetricRule();                                     
                    metricRules.Add(m);                    
                }
                
                m.Name = rule.MetricTrigger.Name;
                if (rule.MetricTrigger.Operator != "GreaterThan" && rule.MetricTrigger.Operator !="GreaterThanOrEqual") 
                {
                    if (m.Min ==0)
                    {
                        m.Min = (int)rule.MetricTrigger.Threshold;
                    }
                    else
                    {
                        m.Min = Math.Min(m.Min, (int)rule.MetricTrigger.Threshold);
                    }
                    
                }
                else
                {
                    if (m.Max ==0)
                    {
                        m.Max = (int)rule.MetricTrigger.Threshold;
                    }
                    else
                    {
                        m.Max = Math.Max((int)rule.MetricTrigger.Threshold, m.Max);
                    }
                    
                }                             
            }
        }

        if (profile.Recurrence !=null)
        {
            profilesTable.Rows.Add("RecurringFrequency",profile.Recurrence.Frequency);

            if (profile.Recurrence.Schedule !=null)
            {
                 profilesTable.Rows.Add("TimeZone",profile.Recurrence.Schedule.TimeZone);
                 profilesTable.Rows.Add("Days",string.Join(",",profile.Recurrence.Schedule.Days));
                 profilesTable.Rows.Add("Hours",string.Join(",",profile.Recurrence.Schedule.Hours));
                 profilesTable.Rows.Add("Minutes",string.Join(",",profile.Recurrence.Schedule.Minutes));

            }
        }

        ruleCount++;
    }    

    var insightDetails = new Dictionary<string, string>();
    foreach(var r in metricRules)
    {
        if ((r.Max - r.Min) < 30 && r.Max !=0 && r.Min !=0)
        {            
            insightDetails.Add(r.Name, $"Min = {r.Min} and Max = {r.Max}");                                 
        }
    }

    if (insightDetails.Count > 0)
    {        
        insightDetails.Add("Tip", "We recommend choosing an adequate margin between the scale-out and in thresholds (a difference of more than 30) to avoid 'flapping' situations, where scale-in and scale-out actions continually go back and forth. For more details refer to <a href='https://docs.microsoft.com/en-us/azure/monitoring-and-diagnostics/insights-autoscale-best-practices' target='_blank'>Autoscaling best practices</a>");
        res.AddInsights(new List<Insight>(){
            new Insight(InsightStatus.Warning, $"Autoscale rule configuration may cause flapping", insightDetails),
        });   
    }

    return profilesTable;
}

public class AutoScaleProfile
    {
        public string Name { get; set; }
        public Capacity Capacity { get; set; }
        public Rule[] Rules { get; set; }
        public Recurrence Recurrence { get; set; }
    }

    public class Recurrence
    {
        public string Frequency { get; set; }
        public Schedule Schedule { get; set; }
    }

    public class Schedule
    {
        public string TimeZone { get; set; }
        public string[] Days { get; set; }
        public int[] Hours { get; set; }
        public int[] Minutes { get; set; }
    }

    public class Capacity
    {
        public string Minimum { get; set; }
        public string Maximum { get; set; }
        public string Default { get; set; }
    }

    public class Rule
    {
        public Metrictrigger MetricTrigger { get; set; }
        public Scaleaction ScaleAction { get; set; }
    }

    public class Metrictrigger
    {
        public string Name { get; set; }
        public string Namespace { get; set; }
        public string Resource { get; set; }
        public string ResourceLocation { get; set; }
        public string TimeGrain { get; set; }
        public string Statistic { get; set; }
        public string TimeWindow { get; set; }
        public string TimeAggregation { get; set; }
        public string Operator { get; set; }
        public float Threshold { get; set; }
        public string Source { get; set; }
        public string MetricType { get; set; }
    }

    public class Scaleaction
    {
        public string Direction { get; set; }
        public string Type { get; set; }
        public string Value { get; set; }
        public string Cooldown { get; set; }
    }

    class MetricRule
    {
        public string Name;
        public int Min;
        public int Max;
    }