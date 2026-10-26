interface LogoProps {
  size?: number;
  className?: string;
}

/**
 * Logo oficial do InfraControl - representa infraestrutura de TI
 * Server rack com LEDs + conexões de rede + gradientes azuis
 */
export function Logo({ size = 32, className = "" }: LogoProps) {
  return (
    <svg
      viewBox="0 0 64 64"
      width={size}
      height={size}
      className={className}
      role="img"
      aria-label="Logo do InfraControl"
    >
      <defs>
        {/* Gradiente principal - azul tecnológico */}
        <linearGradient id="logoPrimaryGrad" x1="0%" y1="0%" x2="100%" y2="100%">
          <stop offset="0%" style={{ stopColor: "#3b82f6", stopOpacity: 1 }} />
          <stop offset="50%" style={{ stopColor: "#2563eb", stopOpacity: 1 }} />
          <stop offset="100%" style={{ stopColor: "#1d4ed8", stopOpacity: 1 }} />
        </linearGradient>

        {/* Gradiente de fundo - mais claro */}
        <linearGradient id="logoBgGrad" x1="0%" y1="0%" x2="100%" y2="100%">
          <stop offset="0%" style={{ stopColor: "#60a5fa", stopOpacity: 0.3 }} />
          <stop offset="100%" style={{ stopColor: "#3b82f6", stopOpacity: 0.1 }} />
        </linearGradient>

        {/* Gradiente dos LEDs */}
        <linearGradient id="ledGreenGrad" x1="0%" y1="0%" x2="100%" y2="100%">
          <stop offset="0%" style={{ stopColor: "#4ade80", stopOpacity: 1 }} />
          <stop offset="100%" style={{ stopColor: "#22c55e", stopOpacity: 1 }} />
        </linearGradient>

        <linearGradient id="ledAmberGrad" x1="0%" y1="0%" x2="100%" y2="100%">
          <stop offset="0%" style={{ stopColor: "#fbbf24", stopOpacity: 1 }} />
          <stop offset="100%" style={{ stopColor: "#eab308", stopOpacity: 1 }} />
        </linearGradient>

        {/* Glow effect */}
        <filter id="logoGlow" x="-50%" y="-50%" width="200%" height="200%">
          <feGaussianBlur stdDeviation="2" result="coloredBlur" />
          <feMerge>
            <feMergeNode in="coloredBlur" />
            <feMergeNode in="SourceGraphic" />
          </feMerge>
        </filter>
      </defs>

      {/* Background circle com gradiente */}
      <circle cx="32" cy="32" r="30" fill="url(#logoBgGrad)" />

      {/* Server rack principal */}
      <g filter="url(#logoGlow)">
        {/* Rack frame */}
        <rect
          x="14"
          y="8"
          width="36"
          height="48"
          rx="6"
          fill="none"
          stroke="url(#logoPrimaryGrad)"
          strokeWidth="2.5"
        />

        {/* Rack rails */}
        <line x1="14" y1="20" x2="50" y2="20" stroke="url(#logoPrimaryGrad)" strokeWidth="1" opacity="0.3" />
        <line x1="14" y1="32" x2="50" y2="32" stroke="url(#logoPrimaryGrad)" strokeWidth="1" opacity="0.3" />
        <line x1="14" y1="44" x2="50" y2="44" stroke="url(#logoPrimaryGrad)" strokeWidth="1" opacity="0.3" />

        {/* Server units */}
        <rect x="18" y="12" width="28" height="6" rx="2" fill="url(#logoPrimaryGrad)" opacity="0.9" />
        <rect x="18" y="22" width="28" height="6" rx="2" fill="url(#logoPrimaryGrad)" opacity="0.8" />
        <rect x="18" y="32" width="28" height="6" rx="2" fill="url(#logoPrimaryGrad)" opacity="0.7" />
        <rect x="18" y="42" width="28" height="6" rx="2" fill="url(#logoPrimaryGrad)" opacity="0.6" />

        {/* LEDs de status */}
        <circle cx="42" cy="15" r="1.8" fill="url(#ledGreenGrad)" />
        <circle cx="42" cy="25" r="1.8" fill="url(#ledGreenGrad)" />
        <circle cx="42" cy="35" r="1.8" fill="url(#ledAmberGrad)" />
        <circle cx="42" cy="45" r="1.8" fill="url(#ledGreenGrad)" />
      </g>

      {/* Conexões de rede */}
      <g stroke="url(#logoPrimaryGrad)" strokeWidth="1.5" fill="none" opacity="0.6">
        {/* Linha de rede superior */}
        <path d="M 8 20 Q 10 20, 14 20" />
        <circle cx="8" cy="20" r="2" fill="url(#logoPrimaryGrad)" />

        {/* Linha de rede inferior */}
        <path d="M 8 44 Q 10 44, 14 44" />
        <circle cx="8" cy="44" r="2" fill="url(#logoPrimaryGrad)" />

        {/* Linha de rede direita */}
        <path d="M 50 32 Q 54 32, 56 32" />
        <circle cx="56" cy="32" r="2" fill="url(#logoPrimaryGrad)" />
      </g>

      {/* Decorativo: pequenos pontos de dados */}
      <g fill="url(#logoPrimaryGrad)" opacity="0.4">
        <circle cx="24" cy="15" r="1" />
        <circle cx="28" cy="15" r="1" />
        <circle cx="24" cy="25" r="1" />
        <circle cx="28" cy="25" r="1" />
        <circle cx="24" cy="35" r="1" />
        <circle cx="28" cy="35" r="1" />
        <circle cx="24" cy="45" r="1" />
        <circle cx="28" cy="45" r="1" />
      </g>
    </svg>
  );
}
