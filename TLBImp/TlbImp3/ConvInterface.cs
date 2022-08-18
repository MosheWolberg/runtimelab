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
    /// Conversion from a local ITypeInfo to a interface
    /// </summary>
    internal interface IConvInterface : IConvBase
    {
        /// <summary>
        /// Returns InterfaceMemberInfo collection
        /// Represents all the members for this interface used to create methods/properties
        /// </summary>
        IEnumerable<InterfaceMemberInfo> AllMembers { get; }

        /// <summary>
        /// Corresponding event interface
        /// </summary>
        IConvEventInterface EventInterface { get; }

        /// <summary>
        /// Whether this interface supports dispatch
        /// </summary>
        bool SupportsIDispatch { get; }

        /// <summary>
        /// Whether this interface implements IEnumerable
        /// </summary>
        bool ImplementsIEnumerable { get; }

        /// <summary>
        /// Get event interface instance for the current interface, if it is a source interface.
        /// However we cannot determine if the interface is a source interface or not, so we should make sure
        /// we don't call this function for non-source interfaces
        /// Note: after calling this function, the event interface is not created yet, you'll need to call IConvEventInterface->Create
        /// to create it
        /// </summary>
        IConvEventInterface DefineEventInterface();

        /// <summary>
        /// Associates this interface exclusively with a class interface for a particular coclass.
        /// This means this interface is only being exposed by one particular coclass
        /// </summary>
        void AssociateWithExclusiveClassInterface(IConvClassInterface convClassInterface);
    }

    /// <summary>
    /// Type of the interface member, either a method or a variable
    /// </summary>
    internal enum InterfaceMemberType
    {
        Method,     // This interface member is a method, either a regular method or a property method
        Variable    // This interface member is a variable, also called dispatch property
    }

    /// <summary>
    /// Member information for a certain interface. Inmutable, except for the name part
    /// </summary>
    internal class InterfaceMemberInfo
    {
        private readonly string basicName;          // Non-decorated name

        public InterfaceMemberInfo(ConverterInfo info, TypeInfo typeInfo, int index, string basicName, string originalName, InterfaceMemberType type, INVOKEKIND kind, int memId, FuncDesc funcDesc, VarDesc varDesc)
        {
            this.UniqueName = originalName;
            this.MemId = memId;
            this.RefFuncDesc = funcDesc;
            this.RefVarDesc = varDesc;
            this.basicName = basicName;
            this.RecommendedName = originalName;
            this.MemberType = type;
            this.Index = index;
            this.RefTypeInfo = typeInfo;
            this.InvokeKind = kind;

            // Support for Guid_DispIdOverrid
            int dispIdMaybe = memId;
            this.DispIdIsOverridden = ConvCommon.GetOverrideDispId(info, typeInfo, index, this.MemberType, ref dispIdMaybe, true);
            this.DispId = dispIdMaybe;
        }

        /// <summary>
        /// Recommended member name for this interface (not for coclass).
        /// </summary>
        /// <remarks>
        /// Recommended name, generated from basic name
        /// </remarks>
        public string RecommendedName { get; private set; }

        /// <summary>
        /// Unique member name for this interface. Will be updated when creating a interface. 
        /// </summary>
        public string UniqueName { get; set; }

        /// <summary>
        /// Valid FuncDesc if MemberType == Method, otherwise null
        /// </summary>
        /// <remarks>
        /// Corresponding FuncDesc, if this MemberInfo is a function (property)
        /// </remarks>
        public FuncDesc RefFuncDesc { get; private set; }

        /// <summary>
        /// Valid VarDesc if MemberType == Var, otherwise null
        /// </summary>
        /// <remarks>
        /// Corresponding VarDesc, if this MemberInfo is a dispatch property
        /// </remarks>
        public VarDesc RefVarDesc { get; private set; }

        /// <summary>
        /// A function or a variable
        /// </summary>
        public InterfaceMemberType MemberType { get; private set; }

        /// <summary>
        /// Whether the DispId has been overridden by GUID_DispIdOverride or not
        /// </summary>
        public bool DispIdIsOverridden { get; private set; }

        /// <summary>
        /// InvokeKind = Func/PropGet/PropPut/PropPutRef
        /// </summary>
        public INVOKEKIND InvokeKind { get; private set; }

        /// <summary>
        /// Member ID = Dispatch ID
        /// </summary>
        public int MemId { get; private set; }

        /// <summary>
        /// Dispatch ID. Could be override by GUID_DispIdOverride. Otherwise = MemId
        /// </summary>
        /// <remarks>
        /// Usually = MemberID unless explicitly overridden with GUID_DispIdOverride
        /// </remarks>
        public int DispId { get; private set; }

        public bool IsProperty => this.InvokeKind != INVOKEKIND.INVOKE_FUNC;

        public bool IsPropertyGet => this.InvokeKind.HasFlag(INVOKEKIND.INVOKE_PROPERTYGET);

        public bool IsPropertyPut => this.InvokeKind.HasFlag(INVOKEKIND.INVOKE_PROPERTYPUT);

        public bool IsPropertyPutRef => this.InvokeKind.HasFlag(INVOKEKIND.INVOKE_PROPERTYPUTREF);

        public InterfacePropertyInfo PropertyInfo { get; private set; }

        public TypeInfo RefTypeInfo { get; private set; }

        /// <summary>
        /// Index of the member info. Same as the index of the FuncDesc/VarDesc which is used to call certain type lib APIs
        /// such as GetFuncCustData/GetVarCustData
        /// </summary>
        public int Index { get; private set; }

        /// <summary>
        /// Builds a list of members according to TypeInfo information
        /// </summary>
        /// <param name="type">The TypeInfo used for generating the member list</param>
        public static List<InterfaceMemberInfo> BuildMemberList(ConverterInfo info, TypeInfo type, IConvInterface convInterface)
        {
            var propertyInvokeKinds = new Dictionary<string, INVOKEKIND>();

            TypeAttr attr = type.GetTypeAttr();

            //
            // 1. Walk through all the propput/propget/propref properties and collect information
            //
            for (int i = ConvCommon.GetIndexOfFirstMethod(type, attr); i < attr.FuncsCount; ++i)
            {
                FuncDesc funcDesc = type.GetFuncDesc(i);
                INVOKEKIND invKind = funcDesc.Invkind;
                if (invKind == INVOKEKIND.INVOKE_FUNC)
                {
                    continue;
                }

                string name = type.GetDocumentation(funcDesc.MemberId);
                INVOKEKIND invokeKind;
                if (!propertyInvokeKinds.TryGetValue(name, out invokeKind))
                {
                    invokeKind = (INVOKEKIND)0;
                }

                invokeKind |= invKind;
                propertyInvokeKinds[name] = invokeKind;
            }

            var allMembers = new List<InterfaceMemberInfo>();
            var allNameToMemberIds = new Dictionary<string, int>();
            var propertyList = new SortedDictionary<int, InterfacePropertyInfo>();

            //
            // 2. Walk through all vars (for disp interface) and generate name for get/set accessors
            // Fortunately we don't need to consider name collision here because variables are always first
            //
            for (int i = 0; i < attr.DataFieldCount; ++i)
            {
                VarDesc varDesc = type.GetVarDesc(i);

                int memId = varDesc.MemberId;
                string name = type.GetDocumentation(memId);
                string getFuncName;
                string setFuncName;

                bool isNewEnumMember = false;
                if (ConvCommon.IsNewEnumDispatchProperty(info, type, varDesc, i))
                {
                    name = getFuncName = setFuncName = "GetEnumerator";
                    isNewEnumMember = true;
                }
                else
                {
                    getFuncName = $"get_{name}";
                    setFuncName = $"set_{name}";
                }

                var memberInfo = new InterfaceMemberInfo(info, type, i, name, getFuncName, InterfaceMemberType.Variable, INVOKEKIND.INVOKE_PROPERTYGET, memId, null, varDesc);
                allNameToMemberIds.Add(memberInfo.RecommendedName, memId);
                allMembers.Add(memberInfo);
                SetPropertyInfo(propertyList, memberInfo, PropertyKind.VarProperty);

                if (!varDesc.IsReadOnly &&  // don't generate set_XXX if the var is read-only
                    !isNewEnumMember)       // don't generate set_XXX if the var is actually a new enum property
                {
                    memberInfo = new InterfaceMemberInfo(info, type, i, name, setFuncName, InterfaceMemberType.Variable, INVOKEKIND.INVOKE_PROPERTYPUT, memId, null, varDesc);
                    allNameToMemberIds.Add(memberInfo.RecommendedName, memId);
                    allMembers.Add(memberInfo);
                    if (memberInfo.IsProperty)
                        SetPropertyInfo(propertyList, memberInfo, PropertyKind.VarProperty);
                }
            }

            //
            // 3. Walk through all funcdesc and generate unique name
            //
            bool implementsIEnumerable = convInterface.ImplementsIEnumerable;
            bool allowNewEnum = !implementsIEnumerable;
            for (int i = ConvCommon.GetIndexOfFirstMethod(type, attr); i < attr.FuncsCount; ++i)
            {
                FuncDesc funcDesc = type.GetFuncDesc(i);
                int memId = funcDesc.MemberId;

                INVOKEKIND invKind = funcDesc.Invkind;

                bool explicitManagedNameUsed = false;
                bool isNewEnumMember = false;

                string basicName = type.GetDocumentation(memId);
                if (allowNewEnum && ConvCommon.IsNewEnumFunc(info, type, funcDesc, i))
                {
                    basicName = "GetEnumerator";
                    allowNewEnum = false;
                    isNewEnumMember = true;

                    // To prevent additional methods from implementing the NewEnum method
                    implementsIEnumerable = false;
                }
                else
                {
                    string managedName = type.GetFuncCustData<string>(i, CustomAttributeGuids.GUID_ManagedName);
                    if (managedName != null)
                    {
                        basicName = managedName;
                        explicitManagedNameUsed = true;
                    }
                }

                //
                // First, check whether GUID_Function2Getter is set
                //
                bool functionToGetter = false;
                if (type.GetFuncCustData<object>(i, CustomAttributeGuids.GUID_Function2Getter) != null)
                {
                    functionToGetter = true;
                }

                // secondly, check for the propget and propset custom attributes if this not already a property.
                if ((invKind & (INVOKEKIND.INVOKE_PROPERTYGET | INVOKEKIND.INVOKE_PROPERTYPUT | INVOKEKIND.INVOKE_PROPERTYPUTREF)) == 0)
                {
                    if (type.GetFuncCustData<object>(i, CustomAttributeGuids.GUID_PropGetCA) != null)
                    {
                        invKind = INVOKEKIND.INVOKE_PROPERTYGET;
                    }
                    else if (type.GetFuncCustData<object>(i, CustomAttributeGuids.GUID_PropPutCA) != null)
                    {
                        invKind = INVOKEKIND.INVOKE_PROPERTYPUT;
                    }
                }

                //
                // Generate the name and take the kind of property into account (get/set/let)
                //
                string name = null;
                if (!explicitManagedNameUsed && !isNewEnumMember && !functionToGetter)
                {
                    switch (invKind)
                    {
                        case INVOKEKIND.INVOKE_FUNC:
                            name = basicName;
                            break;
                        case INVOKEKIND.INVOKE_PROPERTYGET: // [propget]
                            name = $"get_{basicName}";
                            break;
                        case INVOKEKIND.INVOKE_PROPERTYPUT: // [propput]
                            if (!propertyInvokeKinds.ContainsKey(basicName))
                            {
                                propertyInvokeKinds.Add(basicName, (INVOKEKIND)0);
                            }

                            if ((propertyInvokeKinds[basicName] & INVOKEKIND.INVOKE_PROPERTYPUTREF) != 0)
                            {
                                name = $"let_{basicName}";
                            }
                            else
                            {
                                name = $"set_{basicName}";
                            }
                            break;
                        case INVOKEKIND.INVOKE_PROPERTYPUTREF:  // [propputref]
                            name = $"set_{basicName}";
                            break;
                    }
                }
                else
                {
                    // If explicit managed name is used, use the original name
                    invKind = INVOKEKIND.INVOKE_FUNC;
                    name = basicName;
                }

                //
                // Reset to original name if collision occurs and don't treat this as a property
                //
                if (allNameToMemberIds.ContainsKey(name))
                {
                    name = basicName;

                    // Force it to be a normal function to align with TlbImp behavior and reduce test cost
                    // This also makes sense because if we both have set_Prop & also a Prop marked with propput, something is wrong
                    invKind = INVOKEKIND.INVOKE_FUNC;
                }
                else
                {
                    allNameToMemberIds.Add(name, memId);
                }

                //
                // Create the memberinfo
                //
                InterfaceMemberInfo memberInfo =
                    new InterfaceMemberInfo(info, type, i, basicName, name, InterfaceMemberType.Method, invKind, memId, funcDesc, null);
                allMembers.Add(memberInfo);

                //
                // Fill in properties
                //
                if (memberInfo.IsProperty)
                    SetPropertyInfo(propertyList, memberInfo, PropertyKind.FunctionProperty);
            }

            //
            // 4. Walk through all the properties and determine property type
            //
            foreach (InterfacePropertyInfo funcPropInfo in propertyList.Values)
            {
                if (funcPropInfo.DeterminePropType(info, convInterface))
                {
                    continue;
                }

                //
                // Determining prop type failed - this is not a valid property
                //

                if (funcPropInfo.Get != null)
                {
                    InterfaceMemberInfo memInfo = funcPropInfo.Get;
                    // Keep the property information around so that we can emit a warning later
                    // memInfo.PropertyInfo = null;

                    memInfo.InvokeKind = INVOKEKIND.INVOKE_FUNC;
                    memInfo.RecommendedName = memInfo.UniqueName = memInfo.basicName;
                }

                if (funcPropInfo.Put != null)
                {
                    InterfaceMemberInfo memInfo = funcPropInfo.Put;
                    // Keep the property information around so that we can emit a warning later
                    // memInfo.PropertyInfo = null;
                    memInfo.InvokeKind = INVOKEKIND.INVOKE_FUNC;
                    memInfo.RecommendedName = memInfo.UniqueName = memInfo.basicName;
                }

                if (funcPropInfo.PutRef != null)
                {
                    InterfaceMemberInfo memInfo = funcPropInfo.PutRef;
                    // Keep the property information around so that we can emit a warning later
                    // memInfo.PropertyInfo = null;
                    memInfo.InvokeKind = INVOKEKIND.INVOKE_FUNC;
                    memInfo.RecommendedName = memInfo.UniqueName = memInfo.basicName;
                }
            }

            propertyList.Clear();

            //
            // 5. Sort member list according to v-table (for non-dispinterfaces)
            //
            if (!attr.IsIDispatch)
            {
                allMembers.Sort((InterfaceMemberInfo x, InterfaceMemberInfo y) =>
                {
                    Debug.Assert(x.RefFuncDesc != null);
                    Debug.Assert(y.RefFuncDesc != null);
                    return x.RefFuncDesc.VTableOffset - y.RefFuncDesc.VTableOffset;
                });
            }

            return allMembers;
        }

        private static void SetPropertyInfo(SortedDictionary<int, InterfacePropertyInfo> propertyList, InterfaceMemberInfo memberInfo, PropertyKind kind)
        {
            InterfacePropertyInfo funcPropInfo;
            if (!propertyList.ContainsKey(memberInfo.MemId))
                propertyList.Add(memberInfo.MemId, new InterfacePropertyInfo(memberInfo.RefTypeInfo, kind));

            funcPropInfo = propertyList[memberInfo.MemId];
            switch (memberInfo.InvokeKind)
            {
                case INVOKEKIND.INVOKE_PROPERTYGET:
                    funcPropInfo.Get = memberInfo;
                    memberInfo.PropertyInfo = funcPropInfo;
                    break;

                case INVOKEKIND.INVOKE_PROPERTYPUT:
                    funcPropInfo.Put = memberInfo;
                    memberInfo.PropertyInfo = funcPropInfo;
                    break;

                case INVOKEKIND.INVOKE_PROPERTYPUTREF:
                    funcPropInfo.PutRef = memberInfo;
                    memberInfo.PropertyInfo = funcPropInfo;
                    break;
            }
        }
    }

    /// <summary>
    /// Kind of the property, either function or variable
    /// </summary>
    internal enum PropertyKind { FunctionProperty, VarProperty }

    /// <summary>
    /// Represents a property (either from a function or a variable) in a interface
    /// </summary>
    internal class InterfacePropertyInfo
    {
        private readonly TypeInfo typeInfo;
        private readonly PropertyKind propertyKind;
        private ConversionType conversionType;

        public InterfacePropertyInfo(TypeInfo typeInfo, PropertyKind propKind)
        {
            this.typeInfo = typeInfo;
            this.propertyKind = propKind;
        }

        /// <summary>
        /// InterfaceMemberInfo for propget
        /// </summary>
        public InterfaceMemberInfo Get { get; set; }

        /// <summary>
        /// InterfaceMemberInfo for propput
        /// </summary>
        public InterfaceMemberInfo Put { get; set; }

        /// <summary>
        /// InterfaceMemberInfo for propputref
        /// </summary>
        public InterfaceMemberInfo PutRef { get; set; }

        /// <summary>
        /// The best InterfaceMemberInfo that can represent this property. Used to determine many properties of this property
        /// </summary>
        public InterfaceMemberInfo BestMemberInfo { get; private set; }

        /// <summary>
        /// RecommendedName of the property. Not unique
        /// </summary>
        public string RecommendedName
        {
            get { return this.typeInfo.GetDocumentation(MemId); }
        }

        /// <summary>
        /// MemId
        /// </summary>
        public int MemId => this.BestMemberInfo.MemId;

        /// <summary>
        /// Dispatch id. Could be overridden
        /// </summary>
        public int DispId => this.BestMemberInfo.DispId;

        /// <summary>
        /// The best FuncDesc that can represent this property. Used to determine the exact type of the property
        /// </summary>
        public FuncDesc BestFuncDesc
        {
            get
            {
                Debug.Assert(this.propertyKind == PropertyKind.FunctionProperty);
                return this.BestMemberInfo.RefFuncDesc;
            }
        }

        /// <summary>
        /// The best VarDesc that can represent this property. Used to determine the exact type of the property
        /// </summary>
        public VarDesc BestVarDesc
        {
            get
            {
                Debug.Assert(this.propertyKind == PropertyKind.VarProperty);
                return this.BestMemberInfo.RefVarDesc;
            }
        }

        /// <summary>
        /// Get the TypeDesc that represents the property type
        /// </summary>
        public TypeDesc PropertyTypeDesc { get; private set; }

        /// <summary>
        /// Kind of the property. Either function or variable
        /// </summary>
        public PropertyKind Kind => this.propertyKind;

        /// <summary>
        /// TypeInfo that this memberinfo belongs to
        /// </summary>
        public TypeInfo RefTypeInfo => this.typeInfo;

        /// <summary>
        /// Is this property has a invalid getter that doesn't have a valid return value?
        /// </summary>
        public bool HasInvalidGetter { get; private set; }

        /// <summary>
        /// Determine the best representing InterfaceMemberInfo for this property so that we can know the exact type
        /// of the property later
        /// </summary>
        /// <param name="convBase">
        /// Corresponding IConvBase that this property belongs to
        /// </param>
        /// <returns>True if the property is valid, false if the property is invalid</returns>        
        public bool DeterminePropType(ConverterInfo info, IConvBase convBase)
        {
            Debug.Assert(this.BestMemberInfo == null);

            if (this.Get != null) // propget is the best
            {
                this.BestMemberInfo = this.Get;
            }
            else if (this.Put != null) // otherwise try propput
            {
                this.BestMemberInfo = this.Put;
            }
            else if (this.PutRef != null) // otherwise we'll have to use propputref
            {
                this.BestMemberInfo = this.PutRef;
            }

            Debug.Assert(this.BestMemberInfo != null);

            if (this.propertyKind != PropertyKind.FunctionProperty)
            {
                this.PropertyTypeDesc = this.BestMemberInfo.RefVarDesc.ElemDescVar.TypeDesc;
            }
            else
            {
                Debug.Assert(this.PropertyTypeDesc == null);
                this.conversionType = ConversionType.ReturnValue;

                //
                // 1. Find the best FUNCDESC for functions
                //
                FuncDesc bestFuncDesc = this.BestMemberInfo.RefFuncDesc;

                //
                // 2. Determine the type of the property
                //
                // Need to use m_memInfoGet instead of IsPropertyGet because of Guid_PropGetCA
                if (this.Get != null)
                {
                    // find the last [retval] for non-dispatch interfaces or if transform:dispret is specified
                    // for dispatch interfaces, the return value is the real return value
                    if (!bestFuncDesc.IsDispatch || info.TransformDispRetVal)
                    {
                        for (int i = bestFuncDesc.ParamCount - 1; i >= 0; --i)
                        {
                            ElemDesc elemDesc = bestFuncDesc.GetElemDesc(i);
                            if (elemDesc.ParamDesc.IsRetval)
                            {
                                this.PropertyTypeDesc = elemDesc.TypeDesc;
                                this.conversionType = ConversionType.ParamRetVal;
                            }
                        }
                    }

                    // if no [retval], check return type (must not be VT_VOID/VT_HRESULT)
                    TypeDesc retTypeDesc = bestFuncDesc.ElemDescFunc.TypeDesc;
                    if (this.PropertyTypeDesc == null &&
                        retTypeDesc.VarType != (int)VarEnum.VT_VOID && retTypeDesc.VarType != (int)VarEnum.VT_HRESULT)
                    {
                        this.PropertyTypeDesc = retTypeDesc;
                        this.conversionType = ConversionType.ReturnValue;
                    }

                    // Don't use VT_VOID
                    if (this.PropertyTypeDesc != null && this.PropertyTypeDesc.VarType == (int)VarEnum.VT_VOID)
                    {
                        this.PropertyTypeDesc = null;
                    }

                    if (this.PropertyTypeDesc == null)
                    {
                        this.HasInvalidGetter = true;
                        return false;
                    }
                }
                else
                {
                    if (bestFuncDesc.ParamCount < 1)
                    {
                        return false;
                    }

                    // It is possible to write a PROPERTYPUT with [retval], in this case we just ignore it because
                    // if we convert it to a property, it is impossible for C#/VB code to get the return value
                    ElemDesc elemDesc = bestFuncDesc.GetElemDesc(bestFuncDesc.ParamCount - 1);

                    if (bestFuncDesc.IsDispatch && !info.TransformDispRetVal)
                    {
                        // Skip retval check for dispatch functions while TransformDispRetVal is false
                    }
                    else if (elemDesc.ParamDesc.IsRetval)
                    {
                        // RetVal. This is not a valid property
                        return false;
                    }

                    this.PropertyTypeDesc = elemDesc.TypeDesc;
                    // It is a parameter, but it will be the type of the property, so we use ConversionType.ReturnValue
                    this.conversionType = ConversionType.ReturnValue;

                    Debug.Assert(this.PropertyTypeDesc != null, "Cannot determine the type for property!");
                }
            }

            return true;
        }

        /// <summary>
        /// Get the type converter for the property
        /// </summary>
        public TypeConverter GetPropertyTypeConverter(ConverterInfo info, TypeInfo typeInfo)
        {
            if (this.propertyKind == PropertyKind.FunctionProperty)
            {
                return new TypeConverter(info, typeInfo, this.PropertyTypeDesc, this.conversionType);
            }
            else
            {
                return new TypeConverter(info, typeInfo, this.PropertyTypeDesc, ConversionType.ReturnValue);
            }
        }
    }

    /// <summary>
    /// Performs the conversion of an ITypeInfo representing an interface to a managed interface.
    /// </summary>
    internal class ConvInterfaceLocal : ConvLocalBase, IConvInterface
    {
        // Reference to this interface will be replaced with class interface
        // Only happens when there are no other coclass exposing this interface as default
        private IConvClassInterface classInterface;
        private List<InterfaceMemberInfo> allMembers;

        // ConvInterface for parent interface. Used to create parent interface
        private IConvInterface parentInterface;

        // TypeInfo for parent interface. Used to create parent interface
        private TypeInfo parentInterfaceTypeInfo;

        public ConvInterfaceLocal(ConverterInfo info, TypeInfo type)
            : base(info, type)
        {
            Debug.Assert(this.allMembers != null);
        }

        public override ConvType ConvType => ConvType.Interface;

        public IEnumerable<InterfaceMemberInfo> AllMembers => this.allMembers;

        // Corresponding event interface. Null if doesn't exist
        public IConvEventInterface EventInterface { get; private set; }

        // Whether this interface supports IDispatch. Could be dual or dispinterface
        public bool SupportsIDispatch { get; private set; }

        // Whether this interface explicitly implements IEnumerable
        public bool ImplementsIEnumerable { get; private set; }

        /// <summary>
        /// Get event interface instance for the current interface, if it is a source interface.
        /// However we cannot determine if the interface is a source interface or not, so we should make sure
        /// we don't call this function for non-source interfaces
        /// Note: after calling this function, the event interface is not created yet, you'll need to call IConvEventInterface->Create
        /// to create it
        /// </summary>
        public IConvEventInterface DefineEventInterface()
        {
            if (this.EventInterface == null)
            {
                this.EventInterface = new ConvEventInterfaceLocal(this, this.convInfo);
            }

            return this.EventInterface;
        }

        public void AssociateWithExclusiveClassInterface(IConvClassInterface convClassInterface)
        {
            Debug.Assert(this.classInterface == null);
            this.classInterface = convClassInterface;
        }

        public override Type ManagedType
        {
            get
            {
                if (this.classInterface != null)
                {
                    return this.classInterface.ManagedType;
                }
                else
                {
                    return this.RealManagedType;
                }
            }
        }

        protected override void OnDefineType()
        {
            TYPEFLAGS typeFlags = HandleDualIntf();

            TypeInfo typeInfo = RefNonAliasedTypeInfo;
            TypeAttr typeAttr = typeInfo.GetTypeAttr();
            // A interface that has v-table support must be derived from IUnknown
            if (typeAttr.IsInterface || (typeAttr.IsIDispatch || typeAttr.IsDual))
            {

                if (!ConvCommon.IsDerivedFromIUnknown(typeInfo) && typeAttr.Guid != WellKnownGuids.IID_IUnknown)
                {
                    string msg = Resource.FormatString("Wrn_NotIUnknown", typeInfo.GetDocumentation());
                    this.convInfo.ReportEvent(WarningCode.Wrn_NotIUnknown, msg);
                    return;
                }

                // Dual but not derived from IDispatch, continue anyway
                if (typeAttr.IsDual && !ConvCommon.IsDerivedFromIDispatch(typeInfo))
                {
                    string msg = Resource.FormatString("Wrn_DualNotDispatch", typeInfo.GetDocumentation());
                    this.convInfo.ReportEvent(WarningCode.Wrn_DualNotDispatch, msg);
                }
            }

            this.parentInterface = null;
            this.parentInterfaceTypeInfo = null;
            this.ImplementsIEnumerable = ConvCommon.ExplicitlyImplementsIEnumerable(typeInfo, typeAttr);

            string interfacename = this.convInfo.GetUniqueManagedName(this.RefTypeInfo, ConvType.Interface);

            Type typeParent = null;
            var implTypeList = new List<Type>();
            if (typeAttr.ImplTypesCount == 1)
            {
                TypeInfo parent = typeInfo.GetRefType(0);
                TypeAttr parentAttr = parent.GetTypeAttr();

                // Are we derived from something besides IUnknown?
                if (WellKnownGuids.IID_IUnknown != parentAttr.Guid
                    && WellKnownGuids.IID_IDispatch != parentAttr.Guid)
                {
                    this.parentInterfaceTypeInfo = parent;
                    this.parentInterface = (IConvInterface)this.convInfo.GetTypeRef(ConvType.Interface, parent);
                    Debug.Assert(this.parentInterface != null);

                    ConvCommon.ThrowIfImplementingExportedClassInterface(this.RefTypeInfo, this.parentInterface);

                    typeParent = this.parentInterface.RealManagedType;
                    implTypeList.Add(typeParent);
                }
            }

            // If this interface has a NewEnum member but doesn't derive from IEnumerable directly, 
            // then have it implement IEnumerable.
            if (!this.ImplementsIEnumerable && ConvCommon.HasNewEnumMember(this.convInfo, typeInfo, interfacename))
            {
                implTypeList.Add(typeof(System.Collections.IEnumerable));
            }

            this.typeBuilder = this.convInfo.ModuleBuilder.DefineType(
                interfacename,
                TypeAttributes.Public | TypeAttributes.Interface | TypeAttributes.AnsiClass | TypeAttributes.Abstract | TypeAttributes.Import,
                null,
                implTypeList.ToArray());

            // Handled [Guid(...)] custom attribute
            ConvCommon.DefineGuid(RefTypeInfo, RefNonAliasedTypeInfo, this.typeBuilder);

            // Handle [TypeLibType(...)] if evaluate to non-0
            if (typeFlags != 0)
            {
                this.typeBuilder.SetCustomAttribute(CustomAttributeHelper.GetBuilderFor<TypeLibTypeAttribute>((TypeLibTypeFlags)typeFlags));
            }

            // Dual is the default
            this.SupportsIDispatch = true;
            if (typeAttr.IsDual)
            {
                if (!ConvCommon.IsDerivedFromIDispatch(typeInfo))
                {
                    // OK. Now we have a dual interface that doesn't derive from IDispatch. Treat it like IUnknown interface but still emit dispid
                    this.typeBuilder.SetCustomAttribute(CustomAttributeHelper.GetBuilderForInterfaceType(ComInterfaceType.InterfaceIsIUnknown));
                }
            }
            else
            {

                // Handles [InterfaceType(...)] custom attribute
                if (typeAttr.Typekind == TYPEKIND.TKIND_DISPATCH)
                {
                    this.typeBuilder.SetCustomAttribute(CustomAttributeHelper.GetBuilderForInterfaceType(ComInterfaceType.InterfaceIsIDispatch));
                }
                else
                {
                    // This is done to align with old TlbImp behavior
                    if (!ConvCommon.IsDerivedFromIDispatch(typeInfo))
                    {
                        this.typeBuilder.SetCustomAttribute(CustomAttributeHelper.GetBuilderForInterfaceType(ComInterfaceType.InterfaceIsIUnknown));
                    }

                    this.SupportsIDispatch = false;
                }
            }

            this.convInfo.AddToSymbolTable(RefTypeInfo, ConvType.Interface, this);
            this.convInfo.RegisterType(this.typeBuilder, this);

            this.allMembers = InterfaceMemberInfo.BuildMemberList(this.convInfo, typeInfo, this);
        }

        /// <summary>
        /// Creates the managed interface
        /// </summary>
        protected override Type OnCreate()
        {
            if (this.typeBuilder == null)
            {
                // Something went wrong in the conversion process. Probably the interface is not IUnknown derived
                return null;
            }

            Debug.Assert(this.type == null);

            TypeInfo typeInfo = RefNonAliasedTypeInfo;
            TypeAttr typeAttr = typeInfo.GetTypeAttr();

            // If this interface derives from another interface, the interface must be created
            // I create this interface first so that if anything fails in the next step we still would have something
            if (this.parentInterface != null)
            {
                TypeAttr parentAttr = this.parentInterfaceTypeInfo.GetTypeAttr();
                this.parentInterface.Create();
            }

            // Create interface
            InterfaceInfoFlags intFlags = this.SupportsIDispatch ? InterfaceInfoFlags.SupportsIDispatch : InterfaceInfoFlags.None;
            var interfaceInfo = new InterfaceInfo(this.convInfo, this.typeBuilder, typeInfo, typeAttr, intFlags);
            ConvCommon.CreateInterfaceCommon(interfaceInfo);

            // Emit ComConversionLoss if necessary
            if (interfaceInfo.IsConversionLoss)
            {
                this.typeBuilder.SetCustomAttribute(CustomAttributeHelper.GetBuilderFor<ComConversionLossAttribute>());
            }

            return this.typeBuilder.CreateType();
        }

        private TYPEFLAGS HandleDualIntf()
        {
            TypeInfo typeInfo = this.RefNonAliasedTypeInfo;

            // For dual interfaces, it has a "funky" TKIND_DISPATCH|TKIND_DUAL interface with a parter of TKIND_INTERFACE|TKIND_DUAL interface
            // The first one is pretty bad and has duplicated all the interface members of its parent, which is not we want
            // We want the second v-table interface
            // So, if we indeed have seen this kind of interface, prefer its partner
            // However, we should not blindly get the partner because those two interfaces partners with each other
            // So we need to first test to see if the interface is both dispatch & dual, and then get its partner interface
            TypeAttr typeAttr = typeInfo.GetTypeAttr();
            TYPEFLAGS typeFlags = typeAttr.TypeFlags;

            if (typeAttr.IsDual)
            {
                if (typeAttr.IsIDispatch)
                {
                    // The passed interface is the original dual interface
                    TypeInfo refTypeInfo;
                    if (typeInfo.TryGetRefTypeForDual(out refTypeInfo))
                    {
                        typeInfo = refTypeInfo;
                    }
                }
                else
                {
                    // We are dealing with the 'funky' interface, get the original one
                    TypeInfo refTypeInfo;
                    if (typeInfo.TryGetRefTypeForDual(out refTypeInfo))
                    {
                        TypeAttr dualTypeAttr = refTypeInfo.GetTypeAttr();
                        typeFlags = dualTypeAttr.TypeFlags;
                    }
                }
            }

            this.ResetTypeInfos(this.RefTypeInfo, ConvCommon.GetAlias(typeInfo));

            // Prefer the type flag in the alias
            TypeAttr refTypeAttr = this.RefTypeInfo.GetTypeAttr();
            if (refTypeAttr.IsAlias)
            {
                return refTypeAttr.TypeFlags;
            }

            return typeFlags;
        }
    }

    /// <summary>
    /// Represents external interface that is already been created
    /// </summary>
    internal class ConvInterfaceExternal : IConvInterface
    {
        private readonly ConverterInfo convInfo;
        private readonly TypeInfo typeInfo;
        private readonly Type managedType;

        // Type info that is not an alias
        private readonly TypeInfo nonAliasedTypeInfo;

        private List<InterfaceMemberInfo> allMembers;
        private IConvClassInterface m_classInterface;

        public ConvInterfaceExternal(ConverterInfo info, TypeInfo typeInfo, Type managedType, ConverterAssemblyInfo converterAssemblyInfo)
        {
            this.convInfo = info;
            this.typeInfo = typeInfo;

            this.nonAliasedTypeInfo = ConvCommon.GetAlias(typeInfo);

            TypeAttr typeAttr = this.nonAliasedTypeInfo.GetTypeAttr();
            if (typeAttr.IsDual)
            {
                if (typeAttr.IsIDispatch)
                {
                    // The passed interface is the original dual interface
                    TypeInfo refTypeInfo;
                    if (typeInfo.TryGetRefTypeForDual(out refTypeInfo))
                    {
                        this.nonAliasedTypeInfo = refTypeInfo;
                    }
                }
            }

            this.managedType = managedType;
            TypeAttr attr = this.nonAliasedTypeInfo.GetTypeAttr();
            this.SupportsIDispatch = attr.IsIDispatch;
            this.ImplementsIEnumerable = ConvCommon.ExplicitlyImplementsIEnumerable(this.nonAliasedTypeInfo, attr);

            this.convInfo.AddToSymbolTable(typeInfo, ConvType.Interface, this);
            this.convInfo.RegisterType(managedType, this);

            // Special support for external class interface
            // It is possible that this external interface is a "exclusive" default interface in a coclass
            // In this case we need to find the coclass and resolve the coclass so that default interface -> class interface
            // conversion can happen
            TypeInfo coclassTypeInfo;
            if (converterAssemblyInfo.ClassInterfaceMap.TryGetCoClassForExclusiveDefaultInterface(typeInfo, out coclassTypeInfo))
            {
                // Figure out the class interface managed type. It should be the same name as the TypeInfo, unless there is Guid_ManagedName
                string classInterfaceName = info.GetManagedName(coclassTypeInfo, converterAssemblyInfo.Namespace);
                Type classInterfaceType = managedType.Assembly.GetType(classInterfaceName);
                if (classInterfaceType == null)
                {
                    throw new TlbImpGeneralException(
                        Resource.FormatString("Err_CanotFindReferencedType", classInterfaceName, converterAssemblyInfo.Assembly.FullName),
                        ErrorCode.Err_CanotFindReferencedType);
                }
            
                new ConvClassInterfaceExternal(
                    info,
                    coclassTypeInfo,
                    classInterfaceType,
                    converterAssemblyInfo);
            }
        }

        public ConvType ConvType => ConvType.Interface;

        public ConvScope ConvScope => ConvScope.External;

        // Corresponding event interface. Always generated instead of refering to a external one
        public IConvEventInterface EventInterface { get; private set; }

        public IEnumerable<InterfaceMemberInfo> AllMembers
        {
            get
            {
                if (this.allMembers == null)
                {
                    this.allMembers = InterfaceMemberInfo.BuildMemberList(
                        this.convInfo,
                        this.nonAliasedTypeInfo,
                        this);
                }

                return this.allMembers;
            }
        }

        public void AssociateWithExclusiveClassInterface(IConvClassInterface convClassInterface)
        {
            m_classInterface = convClassInterface;
        }

        public bool SupportsIDispatch { get; private set; }

        public void Create()
        {
            // Do nothing
        }

        public IConvEventInterface DefineEventInterface()
        {
            // We need to create a new event interface for external interface if necessary,
            // instead of referencing the existing one TlbImpv1 already does that. So we
            // don't need to change this behavior.
            if (this.EventInterface == null)
            {
                this.EventInterface = new ConvEventInterfaceLocal(this, this.convInfo);
            }

            return this.EventInterface;
        }

        public bool ImplementsIEnumerable { get; private set; }

        public TypeInfo RefTypeInfo => this.typeInfo;

        public TypeInfo RefNonAliasedTypeInfo => this.RefTypeInfo;

        public Type RealManagedType => this.managedType;

        public Type ManagedType
        {
            get
            {
                if (m_classInterface != null)
                    return m_classInterface.ManagedType;
                else
                    return RealManagedType;
            }
        }

        public string ManagedName => this.RealManagedType.FullName;
    }
}
