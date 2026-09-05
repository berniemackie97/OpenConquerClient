using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using OpenConquer.Launcher.Diagnostics;

namespace OpenConquer.Launcher.Tests;

public sealed class LauncherExceptionDiagnosticProjectorTests
{
    [Fact]
    public void ProjectPreservesUsefulDiagnosticIdentityAndStack()
    {
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            ThrowDiagnosticException
        );

        LauncherExceptionDiagnostic diagnostic = LauncherExceptionDiagnosticProjector.Project(
            exception
        );

        Assert.Equal(typeof(InvalidOperationException).FullName, diagnostic.ExceptionType);

        Assert.Equal(exception.HResult, diagnostic.HResult);

        Assert.NotNull(diagnostic.StackTrace);
        Assert.False(diagnostic.StackTraceTruncated);
        Assert.False(diagnostic.ExceptionTypeTruncated);

        Assert.Contains(
            nameof(ThrowDiagnosticException),
            diagnostic.StackTrace,
            StringComparison.Ordinal
        );

        Assert.Empty(diagnostic.InnerExceptions);

        Assert.False(diagnostic.InnerExceptionsTruncated);
    }

    [Fact]
    public void ProjectExcludesMessageDataAndSourceFileInformation()
    {
        const string secretMessage = "secret-message-value";
        const string secretData = "secret-data-value";

        Exception exception;

        try
        {
            throw new InvalidOperationException(secretMessage)
            {
                Data = { ["Token"] = secretData },
            };
        }
        catch (Exception capturedException)
        {
            exception = capturedException;
        }

        LauncherExceptionDiagnostic diagnostic = LauncherExceptionDiagnosticProjector.Project(
            exception
        );

        string serializedDiagnostic = JsonSerializer.Serialize(diagnostic);

        Assert.DoesNotContain(secretMessage, serializedDiagnostic, StringComparison.Ordinal);

        Assert.DoesNotContain(secretData, serializedDiagnostic, StringComparison.Ordinal);

        Assert.DoesNotContain(
            nameof(LauncherExceptionDiagnosticProjectorTests) + ".cs",
            serializedDiagnostic,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void ProjectPreservesNestedExceptionStructureWithoutMessages()
    {
        const string outerSecret = "outer-secret";
        const string innerSecret = "inner-secret";

        Exception exception = new InvalidOperationException(
            outerSecret,
            new ArgumentException(innerSecret)
        );

        LauncherExceptionDiagnostic diagnostic = LauncherExceptionDiagnosticProjector.Project(
            exception
        );

        LauncherExceptionDiagnostic innerDiagnostic = Assert.Single(diagnostic.InnerExceptions);

        Assert.Equal(typeof(ArgumentException).FullName, innerDiagnostic.ExceptionType);

        string serializedDiagnostic = JsonSerializer.Serialize(diagnostic);

        Assert.DoesNotContain(outerSecret, serializedDiagnostic, StringComparison.Ordinal);

        Assert.DoesNotContain(innerSecret, serializedDiagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectBoundsAggregateExceptionTraversal()
    {
        Exception[] innerExceptions = Enumerable
            .Range(0, 32)
            .Select(static index => new InvalidOperationException($"message-{index}"))
            .ToArray();

        AggregateException exception = new(innerExceptions);

        LauncherExceptionDiagnostic diagnostic = LauncherExceptionDiagnosticProjector.Project(
            exception
        );

        Assert.Equal(15, diagnostic.InnerExceptions.Count);

        Assert.True(diagnostic.InnerExceptionsTruncated);
    }

    [Fact]
    public void ProjectBoundsDeepStacksAndReportsOmittedFrames()
    {
        Exception exception = Assert.Throws<InvalidOperationException>(() => ThrowRecursively(80));
        LauncherExceptionDiagnostic diagnostic = LauncherExceptionDiagnosticProjector.Project(exception);

        Assert.NotNull(diagnostic.StackTrace);
        Assert.True(diagnostic.StackTraceTruncated);
        Assert.Equal(32, diagnostic.StackTrace.Split('\n').Length);
        Assert.InRange(diagnostic.StackTrace.Length, 1, 4096);
        Assert.Contains(nameof(ThrowRecursively), diagnostic.StackTrace, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(4095)]
    [InlineData(4096)]
    public void ProjectBoundsIndividualMethodNamesWithoutSplittingSurrogatePairs(int prefixLength)
    {
        string methodName = new string('M', prefixLength) + "\U0001F600" + new string('M', 1000);
        DynamicMethod method = new(methodName, typeof(void), Type.EmptyTypes);
        ILGenerator il = method.GetILGenerator();
        il.Emit(OpCodes.Newobj, typeof(InvalidOperationException).GetConstructor(Type.EmptyTypes)!);
        il.Emit(OpCodes.Throw);
        Action throwException = method.CreateDelegate<Action>();
        Exception exception = Assert.Throws<InvalidOperationException>(throwException);

        LauncherExceptionDiagnostic diagnostic = LauncherExceptionDiagnosticProjector.Project(exception);

        Assert.NotNull(diagnostic.StackTrace);
        Assert.Equal(new string('M', prefixLength), diagnostic.StackTrace);
        Assert.True(diagnostic.StackTraceTruncated);
    }

    [Fact]
    public void ProjectBoundsExceptionTypeIdentity()
    {
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("DiagnosticTypeLimit"), AssemblyBuilderAccess.RunAndCollect);
        TypeBuilder type = assembly.DefineDynamicModule("Tests").DefineType(
            new string('E', 900), TypeAttributes.Public, typeof(Exception));
        type.DefineDefaultConstructor(MethodAttributes.Public);
        Exception exception = (Exception)Activator.CreateInstance(type.CreateType()!)!;

        LauncherExceptionDiagnostic diagnostic = LauncherExceptionDiagnosticProjector.Project(exception);

        Assert.Equal(new string('E', 512), diagnostic.ExceptionType);
        Assert.True(diagnostic.ExceptionTypeTruncated);
    }

    [Fact]
    public void ProjectDoesNotReadVirtualExceptionTextOrRemoteStackText()
    {
        Exception exception = new UnreadableException();
        ExceptionDispatchInfo.SetRemoteStackTrace(exception, "password-session-secret");

        LauncherExceptionDiagnostic diagnostic = LauncherExceptionDiagnosticProjector.Project(exception);

        Assert.Null(diagnostic.StackTrace);
        Assert.False(diagnostic.StackTraceTruncated);
        Assert.DoesNotContain("password-session-secret", JsonSerializer.Serialize(diagnostic), StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectBoundsDepthAndMarksOnlyTheTruncatedNode()
    {
        Exception exception = new InvalidOperationException();
        for (int index = 0; index < 20; index++)
        {
            exception = new InvalidOperationException(null, exception);
        }

        LauncherExceptionDiagnostic diagnostic = LauncherExceptionDiagnosticProjector.Project(exception);
        for (int depth = 0; depth < 8; depth++)
        {
            Assert.False(diagnostic.InnerExceptionsTruncated);
            diagnostic = Assert.Single(diagnostic.InnerExceptions);
        }

        Assert.Empty(diagnostic.InnerExceptions);
        Assert.True(diagnostic.InnerExceptionsTruncated);
    }

    [Fact]
    public void ProjectSharesTheExceptionBudgetAcrossAggregateBranches()
    {
        Exception exception = new AggregateException(Enumerable.Range(0, 20).Select(
            static _ => new AggregateException(new InvalidOperationException(), new ArgumentException())));

        LauncherExceptionDiagnostic diagnostic = LauncherExceptionDiagnosticProjector.Project(exception);

        Assert.Equal(16, CountExceptions(diagnostic));
        Assert.True(diagnostic.InnerExceptionsTruncated);
    }

    private static int CountExceptions(LauncherExceptionDiagnostic diagnostic)
    {
        return 1 + diagnostic.InnerExceptions.Sum(CountExceptions);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowRecursively(int remaining)
    {
        if (remaining == 0)
        {
            throw new InvalidOperationException("deep-stack-secret");
        }

        ThrowRecursively(remaining - 1);
        GC.KeepAlive(remaining);
    }

    private sealed class UnreadableException : Exception
    {
        public override string Message => throw new InvalidOperationException("Message must not be read.");
        public override string? StackTrace => throw new InvalidOperationException("StackTrace must not be read.");
        public override string ToString() => throw new InvalidOperationException("ToString must not be called.");
    }

    private static void ThrowDiagnosticException()
    {
        throw new InvalidOperationException("This message must never enter launcher diagnostics.");
    }
}
