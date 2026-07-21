using AgentUp.Architecture.Tests.Fixtures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace AgentUp.Architecture.Tests.Rules;

[TestFixture]
public sealed class ReviewHygiene
{
    private static readonly string[] DisposableTypeNames =
    [
        "HttpClient",
        "HttpResponseMessage",
        "StringWriter",
        "TcpListener",
        "ClassicDesktopStyleApplicationLifetime",
        "FileStream",
        "StreamReader",
        "StreamWriter",
        "Process"
    ];

    [Test]
    public void Source_does_not_use_generic_catch_clauses()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ArchitectureFixture.AllSourceFiles(root)
            .SelectMany(path => FindCatchClauses(root, path)
                .Where(item => IsGenericCatch(item.CatchClause) && !HasNarrowingExceptionFilter(item.CatchClause))
                .Select(item => $"{item.Location}: generic catch clause"))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Catch only specific exception types, or use an exception filter that explicitly narrows System.Exception to known concrete exception types.");
    }

    [Test]
    public void Source_does_not_contain_empty_catch_conditional_or_loop_bodies()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ArchitectureFixture.AllSourceFiles(root)
            .SelectMany(path => EmptyBlocks(root, path))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Empty catch blocks, conditionals, and loop bodies hide failed cleanup or untested behavior; handle, log, or remove the block.");
    }

    [Test]
    public void Source_does_not_call_path_combine()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ArchitectureFixture.AllSourceFiles(root)
            .SelectMany(path => Invocations(root, path)
                .Where(item => IsMemberInvocation(item.Invocation, "Path", "Combine"))
                .Select(item => $"{item.Location}: Path.Combine(...)"))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Use Path.Join or an owning path-validation provider instead of Path.Combine, whose later absolute arguments can discard earlier path segments.");
    }

    [Test]
    public void Source_does_not_create_local_disposables_without_using_or_ownership_transfer()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ArchitectureFixture.AllSourceFiles(root)
            .SelectMany(path => ObjectCreations(root, path)
                .Where(item => DisposableTypeNames.Contains(ArchitectureFixture.FinalTypeSegment(item.Creation.Type), StringComparer.Ordinal))
                .Where(item => !IsUsingOwned(item.Creation) && !IsOwnershipTransfer(item.Creation))
                .Select(item => $"{item.Location}: new {item.Creation.Type}(...) is not disposed with using or an approved ownership transfer"))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Local IDisposable ownership must be explicit: use using/await using or move creation into an approved ownership-transfer helper.");
    }

    [Test]
    public void Source_does_not_manually_dispose_locals_in_finally_when_using_is_available()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ArchitectureFixture.AllSourceFiles(root)
            .SelectMany(path => TryStatements(root, path)
                .Where(item => item.TryStatement.Finally is not null)
                .Where(item => item.TryStatement.Finally!.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(IsDisposeInvocation))
                .Select(item => $"{item.Location}: manual Dispose() in finally"))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Prefer using/await using over try/finally disposal for local IDisposable instances.");
    }

    [Test]
    public void Production_source_does_not_block_on_async_work()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ArchitectureFixture.ProductionSourceFiles(root)
            .SelectMany(path => SyncOverAsyncUsages(root, path))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Production startup, UI, and composition code must not block on async work; await asynchronously and surface failures as recoverable state.");
    }

    [Test]
    public void Timeout_cancellation_filters_check_the_timeout_source()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ArchitectureFixture.AllSourceFiles(root)
            .SelectMany(path => FindCatchClauses(root, path)
                .Where(item => IsOperationCanceledCatch(item.CatchClause))
                .Where(item => item.CatchClause.Filter is not null)
                .Where(item => item.CatchClause.Filter!.FilterExpression.ToString().Contains("!cancellationToken.IsCancellationRequested", StringComparison.Ordinal))
                .Where(item => !item.CatchClause.Filter!.FilterExpression.ToString().Contains(".IsCancellationRequested", StringComparison.Ordinal)
                               || item.CatchClause.Filter!.FilterExpression.ToString().Contains("!cancellationToken.IsCancellationRequested", StringComparison.Ordinal)
                                  && item.CatchClause.Filter!.FilterExpression.ToString().Split(".IsCancellationRequested").Length <= 2)
                .Select(item => $"{item.Location}: OperationCanceledException filter does not check the timeout token source"))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Timeout filters must check the timeout CancellationTokenSource and separately preserve caller cancellation.");
    }

    [Test]
    public void Source_does_not_contain_redundant_to_string_calls()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ArchitectureFixture.AllSourceFiles(root)
            .SelectMany(path => Invocations(root, path)
                .Where(item => IsToStringInvocation(item.Invocation))
                .Where(item => IsRedundantToStringContext(item.Invocation))
                .Select(item => $"{item.Location}: redundant ToString()"))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Do not call ToString explicitly when string interpolation, concatenation, or string-returning XML/document expressions already perform conversion.");
    }

    [Test]
    public void Source_uses_linq_for_simple_foreach_filter_projection_patterns()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ArchitectureFixture.AllSourceFiles(root)
            .SelectMany(path => ForeachFilterProjectionPatterns(root, path))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Use LINQ Where/Select for simple filtering or projection loops instead of hand-rolled foreach patterns.");
    }

    [Test]
    public void Tests_do_not_skip_coverage_based_on_live_platform_privilege_or_system_state()
    {
        var root = ArchitectureFixture.FindRepositoryRoot(TestContext.CurrentContext.TestDirectory);
        var violations = ArchitectureFixture.TestSourceFiles(root)
            .SelectMany(path => Invocations(root, path)
                .Where(item => IsAssumeThatInvocation(item.Invocation))
                .Where(item =>
                {
                    var text = item.Invocation.ToString();
                    return text.Contains("OperatingSystem.", StringComparison.Ordinal)
                           || text.Contains("Environment.IsPrivilegedProcess", StringComparison.Ordinal)
                           || text.Contains("/Library", StringComparison.Ordinal);
                })
                .Select(item => $"{item.Location}: platform/privilege/system-state Assume.That"))
            .ToArray();

        Assert.That(violations, Is.Empty,
            "Tests must use seams/fakes to cover platform, privilege, and filesystem branches deterministically instead of skipping them on CI.");
    }

    private static IEnumerable<(CatchClauseSyntax CatchClause, string Location)> FindCatchClauses(string root, string path)
    {
        var (tree, rootNode) = ArchitectureFixture.ParseSourceFile(path);
        return rootNode.DescendantNodes()
            .OfType<CatchClauseSyntax>()
            .Select(catchClause => (catchClause, ArchitectureFixture.Location(root, path, tree, catchClause)));
    }

    private static IEnumerable<(InvocationExpressionSyntax Invocation, string Location)> Invocations(string root, string path)
    {
        var (tree, rootNode) = ArchitectureFixture.ParseSourceFile(path);
        return rootNode.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Select(invocation => (invocation, ArchitectureFixture.Location(root, path, tree, invocation)));
    }

    private static IEnumerable<(ObjectCreationExpressionSyntax Creation, string Location)> ObjectCreations(string root, string path)
    {
        var (tree, rootNode) = ArchitectureFixture.ParseSourceFile(path);
        return rootNode.DescendantNodes()
            .OfType<ObjectCreationExpressionSyntax>()
            .Select(creation => (creation, ArchitectureFixture.Location(root, path, tree, creation)));
    }

    private static IEnumerable<(TryStatementSyntax TryStatement, string Location)> TryStatements(string root, string path)
    {
        var (tree, rootNode) = ArchitectureFixture.ParseSourceFile(path);
        return rootNode.DescendantNodes()
            .OfType<TryStatementSyntax>()
            .Select(tryStatement => (tryStatement, ArchitectureFixture.Location(root, path, tree, tryStatement)));
    }

    private static bool IsGenericCatch(CatchClauseSyntax catchClause)
    {
        if (catchClause.Declaration is null)
            return true;

        var typeName = catchClause.Declaration.Type.ToString();
        return typeName is "Exception" or "System.Exception";
    }

    private static bool HasNarrowingExceptionFilter(CatchClauseSyntax catchClause)
    {
        if (catchClause.Declaration?.Type.ToString() is not ("Exception" or "System.Exception"))
            return false;

        var filterText = catchClause.Filter?.FilterExpression.ToString();
        return filterText is not null
               && filterText.Contains(" is ", StringComparison.Ordinal)
               && !filterText.Contains(" is Exception", StringComparison.Ordinal)
               && !filterText.Contains(" is System.Exception", StringComparison.Ordinal);
    }

    private static IEnumerable<string> EmptyBlocks(string root, string path)
    {
        var (tree, rootNode) = ArchitectureFixture.ParseSourceFile(path);

        foreach (var catchClause in rootNode.DescendantNodes().OfType<CatchClauseSyntax>().Where(c => IsEmptyBlock(c.Block)))
            yield return $"{ArchitectureFixture.Location(root, path, tree, catchClause)}: empty catch block";

        foreach (var ifStatement in rootNode.DescendantNodes().OfType<IfStatementSyntax>().Where(i => IsEmptyStatement(i.Statement)))
            yield return $"{ArchitectureFixture.Location(root, path, tree, ifStatement)}: empty if body";

        foreach (var elseClause in rootNode.DescendantNodes().OfType<ElseClauseSyntax>().Where(e => IsEmptyStatement(e.Statement)))
            yield return $"{ArchitectureFixture.Location(root, path, tree, elseClause)}: empty else body";

        foreach (var loop in rootNode.DescendantNodes().Where(IsLoopWithEmptyBody))
            yield return $"{ArchitectureFixture.Location(root, path, tree, loop)}: empty loop body";
    }

    private static bool IsEmptyStatement(StatementSyntax statement)
        => statement is EmptyStatementSyntax || statement is BlockSyntax block && IsEmptyBlock(block);

    private static bool IsEmptyBlock(BlockSyntax block)
        => block.Statements.Count == 0;

    private static bool IsLoopWithEmptyBody(SyntaxNode node)
        => node switch
        {
            ForEachStatementSyntax forEach => IsEmptyStatement(forEach.Statement),
            ForEachVariableStatementSyntax forEach => IsEmptyStatement(forEach.Statement),
            ForStatementSyntax forStatement => IsEmptyStatement(forStatement.Statement),
            WhileStatementSyntax whileStatement => IsEmptyStatement(whileStatement.Statement),
            DoStatementSyntax doStatement => IsEmptyStatement(doStatement.Statement),
            _ => false
        };

    private static bool IsMemberInvocation(InvocationExpressionSyntax invocation, string receiver, string method)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || memberAccess.Name.Identifier.Text != method)
        {
            return false;
        }

        var expression = memberAccess.Expression.ToString();
        return expression == receiver || expression.EndsWith($".{receiver}", StringComparison.Ordinal);
    }

    private static bool IsUsingOwned(ObjectCreationExpressionSyntax creation)
        => creation.Ancestors().OfType<UsingStatementSyntax>().Any()
           || creation.Ancestors().OfType<LocalDeclarationStatementSyntax>().Any(local => local.UsingKeyword.IsKind(SyntaxKind.UsingKeyword))
           || creation.Ancestors().OfType<LocalDeclarationStatementSyntax>().Any(local => local.AwaitKeyword.IsKind(SyntaxKind.AwaitKeyword)
                                                                               && local.UsingKeyword.IsKind(SyntaxKind.UsingKeyword));

    private static bool IsOwnershipTransfer(ObjectCreationExpressionSyntax creation)
    {
        if (creation.Ancestors().OfType<ReturnStatementSyntax>().Any()
            || creation.Ancestors().OfType<ArrowExpressionClauseSyntax>().Any()
            || IsAssignedToField(creation))
        {
            return true;
        }

        var argument = creation.Ancestors().OfType<ArgumentSyntax>().FirstOrDefault();
        if (argument?.Parent?.Parent is ObjectCreationExpressionSyntax outerCreation)
        {
            var outerType = ArchitectureFixture.FinalTypeSegment(outerCreation.Type);
            return IsOwnershipType(outerType);
        }

        var local = creation.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault();
        if (local?.Parent?.Parent is not LocalDeclarationStatementSyntax localDeclaration)
            return false;

        var variableName = local.Identifier.Text;
        var method = localDeclaration.Ancestors().OfType<BaseMethodDeclarationSyntax>().FirstOrDefault();
        if (method is null)
            return false;

        return method.DescendantNodes()
            .OfType<ArgumentSyntax>()
            .Any(arg => arg.Expression is IdentifierNameSyntax identifier
                        && identifier.Identifier.Text == variableName
                        && ArgumentIsPassedToOwner(arg))
               || method.DescendantNodes()
                   .OfType<AssignmentExpressionSyntax>()
                   .Any(assignment => assignment.Right is IdentifierNameSyntax identifier
                                      && identifier.Identifier.Text == variableName
                                      && IsFieldOrPropertyExpression(assignment.Left))
               || method.DescendantNodes()
                   .OfType<ReturnStatementSyntax>()
                   .Any(returnStatement => returnStatement.Expression is IdentifierNameSyntax identifier
                                           && identifier.Identifier.Text == variableName
                                           || ReturnPassesVariableToTaskResult(returnStatement, variableName));
    }

    private static bool IsAssignedToField(ObjectCreationExpressionSyntax creation)
        => creation.Ancestors().OfType<AssignmentExpressionSyntax>().FirstOrDefault() is { } assignment
           && assignment.Right.DescendantNodesAndSelf().Contains(creation)
           && IsFieldOrPropertyExpression(assignment.Left);

    private static bool ArgumentIsPassedToOwner(ArgumentSyntax argument)
    {
        var call = argument.Parent?.Parent;
        return call switch
        {
            ObjectCreationExpressionSyntax objectCreation => IsOwnershipType(ArchitectureFixture.FinalTypeSegment(objectCreation.Type)),
            InvocationExpressionSyntax invocation => invocation.Expression.ToString().Contains("SetupWithLifetime", StringComparison.Ordinal),
            _ => false
        };
    }

    private static bool ReturnPassesVariableToTaskResult(ReturnStatementSyntax returnStatement, string variableName)
        => returnStatement.Expression is InvocationExpressionSyntax
           {
               Expression: MemberAccessExpressionSyntax
               {
                   Expression: IdentifierNameSyntax { Identifier.Text: "Task" },
                   Name.Identifier.Text: "FromResult"
               },
               ArgumentList.Arguments: [{ Expression: IdentifierNameSyntax identifier }]
           }
           && identifier.Identifier.Text == variableName;

    private static bool IsOwnershipType(string typeName)
        => typeName.EndsWith("Client", StringComparison.Ordinal)
           || typeName.EndsWith("Service", StringComparison.Ordinal)
           || typeName.EndsWith("Provider", StringComparison.Ordinal)
           || typeName.EndsWith("Validator", StringComparison.Ordinal)
           || typeName.EndsWith("ViewModel", StringComparison.Ordinal)
           || typeName.EndsWith("Checks", StringComparison.Ordinal)
           || typeName.EndsWith("Smoke", StringComparison.Ordinal);

    private static bool IsFieldOrPropertyExpression(ExpressionSyntax expression)
        => expression is IdentifierNameSyntax identifier && identifier.Identifier.Text.StartsWith("_", StringComparison.Ordinal)
           || expression is MemberAccessExpressionSyntax;

    private static bool IsDisposeInvocation(InvocationExpressionSyntax invocation)
        => invocation.Expression is MemberAccessExpressionSyntax memberAccess
           && memberAccess.Name.Identifier.Text == "Dispose";

    private static IEnumerable<string> SyncOverAsyncUsages(string root, string path)
    {
        var (tree, rootNode) = ArchitectureFixture.ParseSourceFile(path);

        foreach (var invocation in rootNode.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "GetResult" } getResult
                && getResult.Expression is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "GetAwaiter" } })
            {
                yield return $"{ArchitectureFixture.Location(root, path, tree, invocation)}: GetAwaiter().GetResult()";
            }

            if (invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "Wait" })
                yield return $"{ArchitectureFixture.Location(root, path, tree, invocation)}: Wait()";
        }

        foreach (var memberAccess in rootNode.DescendantNodes().OfType<MemberAccessExpressionSyntax>()
                     .Where(member => member.Name.Identifier.Text == "Result"))
        {
            yield return $"{ArchitectureFixture.Location(root, path, tree, memberAccess)}: .Result";
        }
    }

    private static bool IsOperationCanceledCatch(CatchClauseSyntax catchClause)
    {
        var type = catchClause.Declaration?.Type.ToString();
        return type is "OperationCanceledException" or "System.OperationCanceledException";
    }

    private static bool IsToStringInvocation(InvocationExpressionSyntax invocation)
        => invocation.Expression is MemberAccessExpressionSyntax { Name.Identifier.Text: "ToString" }
           && invocation.ArgumentList.Arguments.Count == 0;

    private static bool IsRedundantToStringContext(InvocationExpressionSyntax invocation)
        => invocation.Ancestors().Any(ancestor =>
            ancestor is InterpolationSyntax
            || ancestor is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.AddExpression }
            || ancestor is ArrowExpressionClauseSyntax);

    private static IEnumerable<string> ForeachFilterProjectionPatterns(string root, string path)
    {
        var (tree, rootNode) = ArchitectureFixture.ParseSourceFile(path);
        foreach (var forEach in rootNode.DescendantNodes().OfType<ForEachStatementSyntax>())
        {
            var statements = forEach.Statement is BlockSyntax block ? block.Statements : [forEach.Statement];
            if (statements.Count == 0)
                continue;

            if (statements[0] is IfStatementSyntax { Else: null } firstIf
                && (firstIf.Statement is ContinueStatementSyntax || IsSingleContinueBlock(firstIf.Statement)))
            {
                yield return $"{ArchitectureFixture.Location(root, path, tree, forEach)}: foreach filter should use Where";
                continue;
            }

            if (statements.Count == 1 && statements[0] is IfStatementSyntax { Else: null })
            {
                yield return $"{ArchitectureFixture.Location(root, path, tree, forEach)}: foreach conditional body should use Where";
                continue;
            }

            if (statements.Count == 1 && IsListProjectionReturnCandidate(forEach, statements[0]))
                yield return $"{ArchitectureFixture.Location(root, path, tree, forEach)}: foreach projection should use Select";
        }
    }

    private static bool IsSingleContinueBlock(StatementSyntax statement)
        => statement is BlockSyntax { Statements.Count: 1 } block
           && block.Statements[0] is ContinueStatementSyntax;

    private static bool IsListProjectionReturnCandidate(ForEachStatementSyntax forEach, StatementSyntax statement)
    {
        if (statement is not ExpressionStatementSyntax
        {
            Expression: InvocationExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax
                {
                    Expression: IdentifierNameSyntax target,
                    Name.Identifier.Text: "Add"
                }
            }
        })
        {
            return false;
        }

        var method = forEach.Ancestors().OfType<BaseMethodDeclarationSyntax>().FirstOrDefault();
        return method?.DescendantNodes()
            .OfType<ReturnStatementSyntax>()
            .Any(returnStatement => returnStatement.Expression is IdentifierNameSyntax identifier
                                    && identifier.Identifier.Text == target.Identifier.Text) == true;
    }

    private static bool IsAssumeThatInvocation(InvocationExpressionSyntax invocation)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || memberAccess.Name.Identifier.Text != "That")
        {
            return false;
        }

        var receiver = memberAccess.Expression.ToString();
        return receiver == "Assume";
    }
}
