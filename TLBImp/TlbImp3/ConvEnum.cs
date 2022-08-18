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
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using TypeLibUtilities.TypeLibAPI;

namespace TypeLibUtilities
{
    /// <summary>
    /// Interface for enum conversion
    /// </summary>
    internal interface IConvEnum : IConvBase
    {
    }

    /// <summary>
    /// Conversion from a local ITypeInfo for enum to a managed enum
    /// </summary>
    internal class ConvEnumLocal : ConvLocalBase, IConvEnum
    {
        public ConvEnumLocal(ConverterInfo info, TypeInfo type)
            : base(info, type, ConvLocalFlags.DealWithAlias)
        {
        }

        public override ConvType ConvType => ConvType.Enum;

        protected override void OnDefineType()
        {
            TypeInfo typeInfo = RefNonAliasedTypeInfo;

            // Creates the enum type
            this.typeBuilder = ConvCommon.DefineTypeHelper(
                this.convInfo, 
                RefTypeInfo,
                RefNonAliasedTypeInfo,
                TypeAttributes.Public | TypeAttributes.Sealed,
                typeof(Enum),
                ConvType.Enum);

            // The field must be created here so that TypeBuilder can calculate a hash...
            // Need to create a special field to hold the enum data
            FieldBuilder field = this.typeBuilder.DefineField(
                "value__",
                typeof(int),
                FieldAttributes.Public | FieldAttributes.SpecialName);

            // Handle [TypeLibType(...)] if evaluate to non-0
            TypeAttr refTypeAttr = RefTypeInfo.GetTypeAttr();
            if (refTypeAttr.TypeFlags != 0)
            {
                this.typeBuilder.SetCustomAttribute(CustomAttributeHelper.GetBuilderFor<TypeLibTypeAttribute>((TypeLibTypeFlags)refTypeAttr.TypeFlags));
            }

            this.convInfo.AddToSymbolTable(RefTypeInfo, ConvType.Enum, this);
            this.convInfo.RegisterType(this.typeBuilder, this);
        }

        /// <summary>
        /// Creates the enum
        /// </summary>
        protected override Type OnCreate()
        {
            Debug.Assert(this.type == null);

            // Create constant fields for the enum
            ConvCommon.CreateConstantFields(this.convInfo, this.RefNonAliasedTypeInfo, this.typeBuilder, ConvType.Enum);

            return this.typeBuilder.CreateType();
        }
    }

    /// <summary>
    /// Conversion from a external ITypeInfo for enum to a managed enum
    /// </summary>
    internal class ConvEnumExternal : IConvEnum
    {
        private TypeInfo typeInfo;

        public ConvEnumExternal(ConverterInfo info, TypeInfo typeInfo, Type managedType)
        {
            this.typeInfo = typeInfo;
            this.ManagedType = managedType;

            info.AddToSymbolTable(typeInfo, ConvType.Enum, this);
            info.RegisterType(managedType, this);
        }

        public ConvType ConvType
        {
            get { return ConvType.Enum; }
        }

        public ConvScope ConvScope => ConvScope.External;

        public void Create()
        {
            // Do nothing as the type is already created
        }

        public TypeInfo RefTypeInfo => this.typeInfo;

        public TypeInfo RefNonAliasedTypeInfo => this.RefTypeInfo;

        public Type ManagedType { get; private set; }

        public Type RealManagedType => this.ManagedType;

        public string ManagedName => this.ManagedType.FullName;
    }
}
