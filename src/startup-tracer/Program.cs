using System;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

namespace StartupTracer
{
    class ExecutionCheckpoint
    {
        public ExecutionCheckpoint(string name, DateTime timestamp)
        {
            this.Name = name;
            this.TimeStamp = timestamp;
        }

        public string Name { get; set; }
        public DateTime TimeStamp { get; set; }
    }

    class MethodData
    {
        public MethodData(long methodID, string methodNamespace, string methodName)
        {
            this.MethodID = methodID;
            this.MethodNamespace = methodNamespace;
            this.MethodName = methodName;
        }

        public long MethodID { get; set; }
        public string MethodNamespace { get; set; }
        public string MethodName { get; set; }
    }

    class MethodLoad : MethodData
    {
        public MethodLoad(long methodID, string methodNamespace, string methodName, DateTime timestamp)
            : base(methodID, methodNamespace, methodName)
        {
            this.TimeStamp = timestamp;
        }

        public DateTime TimeStamp { get; set; }
    }

    class Program
    {
        static void PrintHelp()
        {
            StringBuilder message = new StringBuilder();
            message.AppendLine("Usage:");
            message.AppendLine("  startup-tracer [nettrace file] [commands]");
            message.AppendLine();
            message.AppendLine(String.Format("{0,-25} {1,0}", "  [nettrace file]", "Path to nettrace file."));
            message.AppendLine(String.Format("{0,-25} {1,0}", "  --method=[filter]", "Method name(s) to analyze. Each loaded method will be checked against filter."));
            message.AppendLine(String.Format("{0,-25} {1,0}", "", "Using '*' as method name will analyze all loaded methods."));
            message.AppendLine(String.Format("{0,-25} {1,0}", "", "Using empty string or leaving out --method command will analyze first loaded method."));
            message.AppendLine(String.Format("{0,-25} {1,0}", "  --?", "Display help."));
            message.AppendLine();
            message.AppendLine("Enable default method load events in dotnet-trace, --providers Microsoft-Windows-DotNETRuntime:10:5");
            message.AppendLine("Enable MonoProfiler method load events in dotnet-trace, --providers Microsoft-DotNETRuntimeMonoProfiler:10:5:");

            Console.WriteLine(message);
        }

        static bool IncludeMethod(string methodNamespace, string methodName, string pattern)
        {
            var fullMethodName = methodNamespace + "." + methodName;
            if (pattern == "*" || fullMethodName.Contains(pattern))
            {
                return true;
            }

            return false;
        }

        static void ParseRundown(string filePath, string pattern, out List<ExecutionCheckpoint> executionCheckpoints, out Dictionary<long, MethodData> methods)
        {
            var rundownExecutionCheckpoints = new List<ExecutionCheckpoint>();
            var rundownMethods = new Dictionary<long, MethodData>();

            Task streamTask = Task.Run(() =>
            {
                long loadedEvents = 0;
                long exectionCheckPointEvents = 0;
                long methodEvents = 0;

                Console.Write("Loading rundown events...");

                var source = new EventPipeEventSource(filePath);
                var rundown = new ClrRundownTraceEventParser(source);

                rundown.ExecutionCheckpointRundownExecutionCheckpointDCEnd += delegate (ExecutionCheckpointTraceData data)
                {
                    loadedEvents++;
                    exectionCheckPointEvents++;

                    if (loadedEvents % 100 == 0)
                        Console.Write(".");

                    rundownExecutionCheckpoints.Add(new ExecutionCheckpoint(data.CheckpointName, source.QPCTimeToTimeStamp(data.CheckpointTimestamp)));
                };

                rundown.MethodDCStopVerbose += delegate (MethodLoadUnloadVerboseTraceData data)
                {
                    loadedEvents++;
                    methodEvents++;

                    if (loadedEvents % 100 == 0)
                        Console.Write(".");

                    if (string.IsNullOrEmpty(pattern) && rundownMethods.Count != 0)
                        return;

                    if (IncludeMethod(data.MethodNamespace, data.MethodName, pattern))
                    {
                        rundownMethods.TryAdd(data.MethodID, new MethodData(data.MethodID, data.MethodNamespace, data.MethodName));
                    }
                };

                source.Process();

                Console.WriteLine("Done.");
                Console.WriteLine($"Parsed {loadedEvents} rundown events, ExecutionCheckpoints ({exectionCheckPointEvents}), Methods ({methodEvents}).");
            });

            streamTask.Wait();

            executionCheckpoints = rundownExecutionCheckpoints;
            methods = rundownMethods;
        }

        static void Main(string[] args)
        {
            try
            {
                if (args.Length < 1)
                {
                    PrintHelp();
                    throw new ArgumentException("Missing trace file argument.");
                }

                string filePath = args[0];
                string methodName = "";
                int maxArgsIndex = args.Length - 1;

                for (int i = 1; i <= maxArgsIndex; i++)
                {
                    if (args[i].StartsWith("--method="))
                    {
                        methodName = args[i].Substring(9);
                        continue;
                    }
                    else if (args[i].Equals("--?", StringComparison.OrdinalIgnoreCase))
                    {
                        PrintHelp();
                        return;
                    }
                    else
                    {
                        throw new ArgumentException($"Unknown command, {args[i]}");
                    }
                }

                Dictionary<long, MethodData> methods = new Dictionary<long, MethodData>();
                List<ExecutionCheckpoint> executionCheckpoints = new List<ExecutionCheckpoint>();

                ParseRundown(filePath, methodName, out executionCheckpoints, out methods);

                Dictionary<long, MethodLoad> methodLoads = new Dictionary<long, MethodLoad>();
                Task streamTask = Task.Run(() =>
                {
                    var source = new EventPipeEventSource(filePath);
                    var monoProfiler = new MonoProfilerTraceEventParser(source);

                    monoProfiler.MonoProfilerJitDone += delegate (JitTraceData data)
                    {
                        if (string.IsNullOrEmpty(methodName) && methodLoads.Count != 0)
                            return;

                        // Needs to load method data from dictionary.
                        MethodData method;
                        if (methods.TryGetValue(data.MethodID, out method))
                        {
                            if (IncludeMethod(method.MethodNamespace, method.MethodName, methodName))
                            {
                                methodLoads.TryAdd(data.MethodID, new MethodLoad(method.MethodID, method.MethodNamespace, method.MethodName, data.TimeStamp));
                            }
                        }
                    };

                    monoProfiler.MonoProfilerJitDoneVerbose += delegate (JitTraceDataVerbose data)
                    {
                        if (string.IsNullOrEmpty(methodName) && methodLoads.Count != 0)
                            return;

                        if (IncludeMethod(data.MethodNamespace, data.MethodName, methodName))
                        {
                            methodLoads.TryAdd(data.MethodID, new MethodLoad(data.MethodID, data.MethodNamespace, data.MethodName, data.TimeStamp));
                        }
                    };

                    source.Clr.AddCallbackForEvent("Method/Load", (MethodLoadUnloadTraceData data) =>
                    {
                        if (string.IsNullOrEmpty(methodName) && methodLoads.Count != 0)
                            return;

                        // Needs to load method data from dictionary.
                        MethodData method;
                        if (methods.TryGetValue(data.MethodID, out method))
                        {
                            if (IncludeMethod(method.MethodNamespace, method.MethodName, methodName))
                            {
                                methodLoads.TryAdd(data.MethodID, new MethodLoad(method.MethodID, method.MethodNamespace, method.MethodName, data.TimeStamp));
                            }
                        }

                    });

                    source.Clr.AddCallbackForEvent("Method/LoadVerbose", (MethodLoadUnloadVerboseTraceData data) =>
                    {
                        if (string.IsNullOrEmpty(methodName) && methodLoads.Count != 0)
                            return;

                        if (IncludeMethod(data.MethodNamespace, data.MethodName, methodName))
                        {
                            methodLoads.TryAdd(data.MethodID, new MethodLoad(data.MethodID, data.MethodNamespace, data.MethodName, data.TimeStamp));
                        }
                    });

                    source.Process();
                });

                streamTask.Wait();

                if (methodLoads.Count == 0)
                    throw new Exception($"Couldn't find method = {methodName} in trace file = {filePath}");

                ExecutionCheckpoint initRuntime = null;
                ExecutionCheckpoint suspendRuntime = null;
                ExecutionCheckpoint resumeRuntime = null;

                foreach (var checkpoint in executionCheckpoints)
                {
                    if (checkpoint.Name.Contains("RuntimeInit"))
                    {
                        initRuntime = checkpoint;
                        continue;
                    }
                    else if (checkpoint.Name.Contains("RuntimeSuspend"))
                    {
                        suspendRuntime = checkpoint;
                        continue;
                    }
                    else if (checkpoint.Name.Contains("RuntimeResume"))
                    {
                        resumeRuntime = checkpoint;
                        continue;
                    }
                }

                var suspended = resumeRuntime.TimeStamp - suspendRuntime.TimeStamp;

                int maxScreenWidth = Console.WindowWidth;
                int TableMethodIDWidth = (int)(maxScreenWidth * 0.15);
                int TableNameWidth = (int)(maxScreenWidth * 0.75);
                int TableTimeWidth = (int)(maxScreenWidth * 0.1);
                int MaxNameWidth = TableNameWidth - 10;

                if (MaxNameWidth < 10)
                    MaxNameWidth = 10;

                var tableFormat = "{0,-" + TableMethodIDWidth + "}{1,-" + TableNameWidth + "}{2,-" + TableTimeWidth + "}";

                Console.WriteLine(Environment.NewLine + $"Time between runtime init and specific method load, measured in milliseconds, method filter = '{methodName}'" + Environment.NewLine);

                Console.WriteLine(String.Format(tableFormat, "MethodID", "Method", "MSecs"));
                Console.WriteLine(String.Format(tableFormat, "--------", "------", "-----"));

                foreach (var methodLoad in methodLoads)
                {
                    var diff = methodLoad.Value.TimeStamp - initRuntime.TimeStamp - suspended;
                    var name = methodLoad.Value.MethodNamespace + "." + methodLoad.Value.MethodName;
                    if (name.Length > MaxNameWidth)
                    {
                        name = methodLoad.Value.MethodName;
                        if (name.Length < MaxNameWidth)
                        {
                            int left = MaxNameWidth - name.Length;
                            var nsItems = methodLoad.Value.MethodNamespace.Split('.');
                            for (int item = nsItems.Length - 1; item >= 0; item--)
                            {
                                if (left - nsItems[item].Length < 0)
                                {
                                    name = "..." + name;
                                    break;
                                }
                                name = nsItems[item] + "." + name;
                                left -= nsItems[item].Length;
                            }
                        }
                        else
                        {
                            name = "..." + name.Substring (name.Length - MaxNameWidth);
                        }
                    }
                    Console.WriteLine(String.Format(tableFormat, "0x" + methodLoad.Value.MethodID.ToString("X"), name, (int)diff.TotalMilliseconds));
                }
            }
            catch (Exception ex)
            {
                var currentColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.Message);
                Console.ForegroundColor = currentColor;
            }

            return;
        }
    }
}
