// <copyright file="JsonFileDataAttribute.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Reflection;
using Xunit.Sdk;

namespace FhirServerHarness.Tests.Extensions;

/// <summary>Attribute for file data.</summary>
public class TwoFileDataAttribute : DataAttribute
{
    private readonly string _filePath1;
    private readonly string _filePath2;

    /// <summary>Load file contents as the data source for a theory.</summary>
    /// <param name="filePath1">The absolute or relative path to the file to load.</param>
    /// <param name="filePath2">Full pathname of the additional file.</param>
    public TwoFileDataAttribute(string filePath1, string filePath2)
    {
        _filePath1 = filePath1;
        _filePath2 = filePath2;
    }

    /// <inheritDoc />
    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        if (testMethod == null) { throw new ArgumentNullException(nameof(testMethod)); }

        // Get the absolute path to the file
        var path = Path.IsPathRooted(_filePath1)
            ? _filePath1
            : Path.GetRelativePath(Directory.GetCurrentDirectory(), _filePath1);

        if (!File.Exists(path))
        {
            throw new ArgumentException($"Could not find file at path: {path}");
        }

        string data1 = File.ReadAllText(path);

        path = Path.IsPathRooted(_filePath2)
            ? _filePath2
            : Path.GetRelativePath(Directory.GetCurrentDirectory(), _filePath2);

        if (!File.Exists(path))
        {
            throw new ArgumentException($"Could not find file at path: {path}");
        }

        string data2 = File.ReadAllText(path);

        return new object[][] { new object[2] { data1, data2 } };
    }
}