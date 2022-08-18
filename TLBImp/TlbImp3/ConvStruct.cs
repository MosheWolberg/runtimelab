// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

using System;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

using TypeLibUtilities.TypeLibAPI;

namespace TypeLibUtilities
{
    /// <summary>
    /// Interface for struct conversion
    /// </summary>
    internal interface IConvStruct : IConvBase
    {
    }

    /// <summary>
    /// Conversion from a local ITypeInfo to a managed struct
    /// </summary>
    internal class ConvStructLocal : ConvLocalBase, IConvStruct
    {
        public ConvStructLocal(ConverterInfo info, TypeInfo type)
            : base(info, type, ConvLocalFlags.DealWithAlias)
        {
        }

        public override ConvType ConvType => ConvType.Struct;

        protected override void OnDefineType()
        {
            TypeInfo typeInfo = this.RefNonAliasedTypeInfo;

            this.typeBuilder = ConvCommon.DefineTypeHelper(
                this.convInfo,
                RefTypeInfo,
                RefNonAliasedTypeInfo,
                TypeAttributes.Public | TypeAttributes.BeforeFieldInit | TypeAttributes.SequentialLayout | TypeAttributes.Sealed,
                typeof(ValueType),
                ConvType.Struct);

            // Handle [TypeLibType(...)] if evaluate to non-0
            TypeAttr refTypeAttr = RefTypeInfo.GetTypeAttr();
            if (refTypeAttr.TypeFlags != 0)
            {
                this.typeBuilder.SetCustomAttribute(CustomAttributeHelper.GetBuilderFor<TypeLibTypeAttribute>((TypeLibTypeFlags)refTypeAttr.TypeFlags));
            }

            this.convInfo.AddToSymbolTable(RefTypeInfo, ConvType.Struct, this);
            this.convInfo.RegisterType(this.typeBuilder, this);
        }

        protected override Type OnCreate()
        {
            Debug.Assert(this.type == null);

            TypeInfo typeInfo = RefNonAliasedTypeInfo;
            TypeAttr typeAttr = typeInfo.GetTypeAttr();

            // Create fields
            bool isConversionLoss = false;
            int cVars = typeAttr.DataFieldCount;
            for (int n = 0; n < cVars; ++n)
            {
                VarDesc var = typeInfo.GetVarDesc(n);
                CreateField(typeInfo, typeAttr, var, ref isConversionLoss);
            }

            // Emit StructLayout(LayoutKind.Sequential, Pack=cbAlignment)
            this.typeBuilder.SetCustomAttribute(CustomAttributeHelper.GetBuilderForStructLayout(LayoutKind.Sequential, typeAttr.Alignment));

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
            TypeDesc fieldTypeDesc = var.ElemDescVar.TypeDesc;
            TypeConverter typeConverter = new TypeConverter(this.convInfo, type, fieldTypeDesc, ConversionType.Field);
            Type fieldType = typeConverter.ConvertedType;
            string fieldName = type.GetDocumentation(var.MemberId);
            FieldBuilder field = this.typeBuilder.DefineField(fieldName, fieldType, FieldAttributes.Public);
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

            // Emit TypeLibVarAttribute if necessary
            if (var.VarFlags != 0)
            {
                field.SetCustomAttribute(CustomAttributeHelper.GetBuilderFor<TypeLibVarAttribute>((TypeLibVarFlags)var.VarFlags));
            }
        }
    }

    /// <summary>
    /// Represents a external managed struct that is already created
    /// </summary>
    internal class ConvStructExternal : IConvStruct
    {
        private readonly TypeInfo typeInfo;

        public ConvStructExternal(ConverterInfo info, TypeInfo typeInfo, Type managedType)
        {
            this.typeInfo = typeInfo;
            this.ManagedType = managedType;

            info.AddToSymbolTable(typeInfo, ConvType.Struct, this);
            info.RegisterType(managedType, this);
        }

        public ConvType ConvType => ConvType.Struct;

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
