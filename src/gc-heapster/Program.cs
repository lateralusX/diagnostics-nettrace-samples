using System;
using System.Text;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Diagnostics.Tracing;

namespace GCHeapster
{
    class TraceFile
    {
        public TraceFile(string path)
        {
            FilePath = path;
            AggregatedAllocations = new Dictionary<long, AggregatedAllocationData>();
            TypeDictionary = new Dictionary<long, TypeData>();
        }

        public string FilePath { get; set; }
        public Dictionary<long, AggregatedAllocationData> AggregatedAllocations { get; set; }
        public Dictionary<long, TypeData> TypeDictionary { get; set; }
    }

    class AggregatedAllocationData
    {
        public AggregatedAllocationData(long vtableID, long size, long count)
        {
            VTableID = vtableID;
            Size = size;
            Count = count;
        }

        public int CompareAverageSize(AggregatedAllocationData allocData)
        {
            if (allocData.AverageSize < AverageSize)
                return -1;
            if (allocData.AverageSize > AverageSize)
                return 1;
            return 0;
        }

        public int CompareSize(AggregatedAllocationData allocData)
        {
            if (allocData.Size < Size)
                return -1;
            if (allocData.Size > Size)
                return 1;
            return 0;
        }

        public int CompareCount(AggregatedAllocationData allocData)
        {
            if (allocData.Count < Count)
                return -1;
            if (allocData.Count > Count)
                return 1;
            return 0;

        }
        public long VTableID { get; set; }
        public long Size { get; set; }
        public long Count { get; set; }
        public long AverageSize {
            get
            {
                if (Size == 0 || Count == 0)
                    return 0;
                return (long)(Size / Count);
            }
        }
    }

    class TypeData
    {
        public long VTableID;
        public long ClassID;
        public string ClassName;
    }

    class Program
    {
        enum CommandType
        {
            AggregatedAllocations,
            DiffAggregatedAllocations
        }

        enum SortMode
        {
            Ascending,
            Descending
        }

        enum AggregateSortField
        {
            AverageSize,
            Size,
            Count
        }

        static int g_maxAllocations = 100;
        static int g_maxScreenWidth = 80;
        static string g_typeFilter = "";
        static long g_vtableID = 0;
        static CommandType g_commandType = CommandType.AggregatedAllocations;
        static SortMode g_aggregateSortMode = SortMode.Descending;
        static AggregateSortField g_aggregateSortField = AggregateSortField.AverageSize;

        static void PrintHelp()
        {
            StringBuilder message = new StringBuilder();
            message.AppendLine("Usage:");
            message.AppendLine("  gc-heapster [nettrace file(s)] [commands]");
            message.AppendLine();
            message.AppendLine(String.Format("{0,-35} {1,0}", "  [nettrace file(s)]", "Path to one or more nettrace file(s)."));
            message.AppendLine(String.Format("{0,-35} {1,0}", "  --diff[=]", "Show increase/decrease between first and second dump."));
            message.AppendLine(String.Format("{0,-35} {1,0}", "", "  asc|desc, sort order."));
            message.AppendLine(String.Format("{0,-35} {1,0}", "", "  size|count, sort field."));
            message.AppendLine(String.Format("{0,-35} {1,0}", "", "  max=, max number of types to display."));
            message.AppendLine(String.Format("{0,-35} {1,0}", "", "  filter=, only include types matching filter."));
            message.AppendLine(String.Format("{0,-35} {1,0}", "  --aggregate[=]", "List aggregated allocations per type."));
            message.AppendLine(String.Format("{0,-35} {1,0}", "", "  asc|desc, sort order."));
            message.AppendLine(String.Format("{0,-35} {1,0}", "", "  avg|size|count, sort field."));
            message.AppendLine(String.Format("{0,-35} {1,0}", "", "  max=, max number of types to display."));
            message.AppendLine(String.Format("{0,-35} {1,0}", "", "  filter=, only include types matching filter."));
            message.AppendLine();
            message.AppendLine("Enable MonoProfiler heap dump tracing in dotnet-trace, --providers Microsoft-DotNETRuntimeMonoProfiler:0x8900001:4:");

            Console.WriteLine(message);
        }

        static bool IncludeTypeName(string name, string pattern)
        {
            if (pattern == "*" || name.Contains(pattern))
            {
                return true;
            }

            return false;
        }

        static string FormatTypeName(long vtableID, TypeData data, int MaxNameWidth, Func<long, string, bool> includeType)
        {
            string name = data.ClassName;

            if (!includeType(vtableID, name))
                return "";

            if (name.Length > MaxNameWidth)
            {
                name = "";
                int left = MaxNameWidth;
                var nsItems = data.ClassName.Split('.');
                for (int item = nsItems.Length - 1; item >= 0; item--)
                {
                    if (left - nsItems[item].Length < 0)
                    {
                        if (!string.IsNullOrEmpty(name))
                            name = "..." + name;

                        break;
                    }
                    if (!string.IsNullOrEmpty(name))
                        name = nsItems[item] + "." + name;
                    else
                        name = nsItems[item];

                    left -= nsItems[item].Length;
                }
            }

            return name;
        }

        static string FormatTypeName(long vtableID, Dictionary<long, TypeData> typeDirectory, int MaxNameWidth, Func<long, string, bool> includeType)
        {
            string name;
            TypeData data;

            if (typeDirectory.TryGetValue(vtableID, out data))
            {
                name = FormatTypeName(vtableID, data, MaxNameWidth, includeType);
            }
            else
            {
                name = "0x" + vtableID.ToString("X");
                if (!includeType(vtableID, name))
                    name = "";
            }

            return name;
        }

        static bool UseGlobalTypeFilter(long vtableID, string name)
        {
            if (vtableID == g_vtableID)
                return true;

            return IncludeTypeName(name, g_typeFilter);
        }

        static void DiffAggregatedAllocationsCommand(List<TraceFile> traceFiles)
        {
            int TableVTableIDWidth = (int)(g_maxScreenWidth * 0.15);
            int TableSizeWidth = (int)(g_maxScreenWidth * 0.10);
            int TableCountWidth = (int)(g_maxScreenWidth * 0.10);
            int TableNameWidth = (int)(g_maxScreenWidth * 0.60);
            int MaxNameWidth = TableNameWidth - 10;

            if (MaxNameWidth < 10)
                MaxNameWidth = 10;

            var mergedTypeDirectionary = new Dictionary<long, TypeData>(traceFiles[0].TypeDictionary);

            foreach (var item in traceFiles[1].TypeDictionary)
            {
                TypeData value = null;
                if (mergedTypeDirectionary.TryGetValue(item.Key, out value))
                {
                    if (item.Value.ClassID != value.ClassID)
                        throw new ArgumentException($"Class ID missmatch between files, files not captured in same runtime instance.");
                    if (!string.Equals(item.Value.ClassName, value.ClassName,StringComparison.Ordinal))
                        throw new ArgumentException($"VTable ID and class name missmatch between files, files not captured in same runtime instance.");
                }
                else
                {
                    mergedTypeDirectionary.Add(item.Key, item.Value);
                }
            }

            var diffAggregatedAllocations = new Dictionary<long, AggregatedAllocationData>();

            // Deep copy of second trace.
            foreach (var item in traceFiles[1].AggregatedAllocations)
            {
                diffAggregatedAllocations.Add(item.Value.VTableID, new AggregatedAllocationData(item.Value.VTableID, item.Value.Size, item.Value.Count));
            }

            // Diff first aggregated allocations with second.
            foreach (var item in traceFiles[0].AggregatedAllocations)
            {
                AggregatedAllocationData value = null;
                if (diffAggregatedAllocations.TryGetValue(item.Key, out value))
                {
                    // Aggregated allocation still present, reduce second size/count with first, to get delta between dumps
                    value.Size -= item.Value.Size;
                    value.Count -= item.Value.Count;
                }
                else
                {
                    // Allocation not present anymore, add as negative alloc in diff.
                    diffAggregatedAllocations.Add(item.Value.VTableID, new AggregatedAllocationData(item.Value.VTableID, item.Value.Size * -1, item.Value.Count * -1));
                }
            }

            var sortedDiffAggregatedAllocations = new List<AggregatedAllocationData>();
            foreach (var allocation in diffAggregatedAllocations)
            {
                sortedDiffAggregatedAllocations.Add(allocation.Value);
            }

            if (g_aggregateSortField == AggregateSortField.Size)
                sortedDiffAggregatedAllocations.Sort((value1, value2) => (g_aggregateSortMode == SortMode.Ascending ? value2.CompareSize(value1) : value1.CompareSize(value2)));
            else if (g_aggregateSortField == AggregateSortField.Count)
                sortedDiffAggregatedAllocations.Sort((value1, value2) => (g_aggregateSortMode == SortMode.Ascending ? value2.CompareCount(value1) : value1.CompareCount(value2)));

            var tableFormat = "{0,-" + TableVTableIDWidth + "}{1,-" + TableNameWidth + "}{2," + TableSizeWidth + "}{3," + TableCountWidth + "}";

            Console.WriteLine(String.Format(tableFormat, "VTableID", "Type", "Size", "Count"));
            Console.WriteLine(String.Format(tableFormat, "--------", "------", "-----", "------"));

            int count = 1;
            foreach (var allocation in sortedDiffAggregatedAllocations)
            {
                if (count > g_maxAllocations)
                    break;

                if (allocation.Size != 0 && allocation.Count != 0)
                {
                    string name = FormatTypeName(allocation.VTableID, mergedTypeDirectionary, MaxNameWidth, UseGlobalTypeFilter);
                    if (string.IsNullOrEmpty(name))
                        continue;

                    Console.WriteLine(String.Format(tableFormat, "0x" + allocation.VTableID.ToString("X"), name, allocation.Size.ToString("+#;-#;0"), allocation.Count.ToString("+#;-#;0")));
                    count++;
                }
            }
        }

        static void AggregatedAllocationsCommand(Dictionary<long,AggregatedAllocationData> aggregatedAllocations, Dictionary<long, TypeData> classDirectory)
        {
            int TableVTableIDWidth = (int)(g_maxScreenWidth * 0.15);
            int TableAvgSizeWidth = (int)(g_maxScreenWidth * 0.10);
            int TableSizeWidth = (int)(g_maxScreenWidth * 0.10);
            int TableCountWidth = (int)(g_maxScreenWidth * 0.10);
            int TableNameWidth = (int)(g_maxScreenWidth * 0.55);
            int MaxNameWidth = TableNameWidth - 10;

            if (MaxNameWidth < 10)
                MaxNameWidth = 10;

            var sortedAggregatedAllocations = new List<AggregatedAllocationData>();
            foreach (var allocation in aggregatedAllocations)
            {
                sortedAggregatedAllocations.Add(allocation.Value);
            }

            if (g_aggregateSortField == AggregateSortField.AverageSize)
                sortedAggregatedAllocations.Sort((value1, value2) => (g_aggregateSortMode == SortMode.Ascending ? value2.CompareAverageSize(value1) : value1.CompareAverageSize(value2)));
            else if (g_aggregateSortField == AggregateSortField.Size)
                sortedAggregatedAllocations.Sort((value1, value2) => (g_aggregateSortMode == SortMode.Ascending ? value2.CompareSize(value1) : value1.CompareSize(value2)));
            else if (g_aggregateSortField == AggregateSortField.Count)
                sortedAggregatedAllocations.Sort((value1, value2) => (g_aggregateSortMode == SortMode.Ascending ? value2.CompareCount(value1) : value1.CompareCount(value2)));

            var tableFormat = "{0,-" + TableVTableIDWidth + "}{1,-" + TableNameWidth + "}{2,-" + TableAvgSizeWidth + "}{3,-" + TableSizeWidth + "}{4,-" + TableCountWidth + "}";

            Console.WriteLine(String.Format(tableFormat, "VTableID", "Type", "Avg", "Size", "Count"));
            Console.WriteLine(String.Format(tableFormat, "--------", "------", "---------", "-----------", "------"));

            int count = 1;
            foreach (var allocation in sortedAggregatedAllocations)
            {
                if (count > g_maxAllocations)
                    break;

                string name = FormatTypeName(allocation.VTableID, classDirectory, MaxNameWidth, UseGlobalTypeFilter);
                if (string.IsNullOrEmpty(name))
                    continue;

                Console.WriteLine(String.Format(tableFormat, "0x" + allocation.VTableID.ToString("X"), name, allocation.AverageSize, allocation.Size, allocation.Count));
                count++;
            }
        }

        static void Main(string[] args)
        {
            List<TraceFile> traceFiles = new List<TraceFile>();

            try
            {
                if (args.Length < 1)
                {
                    PrintHelp();
                    throw new ArgumentException("Missing trace file argument.");
                }

                g_maxScreenWidth = Console.WindowWidth;

                for (int i = 0; i <= args.Length - 1; i++)
                {
                    if (args[i].Equals("--diff"))
                    {
                        if (traceFiles.Count != 2)
                            throw new ArgumentException($"Diff aggregated allocations needs two trace files.");

                        g_aggregateSortField = AggregateSortField.Size;
                        g_commandType = CommandType.DiffAggregatedAllocations;
                        continue;
                    }
                    else if (args[i].StartsWith("--diff="))
                    {
                        if (traceFiles.Count != 2)
                            throw new ArgumentException($"Diff aggregated allocations needs two trace files.");

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
                            else if (value.Equals("total", StringComparison.OrdinalIgnoreCase))
                            {
                                g_aggregateSortField = AggregateSortField.Size;
                            }
                            else if (value.Equals("count", StringComparison.OrdinalIgnoreCase))
                            {
                                g_aggregateSortField = AggregateSortField.Count;
                            }
                            else if (value.StartsWith("filter=", StringComparison.OrdinalIgnoreCase))
                            {
                                NumberStyles style = NumberStyles.Integer;
                                g_typeFilter = value.Substring(7);
                                if (g_typeFilter.StartsWith("0x"))
                                {
                                    g_typeFilter = g_typeFilter.Substring(2);
                                    style = NumberStyles.HexNumber;
                                }

                                long.TryParse(g_typeFilter, style, CultureInfo.InvariantCulture, out g_vtableID);
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

                                if (!int.TryParse(count, style, CultureInfo.InvariantCulture, out g_maxAllocations))
                                    throw new ArgumentException($"Invalid numeric value, {value}");
                                if (0 > g_maxAllocations)
                                    throw new ArgumentException($"Use postive numeric value, {value}");
                            }
                            else
                            {
                                throw new ArgumentException($"Unknown --diff parameter value, {value}");
                            }
                        }
                        g_commandType = CommandType.AggregatedAllocations;
                        continue;
                    }
                    else if (args[i].Equals("--aggregate"))
                    {
                        if (traceFiles.Count != 1)
                            throw new ArgumentException($"Allocation aggregation only support one trace file.");

                        g_commandType = CommandType.AggregatedAllocations;
                        continue;
                    }
                    else if (args[i].StartsWith("--aggregate="))
                    {
                        if (traceFiles.Count != 1)
                            throw new ArgumentException($"Allocation aggregation only support one trace file.");

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
                                g_aggregateSortField = AggregateSortField.AverageSize;
                            }
                            else if (value.Equals("size", StringComparison.OrdinalIgnoreCase))
                            {
                                g_aggregateSortField = AggregateSortField.Size;
                            }
                            else if (value.Equals("count", StringComparison.OrdinalIgnoreCase))
                            {
                                g_aggregateSortField = AggregateSortField.Count;
                            }
                            else if (value.StartsWith("filter=", StringComparison.OrdinalIgnoreCase))
                            {
                                NumberStyles style = NumberStyles.Integer;
                                g_typeFilter = value.Substring(7);
                                if (g_typeFilter.StartsWith("0x"))
                                {
                                    g_typeFilter = g_typeFilter.Substring(2);
                                    style = NumberStyles.HexNumber;
                                }

                                long.TryParse(g_typeFilter, style, CultureInfo.InvariantCulture, out g_vtableID);
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

                                if (!int.TryParse(count, style, CultureInfo.InvariantCulture, out g_maxAllocations))
                                    throw new ArgumentException($"Invalid numeric value, {value}");
                                if (0 > g_maxAllocations)
                                    throw new ArgumentException($"Use postive numeric value, {value}");
                            }
                            else
                            {
                                throw new ArgumentException($"Unknown --aggregate parameter value, {value}");
                            }
                        }
                        g_commandType = CommandType.AggregatedAllocations;
                        continue;
                    }
                    else
                    {
                        if (File.Exists(args[i]))
                        {
                            traceFiles.Add(new TraceFile(args[i]));
                        }
                        else
                        {
                            throw new ArgumentException($"Unknown command, {args[i]}");
                        }
                    }
                }

                Task streamTask = Task.Run(() =>
                {
                    for (int i = 0; i < traceFiles.Count; ++i)
                    {
                        var source = new EventPipeEventSource(traceFiles[i].FilePath);
                        var monoProfiler = new MonoProfilerTraceEventParser(source);

                        monoProfiler.MonoProfilerGCEvent += delegate (GCEventData data)
                        {
                            ;
                        };

                        monoProfiler.MonoProfilerGCHeapDumpStart += delegate (EmptyTraceData data)
                        {
                            ;
                        };

                        monoProfiler.MonoProfilerGCHeapDumpStop += delegate (EmptyTraceData data)
                        {
                            ;
                        };

                        monoProfiler.MonoProfilerGCHeapDumpObjectReferenceData += delegate (GCHeapDumpObjectReferenceData data)
                        {
                            AggregatedAllocationData value = null;
                            if (!traceFiles[i].AggregatedAllocations.TryGetValue (data.VTableID, out value))
                            {
                                value = new AggregatedAllocationData(data.VTableID, 0, 0);
                                traceFiles[i].AggregatedAllocations.Add (data.VTableID, value);
                            }

                            value.Size += data.ObjectSize;
                            value.Count++;
                        };

                        monoProfiler.MonoProfilerGCHeapDumpVTableClassReference += delegate (GCHeapDumpVTableClassReferenceData data)
                        {
                            TypeData value = null;
                            if (!traceFiles[i].TypeDictionary.TryGetValue(data.VTableID, out value))
                            {
                                value = new TypeData();
                                traceFiles[i].TypeDictionary.Add(data.VTableID, value);
                                value.VTableID = data.VTableID;
                                value.ClassID = data.ClassID;
                                value.ClassName = data.ClassName;
                            }
                        };

                        source.Process();
                    }
                });

                streamTask.Wait();

                switch (g_commandType)
                {
                    case CommandType.AggregatedAllocations:
                        AggregatedAllocationsCommand (traceFiles[0].AggregatedAllocations, traceFiles[0].TypeDictionary);
                        break;
                    case CommandType.DiffAggregatedAllocations:
                        DiffAggregatedAllocationsCommand(traceFiles);
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
