using System;
using LiteNetLib.Utils;
using NUnit.Framework;

namespace Swihoni.Components.Tests
{
    public class SerializerTests
    {
        [Test]
        public void TestSerializer()
        {
            OuterComponent arbitrary = OuterComponent.Arbitrary;
            var writer = new NetDataWriter();
            arbitrary.Serialize(writer);

            var deserialized = new OuterComponent();
            var reader = new NetDataReader(writer.Data);
            deserialized.Deserialize(reader);

            Assert.IsTrue(arbitrary.EqualTo(deserialized));
            Assert.AreNotSame(arbitrary.inner, deserialized.inner);
        }

        [Test]
        public void TestString()
        {
            var arbitrary = new StringProperty("Test");
            var writer = new NetDataWriter();
            arbitrary.Serialize(writer);

            var deserialized = new StringProperty(4);
            var reader = new NetDataReader(writer.Data);
            deserialized.Deserialize(reader);

            Assert.AreEqual("Test", arbitrary.Builder.ToString());
            Assert.AreEqual("Test", deserialized.Builder.ToString());
        }

        [Serializable]
        private class SimpleComponent : ComponentBase
        {
            public ByteProperty b;
        }

        [Serializable]
        private class ByteArrayElement : ArrayElement<SimpleComponent>
        {
            public ByteArrayElement() : base(2) { }
        }

        [Test]
        public void TestNested()
        {
            var nested = new ArrayElement<ByteArrayElement>(2);
            nested[0][0].b.Value = 1;
            nested[0][1].b.Value = 2;
            nested[1][0].b.Value = 3;
            nested[1][1].b.Value = 4;
            
            var writer = new NetDataWriter();
            nested.Serialize(writer);

            var reader = new NetDataReader(writer.Data);
            var deserialized = new ArrayElement<ByteArrayElement>(2);
            deserialized.Deserialize(reader);
            
            Assert.AreEqual(1, deserialized[0][0].b.Value);
            Assert.AreEqual(2, deserialized[0][1].b.Value);
            Assert.AreEqual(3, deserialized[1][0].b.Value);
            Assert.AreEqual(4, deserialized[1][1].b.Value);
        }
    }
}