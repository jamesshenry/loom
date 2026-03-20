using System.Text.Json;
using ConsoleAppFramework;
using Loom;
using Loom.Config;
using NJsonSchema;
using NJsonSchema.Generation;
#pragma warning disable ConsoleUse // Use of Console detected

// #if GENERATE_SCHEMA
// var settings = new SystemTextJsonSchemaGeneratorSettings
// {
//     SerializerOptions = { PropertyNamingPolicy = JsonNamingPolicy.CamelCase },
// };

// var schema = JsonSchema.FromType<LoomSettings>(settings);

// schema.Properties["$schema"] = new JsonSchemaProperty
// {
//     Type = JsonObjectType.String,
//     Description = "The URL to the JSON schema for this file.",
// };

// var ridSchema = new JsonSchema
// {
//     Type = JsonObjectType.String,
//     Enumeration = { "win-x64", "win-arm64", "linux-x64", "linux-arm64", "osx-x64", "osx-arm64" },
// };

// schema.Definitions["RuntimeIdentifier"] = ridSchema;

// foreach (var def in schema.Definitions.Values)
// {
//     foreach (var prop in def.Properties)
//     {
//         if (prop.Key.Equals("rid", StringComparison.OrdinalIgnoreCase))
//         {
//             prop.Value.Reference = ridSchema;
//             prop.Value.Type = JsonObjectType.None; // remove inline type
//         }
//     }
// }
// foreach (var def in schema.Definitions.Values)
// {
//     foreach (var prop in def.Properties.Values)
//     {
//         if (prop.Type.HasFlag(JsonObjectType.Null))
//         {
//             prop.Type &= ~JsonObjectType.Null;
//         }
//     }
// }

// var json = schema.ToJson();
// var schemaJson = schema.ToJson();
// await File.WriteAllTextAsync("loom.schema.json.example", schemaJson);
// #endif

string repoRoot = Directory.GetRepoRoot(Directory.GetCurrentDirectory());

Directory.SetCurrentDirectory(repoRoot);

var app = ConsoleApp.Create();
app.Add<Commands>();
await app.RunAsync(args);

#if DEBUG
Console.ReadLine();
#endif
#pragma warning restore ConsoleUse // Use of Console detected
