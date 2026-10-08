using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor.PackageManager;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmJsonNumberTests
    {
        [Test]
        public void IndependentBinary64OracleSurvivesParsingAndRepeatedPersistence()
        {
            string package = PackageInfo.FindForAssembly(typeof(VmJsonNumber).Assembly).resolvedPath;
            string file = Path.Combine(package, "Tests/Editor/Fixtures/json-number-oracle.json");
            Assert.That(new FileInfo(file).Length, Is.LessThan(128 * 1024));
            var fixture = (Dictionary<string, object>)MiniJson.Deserialize(File.ReadAllText(file));
            var rows = (List<object>)fixture["rows"];
            Assert.That(rows.Count, Is.EqualTo(1024));
            foreach (Dictionary<string, object> row in rows)
            {
                string text = (string)row["literal"];
                string expected = (string)row["bits"];
                object graph = new Dictionary<string, object> { { "value", VmJsonNumber.ParseDouble(text) } };
                for (int cycle = 0; cycle < 16; cycle++)
                {
                    double number = Convert.ToDouble(((Dictionary<string, object>)graph)["value"]);
                    Assert.That(BitConverter.DoubleToInt64Bits(number).ToString("x16"),
                        Is.EqualTo(expected), text + " cycle " + cycle);
                    graph = MiniJson.Deserialize(MiniJson.Serialize(graph));
                }
            }
        }

        [TestCase("1.00000000000000011102230246251565404236316680908203125", "3ff0000000000000")]
        [TestCase("1.00000000000000011102230246251565404236316680908203126", "3ff0000000000001")]
        [TestCase("1.00000000000000033306690738754696212708950042724609375", "3ff0000000000002")]
        [TestCase("-0", "8000000000000000")]
        [TestCase("-1e-999999999999999999999999", "8000000000000000")]
        [TestCase("0e999999999999999999999999", "0000000000000000")]
        public void ExactMidpointsAndSignedUnderflowHaveDefinedBits(string text, string bits)
        {
            Assert.That(BitConverter.DoubleToInt64Bits(VmJsonNumber.ParseDouble(text)).ToString("x16"), Is.EqualTo(bits));
        }

        [Test]
        public void NonzeroTailAfterTheRetainedPrefixResolvesAnExactMidpoint()
        {
            const string midpoint = "1.00000000000000011102230246251565404236316680908203125";
            string above = midpoint + new string('0', 800) + "1";
            Assert.That(BitConverter.DoubleToInt64Bits(VmJsonNumber.ParseDouble(above)), Is.EqualTo(0x3ff0000000000001L));
            Assert.That(BitConverter.DoubleToInt64Bits(VmJsonNumber.ParseDouble("-" + above)),
                Is.EqualTo(unchecked((long)0xbff0000000000001UL)));
        }

        [TestCase("1e309")]
        [TestCase("-1e999999999999999999999999")]
        [TestCase("NaN")]
        [TestCase("Infinity")]
        [TestCase("1.")]
        [TestCase("01")]
        [TestCase("+1")]
        [TestCase("1e+")]
        public void InvalidOrNonFiniteNumbersCannotPublishReplacementValues(string text)
        {
            Assert.Throws<FormatException>(() => VmJsonNumber.ParseDouble(text));
        }

        [TestCase("{\"value\":01}")]
        [TestCase("{\"value\":1e309}")]
        public void PersistenceParsingRejectsInvalidNumericLexemes(string text)
        {
            Assert.Throws<FormatException>(() => MiniJson.Deserialize(text));
        }

        [Test]
        public void SerializationCannotTurnNonFiniteNumbersIntoNullOrText()
        {
            Assert.Throws<FormatException>(() => MiniJson.Serialize(new { value = double.NaN }));
            Assert.Throws<FormatException>(() => MiniJson.Serialize(new[] { double.PositiveInfinity }));
        }
    }
}
