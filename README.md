# SafePipeline

## About

### Why does it exist

- to enable easily readable pipelines of composable functions
- to manage exception handling
- to allow real code re-use with small testable unit of code (functions)
- to reduce cyclomatic complexity

### How it works

SafePipeline is fundamentally a monad that includes exception handling and the mixing
of synchronous and asynchronous functions.
A pipeline is started using an initial value (StartWith).
functions are then passed to each step in the pipeline - each of these functions performs a single
action and returns the transformed value.
Simple continue / skip can be implemented using the Check method. This method allows you to
provide a function that can return an Operable instance and therefore tell the pipeline whether to
continue to the next step or skip to the end in a success state as no processing is required (for example)

---

## Setting up

### Nuget

https://www.nuget.org/packages/SafePipeline/

Install-Package SafePipeline -Version 0.3.0

---

## Examples

### Controller method
```c#
public async Task<IActionResult> Get()
{
  Item[] result = StartWith(new PipelineContext())
          .Then(Configure)
          .Then(OpenDatabaseConnection)
          .Check(SearchIndex)
          .Then(ReadMatches)
          .OnSuccess(CloseDatabaseConnection)
          .OnSkip(s => _logger.Information("No items found in the index"))
          .OnSuccess(s => _logger.Information($"{s.Get<Item[]>().Length} items found"))
          .OnFailure(LogException)
          .ThrowOnException();
  
  return Ok(result)
}
```

you can see from the above that the code can easily be shared with methods like OpenDatabaseConnection etc.

Please see the tests in the project for working examples
