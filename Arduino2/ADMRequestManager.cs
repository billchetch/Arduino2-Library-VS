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

            public bool Proceed { get; set; } = true;

            private String _owner;
            public String Owner
            {
                get { return _owner; }
                set { _owner = value; Proceed = true; ; }
            }

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

        public const int DEFAULT_TTL = 5 * 1000; //how long in millis a Tag can last for 
        private Dictionary<byte, ADMRequest> _requests = new Dictionary<byte, ADMRequest>();
        
        /// <summary>
        /// Releasing a request
        /// <param name="tag"></param>
        /// <returns></returns>
        public ADMRequest Release(byte tag)
        {
            if (tag == 0 || !_requests.ContainsKey(tag)) return null;

            var req = _requests[tag];
            _requests.Remove(tag);
            
            return req;
        }

        public ADMRequest Release(ADMRequest req)
        {
            return Release(req.Tag);
        }

        public ADMRequest AddRequest(int ttl = DEFAULT_TTL)
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


            return req;
        }

        public ADMRequest AddRequest(String owner, int ttl = DEFAULT_TTL)
        {
            ADMRequest req = AddRequest(ttl);
            req.Owner = owner;
            return req;
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
        }
    }
}
