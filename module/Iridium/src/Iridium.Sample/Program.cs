using Iridium;
using Iridium.Authentication;
using Iridium.Installation.Installer;
using Iridium.Java;
using Iridium.Launch;
using Iridium.Models.Installation;
using Iridium.Models.Launch;
using Iridium.Models.Minecraft;

IridiumConfig.Configure(new IridiumContext());

var versions = await VanillaInstaller.GetVersionsAsync();
var installer = new VanillaInstaller(MinecraftTarget.Create(new DirectoryInfo("/home/yang429/文档/mc/.minecraft")));

installer.ProgressChanged += (_, args) => {
    var progress = args.Progress;
    
    Console.Clear();
    Console.WriteLine($"{progress.CompletedSteps}/{progress.TotalSteps} -- {progress.Progress * 100:0.00}%");
    foreach (var step in progress.Steps)
        Console.WriteLine($"[{step.Status}] {step.Name} -- {step.Progress * 100:0.00}% -- {step.Completed}/{step.Total}");
};

var result = await installer
    .InstallAsync(versions?.First(x => x.Id.Equals("1.20.1"))!, 128)
    .ConfigureAwait(false);

var installResult = result as MinecraftInstallResult;

Console.WriteLine(installResult?.Elapsed);
Console.WriteLine(installResult?.IsSuccess);
Console.WriteLine($"Failures Count: {installResult?.Failures.Count}");
Console.WriteLine($"Id: {installResult?.Minecraft?.Entry.Name}");

var launcher = new Launcher();
var javaProvider = new JavaProvider();

await foreach(var java in javaProvider.EnumerableJavaAsync())
    Console.WriteLine($"{java.JavaPath} -- {java.MajorVersion}");

Console.WriteLine();
var process = await launcher.LaunchAsync(installResult?.Minecraft!, new LaunchConfig {
    Account = new OfflineAuthenticator().Authenticate("Offline429"),
    JavaPath = await javaProvider.GetJavaEntryAsync(Console.ReadLine()!),
    LauncherName = "Iridium"
});

process.OutputLogReceived +=  (_, args) => Console.WriteLine(args.Data);

await process.Process?.WaitForExitAsync()!;

Console.WriteLine();
Console.WriteLine(string.Join('\n', process.ArgumentList));
Console.WriteLine("Done!");
Console.ReadKey();