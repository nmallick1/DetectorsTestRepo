private static string GetQuery(OperationContext<HostingEnvironment> cxt)
{
    return
    $@"<YOUR_TABLE_NAME>
        | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
        | <YOUR_QUERY>";
}

[HostingEnvironmentFilter(HostingEnvironmentType = HostingEnvironmentType.All, PlatformType = PlatformType.Windows, InternalOnly = false)]
[Definition(Id = "subnetaddressspaceexhaustion", Name = "Subnet Address Space Exhaustion", Description = "Check to see if we near exhausting all of the available addresses for this subnet")]
public async static Task<Response> Run(DataProviders dp, OperationContext<HostingEnvironment> cxt, Response res)
{
    var hostingEnvironmentData = await dp.Observer.GetResource($"https://wawsobserver.azurewebsites.windows.net/minienvironments/{cxt.Resource.InternalName}");
    int maxHostMachines = ParseMaximumAllowableHosts(hostingEnvironmentData);
    int currentNumberOfHosts = ParseCurrentNumberOfHosts(hostingEnvironmentData);
    int availableAddresses = maxHostMachines - currentNumberOfHosts;

    Insight availableAddressInsight = null;
    string availabilityColor = null;

    if(availableAddresses >= 20){
        availableAddressInsight = new Insight(InsightStatus.Success, "High Availability of Subnet Addresses");
        availabilityColor = "#4DA900";
    }
    else if(availableAddresses > 10){
        availableAddressInsight = new Insight(InsightStatus.Warning, "Low Availability of Subnet Addresses");
        availableAddressInsight.Body = new Dictionary<string,string>{
            {"Observation",$"Your subnet has {availableAddresses} available addresses left in its address space."},
            {"Solution", $"Please consider creating a larger subnet with at least {(maxHostMachines > 128 ? maxHostMachines * 2 : 128)} available addresses or refrain from scaling out your app service environment"},
            {"Learn More", @"<a target=""_blank"" href=""https://docs.microsoft.com/en-us/azure/app-service/environment/network-info#ase-subnet-size"">Subnet Size Recommendation</a>"}
        };
        availabilityColor = "#F7D100";
    }
    else{
        availableAddressInsight = new Insight(InsightStatus.Critical, "Critcally Low Availability of Subnet Addresses");
        availableAddressInsight.Body = new Dictionary<string, string>{
            {"Observation", $"Your subnet has {availableAddresses} available addresses left in its address space. "},
            {"Solution", $"Please consider creating a larger subnet with at least {(maxHostMachines > 128 ? maxHostMachines * 2 : 128)} available addresses or refrain from scaling out your app service environment"},
            {"Learn More", @"<a target=""_blank"" href=""https://docs.microsoft.com/en-us/azure/app-service/environment/network-info#ase-subnet-size"">Subnet Size Recommendation</a>"}
        };
        availabilityColor = "#B20E12";
    }

    res.AddInsight(availableAddressInsight);

    DataSummary available = new DataSummary("Available Addresses", $"{maxHostMachines - currentNumberOfHosts}", availabilityColor);
    DataSummary used = new DataSummary("Total Used", $"{currentNumberOfHosts}", "#71BADD");
    DataSummary total = new DataSummary("Total Addresses", $"{maxHostMachines}", "#71BADD");

    var dataSummaries = new List<DataSummary>(){available, used, total};
    res.AddDataSummary(dataSummaries);
    return res;
}

private static int ParseMaximumAllowableHosts(dynamic hostingEnvironmentData){
    const int ipv4TotalBits = 32;

    var subnetAddressRange = (string) hostingEnvironmentData.vnet_subnet_address_range;

    if(!string.IsNullOrWhiteSpace(subnetAddressRange)){
        int significantBits = int.Parse(subnetAddressRange.Split(new char[]{'/'})[1]);
        int maxHostMachineAddresses = (int) (Math.Pow(2, ipv4TotalBits - significantBits));
        return maxHostMachineAddresses; 
    }

    return 0;
}

private static int ParseCurrentNumberOfHosts(dynamic hostingEnvironmentData){
    const int totalAddressesUsedByInfrastructure = 5;

    var roleCountKeys = new string[]{"multi_role_count", "small_dedicated_webworker_role_count", "medium_dedicated_webworker_role_count", "large_dedicated_webworker_role_count",
    "small_dedicated_linux_webworker_role_count", "medium_dedicated_linux_webworker_role_count", "large_dedicated_linux_webworker_role_count", "file_server_role_count"};
    
    int currentNumberOfHosts = 0;
    
    foreach(var key in roleCountKeys){
        currentNumberOfHosts += hostingEnvironmentData.Value<int>(key);
    }

    currentNumberOfHosts += totalAddressesUsedByInfrastructure;

    return currentNumberOfHosts;
}