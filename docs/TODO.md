# IPLab TODO

## IPLab.Core.Tests

- **Test: circular dependency is caught by `Validate()`**
  Build a flow with a cycle (e.g. O1 → O2 → O3 → O1) and assert that
  `Validate()` returns an invalid result containing a circular dependency error.

- **Test: wired parameter without a declared dependency is caught by `Validate()`**
  Build a flow where a parameter `Source` points to an operator that is not listed
  in the operator's `Dependencies`, and assert that `Validate()` catches the mismatch.

## IPLab.Core

- **Parallel operator execution in `FlowEx`**
  Currently `RunAllAsync` executes operators sequentially in topological order.
  Operators with no dependency on each other can safely run in parallel.
  Implementation: after topological sort, group operators into dependency levels
  (operators at the same level have no inter-dependencies), then use `Task.WhenAll`
  to run each level in parallel while preserving execution order across levels.
