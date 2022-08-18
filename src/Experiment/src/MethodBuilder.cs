// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Metadata;
using static System.Reflection.Emit.Experimental.EntityWrappers;

namespace System.Reflection.Emit.Experimental
{
    public class MethodBuilder : System.Reflection.MethodInfo
    {
        public override string Name { get; }
        public override System.Reflection.MethodAttributes Attributes { get; }
        public override System.Reflection.CallingConventions CallingConvention { get; }
        public override TypeBuilder DeclaringType { get; }
        public override System.Reflection.Emit.Experimental.ModuleBuilder Module { get; }
        internal List<CustomAttributeWrapper> _customAttributes = new ();
        internal Type? _returnType;
#pragma warning disable SA1011 // Closing square brackets should be spaced correctly
        internal Type[]? _parameterTypes;
        internal List<ParameterBuilder> Parameters = new List<ParameterBuilder>();
#pragma warning restore SA1011 // Closing square brackets should be spaced correctly

        internal MethodBuilder(string name, System.Reflection.MethodAttributes attributes, CallingConventions callingConventions, Type? returnType, Type[]? parameters, TypeBuilder declaringType)
        {
            ArgumentException.ThrowIfNullOrEmpty(name);

            if (name[0] == '\0')
            {
                throw new ArgumentException("Illegal name: " + nameof(name));
            }

            if (parameters != null)
            {
                foreach (Type t in parameters)
                {
                    ArgumentNullException.ThrowIfNull(t, nameof(parameters));
                }
            }

            Name = name;
            Attributes = attributes;
            CallingConvention = callingConventions;
            _returnType = returnType ?? typeof(void);
            _parameterTypes = parameters;
            DeclaringType = declaringType;
            Module = declaringType.Module;
        }

        public System.Reflection.Emit.Experimental.ParameterBuilder DefineParameter(int position, System.Reflection.ParameterAttributes attributes, string? strParamName)
        {
            if (position < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            if (position > 0 && (_parameterTypes == null || position > _parameterTypes.Length))
            {
                throw new ArgumentOutOfRangeException((nameof(position)));
            }

            ParameterBuilder builder = new ParameterBuilder(position, attributes, strParamName);
            Parameters.Add(builder);
            return builder;
        }

        public void SetImplementationFlags(System.Reflection.MethodImplAttributes attributes)
        {
            throw new NotImplementedException();
        }

        public void SetParameters(params System.Type[] parameterTypes)
        {
            throw new NotImplementedException();
        }

        public void SetReturnType(System.Type? returnType)
        {
            throw new NotImplementedException();
        }

#pragma warning disable SA1011 // Closing square brackets should be spaced correctly
        public void SetSignature(System.Type? returnType, System.Type[]? returnTypeRequiredCustomModifiers, System.Type[]? returnTypeOptionalCustomModifiers, System.Type[]? parameterTypes, System.Type[][]? parameterTypeRequiredCustomModifiers, System.Type[][]? parameterTypeOptionalCustomModifiers)
#pragma warning restore SA1011 // Closing square brackets should be spaced correctly
        {
            throw new NotImplementedException();
        }

        public override bool ContainsGenericParameters { get => throw new NotImplementedException(); }
        public bool InitLocals
        {
            get => throw new NotImplementedException(); set { }
        }

        public override bool IsGenericMethod { get => throw new NotImplementedException(); }
        public override bool IsGenericMethodDefinition { get => throw new NotImplementedException(); }
        public override bool IsSecurityCritical { get => throw new NotImplementedException(); }
        public override bool IsSecuritySafeCritical { get => throw new NotImplementedException(); }
        public override bool IsSecurityTransparent { get => throw new NotImplementedException(); }
        public override int MetadataToken { get => throw new NotImplementedException(); }
        public override System.RuntimeMethodHandle MethodHandle { get => throw new NotImplementedException(); }
        public override System.Type? ReflectedType { get => throw new NotImplementedException(); }
        public override System.Reflection.ParameterInfo ReturnParameter { get => throw new NotImplementedException(); }
        public override System.Type ReturnType { get => throw new NotImplementedException(); }
        public override System.Reflection.ICustomAttributeProvider ReturnTypeCustomAttributes { get => throw new NotImplementedException(); }

        public System.Reflection.Emit.GenericTypeParameterBuilder[] DefineGenericParameters(params string[] names)
            => throw new NotImplementedException();

        public override System.Reflection.MethodInfo GetBaseDefinition()
            => throw new NotImplementedException();

        public override object[] GetCustomAttributes(bool inherit)
            => throw new NotImplementedException();

        public override object[] GetCustomAttributes(System.Type attributeType, bool inherit)
            => throw new NotImplementedException();

        public override System.Type[] GetGenericArguments()
            => throw new NotImplementedException();

        public override System.Reflection.MethodInfo GetGenericMethodDefinition()
            => throw new NotImplementedException();

        public override int GetHashCode()
            => throw new NotImplementedException();

        public System.Reflection.Emit.ILGenerator GetILGenerator()
           => throw new NotImplementedException();
        public System.Reflection.Emit.ILGenerator GetILGenerator(int size)
            => throw new NotImplementedException();

        public override System.Reflection.MethodImplAttributes GetMethodImplementationFlags()
            => throw new NotImplementedException();

        public override System.Reflection.ParameterInfo[] GetParameters()
            => throw new NotImplementedException();

        public override object Invoke(object? obj, System.Reflection.BindingFlags invokeAttr, System.Reflection.Binder? binder, object?[]? parameters, System.Globalization.CultureInfo? culture)
            => throw new NotImplementedException();

        public override bool IsDefined(System.Type attributeType, bool inherit)
            => throw new NotImplementedException();

        [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("The native code for this instantiation might not be available at runtime.")]
        [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCodeAttribute("If some of the generic arguments are annotated (either with DynamicallyAccessedMembersAttribute, or generic constraints), trimming can't validate that the requirements of those annotations are met.")]
        public override System.Reflection.MethodInfo MakeGenericMethod(params System.Type[] typeArguments)
            => throw new NotImplementedException();

        public void SetCustomAttribute(System.Reflection.ConstructorInfo constructorInfo, byte[] binaryAttribute)
        {
            if (constructorInfo == null)
            {
                throw new ArgumentNullException(nameof(constructorInfo));
            }

            if (binaryAttribute == null)
            {
                throw new ArgumentNullException(nameof(binaryAttribute));
            }

            if (constructorInfo.DeclaringType == null)
            {
                throw new ArgumentException("Attribute constructor has no type.");
            }

            // We check whether the custom attribute is actually a pseudo-custom attribute.
            // (We have only done ComImport for the prototype, eventually all pseudo-custom attributes will be hard-coded.)
            // If it is, simply alter the TypeAttributes.
            // We want to handle this before the type metadata is generated.
            if (constructorInfo.DeclaringType.Name.Equals("ComImportAttribute"))
            {
                Debug.WriteLine("Modifying internal flags");

            }
            else
            {
                CustomAttributeWrapper customAttribute = new CustomAttributeWrapper(constructorInfo, binaryAttribute);
                EntityHandle constructorHandle = Module.AddorGetMethodReference(constructorInfo);
                customAttribute.ConToken = constructorHandle;
                _customAttributes.Add(customAttribute);
            }
        }

        public void SetCustomAttribute(System.Reflection.Emit.Experimental.CustomAttributeBuilder customBuilder)
        {
            SetCustomAttribute(customBuilder.Constructor, customBuilder._blob);
        }

        public override string ToString()
            => Name;
    }
}
