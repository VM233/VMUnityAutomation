using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmUIBuilderPreviewTarget
    {
        private readonly VisualElement root, element;
        private readonly ScrollView[] scrolls;
        private readonly string name;
        private int next;
        private Rect previous;
        internal bool Complete => next == scrolls.Length;
        internal VmUIBuilderPreviewTarget(VisualElement root, string name)
        {
            this.root = root; this.name = name;
            VisualElement match = null; int matches = 0, count = 0;
            root.Query<VisualElement>().ForEach(candidate =>
            {
                if (++count > 2500) throw new InvalidOperationException("UI Builder preview target exceeds2500 adopted elements.");
                if (candidate.name == name) { match = candidate; matches++; }
            });
            if (matches != 1)
                throw new InvalidOperationException("UI Builder preview target requires exactly one adopted element: " + name + "; actual=" + matches);
            element = match;
            var ancestors = new List<ScrollView>();
            int depth = 0;
            for (var current = element.parent; current != null; current = current.parent)
            {
                if (++depth > 64) throw new InvalidOperationException("UI Builder preview target exceeds64 ancestors.");
                if (current is ScrollView scroll) ancestors.Add(scroll);
                if (ReferenceEquals(current, root)) break;
            }
            scrolls = ancestors.ToArray();
            RequireOwner();
        }
        private void RequireOwner()
        {
            if (root.panel == null || element.panel != root.panel || !root.Contains(element))
                throw new InvalidOperationException("UI Builder preview target lost its adopted document owner: " + name);
            for (var current = element; current != null; current = current.parent)
            {
                if (current.resolvedStyle.display == DisplayStyle.None || current.resolvedStyle.visibility != Visibility.Visible)
                    throw new InvalidOperationException("UI Builder preview target is hidden: " + name);
                if (ReferenceEquals(current, root)) break;
            }
        }
        internal void Advance()
        {
            RequireOwner();
            if (Complete) throw new InvalidOperationException("UI Builder preview target already finished native scrolling.");
            var scroll = scrolls[next++];
            var target = element.worldBound; var viewport = scroll.contentViewport.worldBound;
            if (target.width > viewport.width + 1 || target.height > viewport.height + 1)
                throw new InvalidOperationException("UI Builder preview target does not fit its actual ancestor viewport: " + name);
            scroll.ScrollTo(element);
        }
        internal bool Observe(float tolerance)
        {
            RequireOwner();
            var bounds = element.worldBound;
            bool settled = VmAutomationUIBuilderPreviewCommands.WorldRectsAgree(bounds, previous, tolerance);
            previous = bounds;
            return Complete && settled && scrolls.All(scroll =>
                VmAutomationUIBuilderPreviewCommands.IsViewportFramed(bounds, scroll.contentViewport.worldBound, tolerance));
        }
        internal Dictionary<string, object> Receipt() => new Dictionary<string, object>
        {
            { "name", name }, { "scrollViews", scrolls.Length }, { "completedScrolls", next },
            { "worldBound", VmAutomationUIBuilderPreviewCommands.PreviewTargetBounds(element.worldBound) }
        };
    }
}
