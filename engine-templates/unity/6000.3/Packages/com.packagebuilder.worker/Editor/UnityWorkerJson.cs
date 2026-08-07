using System;
using System.Globalization;
using System.Text;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Writes compact deterministic protocol JSON without adding a runtime JSON dependency.</summary>
    internal static class UnityWorkerJson
    {
        internal static string Progress(string jobId, string stage, string message, int percent)
        {
            return "{\"protocolVersion\":1,\"eventKind\":\"progress\",\"jobId\":" +
                Quote(jobId) + ",\"stage\":" + Quote(stage) + ",\"message\":" + Quote(message) +
                ",\"percent\":" + percent.ToString(CultureInfo.InvariantCulture) + "}";
        }

        internal static string Metric(string jobId, string metricId, long value, string unit)
        {
            return "{\"protocolVersion\":1,\"eventKind\":\"metric\",\"jobId\":" + Quote(jobId) +
                ",\"metric\":{\"metricId\":" + Quote(metricId) + ",\"value\":" +
                value.ToString(CultureInfo.InvariantCulture) + ",\"unit\":" + Quote(unit) + "}}";
        }

        internal static string SuccessResult(string jobId, string artifactReference, long byteCount)
        {
            return "{\"protocolVersion\":1,\"jobId\":" + Quote(jobId) +
                ",\"status\":\"success\",\"workerVersion\":\"1.0.0\",\"engineVersion\":" +
                Quote(UnityBatchEntrypoint.EngineVersion) +
                ",\"outputsPromoted\":false,\"artifacts\":[{\"artifactId\":\"UnityWorkerProbe\",\"jobId\":" +
                Quote(jobId) + ",\"role\":\"validation-report\",\"logicalReference\":" +
                Quote(artifactReference) + ",\"target\":\"unity\",\"byteCount\":" +
                byteCount.ToString(CultureInfo.InvariantCulture) +
                "}],\"findings\":[],\"metrics\":[{\"metricId\":\"assets-saved\",\"value\":1,\"unit\":\"count\"}]," +
                "\"logReferences\":[],\"retrySafety\":\"unsafe\"}";
        }

        internal static string FailureResult(string jobId, string code, string explanation)
        {
            return "{\"protocolVersion\":1,\"jobId\":" + Quote(jobId) +
                ",\"status\":\"failure\",\"workerVersion\":\"1.0.0\",\"engineVersion\":" +
                Quote(UnityBatchEntrypoint.EngineVersion) +
                ",\"outputsPromoted\":false,\"artifacts\":[],\"findings\":[{\"code\":" + Quote(code) +
                ",\"severity\":\"error\",\"explanation\":" + Quote(explanation) +
                ",\"source\":\"unity-worker\",\"blocksRelease\":true}],\"metrics\":[],\"logReferences\":[]," +
                "\"retrySafety\":\"safe\"}";
        }

        internal static string CancelledResult(string jobId)
        {
            return "{\"protocolVersion\":1,\"jobId\":" + Quote(jobId) +
                ",\"status\":\"cancelled\",\"workerVersion\":\"1.0.0\",\"engineVersion\":" +
                Quote(UnityBatchEntrypoint.EngineVersion) +
                ",\"outputsPromoted\":false,\"artifacts\":[],\"findings\":[],\"metrics\":[]," +
                "\"logReferences\":[],\"retrySafety\":\"safe\",\"cancellation\":{\"outcome\":\"acknowledged\"}}";
        }

        private static string Quote(string value)
        {
            var builder = new StringBuilder(value.Length + 2);
            builder.Append('"');
            foreach (char character in value)
            {
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\b': builder.Append("\\b"); break;
                    case '\f': builder.Append("\\f"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (char.IsControl(character))
                        {
                            builder.Append("\\u");
                            builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            builder.Append(character);
                        }
                        break;
                }
            }

            builder.Append('"');
            return builder.ToString();
        }
    }
}
