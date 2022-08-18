// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Diagnostics;
using TypeLibUtilities.TypeLibAPI;
using System.IO;

namespace TypeLibUtilities
{
    internal class TlbToAssembly : IDisposable
    {
        private readonly TypeLib typeLib;
        private readonly AssemblyBuilder assemblyBuilder;
        private readonly ModuleBuilder moduleBuilder;
        private readonly ConverterInfo convInfo;
        private ClassInterfaceMap classInterfaceMap;
        private bool isDisposed;

        public TlbToAssembly(
            TypeLib typeLib,
            string asmFilename,
            TypeLibImporterFlags flags,
            ITypeLibImporterNotifySink importSink,
            byte[] publicKey,
            StrongNameKeyPair keyPair,
            string asmNamespace,
            Version asmVersion,
            bool isConvertVariantBoolFieldToBool,
            bool isUseLegacy35QuirksMode)
        {
            Debug.Assert(keyPair == null);
            this.typeLib = typeLib;

            if (asmNamespace == null)
            {
                asmNamespace = this.typeLib.GetDocumentation();
                string fileName = Path.GetFileNameWithoutExtension(asmFilename);
                if (asmNamespace.Equals(fileName))
                {
                    asmNamespace = fileName;
                }

                // Support for GUID_ManagedName (for namespace)
                string customManagedNamespace = this.typeLib.GetCustData<string>(CustomAttributeGuids.GUID_ManagedName);
                if (customManagedNamespace != null)
                {
                    customManagedNamespace = customManagedNamespace.Trim();

                    // Check for common extensions
                    foreach (var ext in new string[] { ".dll", ".exe" })
                    {
                        if (customManagedNamespace.EndsWith(ext, StringComparison.InvariantCultureIgnoreCase))
                        {
                            customManagedNamespace = customManagedNamespace.Substring(0, customManagedNamespace.Length - ext.Length);
                            break;
                        }
                    }

                    asmNamespace = customManagedNamespace;
                }
            }

            // Check for GUID_ExportedFromComPlus
            object value = this.typeLib.GetCustData<object>(CustomAttributeGuids.GUID_ExportedFromComPlus);
            if (value != null)
            {
                throw new TlbImpGeneralException(Resource.FormatString("Err_CircularImport", asmNamespace), ErrorCode.Err_CircularImport);
            }

            string strModuleName = asmFilename;

            int lastSlashIdx = asmFilename.LastIndexOf(Path.DirectorySeparatorChar);
            if (lastSlashIdx != -1)
            {
                strModuleName = strModuleName.Substring(lastSlashIdx);
            }

            // If the version information was not specified, then retrieve it from the typelib.
            TypeLibAttr typeLibAttr = this.typeLib.GetLibAttr();
            if (asmVersion == null)
            {
                asmVersion = new Version(typeLibAttr.MajorVerNum, typeLibAttr.MinorVerNum, 0, 0);
            }

            // Assembly name should not have .DLL
            // while module name must contain the .DLL
            string strAsmName = string.Copy(strModuleName);
            if (strAsmName.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase))
            {
                strAsmName = strAsmName.Substring(0, strAsmName.Length - ".dll".Length);
            }

            var assemblyName = new AssemblyName(strAsmName)
            {
                Version = asmVersion,
            };
            assemblyName.SetPublicKey(publicKey);

            this.assemblyBuilder = CreateAssemblyBuilder(
                assemblyName,
                this.typeLib.GetDocumentation(),
                typeLibAttr,
                flags);

            this.moduleBuilder = this.assemblyBuilder.DefineDynamicModule(strModuleName);

            // Add a listener for the reflection load only resolve events.
            AppDomain currentDomain = Thread.GetDomain();
            currentDomain.ReflectionOnlyAssemblyResolve += this.ReflectionOnlyResolveAsmEvent;

            var settings = new ConverterSettings()
            {
                Namespace = asmNamespace,
                Flags = flags,
                IsConvertVariantBoolFieldToBool = isConvertVariantBoolFieldToBool,
                IsUseLegacy35QuirksMode = isUseLegacy35QuirksMode,
            };
            this.convInfo = new ConverterInfo(this.moduleBuilder, this.typeLib, importSink, settings);
        }

        public AssemblyBuilder Convert()
        {
            //
            // Generate class interfaces
            // NOTE:
            // We have to create class interface ahead of time because of the need to convert default interfaces to
            // class interfafces. However, this creates another problem that the event interface is always named first 
            // before the other interfaces, because we need to create the type builder for the event interface first
            // so that we can create a class interface that implements it. In the previous version of TlbImp,
            // it doesn't have to do that because it can directly create a typeref with the class interface name,
            // without actually creating anything like the TypeBuilder. The result is that the name would be different 
            // with interop assemblies generated by old tlbimp in this case.
            // Given the nature of reflection API, this cannot be easily workarounded unless we switch to metadata APIs. 
            // I believe this is acceptable because this only happens when:
            //  1) People decide to migrate newer .NET framework
            //  2) The event interface name conflicts with a normal interface
            //
            this.classInterfaceMap = ClassInterfaceMap.Create(this.convInfo);

            //
            // Generate the remaining types except coclass
            // Because during creating coclass, we require every type, including all the referenced type to be created
            // This is a restriction of reflection API that when you override a method in parent interface, the method info
            // is needed so the type must be already created and loaded
            //
            List<TypeInfo> coclassList = new List<TypeInfo>();
            int nCount = this.typeLib.GetTypeInfoCount();
            for (int n = 0; n < nCount; ++n)
            {
                TypeInfo type = null;
                try
                {
                    type = this.typeLib.GetTypeInfo(n);
                    string strType = type.GetDocumentation();

                    TypeInfo typeToProcess;
                    TypeAttr attrToProcess;

                    TypeAttr attr = type.GetTypeAttr();

                    TYPEKIND kind = attr.Typekind;
                    if (kind == TYPEKIND.TKIND_ALIAS)
                    {
                        ConvCommon.ResolveAlias(type, attr.TypeDescAlias, out typeToProcess, out attrToProcess);
                        if (attrToProcess.Typekind == TYPEKIND.TKIND_ALIAS)
                        {
                            continue;
                        }
                        else
                        {
                            // We need to duplicate the definition of the user defined type in the name of the alias
                            kind = attrToProcess.Typekind;
                            typeToProcess = type;
                            attrToProcess = attr;
                        }
                    }
                    else
                    {
                        typeToProcess = type;
                        attrToProcess = attr;
                    }

                    switch (kind)
                    {
                        // Process coclass later because of reflection API requirements
                        case TYPEKIND.TKIND_COCLASS:
                            coclassList.Add(typeToProcess);
                            break;

                        case TYPEKIND.TKIND_ENUM:
                            this.convInfo.GetEnum(typeToProcess, attrToProcess);
                            break;

                        case TYPEKIND.TKIND_DISPATCH:
                        case TYPEKIND.TKIND_INTERFACE:
                            this.convInfo.GetInterface(typeToProcess, attrToProcess);
                            break;

                        case TYPEKIND.TKIND_MODULE:
                            this.convInfo.GetModule(typeToProcess, attrToProcess);
                            break;

                        case TYPEKIND.TKIND_RECORD:
                            this.convInfo.GetStruct(typeToProcess, attrToProcess);
                            break;
                        case TYPEKIND.TKIND_UNION:
                            this.convInfo.GetUnion(typeToProcess, attrToProcess);
                            break;
                    }

                    this.convInfo.ReportEvent(
                        MessageCode.Msg_TypeInfoImported,
                        Resource.FormatString("Msg_TypeInfoImported", typeToProcess.GetDocumentation()));
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
                }
            }

            // Process coclass after processing all the other types
            foreach (TypeInfo type in coclassList)
            {
                TypeAttr attr = type.GetTypeAttr();
                try
                {
                    this.convInfo.GetCoClass(type, attr);
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
                }
            }

            // Build an array of EventItfInfo & generate event provider / event sink helpers
            var eventItfList = new List<Event.EventItfInfo>();
            foreach (IConvBase symbol in this.convInfo.GetAllConvBase)
            {
                var convInterface = symbol as IConvInterface;
                if (convInterface != null)
                {
                    var eventInterface = convInterface.EventInterface;
                    if (eventInterface == null)
                    {
                        continue;
                    }

                    Type eventInterfaceType = eventInterface.ManagedType;
                    Type sourceInterfaceType = convInterface.ManagedType;
                    string sourceInterfaceName = sourceInterfaceType.FullName;

                    // Build event interface info and add to the list
                    var eventItfInfo = new Event.EventItfInfo(
                        eventInterfaceType.FullName,
                        sourceInterfaceName,
                        eventInterface.EventProviderName,
                        eventInterfaceType,
                        convInterface.RealManagedType);

                    eventItfList.Add(eventItfInfo);
                }
            }

            Event.TCEAdapterGenerator.Process(this.moduleBuilder, eventItfList);

            return this.assemblyBuilder;
        }

        public void Dispose()
        {
            if (this.isDisposed)
            {
                return;
            }

            AppDomain currentDomain = Thread.GetDomain();
            currentDomain.ReflectionOnlyAssemblyResolve -= this.ReflectionOnlyResolveAsmEvent;

            this.isDisposed = true;
        }

        private static AssemblyBuilder CreateAssemblyBuilder(
            AssemblyName name,
            string typeLibName,
            TypeLibAttr typeLibAttr,
            TypeLibImporterFlags flags)
        {
            var attrList = new List<CustomAttributeBuilder>()
            {
                // Handle the type library name
                CustomAttributeHelper.GetBuilderFor<ImportedFromTypeLibAttribute>(typeLibName),

                // Handle the type library version
                CustomAttributeHelper.GetBuilderFor<TypeLibVersionAttribute>(typeLibAttr.MajorVerNum, typeLibAttr.MinorVerNum),

                // Handle the LIBID
                CustomAttributeHelper.GetBuilderForGuid(typeLibAttr.Guid)
            };

            // If we are generating a PIA, then set the PIA custom attribute.
            if (flags.HasFlag(TypeLibImporterFlags.PrimaryInteropAssembly))
            {
                attrList.Add(CustomAttributeHelper.GetBuilderFor<PrimaryInteropAssemblyAttribute>(typeLibAttr.MajorVerNum, typeLibAttr.MinorVerNum));
            }

            // New assembly as well as loaded assembly should be isolated, but CoreCLR does have ReflectionOnly context :(
            return AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.RunAndCollect, attrList);
        }

        private Assembly ReflectionOnlyResolveAsmEvent(object sender, ResolveEventArgs args)
        {
            string asmName = AppDomain.CurrentDomain.ApplyPolicy(args.Name);
            return Assembly.ReflectionOnlyLoad(args.Name);
        }
    }
}
