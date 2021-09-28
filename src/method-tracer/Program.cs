using System;
using System.Text;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;

namespace MethoTracer
{
    class MethodData
    {
        public MethodData(string methodNamespace, string methodName, string signature)
        {
            this.MethodNamespace = methodNamespace;
            this.MethodName = methodName;
            this.Signature = signature;
        }

        public string MethodNamespace { get; set; }
        public string MethodName { get; set; }
        public string Signature { get; set; }
    }

    class MethodEnterLeave
    {
        DateTime _start;
        DateTime _stop;

        public MethodEnterLeave(long methodID, DateTime start)
        {
            this.MethodID = methodID;
            this.Start = start;
        }

        public MethodEnterLeave(long methodID, DateTime start, DateTime stop)
        {
            this.MethodID = methodID;
            this.Start = start;
            this.Stop = stop;
            this.Diff = stop - start;
        }

        public long Ticks
        {
            get
            {
                return (long)Diff.Ticks;
            }
        }

        public int CompareTicks(MethodEnterLeave method)
        {
            if (method.Ticks < Ticks)
                return -1;
            if (method.Ticks > Ticks)
                return 1;
            return 0;
        }

        public long MethodID { get; set; }
        public DateTime Start
        {
            get => _start;
            set => _start = value;
        }

        public DateTime Stop {
            get => _stop;
            set
            {
                _stop = value;
                Diff = Stop - Start;
            }
        }

        TimeSpan Diff { get; set; }
    }

    class AggregatedMethodData
    {
        public AggregatedMethodData(long methodID, long totalTicks, long ticksCount)
        {
            this.MethodID = methodID;
            this.TotalTicks = totalTicks;
            this.TicksCount = ticksCount;
        }

        public int CompareAverageTicks(AggregatedMethodData methodData)
        {
            if (methodData.AverageTicks < AverageTicks)
                return -1;
            if (methodData.AverageTicks > AverageTicks)
                return 1;
            return 0;
        }

        public int CompareTotalTicks(AggregatedMethodData methodData)
        {
            if (methodData.TotalTicks < TotalTicks)
                return -1;
            if (methodData.TotalTicks > TotalTicks)
                return 1;
            return 0;
        }

        public int CompareTicksCount(AggregatedMethodData methodData)
        {
            if (methodData.TicksCount < TicksCount)
                return -1;
            if (methodData.TicksCount > TicksCount)
                return 1;
            return 0;
        }

        public long MethodID { get; set; }
        public long TotalTicks { get; set; }
        public long TicksCount { get; set; }
        public long AverageTicks
        {
            get
            {
                if (TotalTicks == 0 || TicksCount == 0)
                    return 0;
                return (long)(TotalTicks / TicksCount);
            }
        }
    }

    class Program
    {
        enum CommandType
        {
            InCompleteStacks,
            TopMethods,
            AggregatedMethods,
            Export,
            FindMethod,
            AnalyzeMethodCallers,
            AnalyzeMethodCallees
        }

        enum SortMode
        {
            Ascending,
            Descending
        }

        enum TimeDisplayMode
        {
            Ticks,
            Milliseconds
        }

        enum AggregateSortField
        {
            AverageTime,
            TotalTime,
            Count
        }

        static int g_maxScreenWidth = 80;
        static int g_maxMethods = 100;
        static string g_filePath = "";
        static string g_methodFilter = "";
        static string g_dumpOutputPath = "";
        static bool g_dumpUseTempFolder = false;
        static bool g_dumpReplaceFile = false;
        static long g_methodID = 0;
        static bool g_showSignature = false;
        static CommandType g_commandType = CommandType.TopMethods;
        static SortMode g_topSortMode = SortMode.Descending;
        static SortMode g_aggregateSortMode = SortMode.Descending;
        static AggregateSortField g_aggregateSortField = AggregateSortField.AverageTime;
        static TimeDisplayMode g_timeDisplayMode = TimeDisplayMode.Milliseconds;

        static void PrintHelp()
        {
            StringBuilder message = new StringBuilder();
            message.AppendLine("Usage:");
            message.AppendLine("  method-tracer [nettrace file] [commands]");
            message.AppendLine();
            message.AppendLine(String.Format("{0,-35} {1,0}", "  [nettrace file]", "Path to nettrace file."));
            message.AppendLine(String.Format("{0,-35} {1,0}", "  --incomplete-stacks", "List thread stacks including unleft frames during trace."));
            message.AppendLine(String.Format("{0,-35} {1,0}", "  --top[=]", "List method execution time, per thread."));
            message.AppendLine(String.Format("{0,-35} {1,0}", "", "  asc|desc, sort order."));
            message.AppendLine(String.Format("{0,-35} {1,0}", "", "  max=, max number of methods per thread."));
            message.AppendLine(String.Format("{0,-35} {1,0}", "", "  filter=, only include methods matching filter."));
            message.AppendLine(String.Format("{0,-35} {1,0}", "  --aggregate[=]", "List aggregated method execution times."));
            message.AppendLine(String.Format("{0,-35} {1,0}", "", "  asc|desc, sort order."));
            message.AppendLine(String.Format("{0,-35} {1,0}", "", "  avg|total|count, sort field."));
            message.AppendLine(String.Format("{0,-35} {1,0}", "", "  max=, max number of methods to display."));
            message.AppendLine(String.Format("{0,-35} {1,0}", "", "  filter=, only include methods matching filter."));
            message.AppendLine(String.Format("{0,-35} {1,0}", "  --analyze-callers=[method_id]", "List all stacks calling method_id."));
            message.AppendLine(String.Format("{0,-35} {1,0}", "  --analyze-callees=[method_id]", "List all stacks including method_id."));
            message.AppendLine(String.Format("{0,-35} {1,0}", "  --find-method=[filter]", "List all methods matching filter."));
            message.AppendLine(String.Format("{0,-35} {1,0}", "  --export[=]", "Export method and traces into tbl files."));
            message.AppendLine(String.Format("{0,-35} {1,0}", "", "  out=, exported files path."));
            message.AppendLine(String.Format("{0,-35} {1,0}", "", "  temp, create files in directory using temp name."));
            message.AppendLine(String.Format("{0,-35} {1,0}", "", "  replace, replace files at desitnation if already exists."));
            message.AppendLine(String.Format("{0,-35} {1,0}", "  --time-format=ticks", "Display times in ticks instead of milliseconds."));
            message.AppendLine(String.Format("{0,-35} {1,0}", "  --show-sig", "Display full method signatures."));
            message.AppendLine(String.Format("{0,-35} {1,0}", "  --?", "Display help."));
            message.AppendLine();
            message.AppendLine("Enable MonoProfiler method tracing in dotnet-trace, --providers Microsoft-DotNETRuntimeMonoProfiler:40020000010:5:");

            Console.WriteLine(message);
        }

        static bool IncludeMethod(string method, string pattern)
        {
            if (pattern == "*" || method.Contains(pattern))
            {
                return true;
            }

            return false;
        }

        static double ConvertTicksToDisplay(long ticks)
        {
            if (g_timeDisplayMode == TimeDisplayMode.Ticks)
                return ticks;
            else
                return (double)ticks / TimeSpan.TicksPerMillisecond;
        }

        static bool ShowTicks()
        {
            return g_timeDisplayMode == TimeDisplayMode.Ticks;
        }

        static string FormatMethodName(long methodID, MethodData data, int MaxNameWidth, bool includeSignature, Func<long, string,bool> includeMethod)
        {
            string retParam = "";
            string paramList = "";

            if (includeSignature)
            {
                int paramStart = data.Signature.IndexOf('(');
                if (paramStart > 0)
                {
                    retParam = data.Signature.Substring(0, paramStart) + " ";
                    paramList = data.Signature.Substring(paramStart);
                }
            }

            string name = retParam + data.MethodNamespace + "." + data.MethodName + paramList;

            if (!includeMethod(methodID, name))
                return "";

            if (retParam.Length > (MaxNameWidth / 2))
            {
                retParam = "... ";
            }

            MaxNameWidth = MaxNameWidth - retParam.Length;
            if (MaxNameWidth < 0)
                MaxNameWidth = 0;

            if (name.Length > MaxNameWidth)
            {
                name = data.MethodName + paramList;

                if (name.Length < MaxNameWidth)
                {
                    int left = MaxNameWidth - name.Length;
                    var nsItems = data.MethodNamespace.Split('.');
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
                    name = "..." + name.Substring(name.Length - MaxNameWidth);
                }

                name = retParam + name;
            }

            return name;
        }

        static string FormatMethodName(long methodID, Dictionary<long, MethodData> methodDirectory, int MaxNameWidth, bool includeSignature, Func<long, string,bool> includeMethod)
        {
            string name;
            MethodData data;

            if (methodDirectory.TryGetValue(methodID, out data))
            {
                name = FormatMethodName(methodID, data, MaxNameWidth, includeSignature, includeMethod);
            }
            else
            {
                name = "0x" + methodID.ToString("X");
                if (!includeMethod(methodID, name))
                    name = "";
            }

            return name;
        }

        static bool UseGlobalMethodFilter(long methodID, string name)
        {
            if (methodID == g_methodID)
                return true;

            return IncludeMethod(name, g_methodFilter);
        }

        static void InCompleteStacksCommand(Dictionary<long, Stack<MethodEnterLeave>> threadMethodStacks, Dictionary<long, MethodData> methodDirectory)
        {
            int TableMethodIDWidth = (int)(g_maxScreenWidth * 0.15);
            int TableNameWidth = (int)(g_maxScreenWidth * 0.85);
            int MaxNameWidth = TableNameWidth - 10;

            if (MaxNameWidth < 10)
                MaxNameWidth = 10;

            var tableFormat = "{0,-" + TableMethodIDWidth + "}{1,-" + TableNameWidth + "}";

            foreach (var thread in threadMethodStacks)
            {
                if (thread.Value.Count != 0)
                {
                    Console.WriteLine($"ThreadID: 0x{thread.Key.ToString("X")}" + Environment.NewLine);

                    foreach (var frame in thread.Value)
                    {
                        string name = FormatMethodName(frame.MethodID, methodDirectory, MaxNameWidth, g_showSignature, UseGlobalMethodFilter);
                        if (string.IsNullOrEmpty(name))
                            continue;

                        Console.WriteLine(String.Format(tableFormat, "0x" + frame.MethodID.ToString("X"), name));
                    }

                    Console.WriteLine();
                }
            }
        }

        static void TopMethodsPerThreadCommand(Dictionary<long, List<MethodEnterLeave>> threadMethodTraces, Dictionary<long, MethodData> methodDirectory)
        {
            int TableMethodIDWidth = (int)(g_maxScreenWidth * 0.15);
            int TableNameWidth = (int)(g_maxScreenWidth * 0.75);
            int TableTicksWidth = (int)(g_maxScreenWidth * 0.1);
            int MaxNameWidth = TableNameWidth - 10;

            if (MaxNameWidth < 10)
                MaxNameWidth = 10;

            var tableFormat = "{0,-" + TableMethodIDWidth + "}{1,-" + TableNameWidth + "}{2,-" + TableTicksWidth + (ShowTicks() ? "}" : ":N3}");

            foreach (var thread in threadMethodTraces)
            {
                bool wroteThreadID = false;
                int count = 1;
                thread.Value.Sort((value1, value2) => (g_topSortMode == SortMode.Ascending ? value2.CompareTicks(value1) : value1.CompareTicks(value2)));
                if (thread.Value.Count != 0)
                {
                    foreach (var method in thread.Value)
                    {
                        if (count > g_maxMethods)
                            break;

                        string name = FormatMethodName(method.MethodID, methodDirectory, MaxNameWidth, g_showSignature, UseGlobalMethodFilter);
                        if (string.IsNullOrEmpty(name))
                            continue;

                        if (!wroteThreadID)
                        {
                            Console.WriteLine($"ThreadID: 0x{thread.Key.ToString("X")}" + Environment.NewLine);
                            wroteThreadID = true;
                        }

                        Console.WriteLine(String.Format(tableFormat, "0x" + method.MethodID.ToString("X"), name, ConvertTicksToDisplay (method.Ticks)));
                        count++;
                    }

                    if (wroteThreadID)
                        Console.WriteLine();
                }
            }
        }

        static void AggregatedMethodCommand(Dictionary<long, List<MethodEnterLeave>> threadMethodTraces, Dictionary<long, MethodData> methodDirectory)
        {
            int TableMethodIDWidth = (int)(g_maxScreenWidth * 0.15);
            int TableAvgTicksWidth = (int)(g_maxScreenWidth * 0.10);
            int TableTotalTicksWidth = (int)(g_maxScreenWidth * 0.10);
            int TableCountWidth = (int)(g_maxScreenWidth * 0.10);
            int TableNameWidth = (int)(g_maxScreenWidth * 0.55);
            int MaxNameWidth = TableNameWidth - 10;

            if (MaxNameWidth < 10)
                MaxNameWidth = 10;

            var tableFormat = "{0,-" + TableMethodIDWidth + "}{1,-" + TableNameWidth + "}{2,-" + TableAvgTicksWidth + (ShowTicks() ? "}" : ":N3}") + "{3,-" + TableTotalTicksWidth + (ShowTicks() ? "}" : ":N3}") + "{4,-" + TableCountWidth + "}";

            Dictionary<long, AggregatedMethodData> aggregatedMethods = new Dictionary<long, AggregatedMethodData>();
            foreach (var thread in threadMethodTraces)
            {
                foreach (var method in thread.Value)
                {
                    if (!aggregatedMethods.ContainsKey(method.MethodID))
                    {
                        aggregatedMethods.Add(method.MethodID, new AggregatedMethodData(method.MethodID, 0, 0));
                    }

                    AggregatedMethodData aggregatedMethod;
                    if (aggregatedMethods.TryGetValue(method.MethodID, out aggregatedMethod))
                    {
                        aggregatedMethod.TotalTicks = aggregatedMethod.TotalTicks + method.Ticks;
                        aggregatedMethod.TicksCount = aggregatedMethod.TicksCount + 1;
                    }
                }
            }

            var sortedAggregatedMethods = new List<AggregatedMethodData>();
            foreach (var method in aggregatedMethods)
            {
                sortedAggregatedMethods.Add(method.Value);
            }

            if (g_aggregateSortField == AggregateSortField.AverageTime)
                sortedAggregatedMethods.Sort((value1, value2) => (g_aggregateSortMode == SortMode.Ascending ? value2.CompareAverageTicks(value1) : value1.CompareAverageTicks(value2)));
            else if (g_aggregateSortField == AggregateSortField.TotalTime)
                sortedAggregatedMethods.Sort((value1, value2) => (g_aggregateSortMode == SortMode.Ascending ? value2.CompareTotalTicks(value1) : value1.CompareTotalTicks(value2)));
            else if (g_aggregateSortField == AggregateSortField.Count)
                sortedAggregatedMethods.Sort((value1, value2) => (g_aggregateSortMode == SortMode.Ascending ? value2.CompareTicksCount(value1) : value1.CompareTicksCount(value2)));

            Console.WriteLine(String.Format(tableFormat, "MethodID", "Method", ShowTicks() ? "Avg Ticks" : "Avg MSecs", ShowTicks() ? "Total Ticks" : "Total MSecs", "Count"));
            Console.WriteLine(String.Format(tableFormat, "--------", "------", "---------", "-----------", "------"));

            int count = 1;
            foreach (var method in sortedAggregatedMethods)
            {
                if (count > g_maxMethods)
                    break;

                string name = FormatMethodName(method.MethodID, methodDirectory, MaxNameWidth, g_showSignature, UseGlobalMethodFilter);
                if (string.IsNullOrEmpty(name))
                    continue;

                Console.WriteLine(String.Format(tableFormat, "0x" + method.MethodID.ToString("X"), name, ConvertTicksToDisplay(method.AverageTicks), ConvertTicksToDisplay(method.TotalTicks), method.TicksCount));
                count++;
            }
        }

        static void ExportCommand(Dictionary<long, List<MethodEnterLeave>> threadMethodTraces, Dictionary<long, MethodData> methodDirectory)
        {
            string outputFolder = ".";

            if (!string.IsNullOrEmpty(g_dumpOutputPath))
            {
                outputFolder = g_dumpOutputPath;
            }

            if (g_dumpUseTempFolder)
            {
                outputFolder = Path.Combine(outputFolder, Path.GetRandomFileName());
            }

            DirectoryInfo dir = Directory.CreateDirectory(outputFolder);

            string fileNameBase = Path.GetFileNameWithoutExtension(g_filePath);
            string methodsTableFile = Path.Combine(dir.FullName, fileNameBase + "-methods.tbl");
            string methodTracesTableFile = Path.Combine(dir.FullName, fileNameBase + "-method-traces.tbl");

            if (File.Exists(methodsTableFile))
            {
                if (!g_dumpReplaceFile)
                    throw new ArgumentException($"{methodsTableFile} already exists, specify replace parameter to override.");
                File.Delete(methodsTableFile);
            }

            if (File.Exists(methodTracesTableFile))
            {
                if (!g_dumpReplaceFile)
                    throw new ArgumentException($"{methodTracesTableFile} already exists, specify replace parameter to override.");
                File.Delete(methodTracesTableFile);
            }

            using (var methodWriter = File.CreateText(methodsTableFile))
            {
                foreach (var method in methodDirectory)
                {
                    methodWriter.WriteLine(string.Format($"{method.Key}|{method.Value.MethodNamespace}|{method.Value.MethodName}|{method.Value.Signature}"));
                }

                methodWriter.Close();
            }

            using (var methodWriter = File.CreateText(methodTracesTableFile))
            {
                foreach (var thread in threadMethodTraces)
                {
                    foreach (var method in thread.Value)
                    {
                        methodWriter.WriteLine(string.Format($"{thread.Key}|{method.MethodID}|{method.Start.Ticks}|{method.Stop.Ticks}"));
                    }
                }

                methodWriter.Close();
            }

            Console.WriteLine($"Created {methodsTableFile}.");
            Console.WriteLine($"Created {methodTracesTableFile}.");
        }

        static void FindMethodCommand(Dictionary<long, MethodData> methodDirectory)
        {
            int TableMethodIDWidth = (int)(g_maxScreenWidth * 0.15);
            int TableNameWidth = (int)(g_maxScreenWidth * 0.85);
            int MaxNameWidth = TableNameWidth - 10;

            if (MaxNameWidth < 10)
                MaxNameWidth = 10;

            var tableFormat = "{0,-" + TableMethodIDWidth + "}{1,-" + TableNameWidth + "}";

            if (methodDirectory.Count != 0)
            {
                foreach (var method in methodDirectory)
                {
                    string name = FormatMethodName(method.Key, method.Value, MaxNameWidth, g_showSignature, UseGlobalMethodFilter);
                    if (string.IsNullOrEmpty(name))
                        continue;

                    Console.WriteLine(String.Format(tableFormat, "0x" + method.Key.ToString("X"), name));
                }
            }
        }

        static void AnalyzeMethodCallersCommand(List<Stack<MethodEnterLeave>> methodStacks, Dictionary<long, MethodData> methodDirectory)
        {
            int TableMethodIDWidth = (int)(g_maxScreenWidth * 0.15);
            int TableNameWidth = (int)(g_maxScreenWidth * 0.75);
            int TableTicksWidth = (int)(g_maxScreenWidth * 0.10);
            int MaxNameWidth = TableNameWidth - 10;

            if (MaxNameWidth < 10)
                MaxNameWidth = 10;

            var tableFormat = "{0,-" + TableMethodIDWidth + "}{1,-" + TableNameWidth + "}{2,-" + TableTicksWidth + (ShowTicks() ? "}" : ":N3}");

            foreach (var stack in methodStacks)
            {
                var frames = stack.ToArray();
                for (int i = frames.Length - 1; i >= 0; i--)
                {
                    string name = FormatMethodName(frames[i].MethodID, methodDirectory, MaxNameWidth, g_showSignature, UseGlobalMethodFilter);
                    if (string.IsNullOrEmpty(name))
                        continue;

                    Console.WriteLine(String.Format(tableFormat, "0x" + frames[i].MethodID.ToString("X"), name, ConvertTicksToDisplay(frames[i].Ticks)));
                }

                Console.WriteLine();
            }
        }

        static void AnalyzeMethodCalleesCommand(List<Stack<MethodEnterLeave>> methodStacks, Dictionary<long, MethodData> methodDirectory)
        {
            int TableMethodIDWidth = (int)(g_maxScreenWidth * 0.15);
            int TableNameWidth = (int)(g_maxScreenWidth * 0.75);
            int TableTicksWidth = (int)(g_maxScreenWidth * 0.10);
            int MaxNameWidth = TableNameWidth - 10;

            if (MaxNameWidth < 10)
                MaxNameWidth = 10;

            var tableFormat = "{0,-" + TableMethodIDWidth + "}{1,-" + TableNameWidth + "}{2,-" + TableTicksWidth + (ShowTicks() ? "}" : ":N3}");

            foreach (var stack in methodStacks)
            {
                var frames = stack.ToArray();
                for (int i = frames.Length - 1; i >= 0; i--)
                {
                    string name = FormatMethodName(frames[i].MethodID, methodDirectory, MaxNameWidth, g_showSignature, UseGlobalMethodFilter);
                    if (string.IsNullOrEmpty(name))
                        continue;

                    Console.WriteLine(String.Format(tableFormat, "0x" + frames[i].MethodID.ToString("X"), name, ConvertTicksToDisplay(frames[i].Ticks)));
                }

                Console.WriteLine();
            }
        }

        static void MethodEnter(MethodTraceData traceData, Dictionary<long, Stack<MethodEnterLeave>> threadMethodStacks, List<Stack<MethodEnterLeave>> methodStacks)
        {
            if (!threadMethodStacks.ContainsKey(traceData.ThreadID))
                threadMethodStacks.Add(traceData.ThreadID, new Stack<MethodEnterLeave>());

            Stack<MethodEnterLeave> threadStack;
            if (threadMethodStacks.TryGetValue(traceData.ThreadID, out threadStack))
            {
                threadStack.Push(new MethodEnterLeave(traceData.MethodID, traceData.TimeStamp));

                if (g_commandType == CommandType.AnalyzeMethodCallers && g_methodID == traceData.MethodID)
                {
                    methodStacks.Add(new Stack<MethodEnterLeave>(threadStack));
                }
            }
        }

        static void MethodLeave(MethodTraceData traceData, Dictionary<long, Stack<MethodEnterLeave>> threadMethodStacks, Dictionary<long, List<MethodEnterLeave>> threadMethodTraces, List<Stack<MethodEnterLeave>> methodStacks)
        {
            Stack<MethodEnterLeave> threadStack;
            if (threadMethodStacks.TryGetValue(traceData.ThreadID, out threadStack))
            {
                bool done = false;
                while (!done && threadStack.Count != 0)
                {
                    MethodEnterLeave method = threadStack.Peek();
                    if (method.MethodID == traceData.MethodID)
                    {
                        method.Stop = traceData.TimeStamp;
                        threadStack.Pop();

                        if (g_commandType != CommandType.AnalyzeMethodCallees && g_commandType != CommandType.AnalyzeMethodCallers)
                        {
                            List<MethodEnterLeave> threadMethodTrace;
                            if (!threadMethodTraces.ContainsKey(traceData.ThreadID))
                            {
                                threadMethodTraces.Add(traceData.ThreadID, new List<MethodEnterLeave>());
                            }

                            if (threadMethodTraces.TryGetValue(traceData.ThreadID, out threadMethodTrace))
                            {
                                threadMethodTrace.Add(method);
                            }
                        }
                        else if (g_commandType == CommandType.AnalyzeMethodCallees)
                        {
                            foreach (var frame in threadStack)
                            {
                                if (frame.MethodID == g_methodID && frame != threadStack.Peek())
                                {
                                    methodStacks.Add(new Stack<MethodEnterLeave>(threadStack));
                                }
                            }
                        }

                        done = true;
                    }
                    else
                    {
                        //Missmatching enter/leave
                        //Search back in stack to see if we can find a matching method enter.

                        bool foundFrame = false;
                        foreach (var frame in threadStack)
                        {
                            if (frame.MethodID == traceData.MethodID)
                            {
                                // Found match, continue try to handle leave event.
                                foundFrame = true;
                                break;
                            }
                        }

                        if (foundFrame)
                            threadStack.Pop();
                        done = !foundFrame;
                    }
                }
            }
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

                g_maxScreenWidth = Console.WindowWidth;

                g_filePath = args[0];
                int maxArgsIndex = args.Length - 1;

                for (int i = 1; i <= maxArgsIndex; i++)
                {
                    if (args[i].Equals("--incomplete-stacks"))
                    {
                        g_commandType = CommandType.InCompleteStacks;
                        continue;
                    }
                    else if (args[i].Equals("--aggregate"))
                    {
                        g_commandType = CommandType.AggregatedMethods;
                        continue;
                    }
                    else if (args[i].StartsWith("--aggregate="))
                    {
                        var values = args[i].Substring(12).Split(',');
                        foreach (var value in values)
                        {
                            if (value.Equals("asc", StringComparison.OrdinalIgnoreCase))
                            {
                                g_aggregateSortMode = SortMode.Ascending;
                            }
                            else if (value.Equals("desc", StringComparison.OrdinalIgnoreCase))
                            {
                                g_aggregateSortMode = SortMode.Descending;
                            }
                            else if (value.Equals("avg", StringComparison.OrdinalIgnoreCase))
                            {
                                g_aggregateSortField = AggregateSortField.AverageTime;
                            }
                            else if (value.Equals("total", StringComparison.OrdinalIgnoreCase))
                            {
                                g_aggregateSortField = AggregateSortField.TotalTime;
                            }
                            else if (value.Equals("count", StringComparison.OrdinalIgnoreCase))
                            {
                                g_aggregateSortField = AggregateSortField.Count;
                            }
                            else if (value.StartsWith("filter=", StringComparison.OrdinalIgnoreCase))
                            {
                                NumberStyles style = NumberStyles.Integer;
                                g_methodFilter = value.Substring(7);
                                if (g_methodFilter.StartsWith("0x"))
                                {
                                    g_methodFilter = g_methodFilter.Substring(2);
                                    style = NumberStyles.HexNumber;
                                }

                                long.TryParse(g_methodFilter, style, CultureInfo.InvariantCulture, out g_methodID);
                            }
                            else if (value.StartsWith("max=", StringComparison.OrdinalIgnoreCase))
                            {
                                NumberStyles style = NumberStyles.Integer;
                                string count = value.Substring(4);
                                if (count.StartsWith("0x"))
                                {
                                    count = count.Substring(2);
                                    style = NumberStyles.HexNumber;
                                }

                                if (!int.TryParse(count, style, CultureInfo.InvariantCulture, out g_maxMethods))
                                    throw new ArgumentException($"Invalid numeric value, {value}");
                                if (0 > g_maxMethods)
                                    throw new ArgumentException($"Use postive numeric value, {value}");
                            }
                            else
                            {
                                throw new ArgumentException($"Unknown --aggregate parameter value, {value}");
                            }
                        }
                        g_commandType = CommandType.AggregatedMethods;
                        continue;
                    }
                    else if (args[i].Equals("--top"))
                    {
                        g_commandType = CommandType.TopMethods;
                        continue;
                    }
                    else if (args[i].StartsWith("--top="))
                    {
                        var values = args[i].Substring(6).Split(',');
                        foreach (var value in values)
                        {
                            if (value.Equals("asc", StringComparison.OrdinalIgnoreCase))
                            {
                                g_topSortMode = SortMode.Ascending;
                            }
                            else if (value.Equals("desc", StringComparison.OrdinalIgnoreCase))
                            {
                                g_topSortMode = SortMode.Descending;
                            }
                            else if (value.StartsWith("filter=", StringComparison.OrdinalIgnoreCase))
                            {
                                NumberStyles style = NumberStyles.Integer;
                                g_methodFilter = value.Substring(7);
                                if (g_methodFilter.StartsWith("0x"))
                                {
                                    g_methodFilter = g_methodFilter.Substring(2);
                                    style = NumberStyles.HexNumber;
                                }

                                long.TryParse(g_methodFilter, style, CultureInfo.InvariantCulture, out g_methodID);
                            }
                            else if (value.StartsWith("max=", StringComparison.OrdinalIgnoreCase))
                            {
                                NumberStyles style = NumberStyles.Integer;
                                string count = value.Substring(4);
                                if (count.StartsWith("0x"))
                                {
                                    count = count.Substring(2);
                                    style = NumberStyles.HexNumber;
                                }

                                if (!int.TryParse(count, style, CultureInfo.InvariantCulture, out g_maxMethods))
                                    throw new ArgumentException($"Invalid numeric value, {value}");
                                if (0 > g_maxMethods)
                                    throw new ArgumentException($"Use postive numeric value, {value}");
                            }
                            else
                            {
                                throw new ArgumentException($"Unknown --top parameter value, {value}");
                            }
                        }
                        g_commandType = CommandType.TopMethods;
                        continue;
                    }
                    else if (args[i].Equals("--export",StringComparison.OrdinalIgnoreCase))
                    {
                        g_commandType = CommandType.Export;
                        continue;
                    }
                    else if (args[i].StartsWith("--export="))
                    {
                        var values = args[i].Substring(9).Split(',');
                        foreach (var value in values)
                        {
                            if (value.StartsWith("out=", StringComparison.OrdinalIgnoreCase))
                            {
                                g_dumpOutputPath = value.Substring(4);
                                continue;
                            }
                            else if (value.Equals("temp", StringComparison.OrdinalIgnoreCase))
                            {
                                g_dumpUseTempFolder = true;
                                continue;
                            }
                            else if (value.Equals("replace", StringComparison.OrdinalIgnoreCase))
                            {
                                g_dumpReplaceFile = true;
                                continue;
                            }
                            else
                            {
                                throw new ArgumentException($"Unknown --export parameter value, {value}");
                            }
                        }

                        g_commandType = CommandType.Export;
                        continue;
                    }
                    else if (args[i].StartsWith("--time-format="))
                    {
                        string format = args[i].Substring(14);
                        if (format.Equals("ticks", StringComparison.OrdinalIgnoreCase))
                            g_timeDisplayMode = TimeDisplayMode.Ticks;
                        else if (format.Equals("ms", StringComparison.OrdinalIgnoreCase))
                            g_timeDisplayMode = TimeDisplayMode.Milliseconds;
                        else
                            throw new ArgumentException($"Unknown --time-format parameter value, {format}, use ticks or ms");
                        continue;
                    }
                    else if (args[i].Equals("--show-sig", StringComparison.OrdinalIgnoreCase))
                    {
                        g_showSignature = true;
                    }
                    else if (args[i].StartsWith("--find-method="))
                    {
                        NumberStyles style = NumberStyles.Integer;
                        g_methodFilter = args[i].Substring(14);
                        if (g_methodFilter.StartsWith("0x"))
                        {
                            g_methodFilter = g_methodFilter.Substring(2);
                            style = NumberStyles.HexNumber;
                        }

                        long.TryParse(g_methodFilter, style, CultureInfo.InvariantCulture, out g_methodID);
                        g_commandType = CommandType.FindMethod;
                        continue;
                    }
                    else if (args[i].StartsWith("--analyze-callers="))
                    {
                        NumberStyles style = NumberStyles.Integer;
                        string methodID = args[i].Substring(18);
                        if (methodID.StartsWith("0x"))
                        {
                            methodID = methodID.Substring(2);
                            style = NumberStyles.HexNumber;
                        }

                        if (!long.TryParse(methodID, style, CultureInfo.InvariantCulture, out g_methodID))
                            throw new ArgumentException($"Invalid numeric value, {methodID}");
                        g_commandType = CommandType.AnalyzeMethodCallers;
                        continue;
                    }
                    else if (args[i].StartsWith("--analyze-callees="))
                    {
                        NumberStyles style = NumberStyles.Integer;
                        string methodID = args[i].Substring(18);
                        if (methodID.StartsWith("0x"))
                        {
                            methodID = methodID.Substring(2);
                            style = NumberStyles.HexNumber;
                        }

                        if (!long.TryParse(methodID, style, CultureInfo.InvariantCulture, out g_methodID))
                            throw new ArgumentException($"Invalid numeric value, {methodID}");
                        g_commandType = CommandType.AnalyzeMethodCallees;
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

                Dictionary<long, MethodData> methodDirectory = new Dictionary<long, MethodData>();
                List<Stack<MethodEnterLeave>> methodStacks = new List<Stack<MethodEnterLeave>>();
                Dictionary<long, Stack<MethodEnterLeave>> threadMethodStacks = new Dictionary<long, Stack<MethodEnterLeave>>();
                Dictionary<long, List<MethodEnterLeave>> threadMethodTraces = new Dictionary<long, List<MethodEnterLeave>>();

                Task streamTask = Task.Run(() =>
                {
                    var source = new EventPipeEventSource(g_filePath);
                    var rundown = new ClrRundownTraceEventParser(source);
                    var monoProfiler = new MonoProfilerTraceEventParser(source);

                    if (g_commandType != CommandType.FindMethod)
                    {
                        monoProfiler.MonoProfilerMethodEnter += delegate (MethodTraceData enterData)
                        {
                            MethodEnter(enterData, threadMethodStacks, methodStacks);
                        };

                        monoProfiler.MonoProfilerMethodLeave += delegate (MethodTraceData leaveData)
                        {
                            MethodLeave(leaveData, threadMethodStacks, threadMethodTraces, methodStacks);
                        };

                        monoProfiler.MonoProfilerMethodExceptionLeave += delegate (MethodTraceData leaveData)
                        {
                            MethodLeave(leaveData, threadMethodStacks, threadMethodTraces, methodStacks);
                        };
                    }

                    monoProfiler.MonoProfilerJitDoneVerbose += delegate (JitTraceDataVerbose loadData)
                    {
                        methodDirectory.TryAdd(loadData.MethodID, new MethodData(loadData.MethodNamespace, loadData.MethodName, loadData.MethodSignature));
                    };

                    source.Clr.AddCallbackForEvent("Method/LoadVerbose", (MethodLoadUnloadVerboseTraceData loadData) =>
                    {
                        methodDirectory.TryAdd(loadData.MethodID, new MethodData(loadData.MethodNamespace, loadData.MethodName, loadData.MethodSignature));
                    });

                    rundown.MethodDCStopVerbose += delegate (MethodLoadUnloadVerboseTraceData loadData)
                    {
                        methodDirectory.TryAdd(loadData.MethodID, new MethodData(loadData.MethodNamespace, loadData.MethodName, loadData.MethodSignature));
                    };

                    source.Process();
                });

                streamTask.Wait();

                switch (g_commandType)
                {
                    case CommandType.InCompleteStacks:
                        InCompleteStacksCommand(threadMethodStacks, methodDirectory);
                        break;
                    case CommandType.TopMethods:
                        TopMethodsPerThreadCommand(threadMethodTraces, methodDirectory);
                        break;
                    case CommandType.AggregatedMethods:
                        AggregatedMethodCommand(threadMethodTraces, methodDirectory);
                        break;
                    case CommandType.Export:
                        ExportCommand(threadMethodTraces, methodDirectory);
                        break;
                    case CommandType.FindMethod:
                        FindMethodCommand(methodDirectory);
                        break;
                    case CommandType.AnalyzeMethodCallers:
                        AnalyzeMethodCallersCommand(methodStacks, methodDirectory);
                        break;
                    case CommandType.AnalyzeMethodCallees:
                        AnalyzeMethodCalleesCommand(methodStacks, methodDirectory);
                        break;
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
