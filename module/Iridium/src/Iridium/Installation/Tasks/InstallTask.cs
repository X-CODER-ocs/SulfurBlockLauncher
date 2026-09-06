namespace Iridium.Installation.Tasks;

/// <summary>
/// An installation task to be executed: the ordered set of steps and their dependency relations.
/// Not a linear pipeline — steps with no transitive ordering may run in parallel.
///
/// A task is built with the fluent DSL; every step must carry an explicit
/// <see cref="InstallStepKey"/> so steps can be referenced by <c>After</c>/<c>Before</c> and
/// de-duplicated by <see cref="Combine"/>:
/// <code>
/// InstallTask.Define(task =&gt; task
///     .Do(VanillaInstaller.DownloadVersion, "Download Version", DownloadVersionAsync)
///     .Then(VanillaInstaller.ResolveVersion, "Resolve Version", ResolveVersionAsync));
/// </code>
/// </summary>
public sealed class InstallTask {
    private readonly List<InstallStepNode> _nodes = [];
    private List<InstallStepKey> _frontier = [];

    internal IReadOnlyList<InstallStepNode> Nodes => _nodes;

    internal InstallTask() { }

    private InstallTask(IReadOnlyList<InstallStepNode> nodes) {
        _nodes.AddRange(nodes);
        _frontier = Leaves();
    }

    /// <summary>
    /// Describes an installation task with the fluent DSL. Sequential chains use
    /// <c>Do</c>/<c>Then</c>; special steps use <c>After</c>/<c>Before</c> to insert at a
    /// specific step.
    /// </summary>
    public static InstallTask Define(Action<InstallTask> configure) {
        ArgumentNullException.ThrowIfNull(configure);

        var task = new InstallTask();
        configure(task);
        return task;
    }

    /// <summary>
    /// Merges multiple tasks into a single DAG. Steps that share the same
    /// <see cref="InstallStepKey"/> are treated as the same logical step: the first occurrence
    /// is kept, its dependencies are merged with the duplicates', and every step that
    /// referenced the key keeps referencing that single instance — so the shared step runs
    /// exactly once while all predecessor/successor relations from every task are preserved.
    /// Steps with different keys always remain separate nodes.
    /// </summary>
    public static InstallTask Combine(params InstallTask[] tasks) {
        ArgumentNullException.ThrowIfNull(tasks);

        var merged = new List<InstallStepNode>();
        var indexByKey = new Dictionary<InstallStepKey, int>();

        foreach (var task in tasks) {
            ArgumentNullException.ThrowIfNull(task);

            foreach (var node in task._nodes) {
                if (!indexByKey.TryGetValue(node.Key, out var existingIndex)) {
                    indexByKey[node.Key] = merged.Count;
                    merged.Add(node);
                } else {
                    // Same logical step declared by another task: keep the first implementation
                    // and merge the dependency sets.
                    var existing = merged[existingIndex];
                    var dependencies = existing.DependsOn
                        .Concat(node.DependsOn)
                        .Where(d => d != node.Key)
                        .Distinct()
                        .ToList();
                    merged[existingIndex] = existing with { DependsOn = dependencies };
                }
            }
        }

        return new InstallTask(merged);
    }

    /// <summary>
    /// Executes this task. Aggregated <see cref="InstallProgress"/> snapshots are published
    /// synchronously through <paramref name="reportProgress"/> (in the current execution
    /// thread, never marshalled to a UI thread); the completed <see cref="InstallResult"/>
    /// carries the shared <see cref="InstallState"/> the steps populated. A caller may pass a
    /// pre-seeded <paramref name="state"/> to inject settings before execution.
    /// </summary>
    public async System.Threading.Tasks.Task<InstallResult> InstallAsync(
        Action<InstallProgress>? reportProgress = null,
        InstallState? state = null,
        CancellationToken ct = default) {
        state ??= new InstallState();
        return await InstallTaskExecutor.ExecuteAsync(this, state, reportProgress, ct).ConfigureAwait(false);
    }

    /// <summary>Adds an independent step with a stable key separate from its display name.</summary>
    public InstallTask Do(InstallStepKey key, string name, InstallStepHandler handler) =>
        Do(new InstallStep(key, name, handler));

    /// <summary>Adds an independent class-based step; see the delegate overload.</summary>
    public InstallTask Do(IInstallStep step) {
        Add(step, []);
        return this;
    }

    /// <summary>Adds a step with a stable key that runs after the current frontier.</summary>
    public InstallTask Then(InstallStepKey key, string name, InstallStepHandler handler) =>
        Then(new InstallStep(key, name, handler));

    /// <summary>Adds a class-based step that runs after the current frontier.</summary>
    public InstallTask Then(IInstallStep step) {
        Add(step, _frontier);
        return this;
    }

    /// <summary>
    /// Inserts a step immediately after the step identified by <paramref name="dependsOn"/>.
    /// Everything that previously ran after that step now waits for the inserted step too, so
    /// the pipeline becomes <c>dependsOn → new step → former successors</c>.
    /// </summary>
    public InstallTask After(InstallStepKey dependsOn, InstallStepKey key, string name, InstallStepHandler handler) =>
        After(dependsOn, new InstallStep(key, name, handler));

    /// <summary>Inserts a class-based step immediately after the step identified by <paramref name="dependsOn"/>.</summary>
    public InstallTask After(InstallStepKey dependsOn, IInstallStep step) {
        Require(dependsOn);

        var key = Add(step, [dependsOn]);

        // Re-route: every step that previously depended on `dependsOn` now waits for the new step,
        // so it is inserted into the linear pipeline rather than added as a sibling.
        for (var i = 0; i < _nodes.Count; i++) {
            var node = _nodes[i];
            if (node.Key == key ||
                !node.DependsOn.Contains(dependsOn) ||
                node.DependsOn.Contains(key))
                continue;

            _nodes[i] = node with { DependsOn = [.. node.DependsOn, key] };
        }

        return this;
    }

    /// <summary>
    /// Inserts a step immediately before the step identified by <paramref name="followedBy"/>.
    /// The new step waits for everything <paramref name="followedBy"/> waited for, and
    /// <paramref name="followedBy"/> now waits for the new step.
    /// </summary>
    public InstallTask Before(InstallStepKey followedBy, InstallStepKey key, string name, InstallStepHandler handler) =>
        Before(followedBy, new InstallStep(key, name, handler));

    /// <summary>Inserts a class-based step immediately before the step identified by <paramref name="followedBy"/>.</summary>
    public InstallTask Before(InstallStepKey followedBy, IInstallStep step) {
        Require(followedBy);

        var followed = _nodes.First(node => node.Key == followedBy);
        var key = Add(step, followed.DependsOn);
        AddDependency(followedBy, key);

        return this;
    }

    /// <summary>
    /// Fans out several steps with explicit keys, each running after the current frontier. A
    /// subsequent <see cref="Then(IInstallStep)"/> waits on all of them (join point).
    /// </summary>
    public InstallTask Parallel(params (InstallStepKey Key, string Name, InstallStepHandler Handler)[] steps) =>
        Parallel([.. steps.Select(static step => (IInstallStep)new InstallStep(step.Key, step.Name, step.Handler))]);

    /// <summary>Fans out several class-based steps; see the tuple overload.</summary>
    public InstallTask Parallel(params IInstallStep[] steps) {
        if (steps.Length == 0)
            return this;

        // Snapshot the frontier so every parallel step depends on the SAME set — they are
        // siblings, not a chain. (Add() advances the frontier after each insert.)
        var dependsOn = _frontier.ToArray();
        var keys = new InstallStepKey[steps.Length];
        for (var i = 0; i < steps.Length; i++)
            keys[i] = Add(steps[i], dependsOn);

        _frontier = [.. keys];
        return this;
    }

    internal void Validate() {
        foreach (var node in _nodes)
            foreach (var dependency in node.DependsOn)
                if (_nodes.All(n => n.Key != dependency))
                    throw new InvalidOperationException($"Step '{node.Key}' depends on undefined step '{dependency}'.");

        EnsureAcyclic();
    }

    private InstallStepKey Add(IInstallStep step, IReadOnlyList<InstallStepKey> dependencies) {
        ArgumentNullException.ThrowIfNull(step);

        var key = step.Key;
        if (string.IsNullOrWhiteSpace(key.Value))
            throw new InvalidOperationException("Install step must declare a non-empty InstallStepKey.");
        if (_nodes.Any(n => n.Key == key))
            throw new InvalidOperationException($"Install task already contains step '{key}'.");

        var deps = dependencies.Where(d => d != key).Distinct().ToList();
        _nodes.Add(new InstallStepNode { Key = key, Step = step, DependsOn = deps });
        _frontier = [key];
        return key;
    }

    private void AddDependency(InstallStepKey stepId, InstallStepKey dependsOn) {
        for (var i = 0; i < _nodes.Count; i++) {
            var node = _nodes[i];
            if (node.Key != stepId)
                continue;

            if (!node.DependsOn.Contains(dependsOn))
                _nodes[i] = node with { DependsOn = [.. node.DependsOn, dependsOn] };
            return;
        }
    }

    private void Require(InstallStepKey id) {
        if (_nodes.All(n => n.Key != id))
            throw new InvalidOperationException($"Install task does not contain step '{id}'.");
    }

    private List<InstallStepKey> Leaves() {
        var depended = _nodes.SelectMany(n => n.DependsOn).ToHashSet();
        return [.. _nodes.Select(n => n.Key).Where(k => !depended.Contains(k))];
    }

    private void EnsureAcyclic() {
        const int visiting = 1, visited = 2;
        var marks = new Dictionary<InstallStepKey, int>();
        var byKey = _nodes.ToDictionary(n => n.Key);

        foreach (var key in byKey.Keys)
            Dfs(key);

        void Dfs(InstallStepKey key) {
            if (marks.GetValueOrDefault(key) == visited)
                return;
            if (marks.GetValueOrDefault(key) == visiting)
                throw new InvalidOperationException($"Install task contains a dependency cycle involving '{key}'.");

            marks[key] = visiting;
            foreach (var dependency in byKey[key].DependsOn)
                Dfs(dependency);
            marks[key] = visited;
        }
    }
}