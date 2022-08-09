// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Reflection.Emit.Experimental
{
     // Summary:
    //     Creates or associates parameter information.
    public class ParameterBuilder
    {
        internal ParameterBuilder(int sequence, ParameterAttributes attributes, string? paramName)
        {
            _position = sequence;
            _name = paramName;
            _attributes = attributes;
        }

        // Summary:
        //     Retrieves the attributes for this parameter.
        //
        // Returns:
        //     Read-only. Retrieves the attributes for this parameter.
        public virtual ParameterAttributes Attributes => _attributes;
        // Summary:
        //     Retrieves whether this is an input parameter.
        //
        // Returns:
        //     Read-only. Retrieves whether this is an input parameter.
        public bool IsIn => (_attributes & ParameterAttributes.In) != 0;

        // Summary:
        //     Retrieves whether this parameter is optional.
        //
        // Returns:
        //     Read-only. Specifies whether this parameter is optional.
        public bool IsOptional => (_attributes & ParameterAttributes.Optional) != 0;

        // Summary:
        //     Retrieves whether this parameter is an output parameter.
        //
        // Returns:
        //     Read-only. Retrieves whether this parameter is an output parameter.
        public bool IsOut => (_attributes & ParameterAttributes.Out) != 0;

        // Summary:
        //     Retrieves the name of this parameter.
        //
        // Returns:
        //     Read-only. Retrieves the name of this parameter.
        public virtual string? Name => _name;

        // Summary:
        //     Retrieves the signature position for this parameter.
        //
        // Returns:
        //     Read-only. Retrieves the signature position for this parameter.
        public virtual int Position => _position;

        // Summary:
        //     Sets the default value of the parameter.
        //
        // Parameters:
        //   defaultValue:
        //     The default value of this parameter.
        //
        // Exceptions:
        //   T:System.ArgumentException:
        //     The parameter is not one of the supported types. -or- The type of defaultValue
        //     does not match the type of the parameter. -or- The parameter is of type System.Object
        //     or other reference type, defaultValue is not null, and the value cannot be assigned
        //     to the reference type.
        public virtual void SetConstant(object? defaultValue)
        {
            _defaultValue = defaultValue;
        }

        // Summary:
        //     Set a custom attribute using a specified custom attribute blob.
        //
        // Parameters:
        //   con:
        //     The constructor for the custom attribute.
        //
        //   binaryAttribute:
        //     A byte blob representing the attributes.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     con or binaryAttribute is null.
        public void SetCustomAttribute(ConstructorInfo con, byte[] binaryAttribute)
        {
            throw new NotImplementedException();
        }

        // Summary:
        //     Set a custom attribute using a custom attribute builder.
        //
        // Parameters:
        //   customBuilder:
        //     An instance of a helper class to define the custom attribute.
        //
        // Exceptions:
        //   T:System.ArgumentNullException:
        //     con is null.
        public void SetCustomAttribute(CustomAttributeBuilder customBuilder)
        {
            throw new NotImplementedException();
        }

        private readonly string? _name;
        private readonly int _position;
        private readonly ParameterAttributes _attributes;
        internal object? _defaultValue;
    }
}
