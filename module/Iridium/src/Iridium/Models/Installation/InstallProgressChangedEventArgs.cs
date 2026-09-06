
using Iridium.Installation.Tasks;

namespace Iridium.Models.Installation;

public sealed class InstallProgressChangedEventArgs : EventArgs {
    public InstallProgress Progress { get; }

    internal InstallProgressChangedEventArgs(InstallProgress progress) {
        Progress = progress;
    }
}
