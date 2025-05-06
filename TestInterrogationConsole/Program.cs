using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TestInterrogationConsole
{
    public class Program
    {
        public static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.WriteLine("Usage: TestInterrogationConsole <csproj_file_path>");
                return;
            }

            var csprojFilePath = args[0]?.Trim();

            if (!File.Exists(csprojFilePath))
            {
                Console.WriteLine($"Error: Project file not found at '{csprojFilePath}'");
                return;
            }

            Console.WriteLine($"Analyzing project: '{csprojFilePath}'");

            try
            {
                var projectDirectory = Path.GetDirectoryName(csprojFilePath) ?? string.Empty;
                var sourceFiles = GetCSharpSourceFiles(projectDirectory);
                var missingCategoryMethods = new List<string>();

                foreach (var sourceFile in sourceFiles)
                {
                    ProcessSourceFile(sourceFile, missingCategoryMethods);
                }

                if (missingCategoryMethods.Any())
                {
                    Console.WriteLine($"{Environment.NewLine}Methods Missing [TestCategory] Attribute:");
                    foreach (var method in missingCategoryMethods)
                    {
                        Console.WriteLine($"- {method}");
                    }
                }
                else
                {
                    Console.WriteLine($"{Environment.NewLine}All TestMethods have the [TestCategory] attribute.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }

        static List<string> GetCSharpSourceFiles(string projectDirectory)
        {
            var sourceFiles = new List<string>();

            if (Directory.Exists(projectDirectory))
            {
                sourceFiles.AddRange(Directory.GetFiles(projectDirectory, "*.cs", SearchOption.AllDirectories));
            }

            return sourceFiles.Distinct().ToList();
        }

        static void ProcessSourceFile(string sourceFile, List<string> missingCategoryMethods)
        {
            try
            {
                var fileContent = File.ReadAllText(sourceFile);
                var syntaxTree = CSharpSyntaxTree.ParseText(fileContent);
                var root = syntaxTree.GetCompilationUnitRoot();

                var namespaceName = root.DescendantNodes().OfType<NamespaceDeclarationSyntax>().FirstOrDefault()?.Name.ToString() ?? "";

                var testClasses = root.DescendantNodes().OfType<ClassDeclarationSyntax>()
                    .Where(c => c.Modifiers.Any(SyntaxKind.PublicKeyword) &&
                                c.AttributeLists.SelectMany(al => al.Attributes)
                                 .Any(a => a.Name.ToString() == "TestClass"));

                foreach (var classDeclaration in testClasses)
                {
                    var className = classDeclaration.Identifier.ToString();

                    var testMethods = classDeclaration.Members.OfType<MethodDeclarationSyntax>()
                        .Where(m => m.Modifiers.Any(SyntaxKind.PublicKeyword) &&
                                    m.AttributeLists.SelectMany(al => al.Attributes)
                                     .Any(a => a.Name.ToString() == "TestMethod"));

                    foreach (var methodDeclaration in testMethods)
                    {
                        var methodName = methodDeclaration.Identifier.ToString();
                        var hasTestCategory = methodDeclaration.AttributeLists.SelectMany(al => al.Attributes)
                            .Any(a => a.Name.ToString() == "TestCategory");

                        if (!hasTestCategory)
                        {
                            missingCategoryMethods.Add($"{namespaceName}.{className}.{methodName}");
                            Console.WriteLine($"  - Method '{methodName}' in class '{namespaceName}.{className}' is missing [TestCategory] attribute.");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing '{sourceFile}': {ex.Message}");
            }
        }
    }
}