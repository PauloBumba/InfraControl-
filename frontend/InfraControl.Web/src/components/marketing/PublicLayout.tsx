import { useState, type ReactNode } from "react";
import { Link, useLocation } from "react-router-dom";
import { readStoredTheme, toggleTheme, type Theme } from "@/services/theme";
import { getSupportEmail } from "@/services/support";
import { Logo } from "@/components/ui/Logo";

/** Repositório real — o rodapé antigo apontava para github.com genérico. */
const REPO_URL = "https://github.com/PauloBumba/InfraControl-";

const NAV = [
  { to: "/", label: "Home" },
  { to: "/recursos", label: "Recursos" },
  { to: "/contato", label: "Contato" },
] as const;

const FOOTER_PRODUCT = [
  { to: "/", label: "Início" },
  { to: "/recursos", label: "Recursos" },
  { to: "/contato", label: "Contato" },
] as const;

/* Links externos do repositório — existem para qualquer repo do GitHub,
   nada de "Status"/"Sobre" inventados apontando para a home. */
const FOOTER_CODE = [
  { href: REPO_URL, label: "Código-fonte" },
  { href: `${REPO_URL}/issues`, label: "Issues" },
  { href: `${REPO_URL}/releases`, label: "Releases" },
] as const;

function navLinkClass(active: boolean): string {
  return active ? "uni-public-nav-link uni-public-nav-link-active" : "uni-public-nav-link";
}

export function PublicLayout({ children }: { children: ReactNode }) {
  const { pathname } = useLocation();
  const [theme, setTheme] = useState<Theme>(readStoredTheme);
  const [mobileMenuOpen, setMobileMenuOpen] = useState(false);
  const supportEmail = getSupportEmail();

  // Não mostrar botão "Entrar" quando já estiver na página de login
  const isLoginPage = pathname === "/login";

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
              <Logo size={28} />
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
            {!isLoginPage && (
              <Link to="/login" className="uni-public-button uni-public-button-small">
                Entrar
              </Link>
            )}
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
                {!isLoginPage && (
                  <li>
                    <Link
                      to="/login"
                      className={navLinkClass(pathname === "/login")}
                      aria-current={pathname === "/login" ? "page" : undefined}
                      onClick={handleNavClick}
                    >
                      Entrar
                    </Link>
                  </li>
                )}
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
              <Logo size={32} />
              <span className="uni-public-footer-brand-text">InfraControl</span>
            </div>
            <p className="uni-public-footer-description">
              Plataforma on-premises de monitoramento e operação de infraestrutura de TI.
            </p>
            <div className="uni-public-social">
              <a href={REPO_URL} target="_blank" rel="noopener noreferrer" aria-label="GitHub" className="uni-public-social-link">
                <i className="pi pi-github" aria-hidden="true" />
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
            <h6>Código</h6>
            <ul className="uni-public-footer-links">
              {FOOTER_CODE.map((item) => (
                <li key={item.label}>
                  <a href={item.href} target="_blank" rel="noopener noreferrer">{item.label}</a>
                </li>
              ))}
            </ul>
          </div>

          <div className="uni-public-footer-section">
            <h6>Contato</h6>
            <ul className="uni-public-footer-links">
              <li>
                <Link to="/contato" aria-current={pathname === "/contato" ? "page" : undefined}>
                  Falar com a gente
                </Link>
              </li>
              {supportEmail ? (
                <li>
                  <a href={`mailto:${supportEmail}`}>{supportEmail}</a>
                </li>
              ) : null}
            </ul>
            {supportEmail ? null : (
              <p className="uni-public-footer-note">
                Sem canal público de e-mail: esta instância é self-hosted — fale com o
                administrador.
              </p>
            )}
          </div>
        </div>

        <div className="uni-public-container uni-public-footer-bottom">
          <small>&copy; {new Date().getFullYear()} InfraControl. Todos os direitos reservados.</small>
          <div className="uni-public-footer-bottom-links">
            <small>Self-hosted · nenhum dado sai da sua rede</small>
          </div>
        </div>
      </footer>
    </div>
  );
}
