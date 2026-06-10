using System.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

public class RoslynAgentTools : IDisposable
{
    private readonly MSBuildWorkspace _workspace;
    private Solution? _currentSolution;
    private readonly ILogger<RoslynAgentTools> _logger;
    private bool _disposed = false;

    public RoslynAgentTools(ILogger<RoslynAgentTools> logger)
    {
        // Must be called once per app domain before creating the workspace
        if (!Microsoft.Build.Locator.MSBuildLocator.IsRegistered)
        {
            Microsoft.Build.Locator.MSBuildLocator.RegisterDefaults();
        }

        _logger = logger;

        _workspace = MSBuildWorkspace.Create();
        _logger.LogInformation("MSBuildWorkspace created successfully.");
    }

    public Solution CurrentSolution
    {
        get =>
            _currentSolution
            ?? throw new InvalidOperationException(
                "No solution or project loaded. Please call LoadSolutionAsync or LoadProjectAsync first."
            );
        private set => _currentSolution = value;
    }

    public IList<AITool> Tools =>
        new List<AITool>
        {
            AIFunctionFactory.Create(FindReferences),
            AIFunctionFactory.Create(ReadMethodCode),
            AIFunctionFactory.Create(ReplaceMethodCode),
            AIFunctionFactory.Create(ListProjects),
        };

    internal async Task LoadSolutionAsync(string solutionPath)
    {
        CurrentSolution = await _workspace.OpenSolutionAsync(solutionPath);
    }

    internal async Task LoadProjectAsync(string projectPath)
    {
        var project = await _workspace.OpenProjectAsync(projectPath);
        CurrentSolution = project.Solution;
    }

    [Description("Lists all projects in the solution.")]
    public Task<string> ListProjects()
    {
        var projectNames = CurrentSolution.Projects.Select(p => p.Name);
        return Task.FromResult(string.Join("\n", projectNames));
    }

    [Description(
        "Finds all references to a specific class, method, or property name across all projects in the solution."
    )]
    public async Task<string> FindReferences(
        [Description("The exact name of the symbol to find")] string symbolName
    )
    {
        ISymbol? targetSymbol = null;

        // 1. Hunt for the symbol's definition across all projects in the solution
        foreach (var project in CurrentSolution.Projects)
        {
            var compilation = await project.GetCompilationAsync();
            if (compilation == null)
                continue;

            var symbols = compilation.GetSymbolsWithName(symbolName);
            if (symbols.Any())
            {
                targetSymbol = symbols.First();
                break; // Found it!
            }
        }

        if (targetSymbol == null)
            return $"Symbol '{symbolName}' not found in any project.";

        // 2. Search the entire solution for references to this symbol
        var references = await Microsoft.CodeAnalysis.FindSymbols.SymbolFinder.FindReferencesAsync(
            targetSymbol,
            CurrentSolution
        );

        var output = new List<string>();
        foreach (var reference in references)
        {
            foreach (var location in reference.Locations)
            {
                var span = location.Location.GetLineSpan();
                output.Add($"File: {span.Path}, Line: {span.StartLinePosition.Line}");
            }
        }

        return output.Count > 0 ? string.Join("\n", output) : "No references found.";
    }

    // Helper to find a file anywhere in the solution
    private Document? FindDocument(string fileName)
    {
        return CurrentSolution
            .Projects.SelectMany(p => p.Documents)
            .FirstOrDefault(d => d.Name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
    }

    [Description("Gets the source code of a specific method inside a specific file.")]
    public async Task<string> ReadMethodCode(
        [Description("The file name, e.g. 'Program.cs'")] string fileName,
        [Description("The name of the method")] string methodName
    )
    {
        var document = FindDocument(fileName);
        if (document == null)
            return $"File '{fileName}' not found in the solution.";

        var syntaxTree = await document.GetSyntaxTreeAsync();
        var root = await syntaxTree!.GetRootAsync();

        var methodNode = root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == methodName);

        return methodNode != null ? methodNode.ToFullString() : "Method not found.";
    }

    [Description("Replaces the entire body of an existing method with new C# code.")]
    public async Task<string> ReplaceMethodCode(
        [Description("The file name, e.g. 'Program.cs'")] string fileName,
        [Description("The name of the method")] string methodName,
        [Description("The new C# code for the method")] string newMethodCode
    )
    {
        var document = FindDocument(fileName);
        if (document == null)
            return "File not found.";

        var syntaxTree = await document.GetSyntaxTreeAsync();
        var root = await syntaxTree!.GetRootAsync();

        var oldMethodNode = root.DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .FirstOrDefault(m => m.Identifier.Text == methodName);

        if (oldMethodNode == null)
            return "Method not found.";

        var newMethodNode = Microsoft
            .CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(newMethodCode)
            .GetRoot()
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .FirstOrDefault();

        if (newMethodNode == null)
            return "The provided code was not a valid C# method.";

        var newRoot = root.ReplaceNode(oldMethodNode, newMethodNode);
        var formattedRoot = Microsoft.CodeAnalysis.Formatting.Formatter.Format(newRoot, _workspace);

        // Save to disk
        File.WriteAllText(document.FilePath!, formattedRoot.ToFullString());

        // CRITICAL: Update the solution state so the agent's next action sees the new code!
        CurrentSolution = document.WithSyntaxRoot(formattedRoot).Project.Solution;
        _workspace.TryApplyChanges(CurrentSolution);

        return "Method updated and saved successfully.";
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // This unloads the solution and releases all memory/file locks
                _workspace?.Dispose();
                _logger.LogInformation(
                    "MSBuildWorkspace successfully disposed and solution unloaded."
                );
            }
            _disposed = true;
        }
    }
}
