using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chetch.Messaging;
using Chetch.Utilities;

namespace Chetch.Arduino2
{
    /// <summary>
    /// Messages sent to board
    /// </summary>
    public class ADMMessage
    {
        static public ADMMessage Deserialize(byte[] bytes)
        {
            var msg = new ADMMessage();
            msg.ReadBytes(bytes);
            return msg;
        }

        public MessageType Type { get; set; }
        public byte Tag { get; set; } = 0; //can be used to track messages
        public byte Target { get; set; } = 0; //ID number on board to determine what is beig targeted
        public byte Sender { get; set; } = 0; //
        public List<byte[]> Arguments { get; } = new List<byte[]>();
        public bool LittleEndian { get; set; } = true;

        //Convenience properties
        public bool IsCommand => Type == MessageType.COMMAND;
        public bool IsData => Type == MessageType.DATA;

        public bool IsCommandRelated => Type == MessageType.COMMAND_RESPONSE || Type == MessageType.COMMAND;

        public bool IsConfigRelated => Type == MessageType.CONFIGURE || Type == MessageType.CONFIGURE_RESPONSE;

        public bool IsInitRelated => Type == MessageType.INITIALISE || Type == MessageType.INITIALISE_RESPONSE;

        public dynamic GetArgument(int idx, Type type = null)
        {
            if (type == null) type = typeof(Object);

            return Chetch.Utilities.Convert.ToType(type, Arguments[idx], LittleEndian);
        }

        public T GetArgument<T>(int idx)
        {
            return Chetch.Utilities.Convert.To<T>(Arguments[idx], LittleEndian);
        }

        public void AddArgument(byte[] bytes)
        {
            Arguments.Add(bytes);
        }

        public void AddArgument(byte b)
        {
            AddArgument(new byte[] { b });
        }

        public void AddArgument(bool b)
        {
            AddArgument(b ? (byte)1 : (byte)0);
        }

        public void AddArgument(String s)
        {
            AddArgument(Chetch.Utilities.Convert.ToBytes(s));
        }

        public void AddArgument(Int16 arg)
        {
            byte[] bytes = Chetch.Utilities.Convert.ToBytes(arg, LittleEndian, true, -1);
            AddArgument(bytes);
        }

        public void AddArgument(UInt16 arg)
        {
            byte[] bytes = Chetch.Utilities.Convert.ToBytes(arg, LittleEndian, true, -1);
            AddArgument(bytes);
        }

        public void AddArgument(int arg)
        {
            byte[] bytes = Chetch.Utilities.Convert.ToBytes((Int16)arg, LittleEndian, true, -1);
            AddArgument(bytes);
        }

        public void AddArgument(long arg)
        {
            byte[] bytes = Chetch.Utilities.Convert.ToBytes((Int32)arg, LittleEndian, true, -1);
            AddArgument(bytes);
        }

        public void AddArgument(ulong arg)
        {
            byte[] bytes = Chetch.Utilities.Convert.ToBytes((UInt32)arg, LittleEndian, true, -1);
            AddArgument(bytes);
        }

        public void WriteBytes(List<byte> bytes)
        {

            //1. Add member vars
            bytes.Add((byte)Type);
            bytes.Add(Tag);
            bytes.Add(Target);
            bytes.Add(Sender);

            //2. add arguments (length of argument followed by argment bytes)
            foreach (var b in Arguments)
            {
                bytes.Add((byte)b.Length);
                bytes.AddRange(b);
            }
        }

        public int GetByteCount()
        {
            int byteCount = 4; //bytes for type, tag, target and sender
            foreach (var b in Arguments)
            {
                byteCount += 1 + b.Length;
            }
            return byteCount;
        }

        public byte[] Serialize()
        {
            List<byte> bytes = new List<byte>();
            WriteBytes(bytes);
            return bytes.ToArray();
        }

        public void ReadBytes(byte[] bytes)
        {
            Type = (Chetch.Messaging.MessageType)bytes[0];
            Tag = bytes[1];
            Target = bytes[2];
            Sender = bytes[3];

            //... and convert arguments
            int argumentIndex = 4;
            while (argumentIndex < bytes.Length)
            {
                int length = bytes[argumentIndex];
                byte[] arg = new byte[length];
                for (int i = 0; i < length; i++)
                {
                    arg[i] = bytes[argumentIndex + i + 1];
                }
                AddArgument(arg);
                argumentIndex += length + 1;
            }
        }
    }
}
