namespace Iridium.Installation.Tasks;

/// <summary>
/// Type-safe identity of an install step. Values are stable, comparable and usable as
/// dictionary keys. The raw string only ever appears where a step key is defined (e.g.
/// <c>VanillaSteps.ResolveVersion</c>); display names stay in <see cref="IInstallStep.Name"/>.
/// </summary>
public readonly record struct InstallStepKey(string Value) {
    public override string ToString() => Value;

    public static implicit operator InstallStepKey(string value) => new(value);
}