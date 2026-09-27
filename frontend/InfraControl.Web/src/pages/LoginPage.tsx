import { useState } from 'react';
import { Button } from 'primereact/button';
import { InputText } from 'primereact/inputtext';
import { Password } from 'primereact/password';
import { GoogleLogin } from '@react-oauth/google';
import { useDispatch } from 'react-redux';
import { loginStart, loginSuccess, loginFailure } from '../redux/slices/authSlice';
import { useNavigate } from 'react-router-dom';

export function LoginPage() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const dispatch = useDispatch();
  const navigate = useNavigate();

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    dispatch(loginStart());

    try {
      // Chamada para API de login local
      const response = await fetch('/api/v1/auth/login', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ email, password }),
        credentials: 'include', // Importante para cookies
      });

      if (!response.ok) {
        throw new Error('Credenciais inválidas');
      }

      // Obter dados do usuário logado
      const meResponse = await fetch('/api/v1/auth/me', {
        credentials: 'include',
      });

      if (meResponse.ok) {
        const userData = await meResponse.json();
        dispatch(loginSuccess({
          id: userData.id,
          email: userData.email,
          displayName: userData.displayName,
          roles: userData.roles,
          permissions: userData.permissions,
        }));
        navigate('/dashboard');
      }
    } catch (error) {
      dispatch(loginFailure('Falha ao fazer login. Verifique suas credenciais.'));
    } finally {
      setLoading(false);
    }
  };

  const handleGoogleLogin = async (credentialResponse: any) => {
    setLoading(true);
    dispatch(loginStart());

    try {
      // Chamada para API de login Google
      const response = await fetch('/api/v1/auth/google/callback', {
        method: 'POST',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ credential: credentialResponse.credential }),
        credentials: 'include',
      });

      if (!response.ok) {
        throw new Error('Falha no login Google');
      }

      // Obter dados do usuário logado
      const meResponse = await fetch('/api/v1/auth/me', {
        credentials: 'include',
      });

      if (meResponse.ok) {
        const userData = await meResponse.json();
        dispatch(loginSuccess({
          id: userData.id,
          email: userData.email,
          displayName: userData.displayName,
          roles: userData.roles,
          permissions: userData.permissions,
        }));
        navigate('/dashboard');
      }
    } catch (error) {
      dispatch(loginFailure('Falha no login Google. Tente novamente.'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{
      minHeight: '100vh',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      background: 'linear-gradient(135deg, #1e3a8a 0%, #3b82f6 50%, #60a5fa 100%)',
      padding: '20px',
    }}>
      <div style={{
        width: '100%',
        maxWidth: '400px',
        background: 'white',
        borderRadius: '16px',
        padding: '40px',
        boxShadow: '0 20px 60px rgba(0, 0, 0, 0.3)',
      }}>
        {/* Logo/Brand */}
        <div style={{ textAlign: 'center', marginBottom: '32px' }}>
          <div style={{
            width: '80px',
            height: '80px',
            background: 'linear-gradient(135deg, #3b82f6, #8b5cf6)',
            borderRadius: '20px',
            margin: '0 auto 16px',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            boxShadow: '0 8px 24px rgba(59, 130, 246, 0.3)',
          }}>
            <i className="pi pi-cog" style={{ fontSize: '32px', color: 'white' }} />
          </div>
          <h1 style={{
            fontSize: '28px',
            fontWeight: '700',
            color: '#1f2937',
            margin: '0 0 8px',
          }}>
            InfraControl
          </h1>
          <p style={{
            fontSize: '14px',
            color: '#6b7280',
            margin: '0',
          }}>
            Faça login para gerenciar sua infraestrutura
          </p>
        </div>

        {/* Form */}
        <form onSubmit={handleLogin} style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
          <div>
            <label style={{
              display: 'block',
              fontSize: '14px',
              fontWeight: '500',
              color: '#374151',
              marginBottom: '8px',
            }}>
              Email
            </label>
            <InputText
              id="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="seu@email.com"
              style={{
                width: '100%',
                padding: '12px 16px',
                borderRadius: '8px',
                border: '1px solid #d1d5db',
                fontSize: '14px',
              }}
              disabled={loading}
            />
          </div>

          <div>
            <label style={{
              display: 'block',
              fontSize: '14px',
              fontWeight: '500',
              color: '#374151',
              marginBottom: '8px',
            }}>
              Senha
            </label>
            <Password
              id="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="••••••••"
              feedback={false}
              toggleMask
              style={{
                width: '100%',
              }}
              inputStyle={{
                padding: '12px 16px',
                borderRadius: '8px',
                border: '1px solid #d1d5db',
                fontSize: '14px',
              }}
              disabled={loading}
            />
          </div>

          <Button
            type="submit"
            label={loading ? 'Entrando...' : 'Entrar'}
            loading={loading}
            style={{
              width: '100%',
              padding: '14px',
              borderRadius: '8px',
              background: 'linear-gradient(135deg, #3b82f6, #8b5cf6)',
              border: 'none',
              fontSize: '16px',
              fontWeight: '600',
              cursor: loading ? 'not-allowed' : 'pointer',
            }}
            disabled={loading}
          />
        </form>

        {/* Divider */}
        <div style={{
          display: 'flex',
          alignItems: 'center',
          margin: '24px 0',
          color: '#9ca3af',
          fontSize: '14px',
        }}>
          <div style={{ flex: 1, height: '1px', background: '#e5e7eb' }} />
          <span style={{ padding: '0 16px' }}>ou</span>
          <div style={{ flex: 1, height: '1px', background: '#e5e7eb' }} />
        </div>

        {/* Google Login */}
        <div style={{ textAlign: 'center' }}>
          {!loading && (
            <GoogleLogin
              onSuccess={handleGoogleLogin}
              onError={() => dispatch(loginFailure('Falha no login Google'))}
              useOneTap
              theme="outline"
              type="standard"
              size="large"
              text="signin_with"
              shape="rectangular"
            />
          )}
        </div>

        {/* Forgot Password */}
        <div style={{ textAlign: 'center', marginTop: '16px' }}>
          <a
            href="/forgot-password"
            style={{
              fontSize: '14px',
              color: '#3b82f6',
              textDecoration: 'none',
              fontWeight: '500',
            }}
          >
            Esqueceu sua senha?
          </a>
        </div>
      </div>
    </div>
  );
}
