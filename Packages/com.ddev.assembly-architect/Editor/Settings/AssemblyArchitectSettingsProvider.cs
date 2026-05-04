using System.Collections.Generic;
using AssemblyArchitect.Editor.Core.Layout;
using AssemblyArchitect.Editor.Infrastructure;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace AssemblyArchitect.Editor.Settings
{
    internal static class AssemblyArchitectSettingsProvider
    {
        [SettingsProvider]
        public static SettingsProvider Create()
        {
            return new SettingsProvider("Project/Assembly Architect", SettingsScope.Project)
            {
                label = "Assembly Architect",
                keywords = new HashSet<string>(new[] { "asmdef", "graph", "cycle" }),
                activateHandler = (search, root) => Build(root),
            };
        }

        private static void Build(VisualElement root)
        {
            root.Clear();
            var settings = AssemblyArchitectSettings.instance;

            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.ddev.assembly-architect/Editor/UI/AssemblyArchitectSettings.uxml");
            if (visualTree != null)
                visualTree.CloneTree(root);

            root.Add(BuildSection("Layout"));
            var defaultLayout = new EnumField("Default layout", settings.DefaultLayout);
            defaultLayout.RegisterValueChangedCallback(evt =>
            {
                settings.DefaultLayout = (LayoutKind)evt.newValue;
                settings.Save();
            });
            root.Add(defaultLayout);

            var iterations = new IntegerField("Force layout iterations") { value = settings.ForceLayoutIterations };
            iterations.RegisterValueChangedCallback(evt =>
            {
                settings.ForceLayoutIterations = System.Math.Max(1, evt.newValue);
                settings.Save();
            });
            root.Add(iterations);

            root.Add(BuildSection("Visualization"));
            AddColor(root, "Project node color", settings.ProjectNodeColor, value => settings.ProjectNodeColor = value);
            AddColor(root, "Embedded package color", settings.EmbeddedPkgColor, value => settings.EmbeddedPkgColor = value);
            AddColor(root, "Registry package color", settings.RegistryPkgColor, value => settings.RegistryPkgColor = value);
            AddColor(root, "Cycle edge color", settings.CycleEdgeColor, value => settings.CycleEdgeColor = value);
            AddColor(root, "Broken color", settings.BrokenColor, value => settings.BrokenColor = value);

            root.Add(BuildSection("Build"));
            var failBuilds = new Toggle("Fail builds on cycles") { value = settings.FailBuildsOnCycles };
            failBuilds.RegisterValueChangedCallback(evt =>
            {
                settings.FailBuildsOnCycles = evt.newValue;
                settings.Save();
            });
            root.Add(failBuilds);
            root.Add(new HelpBox("When enabled, builds fail if any dependency cycle exists between project asmdefs. Cycles in package code are ignored.", HelpBoxMessageType.Info));

            root.Add(BuildSection("Storage"));
            var shared = new Toggle("Team-shared layout") { value = settings.LayoutCacheIsTeamShared };
            shared.RegisterValueChangedCallback(evt =>
            {
                var oldValue = settings.LayoutCacheIsTeamShared;
                settings.LayoutCacheIsTeamShared = evt.newValue;
                settings.Save();
                new LayoutCache().MoveFrom(oldValue);
            });
            root.Add(shared);
            root.Add(new HelpBox("When enabled, layout is stored under ProjectSettings and can be shared through source control. Otherwise it is stored under UserSettings.", HelpBoxMessageType.Info));

            var reset = new Button(settings.ResetToDefaults) { text = "Reset to defaults" };
            reset.style.alignSelf = Align.FlexEnd;
            root.Add(reset);
        }

        private static Label BuildSection(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = UnityEngine.FontStyle.Bold;
            label.style.marginTop = 10;
            return label;
        }

        private static void AddColor(VisualElement root, string label, UnityEngine.Color value, System.Action<UnityEngine.Color> set)
        {
            var field = new ColorField(label) { value = value };
            field.RegisterValueChangedCallback(evt =>
            {
                set(evt.newValue);
                AssemblyArchitectSettings.instance.Save();
            });
            root.Add(field);
        }
    }
}
