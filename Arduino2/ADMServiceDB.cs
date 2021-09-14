using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chetch.Database;

namespace Chetch.Arduino2
{

    public class ADMServiceDB : DB
    {
        
        public const String EVENT_LOG_TABLE = "adm_event_log";
        public const String STATE_LOG_TABLE = "adm_state_log";

        protected  System.Web.Script.Serialization.JavaScriptSerializer JSONSerializer = new System.Web.Script.Serialization.JavaScriptSerializer();

        static public ADMServiceDB Create(System.Configuration.ApplicationSettingsBase settings, String dbnameKey = null)
        {
            ADMServiceDB db = dbnameKey != null ? DB.Create<ADMServiceDB>(settings, dbnameKey) : DB.Create<ADMServiceDB>(settings);
            return db;
        }

        override public void Initialize()
        {
            //SELECTS
            // - Events
            String fields = "e.*";
            String from = String.Format("{0} e", EVENT_LOG_TABLE);
            String filter = null;
            String sort = "created {0}";
            this.AddSelectStatement("events", fields, from, filter, sort, null);

            // - Event
            fields = "e.*";
            from = String.Format("{0} e", EVENT_LOG_TABLE);
            filter = "event_name='{0}' AND event_source='{1}' AND IF({2} >= 0, e.id < {2}, true)";
            sort = "created DESC";
            this.AddSelectStatement("latest-event", fields, from, filter, sort, null);

            // - Event
            fields = "e.*";
            from = String.Format("{0} e", EVENT_LOG_TABLE);
            filter = "event_name='{0}' AND event_source='{1}' AND IF({2} >= 0, e.id > {2}, true)";
            sort = "created ASC";
            this.AddSelectStatement("first-event", fields, from, filter, sort, null);

            //Init base
            base.Initialize();
        }

        public long LogEvent(String eventName, String source, Object data, String info = null)
        {
            var newRow = new DBRow();
            newRow["event_name"] = eventName;
            newRow["event_source"] = source;
            if (data != null) newRow["event_data"] = data.ToString();
            if (info != null) newRow["event_info"] = info;

            return Insert(EVENT_LOG_TABLE, newRow);
        }

        public long LogEvent(ArduinoDevice device, String eventName, Object data, String info = null)
        {
            return LogEvent(eventName, device.FullID, data, info);
        }

        public long LogEvent(ArduinoDeviceManager adm, String eventName, Object data, String info = null)
        {
            return LogEvent(eventName, adm.ID, data, info);
        }


        public DBRow GetLatestEvent(String eventName, String source, long limitId = -1)
        {
            return SelectRow("latest-event", "*", eventName, source, limitId.ToString());
        }

        public DBRow GetFirstEvent(String eventName, String source, long limitId = -1)
        {
            return SelectRow("first-event", "*", eventName, source, limitId.ToString());
        }

        public DBRow GetLatestEvent(String currentEventName, String preceedingEventName, String source)
        {
            DBRow pe = GetLatestEvent(preceedingEventName, source);
            long limitId = pe == null ? -1 : pe.ID;
            DBRow ce = GetFirstEvent(currentEventName, source, limitId);
            return ce;
        }

        public long LogState(String stateSource, String stateName, Object state, String description = null)
        {
            var newRow = new DBRow();
            newRow["state_source"] = stateSource;
            newRow["state_name"] = stateName;
            if (description != null) newRow["state_description"] = description;
            newRow["state"] = state;
            return Insert(STATE_LOG_TABLE, newRow);
        }

        public long LogState(ArduinoDevice device, String stateName, Object state, String description = null)
        {
            return LogState(device.FullID, stateName, state, description);
        }

         
    } //end class
} //end namespace
