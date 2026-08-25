using System.Text.Json;
using SereinFlow.Worker.Runner;

namespace SereinFlow.Worker.IntegrationTests;

public sealed class LibraryArgumentConverterTests
{
    [Fact]
    public void ConvertsTextToStringWithoutJsonRoundTripFailure()
        => Assert.Equal("hello", LibraryArgumentConverter.Convert("hello", typeof(string)));

    [Fact]
    public void ConvertsCommonScalarValuesToDeclaredTypes()
    {
        Assert.Equal(42, LibraryArgumentConverter.Convert("42", typeof(int)));
        Assert.Equal(12.5m, LibraryArgumentConverter.Convert("12.5", typeof(decimal)));
        Assert.True((bool)LibraryArgumentConverter.Convert("1", typeof(bool))!);
        Assert.Equal(DayOfWeek.Monday, LibraryArgumentConverter.Convert("monday", typeof(DayOfWeek)));
    }

    [Fact]
    public void ConvertsJsonElementsAndArraysToDeclaredTypes()
    {
        using var document = JsonDocument.Parse("[1, 2, 3]");
        var result = Assert.IsType<int[]>(LibraryArgumentConverter.Convert(document.RootElement, typeof(int[])));
        Assert.Equal([1, 2, 3], result);
    }

    [Fact]
    public void ReportsInvalidConversionToCaller()
    {
        var exception = Assert.Throws<InvalidCastException>(() => LibraryArgumentConverter.Convert("not-a-number", typeof(int)));
        Assert.Contains("cannot be converted", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
