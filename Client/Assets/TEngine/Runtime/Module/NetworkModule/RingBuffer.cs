using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using UnityEngine;

namespace TEngine
{
    /// <summary>
    /// 字节序枚举
    /// </summary>
    public enum ByteOrder
    {
        LittleEndian,  // 小端（系统默认）
        BigEndian      // 大端（网络字节序）
    }

    /// <summary>
    /// 环形缓冲区（用于高效处理流式数据的循环存储结构）。
    /// </summary>
    public class RingBuffer
    {
        private readonly byte[] _buffer;          // 底层字节数组存储
        private int _readerIndex = 0;             // 当前读取位置指针
        private int _writerIndex = 0;             // 当前写入位置指针
        private int _markedReaderIndex = 0;       // 标记的读取位置（用于回滚）
        private int _markedWriterIndex = 0;       // 标记的写入位置（用于回滚）

        /// <summary>
        /// 默认字节序（可设置全局默认值）
        /// </summary>
        public ByteOrder DefaultByteOrder { get; set; } = ByteOrder.LittleEndian;

        /// <summary>
        /// 初始化指定容量的环形缓冲区。
        /// </summary>
        /// <param name="capacity">缓冲区总容量。</param>
        public RingBuffer(int capacity)
        {
            _buffer = new byte[capacity];
        }

        /// <summary>
        /// 使用现有字节数组初始化环形缓冲区。
        /// </summary>
        /// <param name="data">初始数据（写入指针将置于数组末尾）。</param>
        public RingBuffer(byte[] data)
        {
            _buffer = data;
            _writerIndex = data.Length;
        }

        /// <summary>
        /// 获取底层字节数组（直接访问原始存储）。
        /// </summary>
        public byte[] GetBuffer()
        {
            return _buffer;
        }

        /// <summary>
        /// 缓冲区总容量（固定值）。
        /// </summary>
        public int Capacity => _buffer.Length;

        /// <summary>
        /// 清空缓冲区（重置读写指针和标记指针）。
        /// </summary>
        public void Clear()
        {
            _readerIndex = 0;
            _writerIndex = 0;
            _markedReaderIndex = 0;
            _markedWriterIndex = 0;
        }

        /// <summary>
        /// 压缩缓冲区：将未读取的数据移动到头部并重置指针（提升写入空间利用率）。
        /// </summary>
        public void DiscardReadBytes()
        {
            if (_readerIndex == 0)
            {
                return;
            }
            if (_readerIndex == _writerIndex)
            {
                _readerIndex = 0;
                _writerIndex = 0;
            }
            else
            {
                // 数据搬移操作
                for (int i = 0, j = _readerIndex, length = _writerIndex - _readerIndex; i < length; i++, j++)
                {
                    _buffer[i] = _buffer[j];
                }

                _writerIndex -= _readerIndex;
                _readerIndex = 0;
            }
        }

        #region 读取相关

        /// <summary>
        /// 当前读取位置指针。
        /// </summary>
        public int ReaderIndex => _readerIndex;

        /// <summary>
        /// 可读取的字节总数（写入指针-读取指针）。
        /// </summary>
        public int ReadableBytes => _writerIndex - _readerIndex;

        /// <summary>
        /// 检查是否可读取指定长度的数据。
        /// </summary>
        /// <param name="size">需要读取的字节数。</param>
        public bool IsReadable(int size = 1)
        {
            return ReadableBytes >= size;
        }

        /// <summary>
        /// 标记当前读取位置（用于后续回滚操作）。
        /// </summary>
        public void MarkReaderIndex()
        {
            _markedReaderIndex = _readerIndex;
        }

        /// <summary>
        /// 将读取指针重置到最后一次标记的位置。
        /// </summary>
        public void ResetReaderIndex()
        {
            _readerIndex = _markedReaderIndex;
        }

        #endregion

        #region 写入相关

        /// <summary>
        /// 当前写入位置指针。
        /// </summary>
        public int WriterIndex => _writerIndex;

        /// <summary>
        /// 剩余可写入空间（总容量-写入指针）。
        /// </summary>
        public int WriteableBytes => Capacity - _writerIndex;

        /// <summary>
        /// 检查是否可写入指定长度的数据。
        /// </summary>
        /// <param name="size">需要写入的字节数。</param>
        public bool IsWriteable(int size = 1)
        {
            return WriteableBytes >= size;
        }

        /// <summary>
        /// 标记当前写入位置（用于后续回滚操作）。
        /// </summary>
        public void MarkWriterIndex()
        {
            _markedWriterIndex = _writerIndex;
        }

        /// <summary>
        /// 将写入指针重置到最后一次标记的位置。
        /// </summary>
        public void ResetWriterIndex()
        {
            _writerIndex = _markedWriterIndex;
        }

        #endregion

        #region 字节序辅助方法

        /// <summary>
        /// 判断是否需要字节序转换
        /// </summary>
        /// <param name="targetOrder">目标字节序</param>
        /// <returns></returns>
        private bool NeedReverseBytes(ByteOrder targetOrder)
        {
            bool isLittleEndian = BitConverter.IsLittleEndian;
            return (isLittleEndian && targetOrder == ByteOrder.BigEndian) ||
                   (!isLittleEndian && targetOrder == ByteOrder.LittleEndian);
        }

        /// <summary>
        /// 转换字节序（原地修改）
        /// </summary>
        /// <param name="bytes">字节数组</param>
        /// <param name="targetOrder">目标字节序</param>
        private void ConvertByteOrder(byte[] bytes, ByteOrder targetOrder)
        {
            if (NeedReverseBytes(targetOrder))
            {
                ReverseOrder(bytes);
            }
        }

        #endregion

        #region 读取操作

        [Conditional("DEBUG")]
        private void CheckReaderIndex(int length)
        {
            if (_readerIndex + length > _writerIndex)
            {
                throw new IndexOutOfRangeException();
            }
        }

        public byte[] ReadBytes(int count)
        {
            CheckReaderIndex(count);
            var data = new byte[count];
            Buffer.BlockCopy(_buffer, _readerIndex, data, 0, count);
            _readerIndex += count;
            return data;
        }

        public bool ReadBool()
        {
            CheckReaderIndex(1);
            return _buffer[_readerIndex++] == 1;
        }

        public byte ReadByte()
        {
            CheckReaderIndex(1);
            return _buffer[_readerIndex++];
        }

        public sbyte ReadSbyte()
        {
            return (sbyte)ReadByte();
        }

        // 原有方法（使用默认字节序）
        public short ReadShort() => ReadShort(DefaultByteOrder);
        public ushort ReadUShort() => ReadUShort(DefaultByteOrder);
        public int ReadInt() => ReadInt(DefaultByteOrder);
        public uint ReadUInt() => ReadUInt(DefaultByteOrder);
        public long ReadLong() => ReadLong(DefaultByteOrder);
        public ulong ReadULong() => ReadULong(DefaultByteOrder);
        public float ReadFloat() => ReadFloat(DefaultByteOrder);
        public double ReadDouble() => ReadDouble(DefaultByteOrder);

        // 支持指定字节序的新方法
        public short ReadShort(ByteOrder byteOrder)
        {
            CheckReaderIndex(2);
            if (byteOrder == ByteOrder.BigEndian && BitConverter.IsLittleEndian)
            {
                // 大端读取
                short result = (short)((_buffer[_readerIndex] << 8) | _buffer[_readerIndex + 1]);
                _readerIndex += 2;
                return result;
            }
            else if (byteOrder == ByteOrder.LittleEndian && !BitConverter.IsLittleEndian)
            {
                // 小端读取（在大端系统上）
                short result = (short)((_buffer[_readerIndex + 1] << 8) | _buffer[_readerIndex]);
                _readerIndex += 2;
                return result;
            }
            else
            {
                // 系统默认
                short result = BitConverter.ToInt16(_buffer, _readerIndex);
                _readerIndex += 2;
                return result;
            }
        }

        public ushort ReadUShort(ByteOrder byteOrder)
        {
            CheckReaderIndex(2);
            if (byteOrder == ByteOrder.BigEndian && BitConverter.IsLittleEndian)
            {
                // 大端读取
                ushort result = (ushort)((_buffer[_readerIndex] << 8) | _buffer[_readerIndex + 1]);
                _readerIndex += 2;
                return result;
            }
            else if (byteOrder == ByteOrder.LittleEndian && !BitConverter.IsLittleEndian)
            {
                // 小端读取（在大端系统上）
                ushort result = (ushort)((_buffer[_readerIndex + 1] << 8) | _buffer[_readerIndex]);
                _readerIndex += 2;
                return result;
            }
            else
            {
                // 系统默认
                ushort result = BitConverter.ToUInt16(_buffer, _readerIndex);
                _readerIndex += 2;
                return result;
            }
        }

        public int ReadInt(ByteOrder byteOrder)
        {
            CheckReaderIndex(4);
            if (byteOrder == ByteOrder.BigEndian && BitConverter.IsLittleEndian)
            {
                // 大端读取
                int result = (_buffer[_readerIndex] << 24) | (_buffer[_readerIndex + 1] << 16) |
                           (_buffer[_readerIndex + 2] << 8) | _buffer[_readerIndex + 3];
                _readerIndex += 4;
                return result;
            }
            else if (byteOrder == ByteOrder.LittleEndian && !BitConverter.IsLittleEndian)
            {
                // 小端读取（在大端系统上）
                int result = (_buffer[_readerIndex + 3] << 24) | (_buffer[_readerIndex + 2] << 16) |
                           (_buffer[_readerIndex + 1] << 8) | _buffer[_readerIndex];
                _readerIndex += 4;
                return result;
            }
            else
            {
                // 系统默认
                int result = BitConverter.ToInt32(_buffer, _readerIndex);
                _readerIndex += 4;
                return result;
            }
        }

        public uint ReadUInt(ByteOrder byteOrder)
        {
            return (uint)ReadInt(byteOrder);
        }

        public long ReadLong(ByteOrder byteOrder)
        {
            CheckReaderIndex(8);
            if (NeedReverseBytes(byteOrder))
            {
                // 需要字节序转换
                byte[] temp = new byte[8];
                Buffer.BlockCopy(_buffer, _readerIndex, temp, 0, 8);
                ReverseOrder(temp);
                _readerIndex += 8;
                return BitConverter.ToInt64(temp, 0);
            }
            else
            {
                // 系统默认
                long result = BitConverter.ToInt64(_buffer, _readerIndex);
                _readerIndex += 8;
                return result;
            }
        }

        public ulong ReadULong(ByteOrder byteOrder)
        {
            return (ulong)ReadLong(byteOrder);
        }

        public float ReadFloat(ByteOrder byteOrder)
        {
            CheckReaderIndex(4);
            if (NeedReverseBytes(byteOrder))
            {
                // 需要字节序转换
                byte[] temp = new byte[4];
                Buffer.BlockCopy(_buffer, _readerIndex, temp, 0, 4);
                ReverseOrder(temp);
                _readerIndex += 4;
                return BitConverter.ToSingle(temp, 0);
            }
            else
            {
                // 系统默认
                float result = BitConverter.ToSingle(_buffer, _readerIndex);
                _readerIndex += 4;
                return result;
            }
        }

        public double ReadDouble(ByteOrder byteOrder)
        {
            CheckReaderIndex(8);
            if (NeedReverseBytes(byteOrder))
            {
                // 需要字节序转换
                byte[] temp = new byte[8];
                Buffer.BlockCopy(_buffer, _readerIndex, temp, 0, 8);
                ReverseOrder(temp);
                _readerIndex += 8;
                return BitConverter.ToDouble(temp, 0);
            }
            else
            {
                // 系统默认
                double result = BitConverter.ToDouble(_buffer, _readerIndex);
                _readerIndex += 8;
                return result;
            }
        }

        public string ReadUTF()
        {
            ushort count = ReadUShort();
            CheckReaderIndex(count);
            string result = Encoding.UTF8.GetString(_buffer, _readerIndex, count - 1); // 注意：读取的时候忽略字符串末尾写入结束符
            _readerIndex += count;
            return result;
        }

        public string ReadUTF(ByteOrder byteOrder)
        {
            ushort count = ReadUShort(byteOrder);
            CheckReaderIndex(count);
            string result = Encoding.UTF8.GetString(_buffer, _readerIndex, count - 1);
            _readerIndex += count;
            return result;
        }

        // 其他读取方法保持不变...
        public List<int> ReadListInt()
        {
            List<int> result = new List<int>();
            int count = ReadInt();
            for (int i = 0; i < count; i++)
            {
                result.Add(ReadInt());
            }
            return result;
        }

        public List<int> ReadListInt(ByteOrder byteOrder)
        {
            List<int> result = new List<int>();
            int count = ReadInt(byteOrder);
            for (int i = 0; i < count; i++)
            {
                result.Add(ReadInt(byteOrder));
            }
            return result;
        }

        // 可以为其他List方法也添加字节序支持...

        public Vector2 ReadVector2() => ReadVector2(DefaultByteOrder);
        public Vector2 ReadVector2(ByteOrder byteOrder)
        {
            float x = ReadFloat(byteOrder);
            float y = ReadFloat(byteOrder);
            return new Vector2(x, y);
        }

        public Vector3 ReadVector3() => ReadVector3(DefaultByteOrder);
        public Vector3 ReadVector3(ByteOrder byteOrder)
        {
            float x = ReadFloat(byteOrder);
            float y = ReadFloat(byteOrder);
            float z = ReadFloat(byteOrder);
            return new Vector3(x, y, z);
        }

        public Vector4 ReadVector4() => ReadVector4(DefaultByteOrder);
        public Vector4 ReadVector4(ByteOrder byteOrder)
        {
            float x = ReadFloat(byteOrder);
            float y = ReadFloat(byteOrder);
            float z = ReadFloat(byteOrder);
            float w = ReadFloat(byteOrder);
            return new Vector4(x, y, z, w);
        }

        #endregion

        #region 写入操作

        [Conditional("DEBUG")]
        private void CheckWriterIndex(int length)
        {
            if (_writerIndex + length > Capacity)
            {
                throw new IndexOutOfRangeException();
            }
        }

        public void WriteBytes(byte[] data)
        {
            WriteBytes(data, 0, data.Length);
        }

        public void WriteBytes(byte[] data, int offset, int count)
        {
            CheckWriterIndex(count);
            Buffer.BlockCopy(data, offset, _buffer, _writerIndex, count);
            _writerIndex += count;
        }

        public void WriteBool(bool value)
        {
            WriteByte((byte)(value ? 1 : 0));
        }

        public void WriteByte(byte value)
        {
            CheckWriterIndex(1);
            _buffer[_writerIndex++] = value;
        }

        public void WriteSbyte(sbyte value)
        {
            WriteByte((byte)value);
        }

        // 原有方法（使用默认字节序）
        public void WriteShort(short value) => WriteShort(value, DefaultByteOrder);
        public void WriteUShort(ushort value) => WriteUShort(value, DefaultByteOrder);
        public void WriteInt(int value) => WriteInt(value, DefaultByteOrder);
        public void WriteUInt(uint value) => WriteUInt(value, DefaultByteOrder);
        public void WriteLong(long value) => WriteLong(value, DefaultByteOrder);
        public void WriteULong(ulong value) => WriteULong(value, DefaultByteOrder);
        public void WriteFloat(float value) => WriteFloat(value, DefaultByteOrder);
        public void WriteDouble(double value) => WriteDouble(value, DefaultByteOrder);

        // 支持指定字节序的新方法
        public void WriteShort(short value, ByteOrder byteOrder)
        {
            CheckWriterIndex(2);
            if (byteOrder == ByteOrder.BigEndian && BitConverter.IsLittleEndian)
            {
                // 大端写入
                _buffer[_writerIndex++] = (byte)(value >> 8);
                _buffer[_writerIndex++] = (byte)(value & 0xFF);
            }
            else if (byteOrder == ByteOrder.LittleEndian && !BitConverter.IsLittleEndian)
            {
                // 小端写入（在大端系统上）
                _buffer[_writerIndex++] = (byte)(value & 0xFF);
                _buffer[_writerIndex++] = (byte)(value >> 8);
            }
            else
            {
                // 系统默认
                byte[] bytes = BitConverter.GetBytes(value);
                WriteBytes(bytes);
            }
        }

        public void WriteUShort(ushort value, ByteOrder byteOrder)
        {
            CheckWriterIndex(2);
            if (byteOrder == ByteOrder.BigEndian && BitConverter.IsLittleEndian)
            {
                // 大端写入
                _buffer[_writerIndex++] = (byte)(value >> 8);
                _buffer[_writerIndex++] = (byte)(value & 0xFF);
            }
            else if (byteOrder == ByteOrder.LittleEndian && !BitConverter.IsLittleEndian)
            {
                // 小端写入（在大端系统上）
                _buffer[_writerIndex++] = (byte)(value & 0xFF);
                _buffer[_writerIndex++] = (byte)(value >> 8);
            }
            else
            {
                // 系统默认
                byte[] bytes = BitConverter.GetBytes(value);
                WriteBytes(bytes);
            }
        }

        public void WriteInt(int value, ByteOrder byteOrder)
        {
            CheckWriterIndex(4);
            if (byteOrder == ByteOrder.BigEndian && BitConverter.IsLittleEndian)
            {
                // 大端写入
                _buffer[_writerIndex++] = (byte)(value >> 24);
                _buffer[_writerIndex++] = (byte)(value >> 16);
                _buffer[_writerIndex++] = (byte)(value >> 8);
                _buffer[_writerIndex++] = (byte)(value & 0xFF);
            }
            else if (byteOrder == ByteOrder.LittleEndian && !BitConverter.IsLittleEndian)
            {
                // 小端写入（在大端系统上）
                _buffer[_writerIndex++] = (byte)(value & 0xFF);
                _buffer[_writerIndex++] = (byte)(value >> 8);
                _buffer[_writerIndex++] = (byte)(value >> 16);
                _buffer[_writerIndex++] = (byte)(value >> 24);
            }
            else
            {
                // 系统默认
                byte[] bytes = BitConverter.GetBytes(value);
                WriteBytes(bytes);
            }
        }

        public void WriteUInt(uint value, ByteOrder byteOrder)
        {
            WriteInt((int)value, byteOrder);
        }

        public void WriteLong(long value, ByteOrder byteOrder)
        {
            CheckWriterIndex(8);
            byte[] bytes = BitConverter.GetBytes(value);
            ConvertByteOrder(bytes, byteOrder);
            WriteBytes(bytes);
        }

        public void WriteULong(ulong value, ByteOrder byteOrder)
        {
            WriteLong((long)value, byteOrder);
        }

        public void WriteFloat(float value, ByteOrder byteOrder)
        {
            CheckWriterIndex(4);
            byte[] bytes = BitConverter.GetBytes(value);
            ConvertByteOrder(bytes, byteOrder);
            WriteBytes(bytes);
        }

        public void WriteDouble(double value, ByteOrder byteOrder)
        {
            CheckWriterIndex(8);
            byte[] bytes = BitConverter.GetBytes(value);
            ConvertByteOrder(bytes, byteOrder);
            WriteBytes(bytes);
        }

        public void WriteUTF(string value) => WriteUTF(value, DefaultByteOrder);
        public void WriteUTF(string value, ByteOrder byteOrder)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            int num = bytes.Length + 1; // 注意：字符串末尾写入结束符
            if (num > ushort.MaxValue)
                throw new FormatException($"String length cannot be greater than {ushort.MaxValue} !");

            WriteUShort(Convert.ToUInt16(num), byteOrder);
            WriteBytes(bytes);
            WriteByte((byte)'\0');
        }

        // 可以为其他方法也添加字节序支持...
        public void WriteVector2(Vector2 value) => WriteVector2(value, DefaultByteOrder);
        public void WriteVector2(Vector2 value, ByteOrder byteOrder)
        {
            WriteFloat(value.x, byteOrder);
            WriteFloat(value.y, byteOrder);
        }

        public void WriteVector3(Vector3 value) => WriteVector3(value, DefaultByteOrder);
        public void WriteVector3(Vector3 value, ByteOrder byteOrder)
        {
            WriteFloat(value.x, byteOrder);
            WriteFloat(value.y, byteOrder);
            WriteFloat(value.z, byteOrder);
        }

        public void WriteVector4(Vector4 value) => WriteVector4(value, DefaultByteOrder);
        public void WriteVector4(Vector4 value, ByteOrder byteOrder)
        {
            WriteFloat(value.x, byteOrder);
            WriteFloat(value.y, byteOrder);
            WriteFloat(value.z, byteOrder);
            WriteFloat(value.w, byteOrder);
        }

        #endregion

        /// <summary>
        /// 大小端转换。
        /// </summary>
        public static void ReverseOrder(byte[] data)
        {
            ReverseOrder(data, 0, data.Length);
        }

        /// <summary>
        /// 大小端转换（指定偏移和长度）。
        /// </summary>
        public static void ReverseOrder(byte[] data, int offset, int length)
        {
            if (length <= 1)
            {
                return;
            }

            int end = offset + length - 1;
            int max = offset + length / 2;
            byte temp;
            for (int index = offset; index < max; index++, end--)
            {
                temp = data[end];
                data[end] = data[index];
                data[index] = temp;
            }
        }
    }
}