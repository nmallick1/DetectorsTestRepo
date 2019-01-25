private static string GetQuery(OperationContext<App> cxt)
{
    return
    $@"<YOUR_TABLE_NAME>
        | where {Utilities.TimeAndTenantFilterQuery(cxt.StartTime, cxt.EndTime, cxt.Resource)}
        | <YOUR_QUERY>";
}


[AppFilter(AppType = AppType.FunctionApp, PlatformType = PlatformType.Windows, StackType = StackType.All)]
[Definition(Id = "FunctionsTop", Name = "Top Level Functions Detector", Author = "jsanders", Description = "Overall View of Function Detectors")]
public async static Task<Response> Run(DataProviders dp, OperationContext<App> cxt, Response res)
{

res.AddMarkdownView(@"
        Below is a collection of Function detectors. This detector, the parent, provides the ids of the detectors that it wants to list. 
        The detectors are run and the status of each detector is displayed, based on the most severe insight in that detector. 
    ");

    res.AddDetectorCollection(new List<string>() { "FunctionInfo", "funcInvocationCount","functionsinerror", "functionschecker", "functionscale" });

    // Add a markdown section
    // We will use it here to explain how to create an insight


 //   res.Dataset.Add(new DiagnosticData()
  //  {
     var Table = await dp.Kusto.ExecuteQuery(GetQuery(cxt), cxt.Resource.Stamp.Name);
   // });

    return res;
}