using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
    public sealed class VmJsonEnumContractTests
    {
        private enum Ordinary { First, Second }
        [Flags] private enum Station { None = 0, Kitchen = 1, Workbench = 2 }
        [Flags] private enum NamedFlag
        {
            [VmJsonEnumValue("none")] None = 0,
            [VmJsonEnumValue("read")] Read = 1,
            [VmJsonEnumValue("write")] Write = 2
        }
        [Flags] private enum WideFlag : long { None = 0, Low = 1, High = long.MinValue }
        private sealed class Product
        {
            public Product() { }
            public Station? Stations { get; set; }
        }

        [Test]
        public void CombinedFlagsUseTheSameDeclaredNamesInTransportBindingAndSchema()
        {
            const Station combined = Station.Kitchen | Station.Workbench;
            string encoded = (string)VmJsonContract.ToTransportValue(combined);
            Assert.That(encoded, Is.EqualTo("Kitchen, Workbench"));
            Assert.That(VmJsonContract.Bind(encoded, typeof(Station)), Is.EqualTo(combined));
            var schema = VmJsonContract.CreateSchema(typeof(Station));
            Assert.That(schema.ContainsKey("enum"), Is.False);
            Assert.That(schema["x-vmAutomationFlags"], Is.True);
            Assert.That(Regex.IsMatch(encoded, (string)schema["pattern"]), Is.True);
            Assert.That(Regex.IsMatch("Kitchen, Unknown", (string)schema["pattern"]), Is.False);
        }

        [Test]
        public void FlagsUseJsonNamesForEveryMember()
        {
            const NamedFlag combined = NamedFlag.Read | NamedFlag.Write;
            Assert.That(VmJsonContract.ToTransportValue(combined), Is.EqualTo("read, write"));
            Assert.That(VmJsonContract.Bind("read, write", typeof(NamedFlag)), Is.EqualTo(combined));
            Assert.That(VmJsonContract.ToTransportValue(NamedFlag.None), Is.EqualTo("none"));
        }

        [Test]
        public void SignedWideFlagsRetainTheirHighBit()
        {
            const WideFlag combined = WideFlag.High | WideFlag.Low;
            var encoded = (string)VmJsonContract.ToTransportValue(combined);
            Assert.That(VmJsonContract.Bind(encoded, typeof(WideFlag)), Is.EqualTo(combined));
        }

        [Test]
        public void NullableNestedResultAndRequestPreserveTheCombinedValue()
        {
            const Station combined = Station.Kitchen | Station.Workbench;
            var encoded = (Dictionary<string, object>)VmJsonContract.ToTransportValue(new Product { Stations = combined });
            var decoded = (Product)VmJsonContract.Bind(encoded, typeof(Product));
            Assert.That(decoded.Stations, Is.EqualTo(combined));
            var nullable = (Dictionary<string, object>)((Dictionary<string, object>)VmJsonContract.CreateSchema(typeof(Product))["properties"])["Stations"];
            Assert.That(nullable["pattern"], Is.Not.Null);
        }

        [Test]
        public void UndefinedBitsAndUndeclaredSpellingsAreRejected()
        {
            Assert.Throws<InvalidOperationException>(() => VmJsonContract.ToTransportValue((Station)8));
            Assert.Throws<InvalidOperationException>(() => VmJsonContract.Bind("Kitchen, 8", typeof(Station)));
            Assert.Throws<InvalidOperationException>(() => VmJsonContract.Bind("3", typeof(Station)));
            Assert.Throws<InvalidOperationException>(() => VmJsonContract.Bind("Kitchen, Unknown", typeof(Station)));
        }

        [Test]
        public void OrdinaryEnumsRetainTheirSingleValueContract()
        {
            Assert.That(VmJsonContract.ToTransportValue(Ordinary.First), Is.EqualTo("First"));
            Assert.That(VmJsonContract.CreateSchema(typeof(Ordinary)).ContainsKey("enum"), Is.True);
            Assert.Throws<InvalidOperationException>(() => VmJsonContract.Bind("First, Second", typeof(Ordinary)));
            Assert.Throws<InvalidOperationException>(() => VmJsonContract.ToTransportValue((Ordinary)8));
        }
    }
}
