// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using static System.Reflection.Emit.Experimental.EntityWrappers;

namespace System.Reflection.Emit.Experimental
{
    public class ModuleBuilder : System.Reflection.Module
    {
        internal MetadataBuilder Metadata;
        internal Assembly? _contextAssembly;
        internal BlobBuilder _ilBuilder;
        internal List<AssemblyReferenceWrapper> _assemblyRefStore = new List<AssemblyReferenceWrapper>();

        internal List<TypeReferenceWrapper> _typeRefStore = new List<TypeReferenceWrapper>();

        internal List<MethodReferenceWrapper> _methodRefStore = new List<MethodReferenceWrapper>();

        internal List<TypeBuilder> _typeDefStore = new List<TypeBuilder>();
        public override System.Reflection.Assembly Assembly { get; }
        public override string ScopeName
        {
            get;
        }

        internal ModuleBuilder(string name, AssemblyBuilder assembly, BlobBuilder ilBuilder, Assembly? loadContext, MetadataBuilder metadata)
        {
            ScopeName = name;
            Assembly = assembly;
            _contextAssembly = loadContext;
            Metadata = metadata;
            _ilBuilder = ilBuilder;

            // Create type definition for the special <Module> type that holds global functions
            metadata.AddTypeDefinition(
                default(TypeAttributes),
                default(StringHandle),
                metadata.GetOrAddString("<Module>"),
                baseType: default(EntityHandle),
                fieldList: MetadataTokens.FieldDefinitionHandle(1),
                methodList: MetadataTokens.MethodDefinitionHandle(1));
        }

        // Wherever possible metadata construction is done in module.
        internal void AppendMetadata()
        {
            // Get System.Object() constructor
            MethodBase? ctor = typeof(object).GetConstructor(Type.EmptyTypes);
            if (ctor == null)
            {
                throw new Exception("Unable to locate Sytem.Object constructor");
            }

            EntityHandle ctorHandle = AddorGetMethodReference(ctor);

            // Set up IL Blobs
            var methodBodyStream = new MethodBodyStreamEncoder(_ilBuilder);
            var codeBuilder = new BlobBuilder();

            int fieldTempCounter = 1;
            int methodTempCounter = 1;
            int paramTempCounter = 1;
            // Add each type definition to metadata table.
            foreach (TypeBuilder typeBuilder in _typeDefStore)
            {
                TypeDefinitionHandle typeDefintionHandle = MetadataHelper.AddTypeDef(typeBuilder, Metadata, methodTempCounter, fieldTempCounter, typeBuilder._baseToken);
                InstructionEncoder il;
                // Add each constructor definition to metadata table.
                foreach (ConstructorBuilder constructor in typeBuilder._constructorDefStore)
                {
                    // Emit IL for Program::.ctor
                    il = new InstructionEncoder(codeBuilder);

                    // ldarg.0
                    il.LoadArgument(0);

                    // call instance void [mscorlib]System.Object::.ctor()
                    il.Call(ctorHandle);

                    // ret
                    il.OpCode(ILOpCode.Ret);

                    int ctorBodyOffset = methodBodyStream.AddMethodBody(il);
                    codeBuilder.Clear();
                    MetadataHelper.AddConstructorDefintion(Metadata, constructor, this, ctorBodyOffset);
                    methodTempCounter++;
                }

                // Add each method definition to metadata table.
                foreach (MethodBuilder method in typeBuilder._methodDefStore)
                {
                    EntityHandle methodHandle = MetadataHelper.AddMethodDefintion(Metadata, method, this, paramTempCounter);

                    foreach (ParameterBuilder param in method.Parameters)
                    {
                        MetadataHelper.AddParamDefintion(Metadata, param, this);
                        paramTempCounter++;
                    }

                    methodTempCounter++;
                }

                // Add each field definition to metadata table.
                foreach (FieldBuilder field in typeBuilder._fieldDefStore)
                {
                    MetadataHelper.AddFieldDefinition(Metadata, field);
                    fieldTempCounter++;
                }

                // Add each custom attribute to metadata table.
                foreach (CustomAttributeWrapper customAttribute in typeBuilder._customAttributes)
                {
                    MetadataHelper.AddCustomAttr(Metadata, customAttribute, typeDefintionHandle);
                }
            }

            // Add references last because in creating type and member definitions, more references can be added to metadata.

            // Add each assembly reference to metadata table.
            foreach (var assemblyRef in _assemblyRefStore)
            {
                MetadataHelper.AddAssemblyReference(assemblyRef.Assembly, Metadata);
            }

            // Add each type reference to metadata table.
            foreach (var typeReference in _typeRefStore)
            {
                MetadataHelper.AddTypeReference(Metadata, typeReference.Type, typeReference.ParentToken);
            }

            // Add each method reference to metadata table.
            foreach (var methodRef in _methodRefStore)
            {
                MetadataHelper.AddMethodReference(Metadata, methodRef.ParentToken, methodRef.Method, this);
            }

        }

        public System.Reflection.Emit.Experimental.TypeBuilder DefineType(string name, System.Reflection.TypeAttributes attr)
        {
            return DefineType(name, attr, null);
        }

        internal EntityHandle AddorGetMethodReference(MethodBase method)
        {
            // Check if MethodBuilder
            var methodBuilder = method as MethodBuilder;
            if (methodBuilder != null)
            {
                throw new ArgumentException("MethodBuilder should not be used as reference " + nameof(method));
            }

            MethodReferenceWrapper methodReferenceWrapper = new MethodReferenceWrapper(method);

            if ((method.DeclaringType == null))
            {
                throw new ArgumentException("Could not find parent type of method " + nameof(method));
            }

            methodReferenceWrapper.ParentToken = AddorGetTypeReference(method.DeclaringType);

            if (_methodRefStore.Contains(methodReferenceWrapper))
            {
                return MetadataTokens.MemberReferenceHandle(_methodRefStore.IndexOf(methodReferenceWrapper) + 1);
            }
            else
            {
                _methodRefStore.Add(methodReferenceWrapper);
                return MetadataTokens.MemberReferenceHandle(_methodRefStore.Count);
            }
        }

        internal EntityHandle AddorGetTypeReference(Type type)
        {
            // Check if Type Builder
            var typeBuilder = type as TypeBuilder;
            if (typeBuilder != null)
            {
                int token = _typeDefStore.IndexOf(typeBuilder);
                if (token == -1)
                {
                    throw new ArgumentException("This TypeBuilder was created in another module");
                }

                return MetadataTokens.TypeDefinitionHandle(token + 1);
            }

            TypeReferenceWrapper typeReferenceWrapper = new TypeReferenceWrapper(type);
            typeReferenceWrapper.ParentToken = AddorGetAssemblyReference(type.Assembly.GetName());

            if (_typeRefStore.Contains(typeReferenceWrapper))
            {
                return MetadataTokens.TypeReferenceHandle(_typeRefStore.IndexOf(typeReferenceWrapper) + 1);
            }
            else
            {
                _typeRefStore.Add(typeReferenceWrapper);
                return MetadataTokens.TypeReferenceHandle(_typeRefStore.Count);
            }
        }

        internal EntityHandle AddorGetAssemblyReference(AssemblyName assembly)
        {
            AssemblyReferenceWrapper assemblyReference = new AssemblyReferenceWrapper(assembly);
            if (_assemblyRefStore.Contains(assemblyReference))
            {
                return MetadataTokens.AssemblyReferenceHandle(_assemblyRefStore.IndexOf(assemblyReference) + 1);
            }
            else
            {
                _assemblyRefStore.Add(assemblyReference);
                return MetadataTokens.AssemblyReferenceHandle(_assemblyRefStore.Count);
            }
        }

        public void CreateGlobalFunctions()
           => throw new NotImplementedException();

        // For all these methods, return type will be changed to "System.Reflection.Emit.Experiment.x" once non-empty modules/assemblies are supported.
        public System.Reflection.Emit.EnumBuilder DefineEnum(string name, System.Reflection.TypeAttributes visibility, System.Type underlyingType)
            => throw new NotImplementedException();

        public System.Reflection.Emit.MethodBuilder DefineGlobalMethod(string name, System.Reflection.MethodAttributes attributes, System.Reflection.CallingConventions callingConvention, System.Type? returnType, System.Type[]? parameterTypes)
            => throw new NotImplementedException();

        public System.Reflection.Emit.MethodBuilder DefineGlobalMethod(string name, System.Reflection.MethodAttributes attributes, System.Reflection.CallingConventions callingConvention, System.Type? returnType, System.Type[]? requiredReturnTypeCustomModifiers, System.Type[]? optionalReturnTypeCustomModifiers, System.Type[]? parameterTypes, System.Type[][]? requiredParameterTypeCustomModifiers, System.Type[][]? optionalParameterTypeCustomModifiers)
            => throw new NotImplementedException();

        public System.Reflection.Emit.MethodBuilder DefineGlobalMethod(string name, System.Reflection.MethodAttributes attributes, System.Type? returnType, System.Type[]? parameterTypes)
           => throw new NotImplementedException();

        public System.Reflection.Emit.FieldBuilder DefineInitializedData(string name, byte[] data, System.Reflection.FieldAttributes attributes)
            => throw new NotImplementedException();

        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute("P/Invoke marshaling may dynamically access members that could be trimmed.")]
        public System.Reflection.Emit.MethodBuilder DefinePInvokeMethod(string name, string dllName, System.Reflection.MethodAttributes attributes, System.Reflection.CallingConventions callingConvention, System.Type? returnType, System.Type[]? parameterTypes, System.Runtime.InteropServices.CallingConvention nativeCallConv, System.Runtime.InteropServices.CharSet nativeCharSet)
            => throw new NotImplementedException();

        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute("P/Invoke marshaling may dynamically access members that could be trimmed.")]
        public System.Reflection.Emit.MethodBuilder DefinePInvokeMethod(string name, string dllName, string entryName, System.Reflection.MethodAttributes attributes, System.Reflection.CallingConventions callingConvention, System.Type? returnType, System.Type[]? parameterTypes, System.Runtime.InteropServices.CallingConvention nativeCallConv, System.Runtime.InteropServices.CharSet nativeCharSet)
            => throw new NotImplementedException();

        public System.Reflection.Emit.Experimental.TypeBuilder DefineType(string name)
        {
            return DefineType(name, default, null);
        }

        public System.Reflection.Emit.Experimental.TypeBuilder DefineType(string name, System.Reflection.TypeAttributes attr, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] System.Type? parent)
        {
            TypeBuilder type = new TypeBuilder(name, this, Assembly, attr, MetadataTokens.TypeDefinitionHandle(_typeDefStore.Count + 1), parent);
            _typeDefStore.Add(type);
            return type;
        }

        public System.Reflection.Emit.TypeBuilder DefineType(string name, System.Reflection.TypeAttributes attr, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] System.Type? parent, int typesize)
            => throw new NotImplementedException();

        public System.Reflection.Emit.TypeBuilder DefineType(string name, System.Reflection.TypeAttributes attr, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] System.Type? parent, System.Reflection.Emit.PackingSize packsize)
            => throw new NotImplementedException();

        public System.Reflection.Emit.TypeBuilder DefineType(string name, System.Reflection.TypeAttributes attr, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] System.Type? parent, System.Reflection.Emit.PackingSize packingSize, int typesize)
           => throw new NotImplementedException();

        public System.Reflection.Emit.Experimental.TypeBuilder DefineType(string name, System.Reflection.TypeAttributes attr, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] System.Type? parent, System.Type[]? interfaces)
            => throw new NotImplementedException();

        public System.Reflection.Emit.FieldBuilder DefineUninitializedData(string name, int size, System.Reflection.FieldAttributes attributes)
            => throw new NotImplementedException();

        public System.Reflection.MethodInfo GetArrayMethod(System.Type arrayClass, string methodName, System.Reflection.CallingConventions callingConvention, System.Type? returnType, System.Type[]? parameterTypes)
            => throw new NotImplementedException();

        public void SetCustomAttribute(System.Reflection.ConstructorInfo con, byte[] binaryAttribute)
           => throw new NotImplementedException();

        public void SetCustomAttribute(System.Reflection.Emit.CustomAttributeBuilder customBuilder)
            => throw new NotImplementedException();
    }
}
