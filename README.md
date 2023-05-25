# fhir-candle
When a small FHIR will do!

fhir-candle is a small in-memory FHIR server that can be used for testing and development. It is NOT intended to be used for production workloads.


# Documentation


# To-Do
Note: items are unsorted within their priorities

## High priority
[] Reverse chaining (`_has`)
[] IG Package Loading
[] Server-level search
[] Feature/module definitions for selective loading
    Build interfaces for Hosted Services, etc.
    Add module tag to Operation, etc.
    Conditional loading based on discovery within types
[] Story for easier loading of 'default' pages (e.g., listing)
[] Persistent 'unsubscribe' list
[] Finish search evaluators (remaining modifier combinations)
[] Save/restore points
[] History support
[] Resource display / edit in UI
[] Subscription RI scenario/walkthrough

## Mid Priority
[] SMART support
[] Batch / transaction support
[] Proxy header support
[] Versioned Resource support
[] Conditional interaction support (e.g., `conditional-update`)
[] OpenAPI generation
[] Compartments
[] Contained resources

## The long tail
[] Non-terminology validation
[] Link to terminiology server for full validation
[] `_filter` support
[] In-line definitions for named queries
[] GraphQL support
[] FHIR Messaging support

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