using SereinFlow.Library;

namespace SereinFlow.Worker.IntegrationTests;

public sealed class LibraryVariableServiceTests
{
    [Fact]
    public void MissingVariablesReturnDefaultsAndFalse()
    {
        var variables = new LibraryVariableService();
        var containResult = variables.Contain("missing");
        Assert.False(containResult);
        var getResult = variables.Get("missing");
        Assert.Null(getResult);
        var getResult1 = variables.Get<string>("missing");
        Assert.Null(getResult1);
        var getResult2 = variables.Get<int>("missing");
        Assert.Equal(0, getResult2);
        var tryGetResult = variables.TryGet<int>("missing", out var value);
        Assert.False(tryGetResult);
        Assert.Equal(0, value);
    }

    [Fact]
    public void ValuesCanBeReplacedAndNamesAreCaseSensitive()
    {
        var variables = new LibraryVariableService();
        variables.Set("count", 42);
        var containResult3 = variables.Contain("count");
        Assert.True(containResult3);
        var containResult4 = variables.Contain("Count");
        Assert.False(containResult4);
        var getResult5 = variables.Get<int>("count");
        Assert.Equal(42, getResult5);
        var getResult6 = variables.Get("count");
        Assert.IsType<int>(getResult6);
        var tryGetResult7 = variables.TryGet<int>("count", out var value);
        Assert.True(tryGetResult7);
        Assert.Equal(42, value);
        variables.Set("count", "updated");
        var getResult8 = variables.Get<string>("count");
        Assert.Equal("updated", getResult8);
    }

    [Fact]
    public void ReferenceValuesRetainTheirIdentityAndCanBeReadThroughInterfaces()
    {
        var variables = new LibraryVariableService();
        var items = new List<string>
        {
            "item"
        };
        variables.Set("items", items);
        var getResult9 = variables.Get("items");
        Assert.Same(items, getResult9);
        var getResult10 = variables.Get<IReadOnlyList<string>>("items");
        Assert.Same(items, getResult10);
        var tryGetResult11 = variables.TryGet<IReadOnlyList<string>>("items", out var value);
        Assert.True(tryGetResult11);
        Assert.Same(items, value);
    }

    [Fact]
    public void NullIsAnExistingValueForReferenceAndNullableTypes()
    {
        var variables = new LibraryVariableService();
        variables.Set<string?>("empty", null);
        var containResult14 = variables.Contain("empty");
        Assert.True(containResult14);
        var getResult15 = variables.Get("empty");
        Assert.Null(getResult15);
        var getResult16 = variables.Get<string>("empty");
        Assert.Null(getResult16);
        var getResult17 = variables.Get<int?>("empty");
        Assert.Null(getResult17);
        var tryGetResult18 = variables.TryGet<string>("empty", out var value);
        Assert.True(tryGetResult18);
        Assert.Null(value);
        Assert.Throws<InvalidCastException>(() =>
        {
            var getResult12 = variables.Get<int>("empty");
            return getResult12;
        });
        Assert.Throws<InvalidCastException>(() =>
        {
            var tryGetResult13 = variables.TryGet<int>("empty", out _);
            return tryGetResult13;
        });
    }

    [Fact]
    public void GenericReadsThrowForIncompatibleTypesWithoutNumericOrTextCoercion()
    {
        var variables = new LibraryVariableService();
        variables.Set("count", 42);
        var getResult23 = variables.Get<int?>("count");
        Assert.Equal(42, getResult23);
        Assert.Throws<InvalidCastException>(() =>
        {
            var getResult19 = variables.Get<long>("count");
            return getResult19;
        });
        Assert.Throws<InvalidCastException>(() =>
        {
            var tryGetResult20 = variables.TryGet<string>("count", out _);
            return tryGetResult20;
        });
        variables.Set("count", "42");
        Assert.Throws<InvalidCastException>(() =>
        {
            var getResult21 = variables.Get<int>("count");
            return getResult21;
        });
        Assert.Throws<InvalidCastException>(() =>
        {
            var tryGetResult22 = variables.TryGet<int>("count", out _);
            return tryGetResult22;
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AllOperationsRejectEmptyNames(string? name)
    {
        var variables = new LibraryVariableService();
        Assert.ThrowsAny<ArgumentException>(() =>
        {
            var containResult24 = variables.Contain(name!);
            return containResult24;
        });
        Assert.ThrowsAny<ArgumentException>(() =>
        {
            var getResult25 = variables.Get(name!);
            return getResult25;
        });
        Assert.ThrowsAny<ArgumentException>(() =>
        {
            var getResult26 = variables.Get<int>(name!);
            return getResult26;
        });
        Assert.ThrowsAny<ArgumentException>(() =>
        {
            var tryGetResult27 = variables.TryGet<int>(name!, out _);
            return tryGetResult27;
        });
        Assert.ThrowsAny<ArgumentException>(() =>
        {
            variables.Set(name!, 1);
        });
    }

    [Fact]
    public async Task ConcurrentReadersAndWritersPreserveEveryVariable()
    {
        var variables = new LibraryVariableService();
        variables.Set("shared", -1);
        var selectResult = Enumerable.Range(0, 8).Select(worker =>
        {
            var runResult = Task.Run(() =>
            {
                for (var index = 0; index < 250; index++)
                {
                    var name = $"{worker}:{index}";
                    variables.Set(name, index);
                    variables.Set("shared", index);
                    var containResult28 = variables.Contain(name);
                    Assert.True(containResult28);
                    var getResult29 = variables.Get<int>(name);
                    Assert.Equal(index, getResult29);
                    var tryGetResult30 = variables.TryGet<int>("shared", out var shared);
                    Assert.True(tryGetResult30);
                    Assert.InRange(shared, -1, 249);
                }
            });
            return runResult;
        });
        await Task.WhenAll(selectResult);
        for (var worker = 0; worker < 8; worker++)
        {
            for (var index = 0; index < 250; index++)
            {
                var getResult31 = variables.Get<int>($"{worker}:{index}");
                Assert.Equal(index, getResult31);
            }
        }
    }
}
