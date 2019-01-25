using System.Linq;

[HostingEnvironmentFilter(HostingEnvironmentType = HostingEnvironmentType.All, PlatformType = PlatformType.Windows, InternalOnly = false)]
[Definition(Id = "outboundconnectivity", Name = "Outbound Connectivity", Description = "Check if this app service environment has connectivity to external dependencies.", Author = "hawfor")]
public async static Task<Response> Run(DataProviders dp, OperationContext<HostingEnvironment> cxt, Response res)
{

    var overallHealth = (string)(await dp.Kusto.ExecuteQuery(GetOverallStatusQuery(cxt), cxt.Resource.InternalName)).Rows[0][0];
    DataSummary healthSummary = new DataSummary("Overall Connection Health", overallHealth , overallHealth == "Healthy" ? "green" : "red");

    //res.AddDataSummary(new List<DataSummary>(){ healthSummary });


    var connectivityTable = await dp.Kusto.ExecuteQuery(GetLatestConnectivityQuery(cxt), cxt.Resource.InternalName);
    var domainAndProblems = new Dictionary<string, List<Tuple<string, string, bool>>>();
    
    foreach(DataRow row in connectivityTable.Rows)
    {
        InsightStatus isAccessible = ((string)row["IsAccessable"]).Equals("Yes", StringComparison.OrdinalIgnoreCase) ? InsightStatus.Success : InsightStatus.Critical;
        var domain = row["Domain"].ToString();
        var host = row["Host"].ToString();
        var port = row["Port"].ToString();

        List<Tuple<string, string, bool>> connectivityStatus;

        if(string.IsNullOrWhiteSpace(GetDependencyName(domain))){
            continue;
        }

        if(!domainAndProblems.TryGetValue(GetDependencyName(domain), out connectivityStatus)){
            connectivityStatus = new List<Tuple<string, string, bool>>();
            connectivityStatus.Add(Tuple.Create(host, port, ((string)row["IsAccessable"]).Equals("Yes", StringComparison.OrdinalIgnoreCase)));
            domainAndProblems.Add(GetDependencyName(domain), connectivityStatus);
        }
    }

    foreach(var domainAndStatus in domainAndProblems){
        var status = domainAndStatus.Value.All(t => t.Item3 == true);
        var body = new Dictionary<string, string>();
        if(!string.IsNullOrWhiteSpace(GetDescription(domainAndStatus.Key))){
            body.Add("Description", GetDescription(domainAndStatus.Key));
        }
        if(!status && !string.IsNullOrWhiteSpace(GetSolution(domainAndStatus.Key))){
            body.Add("Solution", GetSolution(domainAndStatus.Key));
        }
        
        var link = cxt.Resource.HostingEnvironmentType == HostingEnvironmentType.V1 ? "https://docs.microsoft.com/en-us/azure/app-service/environment/app-service-app-service-environment-network-configuration-expressroute#required-network-connectivity" : "https://docs.microsoft.com/en-us/azure/app-service/environment/network-info#ase-dependencies";
        body.Add("Learn More", $@"<a target=""_blank"" href={link}>Network Connectivity</a>");
        res.AddInsight(status == true ? InsightStatus.Success : InsightStatus.Critical, $"{domainAndStatus.Key}", body);
    }
    
    return res;
}

private static string GetDependencyName(string domain){
    switch(domain.ToLower()){
        case "blob.core.windows.net":
        case "file.core.windows.net":
        case "table.core.windows.net":
        case "queue.core.windows.net":
            return "Azure Storage";
        case "database.windows.net":
            return "Azure SQL Database";
        case "management.core.windows.net":
        case "management.azure.com":
        case "admin.core.windows.net":
            return "Azure Management";
        case "gr-prod-bay.cloudapp.net":
        case "az-prod.metrics.nsatc.net":
            return "Regional Service";
        case "login.windows.net":
            return "Azure Active Directory";
        case "Ocsp.msocsp.com":
        case "mscrl.microsoft.com":
        case "crl.microsoft.com":
            return "SSL Certificate Verification";
        default:
            return "";
    }
}

private static string GetDescription(string dependency){
    switch(dependency){
        case "Azure Storage": return "Outbound network connectivity to Azure Storage endpoints worldwide on both ports 80 and 443. This includes endpoints located in the same region as the App Service Environment, as well as storage endpoints located in other Azure regions. Azure Storage endpoints resolve under the following DNS domains: table.core.windows.net, blob.core.windows.net, queue.core.windows.net and file.core.windows.net.";
        case "Azure SQL Database": return "Outbound network connectivity to Sql DB endpoints located in the same region as the App Service Environment. Sql DB endpoints resolve under the following domain: database.windows.net. This requires opening access to ports 1433, 11000-11999 and 14000-14999";
        case "Azure Management": return "Outbound network connectivity to the Azure management plane endpoints (both ASM and ARM endpoints). This includes outbound connectivity to both management.core.windows.net and management.azure.com.";
        default: return "";
    }
}

private static string GetSolution(string dependency){
    switch(dependency.ToLower()){
        case "azure storage":
            return 
@"
1. If you have network security group linked to your subnet then check your outbound security rules and modify any rules that is blocking HTTP traffic through port 80 and 443 from any source to any destination. <br></br>
2. If you have a route table linked to your subnet, double check that you have the default route of 0.0.0.0/0 with a next hop to Internet. <br></br>
3. If neither the above applies, enable service endpoints for Azure Storage from your subnet.
";
        case "sql azure":
        return 
@"
1. If you have network security group linked to your subnet then check your outbound security rules and modify any rules that is blocking traffic through ports 1433, 11000-11999 and 14000-14999 from any source to any destination. <br></br>
2. If you have a route table linked to your subnet, double check that you have the default route of 0.0.0.0/0 with a next hop to Internet. <br></br>
3. If neither the above applies, enable service endpoints for SQL Azure from your subnet.
";
        default:
            return "";
    }
}


public static string GetOverallStatusQuery(OperationContext<HostingEnvironment> cxt)
{
    return $@"let M1 = MiniStampConnectivityStatus
            | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
            | summarize max(PreciseTimeStamp) by Host, Port
            | project Host, Port, Timestamp=max_PreciseTimeStamp;
            let summary = M1
            | join
            (
                MiniStampConnectivityStatus
                | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
            )
            on Host, Port, $left.Timestamp == $right.PreciseTimeStamp
            | project Host, Port, Message=strcat(Details, Exception), PreciseTimeStamp;
            summary
            | where Message !contains 'self'
            | summarize AseHealthy=iif(countif(Message contains 'failed') == 0, 'Healthy', 'Unhealthy')";
}

public static string GetConnectivityChartByHostQuery(string host, OperationContext<HostingEnvironment> cxt)
{
    return $@"let host = '{host}';
                MiniStampConnectivityStatus
                | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
                | where Details contains host or Exception contains host
                | summarize by bin(PreciseTimeStamp, 5m), success=iif((Details contains host), 1, 0)//, fail=iif((Exception contains host), 1, 0)";
}

public static string GetLatestConnectivityQuery(OperationContext<HostingEnvironment> cxt)
{
    return $@"let M1 = MiniStampConnectivityStatus
            | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
            | summarize max(PreciseTimeStamp) by Host, Port
            | project Host, Port, Timestamp=max_PreciseTimeStamp;
            let summary = M1
            | join
            (
                MiniStampConnectivityStatus
                | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
            )
            on Host, Port, $left.Timestamp == $right.PreciseTimeStamp
            | project Host, Port, Message=strcat(Details, Exception);
            summary
            | where Message !contains 'self'
            | project Host, Port, IsAccessable=iif(Message contains 'succeeded', 'Yes', 'No'), Domain=split(split(Message, 'to ', 1)[0], ' for', 0)[0]
            | order by IsAccessable asc, Host, Port";
} 