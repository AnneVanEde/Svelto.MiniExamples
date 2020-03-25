namespace Svelto.ECS.Serialization
{
    public class DefaultSerializer<T> : ISerializer<T> where T : unmanaged, IEntityComponent
    {
        static readonly uint SIZEOFT = SerializableEntityBuilder<T>.SIZE;

        static DefaultSerializer()
        {
            var _type = typeof(T);

            foreach (var field in _type.GetFields())
                if (field.FieldType.ContainsCustomAttribute(typeof(DoNotSerializeAttribute)) &&
                    field.IsPrivate == false)
                    throw new ECSException("field cannot be serialised ".FastConcat(_type.FullName));

            if (_type.GetProperties().Length > (EntityBuilder<T>.HAS_EGID ? 1 : 0))
                throw new ECSException("serializable entity struct must be property less ".FastConcat(_type.FullName));
        }

        public uint size => SIZEOFT;

        public bool Serialize(in T value, ISerializationData serializationData)
        {
            DefaultSerializerUtils.CopyToByteArray(value, serializationData.data.ToArrayFast(out _),
                serializationData.dataPos);

            serializationData.dataPos += SIZEOFT;

            return true;
        }

        public bool Deserialize(ref T value, ISerializationData serializationData)
        {
            value = DefaultSerializerUtils.CopyFromByteArray<T>(serializationData.data.ToArrayFast(out _),
                serializationData.dataPos);

            serializationData.dataPos += SIZEOFT;

            return true;
        }
    }
}