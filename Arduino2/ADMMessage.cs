using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chetch.Messaging;
using Chetch.Utilities;

namespace Chetch.Arduino2
{
    public class ADMMessage
    {
        public class MessageTags
        {
            class MessageTag
            {
                byte Tag = 0;
                long Created = 0;
                int TTL = 5 * 1000;

                public MessageTag(byte tag, int ttl)
                {
                    if (tag == 0) throw new ArgumentException("Tag value cannot be 0");
                    Tag = tag;
                    TTL = ttl;
                    Created = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                }

                public int RemainingTTL => (int)(TTL - ((DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - Created));

                public bool IsAvailable => RemainingTTL < 0;

            }

            public const int DEFAULT_TTL = 5 * 1000; //how long in millis a Tag can last for 
            private Dictionary<byte, MessageTag> _usedTags = new Dictionary<byte, MessageTag>();
            private Dictionary<byte, List<byte>> _tagSets = new Dictionary<byte, List<byte>>();

            /// <summary>
            /// Releasing the tag means that the byte value can be used again for new tags.  It should be done at the end of a message loop.
            /// The return value is so that the onward message tag value can be set depending on whether the tag is part of a tagset or not
            /// </summary>
            /// <param name="tag"></param>
            /// <returns></returns>
            public byte Release(byte tag)
            {
                if (tag == 0) return 0;

                //if this tag is a tagset key then only remove if it is available and then return the tag
                if (_tagSets.ContainsKey(tag))
                {
                    if (IsAvailable(tag)) _usedTags.Remove(tag);
                    return tag;
                } 
                //otherwise this is just a normal tag so we can remove it immediately
                else if (_usedTags.ContainsKey(tag))
                {
                    _usedTags.Remove(tag);
                }

                //now we check if the tag belongs to a set and we return the tagkey if it does
                foreach (KeyValuePair<byte, List<byte>> tagSet in _tagSets)
                {
                    if (tagSet.Value.Contains(tag))
                    {
                        return tagSet.Key;
                    }
                }
                return tag;
            }

            public byte CreateTag(int ttl = DEFAULT_TTL)
            {
                //start from 1 as we reserve 0 to mean a non-assigned tag
                for (byte i = 1; i <= 255; i++)
                {
                    if (IsAvailable(i))
                    {
                        _usedTags[i] = new MessageTag(i, ttl);
                        return i;
                    }
                }
                throw new Exception("Cannot create tag as all tags are being used");
            }

            public byte CreateTagSet(int ttl)
            {
                byte tag = CreateTag(ttl);
                _tagSets[tag] = new List<byte>();

                return tag;
            }

            public byte CreateTagInSet(byte tagKey)
            {
                if (!_tagSets.ContainsKey(tagKey))
                {
                    throw new InvalidOperationException(String.Format("Cannot create tag in set {0} as the set does not exists", tagKey));
                }


                MessageTag mt = _usedTags[tagKey];
                if (mt.IsAvailable)
                {
                    throw new InvalidOperationException(String.Format("Cannot create tag in set {0} as the set has already expired", tagKey));
                }

                //if the set is empty use the tagKey as the tag otherwise create a new tag
                byte tag = _tagSets[tagKey].Count == 0 ? tagKey : CreateTag(System.Math.Max(1000, mt.RemainingTTL));
                if (_tagSets[tagKey].Contains(tag))
                {
                    throw new Exception(String.Format("Tag set {0} already contains tag {1}", tagKey, tag));
                }
                _tagSets[tagKey].Add(tag);

                return tag;
            }


            public bool IsAvailable(byte tag)
            {
                return !_usedTags.ContainsKey(tag) || _usedTags[tag].IsAvailable;
            }

            public int Used
            {
                get
                {
                    int used = 0;
                    foreach (var mt in _usedTags.Values)
                    {
                        if (!mt.IsAvailable)
                        {
                            used++;
                        }
                    }

                    return used;
                }
            }

            public int Available
            {
                get
                {
                    return 255 - Used;
                }
            }

            public void Reset()
            {
                _usedTags.Clear();
            }
        }

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
