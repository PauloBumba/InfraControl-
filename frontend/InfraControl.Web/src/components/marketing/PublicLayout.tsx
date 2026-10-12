import type { ReactNode } from "react";
import { Link, useLocation } from "react-router-dom";

/**
 * F26-T002 — shell COMPARTILHADO das rotas públicas de vendas
 * (`/home`, `/recursos`, `/contato`).
 *
 * Header e footer são um componente só — cada página só fornece o conteúdo,
 * então os links nunca saem de sincronia. A navegação usa `Link` (nada de
 * `<a href>`, que recarregaria o bundle) e marca a rota atual com
 * `aria-current="page"`.
 *
 * O login está sempre à mão: a landing é porta de entrada de quem ainda não
 * entrou, não um substituto do produto.
 */

const NAV = [
  { to: "/home", label: "Home" },
  { to: "/recursos", label: "Recursos" },
  { to: "/contato", label: "Contato" },
] as const;

function navLinkClass(active: boolean): string {
  return active ? "uni-public-nav-link uni-public-nav-link-active" : "uni-public-nav-link";
}

export function PublicLayout({ children }: { children: ReactNode }) {
  const { pathname } = useLocation();

  return (
    <div className="uni-public">
      <header className="uni-public-header">
        <div className="uni-public-container uni-public-bar">
          <Link to="/home" className="uni-public-brand">
            <span className="uni-public-logo">
              <i className="pi pi-sitemap" />
            </span>
            <span>InfraControl</span>
          </Link>

          <nav aria-label="Navegação principal">
            <ul className="uni-public-nav">
              {NAV.map((item) => (
                <li key={item.to}>
                  <Link
                    to={item.to}
                    className={navLinkClass(pathname === item.to)}
                    aria-current={pathname === item.to ? "page" : undefined}
                  >
                    {item.label}
                  </Link>
                </li>
              ))}
            </ul>
          </nav>

          <div className="uni-public-bar-actions">
            <Link to="/login" className="uni-public-button uni-public-button-small">
              Entrar
            </Link>
          </div>
        </div>
      </header>

      <main className="uni-public-main">{children}</main>

      <footer className="uni-public-footer">
        <div className="uni-public-container uni-public-footer-inner">
          <span>InfraControl — monitoramento e operação de infraestrutura.</span>
          <nav aria-label="Navegação do rodapé">
            <ul className="uni-public-nav">
              {NAV.slice(1).map((item) => (
                <li key={item.to}>
                  <Link
                    to={item.to}
                    className={navLinkClass(pathname === item.to)}
                    aria-current={pathname === item.to ? "page" : undefined}
                  >
                    {item.label}
                  </Link>
                </li>
              ))}
              <li>
                <Link to="/login" className="uni-public-nav-link">
                  Entrar
                </Link>
              </li>
            </ul>
          </nav>
        </div>
      </footer>
    </div>
  );
}
