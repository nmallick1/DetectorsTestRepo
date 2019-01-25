// start - for Function Information Section - jsanders

private static string GetDefinedFunctions(OperationContext<App> cxt)
{
    // gets functions for this app  - if the function is not hit (stopped), it is possible that this will not return anything    
    string siteName = cxt.Resource.Name;
    return
    $@"
        
        let functionapp = '{siteName}';
        FunctionsLogs
        | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime)}
        | where AppName =~ functionapp
        | where Summary contains 'Found the following functions:' 
        | parse Summary with * 'Found the following functions:'  resultOfParse
        | project PreciseTimeStamp,  resultOfParse, Summary
        | order by PreciseTimeStamp desc nulls last 
        | take 1
    ";
}


private static string GetFunctionVersion(OperationContext<App> cxt)
{
    // get current version  - if the function is not hit (stopped), it is possible that this will not return anything    
    string siteName = cxt.Resource.Name;
    return
    $@"
        
        let functionapp = '{siteName}';
        FunctionsLogs
        | where {Utilities.TimeFilterQuery(cxt.StartTime, cxt.EndTime)}
        | where AppName =~ functionapp
        | where Summary contains 'Starting Host (HostId=' 
                | parse Summary with * 'FunctionsExtensionVersion=' Version ')' *
                | project PreciseTimeStamp, Role, RoleInstance, AppName , HostVersion , Version
                                | order by PreciseTimeStamp desc nulls last
                                | take 1
    ";
}

// end - for Function Information Section - jsanders

[AppFilter(AppType = AppType.FunctionApp, PlatformType = PlatformType.Windows, StackType = StackType.All)]
[Definition(Id = "FunctionInfo", Name = "Overall Function Information", Author = "jsanders", Description = "basic function info")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{

    // begin Function Information section - jsanders
 var overallInfo = new Dictionary<string,string>();
 
 var verTable = await dp.Kusto.ExecuteQuery(GetFunctionVersion(cxt), cxt.Resource.Stamp.Name);
 if(verTable != null && verTable.Rows != null && verTable.Rows.Count != 0){
         overallInfo["Runtime version:"] = verTable.Rows[0][4].ToString() + "( " +  verTable.Rows[0][5].ToString() + " )";
     }
 
 var definedFunctions = await dp.Kusto.ExecuteQuery(GetDefinedFunctions(cxt), cxt.Resource.Stamp.Name);
     if(definedFunctions != null && definedFunctions.Rows != null && definedFunctions.Rows.Count != 0){
         var result =  definedFunctions.Rows[0][1].ToString();
         overallInfo["Defined Functions:"] = result.Replace(".Run", ".Run <br>");
     }
 
 if (overallInfo.Count > 0)
 {
    res.AddInsight(new Insight(InsightStatus.Info, "Function Information", overallInfo, true));
 }
// end Function Information section - jsanders


    return res;
}