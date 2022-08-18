// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

using System;

namespace TypeLibUtilities
{
    /// <summary>
    /// Used to signal that a resolve ref operation has failed
    /// </summary>
    public class TlbImpResolveRefFailWrapperException : ApplicationException
    {
        internal TlbImpResolveRefFailWrapperException(Exception ex)
            : base(string.Empty, ex)
        {
        }
    }

    /// <summary>
    /// Used to signal that something went wrong in the type conversion process 
    /// </summary>
    public class TlbImpInvalidTypeConversionException : ApplicationException
    {
        internal TlbImpInvalidTypeConversionException(TypeInfo typeInfo)
        {
            // [TODO] Can this throw?
            this.TypeName = typeInfo.GetDocumentation();
        }

        public string TypeName { get; private set; }
    }

    /// <summary>
    /// The Exception class contain the error ID and whether we need to print out the logo information
    /// </summary>
    public class TlbImpGeneralException : ApplicationException
    {
        internal TlbImpGeneralException(string str, ErrorCode errorId)
            : this(str, errorId, needToPrintLogo : false)
        {
        }

        internal TlbImpGeneralException(string str, ErrorCode errorId, bool needToPrintLogo)
            : base(str)
        {
            this.ErrorId = errorId;
            this.NeedToPrintLogo = needToPrintLogo;
        }

        public ErrorCode ErrorId { get; private set; }

        public bool NeedToPrintLogo { get; private set; }
    }

    /// <summary>
    /// The resource cannot be found
    /// </summary>
    public class TlbImpResourceNotFoundException : ApplicationException
    {
        internal TlbImpResourceNotFoundException(string msg)
            : base(msg)
        {
        }
    }
}
