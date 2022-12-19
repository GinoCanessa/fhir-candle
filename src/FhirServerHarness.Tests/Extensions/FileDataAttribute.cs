// <copyright file="JsonFileDataAttribute.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using System.Reflection;
using Xunit.Sdk;

namespace FhirServerHarness.Tests.Extensions;

/// <summary>Attribute for file data.</summary>
public class FileDataAttribute : DataAttribute
{
    private readonly string _filePath;

    /// <summary>
    /// Load file contents as the data source for a theory
    /// </summary>
    /// <param name="filePath">The absolute or relative path to the file to load</param>
    public FileDataAttribute(string filePath)
    {
        _filePath = filePath;
    }

    /// <inheritDoc />
    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        if (testMethod == null) { throw new ArgumentNullException(nameof(testMethod)); }

        // Get the absolute path to the file
        var path = Path.IsPathRooted(_filePath)
            ? _filePath
            : Path.GetRelativePath(Directory.GetCurrentDirectory(), _filePath);

        if (!File.Exists(path))
        {
            throw new ArgumentException($"Could not find file at path: {path}");
        }

        string data = File.ReadAllText(path);
        return new object[][] { new object[1] { data } };
    }
}