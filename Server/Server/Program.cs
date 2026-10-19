using LockStepServer;

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    await Bootstrap.RunAsync(cts.Token);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Bye.");
}
