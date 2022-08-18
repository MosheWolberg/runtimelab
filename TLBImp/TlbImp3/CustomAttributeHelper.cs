// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

using System;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace TypeLibUtilities
{
    /// <summary>
    /// Helper class used to create CustomAttributeBuilder for different kinds of CustomAttributes
    /// We can also speed up performance by saving instances of ConstructorInfo or CustomAttributeBuilder
    /// </summary>
    internal static class CustomAttributeHelper
    {
        public static CustomAttributeBuilder GetBuilderFor<T>(params object[] args) where T : Attribute
        {
            Type[] argTypes = new Type[args.Length];
            for (int i = 0; i < args.Length; ++i)
            {
                argTypes[i] = args[i].GetType();
            }

            ConstructorInfo ctor = typeof(T).GetConstructor(argTypes);
            return new CustomAttributeBuilder(ctor, args);
        }

        public static CustomAttributeBuilder GetBuilderForGuid(Guid guid)
        {
            ConstructorInfo ctor = typeof(GuidAttribute).GetConstructor(new Type[] { typeof(string) });
            return new CustomAttributeBuilder(ctor, new object[] { guid.ToString().ToUpper() });
        }

        public static CustomAttributeBuilder GetBuilderForInterfaceType(ComInterfaceType interfaceType)
        {
            ConstructorInfo ctor = typeof(InterfaceTypeAttribute).GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new Type[] { typeof(ComInterfaceType) },
                null);

            return new CustomAttributeBuilder(ctor, new object[] { interfaceType });
        }

        public static CustomAttributeBuilder GetBuilderForComVisible(bool isVisible)
        {
            ConstructorInfo ctor = typeof(ComVisibleAttribute).GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new Type[] { typeof(bool) },
                null);

            return new CustomAttributeBuilder(ctor, new object[] { isVisible });
        }

        public static CustomAttributeBuilder GetBuilderForMarshalAsConstArray(UnmanagedType unmanagedType, int length)
        {
            ConstructorInfo ctor = typeof(MarshalAsAttribute).GetConstructor(new Type[] { typeof(UnmanagedType) });
            FieldInfo fieldSizeConst = typeof(MarshalAsAttribute).GetField("SizeConst");
            return new CustomAttributeBuilder(
                ctor,
                new object[] { unmanagedType },
                new FieldInfo[] { fieldSizeConst },
                new object[] { length });
        }

        public static CustomAttributeBuilder GetBuilderForMarshalAsConstArray(UnmanagedType unmanagedType, int length, UnmanagedType arraySubType)
        {
            ConstructorInfo ctor = typeof(MarshalAsAttribute).GetConstructor(new Type[] { typeof(UnmanagedType) });
            FieldInfo fieldSizeConst = typeof(MarshalAsAttribute).GetField("SizeConst");
            FieldInfo fieldArraySubType = typeof(MarshalAsAttribute).GetField("ArraySubType");
            return new CustomAttributeBuilder(
                ctor,
                new object[] { unmanagedType },
                new FieldInfo[] { fieldSizeConst, fieldArraySubType },
                new object[] { length, arraySubType });
        }

        public static CustomAttributeBuilder GetBuilderForMarshalAsCustomMarshaler(Type customMarshaler, string marshalCookie)
        {
            ConstructorInfo ctor = typeof(MarshalAsAttribute).GetConstructor(new Type[] { typeof(UnmanagedType) });
            FieldInfo fieldMarshalTypeRef = typeof(MarshalAsAttribute).GetField("MarshalTypeRef");
            FieldInfo fieldMarshalCookie = typeof(MarshalAsAttribute).GetField("MarshalCookie");

            return new CustomAttributeBuilder(
                ctor,
                new object[] { UnmanagedType.CustomMarshaler },
                new FieldInfo[] { fieldMarshalTypeRef, fieldMarshalCookie },
                new object[] { customMarshaler, marshalCookie });
        }

        public static CustomAttributeBuilder GetBuilderForMarshalAsSafeArray(VarEnum safeArraySubType)
        {
            ConstructorInfo ctor = typeof(MarshalAsAttribute).GetConstructor(new Type[] { typeof(UnmanagedType) });
            FieldInfo fieldSafeArraySubType = typeof(MarshalAsAttribute).GetField("SafeArraySubType");
            return new CustomAttributeBuilder(
                ctor,
                new object[] { UnmanagedType.SafeArray },
                new FieldInfo[] { fieldSafeArraySubType },
                new object[] { safeArraySubType });
        }

        public static CustomAttributeBuilder GetBuilderForMarshalAsSafeArrayAndUserDefinedSubType(VarEnum safeArraySubType, Type safeArrayUserDefinedSubType)
        {
            ConstructorInfo ctor = typeof(MarshalAsAttribute).GetConstructor(new Type[] { typeof(UnmanagedType) });
            FieldInfo fieldSafeArraySubType = typeof(MarshalAsAttribute).GetField("SafeArraySubType");
            FieldInfo fieldSafeArrayUserDefinedSubType = typeof(MarshalAsAttribute).GetField("SafeArrayUserDefinedSubType");
            return new CustomAttributeBuilder(
                ctor,
                new object[] { UnmanagedType.SafeArray },
                new FieldInfo[] { fieldSafeArraySubType, fieldSafeArrayUserDefinedSubType },
                new object[] { safeArraySubType, safeArrayUserDefinedSubType });
        }

        public static CustomAttributeBuilder GetBuilderForFieldOffset(int offset)
        {
            ConstructorInfo ctor = typeof(FieldOffsetAttribute).GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new Type[] { typeof(int) },
                null);
            return new CustomAttributeBuilder(ctor, new object[] { 0 });
        }

        public static CustomAttributeBuilder GetBuilderForStructLayout(LayoutKind layoutKind, int pack)
        {
            ConstructorInfo ctor = typeof(StructLayoutAttribute).GetConstructor(new Type[] { typeof(LayoutKind) });
            FieldInfo fieldPack = typeof(StructLayoutAttribute).GetField("Pack");
            return new CustomAttributeBuilder(
                ctor,
                new object[] { layoutKind },
                new FieldInfo[] { fieldPack },
                new object[] { pack });
        }

        public static CustomAttributeBuilder GetBuilderForStructLayout(LayoutKind layoutKind, int pack, int size)
        {
            ConstructorInfo ctor = typeof(StructLayoutAttribute).GetConstructor(new Type[] { typeof(LayoutKind) });
            FieldInfo fieldPack = typeof(StructLayoutAttribute).GetField("Pack");
            FieldInfo fieldSize = typeof(StructLayoutAttribute).GetField("Size");
            return new CustomAttributeBuilder(
                ctor,
                new object[] { layoutKind },
                new FieldInfo[] { fieldPack, fieldSize },
                new object[] { pack, size });
        }

        public static CustomAttributeBuilder GetBuilderForDecimalConstant(byte scale, byte sign, uint hi, uint mid, uint low)
        {
            // TlbimpV1 uses the uint version, so use the uint version to save some time
            ConstructorInfo ctor = typeof(DecimalConstantAttribute).GetConstructor(
                new Type[] { typeof(byte), typeof(byte), typeof(uint), typeof(uint), typeof(uint) });

            return new CustomAttributeBuilder(ctor, new object[] { scale, sign, hi, mid, low });
        }

        public static CustomAttributeBuilder GetBuilderForIDispatchConstant()
        {
            // [TODO] - Is support for IDispatchConstantAttribute needed?
            throw new NotSupportedException();

            //ConstructorInfo ctor = typeof(IDispatchConstantAttribute).GetConstructor(new Type[] {});
            //return new CustomAttributeBuilder(ctor, new object[] { });
        }
    }
}