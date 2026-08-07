using System;
using System.IO;
using System.Text;

namespace PackageBuilder.UnityWorker.Editor
{
    /// <summary>Resolves protocol references beneath one cloned Unity project and writes results atomically.</summary>
    internal static class UnityWorkerFileSystem
    {
        internal static string ResolveProjectReference(string projectRoot, string logicalReference)
        {
            if (!UnityWorkerRequestParser.IsLogicalReference(logicalReference))
            {
                throw new InvalidDataException("UNITY_LOGICAL_REFERENCE_INVALID");
            }

            string root = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string candidate = Path.GetFullPath(Path.Combine(root, logicalReference.Replace('/', Path.DirectorySeparatorChar)));
            string prefix = root + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("UNITY_LOGICAL_REFERENCE_ESCAPES_PROJECT");
            }

            return candidate;
        }

        internal static void WriteAllTextAtomically(string path, string contents)
        {
            string directory = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidDataException("UNITY_RESULT_DIRECTORY_INVALID");
            }

            Directory.CreateDirectory(directory);
            string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            byte[] bytes = new UTF8Encoding(false).GetBytes(contents + "\n");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                if (File.Exists(path))
                {
                    File.Replace(temporary, path, null);
                }
                else
                {
                    File.Move(temporary, path);
                }
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }
    }
}
