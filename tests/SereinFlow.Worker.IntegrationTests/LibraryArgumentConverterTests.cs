using System.Text.Json;
using SereinFlow.Worker.Runner;

namespace SereinFlow.Worker.IntegrationTests;
public sealed class LibraryArgumentConverterTests
{
    [Fact]
    public void ConvertsTextToStringWithoutJsonRoundTripFailure()
    {
        var convertResult = LibraryArgumentConverter.Convert("hello", typeof(string));
        Assert.Equal("hello", convertResult);
    }

    [Fact]
    public void ConvertsCommonScalarValuesToDeclaredTypes()
    {
        var convertResult1 = LibraryArgumentConverter.Convert("42", typeof(int));
        Assert.Equal(42, convertResult1);
        var convertResult2 = LibraryArgumentConverter.Convert("12.5", typeof(decimal));
        Assert.Equal(12.5m, convertResult2);
        var evaluatedValue = (bool)LibraryArgumentConverter.Convert("1", typeof(bool))!;
        Assert.True(evaluatedValue);
        var convertResult3 = LibraryArgumentConverter.Convert("monday", typeof(DayOfWeek));
        Assert.Equal(DayOfWeek.Monday, convertResult3);
        var convertResult4 = LibraryArgumentConverter.Convert("Read, Write", typeof(FileAccess));
        Assert.Equal(FileAccess.ReadWrite, convertResult4);
    }

    [Fact]
    public void ConvertsJsonElementsAndArraysToDeclaredTypes()
    {
        using var document = JsonDocument.Parse("[1, 2, 3]");
        var convertResult5 = LibraryArgumentConverter.Convert(document.RootElement, typeof(int[]));
        var result = Assert.IsType<int[]>(convertResult5);
        Assert.Equal([1, 2, 3], result);
    }

    [Fact]
    public void ReportsInvalidConversionToCaller()
    {
        var exception = Assert.Throws<InvalidCastException>(() =>
        {
            var convertResult6 = LibraryArgumentConverter.Convert("not-a-number", typeof(int));
            return convertResult6;
        });
        Assert.Contains("cannot be converted", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
