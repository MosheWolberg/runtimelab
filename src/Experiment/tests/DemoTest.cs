// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Reflection.Emit.Experimental.Tests;
using System.Runtime.InteropServices;
using Xunit;

namespace Experiment.Tests.Demo
{
    public class DemoTesting : IDisposable
    {
        public DemoTesting()
        {
        }

        public void Dispose()
        {
        }

        [Fact]
        public void GenerateForInspection()
        {
            // Construct an assembly name.
            AssemblyName assemblyName = new AssemblyName("MyDynamicAssembly");

            // Construct its types via reflection.
            Type[] types = new Type[] { typeof(MyClass), typeof(MyValueType), typeof(IForInspection) };
            string fileLocation = "C:\\Users\\t-mwolberg\\cmp\\AssemblyWithTypesMethodsFields.dll";
            // Generate DLL from these and save it to Disk.
            AssemblyTools.WriteAssemblyToDisk(assemblyName, types, fileLocation, null, false);
        }

        [Fact]
        public void GenerateForConstruction()
        {
            // Construct an assembly name.
            AssemblyName assemblyName = new AssemblyName("MyDynamicAssembly");

            // Construct its types via reflection.
            Type[] types = new Type[] { typeof(MyClass) };

            string fileLocation = "C:\\Users\\t-mwolberg\\cmp\\BasicAssembly.dll";

            // Generate DLL from these and save it to Disk.
            AssemblyTools.WriteAssemblyToDisk(assemblyName, types, fileLocation, null, true);
        }
    }

    public class MyClass
    {
        public int[] _numbers = new int[5];
        public MyClass()
        {
        }
    }

    public struct MyValueType
    {
        public int[] _numbers;
        public Type MyType()
        {
            return typeof(MyValueType);
        }
    }

    public interface IForInspection
    {
        public BinaryWriter Inspection(int[] numbers);
        public void DestoryTheWorld(string input);
    }
}
