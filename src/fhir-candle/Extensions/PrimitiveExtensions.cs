// <copyright file="PrimitiveExtensions.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

namespace fhir.candle.Extensions;

/// <summary>A primitive extensions.</summary>
public static class PrimitiveExtensions
{
    /// <summary>A decimal extension method that gets significant digit count.</summary>
    /// <param name="value">The value to act on.</param>
    /// <returns>The significant digit count.</returns>
    public static int GetSignificantDigitCount(this decimal value)
    {
        /* decimal type is represented as a fraction of two integers: 
         * a numerator that can be anything, and 
         * a denominator that is some power of 10.
         * 
         * For example, the following numbers are represented by
         * the corresponding fractions:
         * 
         * VALUE    NUMERATOR   DENOMINATOR
         * 1        1           1
         * 1.0      10          10
         * 1.012    1012        1000
         * 0.04     4           100
         * 12.01    1201        100
         * 
         * So if the magnitude is greater than or equal to one, the number of digits 
         * is the number of digits in the numerator.
         * If it's less than one, the number of digits is the number of digits
         * in the denominator.
         */

        int[] bits = decimal.GetBits(value);

        if (value >= 1M || value <= -1M)
        {
            int highPart = bits[2];
            int middlePart = bits[1];
            int lowPart = bits[0];

            decimal num = new decimal(lowPart, middlePart, highPart, false, 0);

            int exponent = (int)Math.Ceiling(Math.Log10((double)num));

            return exponent;
        }
        else
        {
            int scalePart = bits[3];

            // According to MSDN, the exponent is represented by
            // bits 16-23 (the 2nd word):
            // http://msdn.microsoft.com/en-us/library/system.decimal.getbits.aspx
            int exponent = (scalePart & 0x00FF0000) >> 16;

            return exponent + 1;
        }
    }
}
