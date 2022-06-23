﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace System.Reflection.Emit.Experimental
{
    public class TypeBuilder : System.Reflection.TypeInfo
    {
        public override string Name { get; }
        public override Assembly Assembly { get; }
        public override ModuleBuilder Module { get; }
        public override string? Namespace { get; }
        private  LinkedList<MethodBuilder> _methods = new();
        internal MethodDefinitionHandle? _first = null;
        public TypeAttributes TypeAttribute { get; }

        internal TypeBuilder(string name, ModuleBuilder module, Assembly assembly, TypeAttributes typeAttributes) 
        {
            Name = name; 
            Module = module;
            Assembly = assembly;
            TypeAttribute = typeAttributes;

            //Extract namespace from name
            int idx = Name.LastIndexOf('.');
            if (idx != -1)
            {
                Namespace = Name.Substring(0, idx);
                Name  = Name.Substring(idx + 1);
            }
        }

        public System.Reflection.Emit.Experimental.MethodBuilder DefineMethod(string name, System.Reflection.MethodAttributes attributes, System.Reflection.CallingConventions callingConvention, System.Type? returnType, System.Type[]? parameterTypes)
        {
            MethodBuilder methodBuilder = new(name, attributes, callingConvention, returnType, parameterTypes, this);
            _methods.AddLast(methodBuilder);
            return methodBuilder;
        }

        //Implement next
        public System.Reflection.Emit.Experimental.MethodBuilder DefineMethod(string name, System.Reflection.MethodAttributes attributes)
        {
             throw new NotImplementedException();
        }

        //Generate underlying metadata of e.g. methods and fields, before constructing type metadata.
        internal void GenerateComponentMetadata(MetadataBuilder _metadata)
        {
            int i = 0;
            foreach (MethodBuilder m in _methods)
            {
                MethodDefinitionHandle handle = m.AppendMetadata(_metadata);
                if(i==0)
                {
                    _first = handle;
                }
                i++;
            }
        }

        internal void AppendMetadata(MetadataBuilder _metadata)
        {
            MethodDefinitionHandle? _nearestHandle = null;

            // Check if I have methods.
            if(_first!=null)
            {
                _nearestHandle = _first;
            }
            else // If not, find the next method in the module.
            {
                foreach(TypeBuilder t in Module._typeStorage)
                {
                    if(t._first!=null)
                    {
                        _nearestHandle = t._first;
                        break;
                    }
                }
            }

            //Add type metadata
            _metadata.AddTypeDefinition(
                attributes: TypeAttribute,
                (Namespace == null) ? default : _metadata.GetOrAddString(Namespace),
                name: _metadata.GetOrAddString(Name),
                baseType: default,//Inheritance to be added
                fieldList: MetadataTokens.FieldDefinitionHandle(1),//Update once we support fields.
                methodList: (MethodDefinitionHandle)((_nearestHandle!=null)? _nearestHandle : MetadataTokens.MethodDefinitionHandle(1))); // if null, no methods in module.
        }

        public const int UnspecifiedTypeSize = 0;
        public override string? AssemblyQualifiedName { get  => throw new NotImplementedException(); }
        public override System.Type? BaseType { get  => throw new NotImplementedException(); }
        public override System.Reflection.MethodBase? DeclaringMethod { get  => throw new NotImplementedException(); }
        public override System.Type? DeclaringType { get  => throw new NotImplementedException(); }
        public override string? FullName { get  => throw new NotImplementedException(); }
        public override System.Reflection.GenericParameterAttributes GenericParameterAttributes { get  => throw new NotImplementedException(); }
        public override int GenericParameterPosition { get  => throw new NotImplementedException(); }
        public override System.Guid GUID { get  => throw new NotImplementedException(); }
        public override bool IsByRefLike { get  => throw new NotImplementedException(); }
        public override bool IsConstructedGenericType { get  => throw new NotImplementedException(); }
        public override bool IsGenericParameter { get  => throw new NotImplementedException(); }
        public override bool IsGenericType { get  => throw new NotImplementedException(); }
        public override bool IsGenericTypeDefinition { get  => throw new NotImplementedException(); }
        public override bool IsSecurityCritical { get  => throw new NotImplementedException(); }
        public override bool IsSecuritySafeCritical { get  => throw new NotImplementedException(); }
        public override bool IsSecurityTransparent { get  => throw new NotImplementedException(); }
        public override bool IsSZArray { get  => throw new NotImplementedException(); }
        public override bool IsTypeDefinition { get  => throw new NotImplementedException(); }
        public override int MetadataToken { get  => throw new NotImplementedException(); }
        public System.Reflection.Emit.PackingSize PackingSize { get  => throw new NotImplementedException(); }
        public override System.Type? ReflectedType { get  => throw new NotImplementedException(); }
        public int Size { get  => throw new NotImplementedException(); }
        public override System.RuntimeTypeHandle TypeHandle { get  => throw new NotImplementedException(); }
        public override System.Type UnderlyingSystemType { get  => throw new NotImplementedException(); }

        public void AddInterfaceImplementation([System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] System.Type interfaceType) 
            => throw new NotImplementedException();

        [return: System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)]
        public System.Type? CreateType()
            => throw new NotImplementedException();

        [return: System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)]
        public System.Reflection.TypeInfo? CreateTypeInfo()
            => throw new NotImplementedException();

        public System.Reflection.Emit.ConstructorBuilder DefineConstructor(System.Reflection.MethodAttributes attributes, System.Reflection.CallingConventions callingConvention, System.Type[]? parameterTypes)
            => throw new NotImplementedException();

        public System.Reflection.Emit.ConstructorBuilder DefineConstructor(System.Reflection.MethodAttributes attributes, System.Reflection.CallingConventions callingConvention, System.Type[]? parameterTypes, System.Type[][]? requiredCustomModifiers, System.Type[][]? optionalCustomModifiers)  => throw new NotImplementedException();
        
        public System.Reflection.Emit.ConstructorBuilder DefineDefaultConstructor(System.Reflection.MethodAttributes attributes)
            => throw new NotImplementedException();

        public System.Reflection.Emit.EventBuilder DefineEvent(string name, System.Reflection.EventAttributes attributes, System.Type eventtype)
            => throw new NotImplementedException();

        public System.Reflection.Emit.FieldBuilder DefineField(string fieldName, System.Type type, System.Reflection.FieldAttributes attributes)
            => throw new NotImplementedException();

        public System.Reflection.Emit.FieldBuilder DefineField(string fieldName, System.Type type, System.Type[]? requiredCustomModifiers, System.Type[]? optionalCustomModifiers, System.Reflection.FieldAttributes attributes)  => throw new NotImplementedException();
        
        public System.Reflection.Emit.GenericTypeParameterBuilder[] DefineGenericParameters(params string[] names)
            => throw new NotImplementedException();

        public System.Reflection.Emit.FieldBuilder DefineInitializedData(string name, byte[] data, System.Reflection.FieldAttributes attributes)
            => throw new NotImplementedException();

        public System.Reflection.Emit.MethodBuilder DefineMethod(string name, System.Reflection.MethodAttributes attributes, System.Reflection.CallingConventions callingConvention)
            => throw new NotImplementedException();

        public System.Reflection.Emit.MethodBuilder DefineMethod(string name, System.Reflection.MethodAttributes attributes, System.Reflection.CallingConventions callingConvention, System.Type? returnType, System.Type[]? returnTypeRequiredCustomModifiers, System.Type[]? returnTypeOptionalCustomModifiers, System.Type[]? parameterTypes, System.Type[][]? parameterTypeRequiredCustomModifiers, System.Type[][]? parameterTypeOptionalCustomModifiers)  
            => throw new NotImplementedException();

        public System.Reflection.Emit.MethodBuilder DefineMethod(string name, System.Reflection.MethodAttributes attributes, System.Type? returnType, System.Type[]? parameterTypes)
            => throw new NotImplementedException();

        public void DefineMethodOverride(System.Reflection.MethodInfo methodInfoBody, System.Reflection.MethodInfo methodInfoDeclaration)
            => throw new NotImplementedException();

        public System.Reflection.Emit.TypeBuilder DefineNestedType(string name)
            => throw new NotImplementedException();

        public System.Reflection.Emit.TypeBuilder DefineNestedType(string name, System.Reflection.TypeAttributes attr)
            => throw new NotImplementedException();

        public System.Reflection.Emit.TypeBuilder DefineNestedType(string name, System.Reflection.TypeAttributes attr, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] System.Type? parent)
            => throw new NotImplementedException();

        public System.Reflection.Emit.TypeBuilder DefineNestedType(string name, System.Reflection.TypeAttributes attr, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] System.Type? parent, int typeSize)
            => throw new NotImplementedException();

        public System.Reflection.Emit.TypeBuilder DefineNestedType(string name, System.Reflection.TypeAttributes attr, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] System.Type? parent, System.Reflection.Emit.PackingSize packSize)
            => throw new NotImplementedException();

        public System.Reflection.Emit.TypeBuilder DefineNestedType(string name, System.Reflection.TypeAttributes attr, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] System.Type? parent, System.Reflection.Emit.PackingSize packSize, int typeSize)
            => throw new NotImplementedException();

        public System.Reflection.Emit.TypeBuilder DefineNestedType(string name, System.Reflection.TypeAttributes attr, [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] System.Type? parent, System.Type[]? interfaces)
            => throw new NotImplementedException();

        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute("P/Invoke marshalling may dynamically access members that could be trimmed.")]
        public System.Reflection.Emit.MethodBuilder DefinePInvokeMethod(string name, string dllName, System.Reflection.MethodAttributes attributes, System.Reflection.CallingConventions callingConvention, System.Type? returnType, System.Type[]? parameterTypes, System.Runtime.InteropServices.CallingConvention nativeCallConv, System.Runtime.InteropServices.CharSet nativeCharSet)
            => throw new NotImplementedException();

        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute("P/Invoke marshalling may dynamically access members that could be trimmed.")]
        public System.Reflection.Emit.MethodBuilder DefinePInvokeMethod(string name, string dllName, string entryName, System.Reflection.MethodAttributes attributes, System.Reflection.CallingConventions callingConvention, System.Type? returnType, System.Type[]? parameterTypes, System.Runtime.InteropServices.CallingConvention nativeCallConv, System.Runtime.InteropServices.CharSet nativeCharSet)
            => throw new NotImplementedException();

        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute("P/Invoke marshalling may dynamically access members that could be trimmed.")]
        public System.Reflection.Emit.MethodBuilder DefinePInvokeMethod(string name, string dllName, string entryName, System.Reflection.MethodAttributes attributes, System.Reflection.CallingConventions callingConvention, System.Type? returnType, System.Type[]? returnTypeRequiredCustomModifiers, System.Type[]? returnTypeOptionalCustomModifiers, System.Type[]? parameterTypes, System.Type[][]? parameterTypeRequiredCustomModifiers, System.Type[][]? parameterTypeOptionalCustomModifiers, System.Runtime.InteropServices.CallingConvention nativeCallConv, System.Runtime.InteropServices.CharSet nativeCharSet)
            => throw new NotImplementedException();

        public System.Reflection.Emit.PropertyBuilder DefineProperty(string name, System.Reflection.PropertyAttributes attributes, System.Reflection.CallingConventions callingConvention, System.Type returnType, System.Type[]? parameterTypes)
            => throw new NotImplementedException();

        public System.Reflection.Emit.PropertyBuilder DefineProperty(string name, System.Reflection.PropertyAttributes attributes, System.Reflection.CallingConventions callingConvention, System.Type returnType, System.Type[]? returnTypeRequiredCustomModifiers, System.Type[]? returnTypeOptionalCustomModifiers, System.Type[]? parameterTypes, System.Type[][]? parameterTypeRequiredCustomModifiers, System.Type[][]? parameterTypeOptionalCustomModifiers) 
            => throw new NotImplementedException();

        public System.Reflection.Emit.PropertyBuilder DefineProperty(string name, System.Reflection.PropertyAttributes attributes, System.Type returnType, System.Type[]? parameterTypes)
            => throw new NotImplementedException();

        public System.Reflection.Emit.PropertyBuilder DefineProperty(string name, System.Reflection.PropertyAttributes attributes, System.Type returnType, System.Type[]? returnTypeRequiredCustomModifiers, System.Type[]? returnTypeOptionalCustomModifiers, System.Type[]? parameterTypes, System.Type[][]? parameterTypeRequiredCustomModifiers, System.Type[][]? parameterTypeOptionalCustomModifiers) 
            => throw new NotImplementedException();

        public System.Reflection.Emit.ConstructorBuilder DefineTypeInitializer()
            => throw new NotImplementedException();

        public System.Reflection.Emit.FieldBuilder DefineUninitializedData(string name, int size, System.Reflection.FieldAttributes attributes)
            => throw new NotImplementedException();

        protected override System.Reflection.TypeAttributes GetAttributeFlagsImpl()
            => throw new NotImplementedException();

        public static System.Reflection.ConstructorInfo GetConstructor(System.Type type, System.Reflection.ConstructorInfo constructor)
            => throw new NotImplementedException();

        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicConstructors | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)]
        protected override System.Reflection.ConstructorInfo? GetConstructorImpl(System.Reflection.BindingFlags bindingAttr, System.Reflection.Binder? binder, System.Reflection.CallingConventions callConvention, System.Type[] types, System.Reflection.ParameterModifier[]? modifiers)
            => throw new NotImplementedException();

        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicConstructors | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors)]
        public override System.Reflection.ConstructorInfo[] GetConstructors(System.Reflection.BindingFlags bindingAttr)  
            => throw new NotImplementedException();

        public override object[] GetCustomAttributes(bool inherit)  
            => throw new NotImplementedException();

        public override object[] GetCustomAttributes(System.Type attributeType, bool inherit)  
            => throw new NotImplementedException();

        public override System.Type GetElementType()  
            => throw new NotImplementedException();

        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicEvents | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicEvents)]
        public override System.Reflection.EventInfo? GetEvent(string name, System.Reflection.BindingFlags bindingAttr)  
            => throw new NotImplementedException();

        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicEvents)]
        public override System.Reflection.EventInfo[] GetEvents()  
            => throw new NotImplementedException();
        
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicEvents | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicEvents)]
        public override System.Reflection.EventInfo[] GetEvents(System.Reflection.BindingFlags bindingAttr)  
            => throw new NotImplementedException();
        
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicFields | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicFields)]
        public override System.Reflection.FieldInfo? GetField(string name, System.Reflection.BindingFlags bindingAttr)  
            => throw new NotImplementedException();

        public static System.Reflection.FieldInfo GetField(System.Type type, System.Reflection.FieldInfo field)  
            => throw new NotImplementedException();

        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicFields | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicFields)]
        public override System.Reflection.FieldInfo[] GetFields(System.Reflection.BindingFlags bindingAttr)  
            => throw new NotImplementedException();
        
        public override System.Type[] GetGenericArguments()  
            => throw new NotImplementedException();

        public override System.Type GetGenericTypeDefinition()  
            => throw new NotImplementedException();

        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.Interfaces)]
        [return: System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.Interfaces)]
        public override System.Type? GetInterface(string name, bool ignoreCase)  
            => throw new NotImplementedException();

        public override System.Reflection.InterfaceMapping GetInterfaceMap([System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicMethods | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)] System.Type interfaceType) 
            => throw new NotImplementedException();

        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.Interfaces)]
        public override System.Type[] GetInterfaces()  
            => throw new NotImplementedException();

        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicConstructors | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicEvents | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicFields | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicMethods | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicNestedTypes | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicEvents | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicFields | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicNestedTypes | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
        public override System.Reflection.MemberInfo[] GetMember(string name, System.Reflection.MemberTypes type, System.Reflection.BindingFlags bindingAttr)  
            => throw new NotImplementedException();

        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicConstructors | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicEvents | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicFields | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicMethods | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicNestedTypes | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicEvents | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicFields | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicNestedTypes | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
        public override System.Reflection.MemberInfo[] GetMembers(System.Reflection.BindingFlags bindingAttr)  
            => throw new NotImplementedException();
        
        public static System.Reflection.MethodInfo GetMethod(System.Type type, System.Reflection.MethodInfo method)  
            => throw new NotImplementedException();
        
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicMethods | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)]
        protected override System.Reflection.MethodInfo? GetMethodImpl(string name, System.Reflection.BindingFlags bindingAttr, System.Reflection.Binder? binder, System.Reflection.CallingConventions callConvention, System.Type[]? types, System.Reflection.ParameterModifier[]? modifiers)  
            => throw new NotImplementedException();
        
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicMethods | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)]
        public override System.Reflection.MethodInfo[] GetMethods(System.Reflection.BindingFlags bindingAttr)  
            => throw new NotImplementedException();
        
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicNestedTypes | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicNestedTypes)]
        public override System.Type? GetNestedType(string name, System.Reflection.BindingFlags bindingAttr)  
            => throw new NotImplementedException();
        
        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicNestedTypes | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicNestedTypes)]
        public override System.Type[] GetNestedTypes(System.Reflection.BindingFlags bindingAttr)  
            => throw new NotImplementedException();

        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
        public override System.Reflection.PropertyInfo[] GetProperties(System.Reflection.BindingFlags bindingAttr)
            => throw new NotImplementedException();

        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.NonPublicProperties | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties)]
        protected override System.Reflection.PropertyInfo GetPropertyImpl(string name, System.Reflection.BindingFlags bindingAttr, System.Reflection.Binder? binder, System.Type? returnType, System.Type[]? types, System.Reflection.ParameterModifier[]? modifiers)
            => throw new NotImplementedException();

        protected override bool HasElementTypeImpl()
            => throw new NotImplementedException();

        [System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)]
        public override object? InvokeMember(string name, System.Reflection.BindingFlags invokeAttr, System.Reflection.Binder? binder, object? target, object?[]? args, System.Reflection.ParameterModifier[]? modifiers, System.Globalization.CultureInfo? culture, string[]? namedParameters)
            => throw new NotImplementedException();

        protected override bool IsArrayImpl()
            => throw new NotImplementedException();

        public override bool IsAssignableFrom([System.Diagnostics.CodeAnalysis.NotNullWhenAttribute(true)] System.Reflection.TypeInfo? typeInfo)
            => throw new NotImplementedException();

        public override bool IsAssignableFrom([System.Diagnostics.CodeAnalysis.NotNullWhenAttribute(true)] System.Type? c)
            => throw new NotImplementedException();

        protected override bool IsByRefImpl()
            => throw new NotImplementedException();

        protected override bool IsCOMObjectImpl()
            => throw new NotImplementedException();

        public bool IsCreated()
            => throw new NotImplementedException();

        public override bool IsDefined(System.Type attributeType, bool inherit)
            => throw new NotImplementedException();

        protected override bool IsPointerImpl()
            => throw new NotImplementedException();

        protected override bool IsPrimitiveImpl()
            => throw new NotImplementedException();

        public override bool IsSubclassOf(System.Type c)
            => throw new NotImplementedException();

        [System.Diagnostics.CodeAnalysis.RequiresDynamicCodeAttribute("The code for an array of the specified type might not be available.")]
        public override System.Type MakeArrayType()
            => throw new NotImplementedException();

        [System.Diagnostics.CodeAnalysis.RequiresDynamicCodeAttribute("The code for an array of the specified type might not be available.")]
        public override System.Type MakeArrayType(int rank)
            => throw new NotImplementedException();

        public override System.Type MakeByRefType()
            => throw new NotImplementedException();

        [System.Diagnostics.CodeAnalysis.RequiresDynamicCodeAttribute("The native code for this instantiation might not be available at runtime.")]
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute("If some of the generic arguments are annotated (either with DynamicallyAccessedMembersAttribute, or generic constraints), trimming can't validate that the requirements of those annotations are met.")]
        public override System.Type MakeGenericType(params System.Type[] typeArguments)
            => throw new NotImplementedException();

        public override System.Type MakePointerType()
            => throw new NotImplementedException();

        public void SetCustomAttribute(System.Reflection.ConstructorInfo con, byte[] binaryAttribute) 
            => throw new NotImplementedException();

        public void SetCustomAttribute(System.Reflection.Emit.CustomAttributeBuilder customBuilder) 
            => throw new NotImplementedException();
        
        public void SetParent([System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembersAttribute(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All)] System.Type? parent) 
            => throw new NotImplementedException();

        public override string ToString()
            => throw new NotImplementedException();
    }
}
