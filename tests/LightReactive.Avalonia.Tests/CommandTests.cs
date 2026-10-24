using Avalonia.Threading;
using LightReactive.Avalonia.Commands;

namespace LightReactive.Avalonia.Tests;

/// <summary>
/// Command tests: CanExecute driven by the canExecute stream and CanExecuteChanged notification on the UI thread.
/// </summary>
public class CommandTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public CommandTests() => TestUiThread.EnsureStarted();

    [Fact]
    public void Create_CanExecuteStream_FollowsLatestValue()
    {
        Subject<bool> canExecute = new();
        int runs = 0;
        using ICommand<Unit, Unit> command = CommandHelper.Create(() => { runs++; }, canExecute);

        Assert.False(command.CanExecute(null));
        canExecute.OnNext(true);
        Assert.True(command.CanExecute(null));

        command.Execute();
        Assert.Equal(1, runs);
    }

    [Fact]
    public void Execute_WrongParameterType_Throws()
    {
        using ICommand<int, Unit> command = CommandHelper.Create<int>(_ => { });

        Assert.Throws<InvalidOperationException>(() => ((System.Windows.Input.ICommand)command).Execute("text"));
    }

    [Fact]
    public async Task CanExecuteChanged_FromBackgroundWhileUiThreadWaits_DoesNotBlock()
    {
        Subject<bool> canExecute = new();
        using ICommand<Unit, Unit> command = CommandHelper.Create(() => { }, canExecute);
        TaskCompletionSource<bool> raisedOnUiThread = new(TaskCreationOptions.RunContinuationsAsynchronously);
        command.CanExecuteChanged += (_, _) => raisedOnUiThread.TrySetResult(Dispatcher.UIThread.CheckAccess());

        // The UI thread waits for a background thread that changes canExecute. Synchronous delivery
        // of the notification to the UI thread would deadlock here.
        bool finished = false;
        await TestUiThread.InvokeAsync(() => finished = Task.Run(() => canExecute.OnNext(true)).Wait(Timeout));

        Assert.True(finished);
        Assert.True(await raisedOnUiThread.Task.WaitAsync(Timeout, TestContext.Current.CancellationToken));
        Assert.True(command.CanExecute(null));
    }
}
