# diagnostics-nettrace-samples

Repro containing dotnet diagnostics samples mainly focusing on analyzing nettrace files.

## startup-tracer

Analyze nettrace files including method load and execution check points displaying time to reach any loaded method from runtime init. Runtime init normally occurs before EventPipe startup, so regular timestamps won't cover all runtime init time. Execution checkpoints can include timestamps happening before EventPipe startup. Using these events its possible to calculate time from runtime init to any loaded/jitted method.

```
Usage:
  startup-tracer [nettrace file] [commands]

  [nettrace file]         Path to nettrace file.
  --method=[filter]       Method name(s) to analyze. Each loaded method will be checked against filter.
                          Using '*' as method name will analyze all loaded methods.
                          Using empty string or leaving out --method command will analyze first loaded method.
  --?                     Display help.

Enable default method load events in dotnet-trace, --providers Microsoft-Windows-DotNETRuntime:10:5
Enable MonoProfiler method load events in dotnet-trace, --providers Microsoft-DotNETRuntimeMonoProfiler:10:5:
```

## method-tracer

Analyze nettrace files including MonoProfiler Enter/Leave/BeginInvoke/EndInvoke (uses MonoVM jit instrumentation). Tool presents multiple views like top method execution time per thread, aggregated views of all executed methods, sortable on average, total and invocation count. Tool also offers capabilities to analyzing caller/callees callstacks (requires nettrace files to include enough instrumentation data to build callstacks based on emitted events). Since tool uses enter/leave instrumentation data it is possible to get specific inclusive execution time per unique method invocation. Powerful complement to regular sample profiler.

```
Usage:
  method-tracer [nettrace file] [commands]

  [nettrace file]                   Path to nettrace file.
  --incomplete-stacks               List thread stacks including unleft frames during trace.
  --top[=]                          List method execution time, per thread.
                                      asc|desc, sort order.
                                      max=, max number of methods per thread.
                                      filter=, only include methods matching filter.
  --aggregate[=]                    List aggregated method execution times.
                                      asc|desc, sort order.
                                      avg|total|count, sort field.
                                      max=, max number of methods to display.
                                      filter=, only include methods matching filter.
  --analyze-callers=[method_id]     List all stacks calling method_id.
  --analyze-callees=[method_id]     List all stacks including method_id.
  --find-method=[filter]            List all methods matching filter.
  --export[=]                       Export method and traces into tbl files.
                                      out=, exported files path.
                                      temp, create files in directory using temp name.
                                      replace, replace files at desitnation if already exists.
  --time-format=ticks               Display times in ticks instead of milliseconds.
  --show-sig                        Display full method signatures.
  --?                               Display help.

Enable MonoProfiler method tracing in dotnet-trace, --providers Microsoft-DotNETRuntimeMonoProfiler:40020000010:5:
```

## gc-heapster

Analyze nettrace files including MonoProfiler GC dump events. Tool can either show aggregated view of all allocations in one dump, sorted on size or count, or tool can be used to diff dumps and display size/count delta between dumps. Powerful way to detect managed memory leaks between different points in application execution.

```
Usage:
  gc-heapster [nettrace file(s)] [commands]

  [nettrace file(s)]                Path to one or more nettrace file(s).
  --diff[=]                         Show increase/decrease between first and second dump.
                                      asc|desc, sort order.
                                      size|count, sort field.
                                      max=, max number of types to display.
                                      filter=, only include types matching filter.
  --aggregate[=]                    List aggregated allocations per type.
                                      asc|desc, sort order.
                                      avg|size|count, sort field.
                                      max=, max number of types to display.
                                      filter=, only include types matching filter.

Enable MonoProfiler heap dump tracing in dotnet-trace, --providers Microsoft-DotNETRuntimeMonoProfiler:0x8900001:4:
```
