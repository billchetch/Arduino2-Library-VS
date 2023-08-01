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
        //Logging stuff
        public struct EventLogEntry
        {
            public String Name;
            public String Source;
            public String Info;
            public Object Data;
        }

        public struct SnapshotLogEntry
        {
            public String Source;
            public String StateName;
            public Object State;
            public String Description;
        }

        public const String EVENT_LOG_TABLE = "adm_event_log";
        public const String SNAPSHOT_LOG_TABLE = "adm_snapshot_log";

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

        public long LogEvent(EventLogEntry entry)
        {
            return LogEvent(entry.Name, entry.Source, entry.Data, entry.Info);
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

        public long LogSnapshot(SnapshotLogEntry entry)
        {
            return LogSnapshot(entry.Source, entry.StateName, entry.State, entry.Description);
        }

        public long LogSnapshot(String stateSource, String stateName, Object state, String description = null)
        {
            var newRow = new DBRow();
            newRow["state_source"] = stateSource;
            newRow["state_name"] = stateName;
            if (description != null) newRow["state_description"] = description;
            newRow["state"] = state;
            return Insert(SNAPSHOT_LOG_TABLE, newRow);
        }

         
    } //end class
} //end namespace
