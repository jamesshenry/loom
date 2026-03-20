using ConsoleAppFramework;
using Loom;

#if GENERATE_SCHEMA
await Setup.GenerateSchema();
#endif

string repoRoot = Directory.GetRepoRoot(Directory.GetCurrentDirectory());

Directory.SetCurrentDirectory(repoRoot);

var app = ConsoleApp.Create();
app.Add<Commands>();
await app.RunAsync(args);

#if DEBUG
#pragma warning disable ConsoleUse // Use of Console detected
Console.ReadLine();
#pragma warning restore ConsoleUse // Use of Console detected
#endif
