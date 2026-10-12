using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmUssDeclarationEditingTests
    {
        [TestCase("/* before; padding-top: 700px; */\n")]
        [TestCase("/* { .fake { color: red; } } */\n")]
        [TestCase("/* uss-audit: allow-single-use retained; authoring reason */\n")]
        [TestCase("/* comment with quoted ';:}' data */\n")]
        public void UpdatingAValuePreservesCommentsAndOtherRules(string comment)
        {
            string source = comment + ".before { color: red; }\n.row {\n" + comment +
                "    min-height: 178px;\n    margin-top: -89px;\n}\n.after { color: blue; }\n";
            string result = Edit(source, "set-declaration", ".row", "margin-top", "-144px");
            Assert.That(result, Is.EqualTo(source.Replace("margin-top: -89px", "margin-top: -144px")));
            var rules = VmAutomationUssStyleSheetParser.ParseStyleSheet("", result);
            Assert.That(rules.Count, Is.EqualTo(3));
            Assert.That(rules[1].Declarations.Count, Is.EqualTo(2));
            Assert.That(rules[1].Declarations["margin-top"], Is.EqualTo("-144px"));
        }

        [TestCase("\"a;b:c\"")]
        [TestCase("\"{a;b}/*literal*/\"")]
        public void QuotedDelimitersRemainInTheirOriginalDeclaration(string value)
        {
            string source = ".row { --caption: " + value + "; padding-top: 4px; }";
            string result = Edit(source, "set-declaration", ".row", "padding-top", "8px");
            Assert.That(result, Is.EqualTo(source.Replace("4px", "8px")));
            var rule = VmAutomationUssStyleSheetParser.ParseStyleSheet("", result)[0];
            Assert.That(rule.Declarations["--caption"], Is.EqualTo(value));
            Assert.That(rule.Declarations.Count, Is.EqualTo(2));
        }

        [Test]
        public void UpdatingPreservesCrLfAndDeclarationOrder()
        {
            const string source = ".row {\r\n\tcolor: red;\r\n\tmargin-top: -89px;\r\n}\r\n";
            Assert.That(Edit(source, "set-declaration", ".row", "margin-top", "-144px"),
                Is.EqualTo(source.Replace("-89px", "-144px")));
        }

        [Test]
        public void EscapedQuotesDoNotEndTheQuotedValue()
        {
            const string value = "\"quoted \\\"; brace } data\"";
            string source = ".row { --caption: " + value + "; color: red; }";
            string result = Edit(source, "set-declaration", ".row", "color", "blue");
            Assert.That(result, Is.EqualTo(source.Replace("red", "blue")));
            Assert.That(VmAutomationUssStyleSheetParser.ParseStyleSheet("", result)[0]
                .Declarations["--caption"], Is.EqualTo(value));
        }

        [Test]
        public void RemovingAPropertyRemovesAllItsOccurrences()
        {
            const string source = ".row { color: red; /* keep */ margin-top: 1px; margin-top: 2px; }";
            string result = Edit(source, "remove-declaration", ".row", "margin-top");
            var declarations = VmAutomationUssStyleSheetParser.ParseStyleSheet("", result)[0].Declarations;
            Assert.That(declarations.ContainsKey("margin-top"), Is.False);
            Assert.That(declarations["color"], Is.EqualTo("red"));
            Assert.That(result, Does.Contain("/* keep */"));
        }

        [Test]
        public void UpdatingChangesTheWinningDuplicateOnly()
        {
            const string source = ".row { margin-top: 1px; color: red; margin-top: 2px; }";
            string result = Edit(source, "set-declaration", ".row", "margin-top", "3px");
            Assert.That(result, Is.EqualTo(source.Replace("2px", "3px")));
            Assert.That(VmAutomationUssStyleSheetParser.ParseStyleSheet("", result)[0]
                .Declarations["margin-top"], Is.EqualTo("3px"));
        }

        [Test]
        public void AddingADeclarationPreservesTheExistingBody()
        {
            const string source = ".row {\r\n    color: red;\r\n}\r\n";
            Assert.That(Edit(source, "set-declaration", ".row", "margin-top", "3px"),
                Is.EqualTo(".row {\r\n    color: red;\r\n    margin-top: 3px;\r\n}\r\n"));
        }

        [Test]
        public void SelectorReplacementAndRemovalKeepPrecedingComments()
        {
            const string source = ".before { color: red; }\n/* retained; { } */\n.row { color: blue; }\n";
            var operation = new Dictionary<string, object>
            {
                { "type", "upsert-selector" }, { "selector", ".row" },
                { "declarations", new Dictionary<string, object> { { "color", "yellow" } } }
            };
            string replaced = Apply(source, operation);
            Assert.That(replaced, Does.StartWith(".before { color: red; }\n/* retained; { } */\n"));
            Assert.That(VmAutomationUssStyleSheetParser.ParseStyleSheet("", replaced)[1]
                .Declarations["color"], Is.EqualTo("yellow"));
            Assert.That(Edit(replaced, "remove-selector", ".row"),
                Is.EqualTo(".before { color: red; }\n/* retained; { } */\n\n"));
        }

        private static string Edit(string source, string type, string selector, string property = null,
            string value = null)
        {
            var operation = new Dictionary<string, object> { { "type", type }, { "selector", selector } };
            if (property != null) operation.Add("property", property);
            if (value != null) operation.Add("value", value);
            return Apply(source, operation);
        }

        private static string Apply(string source, Dictionary<string, object> operation)
        {
            var method = typeof(VmAutomationUIAuthoringCommands).GetMethod("ApplyUssOperation",
                BindingFlags.Static | BindingFlags.NonPublic);
            return (string)method.Invoke(null, new object[] { source, operation });
        }
    }
}
