using AdaptiveCodecContextEngine.Models.Lizard;
using AdaptiveCodecContextEngine.Models.Lsp;
using AdaptiveCodecContextEngine.Models.Rpc;

namespace AdaptiveCodecContextEngine.Models;


[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals)]
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
[JsonSerializable(typeof(QueryRelationsParams))]
[JsonSerializable(typeof(QueryDependenciesParams))]
[JsonSerializable(typeof(QueryPatternsParams))]
[JsonSerializable(typeof(SearchParams))]
[JsonSerializable(typeof(GetHighFrictionParams))]
[JsonSerializable(typeof(GetUnstableParams))]
[JsonSerializable(typeof(JsonRpcRequest))]
[JsonSerializable(typeof(JsonRpcResponse))]
[JsonSerializable(typeof(JsonRpcError))]
[JsonSerializable(typeof(NodeDto))]
[JsonSerializable(typeof(List<NodeDto>))]
[JsonSerializable(typeof(ProjectStatsDto))]
[JsonSerializable(typeof(AvecScores))]
internal partial class ACCJsonContext : JsonSerializerContext { }