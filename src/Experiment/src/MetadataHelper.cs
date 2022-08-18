﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using static System.Reflection.Emit.Experimental.EntityWrappers;

namespace System.Reflection.Emit.Experimental
{
    // This static helper class adds common entities to a Metadata Builder.
    internal static class MetadataHelper
    {
        internal static AssemblyReferenceHandle AddAssemblyReference(AssemblyName assembly, MetadataBuilder metadata)
        {
            if (assembly == null || assembly.Name == null)
            {
                throw new ArgumentException("Could not add to metadata: " + nameof(assembly));
            }

            return AddAssemblyReference(metadata, assembly.Name, assembly.Version, assembly.CultureName, assembly.GetPublicKey(), (AssemblyFlags)assembly.Flags);
        }

#pragma warning disable SA1011 // Closing square brackets should be spaced correctly
        internal static AssemblyReferenceHandle AddAssemblyReference(MetadataBuilder metadata, string name, Version? version, string? culture, byte[]? publicKey, AssemblyFlags flags)
#pragma warning restore SA1011 // Closing square brackets should be spaced correctly
        {
            return metadata.AddAssemblyReference(
            name: metadata.GetOrAddString(name),
            version: version ?? new Version(0, 0, 0, 0),
            culture: (culture == null) ? default : metadata.GetOrAddString(value: culture),
            publicKeyOrToken: (publicKey == null) ? default : metadata.GetOrAddBlob(publicKey),
            flags: flags,
            hashValue: default); // not sure where to find hashValue.
        }

        internal static TypeDefinitionHandle AddTypeDef(TypeBuilder typeBuilder, MetadataBuilder metadata, int methodToken, int fieldToken, EntityHandle? baseType)
        {
            // Add type metadata
            return metadata.AddTypeDefinition(
                attributes: typeBuilder.UserTypeAttribute,
                (typeBuilder.Namespace == null) ? default : metadata.GetOrAddString(typeBuilder.Namespace),
                name: metadata.GetOrAddString(typeBuilder.Name),
                baseType: baseType == null ? default : (EntityHandle)baseType,
                fieldList: MetadataTokens.FieldDefinitionHandle(fieldToken),
                methodList: MetadataTokens.MethodDefinitionHandle(methodToken));
        }

        internal static TypeReferenceHandle AddTypeReference(MetadataBuilder metadata, Type type, EntityHandle parent)
        {
            return AddTypeReference(metadata, parent, type.Name, type.Namespace);
        }

        internal static TypeReferenceHandle AddTypeReference(MetadataBuilder metadata, EntityHandle parent, string name, string? nameSpace)
        {
            return metadata.AddTypeReference(
                parent,
                (nameSpace == null) ? default : metadata.GetOrAddString(nameSpace),
                metadata.GetOrAddString(name));
        }

        internal static MemberReferenceHandle AddMethodReference(MetadataBuilder metadata, EntityHandle parent, MethodBase method, ModuleBuilder module)
        {
            Type[] parameters = Array.ConvertAll(method.GetParameters(), p => p.ParameterType);
            var blob = SignatureHelper.MethodSignatureEncoder(parameters, null, !method.IsStatic, module);
            return metadata.AddMemberReference(
                parent,
                metadata.GetOrAddString(method.Name),
                metadata.GetOrAddBlob(blob));
        }

        internal static MethodDefinitionHandle AddMethodDefintion(MetadataBuilder metadata, MethodBuilder methodBuilder, ModuleBuilder module, int paramToken)
        {
            return metadata.AddMethodDefinition(
                methodBuilder.Attributes,
                MethodImplAttributes.IL,
                metadata.GetOrAddString(methodBuilder.Name),
                metadata.GetOrAddBlob(SignatureHelper.MethodSignatureEncoder(methodBuilder._parameterTypes, methodBuilder._returnType, !methodBuilder.IsStatic, module)),
                -1,
                parameterList: MetadataTokens.ParameterHandle(paramToken));
        }

        internal static MethodDefinitionHandle AddConstructorDefintion(MetadataBuilder metadata, ConstructorBuilder methodBuilder, ModuleBuilder module, int offest)
        {
            Debug.WriteLine("Discovered attributes" + methodBuilder.Attributes);
            return metadata.AddMethodDefinition(
                methodBuilder.Attributes,
                MethodImplAttributes.IL,
                metadata.GetOrAddString(methodBuilder.Name),
                metadata.GetOrAddBlob(SignatureHelper.MethodSignatureEncoder(null, null, !methodBuilder.IsStatic, module)),
                bodyOffset: offest,
                parameterList: MetadataTokens.ParameterHandle(1));
        }

        internal static ParameterHandle AddParamDefintion(MetadataBuilder metadata, ParameterBuilder paramBuilder, ModuleBuilder module)
        {
            return metadata.AddParameter(
                paramBuilder.Attributes,
                (paramBuilder.Name == null) ? default : metadata.GetOrAddString(paramBuilder.Name),
                paramBuilder.Position);
        }

        internal static CustomAttributeHandle AddCustomAttr(MetadataBuilder metadata, CustomAttributeWrapper customAttribute, EntityHandle parent)
        {
           return metadata.AddCustomAttribute(parent, customAttribute.ConToken, metadata.GetOrAddBlob(customAttribute.BinaryAttribute));
        }

        internal static FieldDefinitionHandle AddFieldDefinition(MetadataBuilder metadata, FieldBuilder fieldBuilder)
        {
            return metadata.AddFieldDefinition(fieldBuilder.Attributes, metadata.GetOrAddString(fieldBuilder.Name), metadata.GetOrAddBlob(fieldBuilder.FieldSignature));
        }
    }
}
