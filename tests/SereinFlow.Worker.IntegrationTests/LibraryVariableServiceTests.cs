using SereinFlow.Library;

namespace SereinFlow.Worker.IntegrationTests;

public sealed class LibraryVariableServiceTests
{
    [Fact]
    public void MissingVariablesReturnDefaultsAndFalse()
    {
        var variables = new LibraryVariableService();

        Assert.False(variables.Contain("missing"));
        Assert.Null(variables.Get("missing"));
        Assert.Null(variables.Get<string>("missing"));
        Assert.Equal(0, variables.Get<int>("missing"));
        Assert.False(variables.TryGet<int>("missing", out var value));
        Assert.Equal(0, value);
    }

    [Fact]
    public void ValuesCanBeReplacedAndNamesAreCaseSensitive()
    {
        var variables = new LibraryVariableService();
        variables.Set("count", 42);

        Assert.True(variables.Contain("count"));
        Assert.False(variables.Contain("Count"));
        Assert.Equal(42, variables.Get<int>("count"));
        Assert.IsType<int>(variables.Get("count"));
        Assert.True(variables.TryGet<int>("count", out var value));
        Assert.Equal(42, value);

        variables.Set("count", "updated");
        Assert.Equal("updated", variables.Get<string>("count"));
    }

    [Fact]
    public void ReferenceValuesRetainTheirIdentityAndCanBeReadThroughInterfaces()
    {
        var variables = new LibraryVariableService();
        var items = new List<string> { "item" };
        variables.Set("items", items);

        Assert.Same(items, variables.Get("items"));
        Assert.Same(items, variables.Get<IReadOnlyList<string>>("items"));
        Assert.True(variables.TryGet<IReadOnlyList<string>>("items", out var value));
        Assert.Same(items, value);
    }

    [Fact]
    public void NullIsAnExistingValueForReferenceAndNullableTypes()
    {
        var variables = new LibraryVariableService();
        variables.Set<string?>("empty", null);

        Assert.True(variables.Contain("empty"));
        Assert.Null(variables.Get("empty"));
        Assert.Null(variables.Get<string>("empty"));
        Assert.Null(variables.Get<int?>("empty"));
        Assert.True(variables.TryGet<string>("empty", out var value));
        Assert.Null(value);
        Assert.Throws<InvalidCastException>(() => variables.Get<int>("empty"));
        Assert.Throws<InvalidCastException>(() => variables.TryGet<int>("empty", out _));
    }

    [Fact]
    public void GenericReadsThrowForIncompatibleTypesWithoutNumericOrTextCoercion()
    {
        var variables = new LibraryVariableService();
        variables.Set("count", 42);

        Assert.Equal(42, variables.Get<int?>("count"));
        Assert.Throws<InvalidCastException>(() => variables.Get<long>("count"));
        Assert.Throws<InvalidCastException>(() => variables.TryGet<string>("count", out _));

        variables.Set("count", "42");
        Assert.Throws<InvalidCastException>(() => variables.Get<int>("count"));
        Assert.Throws<InvalidCastException>(() => variables.TryGet<int>("count", out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AllOperationsRejectEmptyNames(string? name)
    {
        var variables = new LibraryVariableService();

        Assert.ThrowsAny<ArgumentException>(() => variables.Contain(name!));
        Assert.ThrowsAny<ArgumentException>(() => variables.Get(name!));
        Assert.ThrowsAny<ArgumentException>(() => variables.Get<int>(name!));
        Assert.ThrowsAny<ArgumentException>(() => variables.TryGet<int>(name!, out _));
        Assert.ThrowsAny<ArgumentException>(() => variables.Set(name!, 1));
    }

    [Fact]
    public async Task ConcurrentReadersAndWritersPreserveEveryVariable()
    {
        var variables = new LibraryVariableService();
        variables.Set("shared", -1);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
        {
            for (var index = 0; index < 250; index++)
            {
                var name = $"{worker}:{index}";
                variables.Set(name, index);
                variables.Set("shared", index);
                Assert.True(variables.Contain(name));
                Assert.Equal(index, variables.Get<int>(name));
                Assert.True(variables.TryGet<int>("shared", out var shared));
                Assert.InRange(shared, -1, 249);
            }
        })));

        for (var worker = 0; worker < 8; worker++)
            for (var index = 0; index < 250; index++)
                Assert.Equal(index, variables.Get<int>($"{worker}:{index}"));
    }
}
