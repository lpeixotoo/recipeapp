namespace RecipeApp.Web

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Mvc
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Prometheus
open Serilog
open System
open RecipeApp

/// Equinox store bindings
module Storage =
    /// Specifies the store to be used, together with any relevant custom parameters
    [<RequireQualifiedAccess>]
    type Config =
        | Mem

    /// Holds an initialized/customized/configured of the store as defined by the `Config`
    type Instance =
        | MemoryStore of Equinox.MemoryStore.VolatileStore<obj>

    /// MemoryStore 'wiring', uses Equinox.MemoryStore nuget package
    module private Memory =
        open Equinox.MemoryStore
        let connect () =
            VolatileStore()

    /// Creates and/or connects to a specific store as dictated by the specified config
    let connect : Config -> Instance = function
        | Config.Mem ->
            let store = Memory.connect()
            Instance.MemoryStore store

/// Dependency Injection wiring for services using Equinox
module Services =
    /// Builds a Stream Resolve function appropriate to the store being used
    type StreamResolver(storage : Storage.Instance) =
        member _.Resolve
            (   codec : FsCodec.IEventCodec<'event, byte[], _>,
                fold: 'state -> 'event seq -> 'state,
                initial: 'state,
                snapshot: ('event -> bool) * ('state -> 'event)) =
            match storage with
            | Storage.MemoryStore store ->
                Equinox.MemoryStore.MemoryStoreCategory(store, FsCodec.Box.Codec.Create(), fold, initial).Resolve

    /// Binds a storage independent Service's Handler's `resolve` function to a given Stream Policy using the StreamResolver
    type ServiceBuilder(resolver: StreamResolver) =
         member _.CreateRecipesService() =
            let fold, initial = Recipe.Fold.fold, Recipe.Fold.initial
            let snapshot = Recipe.Fold.isOrigin, Recipe.Fold.snapshot
            Recipe.create (resolver.Resolve(Recipe.Events.codec, fold, initial, snapshot))

    /// F# syntactic sugar for registering services
    type IServiceCollection with
        /// Register a Service as a Singleton, by supplying a function that can build an instance of the type in question
        member services.Register(factory : IServiceProvider -> 'T) = services.AddSingleton<'T>(fun (sp: IServiceProvider) -> factory sp) |> ignore

    /// F# syntactic sugar to resolve a service dependency
    type IServiceProvider with member sp.Resolve<'t>() = sp.GetRequiredService<'t>()

    /// Registers the Equinox Store, Stream Resolver, Service Builder and the Service
    let register (services : IServiceCollection, storeCfg) =
        services.Register(fun _sp -> Storage.connect storeCfg)
        services.Register(fun sp -> StreamResolver(sp.Resolve()))
        services.Register(fun sp -> ServiceBuilder(sp.Resolve()))
        services.Register(fun sp -> sp.Resolve<ServiceBuilder>().CreateRecipesService())
        //services.Register(fun sp -> sp.Resolve<ServiceBuilder>().CreateThingService())

/// Defines the Hosting configuration, including registration of the store and backend services
type Startup() =
    // This method gets called by the runtime. Use this method to add services to the container.
    member _.ConfigureServices(services: IServiceCollection) : unit =
        services
            .AddMvc()
            .SetCompatibilityVersion(CompatibilityVersion.Latest)
            .AddNewtonsoftJson() // until FsCodec.SystemTextJson is available
            |> ignore

        let storeConfig = Storage.Config.Mem

        Services.register(services, storeConfig)

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    member _.Configure(app: IApplicationBuilder, env: IHostEnvironment) : unit =
        if env.IsDevelopment() then app.UseDeveloperExceptionPage() |> ignore
        else app.UseHsts() |> ignore

        app.UseHttpsRedirection()
            .UseRouting()
            .UseSerilogRequestLogging() // see https://nblumhardt.com/2019/10/serilog-in-aspnetcore-3/
            // NB Jet does now own, control or audit https://todobackend.com; it is a third party site; please satisfy yourself that this is a safe thing use in your environment before using it._
            .UseCors(fun x -> x.WithOrigins([|"https://www.todobackend.com"|]).AllowAnyHeader().AllowAnyMethod() |> ignore)
            .UseEndpoints(fun endpoints ->
                endpoints.MapMetrics() |> ignore // Host /metrics for Prometheus
                endpoints.MapControllers() |> ignore)
            |> ignore
