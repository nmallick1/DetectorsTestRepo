using System.Text;

private static string GetQuery(OperationContext<HostingEnvironment> cxt)
{
    return
    $@"<YOUR_TABLE_NAME>
        | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
        | <YOUR_QUERY>";
}

[HostingEnvironmentFilter(HostingEnvironmentType = HostingEnvironmentType.All, PlatformType = PlatformType.Windows, InternalOnly = false)]
[Definition(Id = "vnetverifier", Name = "NSG Verifier", Description = "Verify the network security group rules for an ASE's subnet", Author="hawfor")]
public async static Task<Response> Run(DataProviders dp, OperationContext<HostingEnvironment> cxt, Response res)
{
    //Get vnet information from observer
    var hostingEnvironmentData = await dp.Observer.GetResource($"https://wawsobserver.azurewebsites.windows.net/minienvironments/{cxt.Resource.InternalName}");
    var vnetName = (string)hostingEnvironmentData.vnet_name;
    var vnetSubnetName = (string)hostingEnvironmentData.vnet_subnet_name;
    var vnetResourceGroup = (string)hostingEnvironmentData.vnet_resource_group;

    var vnetVerifierResults = await dp.GeoMaster.VerifyHostingEnvironmentVnet(cxt.Resource.SubscriptionId, vnetResourceGroup, vnetName, vnetSubnetName);

    string[] testNames = new string[]{"incominggeo", "intrasubnetincoming", "intrasubnetoutgoing", "outboundhttp", "outboundhttps", "outboundsmb", "outboundsql", "dns"};
    foreach(var testName in testNames){
        InsightStatus status = InsightStatus.Success;
        var body = new Dictionary<string, string>{ {"Description", GetDescription(testName)}};

        if(vnetVerifierResults.FailedTests != null){
            if(vnetVerifierResults.FailedTests.Exists(t => t.TestName.Equals(testName, StringComparison.CurrentCultureIgnoreCase))){
                var test = vnetVerifierResults.FailedTests.Find(t => t.TestName.Equals(testName, StringComparison.CurrentCultureIgnoreCase));
                status = InsightStatus.Critical;
                body.Add("Offending NSG Rule", $"{test.Details}");
                body.Add("Solution", $"{GetSolution(testName, test.Details)}");
                body.Add("Learn More", $"{GetNsgDocumentationUrl(testName)}");
            }
        }else{
            //TODO add learn more about network security groups
            res.AddInsight(InsightStatus.Info, $"The subnet {vnetSubnetName} does not have any network security groups attached.");
            return res;
        }

        res.AddInsight(status, $"{GetFriendlyName(testName)} Test", body);
    }

    return res;
}

private static string GetDescription(string testName){
    StringBuilder descriptionBuilder = new StringBuilder();
    switch (testName.ToLower())
        {
            case "incominggeo":
                descriptionBuilder.AppendFormat("Port 454-455 is used by Azure infrastructure for managing and maintaining App Service Environments via SSL.");
                break;
            case "intrasubnetincoming":
            case "intrasubnetoutgoing":
                descriptionBuilder.AppendFormat("Traffic from any port to any port inside of the subnet.");
                break;
            case "outboundhttp":
                descriptionBuilder.AppendFormat("HTTP traffic from your subnet to the Internet through port 80.");
                break;
            case "outboundhttps":
                descriptionBuilder.AppendFormat("HTTPS traffic from your subnet to the Internet through port 443.");
                break;
            case "outboundsmb":
                descriptionBuilder.AppendFormat("Outbound network connectivity to the Azure Files service on port 445.");
                break;
            case "outboundsql":
            case "outboundsqlinternal":
                descriptionBuilder.AppendFormat("Outbound network connectivity to Sql DB endpoints located in the same region as the App Service Environment. Sql DB endpoints resolve under the following domain: database.windows.net. This requires opening access to ports 1433, 11000-11999 and 14000-14999.");
                break;
            case "dns":
                descriptionBuilder.AppendFormat("Traffic to Azure DNS at IP 168.63.129.16 from your subnet through port 53.");
                break;
            default:
                break;
        }
    return descriptionBuilder.ToString();
}

private static string GetFriendlyName(string testName){
        switch (testName)
        {
            case "incominggeo":
                return "Management Connectivity";
            case "intrasubnetincoming":
                return "Intra Subnet Communication (inbound)";
            case "intrasubnetoutgoing":
                return "Intra Subnet Communication (outbound)";
            case "outboundhttp":
                return "Outbound HTTP Traffic from Subnet";
            case "outboundhttps":
                return "Outbound HTTPS Traffic from Subnet";
            case "outboundsmb":
                return "Outbound SMB Traffic from Subnet";
            case "outboundsql":
            case "outboundsqlinternal":
                return "Outbound SQL Traffic from Subnet";
            case "dns":
                return "Traffic to Azure DNS";
            default:
                return "";
        }
}

private static string GetSolution(string testName, string rule){
    var solutionBuilder = new StringBuilder();

    if(!IsAzureDefaultRule(rule)){
        solutionBuilder.Append("Change the offending NSG rule to adhere to our documentation. Modify it to ");
    }else{
        solutionBuilder.Append($"The offending rule is a default Azure security rule that is blocking connectivity. Create a higher priority rule than {rule} that will ");
    }

    switch (testName)
    {
        case "incominggeo":
            return solutionBuilder.Append("allow traffic from any source and any port to any destination through ports 454-455 on any protocol.").ToString();
        case "intrasubnetincoming":
            return solutionBuilder.Append("allow incoming traffic to the subnet by any source through any port to any destination by any ports on any protocol.").ToString();
        case "intrasubnetoutgoing":
            return solutionBuilder.Append("allow outgoing traffic from the subnet through any source and through any port to any destination by any ports on any protocol.").ToString();
        case "outboundhttp":
            return solutionBuilder.Append("allow traffic from any source and any port to any destination through ports 80 of any protocol.").ToString();
        case "outboundhttps":
            return solutionBuilder.Append("allow traffic from any source and any port to any destination through ports 443 of any protocol.").ToString();
        case "outboundsmb":
            return solutionBuilder.Append("allow traffic from any source and any port to any destination through ports 445 of any protocol.").ToString();
        case "outboundsql":
        case "outboundsqlinternal":
            return solutionBuilder.Append("allow traffic from any source and any port to any destination through ports 1433,11000 - 11999, 14000-14999 of any protocol.").ToString();
        case "dns":
            return solutionBuilder.Append("allow traffic from any source and any port to any destination through ports 53 of any protocol.").ToString();
        default:
            return solutionBuilder.Append("").ToString();
    } 

    return solutionBuilder.ToString(); 
}

private static bool IsAzureDefaultRule(string ruleName){
    switch(ruleName.ToLower()){
        case "allowvnetinbound":
        case "allowazureloadbalancerinbound":
        case "denyallinbound":
        case "allowvnetoutbound":
        case "allowinternetoutbound":
        case "denyalloutbound":
            return true;
        default:
            return false;
    }
}

private static string GetNsgDocumentationUrl(string testName){
    const string inboundNSG = @"<a target=""_blank"" href=""https://docs.microsoft.com/en-us/azure/app-service/environment/app-service-app-service-environment-control-inbound-traffic#inbound-network-ports-used-in-an-app-service-environment"">Inbound Network Ports Used in an App Service Environment</a>";
    const string outboundNSG = @"<a target=""_blank"" href=""https://docs.microsoft.com/en-us/azure/app-service/environment/app-service-app-service-environment-network-configuration-expressroute#required-network-connectivity"">Outbound Connectivity</a>";
        switch (testName)
        {
            case "incominggeo":
                return inboundNSG;
            case "intrasubnetincoming":
                return inboundNSG;
            case "intrasubnetoutgoing":
                return outboundNSG;
            case "outboundhttp":
                return outboundNSG;
            case "outboundhttps":
                return outboundNSG;
            case "outboundsmb":
                return outboundNSG;
            case "outboundsql":
            case "outboundsqlinternal":
                return outboundNSG;
            case "dns":
                return outboundNSG;
            default:
                return "";
        }
}