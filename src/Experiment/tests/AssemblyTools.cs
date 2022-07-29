 // Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;

namespace System.Reflection.Emit.Experimental.Tests
{
    internal class AssemblyTools
    {
        internal static void WriteAssemblyToDisk(AssemblyName assemblyName, Type[] types, string fileLocation)
        {
            WriteAssemblyToDisk(assemblyName, types, fileLocation, null, null);
        }

        internal static void WriteAssemblyToDisk(AssemblyName assemblyName, Type[] types, string fileLocation, List<CustomAttributeBuilder> customAttributes, MetadataLoadContext context)
        {
            AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, System.Reflection.Emit.AssemblyBuilderAccess.Run, context);

            ModuleBuilder mb = assemblyBuilder.DefineDynamicModule(assemblyName.Name);

            foreach (Type type in types)
            {
                if (type == null)
                {
                    throw new ArgumentException("We have a null type: " + nameof(type));
                }

                Debug.WriteLine("Type: " + type);

                Type contextType = ContextType(type);
                TypeBuilder tb = mb.DefineType(contextType.FullName, contextType.Attributes, ContextType(contextType.BaseType));

                if (customAttributes != null)
                {
                    foreach (CustomAttributeBuilder customAttribute in customAttributes)
                    {
                        tb.SetCustomAttribute(customAttribute);
                    }
                }

                foreach (var method in contextType.GetMethods())
                {
                    var paramTypes = Array.ConvertAll(method.GetParameters(), item => ContextType(item.ParameterType));
                    tb.DefineMethod(method.Name, method.Attributes, method.CallingConvention, ContextType(method.ReturnType), paramTypes);
                }

                foreach (var field in contextType.GetFields(
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.NonPublic |
                    BindingFlags.Public))
                {
                    tb.DefineField(field.Name, ContextType(field.FieldType), field.Attributes);
                }
            }

            assemblyBuilder.Save(fileLocation);

            Type ContextType(Type type)
            {
                if (type == null)
                {
                    return null;
                }

                Assembly contextAssembly = context.CoreAssembly;

                if (contextAssembly == null)
                {
                    Debug.WriteLine($"Unable to locate specified context for {nameof(type)} , reverting to default context");
                    return type;
                }

                Type contextType = contextAssembly.GetType((type.FullName == null) ? type.Name : type.FullName);

                if (contextType == null)
                {
                    Debug.WriteLine($"Unable to locate specified context for {nameof(type)} , reverting to default context");
                    return type;
                }

                return contextType;
            }
        }

        internal static Assembly TryLoadAssembly(string filePath, string coreAssembly = null)
        {
            // Get the array of runtime assemblies.
            string[] runtimeAssemblies = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");
            // Create the list of assembly paths consisting of runtime assemblies and the inspected assembly.
            var paths = new List<string>(runtimeAssemblies);
            paths.Add(filePath);

            if (coreAssembly != null)
            {
                paths.Add(coreAssembly);
            }

            // Create PathAssemblyResolver that can resolve assemblies using the created list.
            var resolver = new PathAssemblyResolver(paths);
            var mlc = new MetadataLoadContext(resolver);
            // Load assembly into MetadataLoadContext.
            Assembly assembly = mlc.LoadFromAssemblyPath(filePath);
            return assembly;
        }

        internal static void MetadataReader(string filename)
        {
            Debug.WriteLine("Using MetadataReader class");

            using var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var peReader = new PEReader(fs);

            MetadataReader mr = peReader.GetMetadataReader();

            Debug.WriteLine("Number of types is " + mr.TypeDefinitions.Count);
            foreach (TypeDefinitionHandle tdefh in mr.TypeDefinitions)
            {
                TypeDefinition tdef = mr.GetTypeDefinition(tdefh);
                string ns = mr.GetString(tdef.Namespace);
                string name = mr.GetString(tdef.Name);
                Debug.WriteLine($"Name of type is {ns}.{name}");
            }

            Debug.WriteLine("Number of methods is " + mr.MethodDefinitions.Count);
            foreach (MethodDefinitionHandle mdefh in mr.MethodDefinitions)
            {
                MethodDefinition mdef = mr.GetMethodDefinition(mdefh);
                string mname = mr.GetString(mdef.Name);
                var owner = mr.GetTypeDefinition(mdef.GetDeclaringType());
                Debug.WriteLine($"Method name: {mname} is owned by {mr.GetString(owner.Name)}.");
            }

            Debug.WriteLine("Ended MetadataReader class");
        }
    }
}
