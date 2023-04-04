// <copyright file="UcumUtils.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using Antlr4.Runtime;

namespace FhirServerHarness.Ucum;

/// <summary>An ucum utilities.</summary>
public static class UcumUtils
{
    /// <summary>Query if 'val' is valid ucum.</summary>
    /// <param name="val">The value.</param>
    /// <returns>True if valid ucum, false if not.</returns>
    public static bool isValidUcum(string val)
    {
        try
        {
            ICharStream stream = CharStreams.fromString(val);
            UCUMLexer lexer = new UCUMLexer(stream);
            //lexer.RemoveErrorListeners();
            //lexer.AddErrorListener(SYNTAX_ERROR_LISTENER);

            CommonTokenStream tokens = new CommonTokenStream(lexer);

            UCUMParser parser = new UCUMParser(tokens);
            //parser.RemoveErrorListeners();
            //parser.AddErrorListener(SYNTAX_ERROR_LISTENER);

            UCUMParser.MainTermContext expression = parser.mainTerm();
            return expression != null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"isValidUcum <<< caught {ex.Message}");
            if (ex.InnerException != null) 
            {
                Console.WriteLine($" <<< {ex.InnerException.Message}");
            }
            return false;
        }
    }
}
