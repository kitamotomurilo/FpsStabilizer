# Assets

## Icon (icon.ico)

O arquivo `icon.ico` deve ser colocado nesta pasta antes de compilar.

### Como criar/obter:

1. **Gerar online**: https://iconverticons.com/online/
2. **Converter PNG para ICO**: https://convertio.co/png-ico/
3. **Tamanhos necessários no .ico**: 16x16, 32x32, 48x48, 256x256

### Placeholder temporário

Para compilar sem ícone personalizado, você pode:
1. Criar um arquivo vazio: `type nul > assets\icon.ico` (não recomendado)
2. Ou comentar a linha `<ApplicationIcon>..\assets\icon.ico</ApplicationIcon>` no `FpsStabilizer.UI.csproj`

### Sugestão de design

- Fundo escuro (#1E1E1E ou transparente)
- Ícone de "FPS" ou monitor com gráfico
- Cores: Azul primário (#007ACC) + Laranja accent (#FF6B35)
- Estilo moderno, flat

Exemplo SVG para converter:
```svg
<svg viewBox="0 0 64 64" xmlns="http://www.w3.org/2000/svg">
  <rect width="64" height="64" rx="12" fill="#1E1E1E"/>
  <text x="32" y="44" font-family="Arial, sans-serif" font-size="28" font-weight="bold" fill="#007ACC" text-anchor="middle">FPS</text>
  <path d="M16 40 L24 32 L32 36 L40 28 L48 20" stroke="#FF6B35" stroke-width="3" fill="none" stroke-linecap="round" stroke-linejoin="round"/>
</svg>
```