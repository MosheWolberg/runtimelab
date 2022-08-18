// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

// Error message format spec:
// Origin : [subcategory] category code : text
//      Origin: tool name, file name, or file(line,pos).
//          Must not be localized if it is a tool or file name.
//      Subcategory: optional and should be localized.
//      Category: "warning" or "error" and must not be localized.
//      Code: must not contain spaces and be non-localized.
//      Text: the localized error message
// e.g. cl : Command line warning D4024 : Unrecognized source file type

using System;
using System.Linq;
using System.Collections.Generic;

namespace TypeLibUtilities
{
    internal class Output
    {
        private static bool IsSilent = false;
        private static HashSet<int> CurrentSilenceList = new HashSet<int>();

        // Use this for a general error w.r.t. a file, like a missing file.
        public static void WriteError(string message, string fileName)
        {
            WriteError(message, fileName, 0);
        }

        // For specific errors about the contents of a file and you know where
        // the error occurred.
        public static void WriteError(string message, string fileName, int line, int column)
        {
            WriteError(message, fileName, line, column, 0);
        }

        // Use this for general resgen errors with no specific file info
        public static void WriteError(string message, ErrorCode errorCode)
        {
            WriteError(message, "TlbImp", (int)errorCode);
        }

        // Use this for a general error w.r.t. a file, like a missing file.
        public static void WriteError(string message, string fileName, int errorNumber)
        {
            Console.Error.WriteLine($"{fileName} : error TI{errorNumber:0000} : {message}");
        }

        // For specific errors about the contents of a file and you know where
        // the error occurred.
        public static void WriteError(string message, string fileName, int line, int column, int errorNumber)
        {
            Console.Error.WriteLine($"{fileName}({line},{column}): error TI{errorNumber:0000} : {message}");
        }

        public static void WriteError(string strPrefix, Exception e)
        {
            WriteError(strPrefix, e, 0);
        }

        public static void WriteError(string strPrefix, Exception e, ErrorCode errorCode)
        {
            string strErrorMsg = string.IsNullOrEmpty(strPrefix) ? string.Empty : strPrefix;

            strErrorMsg += e.GetType().ToString();
            if (e.Message != null)
            {
                strErrorMsg += " - " + e.Message;
            }

            if (e.InnerException != null)
            {
                strErrorMsg += " : " + e.InnerException.GetType().ToString();
                if (e.InnerException.Message != null)
                {
                    strErrorMsg += " - " + e.InnerException.Message;
                }
            }

            WriteError(strErrorMsg, errorCode);
        }

        public static void WriteTlbimpGeneralException(TlbImpGeneralException tge)
        {
            WriteError(tge.Message, tge.ErrorId);
        }

        public static void WriteError(Exception e)
        {
            WriteError(null, e);
        }

        public static void WriteError(Exception e, ErrorCode errorID)
        {
            WriteError(null, e, errorID);
        }

        // General warnings
        // Note that the warningNumber corresponds to an HRESULT in src\inc\corerror.xml
        public static void WriteWarning(string message, WarningCode warningCode)
        {
            if (!CheckIsSilent((int)warningCode))
            {
                Console.Error.WriteLine($"TlbImp : warning TI{(int)warningCode:0000} : {message}");
            }
        }

        public static void Write(string message)
        {
            if (!IsSilent)
            {
                Console.WriteLine(message);
            }
        }

        public static void WriteInfo(string message, MessageCode code)
        {
            if (!IsSilent)
            {
                // We've decided to still use TlbImp prefix to tell user that this message is outputed by TlbImp
                // and we hide the message code
                string messageFormat = "TlbImp : {0}";
                Console.WriteLine(messageFormat, message);
            }
        }

        public static void SetSilent(bool silent)
        {
            if (CurrentSilenceList.Any())
            {
                SilentExclusive();
            }

            IsSilent = silent;
        }

        public static void Silence(int warningNumber)
        {
            if (IsSilent)
            {
                SilentExclusive();
            }

            CurrentSilenceList.Add(warningNumber);
        }

        public static void Silence(IEnumerable<int> silenceList)
        {
            if (IsSilent && silenceList != null && silenceList.Any())
            {
                SilentExclusive();
            }

            if (silenceList != null)
            {
                CurrentSilenceList.UnionWith(silenceList);
            }
        }

        private static bool CheckIsSilent(int number)
        {
            return IsSilent || CurrentSilenceList.Contains(number);
        }

        private static void SilentExclusive()
        {
            throw new TlbImpGeneralException(Resource.FormatString("Err_SilentExclusive"), ErrorCode.Err_SilentExclusive);
        }
    }
}
