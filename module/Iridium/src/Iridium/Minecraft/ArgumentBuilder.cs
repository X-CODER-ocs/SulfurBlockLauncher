namespace Iridium.Minecraft;

/// <summary>
/// Collects format-specific launch arguments contributed by an
/// <see cref="Iridium.Interfaces.IFormatProvider"/> during argument building.
/// </summary>
public sealed class ArgumentBuilder {
    private readonly List<string> _jvm = [];
    private readonly List<string> _game = [];

    public IReadOnlyList<string> JvmArguments => _jvm;
    public IReadOnlyList<string> GameArguments => _game;

    /// <summary>Overrides the resolved main class when set.</summary>
    public string? MainClass { get; set; }

    public ArgumentBuilder AddJvm(params string[] arguments) {
        _jvm.AddRange(arguments);
        return this;
    }

    public ArgumentBuilder AddGame(params string[] arguments) {
        _game.AddRange(arguments);
        return this;
    }
}
