using MinecraftLaunch.Base.Enums;
using MinecraftLaunch.Base.EventArgs;
using MinecraftLaunch.Base.Interfaces;
using MinecraftLaunch.Base.Models.Game;
using MinecraftLaunch.Base.Models.Network;
using MinecraftLaunch.Extensions;

namespace MinecraftLaunch.Components.Installer;

public sealed class CompositeInstaller : InstallerBase {
    public string JavaPath { get; init; }
    public string CustomId { get; init; }
    public override string MinecraftFolder { get; init; }
    public IEnumerable<IInstallEntry> InstallEntries { get; init; }

    public new event EventHandler<CompositeInstallProgressChangedEventArgs> ProgressChanged;

    internal InstallerBase PrimaryInstaller { get; set; }
    internal InstallerBase SecondaryInstaller { get; set; }
    internal VanillaInstaller VanillaInstaller { get; set; }

    public static CompositeInstaller Create(IEnumerable<IInstallEntry> installEntries, string mcFolder, string javaPath = default, string customId = default) {
        return new CompositeInstaller {
            JavaPath = javaPath,
            CustomId = customId,
            MinecraftFolder = mcFolder,
            InstallEntries = installEntries,
        };
    }

    public override async Task<MinecraftEntry> InstallAsync(CancellationToken cancellationToken = default) {
        MinecraftEntry minecraft = null;

        ReportProgress(InstallStep.Started, 0.0d, TaskStatus.WaitingToRun, 1, 1);

        try {
            ParseInstaller(cancellationToken);

            minecraft = await InstallVanillaAsync(cancellationToken);

            var modifiedMinecraft = await InstallPrimaryModLoaderAsync(minecraft, cancellationToken);
            modifiedMinecraft = await InstallSecondaryModLoaderAsync(modifiedMinecraft, cancellationToken);

            minecraft = modifiedMinecraft;
            ReportProgress(InstallStep.RanToCompletion, 1.0d, TaskStatus.RanToCompletion, 1, 1);
            ReportCompleted(true);
        } catch (Exception ex) {
            ReportProgress(InstallStep.Interrupted, 1.0d, TaskStatus.Faulted, 1, 1);
            ReportCompleted(false, ex);
            throw;
        }

        return minecraft;
    }

    #region Privates

    private void ParseInstaller(CancellationToken cancellationToken) {
        ReportProgress(InstallStep.ParseInstaller, 0.1d, TaskStatus.Running, 1, 0);

        var entries = InstallEntries?.ToArray()
            ?? throw new ArgumentNullException(nameof(InstallEntries));

        if (entries.Length == 0)
            throw new ArgumentException("At least one install entry is required.", nameof(InstallEntries));

        if (entries.Length > 3)
            throw new ArgumentOutOfRangeException(nameof(InstallEntries), "Only vanilla, one primary loader, and OptiFine are supported.");

        var vanillaEntries = entries.OfType<VersionManifestEntry>().ToArray();
        var primaryEntries = entries.Where(static x => x is ForgeInstallEntry or FabricInstallEntry or QuiltInstallEntry).ToArray();
        var optifineEntries = entries.OfType<OptifineInstallEntry>().ToArray();
        if (vanillaEntries.Length != 1 || primaryEntries.Length > 1 || optifineEntries.Length > 1)
            throw new ArgumentException("Select exactly one vanilla version, at most one primary loader, and at most one OptiFine version.", nameof(InstallEntries));

        var minecraftVersion = vanillaEntries[0].McVersion;
        if (entries.OfType<IInstallEntry>().Any(x => x is not VersionManifestEntry && x.McVersion != minecraftVersion))
            throw new ArgumentException("All selected loaders must target the selected Minecraft version.", nameof(InstallEntries));

        if (optifineEntries.Length == 1 && primaryEntries.SingleOrDefault() is ForgeInstallEntry { IsNeoforge: true })
            throw new NotSupportedException("OptiFine is not compatible with NeoForge. Use an alternative such as Embeddium instead.");

        foreach (var entry in entries) {
            if (entry is VersionManifestEntry ve) {
                VanillaInstaller = VanillaInstaller.Create(MinecraftFolder, ve);
                continue;
            }

            if (entry is OptifineInstallEntry oe) {
                SecondaryInstaller = OptifineInstaller.Create(MinecraftFolder, JavaPath, oe, CustomId);
                continue;
            }

            if (entry is ForgeInstallEntry fe) {
                PrimaryInstaller = ForgeInstaller.Create(MinecraftFolder, JavaPath, fe, CustomId);
            } else if (entry is FabricInstallEntry fae) {
                PrimaryInstaller = FabricInstaller.Create(MinecraftFolder, fae, CustomId);
            } else if (entry is QuiltInstallEntry qe) {
                PrimaryInstaller = QuiltInstaller.Create(MinecraftFolder, qe, CustomId);
            }
        }

        ReportProgress(InstallStep.ParseInstaller, 0.2d, TaskStatus.Running, 1, 1);
    }

    private Task<MinecraftEntry> InstallVanillaAsync(CancellationToken cancellationToken) {
        if (VanillaInstaller is null) {
            throw new ArgumentNullException(nameof(VanillaInstaller));
        }

        VanillaInstaller.ProgressChanged += (_, arg) =>
            ReportProgress(arg.StepName, arg.Progress.ToPercentage(0.2d, 0.4d),
                arg.Status, arg.TotalStepTaskCount, arg.FinishedStepTaskCount, InstallStep.InstallVanilla,
                    arg.Speed, arg.IsStepSupportSpeed);

        VanillaInstaller.Completed += (_, arg) => {
            if (!arg.IsSuccessful)
                throw arg.Exception;
        };

        return VanillaInstaller.InstallAsync(cancellationToken);
    }

    private Task<MinecraftEntry> InstallPrimaryModLoaderAsync(MinecraftEntry entry, CancellationToken cancellationToken) {
        if (PrimaryInstaller is null) {
            return Task.FromResult(entry);
        }

        PrimaryInstaller.ProgressChanged += (_, arg) =>
            ReportProgress(arg.StepName, arg.Progress.ToPercentage(0.4d, 0.7d),
                arg.Status, arg.TotalStepTaskCount, arg.FinishedStepTaskCount, InstallStep.InstallPrimaryModLoader,
                    arg.Speed, arg.IsStepSupportSpeed);

        PrimaryInstaller.Completed += (_, arg) => {
            if (!arg.IsSuccessful)
                throw arg.Exception;
        };

        return PrimaryInstaller.InstallAsync(cancellationToken);
    }

    private Task<MinecraftEntry> InstallSecondaryModLoaderAsync(MinecraftEntry entry, CancellationToken cancellationToken) {
        if (SecondaryInstaller is null) {
            return Task.FromResult(entry);
        }

        if (SecondaryInstaller is OptifineInstaller oi) {
            oi.Minecraft = entry;
        }

        SecondaryInstaller.ProgressChanged += (_, arg) =>
            ReportProgress(arg.StepName, arg.Progress.ToPercentage(0.7d, 0.9d),
                arg.Status, arg.TotalStepTaskCount, arg.FinishedStepTaskCount, InstallStep.InstallSecondaryModLoader,
                    arg.Speed, arg.IsStepSupportSpeed);

        SecondaryInstaller.Completed += (_, arg) => {
            if (!arg.IsSuccessful)
                throw arg.Exception;
        };

        return SecondaryInstaller.InstallAsync(cancellationToken);
    }

    internal void ReportProgress(InstallStep step, double progress, TaskStatus status, int totalCount, int finshedCount,
        InstallStep primaryStep = InstallStep.Undefined, double speed = -1, bool isSupportStep = false) {
        ProgressChanged?.Invoke(this, new CompositeInstallProgressChangedEventArgs {
            Speed = speed,
            Status = status,
            StepName = step,
            Progress = progress,
            TotalStepTaskCount = totalCount,
            IsStepSupportSpeed = isSupportStep,
            FinishedStepTaskCount = finshedCount,
            PrimaryStepName = primaryStep
        });
    }

    #endregion
}