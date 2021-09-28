using System;
using System.Text;
using Microsoft.Diagnostics.Tracing;

namespace StartupTracer
{
    public sealed class MonoProfilerTraceEventParser : TraceEventParser
    {
        public static readonly string ProviderName = "Microsoft-DotNETRuntimeMonoProfiler";
        public static readonly Guid ProviderGuid = new Guid(unchecked((int)0x7F442D82), unchecked((short)0x0F1D), unchecked((short)0x5155), 0x4B, 0x8C, 0x15, 0x29, 0xEB, 0x2E, 0x31, 0xC2);

        public enum Keywords : long
        {
            Jit = 0x10,
        };

        public MonoProfilerTraceEventParser(TraceEventSource source) : base(source) { }

        public event Action<JitTraceData> MonoProfilerJitBegin {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new JitTraceData(value, 8, 1, "MonoProfiler", MonoProfilerTaskGuid, 25, "JitBegin", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, 8, ProviderGuid);
                source.UnregisterEventTemplate(value, 25, MonoProfilerTaskGuid);
            }
        }

        public event Action<JitTraceData> MonoProfilerJitDone {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new JitTraceData(value, 10, 1, "MonoProfiler", MonoProfilerTaskGuid, 27, "JitDone", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, 10, ProviderGuid);
                source.UnregisterEventTemplate(value, 27, MonoProfilerTaskGuid);
            }
        }

        public event Action<JitTraceDataVerbose> MonoProfilerJitDoneVerbose {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new JitTraceDataVerbose(value, 62, 1, "MonoProfiler", MonoProfilerTaskGuid, 79, "JitDoneVerbose", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, 62, ProviderGuid);
                source.UnregisterEventTemplate(value, 79, MonoProfilerTaskGuid);
            }
        }

        private const TraceEventID JitBeginEventID = (TraceEventID)8;
        private const TraceEventID JitDoneEventID = (TraceEventID)10;
        private const TraceEventID JitDoneVerboseEventID = (TraceEventID)62;

        protected override string GetProviderName() { return ProviderName; }

        public static Guid GetProviderGuid() { return ProviderGuid; }

        public static ulong GetKeywords() { return (ulong)Keywords.Jit; }

        static private volatile TraceEvent[] s_templates;

        protected override void EnumerateTemplates(Func<string, string, EventFilterResponse> eventsToObserve, Action<TraceEvent> callback)
        {
            if (s_templates == null)
            {
                var templates = new TraceEvent[3];
                templates[0] = new JitTraceData(null, 8, 1, "MonoProfiler", MonoProfilerTaskGuid, 25, "JitBegin", ProviderGuid, ProviderName);
                templates[1] = new JitTraceData(null, 10, 1, "MonoProfiler", MonoProfilerTaskGuid, 27, "JitDone", ProviderGuid, ProviderName);
                templates[2] = new JitTraceDataVerbose(null, 62, 1, "MonoProfiler", MonoProfilerTaskGuid, 79, "JitDoneVerbose", ProviderGuid, ProviderName);
                s_templates = templates;
            }
            foreach (var template in s_templates)
                if (eventsToObserve == null || eventsToObserve(template.ProviderName, template.EventName) == EventFilterResponse.AcceptEvent)
                    callback(template);
        }

        private static readonly Guid MonoProfilerTaskGuid = new Guid(unchecked((int)0x7EC39CC6), unchecked((short)0xC9E3), unchecked((short)0x4328), 0x9B, 0x32, 0xCA, 0x6C, 0x5E, 0xC0, 0xEF, 0x31);
    }

    public sealed class JitTraceData : TraceEvent
    {
        internal JitTraceData(Action<JitTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            Action = action;
        }

        public long MethodID { get { return GetInt64At(0); } }

        public long ModuleID { get { return GetInt64At(8); } }

        public int MethodToken { get { return GetInt32At(16); } }

        protected override void Dispatch()
        {
            Action(this);
        }
        protected override Delegate Target {
            get { return Action; }
            set { Action = (Action<JitTraceData>)value; }
        }
        protected override void Validate()
        {
        }

        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            XmlAttribHex(sb, "MethodID", MethodID);
            XmlAttribHex(sb, "ModuleID", ModuleID);
            XmlAttribHex(sb, "MethodToken", MethodToken);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames {
            get
            {
                if (payloadNames == null)
                {
                    payloadNames = new string[] { "MethodID", "ModuleID", "MethodToken" };
                }

                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return MethodID;
                case 1:
                    return ModuleID;
                case 2:
                    return MethodToken;
                default:
                    return null;
            }
        }

        private event Action<JitTraceData> Action;
    }

    public sealed class JitTraceDataVerbose : TraceEvent
    {
        internal JitTraceDataVerbose(Action<JitTraceDataVerbose> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            Action = action;
        }

        public long MethodID { get { return GetInt64At(0); } }

        public string MethodNamespace { get { return GetUnicodeStringAt(8); } }

        public string MethodName { get { return GetUnicodeStringAt(SkipUnicodeString(8)); } }

        public string MethodSignature { get { return GetUnicodeStringAt(SkipUnicodeString(SkipUnicodeString(8))); } }

        protected override void Dispatch()
        {
            Action(this);
        }
        protected override Delegate Target {
            get { return Action; }
            set { Action = (Action<JitTraceDataVerbose>)value; }
        }
        protected override void Validate()
        {
        }

        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            XmlAttribHex(sb, "MethodID", MethodID);
            XmlAttrib(sb, "MethodNamespace", MethodNamespace);
            XmlAttrib(sb, "MethodName", MethodName);
            XmlAttrib(sb, "MethodSignature", MethodSignature);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames {
            get
            {
                if (payloadNames == null)
                {
                    payloadNames = new string[] { "MethodID", "MethodNamespace", "MethodName", "MethodSignature" };
                }

                return payloadNames;
            }
        }

        public override object PayloadValue(int index)
        {
            switch (index)
            {
                case 0:
                    return MethodID;
                case 1:
                    return MethodNamespace;
                case 2:
                    return MethodName;
                case 3:
                    return MethodSignature;
                default:
                    return null;
            }
        }

        private event Action<JitTraceDataVerbose> Action;
    }
}
