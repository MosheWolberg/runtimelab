// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

using System;
using System.Runtime.InteropServices;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Resources;
using System.Collections;
using System.Globalization;
using System.Threading;
using System.Security;
using System.Collections.Generic;
using TypeLibUtilities.TypeLibAPI;

namespace TypeLibUtilities
{

internal class TlbImp
{
    private const int SuccessReturnCode = 0;
    private const int ErrorReturnCode = 100;
    private const int MAX_PATH = 260;

    public static int Main(String []aArgs)
    {
        int retCode = SuccessReturnCode;
        try
        {
            try
            {
                SetConsoleUI();

                // Parse the command line arguments.
                if (!ParseArguments(aArgs, ref s_Options, ref retCode))
                    return retCode;

                PrintLogo();

                retCode = Run();
            }
            catch (TlbImpGeneralException tge)
            {
                if (tge.NeedToPrintLogo)
                    PrintLogo();

                Output.WriteTlbimpGeneralException(tge);
                retCode = ErrorReturnCode;
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

                retCode = ErrorReturnCode;
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

                retCode = ErrorReturnCode;
            }
            catch (Exception ex)
            {
                string msg = Resource.FormatString(
                    "Err_UnexpectedException",
                    ex.GetType().ToString(),
                    ex.Message
                    );
                Output.WriteError(msg, ErrorCode.Err_UnexpectedException);

                retCode = ErrorReturnCode;
            }
        }
        catch (TlbImpResourceNotFoundException ex)
        {
            Output.WriteError(ex.Message, ErrorCode.Err_ResourceNotFound);
            retCode = ErrorReturnCode;
        }

        return retCode;
    }

    public static int Run()
    {
        string TypeLibName = s_Options.TypeLibName;
        s_Options.TypeLibName = GetFullPath(s_Options.TypeLibName, true);
        if (s_Options.TypeLibName == null)
        {
            // We failed to find the typelib. This might be because a resource ID is specified
            // so let's have LoadTypeLibEx try to load it but remember that we failed to find it.
            s_Options.SearchPathSucceeded = false;
            s_Options.TypeLibName = TypeLibName;
        }
        else
        {
            // We found the typelib.
            s_Options.SearchPathSucceeded = true;
        }

        // Retrieve the full path name of the output file.
        if (s_Options.AssemblyName != null)
        {
            if (Directory.Exists(s_Options.AssemblyName))
                throw new TlbImpGeneralException(Resource.FormatString("Err_OutputCannotBeDirectory"), ErrorCode.Err_OutputCannotBeDirectory);

            if ("".Equals(Path.GetExtension(s_Options.AssemblyName)))
            {
                s_Options.AssemblyName = s_Options.AssemblyName + ".dll";
            }

            // Retrieve the full path name of the output file.
            try
            {
                s_Options.AssemblyName = (new FileInfo(s_Options.AssemblyName)).FullName;
            }
            catch (System.IO.PathTooLongException)
            {
                throw new TlbImpGeneralException(Resource.FormatString("Err_OutputFileNameTooLong", s_Options.AssemblyName), ErrorCode.Err_OutputFileNameTooLong);
            }
        }


        // Determine the output directory for the generated assembly.
        if (s_Options.AssemblyName != null)
        {
            // An output file has been provided so use its directory as the output directory.
            s_Options.OutputDir = Path.GetDirectoryName(s_Options.AssemblyName);
        }
        else
        {
            // No output file has been provided so use the current directory as the output directory.
            s_Options.OutputDir = Environment.CurrentDirectory;
        }

        if (!Directory.Exists(s_Options.OutputDir))
        {
            try
            {
                Directory.CreateDirectory(s_Options.OutputDir);
            }
            catch (System.IO.IOException)
            {
                throw new TlbImpGeneralException(Resource.FormatString("Err_InvalidOutputDirectory"), ErrorCode.Err_InvalidOutputDirectory);
            }
        }

        // If the output directory is different from the current directory then change to that directory.
        if (String.Compare(s_Options.OutputDir, Environment.CurrentDirectory, true, CultureInfo.InvariantCulture) != 0)
            Environment.CurrentDirectory = s_Options.OutputDir;

        return TypeLibConverter.Run(s_Options);
    }

    private static void SetConsoleUI()
    {
        Thread t = Thread.CurrentThread;
        
        t.CurrentUICulture = CultureInfo.CurrentUICulture.GetConsoleFallbackUICulture();

        if (Environment.OSVersion.Platform != PlatformID.Win32Windows)
        {        
            if ( (System.Console.OutputEncoding.CodePage != t.CurrentUICulture.TextInfo.OEMCodePage) &&
                 (System.Console.OutputEncoding.CodePage != t.CurrentUICulture.TextInfo.ANSICodePage))
            {
                t.CurrentUICulture = new CultureInfo("en-US");
            }
        }
    }

    private static bool ParseArguments(String []aArgs, ref TlbImpOptions Options, ref int ReturnCode)
    {
        CommandLine cmdLine;
        Option opt;
        bool delaysign = false;

        // Create the options object that will be returned.
        Options = new TlbImpOptions();

        // Parse the command line arguments using the command line argument parser.
        cmdLine = new CommandLine(aArgs, new String[] { "*out", "*publickey", "*keyfile", "*keycontainer", "delaysign", "*reference",
                                                        "unsafe", "nologo", "silent", "verbose", "+strictref", "primary", "*namespace", 
                                                        "*asmversion", "sysarray", "*transform", "?", "help", "*tlbreference",
                                                        "noclassmembers", "*machine", "*silence", "*product", "*productversion", 
                                                        "*company", "*copyright", "*trademark", "variantboolfieldtobool",
                                                        "legacy35"});

        // Make sure there is at least one argument.
        if ((cmdLine.NumArgs + cmdLine.NumOpts) < 1)
        {
            PrintUsage();
            ReturnCode = SuccessReturnCode;
            return false;
        }

        List<string> assemblyRefList = new List<string>();
        List<string> typeLibRefList = new List<string>();

        // Get the name of the COM typelib.
        Options.TypeLibName = cmdLine.GetNextArg();

        // Go through the list of options.
        while ((opt = cmdLine.GetNextOption()) != null)
        {
            // Determine which option was specified.
            if (opt.Name.Equals("out"))
            {
                Options.AssemblyName = opt.Value;
            }
            else if (opt.Name.Equals("namespace"))
            {
                Options.AssemblyNamespace = opt.Value;
            }
            else if (opt.Name.Equals("asmversion"))
            {
                try
                {
                    Options.AssemblyVersion = new Version(opt.Value);
                }
                catch(Exception)
                {
                    throw new TlbImpGeneralException(Resource.FormatString("Err_InvalidVersion"), ErrorCode.Err_InvalidVersion, true);
                }
            }
            else if (opt.Name.Equals("reference"))
            {
                String FullPath = null;
                
                FullPath = GetFullPath(opt.Value, false);
                
                if (FullPath == null)
                {
                    ReturnCode = ErrorReturnCode;
                    return false;
                }

                if (Options.AssemblyRefList == null)
                {
                    assemblyRefList.Clear();
                    assemblyRefList.Add(FullPath.ToLower());
                    Options.AssemblyRefList = FullPath;
                }
                else
                {
                    if (!assemblyRefList.Contains(FullPath.ToLower())) {
                        assemblyRefList.Add(FullPath.ToLower());
                        Options.AssemblyRefList =
                            Options.AssemblyRefList + ";" + FullPath;
                    }
                }
            }
            else if (opt.Name.Equals("tlbreference"))
            {
                String FullPath = null;
                
                FullPath = GetFullPath(opt.Value, false);
                if (FullPath == null)
                {
                    ReturnCode = ErrorReturnCode;
                    return false;
                }

                if (Options.TypeLibRefList == null) {
                    typeLibRefList.Clear();
                    typeLibRefList.Add(FullPath.ToLower());
                    Options.TypeLibRefList = FullPath;
                }
                else
                {
                    if (!typeLibRefList.Contains(FullPath.ToLower()))
                    {
                        typeLibRefList.Add(FullPath.ToLower());
                        Options.TypeLibRefList =
                            Options.TypeLibRefList + ";" + FullPath;
                    }
                }
            }
            else if (opt.Name.Equals("delaysign"))
            {
                delaysign = true;
            }
            else if (opt.Name.Equals("publickey"))
            {
                if (Options.KeyPair != null || Options.PublicKey != null)
                {
                    throw new TlbImpGeneralException(Resource.FormatString("Err_TooManyKeys"), ErrorCode.Err_TooManyKeys, true);
                }
                // Read data from binary file into byte array.
                byte[] aData;
                FileStream fs = null;
                try
                {
                    fs = new FileStream(opt.Value, FileMode.Open, FileAccess.Read, FileShare.Read);
                    int iLength = (int)fs.Length;
                    aData = new byte[iLength];
                    fs.Read(aData, 0, iLength);
                }
                catch (Exception ex)
                {
                    throw new TlbImpGeneralException(Resource.FormatString("Err_ErrorWhileOpenningFile", new object[] { opt.Value, ex.GetType().ToString(), ex.Message }), ErrorCode.Err_ErrorWhileOpenningFile, true);
                }
                finally
                {
                    if (fs != null)
                        fs.Close();
                }
                Options.PublicKey = aData;
            }
            else if (opt.Name.Equals("keyfile"))
            {
                if (Options.KeyPair != null || Options.PublicKey != null)
                {
                    throw new TlbImpGeneralException(Resource.FormatString("Err_TooManyKeys"), ErrorCode.Err_TooManyKeys, true);
                }
                
                // Read data from binary file into byte array.
                byte[] aData;
                FileStream fs = null;
                try
                {
                    fs = new FileStream(opt.Value, FileMode.Open, FileAccess.Read, FileShare.Read);
                    int iLength = (int)fs.Length;
                    aData = new byte[iLength];
                    fs.Read(aData, 0, iLength);
                }
                catch (Exception ex)
                {
                    throw new TlbImpGeneralException(Resource.FormatString("Err_ErrorWhileOpenningFile", new object[] { opt.Value, ex.GetType().ToString(), ex.Message }), ErrorCode.Err_ErrorWhileOpenningFile, true);
                }
                finally
                {
                    if (fs != null)
                        fs.Close();
                }
                Options.KeyPair = new StrongNameKeyPair(aData);
            }
            else if (opt.Name.Equals("keycontainer"))
            {
                if ((Options.KeyPair != null) || (Options.PublicKey != null))
                {
                    throw new TlbImpGeneralException(Resource.FormatString("Err_TooManyKeys"), ErrorCode.Err_TooManyKeys, true);
                }
                Options.KeyPair = new StrongNameKeyPair(opt.Value);
            }
            else if (opt.Name.Equals("unsafe"))
            {
                Options.Flags |= TypeLibImporterFlags.UnsafeInterfaces;
            }
            else if (opt.Name.Equals("primary"))
            {
                Options.Flags |= TypeLibImporterFlags.PrimaryInteropAssembly;
            }
            else if (opt.Name.Equals("sysarray"))
            {
                Options.Flags |= TypeLibImporterFlags.SafeArrayAsSystemArray;
            }
            else if (opt.Name.Equals("nologo"))
            {
                Options.NoLogo = true;
            }
            else if (opt.Name.Equals("silent"))
            {
                Output.SetSilent(true);
                Options.SilentMode = true;
            }
            else if (opt.Name.Equals("silence"))
            {
                // For compatability with previous command lines, we must support parsing warning numbers
                // in hex.
                int warningNumber = int.Parse(opt.Value, System.Globalization.NumberStyles.HexNumber);
                Output.Silence(warningNumber);
                Options.SilenceList.Add(warningNumber);

                // Warning numbers are displayed in decimal, so we want to parse the given argument in decimal
                // if the numeric string could be interpreted as a valid decimal number
                if (Int32.TryParse(opt.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out warningNumber))
                {
                    Output.Silence(warningNumber);
                    Options.SilenceList.Add(warningNumber);
                }
            }
            else if (opt.Name.Equals("verbose"))
            {
                Options.VerboseMode = true;
            }
            else if (opt.Name.Equals("noclassmembers"))
            {
                Options.Flags |= TypeLibImporterFlags.PreventClassMembers;
            }
            else if (opt.Name.Equals("strictref"))
            {
                if (opt.Value != null)
                {
                    if (String.Compare(opt.Value, "nopia", true) == 0)
                    {
                        Options.StrictRefNoPia = true;
                    }
                    else
                    {
                        throw new TlbImpGeneralException(Resource.FormatString("Err_UnknownStrictRefOpt", opt.Value), ErrorCode.Err_UnknownStrictRefOpt, true);
                    }
                }
                else
                    Options.StrictRef = true;
            }
            else if (opt.Name.Equals("transform"))
            {
                if (opt.Value.ToLower(CultureInfo.InvariantCulture) == "dispret")
                {
                    Options.Flags |= TypeLibImporterFlags.TransformDispRetVals;
                }
                else if (opt.Value.ToLower(CultureInfo.InvariantCulture) == "serializablevalueclasses")
                {
                    Options.Flags |= TypeLibImporterFlags.SerializableValueClasses;
                }
                else
                {
                    throw new TlbImpGeneralException(Resource.FormatString("Err_InvalidTransform", opt.Value), ErrorCode.Err_InvalidTransform, true);
                }
            }
            else if (opt.Name.Equals("machine"))
            {
                if (opt.Value.ToLower(CultureInfo.InvariantCulture) == "itanium")
                {
                    Options.Flags |= TypeLibImporterFlags.ImportAsItanium;
                }
                else if (opt.Value.ToLower(CultureInfo.InvariantCulture) == "x64")
                {
                    Options.Flags |= TypeLibImporterFlags.ImportAsX64;
                }
                else if (opt.Value.ToLower(CultureInfo.InvariantCulture) == "x86")
                {
                    Options.Flags |= TypeLibImporterFlags.ImportAsX86;
                }
                else if (opt.Value.ToLower(CultureInfo.InvariantCulture) == "arm")
                {
                    Options.Flags |= TypeLibImporterFlags.ImportAsArm;
                }
                else if (opt.Value.ToLower(CultureInfo.InvariantCulture) == "agnostic")
                {
                    Options.Flags |= TypeLibImporterFlags.ImportAsAgnostic;
                }
                else
                {
                    throw new TlbImpGeneralException(Resource.FormatString("Err_InvalidMachine", opt.Value), ErrorCode.Err_InvalidMachine, true);
                }
            }
            else if (opt.Name.Equals("product"))
            {
                Options.Product = opt.Value;
            }
            else if (opt.Name.Equals("productversion"))
            {
                Options.ProductVersion = opt.Value;
            }
            else if (opt.Name.Equals("company"))
            {
                Options.Company = opt.Value;
            }
            else if (opt.Name.Equals("copyright"))
            {
                Options.Copyright = opt.Value;
            }
            else if (opt.Name.Equals("trademark"))
            {
                Options.Trademark = opt.Value;
            }
            else if (opt.Name.Equals("?") || opt.Name.Equals("help"))
            {
                PrintUsage();
                ReturnCode = SuccessReturnCode;
                return false;
            }
            else if (opt.Name.Equals("variantboolfieldtobool"))
            {
                Options.ConvertVariantBoolFieldToBool = true;
            }
            else if (opt.Name.Equals("legacy35"))
            {
                Options.UseLegacy35QuirksMode = true;
            }
        }

        // Validate that the typelib name has been specified.
        if (Options.TypeLibName == null)
        {
            throw new TlbImpGeneralException(Resource.FormatString("Err_NoInputFile"), ErrorCode.Err_NoInputFile, true);
        }

        // Gather information needed for strong naming the assembly (if
        // the user desires this).
        if ((Options.KeyPair != null) && (Options.PublicKey == null))
        {
            try
            {
                Options.PublicKey = Options.KeyPair.PublicKey;
            }
            catch
            {
                throw new TlbImpGeneralException(Resource.FormatString("Err_InvalidStrongName"), ErrorCode.Err_InvalidStrongName, true);
            }
        }

        if (delaysign && Options.KeyPair != null)
            Options.KeyPair = null;

        // To be able to generate a PIA, we must also be strong naming the assembly.
        if ((Options.Flags & TypeLibImporterFlags.PrimaryInteropAssembly) != 0)
        {
            if (Options.PublicKey == null && Options.KeyPair == null)
            {
                throw new TlbImpGeneralException(Resource.FormatString("Err_PIAMustBeStrongNamed"), ErrorCode.Err_PIAMustBeStrongNamed, true);
            }
        }

        return true;
    }
    
    private static string GetFullPath(string fileName, bool isInputFile)
    {
        // Try resolving the partial path (or if we just got a filename, the current path)
        //  to a full path and check for the file.
        var fileInfo = new FileInfo(Path.GetFullPath(fileName));
        if (!fileInfo.Exists && !isInputFile)
        {
            throw new TlbImpGeneralException(Resource.FormatString("Err_ReferenceNotFound", fileName), ErrorCode.Err_ReferenceNotFound, true);
        }

        if (s_Options.VerboseMode)
        {
            Output.WriteInfo(Resource.FormatString("Msg_ResolvedFile", fileName, fileInfo.FullName), MessageCode.Msg_ResolvedFile);
        }
        
        return fileInfo.FullName;
    }

    private static void PrintLogo()
    {
        if (!s_Options.NoLogo)
        {
            Output.Write(Resource.FormatString("Msg_Copyright", Assembly.GetExecutingAssembly().ImageRuntimeVersion));
        }
    }

    private static void PrintUsage()
    {
        PrintLogo();

        string resNameBase = "Msg_Usage_";
        string outputStr = "temp";
        string resName;
        int index = 0;

        while (outputStr != null)
        {
            if (index < 10)
                resName = resNameBase + "0" + index;
            else
                resName = resNameBase + index;

            outputStr = Resource.GetStringIfExists(resName);

            if (outputStr != null)
                Output.Write(outputStr);
            
            index++;
        }
    }

    internal static TlbImpOptions s_Options = null;
}

}
