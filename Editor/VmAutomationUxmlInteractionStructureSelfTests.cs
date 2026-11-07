#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml.Linq;
using static VMUnityAutomation.Editor.VmAutomationUxmlLayoutModels;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationUxmlInteractionStructureSelfTests
    {
        private const string Path = "Assets/Interaction.uxml";
        private const string Navigation = "<!-- ui-structure: {\"role\":\"navigation\",\"state\":\"section\",\"context\":\"editor\",\"content\":\"Pages\",\"selectedClass\":\"selected\"} -->";
        private const string Tabs = "<ui:VisualElement>" +
            "<!-- ui-structure: {\"role\":\"tab\",\"target\":\"Body\"} --><ui:Button class=\"selected\" text=\"Body\"/>" +
            "<!-- ui-structure: {\"role\":\"tab\",\"target\":\"Face\"} --><ui:Button text=\"Face\"/></ui:VisualElement>";
        private const string Pages = "<ui:VisualElement name=\"Pages\">" +
            "<!-- ui-structure: {\"role\":\"panel\"} --><ui:VisualElement name=\"Body\"/>" +
            "<!-- ui-structure: {\"role\":\"panel\"} --><ui:VisualElement name=\"Face\" style=\"display: none;\"/></ui:VisualElement>";
        private const string PublicationNavigation = "<!-- ui-structure: {\"role\":\"navigation\",\"state\":\"section\",\"context\":\"editor\",\"content\":\"Pages\",\"selectedClass\":\"selected\",\"contentMode\":\"publication\"} -->";
        private const string PublicationTabs = "<ui:VisualElement>" +
            "<!-- ui-structure: {\"role\":\"tab\",\"target\":\"Pages\",\"value\":\"Body\"} --><ui:Button class=\"selected\" text=\"Body\"/>" +
            "<!-- ui-structure: {\"role\":\"tab\",\"target\":\"Pages\",\"value\":\"Face\"} --><ui:Button text=\"Face\"/></ui:VisualElement>";
        private const string PublicationRole = "<!-- ui-structure: {\"role\":\"publication\",\"state\":\"section\",\"context\":\"editor\",\"value\":\"Body\",\"owner\":\"ActualPublisher.Render\"} -->";
        private const string Publication = PublicationRole + "<ui:VisualElement name=\"Pages\"><ui:Label text=\"Body content\"/></ui:VisualElement>";
        private const string Field = "<!-- ui-structure: {\"role\":\"field\"} --><ui:VisualElement>" +
            "<ui:VisualElement style=\"flex-direction: row;\">" +
            "<!-- ui-structure: {\"role\":\"caption\"} --><ui:Label text=\"Width\"/>" +
            "<!-- ui-structure: {\"role\":\"value\"} --><ui:Label text=\"0\"/>" +
            "</ui:VisualElement><ui:Slider/></ui:VisualElement>";

        internal static IReadOnlyList<Dictionary<string, object>> RunSelfTests()
        {
            var cases = new List<Dictionary<string, object>>();
            var valid = Audit(Navigation + Tabs + Pages + Field);
            Add(cases, "structure real tabs and compact field pass with nonzero coverage",
                valid.Passed && valid.InteractionScopes == 1 && valid.InteractionNavigations == 1 &&
                valid.InteractionButtons == 2 && valid.InteractionFields == 1 && valid.InteractionSliders == 1);
            Add(cases, "structure duplicate tab rows for one state fail",
                Has(Audit(Navigation + Tabs + Navigation + Tabs + Pages), "duplicate-navigation-owner"));
            Add(cases, "structure renamed state cannot hide duplicate content entries",
                Has(Audit(Navigation.Replace("section", "camera") + Tabs + Navigation + Tabs + Pages), "duplicate-navigation-target"));
            Add(cases, "structure missing content and destination identities fail",
                Has(Audit(Navigation.Replace("Pages", "Missing") + Tabs + Pages), "navigation-content-missing") &&
                Has(Audit(Navigation + Tabs.Replace("target\":\"Face", "target\":\"Missing") + Pages), "navigation-target-missing"));
            Add(cases, "structure duplicate destination names fail rather than choose one",
                Has(Audit(Navigation + Tabs + Pages.Replace("name=\"Face\"", "name=\"Body\"")), "navigation-target-missing"));
            Add(cases, "structure selected tabs require one active real panel",
                Has(Audit(Navigation + Tabs.Replace("text=\"Face\"", "class=\"selected\" text=\"Face\"") + Pages), "navigation-selection-cardinality") &&
                Has(Audit(Navigation + Tabs + Pages.Replace("style=\"display: none;\"", "")), "navigation-selection-mismatch"));
            Add(cases, "structure navigation detached from content fails",
                Has(Audit(Navigation + Tabs + "<ui:Label text=\"Unrelated\"/>" + Pages), "navigation-content-detached"));
            Add(cases, "structure deleting the tab row leaves unreachable declared panels",
                Has(Audit(Pages), "unreachable-navigation-panel"));
            Add(cases, "structure inserting an unclassified second button row fails automatically",
                Has(Audit(Navigation + Tabs + Pages + "<ui:VisualElement><ui:Button text=\"Full body\"/><ui:Button text=\"Head\"/></ui:VisualElement>"), "unclassified-interaction-group"));
            string choice = "<!-- ui-structure: {\"role\":\"choice\",\"state\":\"shape\",\"context\":\"editor\"} --><ui:VisualElement><ui:Button text=\"Round\"/><ui:Button text=\"Long\"/></ui:VisualElement>";
            Add(cases, "structure independent choice groups may reuse labels and state names",
                Audit(choice + choice.Replace("shape", "material")).Passed);
            Add(cases, "structure independent task scopes may reuse navigation identities",
                Audit(Navigation + Tabs + Pages, secondScope: true).Passed);
            string left = (Navigation + Tabs + Pages).Replace("editor", "left").Replace("Pages", "LeftPages").Replace("Body", "LeftBody").Replace("Face", "LeftFace");
            string right = (Navigation + Tabs + Pages).Replace("editor", "right").Replace("Pages", "RightPages").Replace("Body", "RightBody").Replace("Face", "RightFace");
            Add(cases, "structure independent work contexts may reuse local state names",
                Audit("<!-- ui-structure: {\"role\":\"context\",\"state\":\"left\"} --><ui:VisualElement>" + left + "</ui:VisualElement>" +
                    "<!-- ui-structure: {\"role\":\"context\",\"state\":\"right\"} --><ui:VisualElement>" + right + "</ui:VisualElement>").Passed);
            Add(cases, "structure controls in the wrong work area fail",
                Has(Audit(choice.Replace("context\":\"editor", "context\":\"preview")), "interaction-context-mismatch"));
            Add(cases, "structure tab mixed into an ordinary choice group fails",
                Has(Audit(choice.Replace("<ui:Button text=\"Round\"/>", "<!-- ui-structure: {\"role\":\"tab\",\"target\":\"Body\"} --><ui:Button text=\"Round\"/>")), "mixed-interaction-group"));
            string split = "<!-- ui-structure: {\"role\":\"field\"} --><ui:VisualElement>" +
                "<!-- ui-structure: {\"role\":\"caption\"} --><ui:Label text=\"Width\"/>" +
                "<ui:Slider/><!-- ui-structure: {\"role\":\"value\"} --><ui:Label text=\"0\"/></ui:VisualElement>";
            Add(cases, "structure original label slider reading stack fails",
                Has(Audit(split), "field-reading-separated") && Has(Audit(split), "field-control-separated"));
            Add(cases, "structure a reading in a separate container fails",
                Has(Audit(Field.Replace("<ui:Label text=\"0\"/>", "<ui:VisualElement><ui:Label text=\"0\"/></ui:VisualElement>")), "invalid-field-part-type"));
            Add(cases, "structure a column header does not qualify name value alignment",
                Has(Audit(Field.Replace("flex-direction: row", "flex-direction: column")), "field-reading-separated"));
            Add(cases, "structure unclassified reading cannot escape through omitted metadata",
                Has(Audit(Field.Replace("<!-- ui-structure: {\"role\":\"value\"} -->", "")), "unclassified-field-text"));
            Add(cases, "structure new slider must retain its field relationship",
                Has(Audit("<ui:Slider/>"), "unclassified-input-field"));
            Add(cases, "structure labelled native slider and integer slider remain valid",
                Audit("<ui:Slider label=\"Volume\"/>" + Field.Replace("ui:Slider/>", "ui:SliderInt/>")).Passed);
            Add(cases, "structure malformed role and unknown properties fail",
                Has(Audit("<!-- ui-structure: {\"role\":\"scope\",\"ignore\":\"yes\"} --><ui:VisualElement/>"), "invalid-interaction-contract") &&
                Has(Audit("<!-- ui-structure: {\"role\":\"surprise\"} --><ui:VisualElement/>"), "invalid-interaction-contract"));
            Add(cases, "structure duplicate JSON properties fail instead of overwrite",
                Has(Audit("<!-- ui-structure: {\"role\":\"field\",\"role\":\"scope\"} --><ui:VisualElement/>"), "invalid-interaction-contract"));
            Add(cases, "structure removal of required scope cannot report zero coverage success",
                Has(Audit("", declareScope: false, requireScope: true), "required-interaction-scope-missing"));
            Add(cases, "structure required name must also be unique among unannotated elements",
                Has(Audit("<ui:VisualElement name=\"Task\"/>", requireScope: true), "required-interaction-scope-missing"));
            var unscoped = Audit("", declareScope: false);
            var coverage = (Dictionary<string, object>)unscoped.ToDictionary()["interactionStructure"];
            Add(cases, "structure zero contracts explicitly reports NotDeclared and no runtime acceptance",
                (string)coverage["coverage"] == "NotDeclared" && !(bool)coverage["runtimeEffectsAccepted"]);
            Add(cases, "structure XML and contract work budgets reject instead of truncate",
                Has(Audit(string.Concat(Enumerable.Repeat("<ui:VisualElement/>", 16385))), "interaction-structure-budget") &&
                Has(Audit(string.Concat(Enumerable.Repeat("<!-- ui-structure: {\"role\":\"field\"} --><ui:VisualElement/>", 1025))), "interaction-structure-budget"));
            Add(cases, "structure persistent scope list rejects invalid types instead of dropping coverage",
                RejectsScopes("ignored") && RejectsScopes(new object[] { 1 }) && RejectsScopes(null));
            Add(cases, "structure persistent scope list rejects aliases and duplicate targets",
                RejectsScopes(new[] { "Assets/../Interaction.uxml#Task" }) &&
                RejectsScopes(new[] { "Assets//Interaction.uxml#Task" }) &&
                RejectsScopes(new[] { "Assets/Folder\\Interaction.uxml#Task" }) &&
                RejectsScopes(new[] { Path + "#Task", Path + "#Task" }));
            Add(cases, "structure persistent scope survives exact valid decoding",
                VmAutomationUIToolkitAuditProjectSettings.ReadRequiredInteractionScopes(
                    new Dictionary<string, object> { { "requiredInteractionScopes", new[] { Path + "#Task" } } }).Single() == Path + "#Task");
            AddPublicationCases(cases);
            return cases;
        }

        private static void AddPublicationCases(List<Dictionary<string, object>> cases)
        {
            string complete = PublicationNavigation + PublicationTabs + Publication;
            var valid = Audit(complete);
            Add(cases, "structure shared publication retains real navigation and nonzero destinations",
                valid.Passed && valid.InteractionNavigations == 1 && valid.InteractionPublicationNavigations == 1 &&
                valid.InteractionPublicationDestinations == 2 && valid.InteractionButtons == 2);
            Add(cases, "structure shared body requires distinct destination values",
                Has(Audit(PublicationNavigation + PublicationTabs.Replace("value\":\"Face", "value\":\"Body") + Publication), "duplicate-navigation-value"));
            Add(cases, "structure publication tab must target the actual shared body",
                Has(Audit(PublicationNavigation + PublicationTabs.Replace("target\":\"Pages", "target\":\"Missing") + Publication), "navigation-target-missing"));
            Add(cases, "structure shared publication rejects zero or multiple selections",
                Has(Audit(complete.Replace("class=\"selected\"", "")), "navigation-selection-cardinality") &&
                Has(Audit(complete.Replace("text=\"Face\"", "class=\"selected\" text=\"Face\"")), "navigation-selection-cardinality"));
            Add(cases, "structure publication initial value must match selected destination",
                Has(Audit(PublicationNavigation + PublicationTabs + Publication.Replace("value\":\"Body", "value\":\"Face")), "navigation-selection-mismatch"));
            Add(cases, "structure renamed state cannot hide a second shared content owner",
                Has(Audit(PublicationNavigation + PublicationTabs + PublicationNavigation.Replace("section", "camera") + PublicationTabs + Publication), "duplicate-navigation-target"));
            Add(cases, "structure shared navigation stays adjacent to its content",
                Has(Audit(PublicationNavigation + PublicationTabs + "<ui:Label text=\"Unrelated\"/>" + Publication), "navigation-content-detached"));
            Add(cases, "structure initially selected shared publication cannot be hidden",
                Has(Audit(complete.Replace("name=\"Pages\"", "name=\"Pages\" style=\"display: none;\"")), "navigation-publication-hidden"));
            Add(cases, "structure shared publication requires an explicit publisher role",
                Has(Audit(complete.Replace(PublicationRole, "")), "navigation-publication-missing") &&
                Has(Audit(complete.Replace(",\"owner\":\"ActualPublisher.Render\"", "")), "invalid-interaction-contract"));
            Add(cases, "structure deleting shared navigation leaves an unreachable publication",
                Has(Audit(Publication), "unreachable-navigation-panel"));
            Add(cases, "structure shared publisher state and context must match navigation",
                Has(Audit(PublicationNavigation + PublicationTabs + Publication.Replace("section", "camera")), "navigation-publication-owner-mismatch") &&
                Has(Audit(PublicationNavigation + PublicationTabs + Publication.Replace("editor", "preview")), "navigation-publication-owner-mismatch"));
            Add(cases, "structure static and publication tab contracts cannot be mixed",
                Has(Audit(Navigation + PublicationTabs + Publication), "mixed-interaction-group") &&
                Has(Audit(PublicationNavigation + PublicationTabs.Replace(",\"value\":\"Body\"", "") + Publication), "mixed-interaction-group"));
            Add(cases, "structure unknown publication modes are rejected",
                Has(Audit(PublicationNavigation.Replace("contentMode\":\"publication", "contentMode\":\"alias") + PublicationTabs + Publication), "invalid-interaction-contract"));
            string left = complete.Replace("editor", "left").Replace("Pages", "LeftPages");
            string right = complete.Replace("editor", "right").Replace("Pages", "RightPages");
            Add(cases, "structure independent publication contexts may reuse state and destination values",
                Audit("<!-- ui-structure: {\"role\":\"context\",\"state\":\"left\"} --><ui:VisualElement>" + left + "</ui:VisualElement>" +
                    "<!-- ui-structure: {\"role\":\"context\",\"state\":\"right\"} --><ui:VisualElement>" + right + "</ui:VisualElement>").Passed);
            Add(cases, "structure one actual task root can also own its work context",
                Audit(complete, scopeState: "editor", childContext: false).Passed);
            Add(cases, "structure independent root contexts may reuse publication identities",
                Audit(complete, secondScope: true, scopeState: "editor", childContext: false).Passed);
            Add(cases, "structure scope without state still requires an actual child context",
                Has(Audit(complete, childContext: false), "interaction-context-mismatch"));
            Add(cases, "structure nested task scope cannot inherit the outer work context",
                Has(Audit("<!-- ui-structure: {\"role\":\"scope\"} --><ui:VisualElement>" + complete + "</ui:VisualElement>",
                    scopeState: "editor", childContext: false), "interaction-context-mismatch"));
        }

        private static VmAutomationUxmlLayoutAuditReport Audit(string body, bool declareScope = true,
            bool requireScope = false, bool secondScope = false, string scopeState = null, bool childContext = true)
        {
            string scope = (declareScope ? "<!-- ui-structure: {\"role\":\"scope\"" +
                (scopeState == null ? "" : ",\"state\":\"" + scopeState + "\"") + "} -->" : "") +
                "<ui:VisualElement name=\"Task\">" + (childContext ? "<!-- ui-structure: {\"role\":\"context\",\"state\":\"editor\"} -->" : "") +
                "<ui:VisualElement>" + body + "</ui:VisualElement></ui:VisualElement>";
            if (!declareScope) scope = "<ui:VisualElement name=\"Task\">" + body + "</ui:VisualElement>";
            var xml = XDocument.Parse("<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" + scope +
                (secondScope ? scope : "") + "</ui:UXML>", LoadOptions.SetLineInfo);
            var report = new VmAutomationUxmlLayoutAuditReport(100);
            VmAutomationUxmlInteractionStructureAuditor.Audit(Path, xml,
                new UxmlInlineStyleContractIndex(), requireScope ? new[] { Path + "#Task" } : Array.Empty<string>(), report);
            return report;
        }

        private static bool Has(VmAutomationUxmlLayoutAuditReport report, string kind) =>
            !report.Passed && report.Issues.Any(issue => issue.Kind == kind);

        private static bool RejectsScopes(object value)
        {
            try
            {
                VmAutomationUIToolkitAuditProjectSettings.ReadRequiredInteractionScopes(
                    new Dictionary<string, object> { { "requiredInteractionScopes", value } });
                return false;
            }
            catch (InvalidDataException) { return true; }
        }

        private static void Add(List<Dictionary<string, object>> cases, string name, bool passed) =>
            cases.Add(new Dictionary<string, object> { { "name", name }, { "passed", passed } });
    }
}
#endif
