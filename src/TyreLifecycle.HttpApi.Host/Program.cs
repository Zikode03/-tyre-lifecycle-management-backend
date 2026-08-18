using TyreLifecycle.HttpApi.Host;
using Volo.Abp;

var builder = WebApplication.CreateBuilder(args);

await builder.AddApplicationAsync<TyreLifecycleHttpApiHostModule>();

var app = builder.Build();
await app.InitializeApplicationAsync();
await app.RunAsync();
