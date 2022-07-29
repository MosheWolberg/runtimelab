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
        private readonly string _newCore = Directory.GetCurrentDirectory() + "\\NovelCoreAssembly.dll";
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

            // Get the array of runtime assemblies.
            string[] runtimeAssemblies = Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll");

            // Create the list of assembly paths consisting of runtime assemblies and the inspected assembly.
            var paths = new List<string>(runtimeAssemblies);
            paths.Add(_newCore);

            var resolver = new PathAssemblyResolver(paths);
            _context = new MetadataLoadContext(resolver, "NovelCoreAssembly");
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
            AssemblyTools.WriteAssemblyToDisk(assemblyName, types, _fileLocation, _customAttributes, _context);

            // Read said assembly back from Disk using MetadataLoadContext
            Assembly assemblyFromDisk = AssemblyTools.TryLoadAssembly(_fileLocation, _newCore);

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
                Assert.Equal(sourceType.Assembly.FullName, typeFromDisk.Assembly.FullName);
                Assert.Equal(sourceType.Attributes | TypeAttributes.Import, typeFromDisk.Attributes); // Pseudo-custom attributes are added to core TypeAttributes.

                // Ordering of custom attributes is not preserved in metadata so we sort before comparing.
                List<CustomAttributeData> attributesFromDisk = typeFromDisk.GetCustomAttributesData().ToList();
                attributesFromDisk.Sort((x, y) => x.AttributeType.ToString().CompareTo(y.AttributeType.ToString()));
                _customAttributes.Sort((x, y) => x.Constructor.DeclaringType.ToString().CompareTo(y.Constructor.DeclaringType.ToString()));

                for (int j = 0; j < _customAttributes.Count; j++)
                {
                    CustomAttributeBuilder sourceAttribute = _customAttributes[j];
                    CustomAttributeData attributeFromDisk = attributesFromDisk[j];
                    Assert.Equal(sourceAttribute.Constructor.DeclaringType.ToString(), attributeFromDisk.AttributeType.ToString());
                    Assert.Equal(sourceAttribute.Constructor.DeclaringType.Name, attributeFromDisk.AttributeType.Name);
                    Assert.Equal(sourceAttribute.Constructor.DeclaringType.Namespace, attributeFromDisk.AttributeType.Namespace);
                    Assert.Equal(sourceAttribute.Constructor.DeclaringType.Assembly.FullName, attributeFromDisk.AttributeType.Assembly.FullName);
                }

                // Method comparison
                for (int j = 0; j < sourceType.GetMethods().Length; j++)
                {
                    MethodInfo sourceMethod = sourceType.GetMethods()[j];
                    MethodInfo methodFromDisk = typeFromDisk.GetMethods()[j];

                    Assert.Equal(sourceMethod.Name, methodFromDisk.Name);
                    Assert.Equal(sourceMethod.Attributes, methodFromDisk.Attributes);

                    Assert.Equal(sourceMethod.ReturnType.FullName, methodFromDisk.ReturnType.FullName);
                    Assert.Equal(sourceMethod.ReturnType.Assembly.FullName, methodFromDisk.ReturnType.Assembly.FullName);
                    // Parameter comparison
                    for (int k = 0; k < sourceMethod.GetParameters().Length; k++)
                    {
                        ParameterInfo sourceParamter = sourceMethod.GetParameters()[k];
                        ParameterInfo paramterFromDisk = methodFromDisk.GetParameters()[k];
                        Assert.Equal(sourceParamter.ParameterType.FullName, paramterFromDisk.ParameterType.FullName);
                        Assert.Equal(sourceParamter.ParameterType.Assembly.FullName, paramterFromDisk.ParameterType.Assembly.FullName);
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
