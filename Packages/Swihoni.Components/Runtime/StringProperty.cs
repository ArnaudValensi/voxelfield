using System;
using System.Text;
using LiteNetLib.Utils;
using UnityEngine;

namespace Swihoni.Components
{
    [Serializable]
    public class StringProperty : PropertyBase
    {
        private readonly int m_MaxSize;
        private const int DefaultSize = byte.MaxValue;

        public StringBuilder Builder { get; }

        public StringProperty() : this(DefaultSize) { }

        public StringProperty(int maxSize)
        {
            if (m_MaxSize > ushort.MaxValue) Debug.Log($"String properties have a max length of {ushort.MaxValue}");
            m_MaxSize = maxSize;
            Builder = new StringBuilder(maxSize);
        }

        public StringProperty(string @string, int maxSize = DefaultSize) : this(maxSize) => SetTo(@string);

        public override StringBuilder AppendValue(StringBuilder builder)
        {
            builder.EnsureCapacity(builder.Length + Builder.Length);
            for (var i = 0; i < Builder.Length; i++) builder.Append(Builder[i]);
            return builder;
        }

        public override void ParseValue(string stringValue) => SetTo(stringValue);

        public bool AsNewString(out string @string)
        {
            if (Builder.Length == 0)
            {
                @string = default;
                return false;
            }
            @string = Builder.ToString();
            return true;
        }

        public string AsNewString() => Builder.ToString();

        private void ThrowIfOverMaxSize() => ThrowIfOverMaxSize(Builder.Length);

        private void ThrowIfOverMaxSize(int size)
        {
            if (size > m_MaxSize) throw new Exception($"String was over max size! Size: {size} Max: {m_MaxSize}");
        }

        public override void Serialize(NetDataWriter writer)
        {
            if (m_MaxSize <= byte.MaxValue) writer.Put((byte) Builder.Length);
            else writer.Put((ushort) Builder.Length);
            for (var i = 0; i < Builder.Length; i++) writer.Put(Builder[i]);
        }

        public override void Deserialize(NetDataReader reader)
        {
            int size = m_MaxSize <= byte.MaxValue ? reader.GetByte() : reader.GetUShort();
            ThrowIfOverMaxSize(size);
            Zero();
            for (var _ = 0; _ < size; _++) Builder.Append(reader.GetChar());
            WithValue = size > 0;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ m_MaxSize;
                hashCode = (hashCode * 397) ^ (Builder != null ? Builder.GetHashCode() : 0);
                return hashCode;
            }
        }

        public bool Equals(string @string) => this == @string;

        public override bool Equals(object other) => this == (StringProperty) other;

        public override bool Equals(PropertyBase other) => this == (StringProperty) other;

        public static bool operator ==(StringProperty s1, string s2) => s1.Builder.ToString() == s2;

        public static bool operator !=(StringProperty s1, string s2) => !(s1 == s2);

        public static bool operator ==(StringProperty s1, StringProperty s2)
        {
            if (s1.Builder.Length != s2.Builder.Length) return false;
            for (var i = 0; i < s1.Builder.Length; i++)
                if (s1.Builder[i] != s2.Builder[i])
                    return false;
            return true;
        }

        public static bool operator !=(StringProperty s1, StringProperty s2) => !(s1 == s2);

        public override void Clear()
        {
            Zero();
            base.Clear();
        }

        public override void Zero() => Builder.Clear();

        public override void SetTo(PropertyBase other)
        {
            if (other is not StringProperty otherString) throw new ArgumentException("Other property is not a string!");
            if (otherString.WithValue)
            {
                ThrowIfOverMaxSize(otherString.Builder.Length);
                Zero();
                Builder.AppendPropertyValue(otherString);
                WithValue = true;
            }
            else Clear();
        }

        public void SetTo(string @string)
        {
            if (@string == null)
            {
                Clear();
                return;
            }
            ThrowIfOverMaxSize(@string.Length);
            Zero();
            Builder.Append(@string);
            WithValue = true;
        }

        public StringProperty Add(string @string)
        {
            ThrowIfOverMaxSize(Builder.Length + @string.Length);
            Builder.Append(@string);
            WithValue = true;
            return this;
        }

        public void SetTo(Action<StringBuilder> action)
        {
            Zero();
            action(Builder);
            ThrowIfOverMaxSize();
            WithValue = true;
        }

        public override void InterpolateFrom(PropertyBase p1, PropertyBase p2, float interpolation) => SetTo(p2);

        public override string ToString() => Builder.ToString();
    }
}