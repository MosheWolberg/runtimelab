// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace System.Reflection.Emit.Experimental
{
    // Summary:
    //     Defines and represents a constructor of a dynamic class.
    public sealed class ConstructorBuilder : ConstructorInfo
    {
        private readonly MethodBuilder _methodBuilder;
        internal MethodImplAttributes _methodImplAttributes;
        #region Constructor
        internal ConstructorBuilder(string name, MethodAttributes attributes, CallingConventions callingConvention,
            Type[]? parameterTypes, TypeBuilder type)
        {
            _methodBuilder = new MethodBuilder(name, attributes, callingConvention, null, null, type);
            Module = type.Module;
            Name = name;
            ReflectedType = type;
            DeclaringType = type;
            CallingConvention = callingConvention;
            Attributes = attributes;
        }

        #endregion

        // Summary:
        //     Holds a reference to the System.Type object from which this object was obtained.
        //
        // Returns:
        //     The Type object from which this object was obtained.
        public override Type? ReflectedType { get; }
        // Summary:
        //     Gets the dynamic module in which this constructor is defined.
        //
        // Returns:
        //     A System.Reflection.Module object that represents the dynamic module in which
        //     this constructor is defined.
        public override ModuleBuilder Module { get; }

        // Summary:
        //     Gets the internal handle for the method. Use this handle to access the underlying
        //     metadata handle.
        //
        // Returns:
        //     The internal handle for the method. Use this handle to access the underlying
        //     metadata handle.
        //
        // Exceptions:
        //   T:System.NotSupportedException:
        //     This property is not supported on this class.
        public override RuntimeMethodHandle MethodHandle => throw new NotImplementedException();

        // Summary:
        //     Gets a token that identifies the current dynamic module in metadata.
        //
        // Returns:
        //     An integer token that identifies the current module in metadata.
        public override int MetadataToken => throw new NotImplementedException();

        // Summary:
        //     Gets or sets whether the local variables in this constructor should be zero-initialized.
        //
        // Returns:
        //     Read/write. Gets or sets whether the local variables in this constructor should
        //     be zero-initialized.
        public bool InitLocals => throw new NotImplementedException();

        // Summary:
        //     Gets a reference to the System.Type object for the type that declares this member.
        //
        // Returns:
        //     The type that declares this member.
        public override Type? DeclaringType { get; }

        // Summary:
        //     Gets a System.Reflection.CallingConventions value that depends on whether the
        //     declaring type is generic.
        //
        // Returns:
        //     System.Reflection.CallingConventions.HasThis if the declaring type is generic;
        //     otherwise, System.Reflection.CallingConventions.Standard.
        public override CallingConventions CallingConvention { get; }

        // Summary:
        //     Gets the attributes for this constructor.
        //
        // Returns:
        //     The attributes for this constructor.
        public override MethodAttributes Attributes { get; }

        // Summary:
        //     Retrieves the name of this constructor.
        //
        // Returns:
        //     The name of this constructor.
        public override string Name { get; }

        // Summary:
        //     Defines a parameter of this constructor.
        //
        // Parameters:
        //   iSequence:
        //     The position of the parameter in the parameter list. Parameters are indexed beginning
        //     with the number 1 for the first parameter.
        //
        //   attributes:
        //     The attributes of the parameter.
        //
        //   strParamName:
        //     The name of the parameter. The name can be the null string.
        //
        // Returns:
        //     An object that represents the new parameter of this constructor.
        //
        // Exceptions:
        //   T:System.ArgumentOutOfRangeException:
        //     iSequence is less than 0 (zero), or it is greater than the number of parameters
        //     of the constructor.
        //
        //   T:System.InvalidOperationException:
        //     The containing type has been created using System.Reflection.Emit.TypeBuilder.CreateType.
        public ParameterBuilder DefineParameter(int iSequence, ParameterAttributes attributes, string? strParamName) => throw new NotImplementedException();

        // Summary:
        //     Returns the custom attributes identified by the given type.
        //
        // Parameters:
        //   attributeType:
        //     The custom attribute type.
        //
        //   inherit:
        //     Controls inheritance of custom attributes from base classes. This parameter is
        //     ignored.
        //
        // Returns:
        //     An object array that represents the attributes of this constructor.
        //
        // Exceptions:
        //   T:System.NotSupportedException:
        //     This method is not currently supported.
        public override object[] GetCustomAttributes(Type attributeType, bool inherit) => throw new NotImplementedException();

        // Summary:
        //     Returns all the custom attributes defined for this constructor.
        //
        // Parameters:
        //   inherit:
        //     Controls inheritance of custom attributes from base classes. This parameter is
        //     ignored.
        //
        // Returns:
        //     An array of objects representing all the custom attributes of the constructor
        //     represented by this System.Reflection.Emit.ConstructorBuilder instance.
        //
        // Exceptions:
        //   T:System.NotSupportedException:
        //     This method is not currently supported.
        public override object[] GetCustomAttributes(bool inherit) => throw new NotImplementedException();

        // Summary:
        //     Gets an System.Reflection.Emit.ILGenerator object, with the specified MSIL stream
        //     size, that can be used to build a method body for this constructor.
        //
        // Parameters:
        //   streamSize:
        //     The size of the MSIL stream, in bytes.
        //
        // Returns:
        //     An System.Reflection.Emit.ILGenerator for this constructor.
        //
        // Exceptions:
        //   T:System.InvalidOperationException:
        //     The constructor is a parameterless constructor. -or- The constructor has System.Reflection.MethodAttributes
        //     or System.Reflection.MethodImplAttributes flags indicating that it should not
        //     have a method body.
        public ILGenerator GetILGenerator(int streamSize) => throw new NotImplementedException();

        // Summary:
        //     Gets an System.Reflection.Emit.ILGenerator for this constructor.
        //
        // Returns:
        //     An System.Reflection.Emit.ILGenerator object for this constructor.
        //
        // Exceptions:
        //   T:System.InvalidOperationException:
        //     The constructor is a parameterless constructor. -or- The constructor has System.Reflection.MethodAttributes
        //     or System.Reflection.MethodImplAttributes flags indicating that it should not
        //     have a method body.
        public ILGenerator GetILGenerator() => throw new NotImplementedException();

        // Summary:
        //     Returns the method implementation flags for this constructor.
        //
        // Returns:
        //     The method implementation flags for this constructor.
        public override MethodImplAttributes GetMethodImplementationFlags() => _methodImplAttributes;

        // Summary:
        //     Returns the parameters of this constructor.
        //
        // Returns:
        //     An array that represents the parameters of this constructor.
        //
        // Exceptions:
        //   T:System.InvalidOperationException:
        //     System.Reflection.Emit.TypeBuilder.CreateType has not been called on this constructor's
        //     type, in the .NET Framework versions 1.0 and 1.1.
        //
        //   T:System.NotSupportedException:
        //     System.Reflection.Emit.TypeBuilder.CreateType has not been called on this constructor's
        //     type, in the .NET Framework version 2.0.
        public override ParameterInfo[] GetParameters() => throw new NotImplementedException();

        // Summary:
        //     Dynamically invokes the constructor represented by this instance on the given
        //     object, passing along the specified parameters, and under the constraints of
        //     the given binder.
        //
        // Parameters:
        //   invokeAttr:
        //     This must be a bit flag from System.Reflection.BindingFlags, such as InvokeMethod,
        //     NonPublic, and so on.
        //
        //   binder:
        //     An object that enables the binding, coercion of argument types, invocation of
        //     members, and retrieval of MemberInfo objects using reflection. If binder is null,
        //     the default binder is used. See System.Reflection.Binder.
        //
        //   parameters:
        //     An argument list. This is an array of arguments with the same number, order,
        //     and type as the parameters of the constructor to be invoked. If there are no
        //     parameters this should be null.
        //
        //   culture:
        //     An instance of System.Globalization.CultureInfo used to govern the coercion of
        //     types. If this is null, the System.Globalization.CultureInfo for the current
        //     thread is used. (For example, this is necessary to convert a System.String that
        //     represents 1000 to a System.Double value, since 1000 is represented differently
        //     by different cultures.)
        //
        // Returns:
        //     The value returned by the invoked constructor.
        //
        // Exceptions:
        //   T:System.NotSupportedException:
        //     This method is not currently supported. You can retrieve the constructor using
        //     System.Type.GetConstructor(System.Reflection.BindingFlags,System.Reflection.Binder,System.Reflection.CallingConventions,System.Type[],System.Reflection.ParameterModifier[])
        //     and call System.Reflection.ConstructorInfo.Invoke(System.Reflection.BindingFlags,System.Reflection.Binder,System.Object[],System.Globalization.CultureInfo)
        //     on the returned System.Reflection.ConstructorInfo.
        public override object Invoke(BindingFlags invokeAttr, Binder? binder, object?[]? parameters, CultureInfo? culture) => throw new NotImplementedException();

        // Summary:
        //     Dynamically invokes the constructor reflected by this instance with the specified
        //     arguments, under the constraints of the specified Binder.
        //
        // Parameters:
        //   obj:
        //     The object that needs to be reinitialized.
        //
        //   invokeAttr:
        //     One of the BindingFlags values that specifies the type of binding that is desired.
        //
        //   binder:
        //     A Binder that defines a set of properties and enables the binding, coercion of
        //     argument types, and invocation of members using reflection. If binder is null,
        //     then Binder.DefaultBinding is used.
        //
        //   parameters:
        //     An argument list. This is an array of arguments with the same number, order,
        //     and type as the parameters of the constructor to be invoked. If there are no
        //     parameters, this should be a null reference (Nothing in Visual Basic).
        //
        //   culture:
        //     A System.Globalization.CultureInfo used to govern the coercion of types. If this
        //     is null, the System.Globalization.CultureInfo for the current thread is used.
        //
        // Returns:
        //     An instance of the class associated with the constructor.
        //
        // Exceptions:
        //   T:System.NotSupportedException:
        //     This method is not currently supported. You can retrieve the constructor using
        //     System.Type.GetConstructor(System.Reflection.BindingFlags,System.Reflection.Binder,System.Reflection.CallingConventions,System.Type[],System.Reflection.ParameterModifier[])
        //     and call System.Reflection.ConstructorInfo.Invoke(System.Reflection.BindingFlags,System.Reflection.Binder,System.Object[],System.Globalization.CultureInfo)
        //     on the returned System.Reflection.ConstructorInfo.
        public override object Invoke(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? parameters, CultureInfo? culture) => throw new NotImplementedException();
        // Summary:
        //     Checks if the specified custom attribute type is defined.
        //
        // Parameters:
        //   attributeType:
        //     A custom attribute type.
        //
        //   inherit:
        //     Controls inheritance of custom attributes from base classes. This parameter is
        //     ignored.
        //
        // Returns:
        //     true if the specified custom attribute type is defined; otherwise, false.
        //
        // Exceptions:
        //   T:System.NotSupportedException:
        //     This method is not currently supported. You can retrieve the constructor using
        //     System.Type.GetConstructor(System.Reflection.BindingFlags,System.Reflection.Binder,System.Reflection.CallingConventions,System.Type[],System.Reflection.ParameterModifier[])
        //     and call System.Reflection.MemberInfo.IsDefined(System.Type,System.Boolean) on
        //     the returned System.Reflection.ConstructorInfo.
        public override bool IsDefined(Type attributeType, bool inherit) => throw new NotImplementedException();

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
            _methodBuilder.SetCustomAttribute(con, binaryAttribute);
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
        //     customBuilder is null.
        public void SetCustomAttribute(CustomAttributeBuilder customBuilder)
        {
            _methodBuilder.SetCustomAttribute(customBuilder);
        }

        // Summary:
        //     Sets the method implementation flags for this constructor.
        //
        // Parameters:
        //   attributes:
        //     The method implementation flags.
        //
        // Exceptions:
        //   T:System.InvalidOperationException:
        //   The containing type has been created using System.Reflection.Emit.TypeBuilder.CreateType.
        public void SetImplementationFlags(MethodImplAttributes attributes)
        {
            _methodImplAttributes = attributes;
        }

        // Summary:
        //     Returns this System.Reflection.Emit.ConstructorBuilder instance as a System.String.
        //
        // Returns:
        //     A string containing the name, attributes, and exceptions of this constructor,
        //     followed by the current Microsoft intermediate language (MSIL) stream.
        public override string ToString() => Name;
    }
}
