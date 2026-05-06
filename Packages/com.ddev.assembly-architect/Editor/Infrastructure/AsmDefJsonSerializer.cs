using System;
using System.Text;
using AssemblyArchitect.Editor.Core;
using UnityEngine;

namespace AssemblyArchitect.Editor.Infrastructure
{
    /// <summary>
    /// Serializes and deserializes <see cref="AsmDefData"/> using Unity's exact <c>.asmdef</c> JSON format:
    /// 4-space indent, LF line endings, trailing newline, and canonical key order.
    /// </summary>
    internal static class AsmDefJsonSerializer
    {
        // ── Deserialization DTOs ──────────────────────────────────────────────
        // Unity's .asmdef format uses lowercase/camelCase JSON keys ("name", "references", …)
        // while AsmDefData uses PascalCase C# fields (Name, References, …).
        // JsonUtility.FromJson is case-sensitive, so we round-trip through these lowercase DTOs.

        [Serializable]
        private sealed class AsmDefDto
        {
            public string   name                  = string.Empty;
            public string   rootNamespace         = string.Empty;
            public string[] references;
            public string[] includePlatforms;
            public string[] excludePlatforms;
            public bool     allowUnsafeCode;
            public bool     overrideReferences;
            public string[] precompiledReferences;
            public bool     autoReferenced;
            public string[] defineConstraints;
            public VersionDefineDto[] versionDefines;
            public bool     noEngineReferences;
        }

        [Serializable]
        private sealed class VersionDefineDto
        {
            public string name       = string.Empty;
            public string expression = string.Empty;
            public string define     = string.Empty;
        }

        /// <summary>
        /// Serializes <paramref name="data"/> to a JSON string that matches Unity's asmdef writer output.
        /// Key order follows the official schema; empty arrays are written as <c>[]</c> on one line.
        /// </summary>
        public static string Serialize(AsmDefData data)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");

            AppendString(sb, "name",             data.Name);
            AppendString(sb, "rootNamespace",    data.RootNamespace);
            AppendStringArray(sb, "references",           data.References);
            AppendStringArray(sb, "includePlatforms",     data.IncludePlatforms);
            AppendStringArray(sb, "excludePlatforms",     data.ExcludePlatforms);
            AppendBool(sb,   "allowUnsafeCode",   data.AllowUnsafeCode);
            AppendBool(sb,   "overrideReferences", data.OverrideReferences);
            AppendStringArray(sb, "precompiledReferences", data.PrecompiledReferences);
            AppendBool(sb,   "autoReferenced",    data.AutoReferenced);
            AppendStringArray(sb, "defineConstraints",    data.DefineConstraints);
            AppendVersionDefines(sb, data.VersionDefines);
            AppendBoolLast(sb, "noEngineReferences", data.NoEngineReferences);

            sb.Append("}\n");
            return sb.ToString();
        }

        /// <summary>Deserializes a <c>.asmdef</c> JSON string into an <see cref="AsmDefData"/> instance.</summary>
        public static AsmDefData Deserialize(string json)
        {
            var dto = JsonUtility.FromJson<AsmDefDto>(json);
            if (dto == null) return new AsmDefData();

            return new AsmDefData
            {
                Name                  = dto.name          ?? string.Empty,
                RootNamespace         = dto.rootNamespace  ?? string.Empty,
                References            = dto.references            ?? Array.Empty<string>(),
                IncludePlatforms      = dto.includePlatforms      ?? Array.Empty<string>(),
                ExcludePlatforms      = dto.excludePlatforms      ?? Array.Empty<string>(),
                AllowUnsafeCode       = dto.allowUnsafeCode,
                AutoReferenced        = dto.autoReferenced,
                OverrideReferences    = dto.overrideReferences,
                PrecompiledReferences = dto.precompiledReferences ?? Array.Empty<string>(),
                DefineConstraints     = dto.defineConstraints     ?? Array.Empty<string>(),
                VersionDefines        = ToVersionDefines(dto.versionDefines),
                NoEngineReferences    = dto.noEngineReferences,
            };
        }

        private static VersionDefine[] ToVersionDefines(VersionDefineDto[] dtos)
        {
            if (dtos == null || dtos.Length == 0) return Array.Empty<VersionDefine>();
            var result = new VersionDefine[dtos.Length];
            for (int i = 0; i < dtos.Length; i++)
                result[i] = new VersionDefine
                {
                    Name       = dtos[i].name       ?? string.Empty,
                    Expression = dtos[i].expression ?? string.Empty,
                    Define     = dtos[i].define      ?? string.Empty,
                };
            return result;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void AppendString(StringBuilder sb, string key, string value)
        {
            sb.Append("    \"").Append(key).Append("\": \"").Append(Escape(value ?? string.Empty)).Append("\",\n");
        }

        private static void AppendBool(StringBuilder sb, string key, bool value)
        {
            sb.Append("    \"").Append(key).Append("\": ").Append(value ? "true" : "false").Append(",\n");
        }

        private static void AppendBoolLast(StringBuilder sb, string key, bool value)
        {
            sb.Append("    \"").Append(key).Append("\": ").Append(value ? "true" : "false").Append("\n");
        }

        private static void AppendStringArray(StringBuilder sb, string key, string[] values)
        {
            sb.Append("    \"").Append(key).Append("\": ");
            if (values == null || values.Length == 0)
            {
                sb.Append("[],\n");
                return;
            }
            sb.Append("[\n");
            for (int i = 0; i < values.Length; i++)
            {
                sb.Append("        \"").Append(Escape(values[i])).Append("\"");
                sb.Append(i < values.Length - 1 ? ",\n" : "\n");
            }
            sb.Append("    ],\n");
        }

        private static void AppendVersionDefines(StringBuilder sb, VersionDefine[] defines)
        {
            sb.Append("    \"versionDefines\": ");
            if (defines == null || defines.Length == 0)
            {
                sb.Append("[],\n");
                return;
            }
            sb.Append("[\n");
            for (int i = 0; i < defines.Length; i++)
            {
                var d = defines[i];
                sb.Append("        {\n");
                sb.Append("            \"name\": \"").Append(Escape(d.Name ?? string.Empty)).Append("\",\n");
                sb.Append("            \"expression\": \"").Append(Escape(d.Expression ?? string.Empty)).Append("\",\n");
                sb.Append("            \"define\": \"").Append(Escape(d.Define ?? string.Empty)).Append("\"\n");
                sb.Append("        }");
                sb.Append(i < defines.Length - 1 ? ",\n" : "\n");
            }
            sb.Append("    ],\n");
        }

        private static string Escape(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
