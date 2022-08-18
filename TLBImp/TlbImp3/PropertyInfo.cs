// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;

using TypeLibUtilities.TypeLibAPI;

namespace TypeLibUtilities
{
    /// <summary>
    /// Represent a property which has get/put/putref methods
    /// Collect the MethodBuilder for get/put/putref and generate a property
    /// </summary>
    internal class ConvProperty
    {
        private readonly InterfacePropertyInfo propertyInfo;

        private MethodBuilder methodGet;
        private MethodBuilder methodPut;
        private MethodBuilder methodPutRef;

        public ConvProperty(InterfacePropertyInfo propertyInfo)
        {
            this.propertyInfo = propertyInfo;
        }

        public void SetGetMethod(MethodBuilder method)
        {
            this.methodGet = method;
        }

        public void SetPutMethod(MethodBuilder method)
        {
            this.methodPut = method;
        }

        public void SetPutRefMethod(MethodBuilder method)
        {
            this.methodPutRef = method;
        }

        /// <summary>
        /// Generate properties for Functions
        /// </summary>
        public void GenerateProperty(InterfaceInfo info)
        {
            // Generate property using unique name
            string uniqueName = info.GenerateUniqueMemberName(
                this.propertyInfo.RecommendedName,
                null, 
                MemberTypes.Property);

            // Convert the signature
            Type[] paramTypes = null;
            Type retType = null;
            if (this.propertyInfo.Kind == PropertyKind.VarProperty)
            {
                // Converting variable to property. There are no parameters at all
                TypeConverter typeConverter = this.propertyInfo.GetPropertyTypeConverter(info.ConverterInfo, info.RefTypeInfo);
                info.IsConversionLoss |= typeConverter.IsConversionLoss;
                retType = typeConverter.ConvertedType;
            }
            else
            {
                // Converting propget/propput/propputref functions to property.
                TypeConverter typeConverter = this.propertyInfo.GetPropertyTypeConverter(info.ConverterInfo, info.RefTypeInfo);
                retType = typeConverter.ConvertedType;
                info.IsConversionLoss |= typeConverter.IsConversionLoss;

                FuncDesc bestFuncDesc = this.propertyInfo.BestFuncDesc;

                // if we have a [vararg]
                int varArg = -1;
                if (bestFuncDesc.ParamOptCount == -1)
                {
                    ConvCommon.CheckForOptionalArguments(info.ConverterInfo, bestFuncDesc, out varArg, out _, out _);
                }

                List<Type> paramTypeList = new List<Type>();

                // CLS Rule 27: The types of the parameters of the property shall be the types of the parameters to the getter and -
                // the types of all but the final parameter of the setter.
                // All of these types shall be CLS - compliant, and shall not be managed pointers (i.e., shall not be passed by reference).
                bool skipLastParam = bestFuncDesc.IsPropertyPut || bestFuncDesc.IsPropertyPutRef;
                for (int i = 0; i < bestFuncDesc.ParamCount; ++i)
                {
                    ElemDesc elemDesc = bestFuncDesc.GetElemDesc(i);
                    ParamDesc paramDesc = elemDesc.ParamDesc;

                    // Skip LCID/RetVal
                    if (paramDesc.IsLCID || paramDesc.IsRetval)
                    {
                        continue;
                    }

                    // Skip the "new value" parameter for putters
                    if (skipLastParam && (i == bestFuncDesc.ParamCount - 1))
                    {
                        break;
                    }

                    var conversionType = (i == varArg) ? ConversionType.VarArgParameter : ConversionType.Parameter;

                    var paramTypeConverter = new TypeConverter(info.ConverterInfo, info.RefTypeInfo, elemDesc.TypeDesc, conversionType);
                    info.IsConversionLoss |= paramTypeConverter.IsConversionLoss;
                    paramTypeList.Add(paramTypeConverter.ConvertedType);
                }

                paramTypes = paramTypeList.ToArray();
            }

            // Define the property
            PropertyBuilder propertyBuilder = info.TypeBuilder.DefineProperty(uniqueName, PropertyAttributes.HasDefault, retType, paramTypes);

            if (info.IsCoClass && !info.IsDefaultInterface)
            {
                // Skip non-default interfaces / implemented interfaces (when we are creating coclass)
            }
            else
            {
                // Emit DISPID attribute
                propertyBuilder.SetCustomAttribute(CustomAttributeHelper.GetBuilderFor<DispIdAttribute>(this.propertyInfo.DispId));
            }

            // We don't need to emit MarshalAs for properties because the get/set functions should already have them
            // [TODO] Is this true? -> Emitting MarshalAs for property will hang up CLR!!
            if (this.methodGet != null)
            {
                propertyBuilder.SetGetMethod(this.methodGet);
            }

            // Has both propPut & propPutRef?
            if (this.methodPut != null && this.methodPutRef != null)
            {
                propertyBuilder.SetSetMethod(this.methodPutRef);
                propertyBuilder.AddOtherMethod(this.methodPut);
            }
            else if (this.methodPut != null)
            {
                propertyBuilder.SetSetMethod(this.methodPut);
            }
            else if (this.methodPutRef != null)
            {
                propertyBuilder.SetSetMethod(this.methodPutRef);
            }

            // Handle DefaultMemberAttribute
            if (this.propertyInfo.DispId == WellKnownDispId.DISPID_VALUE)
            {
                info.ConverterInfo.SetDefaultMember(info.TypeBuilder, uniqueName);
            }

            // Handle alias information
            ConvCommon.HandleAlias(info.ConverterInfo, info.RefTypeInfo, this.propertyInfo.PropertyTypeDesc, propertyBuilder);
        }
    }

    /// <summary>
    /// Represent all the properties for the interface used to create the managed properties
    /// </summary>
    internal class PropertyInfo
    {
        private readonly InterfaceInfo interfaceInfo;
        private readonly Dictionary<int, ConvProperty> properties = new Dictionary<int, ConvProperty>();

        public PropertyInfo(InterfaceInfo info)
        {
            Debug.Assert(info != null);
            this.interfaceInfo = info;
        }

        /// <summary>
        /// Remember the InterfaceMemberInfo/MethodBuilder information for creating properties later
        /// </summary>
        public void SetPropertyInfo(InterfaceMemberInfo memberInfo, MethodBuilder method)
        {
            int dispId = memberInfo.MemId;

            ConvProperty property;
            if (!this.properties.TryGetValue(dispId, out property))
            {
                property = new ConvProperty(memberInfo.PropertyInfo);
                this.properties.Add(dispId, property);
            }

            if (memberInfo.IsPropertyGet)
            {
                property.SetGetMethod(method);
            }

            if (memberInfo.IsPropertyPut)
            {
                property.SetPutMethod(method);
            }

            if (memberInfo.IsPropertyPutRef)
            {
                property.SetPutRefMethod(method);
            }
        }

        /// <summary>
        /// Generate the actual property (not the property accessors)
        /// </summary>
        public void GenerateProperties()
        {
            foreach (KeyValuePair<int, ConvProperty> pair in this.properties)
            {
                ConvProperty property = pair.Value;
                property.GenerateProperty(this.interfaceInfo);
            }

            // Clear all properties so that we can re-use the same interface info again
            this.properties.Clear();
        }
    }
}
