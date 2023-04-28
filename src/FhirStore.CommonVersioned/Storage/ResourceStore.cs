// <copyright file="ResourceStore.cs" company="Microsoft Corporation">
//     Copyright (c) Microsoft Corporation. All rights reserved.
//     Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
// </copyright>

using FhirStore.Extensions;
using FhirStore.Models;
using FhirServerHarness.Search;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.FhirPath;
using Hl7.Fhir.Language.Debugging;
using Hl7.Fhir.Model;
using Hl7.FhirPath;
using Hl7.FhirPath.Expressions;
using System.Collections.Concurrent;
using System.Linq;
using FhirStore.Versioned.Shims.Extensions;

namespace FhirStore.Storage;

/// <summary>A resource store.</summary>
/// <typeparam name="T">Resource type parameter.</typeparam>
public class ResourceStore<T> : IResourceStore
    where T : Resource
{
    /// <summary>The store.</summary>
    private readonly VersionedFhirStore _store;

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

    /// <summary>The supported includes.</summary>
    private string[] _supportedIncludes = Array.Empty<string>();

    /// <summary>The supported reverse includes.</summary>
    private string[] _supportedRevIncludes = Array.Empty<string>();

    /// <summary>
    /// Initializes a new instance of the FhirStore.Storage.ResourceStore&lt;T&gt; class.
    /// </summary>
    /// <param name="fhirStore">   The FHIR store.</param>
    /// <param name="searchTester">The search tester.</param>
    public ResourceStore(VersionedFhirStore fhirStore, SearchTester searchTester)
    {
        _store = fhirStore;
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

        switch (source.TypeName)
        {
            case "SearchParameter":
                SetExecutableSearchParameter((SearchParameter)source);
                break;
        }

        return source;
    }

    /// <summary>Update a specific instance of a resource.</summary>
    /// <param name="source">     [out] The resource.</param>
    /// <param name="allowCreate">True to allow, false to suppress the create.</param>
    /// <returns>The updated resource, or null if it could not be performed.</returns>
    public Resource? InstanceUpdate(Resource source, bool allowCreate)
    {
        if (string.IsNullOrEmpty(source?.Id))
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

        if (!_resourceStore.ContainsKey(source.Id))
        {
            if (allowCreate)
            {
                source.Meta.VersionId = "1";
            }
            else
            {
                return null;
            }
        }
        else if (int.TryParse(_resourceStore[source.Id].Meta?.VersionId ?? string.Empty, out int version))
        {
            source.Meta.VersionId = (version + 1).ToString();
        }
        else
        {
            source.Meta.VersionId = "1";
        }

        source.Meta.LastUpdated = DateTimeOffset.UtcNow;

        _resourceStore[source.Id] = (T)source;

        switch (source.TypeName)
        {
            case "SearchParameter":
                SetExecutableSearchParameter((SearchParameter)source);
                break;
        }

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

        Resource instance = _resourceStore[id];
        _ = _resourceStore.Remove(id);

        switch (instance.TypeName)
        {
            case "SearchParameter":
                RemoveExecutableSearchParameter((SearchParameter)instance);
                break;
        }

        return instance;
    }

    /// <summary>Adds or updates an executable search parameter based on a SearchParameter resource.</summary>
    /// <param name="sp">    The sp.</param>
    /// <param name="delete">(Optional) True to delete.</param>
    private void SetExecutableSearchParameter(SearchParameter sp)
    {
        if ((sp == null) ||
            (sp.Type == null))
        {
            return;
        }

        string name = sp.Code ?? sp.Name ?? sp.Id;

        ModelInfo.SearchParamDefinition spDefinition = new()
        {
            Name = name,
            Url = sp.Url,
            Description = sp.Description,
            Expression = sp.Expression,
            Target = VersionedShims.CopyTargetsToRt(sp.Target),
            Type = (SearchParamType)sp.Type!,
        };

        if (sp.Component.Any())
        {
            spDefinition.CompositeParams = sp.Component.Select(cp => cp.Definition).ToArray();
        }

        foreach (ResourceType rt in VersionedShims.CopyTargetsToRt(sp.Base) ?? Array.Empty<ResourceType>())
        {
            spDefinition.Resource = ModelInfo.ResourceTypeToFhirTypeName(rt)!;
            _store.TrySetExecutableSearchParameter(spDefinition.Resource, spDefinition);
        }
    }

    /// <summary>Removes the executable search parameter described by name.</summary>
    /// <param name="sp">The sp.</param>
    private void RemoveExecutableSearchParameter(SearchParameter sp)
    {
        if ((sp == null) ||
            (sp.Type == null))
        {
            return;
        }

        string name = sp.Code ?? sp.Name ?? sp.Id;

        foreach (ResourceType rt in VersionedShims.CopyTargetsToRt(sp.Base) ?? Array.Empty<ResourceType>())
        {
            _store.TryRemoveExecutableSearchParameter(ModelInfo.ResourceTypeToFhirTypeName(rt)!, name);
        }
    }

    /// <summary>Removes the executable search parameter described by name.</summary>
    /// <param name="name">The name.</param>
    public void RemoveExecutableSearchParameter(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return;
        }

        if (_searchParameters.ContainsKey(name))
        {
            _searchParameters.Remove(name);
        }
    }

    /// <summary>Adds a search parameter definition.</summary>
    /// <param name="spDefinition">The sp definition.</param>
    public void SetExecutableSearchParameter(ModelInfo.SearchParamDefinition spDefinition)
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

        // check for not having a matching search parameter resource
        if (!_store.TryResolve($"SearchParameter/{_resourceName}-{spDefinition.Name}", out ITypedElement? _))
        {
            SearchParameter sp = new()
            {
                Id = $"{_resourceName}-{spDefinition.Name}",
                Name = spDefinition.Name,
                Code = spDefinition.Name,
                Url = spDefinition.Url,
                Description = spDefinition.Description,
                Expression = spDefinition.Expression,
                Target = VersionedShims.CopyTargetsNullable(spDefinition.Target),
                //Target = spDefinition.Target?.Select(r => (ResourceType?)r) ?? Array.Empty<ResourceType?>(),
                Type = spDefinition.Type,
            };

            if (spDefinition.CompositeParams?.Any() ?? false)
            {
                sp.Component = new();

                foreach (string composite in spDefinition.CompositeParams)
                {
                    sp.Component.Add(new()
                    {
                        Definition = composite,
                    });
                }
            }
        }
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
        if (ParsedSearchParameter._allResourceParameters.ContainsKey(name))
        {
            spDefinition = ParsedSearchParameter._allResourceParameters[name];
            return true;
        }

        return _searchParameters.TryGetValue(name, out spDefinition);
    }

    /// <summary>Gets the search parameter definitions known by this store.</summary>
    /// <returns>
    /// An enumerator that allows foreach to be used to process the search parameter definitions in
    /// this collection.
    /// </returns>
    public IEnumerable<ModelInfo.SearchParamDefinition> GetSearchParamDefinitions() => _searchParameters.Values;

    /// <summary>Gets the search includes supported by this store.</summary>
    /// <returns>
    /// An enumerator that allows foreach to be used to process the search includes in this
    /// collection.
    /// </returns>
    public IEnumerable<string> GetSearchIncludes() => _supportedIncludes;

    /// <summary>Gets the search reverse includes supported by this store.</summary>
    /// <returns>
    /// An enumerator that allows foreach to be used to process the search reverse includes in this
    /// collection.
    /// </returns>
    public IEnumerable<string> GetSearchRevIncludes() => _supportedRevIncludes;

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
