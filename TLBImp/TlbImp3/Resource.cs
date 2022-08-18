// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
//

using System;
using System.Resources;
using System.Reflection;

namespace TypeLibUtilities
{

internal class Resource
{
    // For string resources located in a file:
    private static ResourceManager _resmgr;
    
    private static void InitResourceManager()
    {
        if(_resmgr == null)
        {
            _resmgr = new ResourceManager("TlbImp3.Resources", 
                                          Assembly.GetExecutingAssembly());
        }
    }
    
    internal static String GetString(String key)
    {
        string s = null;
        s = GetStringIfExists(key);
        
        if (s == null) 
            // We are not localizing this stringas this is for invalid resource scenario
            throw new TlbImpResourceNotFoundException("The required resource string cannot be found");

        return(s);
    }

    internal static String GetStringIfExists(String key)
    {
        String s;
        try
        {
            InitResourceManager();
            s = _resmgr.GetString(key, null);
        }
        catch (System.Exception)
        {
            return null;        	
        }

        return s;
    }
    
    internal static String FormatString(String key)
    {
        return(GetString(key));
    }
    
    internal static String FormatString(String key, Object a1)
    {
        return(String.Format(GetString(key), a1));
    }
    
    internal static String FormatString(String key, Object a1, Object a2)
    {
        return(String.Format(GetString(key), a1, a2));
    }

    internal static String FormatString(String key, Object[] a)
    {
        return (String.Format(System.Globalization.CultureInfo.CurrentCulture, GetString(key), a));
    }
}

}
