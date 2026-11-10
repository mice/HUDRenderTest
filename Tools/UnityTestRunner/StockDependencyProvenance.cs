// Copied into the isolated validation consumer by Invoke-CleanValidation.ps1.
// This file is outside Unity asset roots in the source repository.
using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

internal static class StockDependencyProvenance
{
    [Serializable]
    private sealed class Evidence
    {
        public string version;
        public string source;
        public string resolvedPath;
        public string vertexHelperSha256;
        public bool hasCustomCopyIndices;
    }

    [InitializeOnLoadMethod]
    private static void Record()
    {
        var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(VertexHelper).Assembly);
        if (package == null) throw new InvalidOperationException("Cannot resolve the compiled UGUI package.");
        var sourcePath = Path.Combine(package.resolvedPath, "Runtime/UI/Core/Utility/VertexHelper.cs");
        using (var sha = SHA256.Create())
        {
            var evidence = new Evidence
            {
                version = package.version,
                source = package.source.ToString(),
                resolvedPath = package.resolvedPath,
                vertexHelperSha256 = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(sourcePath))).Replace("-", ""),
                hasCustomCopyIndices = typeof(VertexHelper).GetMethod("CopyIndices",
                    new[] { typeof(int[]), typeof(int), typeof(int) }) != null
            };
            File.WriteAllText("Library/StockUguiProvenance.json", JsonUtility.ToJson(evidence, true));
        }
    }
}
