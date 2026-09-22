import { Tag } from "primereact/tag";

interface Event {
  id: string;
  time: string;
  type: "info" | "warning" | "error" | "success";
  message: string;
  source: string;
}

const events: Event[] = [
  { id: "1", time: "11:42", type: "info", message: "Client connected", source: "AP-AC-PRO" },
  { id: "2", time: "11:38", type: "success", message: "Firmware updated", source: "US-24-250W" },
  { id: "3", time: "11:35", type: "warning", message: "High CPU usage (87%)", source: "UDM Pro Max" },
  { id: "4", time: "11:30", type: "error", message: "Port 24 link down", source: "US-8-150W" },
  { id: "5", time: "11:28", type: "info", message: "DHCP lease renewed", source: "UDM Pro Max" },
  { id: "6", time: "11:22", type: "success", message: "Backup completed", source: "UDM Pro Max" },
  { id: "7", time: "11:15", type: "warning", message: "Rogue AP detected", source: "IDS/IPS" },
  { id: "8", time: "11:10", type: "info", message: "Site-to-site VPN up", source: "UDM Pro Max" },
];

function typeBody(event: Event) {
  const severityMap = { info: "info", warning: "warning", error: "danger", success: "success" } as const;
  return <Tag severity={severityMap[event.type]} />;
}

export function RecentEvents() {
  return (
    <article className="panel recent-events">
      <div className="panel-heading">
        <div>
          <h2>Recent Events</h2>
          <p>Latest system events</p>
        </div>
        <button className="text-button">
          View All <i className="pi pi-arrow-right" />
        </button>
      </div>
      <div className="events-list">
        {events.map((event) => (
          <div className="event-row" key={event.id}>
            <div className="event-time">
              <i className="pi pi-clock" />
              <span>{event.time}</span>
            </div>
            <div className="event-content">
              <span className="event-message">{event.message}</span>
              <span className="event-source">{event.source}</span>
            </div>
            {typeBody(event)}
          </div>
        ))}
      </div>
    </article>
  );
}
