# Identidade Visual do InfraControl

## Visão Geral

A identidade visual do InfraControl foi projetada para transmitir profissionalismo, tecnologia e confiança. O design utiliza uma paleta de cores azul tecnológico, tipografia moderna e elementos visuais que representam infraestrutura de TI.

## Paleta de Cores

### Cores Primárias

| Cor | Hex | Uso |
|-----|-----|-----|
| Azul principal | `#3b82f6` | Botões primários, links, ícones ativos |
| Azul escuro | `#2563eb` | Hover states, gradientes |
| Azul profundo | `#1d4ed8` | CTA sections, gradientes escuros |

### Cores de Sucesso

| Cor | Hex | Uso |
|-----|-----|-----|
| Verde claro | `#4ade80` | LEDs de status (online) |
| Verde | `#22c55e` | Estados de sucesso, badges positivos |

### Cores de Aviso

| Cor | Hex | Uso |
|-----|-----|-----|
| Amarelo claro | `#fbbf24` | LEDs de warning |
| Amarelo | `#eab308` | Estados de alerta, badges de aviso |

### Cores de Texto

| Cor | Hex | Uso |
|-----|-----|-----|
| Texto principal | `var(--uni-text)` | Títulos, corpo de texto |
| Texto secundário | `var(--uni-text-secondary)` | Descrições, texto de apoio |
| Texto mudo | `var(--uni-text-muted)` | Meta informações, legenda |

### Cores de Fundo

| Cor | Hex | Uso |
|-----|-----|-----|
| Surface card | `var(--surface-card)` | Fundo de cards, header, footer |
| Background | `var(--uni-bg)` | Fundo de elementos destacados |
| Border | `var(--uni-border)` | Bordas, separadores |

## Tipografia

### Fonte Principal

- **Família**: Inter (Variable Font)
- **Weights**: 400 (regular), 500 (medium), 600 (semibold), 700 (bold)
- **Tamanhos base**:
  - Corpo: 13-16px
  - Títulos: 20-38px
  - Subtítulos: 14-15px

### Escala Tipográfica

| Elemento | Tamanho | Weight | Line-height |
|----------|---------|--------|-------------|
| H1 (hero) | clamp(28px, 5vw, 38px) | 700 | 1.15 |
| H2 (seção) | 22px | 700 | 1.3 |
| H3 (card) | 15px | 600 | 1.4 |
| Corpo | 13-16px | 400 | 1.6 |
| Label | 12px | 500 | 1.4 |
| Caption | 11-12px | 400 | 1.4 |

## Logo

### Descrição

O logo do InfraControl representa infraestrutura de TI através de um server rack com LEDs de status e conexões de rede. O design utiliza gradientes azuis para transmitir tecnologia e profissionalismo.

### Elementos

- **Server rack**: 4 unidades representando servidores/dispositivos
- **LEDs de status**: Verde (online) e amarelo (warning)
- **Conexões de rede**: 3 pontos conectados representando topologia
- **Gradiente**: Azul tecnológico (#3b82f6 → #1d4ed8)

### Tamanhos

| Uso | Tamanho (px) |
|-----|-------------|
| Favicon | 16, 32, 64 |
| Header | 28 |
| Footer | 32 |
| Card/Componente | 24-48 |
| Landing page hero | 64-128 |

### Versões

- **SVG**: Vetorial, escala infinita (usado em favicon.svg)
- **PNG**: Rasterizado para ícones (16x16, 32x32, 180x180)
- **Dark mode**: Cores ajustadas automaticamente via CSS variables

### Regras de Uso

1. Mantenha proporção 1:1
2. Use o SVG sempre que possível para melhor qualidade
3. Mantenha espaço mínimo de respiração (padding de 8px ao redor)
4. Não distorça ou rotacione o logo
5. Use sobre fundo claro ou escuro com contraste adequado

## Componentes de UI

### Botões

| Tipo | Background | Texto | Border | Hover |
|------|------------|-------|--------|-------|
| Primário | `#3b82f6` | Branco | Transparent | `#2563eb` |
| Secundário | Transparent | `#3b82f6` | `#d1d5db` | `#f3f4f6` |
| CTA (gradiente) | Gradiente azul | Branco | Transparent | Elevação + sombra |

### Cards

- **Background**: Surface card
- **Border**: 1px solid border color
- **Border-radius**: 12px
- **Padding**: 20px
- **Hover**: translateY(-4px) + shadow azul

### Ícones

- **Tamanho padrão**: 14-16px
- **Cor**: Azul principal (#3b82f6)
- **Fonte**: PrimeIcons
- **Hover**: Escurece para #1d4ed8

## Animações

### Fade In Up

Usado para entrada de elementos do hero e cards:

```css
@keyframes fadeInUp {
  from {
    opacity: 0;
    transform: translateY(20px);
  }
  to {
    opacity: 1;
    transform: translateY(0);
  }
}
```

### Transições

- **Duração padrão**: 0.3s
- **Easing**: ease-out
- **Propriedades**: transform, box-shadow, border-color, color

### Micro-interações

- **Cards**: Elevação + sombra no hover
- **Botões**: Escurecimento + elevação no hover
- **Links**: Underline + mudança de cor no hover
- **Ícones sociais**: Background azul + elevação no hover

## Espaçamento

### Escala de Espaçamento

| Token | Valor (px) | Uso |
|-------|-----------|-----|
| 4 | 4px | Espaços mínimos |
| 8 | 8px | Ícones, pequenos gaps |
| 12 | 12px | Gaps internos de cards |
| 16 | 16px | Padding de componentes |
| 20 | 20px | Padding de seções |
| 24 | 24px | Gaps entre elementos |
| 32 | 32px | Padding de seções grandes |
| 56-64 | 56-64px | Padding de hero/CTA |

### Grid

- **Gap padrão**: 16px
- **Mobile**: 8-12px
- **Container max-width**: 1080px

## Responsividade

### Breakpoints

| Dispositivo | Largura | Ajustes |
|-------------|---------|---------|
| Desktop | > 720px | Layout completo |
| Tablet | 481-720px | Grid 2 colunas, fontes reduzidos |
| Mobile | ≤ 480px | 1 coluna, menu mobile, fontes menores |

### Ajustes Mobile

- Hero: flex-direction column, text-align center
- Grid: 1-2 colunas
- Padding: reduzido
- Fontes: escala reduzida com clamp()

## Acessibilidade

### Contraste

- WCAG AA: 4.5:1 para texto normal
- WCAG AA: 3:1 para texto grande (>18px)
- Cores verificadas para contraste adequado

### Navegação por Teclado

- Todos os elementos interativos são focáveis
- Focus visível com outline
- Tab order lógico
- Links e botões com ARIA labels quando necessário

### Screen Readers

- Imagens com alt text descritivo
- SVG com role="img" e aria-label
- Headings hierárquicos (h1 → h2 → h3)
- ARIA expanded para elementos accordion

## Dark Mode

### Implementação

- Via CSS variables definidas em `[data-theme="dark"]`
- Tokens: `--uni-text`, `--uni-text-secondary`, `--surface-card`, `--uni-bg`, `--uni-border`
- Toggle no header público

### Ajustes

- Backgrounds escuros (#1a1a1a, #2a2a2a)
- Texto claro (#f3f4f6, #d1d5db)
- Bordas mais sutis
- Gradientes ajustados para dark mode

## Arquivos

### Logo

- `public/favicon.svg` - Logo SVG completo
- `public/favicon-16x16.png` - Ícone 16x16
- `public/favicon-32x32.png` - Ícone 32x32
- `public/apple-touch-icon.png` - Ícone 180x180

### Componentes

- `src/components/ui/Logo.tsx` - Componente React do logo
- `src/components/marketing/PublicLayout.tsx` - Layout público com logo

### Estilos

- `src/styles/pages/public-base.css` - Base layout público
- `src/styles/pages/public-components.css` - Componentes públicos
- `src/styles/pages/home.css` - Estilos específicos da home
- `src/styles/pages/features.css` - Estilos da página de recursos
- `src/styles/pages/contact.css` - Estilos da página de contato

## Manutenção

### Atualizar Cores

1. Modificar tokens CSS em `src/styles/variables.css`
2. Atualizar valores de gradientes em SVG do logo
3. Rebuild do frontend

### Atualizar Logo

1. Editar `src/components/ui/Logo.tsx`
2. Atualizar `public/favicon.svg` com mesmo design
3. Regenerar PNGs via ImageMagick
4. Testar em light/dark mode

### Adicionar Novo Componente

1. Seguir padrão de spacing e tipografia
2. Usar tokens CSS existentes
3. Adicionar hover states
4. Testar responsividade e acessibilidade
