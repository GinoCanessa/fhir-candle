// <copyright file="ResourceStore.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirServerHarness.Extensions;
using FhirServerHarness.Models;
using FhirServerHarness.Search;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using Hl7.FhirPath.Expressions;
using System.Collections.Concurrent;

namespace FhirServerHarness.Storage;

/// <summary>A resource store.</summary>
/// <typeparam name="T">Resource type parameter.</typeparam>
public class ResourceStore<T> : IResourceStore
    where T : Resource
{
    /// <summary>Name of the resource.</summary>
    private string _resourceName = typeof(T).Name;

    /// <summary>True if has disposed, false if not.</summary>
    private bool _hasDisposed = false;

    /// <summary>The resource store.</summary>
    private Dictionary<string, T> _resourceStore = new();

    /// <summary>The search tester.</summary>
    public required SearchTester _searchTester;

    /// <summary>(Immutable) The FHIRPath compiler.</summary>
    private readonly FhirPathCompiler _compiler = new();

    /// <summary>Options for controlling the search.</summary>
    private Dictionary<string, ModelInfo.SearchParamDefinition> _searchParameters = new();

    /// <summary>(Immutable) The compiled FHIRPath expressions.</summary>
    private readonly ConcurrentDictionary<string, CompiledExpression> _fpExpressions = new();

    /// <summary>
    /// Initializes a new instance of the FhirServerHarness.Storage.ResourceStore&lt;T&gt; class.
    /// </summary>
    /// <param name="searchTester">The search tester.</param>
    public ResourceStore(SearchTester searchTester)
    {
        _searchTester = searchTester;
    }

    /// <summary>Reads a specific instance of a resource.</summary>
    /// <param name="id">[out] The identifier.</param>
    /// <returns>The requested resource or null.</returns>
    public Resource? InstanceRead(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        if (!_resourceStore.ContainsKey(id))
        {
            return null;
        }

        return _resourceStore[id];
    }

    /// <summary>Create an instance of a resource.</summary>
    /// <param name="source">         [out] The resource.</param>
    /// <param name="allowExistingId">True to allow, false to suppress the existing identifier.</param>
    /// <returns>The created resource, or null if it could not be created.</returns>
    public Resource? InstanceCreate(Resource source, bool allowExistingId)
    {
        if (source == null)
        {
            return null;
        }

        if ((!allowExistingId) || string.IsNullOrEmpty(source.Id))
        {
            source.Id = Guid.NewGuid().ToString();
        }

        if (_resourceStore.ContainsKey(source.Id))
        {
            return null;
        }

        if (source is not T)
        {
            return null;
        }

        if (source.Meta == null)
        {
            source.Meta = new Meta();
        }

        source.Meta.VersionId = "1";
        source.Meta.LastUpdated = DateTimeOffset.UtcNow;

        _resourceStore.Add(source.Id, (T)source);
        return source;
    }

    /// <summary>Update a specific instance of a resource.</summary>
    /// <param name="source">[out] The resource.</param>
    /// <returns>The updated resource, or null if it could not be performed.</returns>
    public Resource? InstanceUpdate(Resource source)
    {
        if (string.IsNullOrEmpty(source?.Id))
        {
            return null;
        }

        if (!_resourceStore.ContainsKey(source.Id))
        {
            return null;
        }

        if (source is not T)
        {
            return null;
        }

        if (int.TryParse(_resourceStore[source.Id].Meta.VersionId, out int version))
        {
            source.Meta.VersionId = (version + 1).ToString();
        }
        else
        {
            source.Meta.VersionId = "1";
        }

        source.Meta.LastUpdated = DateTimeOffset.UtcNow;

        _resourceStore[source.Id] = (T)source;
        return source;
    }

    /// <summary>Instance delete.</summary>
    /// <param name="id">[out] The identifier.</param>
    /// <returns>The deleted resource or null.</returns>
    public Resource? InstanceDelete(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        if (!_resourceStore.ContainsKey(id))
        {
            return null;
        }

        T instance = _resourceStore[id];
        _ = _resourceStore.Remove(id);
        return instance;
    }

    /// <summary>Adds a search parameter definition.</summary>
    /// <param name="spDefinition">The sp definition.</param>
    public void AddSearchParameterDefinition(ModelInfo.SearchParamDefinition spDefinition)
    {
        if (string.IsNullOrEmpty(spDefinition?.Name))
        {
            return;
        }

        if (spDefinition.Resource != _resourceName)
        {
            return;
        }

        _searchParameters.Add(spDefinition.Name, spDefinition);
    }

    /// <summary>
    /// Attempts to get search parameter definition a ModelInfo.SearchParamDefinition from the given
    /// string.
    /// </summary>
    /// <param name="name">        The name.</param>
    /// <param name="spDefinition">[out] The sp definition.</param>
    /// <returns>True if it succeeds, false if it fails.</returns>
    public bool TryGetSearchParamDefinition(string name, out ModelInfo.SearchParamDefinition? spDefinition)
    {
        return _searchParameters.TryGetValue(name, out spDefinition);
    }

    /// <summary>Performs a type search in this resource store.</summary>
    /// <param name="query">The query.</param>
    /// <returns>
    /// An enumerator that allows foreach to be used to process type search in this collection.
    /// </returns>
    public IEnumerable<Resource> TypeSearch(IEnumerable<ParsedSearchParameter> parameters)
    {
        foreach (T resource in _resourceStore.Values)
        {
            ITypedElement r = resource.ToTypedElement();

            if (_searchTester.TestForMatch(r, parameters, out IEnumerable<ParsedSearchParameter> _, out IEnumerable<ParsedSearchParameter> _))
            {
                yield return resource;
            }
        }

        //foreach (ParsedSearchParameter parameter in parameters)
        //{
        //    if (string.IsNullOrEmpty(parameter.SelectExpression))
        //    {
        //        // TODO: special processing - likely need to change ParsedSearchParameter to contain the compiled test function
        //        continue;
        //    }

        //    //// direct resolve
        //    //// avg: 0.7 ms
        //    //return _resourceStore.Values.Where(r => r.Id.Equals(parameter.Value, StringComparison.OrdinalIgnoreCase));

        //    //// direct resolve
        //    //// avg: 0.7 ms
        //    //return _resourceStore.Values.Where(r => r.Id.Contains(parameter.Value, StringComparison.OrdinalIgnoreCase));


        //    //// Need to sort out if we can actually do all the modifiers in FHIRPath (case-sensitivity)
        //    //// using the FHIRPath POCO evaluator
        //    //// avg: 1.0 ms
        //    //string exp = $"Resource.id = '{parameter.Value}'";
        //    //return _resourceStore.Values.Where(r => r.IsTrue(exp));

        //    //// avg: 1.0 ms
        //    //string exp = $"Resource.id.lower().contains('{parameter.Value}')";
        //    //return _resourceStore.Values.Where(r => r.IsTrue(exp));

        //    return _resourceStore.Values.Where(r => TestSearchParameter(r, parameter));

        //    //if (!_fpExpressions.ContainsKey(parameter.Expression))
        //    //{
        //    //    //string exp = parameter.Expression.Replace("Resource.", "%resource.");


        //    //    _fpExpressions.TryAdd(parameter.Expression, _compiler.Compile(parameter.Expression));
        //    //}


        //    //// this ends up recompiling every time *and* passing in a var, didn't bother to finish the code
        //    //foreach (T resource in _resourceStore.Values)
        //    //{
        //    //    SymbolTable symbolTable = new SymbolTable(FhirPathCompiler.DefaultSymbolTable);
        //    //    symbolTable.AddVar("value", parameter.Value);

        //    //    ITypedElement typedElement = resource.ToTypedElement();
        //    //    FhirEvaluationContext ctx = new FhirEvaluationContext(typedElement, typedElement);



        //    //    //if (_fpExpressions[parameter.Expression].Evaluate(resource).Any())
        //    //    //{
        //    //    //    return resource;
        //    //    //}
        //    //}
        //}

        //return Array.Empty<T>();

        //if (!_fpExpressions.ContainsKey(query))
        //{
        //    _fpExpressions.TryAdd(query, _compiler.Compile(query));
        //}

        //return _resourceStore.Values.Where(r => _fpExpressions[query].Predicate(r));
    }

    /// <summary>
    /// Releases the unmanaged resources used by the
    /// FhirModelComparer.Server.Services.FhirManagerService and optionally releases the managed
    /// resources.
    /// </summary>
    /// <param name="disposing">True to release both managed and unmanaged resources; false to
    ///  release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!_hasDisposed)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }

            // TODO: free unmanaged resources (unmanaged objects) and override finalizer
            // TODO: set large fields to null
            _hasDisposed = true;
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged
    /// resources.
    /// </summary>
    void IDisposable.Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
