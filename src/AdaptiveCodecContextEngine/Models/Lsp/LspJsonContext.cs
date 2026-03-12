using AdaptiveCodecContextEngine.Models.Lizard;

namespace AdaptiveCodecContextEngine.Models.Lsp;


[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(LspMessage))]
[JsonSerializable(typeof(DocumentSymbolParams))]
[JsonSerializable(typeof(DocumentSymbol[]))]
[JsonSerializable(typeof(SymbolInformation[]))]  
[JsonSerializable(typeof(ReferenceParams))]      
[JsonSerializable(typeof(ReferenceContext))]     
[JsonSerializable(typeof(Location[]))]
[JsonSerializable(typeof(LizardResult))]
[JsonSerializable(typeof(LizardFunction))]
internal partial class LspJsonContext : JsonSerializerContext { }