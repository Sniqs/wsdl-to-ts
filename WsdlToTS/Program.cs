using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text;

class Program
{
    static async Task Main()
    {
        var filePath = "Reference.cs";

        var csharpCode = await File.ReadAllTextAsync(filePath);

        var tree = CSharpSyntaxTree.ParseText(csharpCode);
        var root = tree.GetRoot();

        var tsCode = new StringBuilder();
        var classNames = new HashSet<string>();

        var classes = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var cls in classes)
        {
            ProcessClass(tsCode, classNames, cls);
        }

        Console.WriteLine("\nGenerated TypeScript Code:\n");
        Console.WriteLine(tsCode.ToString());
        await File.WriteAllTextAsync(filePath.Replace(".cs", ".ts"), tsCode.ToString());
    }

    private static void ProcessClass(StringBuilder tsCode, HashSet<string> classNames, ClassDeclarationSyntax cls)
    {
        string className = cls.Identifier.Text;

        bool isPoco = IsPocoClass(cls);

        if (isPoco)
        {
            Console.WriteLine($"Processing POCO: {className}");
            ProcessPocoClass(cls, tsCode, classNames);
        }
        else
        {
            Console.WriteLine($"Processing non-POCO class: {className}");
            ProcessNonPocoClass(cls, tsCode, classNames);
        }
    }

    private static void ProcessNonPocoClass(ClassDeclarationSyntax cls, StringBuilder tsCode, HashSet<string> classNames)
    {
        string className = cls.Identifier.Text;
        tsCode.AppendLine($"export class {className} {{");

        var methods = cls.Members.OfType<MethodDeclarationSyntax>();

        foreach (var method in methods)
        {
            string methodName = method.Identifier.Text;
            var returnType = method.ReturnType;
            var returnTypeString = GetTypeString(returnType);
            string tsReturnType = CSharpToTypeScript(returnTypeString, classNames);

            var parameters = method.ParameterList.Parameters
                .Select(param => $"{param.Identifier.Text}: {CSharpToTypeScript(GetTypeString(param.Type), classNames)}")
                .ToList();

            string paramList = string.Join(", ", parameters);

            tsCode.AppendLine($"  {methodName}({paramList}): {tsReturnType} {{}}");
        }
        tsCode.AppendLine("}\n");
    }

    static bool IsPocoClass(ClassDeclarationSyntax cls)
    {
        return !cls.Members.OfType<MethodDeclarationSyntax>().Any(); // No methods = POCO
    }

    static void ProcessPocoClass(ClassDeclarationSyntax cls, StringBuilder tsCode, HashSet<string> classNames)
    {
        string className = cls.Identifier.Text;
        tsCode.AppendLine($"export type {className} {{");

        var properties = cls.Members.OfType<PropertyDeclarationSyntax>();

        if (properties.Any())
        {
            ProcessProperties(tsCode, classNames, properties);
        }
        else
        {
            ProcessFields(cls, tsCode, classNames);
        }

        tsCode.AppendLine("}\n");
    }

    private static void ProcessProperties(StringBuilder tsCode, HashSet<string> classNames, IEnumerable<PropertyDeclarationSyntax> properties)
    {
        foreach (var prop in properties)
        {
            if (prop.Identifier.Text == "Any") continue;
            var propertyType = prop.Type;

            var propertyTypeString = GetTypeString(propertyType);

            string tsType = CSharpToTypeScript(propertyTypeString, classNames);

            tsCode.AppendLine($"  {prop.Identifier.Text}: {tsType};");
        }
    }

    private static void ProcessFields(ClassDeclarationSyntax cls, StringBuilder tsCode, HashSet<string> classNames)
    {
        var fields = cls.Members.OfType<FieldDeclarationSyntax>();

        foreach (var field in fields)
        {
            var fieldType = field.Declaration.Type;

            string fieldTypeString = GetTypeString(fieldType);

            string tsType = CSharpToTypeScript(fieldTypeString, classNames);
            var fieldNames = field.Declaration.Variables
                .Select(v => v.Identifier.Text)
                .ToList();
            foreach (var fieldName in fieldNames)
            {
                if (fieldName == "anyField") continue;
                var type = tsType == "string" ? "WithNamespace" : $"WithNamespace<{tsType}>";

                tsCode.AppendLine($"  {fieldName}: {type};");
            }
        }
    }

    private static string GetTypeString(TypeSyntax fullType)
    {
        if (fullType is ArrayTypeSyntax arrayType)
        {
            if (arrayType.ElementType is QualifiedNameSyntax qName)
            {
                var elementType = GetTypeFromQualifiedName(qName);
                return $"{elementType}[]";
            }
        }

        if (fullType is QualifiedNameSyntax qualifiedName)
        {
            return GetTypeFromQualifiedName(qualifiedName);
        }
        else
        {
            return fullType.ToString();
        }
    }

    private static string GetTypeFromQualifiedName(QualifiedNameSyntax? qualifiedName)
    {
        if (qualifiedName == null) return "foobar";

        var right = qualifiedName.Right;
        if (right is GenericNameSyntax generic)
        {
            return (generic.TypeArgumentList.Arguments[0] as QualifiedNameSyntax)?.Right.Identifier.Text.ToString() ?? "foobar";
        }
        else
        {
            return qualifiedName.Right.Identifier.Text;
        }
    }

    static string CSharpToTypeScript(string csharpType, HashSet<string> classNames)
    {
        return csharpType switch
        {
            "string" => "string",
            "int" => "number",
            "double" => "number",
            "float" => "number",
            "bool" => "boolean",
            "DateTime" => "Date",
            _ => csharpType
        };
    }
}