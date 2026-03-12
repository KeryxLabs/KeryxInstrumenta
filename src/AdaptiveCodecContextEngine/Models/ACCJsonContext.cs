using AdaptiveCodecContextEngine.Models.Lizard;
using AdaptiveCodecContextEngine.Models.Lsp;

namespace AdaptiveCodecContextEngine.Models;


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
[JsonSerializable(typeof(CallHierarchyItem[]))]
[JsonSerializable(typeof(CallHierarchyItem))]
[JsonSerializable(typeof(LspRequest))]
[JsonSerializable(typeof(LspRequestParams))]
[JsonSerializable(typeof(TextDocument))]
internal partial class ACCJsonContext : JsonSerializerContext { }