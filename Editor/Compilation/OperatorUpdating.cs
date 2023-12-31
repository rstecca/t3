﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using T3.Core.Logging;
using T3.Core.Model;
using T3.Core.Operator;
using T3.Core.Resource;
using T3.Editor.UiModel;

namespace T3.Editor.Compilation
{
    /// <summary>
    /// And editor functionality that handles the c# compilation of symbol classes.
    /// </summary>
    public static class OperatorUpdating
    {
        /// <summary>
        /// An event called that is called if the file hook detects change to the symbol source code.
        /// </summary>
        public static void ResourceUpdateHandler(OperatorResource resource, string path)
        {
            //Log.Info($"Operator source '{path}' changed.");
            //Log.Info($"Actual thread Id {Thread.CurrentThread.ManagedThreadId}");

            string source;
            try
            {
                source = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                Log.Error($"Error opening file '{path}");
                Log.Error(e.Message);
                return;
            }

            if (string.IsNullOrWhiteSpace(source))
            {
                Log.Info("Source was empty, skip compilation.");
                return;
            }

            var newAssembly = CompileSymbolFromSource(source, path, resource.ParentAssembly.Assembly);
            if (newAssembly == null)
                return;

            resource.OperatorAssembly = newAssembly;
            resource.Updated = true;
        }

        public static bool TryCreateSymbolFromSource(string sourceCode, string newSymbolName, Guid newSymbolId, string @namespace, Assembly parentAssembly,
                                                     out Symbol newSymbol)
        {
            var newAssembly = CompileSymbolFromSource(sourceCode, newSymbolName, parentAssembly);
            if (newAssembly == null)
            {
                Log.Error("Error compiling duplicated type, aborting duplication.");
                newSymbol = null;
                return false;
            }

            var type = newAssembly.ExportedTypes.FirstOrDefault();
            if (type == null)
            {
                Log.Error("Error, new symbol has no compiled instance type");
                newSymbol = null;
                return false;
            }

            newSymbol = new Symbol(type, newSymbolId);
            newSymbol.PendingSource = sourceCode;
            newSymbol.Namespace = @namespace;
            SymbolRegistry.Entries.Add(newSymbol.Id, newSymbol);
            var symbolData = UiSymbolData.SymbolDataByAssembly[parentAssembly];
            symbolData.AddSymbol(newSymbol);
            return true;
        }

        internal static Assembly CompileSymbolFromSource(string source, string symbolName, Assembly parentAssembly)
        {
            IEnumerable<MetadataReference> referencedAssemblies = Array.Empty<MetadataReference>();
            AddAllReferences(parentAssembly, ref referencedAssemblies, true);

            // Todo - I think this can be optimized by reusing the compilation object?
            var syntaxTree = CSharpSyntaxTree.ParseText(source);
            var compilation = CSharpCompilation.Create(symbolName,
                                                       new[] { syntaxTree },
                                                       referencedAssemblies,
                                                       RuntimeCompilationOptions);

            using var dllStream = new MemoryStream();
            using var pdbStream = new MemoryStream();

            var emitResult = compilation.Emit(dllStream, pdbStream);
            //Log.Info($"Compilation results of '{symbolName}':");
            if (!emitResult.Success)
            {
                foreach (var entry in emitResult.Diagnostics)
                {
                    if (entry.WarningLevel == 0)
                        Log.Error(entry.GetMessage());
                    else
                        Log.Warning(entry.GetMessage());
                }

                return null;
            }

            try
            {
                var newAssembly = Assembly.Load(dllStream.GetBuffer());
                if (newAssembly.ExportedTypes.Any())
                {
                    Log.Info($"Compilation of '{symbolName}' successful.");
                    return newAssembly;
                }
                else
                {
                    Log.Error("New compiled assembly had no exported type.");
                    return null;
                }
            }
            catch (Exception e)
            {
                Log.Error("Failed to load compiled type: " + e.Message);
                return null;
            }
        }

        public static bool UpdateSymbolWithNewSource(Symbol symbol, string newSource)
        {
            var newAssembly = CompileSymbolFromSource(newSource, symbol.Name, symbol.ParentAssembly);
            if (newAssembly == null)
                return false;

            //string path = @"Operators\Types\" + symbol.Name + ".cs";
            var sourcePath = symbol.SymbolData.BuildFilepathForSymbol(symbol, SymbolData.SourceExtension);

            var operatorResource = ResourceManager.Instance().GetOperatorFileResource(sourcePath);
            if (operatorResource != null)
            {
                operatorResource.OperatorAssembly = newAssembly;
                operatorResource.Updated = true;
                symbol.PendingSource = newSource;
                return true;
            }

            Log.Error($"Could not update symbol '{symbol.Name}' because its file resource couldn't be found.");

            return false;
        }

        internal static void AddAllReferences(Assembly from, ref IEnumerable<MetadataReference> to, bool includeThisAssembly)
        {
            var references = CollectReferencedAssemblies(from);
            if (includeThisAssembly)
            {
                var referenceToThisAssembly = MetadataReference.CreateFromFile(from.Location);
                references = references.Append(referenceToThisAssembly);
            }

            references = references.DistinctBy(x => x.Display);
            to = to.Concat(references)
                   .Where(x => x.Display != null)
                   .DistinctBy(x => x.Display);
            
            return;

            static IEnumerable<MetadataReference> CollectReferencedAssemblies(Assembly parentAssembly)
            {
                IEnumerable<MetadataReference> referencedAssemblies = Array.Empty<MetadataReference>();
                foreach (var asmName in parentAssembly.GetReferencedAssemblies())
                {
                    var asm = Assembly.Load(asmName);
                    Log.Debug($"  Loaded SUB from {asm} {asm.Location}");

                    referencedAssemblies = referencedAssemblies.Concat(CollectReferencedAssemblies(asm))
                                                               .Append(MetadataReference.CreateFromFile(asm.Location));
                }

                return referencedAssemblies;
            }
        }

        private static readonly CSharpCompilationOptions RuntimeCompilationOptions = new(OutputKind.DynamicallyLinkedLibrary);
    }
}