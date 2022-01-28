using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chetch.Arduino2
{
    public class ADMRequests
    {
        public class ADMRequest
        {
            public byte Tag { get; internal set; } = 0;

            long Created = 0;
            int TTL = 5 * 1000;

            public String Owner { get; set; }

            public ADMRequest(byte tag, int ttl, String owner = null)
            {
                if (tag == 0) throw new ArgumentException("Tag value cannot be 0");
                Tag = tag;
                TTL = ttl;
                Created = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
                Owner = owner;
            }

            public int RemainingTTL => (int)(TTL - ((DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond) - Created));

            public bool HasExpired => RemainingTTL < 0;

        }

        class TagSet
        {
            public byte Tag { get; internal set; }
            byte[] _tags;
            public byte TagCount { get; internal set; } = 0;

            public bool IsFull => TagCount == _tags.Length;

            public bool IsEmpty => TagCount == 0;

            public bool IsExhausted => IsEmpty && _used;

            bool _used = false;

            public TagSet(byte tag, int tagSetSize)
            {
                Tag = tag;
                _tags = new byte[tagSetSize];
                TagCount = 0;
                for (int i = 0; i < _tags.Length; i++)
                {
                    _tags[i] = 0;
                }
            }

            public bool Contains(byte tag)
            {
                foreach(byte t in _tags)
                {
                    if (t == tag) return true;
                }
                return false;
            }

            public void Add(byte tag)
            {
                if (Contains(tag))
                {
                    throw new Exception(String.Format("Tag set {0} already contains {1}", Tag, tag));
                    _tags[TagCount] = tag;
                    TagCount++;
                }
                _used = true;
            }

            public void Remove(byte tag)
            {
                for (int i = 0; i < _tags.Length; i++)
                {
                    if (_tags[i] == tag)
                    {
                        _tags[i] = 0;
                        TagCount--;
                        break;
                    }
                }
            }
        }

        public const int DEFAULT_TTL = 5 * 1000; //how long in millis a Tag can last for 
        private Dictionary<byte, ADMRequest> _requests = new Dictionary<byte, ADMRequest>();
        private Dictionary<byte, TagSet> _tagSets = new Dictionary<byte, TagSet>();

        /// <summary>
        /// Releasing a request
        /// <param name="tag"></param>
        /// <returns></returns>
        public ADMRequest Release(byte tag)
        {
            if (tag == 0 || !_requests.ContainsKey(tag)) return null;

            var req = _requests[tag];

            //if this tag is the key to a tagset key then only remove if it is available and then return the tag
            if (_tagSets.ContainsKey(tag))
            {
                if (IsAvailable(tag) || _tagSets[tag].IsExhausted)
                {
                    _requests.Remove(tag);
                    _tagSets.Remove(tag);
                }
                return req;
            }
            //otherwise this is just a normal tag so we can remove it immediately
            else if (_requests.ContainsKey(tag))
            {
                _requests.Remove(tag);
            }
            
            //now we check if the tag belongs to a set and we return the tagkey if it does
            foreach (var tagSet in _tagSets.Values)
            {
                if (tagSet.Contains(tag))
                {
                    tagSet.Remove(tag);
                    return _requests[tagSet.Tag];
                }
            }
            return req;
        }

        public ADMRequest AddRequest(int ttl = DEFAULT_TTL, int tagsSetSize = 0)
        {
            ADMRequest req = null;
            //start from 1 as we reserve 0 to mean a non-assigned tag
            for (byte i = 1; i <= 255; i++)
            {
                if (IsAvailable(i))
                {
                    req = new ADMRequest(i, ttl);
                    _requests[i] = req;
                    break;
                }
            }
            if (req == null)
            {
                throw new Exception("Cannot add request as all tags are being used");
            }

            if(tagsSetSize > 0)
            {
                _tagSets[req.Tag] = new TagSet(req.Tag, tagsSetSize);
            }

            return req;
        }


        public ADMRequest AddRequestToSet(byte tagKey)
        {
            if (!_tagSets.ContainsKey(tagKey))
            {
                throw new InvalidOperationException(String.Format("Cannot create tag in set {0} as the set does not exists", tagKey));
            }


            ADMRequest req = _requests[tagKey];
            if(req.HasExpired)
            {
                throw new InvalidOperationException(String.Format("Cannot add request set {0} as the set has already expired", tagKey));
            }

            //if the set is empty use the tagKey as the tag otherwise create a new tag
            ADMRequest newReq = AddRequest(System.Math.Max(1000, req.RemainingTTL));
            byte tag = newReq.Tag;
            _tagSets[tagKey].Add(tag);

            return newReq;
        }

        protected bool IsTagSet(byte tag)
        {
            return _tagSets.ContainsKey(tag);
        }

        public bool IsAvailable(byte tag)
        {
            return !_requests.ContainsKey(tag) || _requests[tag].HasExpired;
        }

        public int Used
        {
            get
            {
                int used = 0;
                foreach (var mt in _requests.Values)
                {
                    if (!mt.HasExpired)
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
            _requests.Clear();
            _tagSets.Clear();
        }
    }
}
