using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace PackageBuilder.UnityWorker.Editor
{
    [Serializable]
    internal sealed class UnityWorkerRequest
    {
        public int protocolVersion;
        public string jobId = string.Empty;
        public string operation = string.Empty;
        public string productManifestReference = string.Empty;
        public string inputDirectoryReference = string.Empty;
        public string outputDirectoryReference = string.Empty;
        public string resultFileReference = string.Empty;
        public string engineVersion = string.Empty;
        public string target = string.Empty;
    }

    /// <summary>Parses the protocol-v1 request without accepting duplicate or unknown root properties.</summary>
    internal static class UnityWorkerRequestParser
    {
        internal const int MaximumRequestCharacters = 65_536;

        private static readonly HashSet<string> AllowedProperties = new HashSet<string>(StringComparer.Ordinal)
        {
            "protocolVersion",
            "jobId",
            "operation",
            "productManifestReference",
            "inputDirectoryReference",
            "outputDirectoryReference",
            "resultFileReference",
            "engineVersion",
            "target",
        };

        internal static bool TryParse(string json, out UnityWorkerRequest request, out string diagnosticCode)
        {
            request = null;
            diagnosticCode = "UNITY_REQUEST_INVALID";
            if (string.IsNullOrWhiteSpace(json) || json.Length > MaximumRequestCharacters)
            {
                return false;
            }

            HashSet<string> properties;
            if (!TryReadRootProperties(json, out properties))
            {
                return false;
            }

            foreach (string property in properties)
            {
                if (!AllowedProperties.Contains(property))
                {
                    diagnosticCode = "UNITY_REQUEST_UNKNOWN_PROPERTY";
                    return false;
                }
            }

            string[] required =
            {
                "protocolVersion",
                "jobId",
                "operation",
                "productManifestReference",
                "inputDirectoryReference",
                "outputDirectoryReference",
                "resultFileReference",
            };
            foreach (string property in required)
            {
                if (!properties.Contains(property))
                {
                    diagnosticCode = "UNITY_REQUEST_MISSING_PROPERTY";
                    return false;
                }
            }

            try
            {
                request = JsonUtility.FromJson<UnityWorkerRequest>(json);
            }
            catch (ArgumentException)
            {
                return false;
            }

            if (request == null ||
                request.protocolVersion != 1 ||
                !IsIdentity(request.jobId) ||
                !IsCanonicalIdentifier(request.operation) ||
                !IsLogicalReference(request.productManifestReference) ||
                !IsLogicalReference(request.inputDirectoryReference) ||
                !IsLogicalReference(request.outputDirectoryReference) ||
                !IsLogicalReference(request.resultFileReference) ||
                (!string.IsNullOrEmpty(request.engineVersion) &&
                    !string.Equals(request.engineVersion, UnityBatchEntrypoint.EngineVersion, StringComparison.Ordinal)) ||
                (!string.IsNullOrEmpty(request.target) &&
                    !string.Equals(request.target, "unity", StringComparison.Ordinal)))
            {
                diagnosticCode = "UNITY_REQUEST_SCHEMA_INVALID";
                request = null;
                return false;
            }

            return true;
        }

        internal static bool IsLogicalReference(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                !string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
                value[0] == '/' ||
                value.IndexOf('\\') >= 0 ||
                value.IndexOf(':') >= 0 ||
                value.IndexOf('$') >= 0 ||
                value.IndexOf('%') >= 0 ||
                value.IndexOf("//", StringComparison.Ordinal) >= 0)
            {
                return false;
            }

            string[] segments = value.Split('/');
            foreach (string segment in segments)
            {
                if (segment.Length == 0 || segment == "." || segment == "..")
                {
                    return false;
                }

                foreach (char character in segment)
                {
                    if (char.IsControl(character))
                    {
                        return false;
                    }
                }
            }

            return !(value.Length >= 2 && char.IsLetter(value[0]) && value[1] == ':');
        }

        private static bool IsIdentity(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 256 ||
                !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                return false;
            }

            foreach (char character in value)
            {
                if (char.IsControl(character))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsCanonicalIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value) || value[0] < 'a' || value[0] > 'z')
            {
                return false;
            }

            bool previousHyphen = false;
            foreach (char character in value)
            {
                bool isLetter = character >= 'a' && character <= 'z';
                if (!isLetter && character != '-')
                {
                    return false;
                }

                if (character == '-' && previousHyphen)
                {
                    return false;
                }

                previousHyphen = character == '-';
            }

            return !previousHyphen;
        }

        private static bool TryReadRootProperties(string json, out HashSet<string> properties)
        {
            properties = new HashSet<string>(StringComparer.Ordinal);
            int index = 0;
            SkipWhitespace(json, ref index);
            if (!Consume(json, ref index, '{'))
            {
                return false;
            }

            SkipWhitespace(json, ref index);
            if (Consume(json, ref index, '}'))
            {
                SkipWhitespace(json, ref index);
                return index == json.Length;
            }

            while (index < json.Length)
            {
                string property;
                if (!TryReadSimpleString(json, ref index, out property) || !properties.Add(property))
                {
                    return false;
                }

                SkipWhitespace(json, ref index);
                if (!Consume(json, ref index, ':'))
                {
                    return false;
                }

                SkipWhitespace(json, ref index);
                if (!SkipPrimitiveValue(json, ref index))
                {
                    return false;
                }

                SkipWhitespace(json, ref index);
                if (Consume(json, ref index, '}'))
                {
                    SkipWhitespace(json, ref index);
                    return index == json.Length;
                }

                if (!Consume(json, ref index, ','))
                {
                    return false;
                }

                SkipWhitespace(json, ref index);
            }

            return false;
        }

        private static bool TryReadSimpleString(string json, ref int index, out string value)
        {
            value = string.Empty;
            if (!Consume(json, ref index, '"'))
            {
                return false;
            }

            var builder = new StringBuilder();
            while (index < json.Length)
            {
                char character = json[index++];
                if (character == '"')
                {
                    value = builder.ToString();
                    return true;
                }

                if (character == '\\' || char.IsControl(character))
                {
                    return false;
                }

                builder.Append(character);
            }

            return false;
        }

        private static bool SkipPrimitiveValue(string json, ref int index)
        {
            if (index >= json.Length || json[index] == '{' || json[index] == '[')
            {
                return false;
            }

            if (json[index] == '"')
            {
                index++;
                bool escaped = false;
                while (index < json.Length)
                {
                    char character = json[index++];
                    if (escaped)
                    {
                        if (character == 'u')
                        {
                            for (int count = 0; count < 4; count++)
                            {
                                if (index >= json.Length || !Uri.IsHexDigit(json[index++]))
                                {
                                    return false;
                                }
                            }
                        }
                        else if ("\"\\/bfnrt".IndexOf(character) < 0)
                        {
                            return false;
                        }

                        escaped = false;
                    }
                    else if (character == '\\')
                    {
                        escaped = true;
                    }
                    else if (character == '"')
                    {
                        return true;
                    }
                    else if (char.IsControl(character))
                    {
                        return false;
                    }
                }

                return false;
            }

            int start = index;
            while (index < json.Length && json[index] != ',' && json[index] != '}')
            {
                if (char.IsWhiteSpace(json[index]))
                {
                    break;
                }

                index++;
            }

            string token = json.Substring(start, index - start);
            return token == "true" || token == "false" || token == "null" || int.TryParse(token, out _);
        }

        private static bool Consume(string text, ref int index, char expected)
        {
            if (index >= text.Length || text[index] != expected)
            {
                return false;
            }

            index++;
            return true;
        }

        private static void SkipWhitespace(string text, ref int index)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
            {
                index++;
            }
        }
    }
}
