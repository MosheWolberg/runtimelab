// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Xunit;

namespace System.Reflection.Emit.Experimental.Tests.CustomCore
{
    // Currently hard-coding in Custom Attributes using the CustomAttributeBuilder.
    public class CustomAttributeFrameWorkTest : IDisposable
    {
        private readonly string _newCore = Path.Combine(Directory.GetCurrentDirectory(), "netstandard.dll");
        private List<CustomAttributeBuilder> _customAttributes = new List<CustomAttributeBuilder>();
        private string _fileLocation;
        private MetadataLoadContext _context;
        public CustomAttributeFrameWorkTest()
        {
            const bool _keepFiles = true;
            TempFileCollection tfc;
            Directory.CreateDirectory("testDir");
            tfc = new TempFileCollection("testDir", false);
            _fileLocation = tfc.AddExtension("dll", _keepFiles);

            // Create the list of assembly paths consisting of the inspected assembly.
            var paths = new List<string>();
            paths.Add(_newCore);

            var resolver = new PathAssemblyResolver(paths);
            _context = new MetadataLoadContext(resolver, "netstandard");
          }

        // Add three custom attributes to two types. One is pseudo custom attribute.
        // This also tests that Save doesn't have unnecessary duplicate references to same assembly, type etc.
        [Fact]
        public void TwoInterfaceCustomAttribute()
        {
            // Construct an assembly name.
            AssemblyName assemblyName = new AssemblyName("MyDynamicAssembly");
            assemblyName.Version = new Version("7.0");

            // Construct its types via reflection.
            Type[] types = new Type[] { typeof(IMultipleMethod), typeof(INoMethod) };

            // Generate DLL from these and save it to Disk.
            AssemblyToolsWithContext.WriteAssemblyToDisk(assemblyName, types, _fileLocation, _customAttributes, _context);

            // Read said assembly back from Disk using MetadataLoadContext
            Assembly assemblyFromDisk = AssemblyToolsWithContext.TryLoadAssembly(_fileLocation, _newCore);

            // Now compare them:

            // AssemblyName
            Assert.NotNull(assemblyFromDisk);
            Assert.Equal(assemblyName.Name, assemblyFromDisk.GetName().Name);

            // Module Name
            Module moduleFromDisk = assemblyFromDisk.Modules.First();
            Assert.Equal(assemblyName.Name, moduleFromDisk.ScopeName);

            // Type comparisons
            for (int i = 0; i < types.Length; i++)
            {
                Type sourceType = types[i];
                Type typeFromDisk = moduleFromDisk.GetTypes()[i];

                Assert.Equal(sourceType.Name, typeFromDisk.Name);
                Assert.Equal(sourceType.Namespace, typeFromDisk.Namespace);
                Assert.Equal(sourceType.Attributes, typeFromDisk.Attributes);

                // Method comparison
                for (int j = 0; j < sourceType.GetMethods().Length; j++)
                {
                    MethodInfo sourceMethod = sourceType.GetMethods()[j];
                    MethodInfo methodFromDisk = typeFromDisk.GetMethods()[j];

                    Assert.Equal(sourceMethod.Name, methodFromDisk.Name);
                    Assert.Equal(sourceMethod.Attributes, methodFromDisk.Attributes);
                    Type returnType = _context.CoreAssembly.GetType(sourceMethod.ReturnType.FullName);
                    Assert.Equal(returnType.FullName, methodFromDisk.ReturnType.FullName);
                    Assert.Equal(returnType.Assembly.GetName().Name, methodFromDisk.ReturnType.Assembly.GetName().Name);
                    // Parameter comparison
                    for (int k = 0; k < sourceMethod.GetParameters().Length; k++)
                    {
                        ParameterInfo sourceParamter = sourceMethod.GetParameters()[k];
                        ParameterInfo paramterFromDisk = methodFromDisk.GetParameters()[k];
                        Type type = _context.CoreAssembly.GetType(paramterFromDisk.ParameterType.FullName);
                        Assert.Equal(type.FullName, paramterFromDisk.ParameterType.FullName);
                        Assert.Equal(type.Assembly.GetName().Name, paramterFromDisk.ParameterType.Assembly.GetName().Name);
                    }
                }
            }
        }

        public void Dispose()
        {
        }
    }

    public struct INoMethod
    {
        public int I;
        private struct Bye
        {
        }

        public int Getter()
        {
            I = 5;
            return I;
        }
    }

    public interface IMultipleMethod
    {
        string[] Func(int a, string b);
        bool MoreFunc(int[] a, string b, bool c);
        System.IO.BinaryWriter DoIExist();
        void BuildAPerpetualMotionMachine();
    }

    internal interface IAccess
    {
        public TypeAttributes BuildAI(FieldAttributes field);
        public int DisableRogueAI();
    }

    public class IOneMethod
    {
        private static string hello = "hello";

        private struct Bye
        {
        }

        internal static string Func(int a, string b)
        {
            return hello;
        }
    }
}
