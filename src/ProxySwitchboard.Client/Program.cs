using CliFx;

var builder = new CommandLineApplicationBuilder().AddCommandsFromThisAssembly();

var title = Environment.GetEnvironmentVariable("PROXY_SWITCHBOARD_TITLE");
if (title is not null)
    builder.SetTitle(title);

var executableName = Environment.GetEnvironmentVariable("PROXY_SWITCHBOARD_EXECUTABLE_NAME");
if (executableName is not null)
    builder.SetExecutableName(executableName);

return await builder.Build().RunAsync();
