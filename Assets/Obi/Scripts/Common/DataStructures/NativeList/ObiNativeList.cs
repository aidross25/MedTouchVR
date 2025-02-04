using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using System.Collections;
using UnityEngine.Rendering;
using System.Linq;

namespace Obi
{
    public unsafe class ObiNativeList<T> : IEnumerable<T>, IDisposable, ISerializationCallbackReceiver where T : struct
    {
        public T[] serializedContents;
        protected void* m_AlignedPtr = null;

        protected int m_Stride;
        protected int m_Capacity;
        protected int m_Count;
        [SerializeField] protected int m_AlignBytes = 16;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        protected AtomicSafetyHandle m_SafetyHandle;
#endif

        protected GraphicsBuffer.Target m_ComputeBufferType;
        protected GraphicsBuffer m_ComputeBuffer;
        protected GraphicsBuffer m_CountBuffer; // used to hold the counter value in case m_ComputeBufferType is Counter.
        protected bool computeBufferDirty = false;
        protected AsyncGPUReadbackRequest m_AsyncRequest;
        protected AsyncGPUReadbackRequest m_CounterAsyncRequest;

        public int count
        {
            set
            {
                if (value != m_Count)
                {
                    // we should not use ResizeUninitialized as it would destroy all current data.
                    // we first ensure we can hold the previous count, and then set the new one.
                    EnsureCapacity(m_Count);
                    m_Count = Mathf.Min(m_Capacity, value);
                }
            }
            get { return m_Count; }
        }

        public int capacity
        {
            get { return m_Capacity; }
        }

        public int stride
        {
            get { return m_Stride; }
        }

        public bool isCreated
        {
            get { return m_AlignedPtr != null; }
        }

        public bool noReadbackInFlight
        {
            get { return m_AsyncRequest.done && (m_ComputeBufferType != GraphicsBuffer.Target.Counter || m_CounterAsyncRequest.done); }
        }

        // Returns the current compute buffer representation of this list. Will return null if AsComputeBuffer() hasn't been called yet,
        // or if the list has been disposed of.
        public GraphicsBuffer computeBuffer
        {
            get { return m_ComputeBuffer; }
        }

        public T this[int index]
        {
            get
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (index < 0 || index >= m_Capacity)
                {
                    throw new IndexOutOfRangeException($"Reading from index {index} is out of range of '{m_Capacity}' Capacity.");
                }
#endif
                return UnsafeUtility.ReadArrayElementWithStride<T>(m_AlignedPtr, index, m_Stride);
            }
            set
            {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                if (index < 0 || index >= m_Capacity)
                {
                    throw new IndexOutOfRangeException($"Writing to index {index} is out of range of '{m_Capacity}' Capacity.");
                }
#endif
                UnsafeUtility.WriteArrayElementWithStride<T>(m_AlignedPtr, index, m_Stride, value);
                computeBufferDirty = true;
            }
        }

        // Declare parameterless constructor, called by Unity upon deserialization.
        protected ObiNativeList()
        {
            m_Stride = UnsafeUtility.SizeOf<T>();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_SafetyHandle = AtomicSafetyHandle.Create();
#endif
        }

        public ObiNativeList(int capacity = 8, int alignment = 16)
        {
            m_Stride = UnsafeUtility.SizeOf<T>();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            m_SafetyHandle = AtomicSafetyHandle.Create();
#endif
            m_AlignBytes = alignment;
            ChangeCapacity(capacity);
        }

        ~ObiNativeList()
        {
            Dispose(false);
        }

        protected void Dispose(bool disposing)
        {
            DisposeOfComputeBuffer();

            if (isCreated)
            {
                // free unmanaged memory buffer:
                UnsafeUtility.Free(m_AlignedPtr, Allocator.Persistent);
                m_AlignedPtr = null;
                m_Count = m_Capacity = 0;

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                // dispose of atomic safety handle:
                AtomicSafetyHandle.CheckDeallocateAndThrow(m_SafetyHandle);
                AtomicSafetyHandle.Release(m_SafetyHandle);
#endif
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public void DisposeOfComputeBuffer()
        {
            // dispose of compute buffer representation:
            if (m_ComputeBuffer != null)
            {
                // if there's any pending async readback, finalize it.
                // otherwise we pull the rug from under the readbacks' feet and that's no good.
                WaitForReadback();

                m_ComputeBuffer.Dispose();
                m_ComputeBuffer = null;
            }

            if (m_CountBuffer != null)
            {
                m_CountBuffer.Dispose();
                m_CountBuffer = null;
            }
        }

        public void OnBeforeSerialize()
        {
            if (isCreated)
            {
                // create a new managed array to serialize the data:
                serializedContents = new T[m_Count];

                // pin the managed array and get its address:
                ulong serializedContentsHandle;
                var serializedContentsAddress = UnsafeUtility.PinGCArrayAndGetDataAddress(serializedContents, out serializedContentsHandle);

                // copy data over to the managed array:
                UnsafeUtility.MemCpy(serializedContentsAddress, m_AlignedPtr, m_Count * m_Stride);

                // unpin the managed array:
                UnsafeUtility.ReleaseGCObject(serializedContentsHandle);
            }
        }

        public void OnAfterDeserialize()
        {
            if (serializedContents != null)
            {
                // resize to receive the serialized data:
                ResizeUninitialized(serializedContents.Length);

                // pin the managed array and get its address:
                ulong serializedContentsHandle;
                var serializedContentsAddress = UnsafeUtility.PinGCArrayAndGetDataAddress(serializedContents, out serializedContentsHandle);

                // copy data from the managed array:
                UnsafeUtility.MemCpy(m_AlignedPtr, serializedContentsAddress, m_Count * m_Stride);

                // unpin the managed array:
                UnsafeUtility.ReleaseGCObject(serializedContentsHandle);
            }
        }

        // Reinterprets the data in the list as a native array.
        public NativeArray<U> AsNativeArray<U>() where U : struct
        {
            return AsNativeArray<U>(m_Count);
        }

        public NativeArray<T> AsNativeArray()
        {
            return AsNativeArray<T>(m_Count);
        }

        // Reinterprets the data in the list as a native array of the given length, up to the list's capacity.
        public NativeArray<U> AsNativeArray<U>(int arrayLength) where U : struct
        {
            unsafe
            {
                NativeArray<U> array = NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray<U>(m_AlignedPtr, Mathf.Min(arrayLength, m_Capacity), Allocator.None);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                NativeArrayUnsafeUtility.SetAtomicSafetyHandle(ref array, m_SafetyHandle);
#endif
                // assume the NativeArray will write new data, so we'll need to update the computeBuffer upon Upload().
                computeBufferDirty = true;
                return array;
            }
        }

        // Reinterprets the data in the list as a compute buffer, in case of an empty list it returns a buffer of size 1 with uninitialized content.
        public GraphicsBuffer SafeAsComputeBuffer<U>(GraphicsBuffer.Target bufferType = GraphicsBuffer.Target.Structured) where U : struct
        {
            return AsComputeBuffer<U>(Mathf.Max(1,m_Count), bufferType);
        }

        // Reinterprets the data in the list as a compute buffer.
        public GraphicsBuffer AsComputeBuffer<U>(GraphicsBuffer.Target bufferType = GraphicsBuffer.Target.Structured) where U : struct
        {
            return AsComputeBuffer<U>(m_Count, bufferType);
        }

        // Reinterprets the data in the list as a compute buffer of the given length. Returns null if the list is empty.
        public GraphicsBuffer AsComputeBuffer<U>(int arrayLength, GraphicsBuffer.Target bufferType = GraphicsBuffer.Target.Structured) where U : struct
        {
            DisposeOfComputeBuffer();

            if (arrayLength > 0)
            {
                m_ComputeBufferType = bufferType;
                m_ComputeBuffer = new GraphicsBuffer(bufferType, arrayLength, UnsafeUtility.SizeOf<U>());
                m_ComputeBuffer.SetData(AsNativeArray<U>(arrayLength));

                if (bufferType == GraphicsBuffer.Target.Counter)
                {
                    // initialize count to zero, since counter buffers always start empty:
                    m_Count = 0;
                    m_ComputeBuffer.SetCounterValue((uint)m_Count);
                    m_CountBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, 4);
                    GraphicsBuffer.CopyCount(m_ComputeBuffer, m_CountBuffer, 0);
                }

                return m_ComputeBuffer;
            }
            return null;
        }

        // Kicks a GPU readback request, to bring compute buffer data to this list.
        public void Readback<U>(int readcount, bool async) where U : struct
        {
            if (m_ComputeBuffer != null && m_ComputeBuffer.IsValid() && noReadbackInFlight)
            {
                // On counter buffers, we shouldn't read data up to m_Count and then update m_Count with the compute buffer's counter value *afterwards*.
                // This would lead to reading back less data than we should, so we need to request the entire compute buffer.
                var nativeArray = AsNativeArray<U>(readcount);

                // When using SafeAsComputeBuffer, we'll get a compute buffer of size 1 even if the list (and the NativeArray) is empty.
                // Guard against trying to readback into a smaller NativeArray. Also guard against requesting zero items.
                if (nativeArray.Length >= readcount && readcount > 0)
                    m_AsyncRequest = AsyncGPUReadback.RequestIntoNativeArray(ref nativeArray, m_ComputeBuffer, readcount * UnsafeUtility.SizeOf<U>(), 0);

                // For counter buffers, request the counter value too:
                if (m_ComputeBufferType == GraphicsBuffer.Target.Counter)
                {
                    GraphicsBuffer.CopyCount(m_ComputeBuffer, m_CountBuffer, 0);
                    m_CounterAsyncRequest = AsyncGPUReadback.Request(m_CountBuffer, m_CountBuffer.stride, 0, (AsyncGPUReadbackRequest request)=>
                    {
                        if (!request.hasError)
                            m_Count = Mathf.Min(m_Capacity, request.GetData<int>()[0]);
                    });
                }

                if (!async)
                    WaitForReadback();
            }
        }

        public void Readback(bool async = true)
        {
            if (m_ComputeBuffer != null)
                Readback<T>(m_ComputeBuffer.count, async);
        }

        public void Readback(int readcount ,bool async = true)
        {
            Readback<T>(readcount, async);
        }

        // Makes sure any pending changes by the CPU are sent to the GPU. 
        // If the list data has been changed on the CPU since the last time Unmap() was called and there's a compute buffer associated to it,
        // will write the current contents of the list to the compute buffer.
        public void Upload<U>(int length, bool force = false) where U : struct
        {
            if ((computeBufferDirty || force) && m_ComputeBuffer != null && m_ComputeBuffer.IsValid())
                m_ComputeBuffer.SetData(AsNativeArray<U>(length));

            computeBufferDirty = false;
        }

        public void Upload(bool force = false)
        {
            Upload<T>(m_Count,force);
        }

        public void UploadFullCapacity()
        {
            Upload<T>(m_Capacity, true);
        }

        // Waits for the last readback request to be complete, this brings back data from the GPU to the CPU:
        public void WaitForReadback()
        {
            if (isCreated)
            {
                m_AsyncRequest.WaitForCompletion();
                m_CounterAsyncRequest.WaitForCompletion();
            }
        }

        protected void ChangeCapacity(int newCapacity)
        {
            // invalidate compute buffer:
            DisposeOfComputeBuffer();

            // allocate a new buffer:
            m_Stride = UnsafeUtility.SizeOf<T>();
            var newAlignedPtr = UnsafeUtility.Malloc(newCapacity * m_Stride, m_AlignBytes, Allocator.Persistent);

            // if there was a previous allocation:
            if (isCreated)
            {
                // copy contents from previous memory region
                unsafe
                {
                    UnsafeUtility.MemCpy(newAlignedPtr, m_AlignedPtr, Mathf.Min(newCapacity, m_Capacity) * m_Stride);
                }

                // free previous memory region
                UnsafeUtility.Free(m_AlignedPtr, Allocator.Persistent);
            }

            // get hold of new pointers/capacity.
            m_AlignedPtr = newAlignedPtr;
            m_Capacity = newCapacity;
        }

        public bool Compare(ObiNativeList<T> other)
        {
            if (other == null || !isCreated || !other.isCreated)
                throw new ArgumentNullException();

            if (m_Count != other.m_Count)
                return false;

            return UnsafeUtility.MemCmp(m_AlignedPtr, other.m_AlignedPtr, m_Count * m_Stride) == 0;
        }

        public void CopyFrom(ObiNativeList<T> source)
        {
            if (source == null || !isCreated || !source.isCreated)
                throw new ArgumentNullException();

            if (m_Count < source.m_Count)
                throw new ArgumentOutOfRangeException();

            UnsafeUtility.MemCpy(m_AlignedPtr, source.m_AlignedPtr, source.count * m_Stride);
        }

        public void CopyFrom(ObiNativeList<T> source, int sourceIndex, int destIndex, int length)
        {
            if (source == null || !isCreated || !source.isCreated)
                throw new ArgumentNullException();

            if (length <= 0 || source.m_Count == 0)
                return;

            if (sourceIndex >= source.m_Count || sourceIndex < 0 || destIndex >= m_Count || destIndex < 0 ||
                sourceIndex + length > source.m_Count || destIndex + length > m_Count)
                throw new ArgumentOutOfRangeException();

            void* sourceAddress = source.AddressOfElement(sourceIndex);
            void* destAddress = AddressOfElement(destIndex);
            UnsafeUtility.MemCpy(destAddress, sourceAddress, length * m_Stride);
        }

        public void CopyFrom<U>(NativeArray<U> source, int sourceIndex, int destIndex, int length) where U : struct
        {
            if (!isCreated || !source.IsCreated || UnsafeUtility.SizeOf<U>() != m_Stride)
                throw new ArgumentNullException();

            if (length <= 0 || source.Length == 0)
                return;

            if (sourceIndex >= source.Length || sourceIndex < 0 || destIndex >= m_Count || destIndex < 0 ||
                sourceIndex + length > source.Length || destIndex + length > m_Count)
                throw new ArgumentOutOfRangeException();

            void* sourceAddress = (byte*)source.GetUnsafePtr() + sourceIndex * m_Stride;
            void* destAddress = AddressOfElement(destIndex);
            UnsafeUtility.MemCpy(destAddress, sourceAddress, length * m_Stride);
        }

        public void CopyFrom(T[] source, int sourceIndex, int destIndex, int length)
        {
            if (source == null || !isCreated)
                throw new ArgumentNullException();

            if (length <= 0 || source.Length == 0)
                return;

            if (sourceIndex < 0 || destIndex < 0 ||
                sourceIndex + length > source.Length || destIndex + length > m_Count)
                throw new ArgumentOutOfRangeException();

            // pin the managed array and get its address:
            ulong sourceHandle;
            void* sourceAddress = UnsafeUtility.PinGCArrayAndGetDataAddress(source, out sourceHandle);
            void* destAddress = UnsafeUtility.AddressOf(ref UnsafeUtility.ArrayElementAsRef<T>(m_AlignedPtr, destIndex));
            UnsafeUtility.MemCpy(destAddress, sourceAddress, length * m_Stride);

            // unpin the managed array:
            UnsafeUtility.ReleaseGCObject(sourceHandle);
        }

        public void CopyReplicate(T value, int destIndex, int length)
        {
            if (length <= 0) return;

            if (!isCreated)
                throw new ArgumentNullException();

            if (destIndex >= m_Count || destIndex < 0 || destIndex + length > m_Count)
                throw new ArgumentOutOfRangeException();

            void* sourceAddress = UnsafeUtility.AddressOf(ref value);
            void* destAddress = AddressOfElement(destIndex);
            UnsafeUtility.MemCpyReplicate(destAddress, sourceAddress, m_Stride, length);
        }

        public void CopyTo(T[] dest, int sourceIndex, int length)
        {
            if (length <= 0) return;

            if (dest == null || !isCreated)
                throw new ArgumentNullException();

            if (sourceIndex < 0 || sourceIndex >= m_Count || sourceIndex + length > m_Count || length > dest.Length)
                throw new ArgumentOutOfRangeException();

            ulong destHandle;
            void* sourceAddress = AddressOfElement(sourceIndex);
            void* destAddress = UnsafeUtility.PinGCArrayAndGetDataAddress(dest, out destHandle);
            UnsafeUtility.MemCpy(destAddress, sourceAddress, length * m_Stride);

            UnsafeUtility.ReleaseGCObject(destHandle);
        }

        public void Clear()
        {
            m_Count = 0;
        }

        public void Add(T item)
        {
            EnsureCapacity(m_Count + 1);
            computeBufferDirty = true;
            this[m_Count++] = item;
        }

        public void AddReplicate(T value, int times)
        {
            int appendAt = m_Count;
            ResizeUninitialized(m_Count + times);
            CopyReplicate(value, appendAt, times);
        }

        public void AddRange(T[] array)
        {
            AddRange(array, array.Length);
        }

        public void AddRange(T[] array, int length)
        {
            AddRange(array, 0, length);
        }

        public void AddRange(T[] array, int start, int length)
        {
            int appendAt = m_Count;
            ResizeUninitialized(m_Count + length);
            CopyFrom(array, start, appendAt, length);
        }

        public void AddRange(ObiNativeList<T> array, int length)
        {
            int appendAt = m_Count;
            ResizeUninitialized(m_Count + length);
            CopyFrom(array, 0, appendAt, length);
        }

        public void AddRange(ObiNativeList<T> array, int start, int length)
        {
            int appendAt = m_Count;
            ResizeUninitialized(m_Count + length);
            CopyFrom(array, start, appendAt, length);
        }

        public void AddRange(ObiNativeList<T> array)
        {
            AddRange(array, array.count);
        }

        public void AddRange(IEnumerable<T> enumerable)
        {
            ICollection<T> collection = enumerable as ICollection<T>;
            if (collection != null && collection.Count > 0)
            {
                EnsureCapacity(m_Count + collection.Count);
            }

            using (IEnumerator<T> enumerator = enumerable.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    Add(enumerator.Current);
                }
            }
        }

        public void RemoveRange(int index, int count)
        {
            if (index < 0 || count < 0 || index + count > m_Count)
                throw new ArgumentOutOfRangeException();

            for (int i = index; i < m_Count - count; ++i)
                this[i] = this[i + count];

            m_Count -= count;
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= count)
                throw new ArgumentOutOfRangeException();

            for (int i = index; i < m_Count - 1; ++i)
                this[i] = this[i + 1];

            m_Count--;
        }

        /**
         * Ensures a minimal capacity of count elements, then sets the new count. Useful when passing the backing array to C++
         * for being filled with new data.
         */
        public bool ResizeUninitialized(int newCount)
        {
            newCount = Mathf.Max(0, newCount);
            bool realloc = EnsureCapacity(newCount);

            m_Count = newCount;

            return realloc;
        }

        public bool ResizeInitialized(int newCount, T value = default(T))
        {
            newCount = Mathf.Max(0, newCount);

            bool initialize = newCount >= m_Capacity || !isCreated;
            bool realloc = EnsureCapacity(newCount);

            if (initialize)
            {
                void* sourceAddress = UnsafeUtility.AddressOf(ref value);
                void* destAddress = AddressOfElement(m_Count);
                UnsafeUtility.MemCpyReplicate(destAddress, sourceAddress, m_Stride, m_Capacity - m_Count);
            }

            m_Count = newCount;

            return realloc;
        }

        public bool EnsureCapacity(int min)
        {
            if (min >= m_Capacity || !isCreated)
            {
                ChangeCapacity(min * 2);
                return true;
            }
            return false;
        }

        public void WipeToZero()
        {
            unsafe
            {
                if (isCreated)
                {
                    UnsafeUtility.MemClear(m_AlignedPtr, count * m_Stride);

                    computeBufferDirty = true;
                }
            }
        }

        public void WipeToValue(T value)
        {
            unsafe
            {
                if (isCreated)
                {
                    void* sourceAddress = UnsafeUtility.AddressOf(ref value);
                    UnsafeUtility.MemCpyReplicate(m_AlignedPtr, sourceAddress, m_Stride, count);

                    computeBufferDirty = true;
                }
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append('[');

            for (int t = 0; t < m_Count; t++)
            {
                sb.Append(this[t].ToString());

                if (t < (m_Count - 1)) sb.Append(',');

            }
            sb.Append(']');
            return sb.ToString();
        }

        public void* AddressOfElement(int index)
        {
            return (void*) ((byte*)m_AlignedPtr + m_Stride * index);
        }

        public IntPtr GetIntPtr()
        {
            if (isCreated)
                return new IntPtr(m_AlignedPtr);
            return IntPtr.Zero;
        }

        public void Swap(int index1, int index2)
        {
            // check to avoid out of bounds access:
            if (index1 >= 0 && index1 < count && index2 >= 0 && index2 < count)
            {
                var aux = this[index1];
                this[index1] = this[index2];
                this[index2] = aux;
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < count; ++i)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}

