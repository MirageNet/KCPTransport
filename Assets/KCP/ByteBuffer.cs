using System;
using System.Collections.Generic;
using System.Text;

namespace KCPTransport
{
    class ByteBuffer : ICloneable
    {
        int readIndex;
        int writeIndex;
        int markReadIndex;
        int markWirteIndex;

        static List<ByteBuffer> pool = new List<ByteBuffer>();
        static int poolMaxCount = 200;

        bool isPool;

        ByteBuffer(int capacity)
        {
            RawBuffer = new byte[capacity];
            this.Capacity = capacity;
            readIndex = 0;
            writeIndex = 0;
        }

        ByteBuffer(byte[] bytes)
        {
            RawBuffer = new byte[bytes.Length];
            Array.Copy(bytes, 0, RawBuffer, 0, RawBuffer.Length);
            Capacity = RawBuffer.Length;
            readIndex = 0;
            writeIndex = bytes.Length + 1;
        }

        /// <summary>
        /// Construct a capacity length byte buffer ByteBuffer object
        /// </summary>
        /// <param name="capacity">Initial capacity</param>
        /// <param name="fromPool">
        /// true means to obtain a pooled ByteBuffer object, the pooled object must be pushed into the pool after calling Dispose, this method is thread-safe.
        /// When true, the actual capacity value of the object obtained from the pool
        /// </param>
        /// <returns>ByteBuffer object</returns>
        public static ByteBuffer Allocate(int capacity, bool fromPool = false)
        {
            if (!fromPool)
            {
                return new ByteBuffer(capacity);
            }
            lock (pool)
            {
                ByteBuffer bbuf;
                if (pool.Count == 0)
                {
                    bbuf = new ByteBuffer(capacity)
                    {
                        isPool = true
                    };
                    return bbuf;
                }
                int lastIndex = pool.Count - 1;
                bbuf = pool[lastIndex];
                pool.RemoveAt(lastIndex);
                if (!bbuf.isPool)
                {
                    bbuf.isPool = true;
                }
                return bbuf;
            }
        }

        /// <summary>
        /// Construct a ByteBuffer object with bytes as the byte buffer, generally not recommended
        /// </summary>
        /// <param name="bytes">Initial byte array</param>
        /// <param name="fromPool">
        /// true means to obtain a pooled ByteBuffer object, the pooled object must be pushed into the pool after calling Dispose, this method is thread-safe.
        /// </param>
        /// <returns>ByteBuffer object</returns>
        public static ByteBuffer Allocate(byte[] bytes, bool fromPool = false)
        {
            if (!fromPool)
            {
                return new ByteBuffer(bytes);
            }
            lock (pool)
            {
                ByteBuffer bbuf;
                if (pool.Count == 0)
                {
                    bbuf = new ByteBuffer(bytes)
                    {
                        isPool = true
                    };
                    return bbuf;
                }
                int lastIndex = pool.Count - 1;
                bbuf = pool[lastIndex];
                bbuf.WriteBytes(bytes);
                pool.RemoveAt(lastIndex);
                if (!bbuf.isPool)
                {
                    bbuf.isPool = true;
                }
                return bbuf;
            }
        }

        /// <summary>
        /// According to the value, determine the nearest 2nd power greater than this length, such as length=7, the return value is 8; length=12, then 16
        /// </summary>
        /// <param name="value">Reference capacity</param>
        /// <returns>The nearest second power greater than the reference capacity</returns>
        int FixLength(int value)
        {
            if (value == 0)
            {
                return 1;
            }
            value--;
            value |= value >> 1;
            value |= value >> 2;
            value |= value >> 4;
            value |= value >> 8;
            value |= value >> 16;
            return value + 1;
            //int n = 2;
            //int b = 2;
            //while (b < length)
            //{
            //    b = 2 << n;
            //    n++;
            //}
            //return b;
        }

        /// <summary>
        /// Flip the byte array, if the local byte sequence is a high byte sequence, flip to convert to a low byte sequence
        /// </summary>
        /// <param name="bytes">Byte array to be converted to high-endian</param>
        /// <returns>Byte array of low byte sequence</returns>
        byte[] Flip(byte[] bytes)
        {
            //if (BitConverter.IsLittleEndian)
            //{
            //    Array.Reverse(bytes);
            //}
            return bytes;
        }

        /// <summary>
        /// Determine the size of the internal byte buffer array
        /// </summary>
        /// <param name="currLen">Current capacity</param>
        /// <param name="futureLen">Future capacity</param>
        /// <returns>The maximum capacity of the current buffer</returns>
        int FixSizeAndReset(int currLen, int futureLen)
        {
            if (futureLen > currLen)
            {
                //Determine the size of the internal byte buffer with twice the original size to the power of 2
                int size = FixLength(currLen) * 2;
                if (futureLen > size)
                {
                    //Determine the size of the internal byte buffer by twice the power of the future size
                    size = FixLength(futureLen) * 2;
                }
                byte[] newbuf = new byte[size];
                Array.Copy(RawBuffer, 0, newbuf, 0, currLen);
                RawBuffer = newbuf;
                Capacity = size;
            }
            return futureLen;
        }

        /// <summary>
        /// Make sure there are so many bytes available for writing
        /// </summary>
        /// <param name="minBytes"></param>
        public void EnsureWritableBytes(int minBytes)
        {
            // If there is not enough space for writing
            if (WritableBytes < minBytes)
            {

                // Prioritize space
                if (ReaderIndex >= minBytes)
                {
                    // Organize the available space
                    TrimReadedBytes();
                }
                else
                {
                    // When space is insufficient, reallocate memory
                    FixSizeAndReset(RawBuffer.Length, RawBuffer.Length + minBytes);
                }
            }
        }

        public void TrimReadedBytes()
        {
            Buffer.BlockCopy(RawBuffer, readIndex, RawBuffer, 0, writeIndex - readIndex);
            writeIndex -= readIndex;
            readIndex = 0;
        }

        /// <summary>
        /// Write length bytes starting from startIndex of bytes byte array to this buffer
        /// </summary>
        /// <param name="bytes">Byte data to be written</param>
        /// <param name="startIndex">Start position of writing</param>
        /// <param name="length">Length written</param>
        public void WriteBytes(byte[] bytes, int startIndex, int length)
        {
            if (length <= 0 || startIndex < 0) return;

            int total = length + writeIndex;
            int len = RawBuffer.Length;
            FixSizeAndReset(len, total);
            Array.Copy(bytes, startIndex, RawBuffer, writeIndex, length);
            writeIndex = total;
        }

        /// <summary>
        /// Write the elements from 0 to length in the byte array to the buffer
        /// </summary>
        /// <param name="bytes">Byte data to be written</param>
        /// <param name="length">Length written</param>
        public void WriteBytes(byte[] bytes, int length)
        {
            WriteBytes(bytes, 0, length);
        }

        /// <summary>
        /// Write all byte arrays into the buffer
        /// </summary>
        /// <param name="bytes">Byte data to be written</param>
        public void WriteBytes(byte[] bytes)
        {
            WriteBytes(bytes, bytes.Length);
        }

        /// <summary>
        /// Write the effective byte area of ​​a ByteBuffer into this buffer area
        /// </summary>
        /// <param name="buffer">Byte buffer area to be written</param>
        public void Write(ByteBuffer buffer)
        {
            if (buffer == null) return;
            if (buffer.ReadableBytes <= 0) return;
            WriteBytes(buffer.ToArray());
        }

        public void WriteShort(short value)
        {
            WriteBytes(Flip(BitConverter.GetBytes(value)));
        }

        public void WriteUshort(ushort value)
        {
            WriteBytes(Flip(BitConverter.GetBytes(value)));
        }

        public void WriteInt(int value)
        {
            //byte[] array = new byte[4];
            //for (int i = 3; i >= 0; i--)
            //{
            //    array[i] = (byte)(value & 0xff);
            //    value = value >> 8;
            //}
            //Array.Reverse(array);
            //Write(array);
            WriteBytes(Flip(BitConverter.GetBytes(value)));
        }

        public void WriteUint(uint value)
        {
            WriteBytes(Flip(BitConverter.GetBytes(value)));
        }

        public void WriteLong(long value)
        {
            WriteBytes(Flip(BitConverter.GetBytes(value)));
        }

        public void WriteUlong(ulong value)
        {
            WriteBytes(Flip(BitConverter.GetBytes(value)));
        }

        public void WriteFloat(float value)
        {
            WriteBytes(Flip(BitConverter.GetBytes(value)));
        }

        public void WriteByte(byte value)
        {
            int afterLen = writeIndex + 1;
            int len = RawBuffer.Length;
            FixSizeAndReset(len, afterLen);
            RawBuffer[writeIndex] = value;
            writeIndex = afterLen;
        }

        public void WriteByte(int value)
        {
            byte b = (byte)value;
            WriteByte(b);
        }

        public void WriteDouble(double value)
        {
            WriteBytes(Flip(BitConverter.GetBytes(value)));
        }

        public void WriteChar(char value)
        {
            WriteBytes(Flip(BitConverter.GetBytes(value)));
        }

        public void WriteBoolean(bool value)
        {
            WriteBytes(Flip(BitConverter.GetBytes(value)));
        }

        public byte ReadByte()
        {
            byte b = RawBuffer[readIndex];
            readIndex++;
            return b;
        }

        /// <summary>
        /// Get the length of len bytes from the index
        /// </summary>
        /// <param name="index"></param>
        /// <param name="len"></param>
        /// <returns></returns>
        byte[] Get(int index, int len)
        {
            byte[] bytes = new byte[len];
            Array.Copy(RawBuffer, index, bytes, 0, len);
            return Flip(bytes);
        }

        /// <summary>
        /// Read the byte array of length len from the reading index position
        /// </summary>
        /// <param name="len">Length of bytes to be read</param>
        /// <returns>Byte array</returns>
        byte[] Read(int len)
        {
            byte[] bytes = Get(readIndex, len);
            readIndex += len;
            return bytes;
        }

        public ushort ReadUshort()
        {
            return BitConverter.ToUInt16(Read(2), 0);
        }

        public short ReadShort()
        {
            return BitConverter.ToInt16(Read(2), 0);
        }

        public uint ReadUint()
        {
            return BitConverter.ToUInt32(Read(4), 0);
        }

        public int ReadInt()
        {
            return BitConverter.ToInt32(Read(4), 0);
        }

        public ulong ReadUlong()
        {
            return BitConverter.ToUInt64(Read(8), 0);
        }

        public long ReadLong()
        {
            return BitConverter.ToInt64(Read(8), 0);
        }

        public float ReadFloat()
        {
            return BitConverter.ToSingle(Read(4), 0);
        }

        public double ReadDouble()
        {
            return BitConverter.ToDouble(Read(8), 0);
        }

        public char ReadChar()
        {
            return BitConverter.ToChar(Read(2), 0);
        }

        public bool ReadBoolean()
        {
            return BitConverter.ToBoolean(Read(1), 0);
        }

        /// <summary>
        /// Read bytes of length len from the reading index position into the target byte array of disbytes
        /// </summary>
        /// <param name="disbytes">The bytes read will be stored in this byte array</param>
        /// <param name="disstart">The write index of the target byte array</param>
        /// <param name="len">Read length</param>
        public void ReadBytes(byte[] disbytes, int disstart, int len)
        {
            int size = disstart + len;
            for (int i = disstart; i < size; i++)
            {
                disbytes[i] = ReadByte();
            }
        }

        public byte[] ReadBytes(int len)
        {
            return ReadBytes(readIndex, len);
        }

        public byte[] ReadBytes(int index, int len)
        {
            if (ReadableBytes < len)
                throw new Exception("no more readable bytes");

            var buffer = new byte[len];
            Array.Copy(RawBuffer, index, buffer, 0, len);
            readIndex += len;
            return buffer;
        }

        public byte GetByte(int index)
        {
            return RawBuffer[index];
        }

        public byte GetByte()
        {
            return GetByte(readIndex);
        }

        public double GetDouble(int index)
        {
            return BitConverter.ToDouble(Get(index, 8), 0);
        }

        public double GetDouble()
        {
            return GetDouble(readIndex);
        }

        public float GetFloat(int index)
        {
            return BitConverter.ToSingle(Get(index, 4), 0);
        }

        public float GetFloat()
        {
            return GetFloat(readIndex);
        }

        public long GetLong(int index)
        {
            return BitConverter.ToInt64(Get(index, 8), 0);
        }

        public long GetLong()
        {
            return GetLong(readIndex);
        }

        public ulong GetUlong(int index)
        {
            return BitConverter.ToUInt64(Get(index, 8), 0);
        }

        public ulong GetUlong()
        {
            return GetUlong(readIndex);
        }

        public int GetInt(int index)
        {
            return BitConverter.ToInt32(Get(index, 4), 0);
        }

        public int GetInt()
        {
            return GetInt(readIndex);
        }

        public uint GetUint(int index)
        {
            return BitConverter.ToUInt32(Get(index, 4), 0);
        }

        public uint GetUint()
        {
            return GetUint(readIndex);
        }

        public int GetShort(int index)
        {
            return BitConverter.ToInt16(Get(index, 2), 0);
        }

        public int GetShort()
        {
            return GetShort(readIndex);
        }

        public int GetUshort(int index)
        {
            return BitConverter.ToUInt16(Get(index, 2), 0);
        }

        public int GetUshort()
        {
            return GetUshort(readIndex);
        }

        public char GetChar(int index)
        {
            return BitConverter.ToChar(Get(index, 2), 0);
        }

        public char GetChar()
        {
            return GetChar(readIndex);
        }

        public bool GetBoolean(int index)
        {
            return BitConverter.ToBoolean(Get(index, 1), 0);
        }

        public bool GetBoolean()
        {
            return GetBoolean(readIndex);
        }

        /// <summary>
        /// Clear the read bytes and rebuild the buffer area
        /// </summary>
        public void DiscardReadBytes()
        {
            if (readIndex <= 0) return;
            int len = RawBuffer.Length - readIndex;
            byte[] newbuf = new byte[len];
            Array.Copy(RawBuffer, readIndex, newbuf, 0, len);
            RawBuffer = newbuf;
            writeIndex -= readIndex;
            markReadIndex -= readIndex;
            if (markReadIndex < 0)
            {
                //markReadIndex = readIndex;
                markReadIndex = 0;
            }
            markWirteIndex -= readIndex;
            if (markWirteIndex < 0 || markWirteIndex < readIndex || markWirteIndex < markReadIndex)
            {
                markWirteIndex = writeIndex;
            }
            readIndex = 0;
        }

        public int ReaderIndex
        {
            get
            {
                return readIndex;
            }
            set
            {
                if (value < 0) return;
                readIndex = value;
            }
        }

        public int WriterIndex
        {
            get
            {
                return writeIndex;
            }
            set
            {
                if (value < 0) return;
                writeIndex = value;
            }
        }

        public void MarkReaderIndex()
        {
            markReadIndex = readIndex;
        }

        public void MarkWriterIndex()
        {
            markWirteIndex = writeIndex;
        }

        public void ResetReaderIndex()
        {
            readIndex = markReadIndex;
        }

        public void ResetWriterIndex()
        {
            writeIndex = markWirteIndex;
        }

        public int ReadableBytes
        {
            get
            {
                return writeIndex - readIndex;
            }
        }

        public int WritableBytes
        {
            get
            {
                return Capacity - writeIndex;
            }
        }

        public int Capacity { get; private set; }

        public byte[] RawBuffer { get; private set; }

        public byte[] ToArray()
        {
            byte[] bytes = new byte[writeIndex - readIndex];
            Array.Copy(RawBuffer, readIndex, bytes, 0, bytes.Length);
            return bytes;
        }

        public enum DataType
        {
            BYTE = 1,
            SHORT = 2,
            INT = 3,
            LONG = 4
        }

        void WriteValue(int value, DataType type)
        {
            switch (type)
            {
                case DataType.BYTE:
                    WriteByte(value);
                    break;
                case DataType.SHORT:
                    WriteShort((short)value);
                    break;
                case DataType.LONG:
                    WriteLong(value);
                    break;
                default:
                    WriteInt(value);
                    break;
            }
        }

        int ReadValue(DataType type)
        {
            switch (type)
            {
                case DataType.BYTE:
                    return ReadByte();
                case DataType.SHORT:
                    return ReadShort();
                case DataType.INT:
                    return ReadInt();
                case DataType.LONG:
                    return (int)ReadLong();
                default:
                    return -1;
            }
        }

        /// <summary>
        /// 写入可变长的UTF-8字符串
        /// <para>以长度类型（byte:1, short:2, int:3) + 长度（根据长度类型写入到字节缓冲区） + 字节数组表示一个字符串</para>
        /// </summary>
        /// <param name="content"></param>
        //public void WriteUTF8VarString(string content)
        //{
        //    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(content);
        //    ValueType lenType;
        //    if (bytes.Length <= byte.MaxValue)
        //    {
        //        lenType = ValueType.BYTE;
        //    }
        //    else if (bytes.Length <= short.MaxValue)
        //    {
        //        lenType = ValueType.SHORT;
        //    }
        //    else
        //    {
        //        lenType = ValueType.INT;
        //    }
        //    WriteByte((int)lenType);
        //    if (lenType == ValueType.BYTE)
        //    {
        //        WriteByte(bytes.Length);
        //    }
        //    else if (lenType == ValueType.SHORT)
        //    {
        //        WriteShort((short)bytes.Length);
        //    }
        //    else
        //    {
        //        WriteInt(bytes.Length);
        //    }
        //    WriteBytes(bytes);
        //}

        /// <summary>
        /// 读取可变长的UTF-8字符串
        /// <para>以长度类型（byte:1, short:2, int:3) + 长度（根据长度类型从字节缓冲区读取） + 字节数组表示一个字符串</para>
        /// </summary>
        /// <returns></returns>
        //public string ReadUTF8VarString()
        //{
        //    int lenTypeValue = ReadByte();
        //    int len = 0;
        //    if (lenTypeValue == (int)ValueType.BYTE)
        //    {
        //        len = ReadByte();
        //    }
        //    else if (lenTypeValue == (int)ValueType.SHORT)
        //    {
        //        len = ReadShort();
        //    }
        //    else if (lenTypeValue == (int)ValueType.INT)
        //    {
        //        len = ReadInt();
        //    }
        //    if (len > 0)
        //    {
        //        byte[] bytes = new byte[len];
        //        ReadBytes(bytes, 0, len);
        //        return System.Text.Encoding.UTF8.GetString(bytes);
        //    }
        //    return "";
        //}

        /// <summary>
        /// Write a UTF-8 string, UTF-8 string has no byte order problem
        /// <para>The structure of the write buffer is string byte length (the type is specified by lenType) + string byte array</para>
        /// </summary>
        /// <param name="content">String to be written</param>
        /// <param name="lenType">Type of string length written</param>
        public void WriteUTF8String(string content, DataType lenType)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(content);
            int max;
            if (lenType == DataType.BYTE)
            {
                WriteByte(bytes.Length);
                max = byte.MaxValue;
            }
            else if (lenType == DataType.SHORT)
            {
                WriteShort((short)bytes.Length);
                max = short.MaxValue;
            }
            else
            {
                WriteInt(bytes.Length);
                max = int.MaxValue;
            }
            if (bytes.Length > max)
            {
                WriteBytes(bytes, 0, max);
            }
            else
            {
                WriteBytes(bytes, 0, bytes.Length);
            }
        }

        public void WriteUTF(string content)
        {
            WriteUTF8String(content, DataType.SHORT);
        }

        public string ReadUTF8String(int len)
        {
            byte[] bytes = new byte[len];
            ReadBytes(bytes, 0, len);
            return Encoding.UTF8.GetString(bytes);
        }

        public string ReadUTF8String(DataType lenType)
        {
            int len = ReadValue(lenType);
            return ReadUTF8String(len);
        }

        public string ReadUTF()
        {
            return ReadUTF8String(DataType.SHORT);
        }

        /// <summary>
        /// Copy an object with the same data as the original object without changing the data of the original object, excluding the read data
        /// </summary>
        /// <returns></returns>
        public ByteBuffer Copy()
        {
            if (RawBuffer == null)
            {
                return new ByteBuffer(16);
            }
            if (readIndex < writeIndex)
            {
                byte[] newbytes = new byte[writeIndex - readIndex];
                Array.Copy(RawBuffer, readIndex, newbytes, 0, newbytes.Length);
                ByteBuffer buffer = new ByteBuffer(newbytes.Length);
                buffer.WriteBytes(newbytes);
                buffer.isPool = isPool;
                return buffer;
            }
            return new ByteBuffer(16);
        }

        /// <summary>
        /// Deep copy, with the same data as the original object,
        /// without changing the data of the original object, including the read data
        /// </summary>
        /// <returns></returns>
        public object Clone()
        {
            if (RawBuffer == null)
            {
                return new ByteBuffer(16);
            }
            ByteBuffer newBuf = new ByteBuffer(RawBuffer)
            {
                Capacity = Capacity,
                readIndex = readIndex,
                writeIndex = writeIndex,
                markReadIndex = markReadIndex,
                markWirteIndex = markWirteIndex,
                isPool = isPool
            };
            return newBuf;
        }

        public void ForEach(Action<byte> action)
        {
            for (int i = 0; i < ReadableBytes; i++)
            {
                action.Invoke(RawBuffer[i]);
            }
        }

        public void Clear()
        {
            readIndex = 0;
            writeIndex = 0;
            markReadIndex = 0;
            markWirteIndex = 0;
            Capacity = RawBuffer.Length;
        }

        public void Dispose()
        {
            if (isPool)
            {
                lock (pool)
                {
                    if (pool.Count < poolMaxCount)
                    {
                        Clear();
                        pool.Add(this);
                        return;
                    }
                }
            }

            readIndex = 0;
            writeIndex = 0;
            markReadIndex = 0;
            markWirteIndex = 0;
            Capacity = 0;
            RawBuffer = null;
        }
    }
}
