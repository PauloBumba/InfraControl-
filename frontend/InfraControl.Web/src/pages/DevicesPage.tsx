export function DevicesPage() {
  return (
    <div className="page-content">
      <section className="page-heading">
        <div>
          <p className="eyebrow">DISPOSITIVOS</p>
          <h1>Devices</h1>
          <p className="heading-subtitle">Lista de dispositivos de rede.</p>
        </div>
        <button className="primary-button">
          <i className="pi pi-plus" /> Adicionar dispositivo
        </button>
      </section>
      <div className="panel">
        <h2>Device List</h2>
        <p style={{ color: "var(--text-muted)", marginTop: "var(--space-4)" }}>
          Tabela de dispositivos será implementada na F13-T009.
        </p>
      </div>
    </div>
  );
}
