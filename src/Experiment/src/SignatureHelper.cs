// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

namespace System.Reflection.Emit.Experimental
{
    internal static class SignatureHelper
    {
        internal static BlobBuilder FieldSignatureEncoder(Type fieldType, ModuleBuilder moduleBuilder)
        {
            var fieldSignature = new BlobBuilder();

            var encoder = new BlobEncoder(fieldSignature).FieldSignature();

            MapReflectionTypeToSignatureType(encoder, fieldType, moduleBuilder);

            return fieldSignature;
        }

        internal static BlobBuilder MethodSignatureEncoder(ParameterInfo[]? parameters, ParameterInfo? returnType, bool isInstance, ModuleBuilder moduleBuilder)
        {
            Type[]? typeParameters = null;
            Type? typeReturn = null;

            if (parameters != null)
            {
                typeParameters = Array.ConvertAll(parameters, parameter => parameter.ParameterType);
            }

            if (returnType != null)
            {
                typeReturn = returnType.ParameterType;
            }

            return MethodSignatureEncoder(typeParameters, typeReturn, isInstance, moduleBuilder);
        }

        internal static BlobBuilder MethodSignatureEncoder(Type[]? parameters, Type? returnType, bool isInstance, ModuleBuilder moduleBuilder)
        {
            // Encoding return type and parameters.
            var methodSignature = new BlobBuilder();

            ParametersEncoder parEncoder;
            ReturnTypeEncoder retEncoder;

            new BlobEncoder(methodSignature)
                .MethodSignature(isInstanceMethod: isInstance).
                Parameters((parameters == null) ? 0 : parameters.Length, out retEncoder, out parEncoder);

            if (returnType != null && returnType != typeof(void))
            {
                MapReflectionTypeToSignatureType(retEncoder.Type(), returnType, moduleBuilder);
            }
            else // If null mark ReturnTypeEncoder as void
            {
                retEncoder.Void();
            }

            if (parameters != null) // If parameters null, just keep empty ParametersEncoder empty
            {
                foreach (var parameter in parameters)
                {
                    MapReflectionTypeToSignatureType(parEncoder.AddParameter().Type(), parameter, moduleBuilder);
                }
            }

            return methodSignature;
        }

        private static void MapReflectionTypeToSignatureType(SignatureTypeEncoder signature, Type type, ModuleBuilder module)
        {
            bool standardType = true;

            if (type.IsArray) // Currently assuming SZ arrays
            {
                signature.SZArray();
                var type1 = type.GetElementType();
                if (type1 == null)
                {
                    throw new ArgumentException("Array has no type");
                }

                type = type1;
            }

            // We need to translate from Reflection.Type to SignatureTypeEncoder.
            if (type == ContextType(typeof(bool), module))
            {
                signature.Boolean();
            }
            else if (type == ContextType(typeof(byte), module))
            {
                signature.Byte();
            }
            else if (type == ContextType(typeof(sbyte), module))
            {
                signature.SByte();
            }
            else if (type == ContextType(typeof(char), module))
            {
                signature.Char();
            }
            else if (type == ContextType(typeof(decimal), module))
            {
                standardType = false;
                // Encoder doesn't have native operation for decimal.
            }
            else if (type == ContextType(typeof(double), module))
            {
                signature.Double();
            }
            else if (type == ContextType(typeof(float), module))
            {
                signature.Single();
            }
            else if (type == ContextType(typeof(int), module))
            {
                signature.Int32();
            }
            else if (type == ContextType(typeof(uint), module))
            {
                signature.UInt32();
            }
            else if (type == ContextType(typeof(nint), module))
            {
                signature.IntPtr();
            }
            else if (type == ContextType(typeof(nuint), module))
            {
                signature.UIntPtr();
            }
            else if (type == ContextType(typeof(long), module))
            {
                signature.Int64();
            }
            else if (type == ContextType(typeof(ulong), module))
            {
                signature.UInt64();
            }
            else if (type == ContextType(typeof(short), module))
            {
                signature.Int16();
            }
            else if (type == ContextType(typeof(ushort), module))
            {
                signature.UInt16();
            }
            else
            {
                standardType = false;
            }

            if (!standardType)
            {
                signature.Type(module.AddorGetTypeReference(type), type.IsValueType);
            }

        }

        internal static Type ContextType(Type type, ModuleBuilder module)
        {
            if (module._contextAssembly == null)
            {
                Debug.WriteLine($"Unable to locate specified context for {nameof(type)} , reverting to default context");
                return type;
            }

            Type? contextType = module._contextAssembly.GetType((type.FullName == null) ? type.Name : type.FullName);

            if (contextType == null)
            {
                Debug.WriteLine($"Unable to locate specified context for {nameof(type)} , reverting to default context");
                return type;
            }

            return contextType;
        }
    }
}
