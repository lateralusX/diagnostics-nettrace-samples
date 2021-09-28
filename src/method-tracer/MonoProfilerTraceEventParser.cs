using System;
using System.Text;
using Microsoft.Diagnostics.Tracing;

namespace MethoTracer
{
    public sealed class MonoProfilerTraceEventParser : TraceEventParser
    {
        public static readonly string ProviderName = "Microsoft-DotNETRuntimeMonoProfiler";
        public static readonly Guid ProviderGuid = new Guid(unchecked((int)0x7F442D82), unchecked((short)0x0F1D), unchecked((short)0x5155), 0x4B, 0x8C, 0x15, 0x29, 0xEB, 0x2E, 0x31, 0xC2);

        public enum Keywords : long
        {
            JIT = 0x10,
            MethodTracing = 0x20000000,
        };

        public MonoProfilerTraceEventParser(TraceEventSource source) : base(source) { }

        public event Action<MethodTraceData> MonoProfilerMethodEnter
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new MethodTraceData(value,29, 1, "MonoProfiler", MonoProfilerTaskGuid, 46, "MethodEnter", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, 29, ProviderGuid);
                source.UnregisterEventTemplate(value, 46, MonoProfilerTaskGuid);
            }
        }

        public event Action<MethodTraceData> MonoProfilerMethodLeave
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new MethodTraceData(value, 30, 1, "MonoProfiler", MonoProfilerTaskGuid, 47, "MethodLeave", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, 30, ProviderGuid);
                source.UnregisterEventTemplate(value, 47, MonoProfilerTaskGuid);
            }
        }

        public event Action<MethodTraceData> MonoProfilerMethodTailCall
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new MethodTraceData(value, 31, 1, "MonoProfiler", MonoProfilerTaskGuid, 48, "MethodTailCall", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, 31, ProviderGuid);
                source.UnregisterEventTemplate(value, 48, MonoProfilerTaskGuid);
            }
        }

        public event Action<MethodTraceData> MonoProfilerMethodExceptionLeave
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new MethodTraceData(value, 32, 1, "MonoProfiler", MonoProfilerTaskGuid, 49, "MethodExceptionLeave", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, 32, ProviderGuid);
                source.UnregisterEventTemplate(value, 49, MonoProfilerTaskGuid);
            }
        }

        public event Action<MethodTraceData> MonoProfilerMethodBeginInvoke
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new MethodTraceData(value, 34, 1, "MonoProfiler", MonoProfilerTaskGuid, 51, "MethodBeginInvoke", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, 34, ProviderGuid);
                source.UnregisterEventTemplate(value, 51, MonoProfilerTaskGuid);
            }
        }

        public event Action<MethodTraceData> MonoProfilerMethodEndInvoke
        {
            add
            {
                // action, eventid, taskid, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName
                source.RegisterEventTemplate(new MethodTraceData(value, 35, 1, "MonoProfiler", MonoProfilerTaskGuid, 52, "MethodEndInvoke", ProviderGuid, ProviderName));
            }
            remove
            {
                source.UnregisterEventTemplate(value, 35, ProviderGuid);
                source.UnregisterEventTemplate(value, 52, MonoProfilerTaskGuid);
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

        private const TraceEventID MethodEnterEventID = (TraceEventID)29;
        private const TraceEventID MethodLeaveEventID = (TraceEventID)30;
        private const TraceEventID MethodTailCallID = (TraceEventID)31;
        private const TraceEventID MethodExceptionLeaveID = (TraceEventID)32;
        private const TraceEventID MethodBeginInvokeID = (TraceEventID)34;
        private const TraceEventID MethodEndInvokeID = (TraceEventID)35;
        private const TraceEventID JitDoneVerboseEventID = (TraceEventID)62;

        protected override string GetProviderName() { return ProviderName; }

        public static Guid GetProviderGuid() { return ProviderGuid; }

        public static ulong GetKeywords() { return (ulong)(Keywords.JIT | Keywords.MethodTracing); }

        static private volatile TraceEvent[] s_templates;

        protected override void EnumerateTemplates(Func<string, string, EventFilterResponse> eventsToObserve, Action<TraceEvent> callback)
        {
            if (s_templates == null)
            {
                var templates = new TraceEvent[7];
                templates[0] = new MethodTraceData(null, 29, 1, "MonoProfiler", MonoProfilerTaskGuid, 46, "MethodEnter", ProviderGuid, ProviderName);
                templates[1] = new MethodTraceData(null, 30, 1, "MonoProfiler", MonoProfilerTaskGuid, 47, "MethodLeave", ProviderGuid, ProviderName);
                templates[2] = new MethodTraceData(null, 31, 1, "MonoProfiler", MonoProfilerTaskGuid, 48, "MethodTailCall", ProviderGuid, ProviderName);
                templates[3] = new MethodTraceData(null, 32, 1, "MonoProfiler", MonoProfilerTaskGuid, 49, "MethodExceptionLeave", ProviderGuid, ProviderName);
                templates[4] = new MethodTraceData(null, 34, 1, "MonoProfiler", MonoProfilerTaskGuid, 51, "MethodBeginInvoke", ProviderGuid, ProviderName);
                templates[5] = new MethodTraceData(null, 35, 1, "MonoProfiler", MonoProfilerTaskGuid, 52, "MethodEndInvoke", ProviderGuid, ProviderName);
                templates[6] = new JitTraceDataVerbose(null, 62, 1, "MonoProfiler", MonoProfilerTaskGuid, 79, "JitDoneVerbose", ProviderGuid, ProviderName);
                s_templates = templates;
            }
            foreach (var template in s_templates)
                if (eventsToObserve == null || eventsToObserve(template.ProviderName, template.EventName) == EventFilterResponse.AcceptEvent)
                    callback(template);
        }

        private static readonly Guid MonoProfilerTaskGuid = new Guid(unchecked((int)0x7EC39CC6), unchecked((short)0xC9E3), unchecked((short)0x4328), 0x9B, 0x32, 0xCA, 0x6C, 0x5E, 0xC0, 0xEF, 0x31);
    }

    public sealed class MethodTraceData : TraceEvent
    {
        internal MethodTraceData(Action<MethodTraceData> action, int eventID, int task, string taskName, Guid taskGuid, int opcode, string opcodeName, Guid providerGuid, string providerName)
            : base(eventID, task, taskName, taskGuid, opcode, opcodeName, providerGuid, providerName)
        {
            Action = action;
        }

        public long MethodID { get { return GetInt64At(0); } }

        protected override void Dispatch()
        {
            Action(this);
        }
        protected override Delegate Target {
            get { return Action; }
            set { Action = (Action<MethodTraceData>)value; }
        }
        protected override void Validate()
        {
        }

        public override StringBuilder ToXml(StringBuilder sb)
        {
            Prefix(sb);
            XmlAttribHex(sb, "MethodID", MethodID);
            sb.Append("/>");
            return sb;
        }

        public override string[] PayloadNames {
            get
            {
                if (payloadNames == null)
                {
                    payloadNames = new string[] { "MethodID" };
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
                default:
                    return null;
            }
        }

        private event Action<MethodTraceData> Action;
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
