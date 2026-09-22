export function ClientsPage() {
  return (
    <div className="page-content">
      <section className="page-heading">
        <div>
          <p className="eyebrow">CLIENTES</p>
          <h1>Clients</h1>
          <p className="heading-subtitle">Clientes conectados à rede.</p>
        </div>
        <button className="primary-button">
          <i className="pi pi-download" /> Export CSV
        </button>
      </section>
      <div className="panel">
        <h2>Client List</h2>
        <p style={{ color: "var(--text-muted)", marginTop: "var(--space-4)" }}>
          Tabela de clientes será implementada na F13-T012.
        </p>
      </div>
    </div>
  );
}
