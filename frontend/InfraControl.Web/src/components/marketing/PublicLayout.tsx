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
              <svg viewBox="0 0 64 64" width="28" height="28" role="img" aria-label="Logo do InfraControl">
                <defs>
                  <linearGradient id="infraHeaderGrad" x1="0%" y1="0%" x2="100%" y2="100%">
                    <stop offset="0%" style={{stopColor:'#3b82f6',stopOpacity:1}} />
                    <stop offset="100%" style={{stopColor:'#2563eb',stopOpacity:1}} />
                  </linearGradient>
                </defs>
                <rect width="64" height="64" rx="12" fill="url(#infraHeaderGrad)" />
                <g fill="white">
                  <rect x="16" y="12" width="32" height="40" rx="4" fill="none" stroke="white" strokeWidth="2.5"/>
                  <rect x="20" y="18" width="24" height="6" rx="2" fill="white" opacity="0.9"/>
                  <rect x="20" y="28" width="24" height="6" rx="2" fill="white" opacity="0.9"/>
                  <rect x="20" y="38" width="24" height="6" rx="2" fill="white" opacity="0.9"/>
                  <circle cx="38" cy="21" r="1.5" fill="#22c55e"/>
                  <circle cx="38" cy="31" r="1.5" fill="#22c55e"/>
                  <circle cx="38" cy="41" r="1.5" fill="#eab308"/>
                </g>
              </svg>
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
