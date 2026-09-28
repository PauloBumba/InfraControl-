import { useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { AuthApiError, requestPasswordReset } from "@/services/api/auth";

/**
 * Recuperação de senha. A resposta do backend é a MESMA para e-mail
 * desconhecido, inexistente e inativo (anti-enumeração): aqui só mostramos a
 * mensagem genérica, sem indicar o que aconteceu.
 */
export function ForgotPasswordPage() {
  const navigate = useNavigate();
  const [email, setEmail] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [pending, setPending] = useState(false);

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setPending(true);
    setError(null);
    setMessage(null);

    try {
      setMessage(await requestPasswordReset(email.trim()));
    } catch (submitError) {
      setError(
        submitError instanceof AuthApiError
          ? submitError.message
          : "Não foi possível enviar agora. Tente novamente em instantes.",
      );
    } finally {
      setPending(false);
    }
  };

  return (
    <div className="uni-login">
      <div className="uni-login-card">
        <div className="uni-login-brand">
          <div className="uni-login-logo">
            <i className="pi pi-key" />
          </div>
          <h1>Recuperar acesso</h1>
          <p>Informe seu e-mail e enviaremos as instruções de redefinição.</p>
        </div>

        {error && (
          <div className="uni-login-alert" role="alert">
            <i className="pi pi-exclamation-circle" />
            <span>{error}</span>
          </div>
        )}

        {message ? (
          <>
            <div className="uni-login-alert uni-login-alert-success" role="status">
              <i className="pi pi-check-circle" />
              <span>{message}</span>
            </div>
            <button className="uni-login-submit" type="button" onClick={() => navigate("/login")}>
              <i className="pi pi-arrow-left" />
              <span>Voltar para o login</span>
            </button>
          </>
        ) : (
          <form className="uni-login-form" onSubmit={handleSubmit}>
            <label className="uni-login-label" htmlFor="forgot-email">E-mail</label>
            <input
              id="forgot-email"
              className="uni-login-input"
              type="email"
              name="email"
              autoComplete="email"
              placeholder="voce@empresa.com"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
              disabled={pending}
              required
            />

            <button className="uni-login-submit" type="submit" disabled={pending}>
              {pending ? <i className="pi pi-spin pi-spinner" /> : <i className="pi pi-envelope" />}
              <span>{pending ? "Enviando..." : "Enviar instruções"}</span>
            </button>

            <button className="uni-login-link uni-login-link-center" type="button" onClick={() => navigate("/login")}>
              Voltar para o login
            </button>
          </form>
        )}
      </div>
    </div>
  );
}
