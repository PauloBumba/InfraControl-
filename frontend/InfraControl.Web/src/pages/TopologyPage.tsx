import { TopologyView } from "@/components/topology/TopologyView";

export function TopologyPage() {
  return (
    <div className="page-content">
      <section className="page-heading">
        <div>
          <p className="eyebrow">TOPOLOGIA</p>
          <h1>Topology</h1>
          <p className="heading-subtitle">Mapa interativo da rede</p>
        </div>
      </section>
      <TopologyView />
    </div>
  );
}
