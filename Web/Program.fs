module recipeapp.Web.Program

open Microsoft.AspNetCore
open Microsoft.AspNetCore.Hosting
open Serilog

[<System.Runtime.CompilerServices.Extension>]
type Logging() =

    [<System.Runtime.CompilerServices.Extension>]
    static member Configure(c : LoggerConfiguration, appName) =
        let customTags = ["app",appName]
        c
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft.AspNetCore", Serilog.Events.LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console()

let createWebHostBuilder args : IWebHostBuilder =
    WebHost
        .CreateDefaultBuilder(args)
        .UseSerilog()
        .UseStartup<Startup>()

let [<Literal>] AppName = "TodoApp"

[<EntryPoint>]
let main argv =
    try Log.Logger <- LoggerConfiguration().Configure(AppName).CreateLogger()
        try createWebHostBuilder(argv).Build().Run()
            0
        with e ->
            Log.Fatal(e, "Application Startup failed")
            1
    finally Log.CloseAndFlush()
