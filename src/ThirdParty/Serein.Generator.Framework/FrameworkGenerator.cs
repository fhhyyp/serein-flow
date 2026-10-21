using Microsoft.CodeAnalysis;

namespace Serein.Generator.Framework;

/// <summary>A non-generic entry point that keeps domain model types out of the generator's public base type.</summary>
public abstract class FrameworkGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var builder = new GeneratorBuilder(context);
        Configure(builder);
    }

    protected abstract void Configure(GeneratorBuilder builder);
}
