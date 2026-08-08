using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Doject;

[Generator]
public class JobLookupInjectorGenerator : ISourceGenerator
{
    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(() => new JobSyntaxReceiver());
    }

    public void Execute(GeneratorExecutionContext context)
    {
        if (!(context.SyntaxContextReceiver is JobSyntaxReceiver receiver)) return;

        foreach (var jobSyntax in receiver.CandidateJobs)
        {
            var model = context.Compilation.GetSemanticModel(jobSyntax.SyntaxTree);
            if (!(model.GetDeclaredSymbol(jobSyntax) is INamedTypeSymbol jobSymbol)) continue;

            bool hasInjectAttr = jobSymbol.GetAttributes().Any(attr =>
            {
                var name = attr.AttributeClass?.Name;
                return name == "AutoInject" || name == "AutoInjectAttribute" ||
                       name == "AutoInjectLookups" || name == "AutoInjectLookupsAttribute";
            });

            if (!hasInjectAttr) continue;

            string fullJobName = jobSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
            string jobName = jobSymbol.Name;

            string safeFileName = jobSymbol.ToDisplayString()
                .Replace("global::", "")
                .Replace("<", "_")
                .Replace(">", "_")
                .Replace(".", "_");

            var cacheFieldsSb = new StringBuilder();
            var initCacheSb = new StringBuilder();
            var updateCacheSb = new StringBuilder();
            var assignmentsSb = new StringBuilder();

            foreach (var member in jobSyntax.Members.OfType<FieldDeclarationSyntax>())
            {
                foreach (var variable in member.Declaration.Variables)
                {
                    if (!(model.GetDeclaredSymbol(variable) is IFieldSymbol fieldSymbol)) continue;

                    if (!(fieldSymbol.Type is INamedTypeSymbol fieldType)) continue;

                    string typeName = fieldType.Name;
                    bool isLookup = typeName.Contains("ComponentLookup") || typeName.Contains("BufferLookup");
                    if (!isLookup) continue;

                    bool hasIgnoreAttr = fieldSymbol.GetAttributes().Any(attr =>
                    {
                        var name = attr.AttributeClass?.Name;
                        return name == "IgnoreInject" || name == "IgnoreInjectAttribute";
                    });

                    if (hasIgnoreAttr) continue;

                    bool isReadOnly = fieldSymbol.GetAttributes().Any(attr =>
                    {
                        var name = attr.AttributeClass?.Name;
                        return name == "ReadOnly" || name == "ReadOnlyAttribute";
                    });

                    string lookupMethod = typeName.Contains("ComponentLookup") ? "GetComponentLookup" : "GetBufferLookup";
                    var genericType = fieldType.TypeArguments.FirstOrDefault();
                    if (genericType == null) continue;

                    string compTypeName = genericType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    string fullFieldTypeName = fieldType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    string isRoStr = isReadOnly ? "true" : "false";
                    string fieldName = fieldSymbol.Name;

                    cacheFieldsSb.AppendLine($"            public {fullFieldTypeName} {fieldName};");
                    initCacheSb.AppendLine($"                {fieldName} = state.{lookupMethod}<{compTypeName}>({isRoStr});");
                    updateCacheSb.AppendLine($"                {fieldName}.Update(ref state);");
                    assignmentsSb.AppendLine($"            this.{fieldName} = cache.{fieldName};");
                }
            }

            if (cacheFieldsSb.Length == 0) continue;

            var outerTypesStack = new Stack<INamedTypeSymbol>();
            var parentSymbol = jobSymbol.ContainingType;
            while (parentSymbol != null)
            {
                outerTypesStack.Push(parentSymbol);
                parentSymbol = parentSymbol.ContainingType;
            }

            var typeDeclarationBuilder = new StringBuilder();
            var typeCloseBuilder = new StringBuilder();
            string indent = "";

            bool hasNamespace = !jobSymbol.ContainingNamespace.IsGlobalNamespace;
            string ns = hasNamespace ? jobSymbol.ContainingNamespace.ToDisplayString() : "";

            if (hasNamespace)
            {
                typeDeclarationBuilder.AppendLine($"namespace {ns}\n{{");
                typeCloseBuilder.Insert(0, "}\n");
                indent += "    ";
            }

            while (outerTypesStack.Count > 0)
            {
                var outer = outerTypesStack.Pop();
                string kind = outer.IsRecord ? "record" : (outer.IsValueType ? "struct" : "class");
                typeDeclarationBuilder.AppendLine($"{indent}public partial {kind} {outer.Name}\n{indent}{{");
                typeCloseBuilder.Insert(0, $"{indent}}}\n");
                indent += "    ";
            }

            var extClassBuilder = new StringBuilder();
            var extClassCloseBuilder = new StringBuilder();
            string extIndent = "";

            if (hasNamespace)
            {
                extClassBuilder.AppendLine($"namespace {ns}\n{{");
                extClassCloseBuilder.AppendLine("}");
                extIndent = "    ";
            }

            var source = $@"// <auto-generated />
using Unity.Entities;

{typeDeclarationBuilder}
{indent}public partial struct {jobName}
{indent}{{
{indent}    public struct Cache
{indent}    {{
{cacheFieldsSb.ToString().TrimEnd()}

{indent}        public void Init(ref SystemState state)
{indent}        {{
{initCacheSb.ToString().TrimEnd()}
{indent}        }}

{indent}        public void Update(ref SystemState state)
{indent}        {{
{updateCacheSb.ToString().TrimEnd()}
{indent}        }}
{indent}    }}

{indent}    public {jobName}(ref Cache cache)
{indent}    {{
{indent}        this = default;
{assignmentsSb.ToString().TrimEnd()}
{indent}    }}
{indent}}}
{typeCloseBuilder}

{extClassBuilder}{extIndent}public static class {safeFileName}_InjectExtensions
{extIndent}{{
{extIndent}    public static void Inject(this ref {fullJobName} job, ref {fullJobName}.Cache cache)
{extIndent}    {{
{assignmentsSb.ToString().Replace("this.", "job.").TrimEnd()}
{extIndent}    }}
{extIndent}}}
{extClassCloseBuilder}";

            context.AddSource($"{safeFileName}_Inject.g.cs", SourceText.From(source, Encoding.UTF8));
        }
    }

    private class JobSyntaxReceiver : ISyntaxContextReceiver
    {
        public List<StructDeclarationSyntax> CandidateJobs { get; } = new();

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            if (context.Node is StructDeclarationSyntax structDecl && structDecl.AttributeLists.Count > 0)
            {
                CandidateJobs.Add(structDecl);
            }
        }
    }
}