/**
 * F17-T016 — InsightsPage: N/A de contrato (documentado, sem serviço novo).
 *
 * Os três painéis do protótipo — DPI por aplicação, Tráfego por Cliente e
 * Histórico de Speed Test — não têm DOMÍNIO no backend: varredura em `src/`
 * não acha nenhum controller/entidade de DPI, tráfego ou speed test (mesmos
 * gráficos já removidos do Dashboard no T009 por "sem contrato"). Não há o
 * que conectar, e exibir os arrays fabricados de `dashboardData` como se
 * fossem dados reais do sistema seria mentira para o usuário.
 *
 * Decisão tomada aqui: esvazer a página com estados vazios honestos,
 * mantendo rota/menu estáveis (não deletar feature sem decisão humana).
 * O que fazer quando houver produto de tráfego — construir o backend, apagar
 * a página/rota ou manter como demo — está escalado em TASKS.md e
 * PROJECT_STATE.md.
 */
export function InsightsPage() {
  const emptyStyle = {
    textAlign: "center" as const,
    padding: 24,
    color: "#94a3b8",
    fontSize: 13,
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", gap: 16 }}>
      <div>
        <h1 style={{ fontSize: 20, fontWeight: 700 }}>Insights</h1>
        <p style={{ fontSize: 12, color: "#6b7280", marginTop: 2 }}>Análises de tráfego, DPI e performance</p>
      </div>

      <div className="uni-panel">
        <div className="uni-panel-header">
          <h2>Aplicações por Tráfego (DPI)</h2>
        </div>
        <div className="uni-panel-body">
          <div style={emptyStyle}>
            Não implementado no backend — não há coleta de tráfego por aplicação
          </div>
        </div>
      </div>

      <div className="uni-grid-2">
        <div className="uni-panel">
          <div className="uni-panel-header">
            <h2>Tráfego por Cliente</h2>
          </div>
          <div className="uni-panel-body">
            <div style={emptyStyle}>
              Não implementado no backend — não há medição de tráfego por cliente
            </div>
          </div>
        </div>

        <div className="uni-panel">
          <div className="uni-panel-header">
            <h2>Histórico de Speed Test</h2>
          </div>
          <div className="uni-panel-body">
            <div style={emptyStyle}>
              Não implementado no backend — não há execução de speed test
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
