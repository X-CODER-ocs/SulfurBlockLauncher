namespace MinecraftLaunch.Base.Enums;

public enum InstallStep {
    //Common
    Started,
    DownloadVersionJson,
    ParseMinecraft,
    DownloadLibraries,
    DownloadAssetIndexFile,
    RanToCompletion,

    //Copy local resources
    CopyLibraries,

    //Forge Optifine
    DownloadPackage,
    ParsePackage,
    WriteVersionJsonAndSomeDependencies,
    RunInstallProcessor,

    //Modpack
    ParseFiles,
    ParseDownloadUrls,
    DownloadMods,
    ExtractModpack,
    RedirectInvalidMod,

    //Composite
    ParseInstaller,
    InstallVanilla,
    InstallPrimaryModLoader,
    InstallSecondaryModLoader,

    //Java
    FetchingMetadata,
    DownloadJava,
    ExtractingFiles,

    //Error handle
    Interrupted = -1,

    //undefined
    Undefined = -2
}