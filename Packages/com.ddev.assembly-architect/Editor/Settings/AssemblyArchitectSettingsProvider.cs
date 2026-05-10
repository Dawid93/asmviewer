using System.Collections.Generic;
using AssemblyArchitect.Editor.Core.Layout;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AssemblyArchitect.Editor.Settings
{
    internal static class AssemblyArchitectSettingsProvider
    {
        private const string UxmlPath =
            "Packages/com.ddev.assembly-architect/Editor/UI/AssemblyArchitectSettings.uxml";

        [SettingsProvider]
        public static SettingsProvider Create() =>
            new SettingsProvider("Project/Assembly Architect", SettingsScope.Project)
            {
                label    = "Assembly Architect",
                keywords = new HashSet<string> { "asmdef", "graph", "cycle", "layout", "architect" },
                activateHandler = (_, root) => Build(root),
            };

        private static void Build(VisualElement root)
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (uxml != null)
            {
                uxml.CloneTree(root);
                BindFields(root);
            }
            else
            {
                root.Add(new Label("[Assembly Architect] Settings UXML not found.")
                    { style = { color = Color.red } });
            }
        }

        private static void BindFields(VisualElement root)
        {
            var s = AssemblyArchitectSettings.instance;

            // Layout group — EnumField uses BaseField<Enum>, not BaseField<LayoutKind>, so bind directly
            var enumField = root.Q<EnumField>("field-default-layout");
            if (enumField != null)
            {
                enumField.Init(s.DefaultLayout);
                enumField.value = s.DefaultLayout;
                enumField.RegisterValueChangedCallback(evt =>
                {
                    s.DefaultLayout = (LayoutKind)evt.newValue;
                    s.Save();
                });
            }

            Bind<IntegerField, int>(root, "field-force-iterations",
                f => f.value = s.ForceLayoutIterations,
                (f, v) => { s.ForceLayoutIterations = v; s.Save(); });

            // Visualization group
            Bind<ColorField, Color>(root, "field-project-color",
                f => f.value = s.ProjectNodeColor,
                (f, v) => { s.ProjectNodeColor = v; s.Save(); });

            Bind<ColorField, Color>(root, "field-embedded-color",
                f => f.value = s.EmbeddedPkgColor,
                (f, v) => { s.EmbeddedPkgColor = v; s.Save(); });

            Bind<ColorField, Color>(root, "field-registry-color",
                f => f.value = s.RegistryPkgColor,
                (f, v) => { s.RegistryPkgColor = v; s.Save(); });

            Bind<ColorField, Color>(root, "field-cycle-color",
                f => f.value = s.CycleEdgeColor,
                (f, v) => { s.CycleEdgeColor = v; s.Save(); });

            Bind<ColorField, Color>(root, "field-broken-color",
                f => f.value = s.BrokenColor,
                (f, v) => { s.BrokenColor = v; s.Save(); });

            // Build group
            Bind<Toggle, bool>(root, "field-fail-builds",
                f => f.value = s.FailBuildsOnCycles,
                (f, v) => { s.FailBuildsOnCycles = v; s.Save(); });

            // Storage group
            Bind<Toggle, bool>(root, "field-team-shared",
                f => f.value = s.LayoutCacheIsTeamShared,
                (f, v) => { s.LayoutCacheIsTeamShared = v; s.Save(); });

            // Reset to defaults
            var resetBtn = root.Q<Button>("btn-reset");
            if (resetBtn != null)
                resetBtn.clicked += () => { ResetToDefaults(s); root.Clear(); Build(root); };
        }

        private static void ResetToDefaults(AssemblyArchitectSettings s)
        {
            s.DefaultLayout           = LayoutKind.Hierarchical;
            s.FailBuildsOnCycles      = false;
            s.LayoutCacheIsTeamShared = true;
            s.ProjectNodeColor        = new Color(0.30f, 0.63f, 1f);
            s.EmbeddedPkgColor        = new Color(0.63f, 0.42f, 1f);
            s.RegistryPkgColor        = new Color(0.53f, 0.53f, 0.53f);
            s.CycleEdgeColor          = new Color(1f, 0.55f, 0.26f);
            s.BrokenColor             = new Color(1f, 0.30f, 0.30f);
            s.ForceLayoutIterations   = 200;
            s.Save();
        }

        // ── Generic bind helper ───────────────────────────────────────────────

        private static void Bind<TField, TValue>(
            VisualElement root, string name,
            System.Action<TField> init,
            System.Action<TField, TValue> onChange)
            where TField : BaseField<TValue>
        {
            var field = root.Q<TField>(name);
            if (field == null) return;
            init(field);
            field.RegisterValueChangedCallback(evt => onChange(field, evt.newValue));
        }
    }
}
