using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chetch.Arduino2
{
    public class ADMRequestManager
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
            public ADMRequest Request { get; internal set; }

            public byte Tag { get; internal set; }
            byte[] _tags;
            public byte TagCount { get; internal set; } = 0;

            public bool IsFull => TagCount == _tags.Length;

            public bool HasExpired => Request.HasExpired || (_filled && TagCount == 0);

            public bool IsUsed => _used;
            bool _used = false;

            bool _filled = false;

            public TagSet(ADMRequest request, int tagSetSize)
            {
                Request = request;
                Tag = Request.Tag;
                _tags = new byte[tagSetSize];
                TagCount = 1;
                _tags[0] = Tag;
                for (int i = 1; i < _tags.Length; i++)
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

            public void Use()
            {
                Add(Tag);
            }

            public void Add(byte tag)
            {
                _used = true;
                if (Contains(tag))
                {
                    return;
                }

                if (IsFull)
                {
                    throw new InvalidOperationException(String.Format("Tag set {0} is full", Tag));
                }
                _tags[TagCount] = tag;
                TagCount++;
                if (IsFull) _filled = true;
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

            
            //Check if the tag belongs to a set and we return the set Request if it does
            TagSet tagSet = null;
            foreach (var ts in _tagSets.Values)
            {
                if (ts.Contains(tag))
                {
                    tagSet = ts;
                    break;
                }
            }

            ADMRequest req = null;
            if(tagSet != null)
            {
                tagSet.Remove(tag);
                if (tagSet.HasExpired)
                {
                    _requests.Remove(tagSet.Tag);
                    _tagSets.Remove(tagSet.Tag);
                }
                if (tagSet.Tag != tag)
                {
                    _requests.Remove(tag);
                }
                req = tagSet.Request;
            } else
            {
                req = _requests[tag];
                _requests.Remove(tag);
            }

            return req;
        }

        public ADMRequest Release(ADMRequest req)
        {
            return Release(req.Tag);
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
                _tagSets[req.Tag] = new TagSet(req, tagsSetSize);
            }

            return req;
        }


        public ADMRequest AddRequestToSet(byte tagKey)
        {
            if (!_tagSets.ContainsKey(tagKey))
            {
                throw new InvalidOperationException(String.Format("Cannot add request to set {0} as the set does not exists", tagKey));
            }


            TagSet tagSet = _tagSets[tagKey];
            if (tagSet.HasExpired)
            {
                throw new InvalidOperationException(String.Format("Cannot add request to set {0} as the set has already expired", tagKey));
            }


            if (!tagSet.IsUsed)
            {
                tagSet.Use(); 
                return tagSet.Request;
            }
            else
            {
                ADMRequest newReq = AddRequest(System.Math.Max(1000, tagSet.Request.RemainingTTL));
                tagSet.Add(newReq.Tag);
                return newReq;
            }
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
                foreach (var req in _requests.Values)
                {
                    if (!req.HasExpired)
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

        public void RemoveExpiredRequests()
        {
            List<byte> toRemove = new List<byte>();
            foreach(var req in _requests.Values)
            {
                if (req.HasExpired)
                {
                    toRemove.Add(req.Tag);
                }
            }
            foreach(var tag in toRemove)
            {
                _requests.Remove(tag);
            }
        }

        public void Reset()
        {
            _requests.Clear();
            _tagSets.Clear();
        }
    }
}
