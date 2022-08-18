// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

using System;
using System.Text;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Diagnostics;
using TypeLibUtilities.TypeLibAPI;

namespace TypeLibUtilities
{
    /// <summary>
    /// What is the category of the type that needs to be converted?
    /// </summary>
    internal enum ConversionType
    {
        VarArgParameter,    // Type is a vararg parameter
        Parameter,          // Type is a parameter
        ParamRetVal,        // Type is a paramter as [out, retval]
        Field,              // Type is a field
        ReturnValue,        // Type is a return value (function or property)
        Element,            // Type is a element
    }

    /// <summary>
    /// Convert unmanaged type (VT_XXX) to managed type
    /// The conversion is done at the time when you creates this object. After that this object is available
    /// for you to write custom attributes to the parameter and get the converted type
    /// </summary>
    internal class TypeConverter
    {
        private readonly ConverterInfo convInfo;
        private readonly TypeInfo typeInfo;
        private readonly ConversionType conversionType;
        private readonly ParamDesc paramDesc;
        private readonly bool convertingNewEnumMember = false;

        private TypeDesc typeDesc;
        private CustomAttributeBuilder marshalAttr;
        private int nativeIndirectionCount = 0;

        // Indicate if native indirection count has been adjusted for ConversionType.ParamRetVal
        private bool paramRetValIsHandled = false;

        /// <summary>
        /// Wrapper for a already converted type
        /// </summary>
        public TypeConverter(Type type)
        {
            this.ConvertedType = type;
            ResetUnmanagedTypeAndMarshalAttr();
        }

        public TypeConverter(ConverterInfo info, TypeInfo type, TypeDesc desc, ConversionType conversionType)
            : this(info, type, desc, conversionType, false, null)
        {
        }

        public TypeConverter(ConverterInfo info, TypeInfo type, TypeDesc desc, ConversionType conversionType, bool convertingNewEnumMember)
            : this(info, type, desc, conversionType, convertingNewEnumMember, null)
        {
        }

        public TypeConverter(ConverterInfo info, TypeInfo type, TypeDesc desc, ConversionType conversionType, bool convertingNewEnumMember, ParamDesc paramDesc)
        {
            this.convInfo = info;
            this.typeInfo = type;
            this.typeDesc = desc;
            this.paramDesc = paramDesc;
            this.conversionType = conversionType;

            this.convertingNewEnumMember = convertingNewEnumMember;

            this.ResetUnmanagedTypeAndMarshalAttr();

            // Do the conversion
            this.Convert();
        }

        /// <summary>
        /// Apply the custom attribute to parameters
        /// </summary>
        public void ApplyAttributes(ParameterBuilder paramBuilder)
        {
            if (this.marshalAttr != null)
            {
                paramBuilder.SetCustomAttribute(this.marshalAttr);
            }

            ConvCommon.HandleAlias(this.convInfo, this.typeInfo, this.typeDesc, paramBuilder);
        }

        /// <summary>
        /// Apply the custom attribute to fields
        /// </summary>
        public void ApplyAttributes(FieldBuilder fieldBuilder)
        {
            if (this.marshalAttr != null)
            {
                fieldBuilder.SetCustomAttribute(this.marshalAttr);
            }

            if (this.typeInfo != null)
            {
                ConvCommon.HandleAlias(this.convInfo, this.typeInfo, this.typeDesc, fieldBuilder);
            }
        }

        /// <summary>
        /// Returns the converted type after the conversion is done
        /// </summary>
        public Type ConvertedType { get; private set; }

        /// <summary>
        /// Is some information lost during the conversion process?
        /// </summary>
        public bool IsConversionLoss { get; private set; }

        /// <summary>
        /// Whether we use default marshal. If true, you cannot use the public property UnmanagedType because it is invalid.
        /// </summary>
        public bool UseDefaultMarshal { get; private set; }

        /// <summary>
        /// The corresponding unmanaged type. Only can be used when UseDefaultMarshal is false.
        /// </summary>
        /// <remarks>
        /// Corresponding unmanaged type.
        /// Unfortunately we cannot get it from CustomAttributeBuilder
        /// </remarks>
        public UnmanagedType UnmanagedType { get; private set; }

        private void SetUnmanagedType(UnmanagedType unmanagedType)
        {
            Debug.Assert(this.UseDefaultMarshal);
            Debug.Assert(UnmanagedType == (UnmanagedType)(-1));

            this.UseDefaultMarshal = false;
            UnmanagedType = unmanagedType;
        }

        private void ResetUnmanagedTypeAndMarshalAttr()
        {
            this.marshalAttr = null;
            this.UseDefaultMarshal = true;
            this.UnmanagedType = (UnmanagedType)(-1);
        }

        private void Convert()
        {
            VarEnum vt = (VarEnum)this.typeDesc.VarType;

            // Strip out VT_PTR
            while (vt == VarEnum.VT_PTR)
            {
                Debug.Assert(this.typeDesc.InnerTypeDesc != null);
                this.typeDesc = this.typeDesc.InnerTypeDesc;
                vt = (VarEnum)this.typeDesc.VarType;
                this.nativeIndirectionCount++;
            }

            // Strip out VT_BYREF
            if ((vt & VarEnum.VT_BYREF) != 0)
            {
                vt &= ~VarEnum.VT_BYREF;
                this.nativeIndirectionCount++;
            }

            Debug.Assert(this.marshalAttr == null);

            //
            // Find the corresponding type and save it in result and store the custom attribute in this.marshalAttr
            //
            Type result;
            switch (vt)
            {
                case VarEnum.VT_VOID:
                    result = typeof(void);
                    break;

                case VarEnum.VT_UI1:
                    result = typeof(byte);
                    break;

                case VarEnum.VT_UI2:
                    result = typeof(ushort);
                    break;

                case VarEnum.VT_UI4:
                case VarEnum.VT_UINT:
                    result = typeof(uint);
                    break;

                case VarEnum.VT_UI8:
                    result = typeof(ulong);
                    break;

                case VarEnum.VT_I1:
                    result = typeof(sbyte);
                    break;

                case VarEnum.VT_I2:
                    result = typeof(short);
                    break;

                case VarEnum.VT_I4:
                case VarEnum.VT_INT:
                    result = typeof(int);
                    break;

                case VarEnum.VT_I8:
                    result = typeof(long);
                    break;

                case VarEnum.VT_R4:
                    result = typeof(float);
                    break;

                case VarEnum.VT_R8:
                    result = typeof(double);
                    break;

                case VarEnum.VT_ERROR:
                case VarEnum.VT_HRESULT:
                    result = typeof(int);
                    this.marshalAttr = CustomAttributeHelper.GetBuilderFor<MarshalAsAttribute>(UnmanagedType.Error);
                    SetUnmanagedType(UnmanagedType.Error);
                    break;

                case VarEnum.VT_DISPATCH:
                    result = typeof(object);
                    if (this.convertingNewEnumMember)
                    {
                        // When we are creating a new enum member, convert IDispatch to IEnumVariant
                        TryUseCustomMarshaler(WellKnownGuids.IID_IEnumVARIANT, out result);
                    }
                    else
                    {
                        this.marshalAttr = CustomAttributeHelper.GetBuilderFor<MarshalAsAttribute>(UnmanagedType.IDispatch);
                        SetUnmanagedType(UnmanagedType.IDispatch);
                    }

                    // VT_DISPATCH => IDispatch *
                    this.nativeIndirectionCount++;

                    break;

                case VarEnum.VT_UNKNOWN:
                    result = typeof(object);
                    if (this.convertingNewEnumMember)
                    {
                        // When we are creating a new enum member, convert IUnknown to IEnumVariant
                        TryUseCustomMarshaler(WellKnownGuids.IID_IEnumVARIANT, out result);
                    }
                    else
                    {
                        this.marshalAttr = CustomAttributeHelper.GetBuilderFor<MarshalAsAttribute>(UnmanagedType.IUnknown);
                        SetUnmanagedType(UnmanagedType.IUnknown);
                    }

                    // VT_UNKNOWN => IUnknown *
                    this.nativeIndirectionCount++;

                    break;

                case VarEnum.VT_LPSTR:
                    result = typeof(string);
                    this.marshalAttr = CustomAttributeHelper.GetBuilderFor<MarshalAsAttribute>(UnmanagedType.LPStr);
                    SetUnmanagedType(UnmanagedType.LPStr);
                    this.nativeIndirectionCount++;
                    break;

                case VarEnum.VT_LPWSTR:
                    result = typeof(string);
                    this.marshalAttr = CustomAttributeHelper.GetBuilderFor<MarshalAsAttribute>(UnmanagedType.LPWStr);
                    SetUnmanagedType(UnmanagedType.LPWStr);
                    this.nativeIndirectionCount++;
                    break;

                case VarEnum.VT_BSTR:
                    result = typeof(string);
                    this.marshalAttr = CustomAttributeHelper.GetBuilderFor<MarshalAsAttribute>(UnmanagedType.BStr);
                    SetUnmanagedType(UnmanagedType.BStr);

                    // BSTR => string is special as BSTR are actually OLECHAR*, so add one indirection
                    this.nativeIndirectionCount++;
                    break;

                case VarEnum.VT_SAFEARRAY:
                    {
                        TypeDesc arrayDesc = this.typeDesc.InnerArrayDesc.TypeDescElement;
                        VarEnum arrayVt = (VarEnum)arrayDesc.VarType;
                        Type userDefinedType = null;
                       
                        TypeConverter elemTypeConverter = new TypeConverter(this.convInfo, this.typeInfo, arrayDesc, ConversionType.Element);
                        Type elemType = elemTypeConverter.ConvertedType;

                        // Determine the right VT for MarshalAs attribute
                        bool pointerArray = false;
                        if (arrayVt == VarEnum.VT_PTR)
                        {
                            arrayDesc = arrayDesc.InnerTypeDesc;
                            arrayVt = (VarEnum)arrayDesc.VarType;
                            pointerArray = true;

                            // We don't support marshalling pointers in array except UserType* & void*
                            if (arrayVt != VarEnum.VT_USERDEFINED
                                && arrayVt != VarEnum.VT_VOID)
                            {
                                arrayVt = VarEnum.VT_INT;
                                this.IsConversionLoss = true;
                            }
                        }

                        // Emit UserDefinedSubType if necessary
                        if (arrayVt == VarEnum.VT_USERDEFINED)
                        {
                            if (elemType.IsEnum)
                            {
                                if (pointerArray)
                                {
                                    arrayVt = VarEnum.VT_INT;
                                    this.IsConversionLoss = true;
                                }
                                else
                                {
                                    // For enums, using VT_RECORD is better than VT_I4. Within the
                                    // runtime, if you specify VT_I4 for enums in SafeArray, we
                                    // treat it the same way as VT_RECORD Reflection API also
                                    // accepts VT_RECORD instead of VT_I4.
                                    arrayVt = VarEnum.VT_RECORD;
                                }
                            }
                            else if(elemType.IsValueType)
                            {
                                if (pointerArray)
                                {
                                    arrayVt = VarEnum.VT_INT;
                                    this.IsConversionLoss = true;
                                }
                                else
                                {
                                    arrayVt = VarEnum.VT_RECORD;
                                }
                            }
                            else if (elemType.IsInterface)
                            {
                                if (pointerArray)
                                {
                                    // decide VT_UNKNOWN / VT_DISPATCH
                                    if (this.convInfo.TypeSupportsDispatch(elemType))
                                    {
                                        arrayVt = VarEnum.VT_DISPATCH;
                                    }
                                    else
                                    {
                                        arrayVt = VarEnum.VT_UNKNOWN;
                                    }
                                }
                                else
                                {
                                    arrayVt = VarEnum.VT_INT;
                                    this.IsConversionLoss = true;
                                }
                            }
                            else if (elemType == typeof(object)
                                && !elemTypeConverter.UseDefaultMarshal
                                && elemTypeConverter.UnmanagedType == UnmanagedType.IUnknown)
                            {
                                // Special case for object that doesn't have default interface and will be marshalled as IUnknown
                                arrayVt = VarEnum.VT_UNKNOWN;
                            }

                            userDefinedType = elemType;
                        }

                        // Merge computed conversion loss with element type converter.
                        this.IsConversionLoss |= elemTypeConverter.IsConversionLoss;

                        // Transform to System.Array if /sysarray is set and not vararg
                        if (this.convInfo.Settings.Flags.HasFlag(TypeLibImporterFlags.SafeArrayAsSystemArray)
                            && this.conversionType != ConversionType.VarArgParameter)
                        {
                            result = typeof(Array);
                        }
                        else
                        {
                            result = elemType.MakeArrayType();

                            // Don't need SafeArrayUserDefinedSubType for non System.Array case
                            userDefinedType = null;
                        }

                        // Desktop TlbImp doesn't have this check for vt == VT_RECORD/VT_UNKNOWN/VT_DISPATCH therefore
                        // it will emit SafeArrayUserDefinedSubType even it is not necessary/not valid.
                        // CoreCLR TlbImp will take this into account
                        if (userDefinedType != null
                            && (arrayVt == VarEnum.VT_RECORD || arrayVt == VarEnum.VT_UNKNOWN || arrayVt == VarEnum.VT_DISPATCH))
                        {
                            // The name of the type will be full name
                            this.marshalAttr = CustomAttributeHelper.GetBuilderForMarshalAsSafeArrayAndUserDefinedSubType(arrayVt, userDefinedType);
                        }
                        else
                        {
                            // Use I4 for enums when SafeArrayUserDefinedSubType is not specified
                            if (elemType.IsEnum && arrayVt == VarEnum.VT_RECORD)
                            {
                                arrayVt = VarEnum.VT_I4;
                            }

                            this.marshalAttr = CustomAttributeHelper.GetBuilderForMarshalAsSafeArray(arrayVt);
                        }

                        SetUnmanagedType(UnmanagedType.SafeArray);

                        // SafeArray <=> array is special because SafeArray is similar to Element*
                        this.nativeIndirectionCount++;
                        break;
                    }

                case VarEnum.VT_RECORD:
                case VarEnum.VT_USERDEFINED:
                    {
                        // Handle structs, interfaces, enums, and unions

                        // Support for aliasing
                        TypeInfo realType;
                        TypeAttr realAttr;
                        ConvCommon.ResolveAlias(this.typeInfo, this.typeDesc, out realType, out realAttr);

                        // Alias for a built-in type?
                        if (realAttr.Typekind == TYPEKIND.TKIND_ALIAS)
                        {
                            // Recurse to convert the built-in type
                            TypeConverter builtinType = new TypeConverter(this.convInfo, realType, realAttr.TypeDescAlias, this.conversionType);
                            result = builtinType.ConvertedType;
                            this.marshalAttr = builtinType.marshalAttr;
                            this.paramRetValIsHandled = builtinType.paramRetValIsHandled;
                        }
                        else
                        {
                            // Otherwise, we must have a non-aliased type, and it is a user defined type
                            // We should use the TypeInfo that this TypeDesc refers to
                            realType = this.typeDesc.GetUserDefinedTypeInfo(this.typeInfo);
                            TYPEKIND typeKind = realAttr.Typekind;

                            realAttr = realType.GetTypeAttr();
                            TypeLib typeLib = realType.GetContainingTypeLib();

                            // Convert StdOle2.Guid to System.Guid
                            if (realType.IsStdOleGuid())
                            {
                                result = typeof(Guid);
                                ResetUnmanagedTypeAndMarshalAttr();
                            }
                            else if (realAttr.Guid == WellKnownGuids.IID_IUnknown)
                            {
                                // Occasional goto makes sense
                                // If the VT_USERDEFINED is actually a VT_UNKNOWN => IUnknown *,
                                // we need to decrease the indirection count to compensate for
                                // the increment in VT_UNKNOWN.
                                this.nativeIndirectionCount--;
                                goto case VarEnum.VT_UNKNOWN;
                            }
                            else if (realAttr.Guid == WellKnownGuids.IID_IDispatch)
                            {
                                // Occasional goto makes sense
                                // See the IID_IUnknown case for decrement
                                this.nativeIndirectionCount--;
                                goto case VarEnum.VT_DISPATCH;
                            }
                            else
                            {
                                // Need to use CustomMarshaler?
                                Type customMarshalerResultType;
                                if (TryUseCustomMarshaler(realAttr.Guid, out customMarshalerResultType))
                                {
                                    result = customMarshalerResultType;
                                }
                                else
                                {
                                    IConvBase ret = this.convInfo.GetTypeRef(ConvCommon.TypeKindToConvType(typeKind), realType);
                                    if (this.conversionType == ConversionType.Field)
                                    {
                                        // Too bad. Reflection API requires that the field type must be created before creating
                                        // the struct/union type

                                        // Only process indirection = 0 case because > 1 case will be converted to IntPtr
                                        // Otherwise it will leads to a infinite recursion, if you consider the following scenario:
                                        // struct A
                                        // {
                                        //      struct B
                                        //      {
                                        //          struct A *a;
                                        //      } b;
                                        // }
                                        if (ret is ConvUnionLocal && this.nativeIndirectionCount == 0)
                                        {
                                            ConvUnionLocal convUnion = ret as ConvUnionLocal;
                                            convUnion.Create();
                                        }
                                        else if (ret is ConvStructLocal && this.nativeIndirectionCount == 0)
                                        {
                                            ConvStructLocal convStruct = ret as ConvStructLocal;
                                            convStruct.Create();
                                        }
                                        else if (ret is ConvEnumLocal && this.nativeIndirectionCount == 0)
                                        {
                                            ConvEnumLocal convEnum = ret as ConvEnumLocal;
                                            convEnum.Create();
                                        }
                                    }

                                    result = ret.ManagedType;

                                    // Don't reply on result.IsInterface as we have some weird scenarios like refering to a exported type lib
                                    // which has interfaces that are class interfaces and have the same name as a class.
                                    // For example, manage class M has a class interface _M, and their managed name are both M
                                    if (ret.ConvType == ConvType.Interface || ret.ConvType == ConvType.EventInterface || ret.ConvType == ConvType.ClassInterface)
                                    {
                                        this.marshalAttr = CustomAttributeHelper.GetBuilderFor<MarshalAsAttribute>(UnmanagedType.Interface);
                                        SetUnmanagedType(UnmanagedType.Interface);
                                    }

                                    if (ret.ConvType == ConvType.CoClass)
                                    {
                                        // We need to convert CoClass to default interface (could be converted to class interface if it is exclusive) in signatures
                                        Debug.Assert(ret is IConvCoClass);
                                        IConvCoClass convCoClass = ret as IConvCoClass;
                                        if (convCoClass.DefaultInterface != null)
                                        {
                                            // Use the default interface
                                            result = convCoClass.DefaultInterface.ManagedType;
                                            this.marshalAttr = CustomAttributeHelper.GetBuilderFor<MarshalAsAttribute>(UnmanagedType.Interface);
                                            SetUnmanagedType(UnmanagedType.Interface);
                                        }
                                        else
                                        {
                                            // The coclass has no default interface (source interface excluded)
                                            // Marshal it as IUnknown
                                            result = typeof(object);
                                            this.marshalAttr = CustomAttributeHelper.GetBuilderFor<MarshalAsAttribute>(UnmanagedType.IUnknown);
                                            SetUnmanagedType(UnmanagedType.IUnknown);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    break;

                case VarEnum.VT_VARIANT:
                    result = typeof(object);
                    this.marshalAttr = CustomAttributeHelper.GetBuilderFor<MarshalAsAttribute>(UnmanagedType.Struct);
                    SetUnmanagedType(UnmanagedType.Struct);

                    // object is special that it will be marshaled as VARIANT.
                    // Because we think object as having one indirection, now we are one indirection
                    // less so increment indirection count.
                    this.nativeIndirectionCount++;
                    break;

                case VarEnum.VT_CY:
                    result = typeof(decimal);
                    this.marshalAttr = CustomAttributeHelper.GetBuilderFor<MarshalAsAttribute>(UnmanagedType.Currency);
                    SetUnmanagedType(UnmanagedType.Currency);
                    break;

                case VarEnum.VT_DATE:
                    result = typeof(DateTime);
                    break;

                case VarEnum.VT_DECIMAL:
                    result = typeof(decimal);
                    break;

                case VarEnum.VT_CARRAY:
                    {
                        Debug.Assert(this.typeDesc.InnerTypeDesc != null);
                        TypeDesc elemTypeDesc = this.typeDesc.InnerTypeDesc;
                        TypeConverter elemTypeConverter = new TypeConverter(this.convInfo, this.typeInfo, elemTypeDesc, ConversionType.Element);
                        Type elemType = elemTypeConverter.ConvertedType;
                        result = elemType.MakeArrayType();

                        this.IsConversionLoss |= elemTypeConverter.IsConversionLoss;

                        uint elements = 1;
                        SAFEARRAYBOUND[] bounds = this.typeDesc.InnerArrayDesc.GetBounds();
                        foreach (SAFEARRAYBOUND bound in bounds)
                        {
                            elements *= bound.cElements;
                        }

                        // SizeConst can only hold Int32.MaxValue
                        if (elements <= int.MaxValue)
                        {
                            UnmanagedType arrayType = UnmanagedType.LPArray;
                            if (this.conversionType == ConversionType.Field)
                            {
                                arrayType = UnmanagedType.ByValArray;
                            }

                            // If the default marshaller isn't be used and the unmanaged type is a string or variant bool
                            // get a custom specific marshalas attribute.
                            if (!elemTypeConverter.UseDefaultMarshal
                                && (elemTypeConverter.UnmanagedType == UnmanagedType.BStr
                                    || elemTypeConverter.UnmanagedType == UnmanagedType.LPStr
                                    || elemTypeConverter.UnmanagedType == UnmanagedType.LPWStr
                                    || elemTypeConverter.UnmanagedType == UnmanagedType.VariantBool))
                            {
                                this.marshalAttr = CustomAttributeHelper.GetBuilderForMarshalAsConstArray(arrayType, (int)elements, elemTypeConverter.UnmanagedType);
                            }
                            else
                            {
                                this.marshalAttr = CustomAttributeHelper.GetBuilderForMarshalAsConstArray(arrayType, (int)elements);
                            }

                            SetUnmanagedType(arrayType);
                        }
                        else
                        {
                            this.nativeIndirectionCount = 0;
                            result = typeof(IntPtr);
                            ResetUnmanagedTypeAndMarshalAttr();
                            this.IsConversionLoss = true;
                        }
                    }
                    break;

                case VarEnum.VT_BOOL:
                    if (this.conversionType == ConversionType.Field)
                    {
                        // For VT_BOOL in fields, use 'bool' if user requested; otherwise,
                        // use 'short' for back compatibility.
                        result = typeof(short);
                        if (this.convInfo.Settings.IsConvertVariantBoolFieldToBool)
                        {
                            result = typeof(bool);
                            this.marshalAttr = CustomAttributeHelper.GetBuilderFor<MarshalAsAttribute>(UnmanagedType.VariantBool);
                            SetUnmanagedType(UnmanagedType.VariantBool);
                        }
                    }
                    else
                    {
                        result = typeof(bool);
                        SetUnmanagedType(UnmanagedType.VariantBool);
                    }
                    break;

                case VarEnum.VT_PTR:
                    Debug.Assert(false, "Should not get here");
                    result = null;
                    break;

                default:
                    this.convInfo.ReportEvent(
                        WarningCode.Wrn_BadVtType,
                        Resource.FormatString("Wrn_BadVtType", (int)vt, this.typeInfo.GetDocumentation()));
                    result = typeof(IntPtr);
                    this.IsConversionLoss = true;
                    break;
            }

            // String -> StringBuilder special case
            if (result == typeof(string))
            {
                if (this.IsParamOut()
                    && this.nativeIndirectionCount == 1
                    && (this.conversionType == ConversionType.Parameter || this.conversionType == ConversionType.VarArgParameter))
                {
                    // [out] or [in, out] LPSTR/LPWSTR scenario
                    if (vt != VarEnum.VT_BSTR)
                    {
                        // String is immutable and cannot be [out]/[in, out]. Convert to StringBuilder
                        result = typeof(StringBuilder);
                    }
                    else // VT_BSTR
                    {
                        // VT_BSTR is also immutable. So conversion loss here
                        this.IsConversionLoss = true;
                        result = typeof(IntPtr);
                        this.nativeIndirectionCount = 0;
                        ResetUnmanagedTypeAndMarshalAttr();
                    }
                }
            }

            // Special rule for void* => IntPtr
            if (result == typeof(void))
            {
                result = typeof(IntPtr);
                switch (this.conversionType)
                {
                    case ConversionType.Element:
                    case ConversionType.Field:
                        this.nativeIndirectionCount = 0;
                        break;

                    default:
                        if (this.nativeIndirectionCount > 1)
                        {
                            this.nativeIndirectionCount = 1;
                        }
                        else
                        {
                            this.nativeIndirectionCount = 0;
                        }
                        break;
                }
            }

            // If the type is already a byref type, remove the byref and add extra indirection(s).
            // This is necessary to avoid trying to call MakeByRef on the byref type
            if (result.IsByRef)
            {
                result = result.GetElementType();
                if (result.IsValueType)
                {
                    // Value& = Value *
                    this.nativeIndirectionCount++;
                }
                else
                {
                    // RefType& = RefType**
                    this.nativeIndirectionCount += 2;
                }
            }

            // Process indirection
            if (this.nativeIndirectionCount > 0)
            {
                if (result.IsValueType)
                {
                    switch (this.conversionType)
                    {
                        case ConversionType.VarArgParameter:
                        case ConversionType.Parameter:
                            // Decimal/Guid can support extra level of indirection using LpStruct in parameters
                            // LpStruct has no effect in other places and for other types
                            // Only use LpStruct for scenarios like GUID **
                            // This is different from old TlbImp. Old TlbImp will use IntPtr
                            if ((result == typeof(decimal) || result == typeof(Guid))
                                && this.nativeIndirectionCount == 2)
                            {
                                this.nativeIndirectionCount--;
                                ResetUnmanagedTypeAndMarshalAttr();
                                this.marshalAttr = CustomAttributeHelper.GetBuilderFor<MarshalAsAttribute>(UnmanagedType.LPStruct);
                                SetUnmanagedType(UnmanagedType.LPStruct);
                            }

                            if (this.nativeIndirectionCount >= 2)
                            {
                                this.IsConversionLoss = true;
                                result = typeof(IntPtr);
                                ResetUnmanagedTypeAndMarshalAttr();
                            }
                            else if (this.nativeIndirectionCount > 0)
                            {
                                result = result.MakeByRefType();
                            }
                            break;

                        case ConversionType.Field:
                            this.IsConversionLoss = true;
                            result = typeof(IntPtr);
                            ResetUnmanagedTypeAndMarshalAttr();
                            break;

                        case ConversionType.ParamRetVal:
                            if (!this.paramRetValIsHandled)
                            {
                                this.nativeIndirectionCount--;
                                this.paramRetValIsHandled = true;
                            }
                            goto case ConversionType.ReturnValue;
                            // Fall through to ConversionType.ReturnValue

                        case ConversionType.ReturnValue:
                            if (this.nativeIndirectionCount >= 1)
                            {
                                this.IsConversionLoss = true;
                                result = typeof(IntPtr);
                                ResetUnmanagedTypeAndMarshalAttr();
                            }
                            break;

                        case ConversionType.Element:
                            this.IsConversionLoss = true;
                            result = typeof(IntPtr);
                            ResetUnmanagedTypeAndMarshalAttr();
                            break;
                    }
                }
                else
                {
                    switch (this.conversionType)
                    {
                        case ConversionType.Field:
                            // ** => IntPtr, ConversionLoss
                            if (this.nativeIndirectionCount > 1)
                            {
                                result = typeof(IntPtr);
                                this.IsConversionLoss = true;
                                ResetUnmanagedTypeAndMarshalAttr();
                            }
                            break;

                        case ConversionType.VarArgParameter:
                        case ConversionType.Parameter:
                            if (this.nativeIndirectionCount > 2)
                            {
                                result = typeof(IntPtr);
                                this.IsConversionLoss = true;
                                ResetUnmanagedTypeAndMarshalAttr();
                            }
                            else if (this.nativeIndirectionCount == 2)
                            {
                                result = result.MakeByRefType();
                            }
                            break;

                        case ConversionType.ParamRetVal:
                            if (!this.paramRetValIsHandled)
                            {
                                this.nativeIndirectionCount--;
                                this.paramRetValIsHandled = true;
                            }
                            goto case ConversionType.ReturnValue;
                            // Fall through to ConversionType.ReturnValue

                        case ConversionType.ReturnValue:
                            if (this.nativeIndirectionCount > 1)
                            {
                                result = typeof(IntPtr);
                                this.IsConversionLoss = true;
                                ResetUnmanagedTypeAndMarshalAttr();
                            }
                            break;

                        case ConversionType.Element:
                            if (this.nativeIndirectionCount > 1)
                            {
                                this.IsConversionLoss = true;
                                result = typeof(IntPtr);
                                ResetUnmanagedTypeAndMarshalAttr();
                            }
                            break;
                    }
                }
            }

            this.ConvertedType = result;
        }

        /// <summary>
        /// Is the parameter a [out]?
        /// </summary>
        private bool IsParamOut()
        {
            return this.paramDesc != null && this.paramDesc.IsOut;
        }

        /// <summary>
        /// Does the type need custom marshaler for marshalling?
        /// </summary>
        /// <returns>Whether we need to use custom marshaler</returns>
        private bool TryUseCustomMarshaler(Guid iid, out Type result)
        {
            result = null;

            // [TODO] Add back support for CustomMarshaler in CoreCLR
            throw new NotSupportedException();

            //if (iid == WellKnownGuids.IID_IDispatchEx)
            //{
            //    Type type = typeof(System.Runtime.InteropServices.CustomMarshalers.ExpandoToDispatchExMarshaler);
            //    this.marshalAttr = CustomAttributeHelper.GetBuilderForMarshalAsCustomMarshaler(type, "IExpando");
            //    SetUnmanagedType(UnmanagedType.CustomMarshaler);
            //    result = typeof(System.Runtime.InteropServices.Expando.IExpando);
            //    return true;
            //}
            //else if (iid == WellKnownGuids.IID_IEnumVARIANT)
            //{
            //    Type type = typeof(System.Runtime.InteropServices.CustomMarshalers.EnumeratorToEnumVariantMarshaler);
            //    this.marshalAttr = CustomAttributeHelper.GetBuilderForMarshalAsCustomMarshaler(type, null);
            //    SetUnmanagedType(UnmanagedType.CustomMarshaler);
            //    result = typeof(System.Collections.IEnumerator);
            //    return true;
            //}
            //else if (iid == WellKnownGuids.IID_ITypeInfo)
            //{
            //    Type type = typeof(System.Runtime.InteropServices.CustomMarshalers.TypeToTypeInfoMarshaler);
            //    this.marshalAttr = CustomAttributeHelper.GetBuilderForMarshalAsCustomMarshaler(type, null);
            //    SetUnmanagedType(UnmanagedType.CustomMarshaler);
            //    result = typeof(Type);
            //    return true;
            //}

            //return false;
        }
    }
}

