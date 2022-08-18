// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

using System;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Security;

namespace TypeLibUtilities
{
    [Flags]
    internal enum ConvLocalFlags
    {
        None = 0,
        DealWithAlias,
        DeferDefineType,
    }

    /// <summary>
    /// Common base class of almost all ConvLocalXXX classes
    /// </summary>
    internal abstract class ConvLocalBase : IConvBase
    {
        protected ConverterInfo convInfo;
        protected TypeBuilder typeBuilder;
        protected Type type;

        // Type info for this ConvXXX class. Could be aliased
        private TypeInfo typeInfo;
        private TypeInfo nonAliasedTypeInfo;
        private bool hasFailed;

        protected ConvLocalBase(ConverterInfo info, TypeInfo typeInfo, ConvLocalFlags flags = ConvLocalFlags.None)
        {
            this.convInfo = info;
            this.typeInfo = typeInfo;
            if (flags.HasFlag(ConvLocalFlags.DealWithAlias))
            {
                this.nonAliasedTypeInfo = ConvCommon.GetAlias(this.typeInfo);
            }
            else
            {
                this.nonAliasedTypeInfo = typeInfo;
            }

            if (!flags.HasFlag(ConvLocalFlags.DeferDefineType))
            {
                this.DefineType();
            }
        }

        public abstract ConvType ConvType
        {
            get;
        }

        public TypeInfo RefTypeInfo => this.typeInfo;

        public TypeInfo RefNonAliasedTypeInfo => this.nonAliasedTypeInfo;

        public virtual Type ManagedType => this.RealManagedType;

        public string ManagedName => ManagedType.FullName;

        public Type RealManagedType
        {
            get
            {
                if (this.type == null)
                {
                    return this.typeBuilder;
                }
                else
                {
                    return this.type;
                }
            }
        }

        public ConvScope ConvScope
        {
            get { return ConvScope.Local; }
        }

        public void Create()
        {
            try
            {
                // Try to create if we haven't failed before
                if (!this.hasFailed && this.type == null)
                {
                    this.type = OnCreate();
                }
            }
            catch (ReflectionTypeLoadException)
            {
                throw; // Fatal failure. Throw
            }
            catch (TlbImpResolveRefFailWrapperException)
            {
                throw; // Fatal failure. Throw
            }
            catch (TlbImpGeneralException)
            {
                throw; // Fatal failure. Throw
            }
            catch (TypeLoadException)
            {
                throw; // TypeLoadException is critical. Throw.
            }
            catch (Exception)
            {
                string name = string.Empty;
                if (this.typeInfo != null)
                {
                    try
                    {
                        name = this.typeInfo.GetDocumentation();
                    }
                    catch (Exception)
                    {
                    }
                }

                if (name != string.Empty)
                {
                    string msg = Resource.FormatString("Wrn_InvalidTypeInfo", name);
                    this.convInfo.ReportEvent(WarningCode.Wrn_InvalidTypeInfo, msg);
                }
                else
                {
                    string msg = Resource.FormatString("Wrn_InvalidTypeInfo_Unnamed");
                    this.convInfo.ReportEvent(WarningCode.Wrn_InvalidTypeInfo_Unnamed, msg);
                }

                // When failure, try to create the type anyway
                if (this.type == null)
                {
                    this.hasFailed = true;
                    this.type = this.typeBuilder.CreateType();
                }
            }
        }

        /// <summary>
        /// Used to reinitialize TypeInfo & NonAliasedTypeInfo in special cases. Currently it is used
        /// by ConvInterfaceLocal
        /// </summary>
        protected void ResetTypeInfos(TypeInfo typeInfo, TypeInfo nonAliasedTypeInfo)
        {
            Debug.Assert(ConvType == ConvType.Interface, $"Only {nameof(ConvType.Interface)} should reset TypeInfo instances");

            this.typeInfo = typeInfo;
            this.nonAliasedTypeInfo = nonAliasedTypeInfo;
        }

        /// <summary>
        /// Creation of managed type is split into two stages: define & create. This is the define stage.
        /// Defines the type in the assembly. This usually involves the following:
        /// 1. Defining parent types
        /// 2. Create and set typeBuilder field
        /// 3. Define attributes
        /// </summary>
        protected abstract void OnDefineType();

        protected abstract Type OnCreate();

        protected void DefineType()
        {
            try
            {
                OnDefineType();
                Debug.Assert(this.typeBuilder != null);

                // Emit SuppressUnmanagedCodeSecurityAttribute for /unsafe switch
                if (this.convInfo.Settings.Flags.HasFlag(TypeLibImporterFlags.UnsafeInterfaces))
                {
                    if (this.ConvType != ConvType.ClassInterface && this.ConvType != ConvType.EventInterface)
                    {
                        this.typeBuilder.SetCustomAttribute(CustomAttributeHelper.GetBuilderFor<SuppressUnmanagedCodeSecurityAttribute>());
                    }
                }
            }
            catch (ReflectionTypeLoadException)
            {
                throw; // Fatal failure. Throw
            }
            catch (TlbImpResolveRefFailWrapperException)
            {
                throw; // Fatal failure. Throw
            }
            catch (TlbImpGeneralException)
            {
                throw; // Fatal failure. Throw
            }
            catch (TypeLoadException)
            {
                throw; // TypeLoadException is critical. Throw.
            }
            catch (Exception)
            {
                string name = string.Empty;
                if (this.typeInfo != null)
                {
                    try
                    {
                        name = this.typeInfo.GetDocumentation();
                    }
                    catch (Exception)
                    {
                    }
                }

                if (name != string.Empty)
                {
                    string msg = Resource.FormatString("Wrn_InvalidTypeInfo", name);
                    this.convInfo.ReportEvent(WarningCode.Wrn_InvalidTypeInfo, msg);
                }
                else
                {
                    string msg = Resource.FormatString("Wrn_InvalidTypeInfo_Unnamed");
                    this.convInfo.ReportEvent(WarningCode.Wrn_InvalidTypeInfo_Unnamed, msg);
                }

                // When failure, try to create the type anyway
                if (this.typeBuilder != null)
                {
                    this.type = this.typeBuilder.CreateType();
                }
            }
        }
    }
}