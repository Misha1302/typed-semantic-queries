using System.Reflection;
using System.Runtime.Loader;
using MiniCompiler.IR;
using Packages.ArraySemantics;
using Packages.BoundsBridge;
using Packages.BoundsOptimizer;
using SemanticQueries.Core;

const string ExternalAssemblyName = "ExternalModules.RangeProvider";

if (args.Length != 1)
    throw new InvalidOperationException("Expected one argument: path to the independently built provider assembly.");

var hostReferences = Assembly.GetExecutingAssembly().GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
if (hostReferences.Contains(ExternalAssemblyName, StringComparer.Ordinal))
    throw new InvalidOperationException("Acceptance host must not have a compile-time reference to the external provider module.");

var pluginPath = Path.GetFullPath(args[0]);
if (!File.Exists(pluginPath))
    throw new FileNotFoundException("External provider module was not built.", pluginPath);

var index = new ValueId("i");
var array = new ArrayId("a");
var point = new ProgramPoint("body");
var unit = new CompilationUnit().Array(array, 4);
var check = new BoundsCheck(array, index, point);
var optimizer = new BoundsCheckOptimizer();

static SemanticSession Session(CompilationUnit unit, StaticPlan plan) =>
    new(plan, unit.Revision, () => unit.Revision);

var oldPlan = new StaticPlan()
    .Add(new ArrayLengthProvider(unit))
    .Add(new BoundsBridgeProvider());
if (optimizer.Run(check, Session(unit, oldPlan)).Removed)
    throw new InvalidOperationException("Precondition failed: old modules must keep the bounds check without range knowledge.");

var externalAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(pluginPath);
var forbiddenExternalDependencies = new[]
{
    "Packages.ArraySemantics",
    "Packages.BoundsBridge",
    "Packages.BoundsOptimizer"
};
var externalReferences = externalAssembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
var forbiddenReference = externalReferences.FirstOrDefault(reference =>
    forbiddenExternalDependencies.Contains(reference, StringComparer.Ordinal));
if (forbiddenReference is not null)
    throw new InvalidOperationException($"External provider illegally references old module '{forbiddenReference}'.");

var providerTypes = externalAssembly.GetExportedTypes()
    .Where(type => !type.IsAbstract && typeof(IQueryProviderRegistration).IsAssignableFrom(type))
    .ToArray();
if (providerTypes.Length != 1)
    throw new InvalidOperationException($"Expected exactly one external query provider, found {providerTypes.Length}.");

var externalProvider = (IQueryProviderRegistration?)Activator.CreateInstance(providerTypes[0])
    ?? throw new InvalidOperationException("Failed to instantiate external provider through the public registration contract.");

var extendedPlan = new StaticPlan()
    .Add(new ArrayLengthProvider(unit))
    .Add(new BoundsBridgeProvider())
    .Add(externalProvider);
var optimized = optimizer.Run(check, Session(unit, extendedPlan));
if (!optimized.Removed)
    throw new InvalidOperationException(
        "Typed semantic exchange failed: the old bridge/consumer did not observe knowledge from the external provider.");

Console.WriteLine("PASS module semantic exchange");
Console.WriteLine("host->external compile reference: absent");
Console.WriteLine("external->old producers/bridge/consumer references: absent");
Console.WriteLine("old bounds optimizer behavior: keep -> remove via public typed semantic contracts");
