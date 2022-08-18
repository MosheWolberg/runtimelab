// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using TypeLibUtilities.TypeLibAPI;

namespace TypeLibUtilities
{
    /// <summary>
    /// Interface for union conversion
    /// </summary>
    internal interface IConvUnion : IConvBase
    {
    }

    /// <summary>
    /// Conversion from a local ITypeInfo to a managed union
    /// </summary>
    internal class ConvUnionLocal : ConvLocalBase, IConvUnion
    {
        public ConvUnionLocal(ConverterInfo info, TypeInfo type)
            : base(info, type, ConvLocalFlags.DealWithAlias)
        {
        }

        public override ConvType ConvType => ConvType.Union;

        protected override void OnDefineType()
        {
            TypeInfo typeInfo = RefNonAliasedTypeInfo;

            this.typeBuilder = ConvCommon.DefineTypeHelper(
                this.convInfo,
                RefTypeInfo,
                RefNonAliasedTypeInfo,
                TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.ExplicitLayout,
                typeof(ValueType),
                ConvType.Union
                );

            // Handle [TypeLibType(...)] if evaluate to non-0
            TypeAttr refTypeAttr = RefTypeInfo.GetTypeAttr();
            if (refTypeAttr.TypeFlags != 0)
            {
                this.typeBuilder.SetCustomAttribute(CustomAttributeHelper.GetBuilderFor<TypeLibTypeAttribute>((TypeLibTypeFlags)refTypeAttr.TypeFlags));
            }

            this.convInfo.AddToSymbolTable(RefTypeInfo, ConvType.Union, this);
            this.convInfo.RegisterType(this.typeBuilder, this);
        }

        protected override Type OnCreate()
        {
            Debug.Assert(this.type == null);
            string name = ManagedName;

            TypeInfo typeInfo = RefNonAliasedTypeInfo;
            TypeAttr typeAttr = typeInfo.GetTypeAttr();

            // Create fields
            int cVars = typeAttr.DataFieldCount;
            bool isConversionLoss = false;

            for (int i = 0; i < cVars; ++i)
            {
                VarDesc var = typeInfo.GetVarDesc(i);
                CreateField(typeInfo, typeAttr, var, ref isConversionLoss);
            }

            // Emit StructLayout(LayoutKind.Sequential, Pack=cbAlignment)
            this.typeBuilder.SetCustomAttribute(CustomAttributeHelper.GetBuilderForStructLayout(LayoutKind.Explicit, typeAttr.Alignment, typeAttr.SizeInstanceInBytes));

            // Emit ComConversionLossAttribute if necessary
            if (isConversionLoss)
            {
                this.typeBuilder.SetCustomAttribute(CustomAttributeHelper.GetBuilderFor<ComConversionLossAttribute>());
            }

            // Emit SerializableAttribute for /transform:serializablevalueclasses
            if (this.convInfo.Settings.Flags.HasFlag(TypeLibImporterFlags.SerializableValueClasses))
            {
                this.typeBuilder.SetCustomAttribute(CustomAttributeHelper.GetBuilderFor<SerializableAttribute>());
            }

            return this.typeBuilder.CreateType();
        }

        private void CreateField(TypeInfo type, TypeAttr attr, VarDesc var, ref bool isConversionLoss)
        {
            if (IsObjectType(type, var.ElemDescVar.TypeDesc))
            {
                isConversionLoss = true;
            }

            TypeConverter typeConverter = new TypeConverter(this.convInfo, type, var.ElemDescVar.TypeDesc, ConversionType.Field);
            Type fieldType = typeConverter.ConvertedType;

            string fieldName = type.GetDocumentation(var.MemberId);

            // Generates the [FieldOffset(0)] layout declarations that approximate unions in managed code
            FieldBuilder field = this.typeBuilder.DefineField(fieldName, fieldType, FieldAttributes.Public);
            field.SetCustomAttribute(CustomAttributeHelper.GetBuilderForFieldOffset(0));
            typeConverter.ApplyAttributes(field);

            isConversionLoss |= typeConverter.IsConversionLoss;

            // Emit ComConversionLossAttribute for fields if necessary
            if (typeConverter.IsConversionLoss)
            {
                field.SetCustomAttribute(CustomAttributeHelper.GetBuilderFor<ComConversionLossAttribute>());

                // Emit Wrn_UnconvertableField warning
                this.convInfo.ReportEvent(
                    WarningCode.Wrn_UnconvertableField,
                    Resource.FormatString("Wrn_UnconvertableField", this.typeBuilder.FullName, fieldName));
            }
        }

        /// <summary>
        /// Test whether the specified VT_RECORD contains any field that can be converted to a managed reference type
        /// </summary>
        private static bool HasObjectFields(TypeInfo typeInfo)
        {
            TypeAttr typeAttr = typeInfo.GetTypeAttr();
            for (int i = 0; i < typeAttr.DataFieldCount; ++i)
            {
                VarDesc var = typeInfo.GetVarDesc(i);
                if (IsObjectType(typeInfo, var.ElemDescVar.TypeDesc))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Test whether the typeDesc corresponds to a managed reference type
        /// </summary>
        private static bool IsObjectType(TypeInfo typeInfo, TypeDesc typeDesc)
        {
            int nativeIndirection = 0;
            int vt = typeDesc.VarType;

            // Strip off leading VT_PTR and VT_BYREF
            while (vt == (int)VarEnum.VT_PTR)
            {
                typeDesc = typeDesc.InnerTypeDesc;
                vt = typeDesc.VarType;
                nativeIndirection++;
            }

            if ((vt & (int)VarEnum.VT_BYREF) != 0)
            {
                vt &= ~(int)VarEnum.VT_BYREF;
                nativeIndirection++;
            }

            // Determine if the field is/has object type.
            Debug.Assert(vt != (int)VarEnum.VT_PTR);

            switch ((VarEnum)vt)
            {
                // These are object types.
                case VarEnum.VT_BSTR:
                case VarEnum.VT_DISPATCH:
                case VarEnum.VT_VARIANT:
                case VarEnum.VT_UNKNOWN:
                case VarEnum.VT_SAFEARRAY:
                case VarEnum.VT_LPSTR:
                case VarEnum.VT_LPWSTR:
                    return true;

                // A user-defined may or may not be/contain Object type.
                case VarEnum.VT_USERDEFINED:
                    // User defined type.  Get the TypeInfo.
                    TypeInfo refTypeInfo = typeInfo.GetRefTypeInfo(typeDesc.HRefType);
                    TypeAttr refTypeAttr = refTypeInfo.GetTypeAttr();

                    // Some user defined class.  Is it a value class, or a VOS class?
                    switch (refTypeAttr.Typekind)
                    {
                        // Alias -- Is the aliased thing an Object type?
                        case TYPEKIND.TKIND_ALIAS:
                            return IsObjectType(refTypeInfo, refTypeAttr.TypeDescAlias);

                        // Record/Enum/Union -- Does it contain an Object type?
                        case TYPEKIND.TKIND_RECORD:
                        case TYPEKIND.TKIND_ENUM:
                        case TYPEKIND.TKIND_UNION:
                            // Byref/Ptrto record is Object.  Contained record might be.
                            if (nativeIndirection > 0)
                            {
                                return true;
                            }
                            else
                            {
                                return HasObjectFields(refTypeInfo);
                            }

                        // Class/Interface -- An Object Type.
                        case TYPEKIND.TKIND_INTERFACE:
                        case TYPEKIND.TKIND_DISPATCH:
                        case TYPEKIND.TKIND_COCLASS:
                            return true;

                        default:
                            return true;

                    }

                case VarEnum.VT_CY:
                case VarEnum.VT_DATE:
                case VarEnum.VT_DECIMAL:
                    // Pointer to the value type if an object. Contained one isn't.
                    if (nativeIndirection > 0)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }

                // A fixed array is an Object type.
                case VarEnum.VT_CARRAY:
                    return true;

                // Other types I4, etc., are not Object types.
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Represents a managed union that is already created
    /// </summary>
    internal class ConvUnionExternal : IConvUnion
    {
        private readonly TypeInfo typeInfo;

        public ConvUnionExternal(ConverterInfo info, TypeInfo typeInfo, Type managedType)
        {
            this.typeInfo = typeInfo;
            this.ManagedType = managedType;

            info.AddToSymbolTable(typeInfo, ConvType.Union, this);
            info.RegisterType(managedType, this);
        }

        public ConvType ConvType => ConvType.Union;

        public ConvScope ConvScope => ConvScope.External;

        public TypeInfo RefTypeInfo => this.typeInfo;

        public TypeInfo RefNonAliasedTypeInfo => this.RefTypeInfo;

        public Type ManagedType { get; private set; }

        public Type RealManagedType => this.RealManagedType;

        public string ManagedName => this.RealManagedType.FullName;

        public void Create()
        {
            // Do nothing as the type is already created
        }
    }
}
