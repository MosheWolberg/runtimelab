// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using System.Text;
using System.Globalization;
using System.Security;
using TypeLibUtilities.TypeLibAPI;

using Microsoft.Win32;

namespace TypeLibUtilities
{
    internal static class AssemblyExtensions
    {
        public static Guid GetTypeLibGuidForAssembly(this Assembly asm)
        {
            // [assembly:Guid("[If not set, load in .NET Framework and determine auto-generated GUID]")]
            GuidAttribute asmGuid = asm.GetCustomAttribute<GuidAttribute>();
            return Guid.Parse(asmGuid.Value);
        }
    }

    internal class TypeLibConverter
    {
        private const int SuccessReturnCode = 0;
        private const int ErrorReturnCode = 100;
        private static REGKIND s_RK;

        //**************************************************************************
        // Entry point called on the typelib importer in the proper app domain.
        //**************************************************************************
        public static int Run(TlbImpOptions options)
        {
            s_Options = options;

            Output.SetSilent(options.SilentMode);
            Output.Silence(options.SilenceList);

            TypeLib TypeLib = null;
            String strPIAName = null;
            String strPIACodeBase = null;

            s_RK = REGKIND.REGKIND_NONE;

            if (Environment.OSVersion.Platform != PlatformID.Win32Windows)
            {
                if (IsImportingToItanium(options.Flags) || IsImportingToX64(options.Flags))
                {
                    s_RK |= REGKIND.REGKIND_LOAD_TLB_AS_64BIT;
                }
                else if (IsImportingToX86(options.Flags))
                {
                    s_RK |= REGKIND.REGKIND_LOAD_TLB_AS_32BIT;
                }
            }

            //----------------------------------------------------------------------
            // Load the typelib.
            try
            {
                OleAut32.LoadTypeLibEx(s_Options.TypeLibName, s_RK, out ITypeLib typeLib);
                TypeLib = new TypeLib(typeLib);
                s_RefTypeLibraries.Add(s_Options.TypeLibName, TypeLib);
            }
            catch (COMException e)
            {
                if (!s_Options.SearchPathSucceeded)
                {
                    // We failed to search for the typelib and we failed to load it.
                    // This means that the input typelib is not available.
                    Output.WriteError(Resource.FormatString("Err_InputFileNotFound", s_Options.TypeLibName), ErrorCode.Err_InputFileNotFound);
                }
                else
                {
                    if (e.ErrorCode == unchecked((int)0x80029C4A))
                    {
                        Output.WriteError(Resource.FormatString("Err_InputFileNotValidTypeLib", s_Options.TypeLibName), ErrorCode.Err_InputFileNotValidTypeLib);
                    }
                    else
                    {
                        Output.WriteError(Resource.FormatString("Err_TypeLibLoad", e), ErrorCode.Err_TypeLibLoad);
                    }
                }
                return ErrorReturnCode;
            }
            catch (Exception e)
            {
                Output.WriteError(Resource.FormatString("Err_TypeLibLoad", e), ErrorCode.Err_TypeLibLoad);
                return ErrorReturnCode;
            }

            //----------------------------------------------------------------------
            // Check to see if there already exists a primary interop assembly for 
            // this typelib.

            if (TypeLibConverter.GetPrimaryInteropAssembly(TypeLib, out strPIAName, out strPIACodeBase))
            {
                Output.WriteWarning(Resource.FormatString("Wrn_PIARegisteredForTlb", strPIAName, s_Options.TypeLibName), WarningCode.Wrn_PIARegisteredForTlb);
            }

            //----------------------------------------------------------------------
            // Retrieve the name of output assembly if it was not explicitly set.

            if (s_Options.AssemblyName == null)
            {
                s_Options.AssemblyName = TypeLib.GetDocumentation() + ".dll";
            }

            //----------------------------------------------------------------------
            // Do some verification on the output assembly.

            String strFileNameNoPath = Path.GetFileName(s_Options.AssemblyName);
            String strExtension = Path.GetExtension(s_Options.AssemblyName);

            // Validate that the extension is valid.
            bool bExtensionValid = ".dll".Equals(strExtension.ToLower(CultureInfo.InvariantCulture));

            // If the extension is not valid then tell the user and quit.
            if (!bExtensionValid)
            {
                Output.WriteError(Resource.FormatString("Err_InvalidExtension"), ErrorCode.Err_InvalidExtension);
                return ErrorReturnCode;
            }

            // Make sure the output file will not overwrite the input file.
            String strInputFilePath = (new FileInfo(s_Options.TypeLibName)).FullName.ToLower(CultureInfo.InvariantCulture);
            String strOutputFilePath;
            try
            {
                strOutputFilePath = (new FileInfo(s_Options.AssemblyName)).FullName.ToLower(CultureInfo.InvariantCulture);
            }
            catch (System.IO.PathTooLongException)
            {
                Output.WriteError(Resource.FormatString("Err_OutputFileNameTooLong", s_Options.AssemblyName), ErrorCode.Err_OutputFileNameTooLong);
                return ErrorReturnCode;
            }

            if (strInputFilePath.Equals(strOutputFilePath))
            {
                Output.WriteError(Resource.FormatString("Err_OutputWouldOverwriteInput"), ErrorCode.Err_OutputWouldOverwriteInput);
                return ErrorReturnCode;
            }

            //-------------------------------------------------------------------------
            // Load all assemblies provided as explicit references on the command line.
            if (s_Options.AssemblyRefList != null)
            {
                String[] asmPaths = s_Options.AssemblyRefList.Split(';');

                foreach (String asmPath in asmPaths)
                {
                    if (!LoadAssemblyRef(asmPath))
                        return ErrorReturnCode;
                }
            }

            //-------------------------------------------------------------------------
            // And the same for type library references.
            if (s_Options.TypeLibRefList != null)
            {
                String[] tlbPaths = s_Options.TypeLibRefList.Split(';');

                foreach (String tlbPath in tlbPaths)
                {
                    if (!LoadTypeLibRef(tlbPath))
                        return ErrorReturnCode;
                }
            }

            //-------------------------------------------------------------------------
            // Before we attempt the import, verify the references first
            if (!VerifyTypeLibReferences(s_Options.TypeLibName))
                return ErrorReturnCode;

            //----------------------------------------------------------------------
            // Attempt the import.

            try
            {
                try
                {
                    // Import the typelib to an assembly.
                    AssemblyBuilder AsmBldr = DoImport(TypeLib, s_Options.AssemblyName, s_Options.AssemblyNamespace,
                        s_Options.AssemblyVersion, s_Options.PublicKey, s_Options.KeyPair, s_Options.Product,
                        s_Options.ProductVersion, s_Options.Company, s_Options.Copyright, s_Options.Trademark,
                        s_Options.Flags, s_Options.ConvertVariantBoolFieldToBool, s_Options.UseLegacy35QuirksMode);
                    if (AsmBldr == null)
                        return ErrorReturnCode;
                }
                catch (TlbImpResolveRefFailWrapperException ex)
                {
                    // Throw out the inner exception instead
                    throw ex.InnerException;
                }
            }
            catch (ReflectionTypeLoadException e)
            {
                int i;
                Exception[] exceptions;
                Output.WriteError(Resource.FormatString("Err_TypeLoadExceptions"), ErrorCode.Err_TypeLoadExceptions);
                exceptions = e.LoaderExceptions;
                for (i = 0; i < exceptions.Length; i++)
                {
                    try
                    {
                        Output.WriteInfo(Resource.FormatString("Msg_DisplayException", new object[] { i, exceptions[i].GetType().ToString(), exceptions[i].Message }), MessageCode.Msg_DisplayException);
                    }
                    catch (Exception ex)
                    {
                        Output.WriteInfo(Resource.FormatString("Msg_DisplayNestedException", new object[] { i, ex.GetType().ToString(), ex.Message }), MessageCode.Msg_DisplayNestedException);
                    }
                }
                return ErrorReturnCode;
            }
            catch (TlbImpGeneralException tge)
            {
                Output.WriteTlbimpGeneralException(tge);
                return ErrorReturnCode;
            }
            catch (COMException ex)
            {
                if ((uint)ex.ErrorCode == HResults.TYPE_E_CANTLOADLIBRARY)
                {
                    // Give a more specific message
                    Output.WriteError(Resource.FormatString("Err_RefTlbCantLoad"), ErrorCode.Err_RefTlbCantLoad);
                }
                else
                {
                    // TlbImp COM exception
                    string msg = Resource.FormatString(
                        "Err_UnexpectedException",
                        ex.GetType().ToString(),
                        ex.Message
                        );
                    Output.WriteError(msg, ErrorCode.Err_UnexpectedException);
                }

                return ErrorReturnCode;
            }
            catch (TlbImpInvalidTypeConversionException ex)
            {
                // This usually means that a type conversion has failed outside normal conversion process...
                string name = ex.TypeName;
                if (name != null)
                    Output.WriteError(Resource.FormatString("Err_FatalErrorInConversion_Named", name), ErrorCode.Err_FatalErrorInConversion_Named);
                else
                    Output.WriteError(Resource.FormatString("Err_FatalErrorInConversion_Unnamed"), ErrorCode.Err_FatalErrorInConversion_Unnamed);

                return ErrorReturnCode;
            }
            catch (SecurityException ex)
            {
                // Only treat SecurityException with PermissionType != null as permission issue
                if (ex.PermissionType == null)
                {
                    string msg = Resource.FormatString(
                        "Err_UnexpectedException",
                        ex.GetType().ToString(),
                        ex.Message
                        );
                    Output.WriteError(msg, ErrorCode.Err_UnexpectedException);
                }
                else
                {
                    Output.WriteError(Resource.GetString("Err_PermissionException"), ErrorCode.Err_PermissionException);
                }

                return ErrorReturnCode;
            }
            catch (Exception ex)
            {
                string msg = Resource.FormatString(
                    "Err_UnexpectedException",
                    ex.GetType().ToString(),
                    ex.Message
                    );
                Output.WriteError(msg, ErrorCode.Err_UnexpectedException);

                return ErrorReturnCode;
            }

            Output.WriteInfo(Resource.FormatString("Msg_TypeLibImported", s_Options.AssemblyName), MessageCode.Msg_TypeLibImported);

            return SuccessReturnCode;
        }

        /// <summary>
        /// Try to load the type library and verify guid/version
        /// </summary>
        /// <returns>HRESULT. >=0 if succeeds, otherwise failed</returns>
        private static int TryLoadTypeLib(string pathName, string simpleName, Guid tlbId, ushort majorVersion, ushort minorVersion)
        {
            int hr = OleAut32.LoadTypeLib(pathName, out ITypeLib typeLib);
            if (hr >= 0)
            {
                var refTypeLib = new TypeLib(typeLib);
                s_RefTypeLibraries.Add(pathName, refTypeLib);

                TypeLibAttr libAttr = refTypeLib.GetLibAttr();
                if (libAttr.Guid == tlbId
                    && libAttr.MajorVerNum == majorVersion
                    && libAttr.MinorVerNum == minorVersion)
                {
                    if (TypeLibConverter.s_Options.VerboseMode)
                    {
                        Output.WriteInfo(
                            Resource.FormatString("Msg_TypeLibRefResolved",
                                new object[] { simpleName, majorVersion.ToString() + "." + minorVersion, tlbId.ToString(), pathName }),
                            MessageCode.Msg_TypeLibRefResolved);
                    }

                    return 0;
                }
                else
                {
                    if (TypeLibConverter.s_Options.VerboseMode)
                    {
                        Output.WriteInfo(
                            Resource.FormatString("Msg_TypeLibRefMismatch",
                                new object[] {
                                        simpleName, majorVersion.ToString() + "." + minorVersion, tlbId.ToString(),
                                        simpleName, libAttr.MajorVerNum.ToString() + "." + libAttr.MajorVerNum.ToString(), libAttr.Guid,
                                        pathName }),
                            MessageCode.Msg_TypeLibRefMismatch);
                    }

                    return -1;
                }
            }

            return hr;
        }

        /// <summary>
        /// Verify that whether we can resolve all type library references.
        /// </summary>
        /// <returns>true if succeed. false if failed</returns>
        private static bool VerifyTypeLibReferences(string tlbFileName)
        {
            // For now, we do the check only in verbose mode to minimize the potential impact
            if (!TypeLibConverter.s_Options.VerboseMode)
                return true;

            // See TlbRef!LoadTypeLibWithResolver() in framework (tlibapi.cpp)
            // Logic from TlbRef API. This is a .NET Framework DLL that tries to load all
            // TLBs without hitting the registry. The reason is unclear, but that is what happens.
            //  - If import of stdole32.tlb (old name), try stdole2.tlb (new name) instead.
            //  - Old TypeLib formats not support - exception is stdole32.tlb is the only exception.
            //  - In failure case, we do not deal with monikers - verified using MkParseDisplayName()
            //     - See https://docs.microsoft.com/en-us/windows/desktop/api/objbase/nf-objbase-mkparsedisplayname

            //{

            //    //
            //    // Find this type library in list of referenced type libraries
            //    //
            //    foreach (string pathName in s_RefTypeLibraries.Keys)
            //    {
            //        TypeLib refTypeLib = new TypeLib(s_RefTypeLibraries[pathName] as TypeLibUtilities.ITypeLib);
            //        TypeLibAttr libAttr = refTypeLib.GetLibAttr();
            //        if (libAttr.guid == tlbId)
            //        {
            //            if (libAttr.wMajorVerNum == majorVersion && libAttr.wMinorVerNum == minorVersion)
            //            {
            //                // Resolved to a matching type lib
            //                bstrPathName = pathName;
            //                return 0;
            //            }
            //        }
            //    }

            //    //
            //    // Find using GUID
            //    //
            //    int hr = OleAut32.QueryPathOfRegTypeLib(ref tlbId, majorVersion, minorVersion, lcid, out bstrPathName);
            //    if (hr >= 0)
            //    {
            //        // Try loading the type library and verify guid/version
            //        hr = TryLoadTypeLib(bstrPathName, simpleName, tlbId, majorVersion, minorVersion);
            //        if (hr >= 0) return hr;
            //    }

            //    //
            //    // Try to load current directory
            //    //
            //    bstrPathName = Path.Combine(Directory.GetCurrentDirectory(), simpleName);
            //    if (File.Exists(bstrPathName))
            //    {
            //        // Try loading the type library guid/version
            //        hr = TryLoadTypeLib(bstrPathName, simpleName, tlbId, majorVersion, minorVersion);
            //        if (hr >= 0) return hr;
            //    }

            //    if (TlbImpCode.s_Options.m_bVerboseMode)
            //    {
            //        Output.WriteInfo(
            //            Resource.FormatString("Msg_TypeLibRefResolveFailed",
            //                new object[] { simpleName, majorVersion.ToString() + "." + minorVersion, tlbId.ToString() }),
            //            MessageCode.Msg_TypeLibRefResolveFailed);
            //    }

            //    return -1;
            //}

            return true;
        }

        //**************************************************************************
        // Load an assembly reference specified on the command line.
        //**************************************************************************
        private static bool LoadAssemblyRef(String path)
        {
            Assembly asm = null;

            // We're guaranteed to have a fully qualified path at this point.              
            try
            {
                // Load the assembly.
                asm = Assembly.ReflectionOnlyLoadFrom(path);

                // Retrieve the GUID and add the assembly to the hashtable of referenced assemblies.
                Guid TypeLibId = asm.GetTypeLibGuidForAssembly();

                // Add the assembly to the list of referenced assemblies if it isn't already present.
                if (s_AssemblyRefs.Contains(TypeLibId))
                {
                    // If this is the same assembly and same version, just return
                    // Since asm is a RuntimeAssembly we can simply do object reference comparison
                    if ((object)asm == s_AssemblyRefs[TypeLibId])
                        return true;

                    // Otherwise, we have two versions of the same type assembly.
                    Output.WriteError(Resource.FormatString("Err_MultipleVersionsOfAssembly", TypeLibId), ErrorCode.Err_MultipleVersionsOfAssembly);
                    return false;
                }

                s_AssemblyRefs.Add(TypeLibId, asm);
            }
            catch (BadImageFormatException)
            {
                Output.WriteError(Resource.FormatString("Err_RefAssemblyInvalid", path), ErrorCode.Err_RefAssemblyInvalid);
                return false;
            }
            catch (FileNotFoundException)
            {
                Output.WriteError(Resource.FormatString("Err_RefAssemblyNotFound", path), ErrorCode.Err_RefAssemblyNotFound);
                return false;
            }
            catch (FileLoadException e)
            {
                Output.WriteError(Resource.FormatString("Err_RefAssemblyCantLoad", path), ErrorCode.Err_RefAssemblyCantLoad);
                Output.WriteError(e);
                return false;
            }
            catch (ApplicationException e)
            {
                Output.WriteError(e);
                return false;
            }

            return true;
        }

        //**************************************************************************
        // Load a type library specified on the command line.
        //**************************************************************************
        private static bool LoadTypeLibRef(String path)
        {
            // For now just expect the user to supply a full path to the type
            // library. We can improve this later as need be.
            try
            {
                ITypeLib typeLib;
                OleAut32.LoadTypeLibEx(path, s_RK, out typeLib);
                s_RefTypeLibraries.Add(path, new TypeLib(typeLib));
            }
            catch (COMException e)
            {
                if (e.ErrorCode == unchecked((int)0x80029C4A))
                    Output.WriteError(Resource.FormatString("Err_InputFileNotValidTypeLib", path), ErrorCode.Err_InputFileNotValidTypeLib);
                else
                    Output.WriteError(Resource.FormatString("Err_TypeLibLoad", path, e), ErrorCode.Err_TypeLibLoad);
                return false;
            }
            catch (Exception e)
            {
                Output.WriteError(Resource.FormatString("Err_TypeLibLoad", e), ErrorCode.Err_TypeLibLoad);
                return false;
            }

            return true;
        }

        //**************************************************************************
        // Static importer function used by main and the callback.
        //**************************************************************************
        public static AssemblyBuilder DoImport(TypeLib TypeLib,
                                               String strAssemblyFileName,
                                               String strAssemblyNamespace,
                                               Version asmVersion,
                                               byte[] publicKey,
                                               StrongNameKeyPair keyPair,
                                               String strProduct,
                                               String strProductVersion,
                                               String strCompany,
                                               String strCopyright,
                                               String strTrademark,
                                               TypeLibImporterFlags flags,
                                               bool isConvertVariantBoolFieldToBool,
                                               bool isUseLegacy35QuirksMode)
        {
            // Detemine the assembly file name.
            String asmFileName = Path.GetFileName(strAssemblyFileName);

            // If the type library is 64-bit, make sure the user specified a platform type.
            TypeLibAttr TLibAttr = TypeLib.GetLibAttr();

            // Add this typelib to list of importing typelibs.
            s_ImportingLibraries.Add(TLibAttr.Guid.ToString(), TLibAttr.Guid);

            // Validate the machine options
            if (!ValidateMachineType(flags, TLibAttr.Syskind))
                return null;

            AssemblyBuilder AsmBldr;

            // Convert the typelib.
            using (ImporterCallback callback = new ImporterCallback())
            {
                using (var process = new TlbToAssembly(
                    TypeLib,
                    strAssemblyFileName,
                    flags,
                    callback,
                    publicKey,
                    keyPair,
                    strAssemblyNamespace,
                    asmVersion,
                    isConvertVariantBoolFieldToBool,
                    isUseLegacy35QuirksMode))
                {
                    AsmBldr = process.Convert();

                    //new System.Reflection.Metadata.Ecma335.MetadataBuilder()

                    var writer = new AssemblySourceWriter(AsmBldr);
                    writer.Write(Console.Out);
                }
            }

            if (AsmBldr == null) return null;

            // Remove this typelib from list of importing typelibs.
            s_ImportingLibraries.Remove(TLibAttr.Guid.ToString());

            // Delete the output assembly.
            File.Delete(asmFileName);

            // [TODO] Set flags on the generated assembly
            //AsmBldr.DefineVersionInfoResource(strProduct, strProductVersion, strCompany, strCopyright, strTrademark);

            //if (IsImportingToX64(flags))
            //{
            //    AsmBldr.Save(asmFileName, PortableExecutableKinds.ILOnly | PortableExecutableKinds.PE32Plus, 
            //        ImageFileMachine.AMD64);
            //}
            //else if (IsImportingToItanium(flags))
            //{
            //    AsmBldr.Save(asmFileName, PortableExecutableKinds.ILOnly | PortableExecutableKinds.PE32Plus, 
            //        ImageFileMachine.IA64);
            //}
            //else if (IsImportingToX86(flags))
            //{
            //    AsmBldr.Save(asmFileName, PortableExecutableKinds.ILOnly | PortableExecutableKinds.Required32Bit, 
            //        ImageFileMachine.I386);
            //}
            //else if (IsImportingToArm(flags))
            //{
            //    AsmBldr.Save(asmFileName, PortableExecutableKinds.ILOnly, 
            //        ImageFileMachine.ARM);
            //}
            //else
            //{
            //    // Default is agnostic
            //    AsmBldr.Save(asmFileName);        
            //}

            return AsmBldr;
        }

        //**************************************************************************
        // Helper to get a PIA from a typelib.
        //**************************************************************************
        internal static bool GetPrimaryInteropAssembly(TypeLib typeLib, out string asmName, out string asmCodeBase)
        {
            TypeLibAttr typeLibAttr = typeLib.GetLibAttr();

            string strTlbId = "{" + typeLibAttr.Guid.ToString().ToUpper(CultureInfo.InvariantCulture) + "}";
            string strVersion = typeLibAttr.MajorVerNum.ToString("x", CultureInfo.InvariantCulture) + "." + typeLibAttr.MinorVerNum.ToString("x", CultureInfo.InvariantCulture);

            // Set the two out values to null before we start.
            asmName = null;
            asmCodeBase = null;

            // Try to open the HKEY_CLASS_ROOT\TypeLib key.
            using (RegistryKey TypeLibKey = Registry.ClassesRoot.OpenSubKey("TypeLib", false))
            {
                if (TypeLibKey != null)
                {
                    // Try to open the HKEY_CLASS_ROOT\TypeLib\<TLBID> key.            
                    using (RegistryKey TypeLibSubKey = TypeLibKey.OpenSubKey(strTlbId))
                    {
                        if (TypeLibSubKey != null)
                        {
                            // Try to open the HKEY_CLASS_ROOT\TypeLib\<TLBID>\<Major.Minor> key.
                            using (RegistryKey VersionKey = TypeLibSubKey.OpenSubKey(strVersion, false))
                            {
                                if (VersionKey != null)
                                {
                                    // Attempt to retrieve the assembly name and codebase under the version key.
                                    asmName = (string)VersionKey.GetValue("PrimaryInteropAssemblyName");
                                    asmCodeBase = (string)VersionKey.GetValue("PrimaryInteropAssemblyCodeBase");
                                }
                            }
                        }
                    }
                }
            }

            // If the assembly name isn't null, then we found an PIA.
            return asmName != null;
        }

        private static bool IsPrimaryInteropAssembly(Assembly asm)
        {
            // Retrieve the list of PIA attributes.
            IList<CustomAttributeData> aPIAAttrs = CustomAttributeData.GetCustomAttributes(asm);
            int count = aPIAAttrs.Count;

            for (int i = 0; i < count; i++)
            {
                if (aPIAAttrs[i].Constructor.DeclaringType == typeof(PrimaryInteropAssemblyAttribute))
                {
                    return true;
                }
            }

            return false;
        }

        internal static bool ValidateMachineType(TypeLibImporterFlags flags, SYSKIND syskind)
        {
            int count = 0;
            if (IsImportingToX86(flags))
                count++;
            if (IsImportingToX64(flags))
                count++;
            if (IsImportingToItanium(flags))
                count++;
            if (IsImportingToArm(flags))
                count++;
            if (IsImportingToAgnostic(flags))
                count++;

            if (count > 1)
            {
                Output.WriteError(Resource.FormatString("Err_BadMachineSwitch"), ErrorCode.Err_BadMachineSwitch);
                return false;
            }


            // Check the import type against the type of the type library.
            if (syskind == SYSKIND.SYS_WIN64)
            {
                // If x86 or ARM was chosen, throw an error.
                if (IsImportingToX86(flags) || IsImportingToArm(flags))
                {
                    Output.WriteError(Resource.FormatString("Err_BadMachineSwitch"), ErrorCode.Err_BadMachineSwitch);
                    return false;
                }

                // If nothing is chosen, output a warning on all platforms.
                if (IsImportingToDefault(flags))
                {
                    Output.WriteWarning(Resource.FormatString("Wrn_AgnosticAssembly"), WarningCode.Wrn_AgnosticAssembly);
                }
            }
            else if (syskind == SYSKIND.SYS_WIN32)
            {
                // If a 64-bit option was chosen, throw an error.
                if (IsImportingToItanium(flags) || IsImportingToX64(flags))
                {
                    Output.WriteError(Resource.FormatString("Err_BadMachineSwitch"), ErrorCode.Err_BadMachineSwitch);
                    return false;
                }

#if WIN64
            // If nothing is chosen, and we're on a 64-bit machine, output a warning
            if (IsImportingToDefault(flags))
            {
                Output.WriteWarning(Resource.FormatString("Wrn_AgnosticAssembly"), WarningCode.Wrn_AgnosticAssembly);
            }
#endif

            }

            return true;
        }

        internal static bool IsImportingToItanium(TypeLibImporterFlags flags)
        {
            return ((flags & TypeLibImporterFlags.ImportAsItanium) != 0);
        }

        internal static bool IsImportingToX64(TypeLibImporterFlags flags)
        {
            return ((flags & TypeLibImporterFlags.ImportAsX64) != 0);
        }

        internal static bool IsImportingToX86(TypeLibImporterFlags flags)
        {
            return ((flags & TypeLibImporterFlags.ImportAsX86) != 0);
        }

        internal static bool IsImportingToArm(TypeLibImporterFlags flags)
        {
            return ((flags & TypeLibImporterFlags.ImportAsArm) != 0);
        }

        internal static bool IsImportingToAgnostic(TypeLibImporterFlags flags)
        {
            return ((flags & TypeLibImporterFlags.ImportAsAgnostic) != 0);
        }

        internal static bool IsImportingToDefault(TypeLibImporterFlags flags)
        {
            return !(IsImportingToItanium(flags) || IsImportingToX64(flags) || IsImportingToX86(flags) || IsImportingToArm(flags) || IsImportingToAgnostic(flags));
        }

        internal static TlbImpOptions s_Options;

        // List of libraries being imported, as guids.
        internal static Hashtable s_ImportingLibraries = new Hashtable();

        // List of libraries that have been imported, as guids.
        internal static Hashtable s_AlreadyImportedLibraries = new Hashtable();

        // Assembly references provided on the command line via /reference; keyed by guid.
        internal static Hashtable s_AssemblyRefs = new Hashtable();

        // Array of type libraries just to keep the references alive.
        private static Dictionary<string, TypeLib> s_RefTypeLibraries = new Dictionary<string, TypeLib>();

        //******************************************************************************
        // The resolution callback class.
        //******************************************************************************
        private class ImporterCallback : ITypeLibImporterNotifySink, IDisposable
        {
            //private readonly TypeLoader typeLoader = new TypeLoader();
            private bool isDisposed = false;

            public void Dispose()
            {
                if (this.isDisposed)
                {
                    return;
                }

                //this.typeLoader.Dispose();

                this.isDisposed = true;
            }

            public void ReportEvent(ImporterEventKind EventKind, int EventCode, String EventMsg)
            {
                if (EventKind == ImporterEventKind.NOTIF_TYPECONVERTED)
                {
                    if (TypeLibConverter.s_Options.VerboseMode)
                        Output.WriteInfo(EventMsg, (MessageCode)EventCode);
                }
                else if (EventKind == ImporterEventKind.NOTIF_CONVERTWARNING)
                {
                    Output.WriteWarning(EventMsg, (WarningCode)EventCode);
                }
                else
                {
                    Output.Write(EventMsg);
                }
            }

            public Assembly ResolveRef(TypeLib typeLib)
            {
                // Display a message indicating we are resolving a reference.
                if (TypeLibConverter.s_Options.VerboseMode)
                {
                    Output.WriteInfo(Resource.FormatString("Msg_ResolvingRef", typeLib.GetDocumentation()), MessageCode.Msg_ResolvingRef);
                }

                bool generatePIA = TypeLibConverter.s_Options.Flags.HasFlag(TypeLibImporterFlags.PrimaryInteropAssembly);

                TypeLibAttr typeLibAttr = typeLib.GetLibAttr();

                // Check our list of referenced assemblies.
                Assembly assemblyRef = (Assembly)TypeLibConverter.s_AssemblyRefs[typeLibAttr.Guid];
                if (assemblyRef != null)
                {
                    // PIA should only reference PIA
                    if (generatePIA && !TypeLibConverter.IsPrimaryInteropAssembly(assemblyRef))
                    {
                        throw new TlbImpGeneralException(Resource.FormatString("Err_ReferencedPIANotPIA", assemblyRef.GetName().Name), ErrorCode.Err_ReferencedPIANotPIA);
                    }

                    // If we are in verbose mode then display message indicating we successfully resolved the assembly 
                    // from the list of referenced assemblies.
                    if (TypeLibConverter.s_Options.VerboseMode)
                    {
                        Output.WriteInfo(Resource.FormatString("Msg_RefFoundInAsmRefList", typeLib.GetDocumentation(), assemblyRef.GetName().Name), MessageCode.Msg_RefFoundInAsmRefList);
                    }

                    return assemblyRef;
                }

                // If the assembly wasn't provided on the command line and the user
                // doesn't want us touching the registry for PIAs, throw an error now.
                if (TypeLibConverter.s_Options.StrictRefNoPia)
                {
                    throw new TlbImpGeneralException(Resource.FormatString("Err_RefNotInList", typeLib.GetDocumentation()), ErrorCode.Err_RefNotInList);
                }

                // Look for a primary interop assembly for the typelib.
                string piaName;
                string piaCodeBase;
                if (TypeLibConverter.GetPrimaryInteropAssembly(typeLib, out piaName, out piaCodeBase))
                {
                    if (piaName != null)
                    {
                        piaName = AppDomain.CurrentDomain.ApplyPolicy(piaName);
                    }

                    // Load the primary interop assembly.
                    try
                    {
                        // First try loading the assembly using its full name.
                        //assemblyRef = this.typeLoader.LoadFromAssemblyName(piaName);
                    }
                    catch (FileNotFoundException)
                    {
                        // If that failed, try loading it using LoadFrom bassed on the codebase if specified.
                        if (piaCodeBase != null)
                        {
                            //assemblyRef = this.typeLoader.LoadFromAssemblyName(piaCodeBase);
                        }
                    }
                    catch (FileLoadException)
                    {
                        // If that failed, try loading it using LoadFrom bassed on the codebase if specified.
                        if (piaCodeBase != null)
                        {
                            //assemblyRef = this.typeLoader.LoadFromAssemblyName(piaCodeBase);
                        }
                    }

                    if (assemblyRef != null)
                    {
                        // Validate that the assembly is indeed a PIA.
                        if (!TypeLibConverter.IsPrimaryInteropAssembly(assemblyRef))
                        {
                            throw new TlbImpGeneralException(Resource.FormatString("Err_RegisteredPIANotPIA", assemblyRef.GetName().Name, typeLib.GetDocumentation()), ErrorCode.Err_RegisteredPIANotPIA);
                        }

                        // If we are in verbose mode then display message indicating we successfully resolved the PIA.
                        if (TypeLibConverter.s_Options.VerboseMode)
                        {
                            Output.WriteInfo(Resource.FormatString("Msg_ResolvedRefToPIA", typeLib.GetDocumentation(), assemblyRef.GetName().Name), MessageCode.Msg_ResolvedRefToPIA);
                        }

                        return assemblyRef;
                    }
                }

                // If we are generating a primary interop assembly or if strict ref mode
                // is enabled, then the resolve ref has failed.
                if (generatePIA)
                {
                    throw new TlbImpGeneralException(Resource.FormatString("Err_NoPIARegistered", typeLib.GetDocumentation()), ErrorCode.Err_NoPIARegistered);
                }

                if (TypeLibConverter.s_Options.StrictRef)
                {
                    throw new TlbImpGeneralException(Resource.FormatString("Err_RefNotInList", typeLib.GetDocumentation()), ErrorCode.Err_RefNotInList);
                }


                //----------------------------------------------------------------------
                // See if this has already been imported.

                assemblyRef = (Assembly)TypeLibConverter.s_AlreadyImportedLibraries[typeLibAttr.Guid];
                if (assemblyRef != null)
                {
                    // If we are in verbose mode then display message indicating we successfully resolved the assembly 
                    // from the list of referenced assemblies.
                    if (TypeLibConverter.s_Options.VerboseMode)
                        Output.WriteInfo(Resource.FormatString("Msg_AssemblyResolved", typeLib.GetDocumentation()), MessageCode.Msg_AssemblyResolved);

                    return assemblyRef;
                }


                // Try to load the assembly.
                bool bExistingAsmLoaded = false;
                string absPathToFile = Path.Combine(TypeLibConverter.s_Options.OutputDir, typeLib.GetDocumentation() + ".dll");
                try
                {
                    // Check to see if we've already built the assembly.
                    //assemblyRef = this.typeLoader.LoadFromAssemblyName(absPathToFile);

                    // Remember we loaded an existing assembly.
                    bExistingAsmLoaded = true;

                    // Make sure the assembly is for the current typelib and that the version number of the 
                    // loaded assembly is the same as the version number of the typelib.
                    Version asmVersion = assemblyRef.GetName().Version;
                    if (assemblyRef.GetTypeLibGuidForAssembly() == typeLibAttr.Guid
                        && ((asmVersion.Major == typeLibAttr.MajorVerNum && asmVersion.Minor == typeLibAttr.MinorVerNum)
                            || (asmVersion.Major == 0 && asmVersion.Minor == 0 && typeLibAttr.MajorVerNum == 1 && typeLibAttr.MinorVerNum == 0)))
                    {
                        // If we are in verbose mode then display message indicating we successfully loaded the assembly.
                        if (TypeLibConverter.s_Options.VerboseMode)
                        {
                            Output.WriteInfo(Resource.FormatString("Msg_AssemblyLoaded", absPathToFile), MessageCode.Msg_AssemblyLoaded);
                        }

                        // Remember the loaded assembly.
                        TypeLibConverter.s_AlreadyImportedLibraries[typeLibAttr.Guid] = assemblyRef;
                        return assemblyRef;
                    }
                    else if (TypeLibConverter.s_Options.VerboseMode)
                    {
                        // If we are in verbose mode then display message indicating we found an assembly that doesn't match
                        Output.WriteInfo(Resource.FormatString("Msg_AsmRefLookupMatchProblem", new object[] {
                            absPathToFile,
                            assemblyRef.GetTypeLibGuidForAssembly(),
                            asmVersion.Major,
                            asmVersion.Minor,
                            typeLib.GetDocumentation(),
                            typeLibAttr.Guid,
                            typeLibAttr.MajorVerNum,
                            typeLibAttr.MinorVerNum }), MessageCode.Msg_AsmRefLookupMatchProblem);
                    }
                }
                catch (FileNotFoundException)
                {
                    // This is actually great, just fall through to create the new file.
                }


                // Make sure an existing assembly will not be overwritten by the 
                // assembly generated by the typelib being imported.
                if (bExistingAsmLoaded)
                {
                    throw new TlbImpGeneralException(Resource.FormatString("Err_ExistingAsmOverwrittenByRefAsm", typeLib.GetDocumentation(), absPathToFile), ErrorCode.Err_ExistingAsmOverwrittenByRefAsm);
                }


                // Make sure the current assembly will not be overriten by the 
                // assembly generated by the typelib being imported.
                if (string.Compare(absPathToFile, TypeLibConverter.s_Options.AssemblyName, true /*ignoreCase*/, CultureInfo.InvariantCulture) == 0)
                {
                    throw new TlbImpGeneralException(Resource.FormatString("Err_RefAsmOverwrittenByOutput", typeLib.GetDocumentation(), absPathToFile), ErrorCode.Err_RefAsmOverwrittenByOutput);
                }

                // See if this is already on the stack.
                if (TypeLibConverter.s_ImportingLibraries.Contains(typeLibAttr.Guid.ToString()))
                {
                    // Print an error message and return null to stop importing the current type but
                    // continue with the rest of the import.
                    Output.WriteWarning(Resource.FormatString("Wrn_CircularReference", typeLib.GetDocumentation()), WarningCode.Wrn_CircularReference);
                    return null;
                }


                // If we have not managed to load the assembly then import the typelib.
                if (TypeLibConverter.s_Options.VerboseMode)
                {
                    Output.WriteInfo(Resource.FormatString("Msg_AutoImportingTypeLib", typeLib.GetDocumentation(), absPathToFile), MessageCode.Msg_AutoImportingTypeLib);
                }

                try
                {
                    assemblyRef = TypeLibConverter.DoImport(typeLib,
                                            absPathToFile,
                                            null,
                                            null,
                                            TypeLibConverter.s_Options.PublicKey,
                                            TypeLibConverter.s_Options.KeyPair,
                                            TypeLibConverter.s_Options.Product,
                                            TypeLibConverter.s_Options.ProductVersion,
                                            TypeLibConverter.s_Options.Company,
                                            TypeLibConverter.s_Options.Copyright,
                                            TypeLibConverter.s_Options.Trademark,
                                            TypeLibConverter.s_Options.Flags,
                                            TypeLibConverter.s_Options.ConvertVariantBoolFieldToBool,
                                            TypeLibConverter.s_Options.UseLegacy35QuirksMode);

                    // The import could fail. In this case, 
                    if (assemblyRef == null)
                    {
                        return null;
                    }

                    // Remember the imported assembly.
                    TypeLibConverter.s_AlreadyImportedLibraries[typeLibAttr.Guid] = assemblyRef;
                }
                catch (ReflectionTypeLoadException e)
                {
                    // Display the type load exceptions that occurred and rethrow the exception.
                    int i;
                    Exception[] exceptions;
                    Output.WriteError(Resource.FormatString("Err_TypeLoadExceptions"), ErrorCode.Err_TypeLoadExceptions);
                    exceptions = e.LoaderExceptions;
                    for (i = 0; i < exceptions.Length; i++)
                    {
                        try
                        {
                            Output.WriteInfo(
                                Resource.FormatString(
                                    "Msg_DisplayException",
                                    new object[] { i, exceptions[i].GetType().ToString(), exceptions[i].Message }),
                                MessageCode.Msg_DisplayException);
                        }
                        catch (Exception ex)
                        {
                            Output.WriteInfo(
                                Resource.FormatString(
                                    "Msg_DisplayNestedException",
                                    new object[] { i, ex.GetType().ToString(), ex.Message }),
                                MessageCode.Msg_DisplayNestedException);
                        }
                    }

                    throw e;
                }

                return assemblyRef;
            }
        }
    }
}
