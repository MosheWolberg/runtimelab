// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.Reflection.Emit.Experimental.Tests
{
    internal class AssemblyTools
    {
        internal static void WriteAssemblyToDisk(AssemblyName assemblyName, Type[] types, string fileLocation)
        {
            WriteAssemblyToDisk(assemblyName, types, fileLocation, null);
        }

        internal static void WriteAssemblyToDisk(AssemblyName assemblyName, Type[] types, string fileLocation, List<CustomAttributeBuilder> customAttributes, bool ignoreMethods = false)
        {
            ConstructorInfo compilationRelax = typeof(CompilationRelaxationsAttribute).GetConstructor(new Type[] { typeof(int) });
            ConstructorInfo runtimeCompat = typeof(RuntimeCompatibilityAttribute).GetConstructor(new Type[] { });
            var runtimeProperty = typeof(RuntimeCompatibilityAttribute).GetProperty("WrapNonExceptionThrows");

            CustomAttributeBuilder customAttribute1 = new CustomAttributeBuilder(compilationRelax, new object[] { 8 });
            CustomAttributeBuilder customAttribute2 = new CustomAttributeBuilder(runtimeCompat, new object[] { }, new PropertyInfo[] { runtimeProperty }, new object[] { true });
            List<CustomAttributeBuilder> customs = new List<CustomAttributeBuilder>();
            customs.Add(customAttribute1);
            customs.Add(customAttribute2);

            AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, System.Reflection.Emit.AssemblyBuilderAccess.Run, customs);

            ModuleBuilder mb = assemblyBuilder.DefineDynamicModule(assemblyName.Name);

            foreach (Type type in types)
            {
                if (type == null)
                {
                    throw new ArgumentException("We have a null type: " + nameof(type));
                }

                Debug.WriteLine("Type: " + type);

                Type contextType = type;
                TypeBuilder tb = mb.DefineType(contextType.FullName, contextType.Attributes, contextType.BaseType);

                if (customAttributes != null)
                {
                    foreach (CustomAttributeBuilder customAttribute in customAttributes)
                    {
                        tb.SetCustomAttribute(customAttribute);
                    }
                }

                foreach (var method in contextType.GetMethods())
                {
                    if (!type.IsInterface && ignoreMethods)
                    {
                        break;
                    }

                    var paramTypes = Array.ConvertAll(method.GetParameters(), item => item.ParameterType);
                    MethodBuilder methodBuilder = tb.DefineMethod(method.Name, method.Attributes, method.CallingConvention, method.ReturnType, paramTypes);

                    int parameterCount = 0;
                    foreach (ParameterInfo parameterInfo in method.GetParameters())
                    {
                        parameterCount++;
                        methodBuilder.DefineParameter(parameterCount, parameterInfo.Attributes, parameterInfo.Name);
                        // Add in parameter default value when we do method bodies.
                    }
                }

                foreach (var constructor in contextType.GetConstructors())
                {
                    Debug.WriteLine("Consturctor Attributes: " + constructor.Attributes);
                    tb.DefineDefaultConstructor(constructor.Attributes);
                }

                foreach (var field in contextType.GetFields(
                    BindingFlags.Instance |
                    BindingFlags.Static |
                    BindingFlags.NonPublic |
                    BindingFlags.Public))
                {
                    tb.DefineField(field.Name, field.FieldType, field.Attributes);
                }
            }

            assemblyBuilder.Save(fileLocation);
        }

        internal static Assembly TryLoadAssembly(string filePath)
        {
            // Get the array of runtime assemblies.
            string[] runtimeAssemblies = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");
            // Create the list of assembly paths consisting of runtime assemblies.
            var paths = new List<string>(runtimeAssemblies);
            paths.Add(filePath);

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
