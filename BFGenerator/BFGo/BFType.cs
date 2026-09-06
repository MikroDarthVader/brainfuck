namespace BFGo
{
    /// <summary>
    /// Abstract type descriptor. Knows its size and internal field layout.
    /// Provides methods to create real root descriptors and recursive copy/move.
    /// </summary>
    public class BFType
    {
        /// <summary>Total size of this type in cells.</summary>
        public int Size { get; private set; }

        // Fields registered via RegisterField (for recursive copy/move)
        private readonly List<(BFType child, int offset)> childFields = [];
        private BFType? parentType = null;
        private int offsetInParent = 0;

        /// <summary>
        /// Creates a root type with the given size.
        /// </summary>
        protected BFType(int size = 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(size);
            Size = size;
        }

        /// <summary>
        /// Registers a field in a struct type. Assigns a child descriptor to the field
        /// and updates the struct's size. For nested structs, parent links are updated recursively.
        /// </summary>
        protected T RegisterField<T>(T fieldType) where T : BFType
        {
            if (fieldType.parentType != null)
                throw new InvalidOperationException($"Type instance {fieldType.GetType().Name} " +
                    $"is already registered as a field in another struct. Create a new instance.");

            int offset = Size;
            fieldType.parentType = this;
            fieldType.offsetInParent = offset;

            Size += fieldType.Size;
            childFields.Add((fieldType, offset));
            return fieldType;
        }

        protected BFType RegisterField(int size) => RegisterField(new BFType(size));

        /// <summary>
        /// Creates a root descriptor by applying this type's layout to the given root.
        /// For root types, creates a descriptor directly. For fields, delegates to the skeleton's <see cref="ChildDescriptor.From"/>.
        /// </summary>
        public BFVar From(BFVar rootDesc)
        {
            int totalOffset = 0;
            BFType? current = this;
            while (current != null)
            {
                totalOffset += current.offsetInParent;
                current = current.parentType;
            }
            return new BFVar(rootDesc, totalOffset, Size);
        }

        /// <summary>
        /// Recursively copies data from source to target, respecting the structure 
        /// of both the source type and the provided target type configuration.
        /// </summary>
        public void Copy(BFVar source, BFVar target, BFType targetType)
        {
            if (GetType() != targetType.GetType())
                throw new InvalidOperationException($"Type mismatch: Cannot transfer data from '{GetType().Name}' to '{targetType.GetType().Name}'.");

            if (childFields.Count == 0)
            {
                source.CopyTo(target);
            }
            else
            {
                for (int i = 0; i < childFields.Count; i++)
                {
                    var (child, offset) = childFields[i];
                    var tgtFieldMeta = targetType.childFields[i];

                    var srcField = source.Offset(offset, child.Size);
                    var tgtField = target.Offset(tgtFieldMeta.offset, tgtFieldMeta.child.Size);

                    child.Copy(srcField, tgtField, tgtFieldMeta.child);
                }
            }
        }

        /// <summary>
        /// Recursively moves data from source to target (source is cleared), respecting the structure 
        /// of both the source type and the provided target type configuration.
        /// </summary>
        public void Move(BFVar source, BFVar target, BFType targetType)
        {
            if (GetType() != targetType.GetType())
                throw new InvalidOperationException($"Type mismatch: Cannot transfer data from '{GetType().Name}' to '{targetType.GetType().Name}'.");

            if (childFields.Count == 0)
            {
                source.MoveTo(target);
            }
            else
            {
                for (int i = 0; i < childFields.Count; i++)
                {
                    var (child, offset) = childFields[i];
                    var tgtFieldMeta = targetType.childFields[i];

                    var srcField = source.Offset(offset, child.Size);
                    var tgtField = target.Offset(tgtFieldMeta.offset, tgtFieldMeta.child.Size);

                    child.Move(srcField, tgtField, tgtFieldMeta.child);
                }
            }
        }

    }
}