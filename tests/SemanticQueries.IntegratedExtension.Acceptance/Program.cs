using System.Reflection;
using System.Runtime.Loader;
using MiniCompiler.IR;
using Packages.ArraySemantics;
using Packages.BoundsBridge;
using Packages.BoundsOptimizer;
using SemanticQueries.Core;

const string ExternalAssemblyName = "ExternalModules.ExactValue";
const string PackageTypeName = "ExternalModules.ExactValue.ExternalExactValuePackage";
const string QueryTypeName = "ExternalModules.ExactValue.ExactValueQuery";

if (args.Length != 1)
    throw new InvalidOperationException("Expected one argument: path to the independently built extension assembly.");

AssertNoReference(Assembly.GetExecutingAssembly(), ExternalAssemblyName, "acceptance host");
AssertNoReference(typeof(BoundsBridgeProvider).Assembly, ExternalAssemblyName, "old bounds bridge");
AssertNoReference(typeof(BoundsCheckOptimizer).Assembly, ExternalAssemblyName, "old bounds optimizer");

var pluginPath = Path.GetFullPath(args[0]);
if (!File.Exists(pluginPath))
    throw new FileNotFoundException("External semantic package was not built.", pluginPath);

var index = new ValueId("i");
var array = new ArrayId("a");
var point = new ProgramPoint("body");
var unit = new CompilationUnit().Array(array, 4);
var check = new BoundsCheck(array, index, point);
var optimizer = new BoundsCheckOptimizer();

var oldPlan = OldPlan(unit);
if (optimizer.Run(check, Session(unit, oldPlan)).Removed)
    throw new InvalidOperationException("Precondition failed: unchanged old modules must keep the check without new knowledge.");

var externalAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(pluginPath);
foreach (var forbidden in new[] { "Packages.ArraySemantics", "Packages.BoundsBridge", "Packages.BoundsOptimizer" })
    AssertNoReference(externalAssembly, forbidden, "external semantic package");

var queryType = externalAssembly.GetType(QueryTypeName, throwOnError: true)!;
if (queryType.Assembly != externalAssembly ||
    !queryType.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IQuerySpec<,>)))
{
    throw new InvalidOperationException("The new ExactValue query contract is not owned by the independent external package.");
}

var packageType = externalAssembly.GetType(PackageTypeName, throwOnError: true)!;
var package = Activator.CreateInstance(packageType)
    ?? throw new InvalidOperationException("Failed to instantiate the external package.");
var providersProperty = packageType.GetProperty("Providers", BindingFlags.Instance | BindingFlags.Public)
    ?? throw new InvalidOperationException("External package does not expose Providers.");
var setMethod = packageType.GetMethod("Set", BindingFlags.Instance | BindingFlags.Public)
    ?? throw new InvalidOperationException("External package does not expose Set.");
var providers = ((IEnumerable<IQueryProviderRegistration>?)providersProperty.GetValue(package))?.ToArray()
    ?? throw new InvalidOperationException("External package did not return public provider registrations.");
if (providers.Length != 2)
    throw new InvalidOperationException($"Expected two external providers, found {providers.Length}.");

setMethod.Invoke(package, ["i", "body", 2]);
var extendedPlan = ExtendedPlan(unit, providers);
var firstSession = Session(unit, extendedPlan);
if (!optimizer.Run(check, firstSession).Removed)
    throw new InvalidOperationException("Unchanged old consumer did not benefit from the new external typed semantic path.");

var permutedPlan = ExtendedPlan(unit, providers.Reverse());
if (!optimizer.Run(check, Session(unit, permutedPlan)).Removed)
    throw new InvalidOperationException("Behavior depends on external provider registration order.");

setMethod.Invoke(package, ["i", "body", 9]);
AssertThrows<StaleSemanticSessionException>(
    () => optimizer.Run(check, firstSession),
    "external package state mutation must invalidate the old session");
if (optimizer.Run(check, Session(unit, extendedPlan)).Removed)
    throw new InvalidOperationException("Fresh session did not observe mutated external analysis state.");

setMethod.Invoke(package, ["i", "body", 1]);
if (!optimizer.Run(check, Session(unit, extendedPlan)).Removed)
    throw new InvalidOperationException("Fresh session did not observe the next external analysis revision.");

Console.WriteLine("PASS integrated external semantic extension");
Console.WriteLine("new query contract owner: external package");
Console.WriteLine("external mutable analysis/revision/provider: package-owned");
Console.WriteLine("bridge: ExactValue -> old RangeQuery");
Console.WriteLine("old bounds bridge/optimizer compile reference to extension: absent");
Console.WriteLine("old consumer behavior: keep -> remove -> keep -> remove across external revisions");
Console.WriteLine("external provider order: permutation invariant");
return;

static StaticPlan OldPlan(CompilationUnit unit) => new StaticPlan()
    .Add(new ArrayLengthProvider(unit))
    .Add(new BoundsBridgeProvider());

static StaticPlan ExtendedPlan(CompilationUnit unit, IEnumerable<IQueryProviderRegistration> externalProviders)
{
    var plan = OldPlan(unit);
    foreach (var provider in externalProviders)
        plan.Add(provider);
    return plan;
}

static SemanticSession Session(CompilationUnit unit, StaticPlan plan) =>
    new(plan, unit.Revision, () => unit.Revision);

static void AssertNoReference(Assembly assembly, string forbiddenName, string owner)
{
    if (assembly.GetReferencedAssemblies().Any(reference =>
            string.Equals(reference.Name, forbiddenName, StringComparison.Ordinal)))
    {
        throw new InvalidOperationException($"{owner} must not reference {forbiddenName}.");
    }
}

static void AssertThrows<TException>(Action action, string label) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"{label}: expected {typeof(TException).Name}");
}
