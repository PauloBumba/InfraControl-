export function AlertsPage() {
  return (
    <div className="page-content">
      <section className="page-heading">
        <div>
          <p className="eyebrow">ALERTAS</p>
          <h1>Alerts</h1>
          <p className="heading-subtitle">Alertas e notificações do sistema.</p>
        </div>
        <button className="primary-button">
          <i className="pi pi-check" /> Marcar tudo como lido
        </button>
      </section>
      <div className="panel">
        <h2>Alert List</h2>
        <p style={{ color: "var(--text-muted)", marginTop: "var(--space-4)" }}>
          Lista de alertas com severidade será implementada na F13-T014.
        </p>
      </div>
    </div>
  );
}
