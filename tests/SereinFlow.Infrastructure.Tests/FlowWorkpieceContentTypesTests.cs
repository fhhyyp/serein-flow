using SereinFlow.Library;

namespace SereinFlow.Infrastructure.Tests;

public sealed class FlowWorkpieceContentTypesTests
{
    [Fact]
    public void ExposesCommonWorkpieceMimeTypes()
    {
        Assert.Equal("image/png", FlowWorkpieceContentTypes.Png);
        Assert.Equal("application/json", FlowWorkpieceContentTypes.Json);
    }
}
