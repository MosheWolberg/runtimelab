// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;

namespace JIT.HardwareIntrinsics.General
{
    public static partial class Program
    {
        static Program()
        {
            TestList = new Dictionary<string, Action>() {
                ["Zero.Byte"] = ZeroByte,
                ["Zero.Double"] = ZeroDouble,
                ["Zero.Int16"] = ZeroInt16,
                ["Zero.Int32"] = ZeroInt32,
                ["Zero.Int64"] = ZeroInt64,
                ["Zero.SByte"] = ZeroSByte,
                ["Zero.Single"] = ZeroSingle,
                ["Zero.UInt16"] = ZeroUInt16,
                ["Zero.UInt32"] = ZeroUInt32,
                ["Zero.UInt64"] = ZeroUInt64,
                ["AllBitsSet.Byte"] = AllBitsSetByte,
                ["AllBitsSet.Double"] = AllBitsSetDouble,
                ["AllBitsSet.Int16"] = AllBitsSetInt16,
                ["AllBitsSet.Int32"] = AllBitsSetInt32,
                ["AllBitsSet.Int64"] = AllBitsSetInt64,
                ["AllBitsSet.SByte"] = AllBitsSetSByte,
                ["AllBitsSet.Single"] = AllBitsSetSingle,
                ["AllBitsSet.UInt16"] = AllBitsSetUInt16,
                ["AllBitsSet.UInt32"] = AllBitsSetUInt32,
                ["AllBitsSet.UInt64"] = AllBitsSetUInt64,
                ["As.Byte"] = AsByte,
                ["As.Double"] = AsDouble,
                ["As.Int16"] = AsInt16,
                ["As.Int32"] = AsInt32,
                ["As.Int64"] = AsInt64,
                ["As.SByte"] = AsSByte,
                ["As.Single"] = AsSingle,
                ["As.UInt16"] = AsUInt16,
                ["As.UInt32"] = AsUInt32,
                ["As.UInt64"] = AsUInt64,
                ["AsVector.Byte"] = AsVectorByte,
                ["AsVector.Double"] = AsVectorDouble,
                ["AsVector.Int16"] = AsVectorInt16,
                ["AsVector.Int32"] = AsVectorInt32,
                ["AsVector.Int64"] = AsVectorInt64,
                ["AsVector.SByte"] = AsVectorSByte,
                ["AsVector.Single"] = AsVectorSingle,
                ["AsVector.UInt16"] = AsVectorUInt16,
                ["AsVector.UInt32"] = AsVectorUInt32,
                ["AsVector.UInt64"] = AsVectorUInt64,
                ["GetAndWithElement.Byte.0"] = GetAndWithElementByte0,
                ["GetAndWithElement.Byte.7"] = GetAndWithElementByte7,
                ["GetAndWithElement.Byte.15"] = GetAndWithElementByte15,
                ["GetAndWithElement.Byte.31"] = GetAndWithElementByte31,
                ["GetAndWithElement.Double.0"] = GetAndWithElementDouble0,
                ["GetAndWithElement.Double.1"] = GetAndWithElementDouble1,
                ["GetAndWithElement.Double.3"] = GetAndWithElementDouble3,
                ["GetAndWithElement.Int16.0"] = GetAndWithElementInt160,
                ["GetAndWithElement.Int16.3"] = GetAndWithElementInt163,
                ["GetAndWithElement.Int16.7"] = GetAndWithElementInt167,
                ["GetAndWithElement.Int16.15"] = GetAndWithElementInt1615,
                ["GetAndWithElement.Int32.0"] = GetAndWithElementInt320,
                ["GetAndWithElement.Int32.1"] = GetAndWithElementInt321,
                ["GetAndWithElement.Int32.3"] = GetAndWithElementInt323,
                ["GetAndWithElement.Int32.7"] = GetAndWithElementInt327,
                ["GetAndWithElement.Int64.0"] = GetAndWithElementInt640,
                ["GetAndWithElement.Int64.1"] = GetAndWithElementInt641,
                ["GetAndWithElement.Int64.3"] = GetAndWithElementInt643,
                ["GetAndWithElement.SByte.0"] = GetAndWithElementSByte0,
                ["GetAndWithElement.SByte.7"] = GetAndWithElementSByte7,
                ["GetAndWithElement.SByte.15"] = GetAndWithElementSByte15,
                ["GetAndWithElement.SByte.31"] = GetAndWithElementSByte31,
                ["GetAndWithElement.Single.0"] = GetAndWithElementSingle0,
                ["GetAndWithElement.Single.1"] = GetAndWithElementSingle1,
                ["GetAndWithElement.Single.3"] = GetAndWithElementSingle3,
                ["GetAndWithElement.Single.7"] = GetAndWithElementSingle7,
                ["GetAndWithElement.UInt16.0"] = GetAndWithElementUInt160,
                ["GetAndWithElement.UInt16.3"] = GetAndWithElementUInt163,
                ["GetAndWithElement.UInt16.7"] = GetAndWithElementUInt167,
                ["GetAndWithElement.UInt16.15"] = GetAndWithElementUInt1615,
                ["GetAndWithElement.UInt32.0"] = GetAndWithElementUInt320,
                ["GetAndWithElement.UInt32.1"] = GetAndWithElementUInt321,
                ["GetAndWithElement.UInt32.3"] = GetAndWithElementUInt323,
                ["GetAndWithElement.UInt32.7"] = GetAndWithElementUInt327,
                ["GetAndWithElement.UInt64.0"] = GetAndWithElementUInt640,
                ["GetAndWithElement.UInt64.1"] = GetAndWithElementUInt641,
                ["GetAndWithElement.UInt64.3"] = GetAndWithElementUInt643,
                ["GetAndWithLowerAndUpper.Byte"] = GetAndWithLowerAndUpperByte,
                ["GetAndWithLowerAndUpper.Double"] = GetAndWithLowerAndUpperDouble,
                ["GetAndWithLowerAndUpper.Int16"] = GetAndWithLowerAndUpperInt16,
                ["GetAndWithLowerAndUpper.Int32"] = GetAndWithLowerAndUpperInt32,
                ["GetAndWithLowerAndUpper.Int64"] = GetAndWithLowerAndUpperInt64,
                ["GetAndWithLowerAndUpper.SByte"] = GetAndWithLowerAndUpperSByte,
                ["GetAndWithLowerAndUpper.Single"] = GetAndWithLowerAndUpperSingle,
                ["GetAndWithLowerAndUpper.UInt16"] = GetAndWithLowerAndUpperUInt16,
                ["GetAndWithLowerAndUpper.UInt32"] = GetAndWithLowerAndUpperUInt32,
                ["GetAndWithLowerAndUpper.UInt64"] = GetAndWithLowerAndUpperUInt64,
                ["ToScalar.Byte"] = ToScalarByte,
                ["ToScalar.Double"] = ToScalarDouble,
                ["ToScalar.Int16"] = ToScalarInt16,
                ["ToScalar.Int32"] = ToScalarInt32,
                ["ToScalar.Int64"] = ToScalarInt64,
                ["ToScalar.SByte"] = ToScalarSByte,
                ["ToScalar.Single"] = ToScalarSingle,
                ["ToScalar.UInt16"] = ToScalarUInt16,
                ["ToScalar.UInt32"] = ToScalarUInt32,
                ["ToScalar.UInt64"] = ToScalarUInt64,
                ["ToString.Byte"] = ToStringByte,
                ["ToString.SByte"] = ToStringSByte,
                ["ToString.Int16"] = ToStringInt16,
                ["ToString.UInt16"] = ToStringUInt16,
                ["ToString.Int32"] = ToStringInt32,
                ["ToString.UInt32"] = ToStringUInt32,
                ["ToString.Single"] = ToStringSingle,
                ["ToString.Double"] = ToStringDouble,
                ["ToString.Int64"] = ToStringInt64,
                ["ToString.UInt64"] = ToStringUInt64,
                ["op_Addition.Byte"] = op_AdditionByte,
                ["op_Addition.Double"] = op_AdditionDouble,
                ["op_Addition.Int16"] = op_AdditionInt16,
                ["op_Addition.Int32"] = op_AdditionInt32,
                ["op_Addition.Int64"] = op_AdditionInt64,
                ["op_Addition.SByte"] = op_AdditionSByte,
                ["op_Addition.Single"] = op_AdditionSingle,
                ["op_Addition.UInt16"] = op_AdditionUInt16,
                ["op_Addition.UInt32"] = op_AdditionUInt32,
                ["op_Addition.UInt64"] = op_AdditionUInt64,
                ["op_BitwiseAnd.Byte"] = op_BitwiseAndByte,
                ["op_BitwiseAnd.Double"] = op_BitwiseAndDouble,
                ["op_BitwiseAnd.Int16"] = op_BitwiseAndInt16,
                ["op_BitwiseAnd.Int32"] = op_BitwiseAndInt32,
                ["op_BitwiseAnd.Int64"] = op_BitwiseAndInt64,
                ["op_BitwiseAnd.SByte"] = op_BitwiseAndSByte,
                ["op_BitwiseAnd.Single"] = op_BitwiseAndSingle,
                ["op_BitwiseAnd.UInt16"] = op_BitwiseAndUInt16,
                ["op_BitwiseAnd.UInt32"] = op_BitwiseAndUInt32,
                ["op_BitwiseAnd.UInt64"] = op_BitwiseAndUInt64,
                ["op_BitwiseOr.Byte"] = op_BitwiseOrByte,
                ["op_BitwiseOr.Double"] = op_BitwiseOrDouble,
                ["op_BitwiseOr.Int16"] = op_BitwiseOrInt16,
                ["op_BitwiseOr.Int32"] = op_BitwiseOrInt32,
                ["op_BitwiseOr.Int64"] = op_BitwiseOrInt64,
                ["op_BitwiseOr.SByte"] = op_BitwiseOrSByte,
                ["op_BitwiseOr.Single"] = op_BitwiseOrSingle,
                ["op_BitwiseOr.UInt16"] = op_BitwiseOrUInt16,
                ["op_BitwiseOr.UInt32"] = op_BitwiseOrUInt32,
                ["op_BitwiseOr.UInt64"] = op_BitwiseOrUInt64,
                ["op_Division.Byte"] = op_DivisionByte,
                ["op_Division.Double"] = op_DivisionDouble,
                ["op_Division.Int16"] = op_DivisionInt16,
                ["op_Division.Int32"] = op_DivisionInt32,
                ["op_Division.Int64"] = op_DivisionInt64,
                ["op_Division.SByte"] = op_DivisionSByte,
                ["op_Division.Single"] = op_DivisionSingle,
                ["op_Division.UInt16"] = op_DivisionUInt16,
                ["op_Division.UInt32"] = op_DivisionUInt32,
                ["op_Division.UInt64"] = op_DivisionUInt64,
                ["op_Equality.Byte"] = op_EqualityByte,
                ["op_Equality.Double"] = op_EqualityDouble,
                ["op_Equality.Int16"] = op_EqualityInt16,
                ["op_Equality.Int32"] = op_EqualityInt32,
                ["op_Equality.Int64"] = op_EqualityInt64,
                ["op_Equality.SByte"] = op_EqualitySByte,
                ["op_Equality.Single"] = op_EqualitySingle,
                ["op_Equality.UInt16"] = op_EqualityUInt16,
                ["op_Equality.UInt32"] = op_EqualityUInt32,
                ["op_Equality.UInt64"] = op_EqualityUInt64,
                ["op_ExclusiveOr.Byte"] = op_ExclusiveOrByte,
                ["op_ExclusiveOr.Double"] = op_ExclusiveOrDouble,
                ["op_ExclusiveOr.Int16"] = op_ExclusiveOrInt16,
                ["op_ExclusiveOr.Int32"] = op_ExclusiveOrInt32,
                ["op_ExclusiveOr.Int64"] = op_ExclusiveOrInt64,
                ["op_ExclusiveOr.SByte"] = op_ExclusiveOrSByte,
                ["op_ExclusiveOr.Single"] = op_ExclusiveOrSingle,
                ["op_ExclusiveOr.UInt16"] = op_ExclusiveOrUInt16,
                ["op_ExclusiveOr.UInt32"] = op_ExclusiveOrUInt32,
                ["op_ExclusiveOr.UInt64"] = op_ExclusiveOrUInt64,
                ["op_Inequality.Byte"] = op_InequalityByte,
                ["op_Inequality.Double"] = op_InequalityDouble,
                ["op_Inequality.Int16"] = op_InequalityInt16,
                ["op_Inequality.Int32"] = op_InequalityInt32,
                ["op_Inequality.Int64"] = op_InequalityInt64,
                ["op_Inequality.SByte"] = op_InequalitySByte,
                ["op_Inequality.Single"] = op_InequalitySingle,
                ["op_Inequality.UInt16"] = op_InequalityUInt16,
                ["op_Inequality.UInt32"] = op_InequalityUInt32,
                ["op_Inequality.UInt64"] = op_InequalityUInt64,
                ["op_Multiply.Byte"] = op_MultiplyByte,
                ["op_Multiply.Double"] = op_MultiplyDouble,
                ["op_Multiply.Int16"] = op_MultiplyInt16,
                ["op_Multiply.Int32"] = op_MultiplyInt32,
                ["op_Multiply.Int64"] = op_MultiplyInt64,
                ["op_Multiply.SByte"] = op_MultiplySByte,
                ["op_Multiply.Single"] = op_MultiplySingle,
                ["op_Multiply.UInt16"] = op_MultiplyUInt16,
                ["op_Multiply.UInt32"] = op_MultiplyUInt32,
                ["op_Multiply.UInt64"] = op_MultiplyUInt64,
                ["op_OnesComplement.Byte"] = op_OnesComplementByte,
                ["op_OnesComplement.Double"] = op_OnesComplementDouble,
                ["op_OnesComplement.Int16"] = op_OnesComplementInt16,
                ["op_OnesComplement.Int32"] = op_OnesComplementInt32,
                ["op_OnesComplement.Int64"] = op_OnesComplementInt64,
                ["op_OnesComplement.SByte"] = op_OnesComplementSByte,
                ["op_OnesComplement.Single"] = op_OnesComplementSingle,
                ["op_OnesComplement.UInt16"] = op_OnesComplementUInt16,
                ["op_OnesComplement.UInt32"] = op_OnesComplementUInt32,
                ["op_OnesComplement.UInt64"] = op_OnesComplementUInt64,
                ["op_Subtraction.Byte"] = op_SubtractionByte,
                ["op_Subtraction.Double"] = op_SubtractionDouble,
                ["op_Subtraction.Int16"] = op_SubtractionInt16,
                ["op_Subtraction.Int32"] = op_SubtractionInt32,
                ["op_Subtraction.Int64"] = op_SubtractionInt64,
                ["op_Subtraction.SByte"] = op_SubtractionSByte,
                ["op_Subtraction.Single"] = op_SubtractionSingle,
                ["op_Subtraction.UInt16"] = op_SubtractionUInt16,
                ["op_Subtraction.UInt32"] = op_SubtractionUInt32,
                ["op_Subtraction.UInt64"] = op_SubtractionUInt64,
                ["op_UnaryNegation.Byte"] = op_UnaryNegationByte,
                ["op_UnaryNegation.Double"] = op_UnaryNegationDouble,
                ["op_UnaryNegation.Int16"] = op_UnaryNegationInt16,
                ["op_UnaryNegation.Int32"] = op_UnaryNegationInt32,
                ["op_UnaryNegation.Int64"] = op_UnaryNegationInt64,
                ["op_UnaryNegation.SByte"] = op_UnaryNegationSByte,
                ["op_UnaryNegation.Single"] = op_UnaryNegationSingle,
                ["op_UnaryNegation.UInt16"] = op_UnaryNegationUInt16,
                ["op_UnaryNegation.UInt32"] = op_UnaryNegationUInt32,
                ["op_UnaryNegation.UInt64"] = op_UnaryNegationUInt64,
                ["op_UnaryPlus.Byte"] = op_UnaryPlusByte,
                ["op_UnaryPlus.Double"] = op_UnaryPlusDouble,
                ["op_UnaryPlus.Int16"] = op_UnaryPlusInt16,
                ["op_UnaryPlus.Int32"] = op_UnaryPlusInt32,
                ["op_UnaryPlus.Int64"] = op_UnaryPlusInt64,
                ["op_UnaryPlus.SByte"] = op_UnaryPlusSByte,
                ["op_UnaryPlus.Single"] = op_UnaryPlusSingle,
                ["op_UnaryPlus.UInt16"] = op_UnaryPlusUInt16,
                ["op_UnaryPlus.UInt32"] = op_UnaryPlusUInt32,
                ["op_UnaryPlus.UInt64"] = op_UnaryPlusUInt64,
            };
        }
    }
}
