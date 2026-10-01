// Start Play mode from Bootstrap and run after Inventory has loaded.
if (!Application.isPlaying) throw new System.Exception("Enter Play mode from Bootstrap first.");
if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "Inventory")
    throw new System.Exception("Bootstrap must finish loading Inventory.");
if (!Assets.Scripts.ReactionCatalog.IsInitialized || Assets.Scripts.ReactionCatalog.CompilationCount != 1)
    throw new System.Exception("Bootstrap must compile exactly once before Inventory.");
var compiled = Assets.Scripts.ReactionCatalog.Reactions;
var first = new Assets.Scripts.RunLoadout(new[] { 1000, 1009 });
var second = new Assets.Scripts.RunLoadout(new[] { 1009, 1000, 1000 });
if (!first.Reactions.SequenceEqual(second.Reactions) || !object.ReferenceEquals(compiled, Assets.Scripts.ReactionCatalog.Reactions) ||
    Assets.Scripts.ReactionCatalog.CompilationCount != 1)
    throw new System.Exception("Changing equipped grants must reuse the compiled catalog.");
System.IO.Directory.CreateDirectory("Temp/SpellCompiler");
string result = "PASS: Bootstrap compiled once before Inventory; loadouts reuse compiled mappings.";
System.IO.File.WriteAllText("Temp/SpellCompiler/Bootstrap.txt", result);
return result;
