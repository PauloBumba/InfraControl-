import { useState, type ReactNode } from "react";
import { Link, useLocation } from "react-router-dom";
import { readStoredTheme, toggleTheme, type Theme } from "@/services/theme";

const NAV = [
  { to: "/", label: "Home" },
  { to: "/recursos", label: "Recursos" },
  { to: "/contato", label: "Contato" },
] as const;

const FOOTER_PRODUCT = [
  { to: "/recursos", label: "Recursos" },
  { to: "/recursos", label: "Funcionalidades" },
  { to: "/", label: "Início" },
] as const;

const FOOTER_RESOURCES = [
  { to: "/contato", label: "Contato" },
  { to: "/recursos", label: "Documentação" },
  { to: "/contato", label: "Suporte" },
] as const;

const FOOTER_COMPANY = [
  { to: "/contato", label: "Sobre" },
  { to: "/contato", label: "Contato" },
  { to: "/", label: "Status" },
] as const;

function navLinkClass(active: boolean): string {
  return active ? "uni-public-nav-link uni-public-nav-link-active" : "uni-public-nav-link";
}

function InfraLogo({ size = 28 }: { size?: number }) {
  return (
    <svg viewBox="0 0 64 64" width={size} height={size} role="img" aria-label="Logo do InfraControl">
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
  );
}

export function PublicLayout({ children }: { children: ReactNode }) {
  const { pathname } = useLocation();
  const [theme, setTheme] = useState<Theme>(readStoredTheme);
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);

  const handleToggleTheme = () => {
    setTheme((current) => toggleTheme(current));
  };

  const handleNavClick = () => {
    setMobileMenuOpen(false);
  };

  return (
    <div className="uni-public">
      <header className="uni-public-header">
        <div className="uni-public-container uni-public-bar">
          <Link to="/" className="uni-public-brand" onClick={handleNavClick}>
            <span className="uni-public-logo">
              <InfraLogo size={28} />
            </span>
            <span>InfraControl</span>
          </Link>

          <nav aria-label="Navegação principal" className="uni-public-nav-desktop">
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
            <button
              className="uni-public-theme-toggle"
              onClick={handleToggleTheme}
              aria-label={theme === "dark" ? "Mudar para tema claro" : "Mudar para tema escuro"}
              title={theme === "dark" ? "Tema claro" : "Tema escuro"}
            >
              <i className={`pi ${theme === "dark" ? "pi-sun" : "pi-moon"}`} />
            </button>
            <Link to="/login" className="uni-public-button uni-public-button-small">
              Entrar
            </Link>
            <button
              className="uni-public-mobile-toggle"
              onClick={() => setMobileMenuOpen(!mobileMenuOpen)}
              aria-label={mobileMenuOpen ? "Fechar menu" : "Abrir menu"}
              aria-expanded={mobileMenuOpen}
            >
              <i className={`pi ${mobileMenuOpen ? "pi-times" : "pi-bars"}`} />
            </button>
          </div>
        </div>

        {mobileMenuOpen && (
          <div className="uni-public-mobile-menu">
            <nav aria-label="Navegação móvel">
              <ul className="uni-public-nav">
                {NAV.map((item) => (
                  <li key={item.to}>
                    <Link
                      to={item.to}
                      className={navLinkClass(pathname === item.to)}
                      aria-current={pathname === item.to ? "page" : undefined}
                      onClick={handleNavClick}
                    >
                      {item.label}
                    </Link>
                  </li>
                ))}
              </ul>
            </nav>
          </div>
        )}
      </header>

      <main className="uni-public-main">{children}</main>

      <footer className="uni-public-footer">
        <div className="uni-public-container uni-public-footer-grid">
          <div className="uni-public-footer-brand">
            <div className="uni-public-footer-logo">
              <InfraLogo size={32} />
              <span className="uni-public-footer-brand-text">InfraControl</span>
            </div>
            <p className="uni-public-footer-description">
              Plataforma on-premises de monitoramento e operação de infraestrutura de TI.
            </p>
            <div className="uni-public-social">
              <a href="https://github.com" target="_blank" rel="noopener noreferrer" aria-label="GitHub" className="uni-public-social-link">
                <i className="pi pi-github" />
              </a>
              <a href="https://linkedin.com" target="_blank" rel="noopener noreferrer" aria-label="LinkedIn" className="uni-public-social-link">
                <i className="pi pi-linkedin" />
              </a>
              <a href="https://twitter.com" target="_blank" rel="noopener noreferrer" aria-label="Twitter" className="uni-public-social-link">
                <i className="pi pi-twitter" />
              </a>
            </div>
          </div>

          <div className="uni-public-footer-section">
            <h6>Produto</h6>
            <ul className="uni-public-footer-links">
              {FOOTER_PRODUCT.map((item) => (
                <li key={item.label}>
                  <Link
                    to={item.to}
                    aria-current={pathname === item.to ? "page" : undefined}
                  >{item.label}</Link>
                </li>
              ))}
            </ul>
          </div>

          <div className="uni-public-footer-section">
            <h6>Recursos</h6>
            <ul className="uni-public-footer-links">
              {FOOTER_RESOURCES.map((item) => (
                <li key={item.label}>
                  <Link
                    to={item.to}
                    aria-current={pathname === item.to ? "page" : undefined}
                  >{item.label}</Link>
                </li>
              ))}
            </ul>
          </div>

          <div className="uni-public-footer-section">
            <h6>Empresa</h6>
            <ul className="uni-public-footer-links">
              {FOOTER_COMPANY.map((item) => (
                <li key={item.label}>
                  <Link
                    to={item.to}
                    aria-current={pathname === item.to ? "page" : undefined}
                  >{item.label}</Link>
                </li>
              ))}
            </ul>
          </div>
        </div>

        <div className="uni-public-container uni-public-footer-bottom">
          <small>&copy; {new Date().getFullYear()} InfraControl. Todos os direitos reservados.</small>
          <div className="uni-public-footer-bottom-links">
            <Link to="/contato">Privacidade</Link>
            <Link to="/contato">Termos</Link>
            <small>v0.1-foundation</small>
          </div>
        </div>
      </footer>
    </div>
  );
}

export function PublicHeaderFooterLayout({ children }: { children: ReactNode }) {
  const [theme, setTheme] = useState<Theme>(readStoredTheme);

  const handleToggleTheme = () => {
    setTheme((current) => toggleTheme(current));
  };

  return (
    <div className="uni-public">
      <header className="uni-public-header">
        <div className="uni-public-container uni-public-bar">
          <Link to="/" className="uni-public-brand">
            <span className="uni-public-logo">
              <InfraLogo size={28} />
            </span>
            <span>InfraControl</span>
          </Link>

          <div className="uni-public-bar-actions">
            <button
              className="uni-public-theme-toggle"
              onClick={handleToggleTheme}
              aria-label={theme === "dark" ? "Mudar para tema claro" : "Mudar para tema escuro"}
              title={theme === "dark" ? "Tema claro" : "Tema escuro"}
            >
              <i className={`pi ${theme === "dark" ? "pi-sun" : "pi-moon"}`} />
            </button>
            <Link to="/login" className="uni-public-button uni-public-button-small">
              Entrar
            </Link>
          </div>
        </div>
      </header>

      <main className="uni-public-main" style={{ padding: "48px 0" }}>{children}</main>

      <footer className="uni-public-footer">
        <div className="uni-public-container uni-public-footer-grid">
          <div className="uni-public-footer-brand">
            <div className="uni-public-footer-logo">
              <InfraLogo size={32} />
              <span className="uni-public-footer-brand-text">InfraControl</span>
            </div>
            <p className="uni-public-footer-description">
              Plataforma on-premises de monitoramento e operação de infraestrutura de TI.
            </p>
          </div>

          <div className="uni-public-footer-section">
            <h6>Produto</h6>
            <ul className="uni-public-footer-links">
              {FOOTER_PRODUCT.map((item) => (
                <li key={item.label}>
                  <Link to={item.to}>{item.label}</Link>
                </li>
              ))}
            </ul>
          </div>

          <div className="uni-public-footer-section">
            <h6>Recursos</h6>
            <ul className="uni-public-footer-links">
              {FOOTER_RESOURCES.map((item) => (
                <li key={item.label}>
                  <Link to={item.to}>{item.label}</Link>
                </li>
              ))}
            </ul>
          </div>

          <div className="uni-public-footer-section">
            <h6>Empresa</h6>
            <ul className="uni-public-footer-links">
              {FOOTER_COMPANY.map((item) => (
                <li key={item.label}>
                  <Link to={item.to}>{item.label}</Link>
                </li>
              ))}
            </ul>
          </div>
        </div>

        <div className="uni-public-container uni-public-footer-bottom">
          <small>&copy; {new Date().getFullYear()} InfraControl. Todos os direitos reservados.</small>
          <div className="uni-public-footer-bottom-links">
            <Link to="/contato">Privacidade</Link>
            <Link to="/contato">Termos</Link>
            <small>v0.1-foundation</small>
          </div>
        </div>
      </footer>
    </div>
  );
}
