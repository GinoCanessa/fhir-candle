# fhir-candle
[![Tests](https://github.com/GinoCanessa/fhir-candle/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/GinoCanessa/fhir-candle/actions/workflows/build-and-test.yml)
[![Publish dotnet tool](https://img.shields.io/nuget/v/fhir-candle.svg)](https://github.com/GinoCanessa/fhir-candle/actions/workflows/nuget-tool.yml)
[![Deploy to subscriptions.argo.run](https://github.com/GinoCanessa/fhir-candle/actions/workflows/argo-subscriptions.yml/badge.svg)](https://github.com/GinoCanessa/fhir-candle/actions/workflows/argo-subscriptions.yml)

When you need a small FHIR.

fhir-candle is a small in-memory FHIR server that can be used for testing and development. It is NOT intended to be used for production workloads.

# Documentation

## Get Started

[Install .NET 7 or newer](https://get.dot.net) and run this command:

```
dotnet tool install --global fhir-candle
```

Note that this software is still under heavy development.

Start a FHIR server and open the browser by running:
```
fhir-candle -o
```

### Cloning this repository

To run the default server from the command line:
```
dotnet run --project src/fhir-candle/fhir-candle.csproj
```

To pass arguments when using `dotnet run`, add an extra `--`.  For example, to see help:
```
dotnet run --project src/fhir-candle/fhir-candle.csproj -- --help
```

To build a release version of the project:
```
dotnet build src/fhir-candle/fhir-candle.csproj -c Release
```


The output of the release build can be run (from the root directory of the repo)
* on all platforms:
```
dotnet ./src/fhir-candle/bin/Release/net7.0/fhir-candle.dll
```
* if you built on Windows:
```
.\src\fhir-candle\bin\Release\net7.0\fhir-candle.exe
```
* if you built on Linux or MacOs:
```
./src/fhir-candle/bin/Release/net7.0/fhir-candle
```

### FHIR Tenants

By default, this software loads three FHIR 'tenants':
* a FHIR R4 endpoint at `/r4`,
* a FHIR R4B endpoint at `/r4b`, and
* a FHIR R5 endpoint at `/r5`.

The tenants can be controlled by command line arguments - note that manually specifying any tentants
overrides the default configuration and will *only* load the ones specified.  To load only an R4
endpoint at 'fhir', the arguments would include `--r4 fhir`.  You can specify multiple tenants for
the same version, for example `--r5 fhir --r5 also-fhir` will create two endpoints.

### Loading Initial Data

The server will load initial data specified by the `--fhir-source` argument.  If the path specified
is a relative path, the software will look for the directory starting at the current running path.

If the system is loading multiple tenants, it will check the path for additional directories based
on the tenant names.  For example, a path like `data` passed into the default server will look for
`data/r4`, `data/r4b`, and `data/r5`.  If tenant directories are not found, all tenants will try to
load resources from the specified path.

### Subscriptions Reference Implementation

This project also contains the reference stack for FHIR Subscriptions.  To use the default landing page
of the subscriptions RI, the following command can be used:
```
fhir-candle --reference-implementation subscriptions --load-package hl7.fhir.uv.subscriptions-backport#1.1.0 --load-examples false --protect-source true -m 1000
```


# To-Do
Note: items are unsorted within their priorities

## High priority
* Reverse chaining (`_has`)
* IG Package Loading
* Server-level search
* Feature/module definitions for selective loading
    Build interfaces for Hosted Services, etc.
    Add module tag to Operation, etc.
    Conditional loading based on discovery within types
* Persistent 'unsubscribe' list
* Finish search evaluators (remaining modifier combinations)
* Save/restore points
* Versioned Resource support
* Resource display / edit in UI
* Subscription RI scenario/walkthrough
* Resource editor design improvements
* Composite search parameters

## Mid Priority
* SMART support
* Batch / transaction support
* Proxy header support
* Conditional interaction support (e.g., `conditional-update`, `if-match`)
* OpenAPI generation
* Compartments
* Contained resources
* Subscription websocket support

## The long tail
* Non-terminology validation
* Link to terminiology server for full validation
* `_filter` support
* Runtime named queries
* GraphQL support

## More Information


## Contributing

There are many ways to contribute:
* [Submit bugs](https://github.com/ginocanessa/fhir-candle/issues) and help us verify fixes as they are checked in.
* Review the [source code changes](https://github.com/ginocanessa/fhir-candle/pulls).
* Engage with users and developers on [Official FHIR Zulip](https://chat.fhir.org/)
* [Contribute bug fixes](CONTRIBUTING.md).

See [Contributing](CONTRIBUTING.md) for more information.

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).
For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or
contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

FHIR&reg; is the registered trademark of HL7 and is used with the permission of HL7. 