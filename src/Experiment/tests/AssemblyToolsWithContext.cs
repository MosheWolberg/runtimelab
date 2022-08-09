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
    internal class AssemblyToolsWithContext
    {
        internal static void WriteAssemblyToDisk(AssemblyName assemblyName, Type[] types, string fileLocation, List<CustomAttributeBuilder> customAttributes, MetadataLoadContext context)
        {
            // Required attributes
            ConstructorInfo compilationRelax = ContextType(typeof(CompilationRelaxationsAttribute)).GetConstructor(new Type[] { ContextType(typeof(int)) });
            ConstructorInfo runtimeCompat = ContextType(typeof(RuntimeCompatibilityAttribute)).GetConstructor(new Type[] { });
            var runtimeProperty = ContextType(typeof(RuntimeCompatibilityAttribute)).GetProperty("WrapNonExceptionThrows");

            CustomAttributeBuilder customAttribute1 = new CustomAttributeBuilder(compilationRelax, new object[] { 8 }, context);
            CustomAttributeBuilder customAttribute2 = new CustomAttributeBuilder(runtimeCompat, new object[] { }, new PropertyInfo[] { runtimeProperty }, new object[] { true }, context);
            List<CustomAttributeBuilder> customs = new List<CustomAttributeBuilder>();
            customs.Add(customAttribute1);
            customs.Add(customAttribute2);

            // End of required attributes
            AssemblyBuilder assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(assemblyName, System.Reflection.Emit.AssemblyBuilderAccess.Run, customs, context);

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
                    MethodBuilder methodBuilder = tb.DefineMethod(method.Name, method.Attributes, method.CallingConvention, ContextType(method.ReturnType), paramTypes);

                    int parameterCount = 0;
                    foreach (ParameterInfo parameterInfo in method.GetParameters())
                    {
                        parameterCount++; // Should this ever be 0?
                        methodBuilder.DefineParameter(parameterCount, parameterInfo.Attributes, parameterInfo.Name);
                        // Add in parameter default value when we do method bodies.
                    }
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
                    Debug.WriteLine($"Unable to locate specified assembly for {nameof(type)} , reverting to default context");
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

        internal static Assembly TryLoadAssembly(string filePath, string coreAssembly)
        {
            // Create the list of assembly paths consisting of runtime assemblies and the inspected assembly.
            var paths = new List<string>();
            paths.Add(coreAssembly);
            // Create PathAssemblyResolver that can resolve assemblies using the created list.
            var resolver = new PathAssemblyResolver(paths);
            var mlc = new MetadataLoadContext(resolver);
            // Load assembly into MetadataLoadContext.
            Assembly assembly = mlc.LoadFromAssemblyPath(filePath);
            return assembly;
        }
    }
}
