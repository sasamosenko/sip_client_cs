using System;
using System.Linq;
using System.Reflection;

var sipsorceryDll = System.IO.Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    ".nuget", "packages", "sipsorcery", "6.0.2", "lib", "net6.0", "SIPSorcery.dll");
var asm = System.Reflection.Assembly.LoadFrom(sipsorceryDll);

var voipType = asm.GetTypes().FirstOrDefault(t => t.Name == "VoIPMediaSession");
if (voipType != null) {
    Console.WriteLine("=== VoIPMediaSession properties ===");
    foreach (var p in voipType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        Console.WriteLine($"  {p.PropertyType.Name} {p.Name}");

    Console.WriteLine("\n=== VoIPMediaSession methods ===");
    foreach (var m in voipType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        Console.WriteLine($"  {m.ReturnType.Name} {m.Name}({string.Join(", ", m.GetParameters().Select(x => $"{x.ParameterType.Name} {x.Name}"))})");

    Console.WriteLine("\n=== VoIPMediaSession constructors ===");
    foreach (var c in voipType.GetConstructors())
        Console.WriteLine($"  ({string.Join(", ", c.GetParameters().Select(x => $"{x.ParameterType.Name} {x.Name}"))})");
}

var mediaType = asm.GetTypes().FirstOrDefault(t => t.Name == "MediaEndPoints");
if (mediaType != null) {
    Console.WriteLine("\n=== MediaEndPoints properties ===");
    foreach (var p in mediaType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        Console.WriteLine($"  {p.PropertyType.Name} {p.Name}");
}
