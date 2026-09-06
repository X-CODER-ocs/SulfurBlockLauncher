# 任务管理

`Tio.Avalonia.Standard.Modules.Tasks` 提供应用生命周期内统一的任务记录、嵌套、取消、进度、描述、日志和 UI 操作能力。

## 基本用法

通过全局单例 `TaskManager.Instance` 创建根任务。创建只会生成状态为 `Pending` 的实体和任务树，绝不会自动执行；在验证、配置和子任务添加完成后，调用 `Start()` 才启动任务。

```csharp
using Tio.Avalonia.Standard.Modules.Tasks;

var task = TaskManager.Instance.CreateTask(
    new TaskOptions
    {
        Name = "下载整合包",
        Description = "正在准备下载"
    },
    async context =>
    {
        context.LogInformation("开始下载版本清单。");
        context.SetWaiting("等待网络连接");

        await WaitForNetworkAsync(context.CancellationToken);

        context.SetRunning("正在下载文件");
        await DownloadAsync(context.CancellationToken, context.ReportProgress);
        context.LogInformation("下载完成。");
    });

// 此前可继续按账户类型配置子任务、添加操作或进行最后验证。
task.Start();

await task.Completion;
```

## 描述与进度

`Description` 用于显示当前工作说明，例如当前下载文件或等待原因。执行器可通过 `SetWaiting`、`SetRunning` 同时更新状态和说明，也可单独使用 `SetDescription` 更新说明。

进度以 `0` 到 `1` 的值上报；`null` 表示当前无法确定进度。任务完成时进度自动设为 `1`。父任务的 `AggregateProgress` 会平均当前节点和所有后代节点的已知进度，适合用于展示多阶段流程的总进度，避免每个子任务开始时进度条回到零。

```csharp
context.SetDescription("正在下载 client.jar");
context.ReportProgress(0.35);

// 不确定总大小时可显示不确定进度条。
context.ReportProgress(null);
```

`TaskManager.Instance.CurrentTask` 会遍历整个任务树，并按照失败、执行中、等待、成功的优先级返回应显示的任务；同一状态会选择后创建的节点。标题栏等全局 UI 可绑定其 `CurrentStatus` 和 `CurrentTask`。

已完成的根任务可在 UI 中调用 `TaskManager.Instance.RemoveCompletedTask(task)` 从列表移除。该方法只移除 `Completed` 根任务，失败和已取消任务会保留以便检查状态和日志。

业务代码必须将 `context.CancellationToken` 传入支持取消的异步 API，并在自行实现的长循环中定期调用 `ThrowIfCancellationRequested()`。取消信号到达后，任务可以先执行清理工作，随后抛出 `OperationCanceledException` 或正常返回；两种方式都会在取消已请求时使任务最终变为 `Cancelled`。

```csharp
async Task DownloadAsync(CancellationToken cancellationToken)
{
    try
    {
        await DownloadFilesAsync(cancellationToken);
    }
    finally
    {
        // 收尾尚未完成前，任务状态保持 Cancelling。
        await RemoveTemporaryFilesAsync();
    }
}
```

## 任务状态

| 状态 | 含义 |
| --- | --- |
| `Pending` | 已创建，执行器尚未开始。 |
| `Waiting` | 执行器已开始，正在等待网络、用户输入或其他外部条件。 |
| `Running` | 正在执行。 |
| `Cancelling` | 已发出取消信号，任务正在完成清理。 |
| `Cancelled` | 取消和清理已经完成。 |
| `Completed` | 正常完成。 |
| `Faulted` | 执行器出现非取消异常。 |

`Completed`、`Cancelled` 和 `Faulted` 是终态，不能再改变。调用 `RequestCancellation()` 时，状态会先转为 `Cancelling`，不会立即变成 `Cancelled`。

## 延迟构建与启动

适用于启动器一类的链式流程：先创建任务实体，按账户类型配置完整任务树，完成验证后再统一启动。子任务创建时同样不会自动执行。

```csharp
var launchTask = TaskManager.Instance.CreateTask(
    new TaskOptions { Name = "启动游戏", Description = "正在配置启动流程" });

var accountTask = launchTask.CreateChild(
    new TaskOptions { Name = "验证账户" });

if (account.Type == AccountType.Microsoft)
{
    accountTask.CreateChild(new TaskOptions { Name = "刷新 Microsoft 令牌" });
}
else
{
    accountTask.CreateChild(new TaskOptions { Name = "验证离线账户" });
}

var launchProcessTask = launchTask.CreateChild(
    new TaskOptions { Name = "启动游戏进程" });

// 此处执行启动参数、文件和账户等最终验证。验证通过后才启动。
ValidateLaunchOptions();
launchTask.Start();
accountTask.Start();
foreach (var child in accountTask.Children) child.Start();
launchProcessTask.Start();
```

无执行器任务在 `Start()` 后会进入 `Running`，由业务逻辑在合适时机调用 `Complete()` 或 `Fault(exception)`。这适合任务的实际工作由事件、进程回调或链式调用推进的情况。

```csharp
launchProcessTask.Start();

try
{
    await StartGameProcessAsync(launchProcessTask.CancellationToken);
    launchProcessTask.Complete();
}
catch (Exception exception)
{
    launchProcessTask.Fault(exception);
}
```

也可以在最后启动时为先前无执行器的任务提供执行器：

```csharp
launchTask.Start(async context =>
{
    await StartGameAsync(context.CancellationToken);
});
```

## 嵌套任务

可通过 `ManagedTask.CreateChild` 在启动前构建子任务，也可以在父任务执行器内通过 `TaskExecutionContext.CreateChild` 动态添加。子任务与父任务共享取消链：取消父任务会请求取消所有后代任务；取消子任务只影响该子树，不会自动取消父任务和兄弟任务。

```csharp
var task = TaskManager.Instance.CreateTask(
    new TaskOptions { Name = "安装整合包" },
    async context =>
    {
        var download = context.CreateChild(
            new TaskOptions { Name = "下载文件" },
            async child => await DownloadAsync(child.CancellationToken));

        var verify = context.CreateChild(
            new TaskOptions { Name = "校验文件" },
            async child => await VerifyAsync(child.CancellationToken));

        await Task.WhenAll(download.Completion, verify.Completion);
    });

task.Start();
```

`TaskManager.Instance.RootTasks` 只包含根任务。每个 `ManagedTask.Children` 都是可绑定的子任务集合，因此从根任务可以得到完整任务树。任务不会自动移除，直至应用进程结束。

## 任务日志

任务拥有独立的结构化日志列表 `Logs`。使用执行上下文写入日志：

```csharp
context.LogDebug("请求地址已生成。");
context.LogInformation("已完成 3/10 个文件。");
context.LogWarning("镜像源响应较慢，准备重试。");
context.LogError("文件校验失败。", exception);
```

通过 `task.GetFormattedLog()` 获取适合复制或导出的文本。日志属于当前节点，不会自动混入子任务日志；如果需要包含子任务的完整日志，可在应用层递归组合。

## UI 操作

每个任务的 `Actions` 是 `TaskAction` 的只读集合。它包含默认的“取消任务”操作，也可以通过 `TaskOptions.Actions` 注册业务操作。`TaskAction.Command` 是 CommunityToolkit.Mvvm 的 `IAsyncRelayCommand`，可直接绑定 Avalonia 按钮。

```csharp
var task = TaskManager.Instance.CreateTask(
    new TaskOptions
    {
        Name = "下载整合包",
        Actions =
        [
            new TaskActionDefinition
            {
                Name = "复制日志信息",
                IconKey = "Copy",
                ExecuteAsync = async (managedTask, cancellationToken) =>
                {
                    await clipboard.SetTextAsync(managedTask.GetFormattedLog(), cancellationToken);
                },
                CanExecute = managedTask => managedTask.Logs.Count > 0
            },
            new TaskActionDefinition
            {
                Name = "打开日志浏览器",
                IconKey = "DocumentSearch",
                ExecuteAsync = (managedTask, _) =>
                {
                    logViewer.Open(managedTask);
                    return Task.CompletedTask;
                }
            }
        ]
    },
    async context => await DownloadAsync(context.CancellationToken));

task.Start();
```

运行中的任务也可以动态添加操作，这适合进程启动后才出现的“打开日志浏览器”等交互：

```csharp
task.Start();
task.AddAction(new TaskActionDefinition
{
    Name = "打开日志浏览器",
    IconKey = "DocumentSearch",
    ExecuteAsync = (managedTask, _) =>
    {
        logViewer.Open(managedTask);
        return Task.CompletedTask;
    },
    IsVisible = managedTask => managedTask.Status == ManagedTaskStatus.Running
});
```

按钮绑定示例：

```xml
<ItemsControl ItemsSource="{Binding SelectedTask.Actions}">
    <ItemsControl.ItemTemplate>
        <DataTemplate>
            <Button Content="{Binding Name}"
                    Command="{Binding Command}"
                    IsVisible="{Binding IsVisible}"
                    IsEnabled="{Binding IsEnabled}" />
        </DataTemplate>
    </ItemsControl.ItemTemplate>
</ItemsControl>
```

操作的 `CanExecute`、`IsVisible` 会在任务状态或日志变更时自动刷新。例如默认的“取消任务”只会在 `Pending`、`Waiting`、`Running` 时可用，在 `Cancelling` 时不可用，在终态时隐藏。操作本身的异常不会将主任务改为 `Faulted`，而会写入该任务日志。

标准库不直接依赖剪贴板、窗口或导航实现。请在应用层的操作委托中调用自己的剪贴板服务、对话框服务或日志浏览器，以保持任务库与具体 UI 解耦。

## 线程约束

`RootTasks`、`Children`、`Actions` 和 `Logs` 是用于 UI 绑定的 `ObservableCollection`。在 Avalonia 应用中，从后台线程创建任务、子任务或写入日志时，应通过 `Dispatcher.UIThread.InvokeAsync` 或 `Post` 切换到 UI 线程，避免跨线程修改绑定集合。

任务执行器自身不受此限制，可运行普通异步 I/O 或后台计算；只需把集合和可绑定属性的更新切换回 UI 线程。
